#nullable disable
using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Phase 33 UI-01: 그라디언트 배경 렌더러.
    /// 3가지 모드(Vertical2Color, Horizontal2Color, Radial)로 배경 텍스처를 생성합니다.
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
            Radial
        }

        // ================================================================
        // 캐싱
        // ================================================================

        private static Texture2D _cachedGradientTex;

        // ================================================================
        // 공개 API
        // ================================================================

        /// <summary>
        /// 지정된 모드와 색상으로 그라디언트 텍스처를 생성하고 GUI.DrawTexture로 렌더링합니다.
        /// </summary>
        public static void DrawGradientBackground(Rect rect, GradientMode mode, Color c1, Color c2)
        {
            if (rect.width <= 0 || rect.height <= 0)
                return;

            _cachedGradientTex = GenerateGradientTexture((int)rect.width, (int)rect.height, mode, c1, c2);
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

        // ================================================================
        // 내부 생성 로직
        // ================================================================

        private static Texture2D GenerateGradientTexture(int width, int height, GradientMode mode, Color c1, Color c2)
        {
            if (width <= 0 || height <= 0)
                return null;

            var tex = new Texture2D(width, height);
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float t;
                    switch (mode)
                    {
                        case GradientMode.Vertical2Color:
                            t = (float)y / (height - 1);
                            break;
                        case GradientMode.Horizontal2Color:
                            t = (float)x / (width - 1);
                            break;
                        case GradientMode.Radial:
                            float cx = (float)x / (width - 1) - 0.5f;
                            float cy = (float)y / (height - 1) - 0.5f;
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
    }
}