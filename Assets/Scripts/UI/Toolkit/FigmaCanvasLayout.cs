using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Maps absolute Figma canvas pixel rectangles onto the currently resolved UI Toolkit root.
    /// This is intentionally independent of PanelSettings and existing layout helpers.
    /// </summary>
    public static class FigmaCanvasLayout
    {
        public const float CanvasWidth = 1920f;
        public const float CanvasHeight = 1080f;
        public static readonly Vector2 CanonicalCanvasSize = new Vector2(CanvasWidth, CanvasHeight);

        /// <summary>
        /// Scales all rectangle coordinates and dimensions independently against the root size.
        /// Returns the input unchanged when a usable root size is not supplied.
        /// </summary>
        public static Rect ScaleRect(Rect figmaRect, Vector2 rootSize)
        {
            if (!IsUsableDimension(rootSize.x) || !IsUsableDimension(rootSize.y))
                return figmaRect;

            float scaleX = rootSize.x / CanvasWidth;
            float scaleY = rootSize.y / CanvasHeight;
            return new Rect(
                figmaRect.x * scaleX,
                figmaRect.y * scaleY,
                figmaRect.width * scaleX,
                figmaRect.height * scaleY);
        }

        /// <summary>
        /// Creates and attaches a positioned element under UIRoot. Returns null until the root
        /// exists and has resolved, non-zero dimensions.
        /// </summary>
        public static VisualElement Place(Rect figmaRect)
        {
            VisualElement root = UIToolkitBootstrap.UIRoot;
            if (root == null)
                return null;

            Vector2 rootSize = new Vector2(root.resolvedStyle.width, root.resolvedStyle.height);
            if (!IsUsableDimension(rootSize.x) || !IsUsableDimension(rootSize.y))
                return null;

            Rect localRect = ScaleRect(figmaRect, rootSize);
            var element = new VisualElement();
            element.style.position = Position.Absolute;
            element.style.left = localRect.x;
            element.style.top = localRect.y;
            element.style.width = localRect.width;
            element.style.height = localRect.height;
            root.Add(element);
            return element;
        }

        /// <summary>
        /// Applies Figma canvas bounds to an existing element using the canvas root's resolved size.
        /// The element remains under its current parent; this method never attaches or reparents it.
        /// </summary>
        /// <returns>True when the bounds were applied; false when an argument or root size is unusable.</returns>
        public static bool Apply(VisualElement element, Rect figmaRect, VisualElement canvasRoot)
        {
            if (element == null || canvasRoot == null)
                return false;

            Vector2 rootSize = new Vector2(canvasRoot.resolvedStyle.width, canvasRoot.resolvedStyle.height);
            if (!IsUsableDimension(rootSize.x) || !IsUsableDimension(rootSize.y))
                return false;

            Rect localRect = ScaleRect(figmaRect, rootSize);
            element.style.position = Position.Absolute;
            element.style.left = localRect.x;
            element.style.top = localRect.y;
            element.style.width = localRect.width;
            element.style.height = localRect.height;
            return true;
        }

        /// <summary>
        /// Computes the uniform (isotropic) design-space scale for the 1920x1080 Figma canvas:
        /// k = min(rootWidth / 1920, rootHeight / 1080). The minimum guarantees the whole canvas
        /// always fits the root without any aspect-ratio stretch. Returns 0f for unusable sizes.
        /// </summary>
        public static float DesignScale(Vector2 rootSize)
        {
            if (!IsUsableDimension(rootSize.x) || !IsUsableDimension(rootSize.y))
                return 0f;

            return Mathf.Min(rootSize.x / CanvasWidth, rootSize.y / CanvasHeight);
        }

        /// <summary>
        /// Applies the design-space contract: the window box is the Figma bounds scaled by the
        /// uniform scale k, while the design space (content container) keeps the raw Figma pixel
        /// size and is scaled with style.scale = k from its top-left origin. Children of the
        /// design space therefore keep their authored Figma coordinates unchanged.
        /// The window remains under its current parent; this method never attaches or reparents it.
        /// </summary>
        /// <returns>True when the design space was applied; false when an argument or root size is unusable.</returns>
        public static bool ApplyDesignSpace(VisualElement window, VisualElement designSpace, Rect figmaBounds, VisualElement canvasRoot)
        {
            if (window == null || designSpace == null || canvasRoot == null)
                return false;

            Vector2 rootSize = new Vector2(canvasRoot.resolvedStyle.width, canvasRoot.resolvedStyle.height);
            float scale = DesignScale(rootSize);
            if (scale <= 0f)
            {
                // [FigmaV3 진단+수리] 루트 미해석(부착 직후/부팅 초) — 조용한 실패 대신 경고 후
                // 100ms 간격 최대 5회 자동 재시도(클로저 카운터 — 성공 시 중단). 스테일 판정은 로그로 관측.
                Debug.LogWarning($"[FigmaV3] ApplyDesignSpace FAILED (root unresolved {rootSize}) win={window.name} rect={figmaBounds} — 재시도 예약");
                int attempts = 0;
                window.schedule.Execute(() =>
                {
                    if (ApplyDesignSpace(window, designSpace, figmaBounds, canvasRoot)) return;
                    attempts++;
                    if (attempts < 5)
                        window.schedule.Execute(() => ApplyDesignSpace(window, designSpace, figmaBounds, canvasRoot)).StartingIn(100);
                }).StartingIn(100);
                return false;
            }
            // [FigmaV3 진단] 윈도우+rect별 k 변화 시만 로그(폴링 재적용 스팸 방지).
            string sig = $"{window.name}|{figmaBounds.x:F1},{figmaBounds.y:F1},{figmaBounds.width:F1},{figmaBounds.height:F1}";
            if (!LastLoggedK.TryGetValue(sig, out float prevK) || Mathf.Abs(prevK - scale) > 0.0005f)
            {
                LastLoggedK[sig] = scale;
                Debug.Log($"[FigmaV3] {window.name}: ok k={scale:F3} root={rootSize} rect=({figmaBounds.x},{figmaBounds.y},{figmaBounds.width},{figmaBounds.height})");
            }

            window.style.position = Position.Absolute;
            window.style.left = figmaBounds.x * scale;
            window.style.top = figmaBounds.y * scale;
            window.style.width = figmaBounds.width * scale;
            window.style.height = figmaBounds.height * scale;

            designSpace.style.width = figmaBounds.width;
            designSpace.style.height = figmaBounds.height;
            designSpace.style.scale = new Scale(new Vector2(scale, scale));
            designSpace.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0));
            return true;
        }

        private static bool IsUsableDimension(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>[FigmaV3 진단] 윈도우+rect별 마지막 로그 k — 변화 시만 로그(스팸 방지).</summary>
        private static readonly System.Collections.Generic.Dictionary<string, float> LastLoggedK = new();
    }
}
