#pragma warning disable 0414
#nullable disable
using System.Collections;
using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Phase 33 UI-01: 창 애니메이션 프로필.
    /// UIWindow에서 사용할 8종 Open/Close 애니메이션 코루틴을 제공합니다.
    /// </summary>
    public static class WindowAnimationProfile
    {
        // ================================================================
        // 공개 API: 애니메이션 코루틴 반환
        // ================================================================

        /// <summary>
        /// 지정된 AnimationType의 열기 코루틴을 반환합니다.
        /// </summary>
        public static IEnumerator GetOpenAnimation(UIDesignTheme.AnimationType type,
            CanvasGroup canvasGroup, RectTransform rectTransform,
            CanvasGroup dimCG, float dimAlpha,
            float duration, float slideOffset)
        {
            switch (type)
            {
                case UIDesignTheme.AnimationType.FadeSlide:
                    return OpenFadeSlide(canvasGroup, rectTransform, dimCG, dimAlpha, duration, slideOffset);
                case UIDesignTheme.AnimationType.Scale:
                    return OpenScale(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Flip:
                    return OpenFlip(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Shatter:
                    return OpenShatter(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Spin:
                    return OpenSpin(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Pop:
                    return OpenPop(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Bounce:
                    return OpenBounce(canvasGroup, rectTransform, dimCG, dimAlpha, duration, slideOffset);
                case UIDesignTheme.AnimationType.Expand:
                    return OpenExpand(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Reveal:
                    return OpenReveal(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Zoom:
                    return OpenZoom(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                default:
                    return OpenFadeSlide(canvasGroup, rectTransform, dimCG, dimAlpha, duration, slideOffset);
            }
        }

        /// <summary>
        /// 지정된 AnimationType의 닫기 코루틴을 반환합니다.
        /// </summary>
        public static IEnumerator GetCloseAnimation(UIDesignTheme.AnimationType type,
            CanvasGroup canvasGroup, RectTransform rectTransform,
            CanvasGroup dimCG, float dimAlpha,
            float duration, float slideOffset)
        {
            switch (type)
            {
                case UIDesignTheme.AnimationType.FadeSlide:
                    return CloseFadeSlide(canvasGroup, rectTransform, dimCG, dimAlpha, duration, slideOffset);
                case UIDesignTheme.AnimationType.Scale:
                    return CloseScale(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Flip:
                    return CloseFlip(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Shatter:
                    return CloseShatter(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Spin:
                    return CloseSpin(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Pop:
                    return ClosePop(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Bounce:
                    return CloseBounce(canvasGroup, rectTransform, dimCG, dimAlpha, duration, slideOffset);
                case UIDesignTheme.AnimationType.Expand:
                    return CloseExpand(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Reveal:
                    return CloseReveal(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                case UIDesignTheme.AnimationType.Zoom:
                    return CloseZoom(canvasGroup, rectTransform, dimCG, dimAlpha, duration);
                default:
                    return CloseFadeSlide(canvasGroup, rectTransform, dimCG, dimAlpha, duration, slideOffset);
            }
        }

        // ================================================================
        // 열기 애니메이션
        // ================================================================

        /// <summary>FadeSlide: alpha 0→1 + y slide up</summary>
        private static IEnumerator OpenFadeSlide(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur, float slideOffset)
        {
            Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            cg.alpha = 0f;
            if (rt != null)
            {
                Vector2 slidePos = startPos;
                slidePos.y -= slideOffset;
                rt.anchoredPosition = slidePos;
            }
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = t;
                if (rt != null)
                    rt.anchoredPosition = Vector2.Lerp(startPos - new Vector2(0, slideOffset), startPos, t);
                if (dimCG != null) dimCG.alpha = t * dimAlpha;
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.anchoredPosition = startPos;
            if (dimCG != null) dimCG.alpha = dimAlpha;
        }

        /// <summary>Scale: 0.8→1.0 scale</summary>
        private static IEnumerator OpenScale(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            float elapsed = 0f;
            cg.alpha = 0f;
            if (rt != null) rt.localScale = startScale * 0.8f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = t;
                if (rt != null) rt.localScale = Vector3.Lerp(startScale * 0.8f, startScale, t);
                if (dimCG != null) dimCG.alpha = t * dimAlpha;
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.localScale = startScale;
            if (dimCG != null) dimCG.alpha = dimAlpha;
        }

        /// <summary>Flip: X축 회전 90→0</summary>
        private static IEnumerator OpenFlip(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            float elapsed = 0f;
            cg.alpha = 0f;
            if (rt != null) rt.localRotation = Quaternion.Euler(90f, 0f, 0f);
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = t;
                if (rt != null) rt.localRotation = Quaternion.Euler(Mathf.Lerp(90f, 0f, t), 0f, 0f);
                if (dimCG != null) dimCG.alpha = t * dimAlpha;
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.localRotation = Quaternion.identity;
            if (dimCG != null) dimCG.alpha = dimAlpha;
        }

        /// <summary>Shatter: 여러 조각으로 흩어졌다 모임 (페이드 인)</summary>
        private static IEnumerator OpenShatter(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            // Shatter: alpha 0→1 + scale overshoot (1.1→1.0)
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            float elapsed = 0f;
            cg.alpha = 0f;
            if (rt != null) rt.localScale = startScale * 1.1f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                // Overshoot: 1.1 → 1.0 (ease out)
                float scaleT = 1f - Mathf.Pow(1f - t, 2f); // ease out quad
                cg.alpha = t;
                if (rt != null) rt.localScale = Vector3.Lerp(startScale * 1.1f, startScale, scaleT);
                if (dimCG != null) dimCG.alpha = t * dimAlpha;
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.localScale = startScale;
            if (dimCG != null) dimCG.alpha = dimAlpha;
        }

        /// <summary>Spin: Z축 360도 회전</summary>
        private static IEnumerator OpenSpin(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            float elapsed = 0f;
            cg.alpha = 0f;
            if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, 360f);
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = t;
                if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(360f, 0f, t));
                if (dimCG != null) dimCG.alpha = t * dimAlpha;
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.localRotation = Quaternion.identity;
            if (dimCG != null) dimCG.alpha = dimAlpha;
        }

        /// <summary>Bounce: y축 바운스 (아래→위→약간 아래→목표)</summary>
        private static IEnumerator OpenBounce(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur, float slideOffset)
        {
            Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            float elapsed = 0f;
            cg.alpha = 0f;
            if (rt != null)
            {
                Vector2 bounceStart = startPos;
                bounceStart.y -= slideOffset * 1.5f;
                rt.anchoredPosition = bounceStart;
            }
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = t;
                if (rt != null)
                {
                    // 바운스: overshoot + settle
                    float yOffset = slideOffset * 1.5f * (1f - t) - Mathf.Sin(t * Mathf.PI * 3f) * slideOffset * 0.3f * (1f - t);
                    rt.anchoredPosition = new Vector2(startPos.x, startPos.y - Mathf.Abs(yOffset));
                }
                if (dimCG != null) dimCG.alpha = t * dimAlpha;
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.anchoredPosition = startPos;
            if (dimCG != null) dimCG.alpha = dimAlpha;
        }

        /// <summary>
        /// Pop: 0.3→1.0 overshoot 스케일 + 약간의 바운스 (탄력적 등장)
        /// </summary>
        private static IEnumerator OpenPop(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            float elapsed = 0f;
            cg.alpha = 0f;
            if (rt != null) rt.localScale = startScale * 0.3f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = Mathf.Clamp01(t * 1.5f); // 빠르게 페이드 인
                if (rt != null)
                {
                    // Overshoot scale: 0.3 → 1.15 → 0.95 → 1.0
                    float scaleT;
                    if (t < 0.6f)
                        scaleT = Mathf.Lerp(0.3f, 1.15f, t / 0.6f);
                    else if (t < 0.85f)
                        scaleT = Mathf.Lerp(1.15f, 0.95f, (t - 0.6f) / 0.25f);
                    else
                        scaleT = Mathf.Lerp(0.95f, 1.0f, (t - 0.85f) / 0.15f);
                    rt.localScale = startScale * scaleT;
                }
                if (dimCG != null) dimCG.alpha = t * dimAlpha;
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.localScale = startScale;
            if (dimCG != null) dimCG.alpha = dimAlpha;
        }

        /// <summary>
        /// Expand: 중앙에서 좌우로 확장 (width 애니메이션 + X축 스케일)
        /// </summary>
        private static IEnumerator OpenExpand(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            float elapsed = 0f;
            cg.alpha = 0f;
            if (rt != null) rt.localScale = new Vector3(0.01f, startScale.y, startScale.z);
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = t;
                if (rt != null)
                {
                    // X축만 확장: 0.01 → 1.0 (ease out)
                    float scaleX = 1f - Mathf.Pow(1f - t, 3f);
                    rt.localScale = new Vector3(Mathf.Lerp(0.01f, startScale.x, scaleX), startScale.y, startScale.z);
                }
                if (dimCG != null) dimCG.alpha = t * dimAlpha;
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.localScale = startScale;
            if (dimCG != null) dimCG.alpha = dimAlpha;
        }

        /// <summary>Reveal: 좌→우 리빌 (width 애니메이션)</summary>
        private static IEnumerator OpenReveal(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector2 startSizeDelta = rt != null ? rt.sizeDelta : Vector2.zero;
            float elapsed = 0f;
            cg.alpha = 1f;
            if (rt != null) rt.sizeDelta = new Vector2(0, startSizeDelta.y);
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                if (rt != null) rt.sizeDelta = new Vector2(Mathf.Lerp(0, startSizeDelta.x, t), startSizeDelta.y);
                if (dimCG != null) dimCG.alpha = t * dimAlpha;
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.sizeDelta = startSizeDelta;
            if (dimCG != null) dimCG.alpha = dimAlpha;
        }

        /// <summary>Zoom: 0.5→1.0 확대</summary>
        private static IEnumerator OpenZoom(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            float elapsed = 0f;
            cg.alpha = 0f;
            if (rt != null) rt.localScale = startScale * 0.5f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = t;
                if (rt != null) rt.localScale = Vector3.Lerp(startScale * 0.5f, startScale, t);
                if (dimCG != null) dimCG.alpha = t * dimAlpha;
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.localScale = startScale;
            if (dimCG != null) dimCG.alpha = dimAlpha;
        }

        // ================================================================
        // 닫기 애니메이션 (역방향)
        // ================================================================

        /// <summary>FadeSlide: alpha 1→0 + y slide down</summary>
        private static IEnumerator CloseFadeSlide(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur, float slideOffset)
        {
            Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = 1f - t;
                if (rt != null) rt.anchoredPosition = Vector2.Lerp(startPos, startPos + new Vector2(0, slideOffset), t);
                if (dimCG != null) dimCG.alpha = (1f - t) * dimAlpha;
                yield return null;
            }
            cg.alpha = 0f;
            if (dimCG != null) dimCG.alpha = 0f;
        }

        /// <summary>Scale: 1.0→0.8 scale</summary>
        private static IEnumerator CloseScale(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = 1f - t;
                if (rt != null) rt.localScale = Vector3.Lerp(startScale, startScale * 0.8f, t);
                if (dimCG != null) dimCG.alpha = (1f - t) * dimAlpha;
                yield return null;
            }
            cg.alpha = 0f;
            if (rt != null) rt.localScale = startScale * 0.8f;
            if (dimCG != null) dimCG.alpha = 0f;
        }

        /// <summary>Flip: X축 0→90</summary>
        private static IEnumerator CloseFlip(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = 1f - t;
                if (rt != null) rt.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 90f, t), 0f, 0f);
                if (dimCG != null) dimCG.alpha = (1f - t) * dimAlpha;
                yield return null;
            }
            cg.alpha = 0f;
            if (rt != null) rt.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (dimCG != null) dimCG.alpha = 0f;
        }

        /// <summary>Shatter: 파편 흩어짐 (페이드 아웃)</summary>
        private static IEnumerator CloseShatter(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = 1f - t;
                if (rt != null) rt.localScale = Vector3.Lerp(startScale, startScale * 0.9f + Vector3.one * 0.1f * t, t);
                if (dimCG != null) dimCG.alpha = (1f - t) * dimAlpha;
                yield return null;
            }
            cg.alpha = 0f;
            if (dimCG != null) dimCG.alpha = 0f;
        }

        /// <summary>Spin: Z축 0→360</summary>
        private static IEnumerator CloseSpin(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = 1f - t;
                if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 360f, t));
                if (dimCG != null) dimCG.alpha = (1f - t) * dimAlpha;
                yield return null;
            }
            cg.alpha = 0f;
            if (dimCG != null) dimCG.alpha = 0f;
        }

        /// <summary>Bounce: y축 바운스 아웃</summary>
        private static IEnumerator CloseBounce(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur, float slideOffset)
        {
            Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = 1f - t;
                if (rt != null)
                {
                    float yOffset = slideOffset * t - Mathf.Sin(t * Mathf.PI * 3f) * slideOffset * 0.2f * (1f - t);
                    rt.anchoredPosition = new Vector2(startPos.x, startPos.y + Mathf.Abs(yOffset));
                }
                if (dimCG != null) dimCG.alpha = (1f - t) * dimAlpha;
                yield return null;
            }
            cg.alpha = 0f;
            if (dimCG != null) dimCG.alpha = 0f;
        }

        /// <summary>Pop: 1.0→0.3 축소 + 빠른 페이드</summary>
        private static IEnumerator ClosePop(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = 1f - Mathf.Clamp01(t * 1.5f);
                if (rt != null)
                    rt.localScale = Vector3.Lerp(startScale, startScale * 0.3f, t);
                if (dimCG != null) dimCG.alpha = (1f - t) * dimAlpha;
                yield return null;
            }
            cg.alpha = 0f;
            if (rt != null) rt.localScale = startScale * 0.3f;
            if (dimCG != null) dimCG.alpha = 0f;
        }

        /// <summary>Expand: X축 축소 (우→좌)</summary>
        private static IEnumerator CloseExpand(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = 1f - t;
                if (rt != null)
                {
                    float scaleX = 1f - Mathf.Pow(t, 2f);
                    rt.localScale = new Vector3(Mathf.Lerp(startScale.x, 0.01f, 1f - scaleX), startScale.y, startScale.z);
                }
                if (dimCG != null) dimCG.alpha = (1f - t) * dimAlpha;
                yield return null;
            }
            cg.alpha = 0f;
            if (rt != null) rt.localScale = new Vector3(0.01f, startScale.y, startScale.z);
            if (dimCG != null) dimCG.alpha = 0f;
        }

        /// <summary>Reveal: 우→좌 축소</summary>
        private static IEnumerator CloseReveal(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector2 startSizeDelta = rt != null ? rt.sizeDelta : Vector2.zero;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                if (rt != null) rt.sizeDelta = new Vector2(Mathf.Lerp(startSizeDelta.x, 0, t), startSizeDelta.y);
                if (dimCG != null) dimCG.alpha = (1f - t) * dimAlpha;
                yield return null;
            }
            cg.alpha = 0f;
            if (dimCG != null) dimCG.alpha = 0f;
        }

        /// <summary>Zoom: 1.0→0.5 축소</summary>
        private static IEnumerator CloseZoom(CanvasGroup cg, RectTransform rt,
            CanvasGroup dimCG, float dimAlpha, float dur)
        {
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                cg.alpha = 1f - t;
                if (rt != null) rt.localScale = Vector3.Lerp(startScale, startScale * 0.5f, t);
                if (dimCG != null) dimCG.alpha = (1f - t) * dimAlpha;
                yield return null;
            }
            cg.alpha = 0f;
            if (rt != null) rt.localScale = startScale * 0.5f;
            if (dimCG != null) dimCG.alpha = 0f;
        }
    }
}