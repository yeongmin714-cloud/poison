using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Phase 33 UI-01: 그라디언트 배경 렌더러.
    /// 4가지 모드(Vertical2Color, Horizontal2Color, Radial, Vertical4Color)로 배경 텍스처를 생성합니다.
    /// </summary>
    public static class GradientBackgroundRenderer
    {
        // ================================================================
        // 그라디언트 모드 열거형
        // ================================================================

        public enum GradientMode
        {
            Vertical2Color,
            Horizontal2Color,
            Radial,
            Vertical4Color
        }

        // ================================================================
        // 캐싱
        // ================================================================

        private static Texture2D _cachedGradientTex;
        private static GradientMode _lastMode;
        private static Color _lastC1;
        private static Color _lastC2;
        private static Color _lastC3;
        private static Color _lastC4;
        private static int _lastWidth;
        private static int _lastHeight;

        // ================================================================
        // 공개 API
        // ================================================================

        /// <summary>
        /// 지정된 모드와 색상으로 그라디언트 텍스처를 생성하고 GUI.DrawTexture로 렌더링합니다.
        /// 2색 및 4색 모드를 지원합니다.
        /// </summary>
        public static void DrawGradientBackground(Rect rect, GradientMode mode, Color c1, Color c2, Color c3 = default, Color c4 = default)
        {
            if (rect.width <= 0 || rect.height <= 0)
                return;

            int w = (int)rect.width;
            int h = (int)rect.height;

            // 4색 모드 캐시 키 확장
            if (mode == GradientMode.Vertical4Color)
            {
                if (_cachedGradientTex == null || w != _lastWidth || h != _lastHeight ||
                    mode != _lastMode || !c1.Equals(_lastC1) || !c2.Equals(_lastC2) ||
                    !c3.Equals(_lastC3) || !c4.Equals(_lastC4))
                {
                    if (_cachedGradientTex != null)
                        Object.DestroyImmediate(_cachedGradientTex);

                    _cachedGradientTex = GenerateGradientTexture4Color(w, h, c1, c2, c3, c4);
                    _lastWidth = w;
                    _lastHeight = h;
                    _lastMode = mode;
                    _lastC1 = c1;
                    _lastC2 = c2;
                    _lastC3 = c3;
                    _lastC4 = c4;
                }
            }
            else
            {
                // 캐시된 텍스처가 같은 파라미터와 일치하면 재사용
                if (_cachedGradientTex == null || w != _lastWidth || h != _lastHeight ||
                    mode != _lastMode || !c1.Equals(_lastC1) || !c2.Equals(_lastC2))
                {
                    // 이전 텍스처 해제
                    if (_cachedGradientTex != null)
                        Object.DestroyImmediate(_cachedGradientTex);

                    _cachedGradientTex = GenerateGradientTexture(w, h, mode, c1, c2);
                    _lastWidth = w;
                    _lastHeight = h;
                    _lastMode = mode;
                    _lastC1 = c1;
                    _lastC2 = c2;
                }
            }

            if (_cachedGradientTex != null)
                GUI.DrawTexture(rect, _cachedGradientTex, ScaleMode.StretchToFill);
        }

        /// <summary>
        /// 세로 방향 2색 그라디언트 텍스처를 생성합니다.
        /// </summary>
        public static Texture2D Vertical2Color(Color top, Color bottom, int width = 256, int height = 256)
        {
            return GenerateGradientTexture(width, height, GradientMode.Vertical2Color, top, bottom);
        }

        /// <summary>
        /// 가로 방향 2색 그라디언트 텍스처를 생성합니다.
        /// </summary>
        public static Texture2D Horizontal2Color(Color left, Color right, int width = 256, int height = 256)
        {
            return GenerateGradientTexture(width, height, GradientMode.Horizontal2Color, left, right);
        }

        /// <summary>
        /// 방사형 그라디언트 텍스처를 생성합니다.
        /// </summary>
        public static Texture2D Radial(Color center, Color edge, int width = 256, int height = 256)
        {
            return GenerateGradientTexture(width, height, GradientMode.Radial, center, edge);
        }

        /// <summary>
        /// 4색 세로 방향 그라디언트 텍스처를 생성합니다.
        /// 위에서 아래로 c1→c2→c3→c4 순서로 블렌딩됩니다.
        /// </summary>
        public static Texture2D Vertical4Color(Color top, Color topMid, Color botMid, Color bottom, int width = 256, int height = 256)
        {
            return GenerateGradientTexture4Color(width, height, top, topMid, botMid, bottom);
        }

        // ================================================================
        // 내부 생성 로직
        // ================================================================

        private static Texture2D GenerateGradientTexture(int width, int height, GradientMode mode, Color c1, Color c2)
        {
            if (width <= 0 || height <= 0)
                return null;

            // 1픽셀인 경우 div-by-zero 방지
            int safeW = Mathf.Max(width, 2);
            int safeH = Mathf.Max(height, 2);

            var tex = new Texture2D(width, height);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float t;
                    switch (mode)
                    {
                        case GradientMode.Vertical2Color:
                            t = (float)y / (safeH - 1);
                            break;
                        case GradientMode.Horizontal2Color:
                            t = (float)x / (safeW - 1);
                            break;
                        case GradientMode.Radial:
                            float cx = (float)x / (safeW - 1) - 0.5f;
                            float cy = (float)y / (safeH - 1) - 0.5f;
                            t = Mathf.Sqrt(cx * cx * 4f + cy * cy * 4f);
                            t = Mathf.Clamp01(t);
                            break;
                        default:
                            t = 0f;
                            break;
                    }

                    Color pixel = Color.Lerp(c1, c2, t);
                    tex.SetPixel(x, y, pixel);
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 4색 세로 방향 그라디언트 텍스처를 생성합니다.
        /// 위(top) → topMid → botMid → 아래(bottom)로 자연스럽게 블렌딩됩니다.
        /// </summary>
        private static Texture2D GenerateGradientTexture4Color(int width, int height, Color top, Color topMid, Color botMid, Color bottom)
        {
            if (width <= 0 || height <= 0)
                return null;

            int safeH = Mathf.Max(height, 2);
            int safeW = Mathf.Max(width, 2);

            var tex = new Texture2D(width, height);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (safeH - 1); // 0 = top, 1 = bottom
                Color pixel;

                if (t < 0.5f)
                {
                    // top → topMid (0 ~ 0.5)
                    float localT = t / 0.5f;
                    pixel = Color.Lerp(top, topMid, localT);
                }
                else
                {
                    // topMid → botMid → bottom (0.5 ~ 1.0)
                    float localT = (t - 0.5f) / 0.5f;
                    if (localT < 0.5f)
                    {
                        pixel = Color.Lerp(topMid, botMid, localT * 2f);
                    }
                    else
                    {
                        pixel = Color.Lerp(botMid, bottom, (localT - 0.5f) * 2f);
                    }
                }

                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, pixel);
                }
            }
            tex.Apply();
            return tex;
        }
    }
}