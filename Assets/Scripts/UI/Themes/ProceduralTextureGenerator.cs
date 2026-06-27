#nullable disable
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Phase 33 UI-01: 절차적 텍스처 생성기.
    /// Perlin noise 기반 7종 배경 패턴을 생성합니다.
    /// 모든 텍스처는 1회 생성 후 캐싱됩니다.
    /// </summary>
    public static class ProceduralTextureGenerator
    {
        private const int DEFAULT_SIZE = 256;
        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        // ================================================================
        // 공개 API
        // ================================================================

        /// <summary>
        /// 지정된 패턴 타입의 배경 텍스처를 반환합니다. 캐싱 적용.
        /// </summary>
        public static Texture2D GetPatternTexture(UIDesignTheme.PatternType patternType)
        {
            string key = patternType.ToString();
            if (_cache.TryGetValue(key, out Texture2D cached))
                return cached;

            Texture2D tex = GeneratePattern(patternType, DEFAULT_SIZE, DEFAULT_SIZE);
            _cache[key] = tex;
            return tex;
        }

        /// <summary>
        /// 모든 캐시된 텍스처를 해제합니다.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value != null)
                    Object.DestroyImmediate(kvp.Value);
            }
            _cache.Clear();
        }

        /// <summary>
        /// 단색 텍스처를 생성합니다 (UIStyleManager 호환).
        /// </summary>
        public static Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        // ================================================================
        // 내부 생성 로직
        // ================================================================

        private static Texture2D GeneratePattern(UIDesignTheme.PatternType type, int w, int h)
        {
            switch (type)
            {
                case UIDesignTheme.PatternType.Parchment: return GenerateParchment(w, h);
                case UIDesignTheme.PatternType.Leather:  return GenerateLeather(w, h);
                case UIDesignTheme.PatternType.Marble:   return GenerateMarble(w, h);
                case UIDesignTheme.PatternType.Wood:     return GenerateWood(w, h);
                case UIDesignTheme.PatternType.Stone:    return GenerateStone(w, h);
                case UIDesignTheme.PatternType.Metal:    return GenerateMetal(w, h);
                case UIDesignTheme.PatternType.Glass:    return GenerateGlass(w, h);
                default:                                 return MakeTexture(w, h, new Color(0.1f, 0.1f, 0.1f, 0.9f));
            }
        }

        /// <summary>세피아 톤 + Perlin 얼룩 + 잡티 노이즈</summary>
        private static Texture2D GenerateParchment(int w, int h)
        {
            var tex = new Texture2D(w, h);
            Color baseColor = new Color(0.85f, 0.72f, 0.53f); // 세피아 베이스
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w * 4f;
                    float ny = (float)y / h * 4f;
                    float noise = Mathf.PerlinNoise(nx, ny) * 0.15f;
                    float speckle = (Random.value * 0.08f) - 0.04f; // 잡티
                    float r = Mathf.Clamp01(baseColor.r + noise + speckle);
                    float g = Mathf.Clamp01(baseColor.g + noise * 0.5f + speckle * 0.5f);
                    float b = Mathf.Clamp01(baseColor.b - noise * 0.3f);
                    tex.SetPixel(x, y, new Color(r, g, b, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>암갈색 + 세로줄 결 노이즈</summary>
        private static Texture2D GenerateLeather(int w, int h)
        {
            var tex = new Texture2D(w, h);
            Color baseColor = new Color(0.35f, 0.20f, 0.10f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 세로줄 결: x축 Perlin
                    float nx = (float)x / w * 8f;
                    float ny = (float)y / h * 2f;
                    float grain = Mathf.PerlinNoise(nx, ny) * 0.12f;
                    // 가로 방향 미세 결
                    float hGrain = Mathf.PerlinNoise((float)y / h * 16f, (float)x / w * 1f) * 0.06f;
                    float r = Mathf.Clamp01(baseColor.r + grain + hGrain);
                    float g = Mathf.Clamp01(baseColor.g + grain * 0.7f);
                    float b = Mathf.Clamp01(baseColor.b + grain * 0.3f);
                    tex.SetPixel(x, y, new Color(r, g, b, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>흰색/회색 + Perlin 대리석 무늬</summary>
        private static Texture2D GenerateMarble(int w, int h)
        {
            var tex = new Texture2D(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w * 6f;
                    float ny = (float)y / h * 6f;
                    float noise = Mathf.PerlinNoise(nx, ny);
                    // 대리석 vein: sin(noise * PI)로 줄무늬 강조
                    float vein = Mathf.Sin(noise * Mathf.PI * 4f) * 0.5f + 0.5f;
                    float value = Mathf.Lerp(0.5f, 0.95f, vein);
                    float r = value + (Mathf.PerlinNoise(nx * 0.5f, ny * 0.5f) - 0.5f) * 0.1f;
                    float g = value + (Mathf.PerlinNoise(nx * 0.5f + 5f, ny * 0.5f + 3f) - 0.5f) * 0.1f;
                    float b = value + (Mathf.PerlinNoise(nx * 0.5f + 10f, ny * 0.5f + 7f) - 0.5f) * 0.1f;
                    tex.SetPixel(x, y, new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>나무 결 수평선 노이즈</summary>
        private static Texture2D GenerateWood(int w, int h)
        {
            var tex = new Texture2D(w, h);
            Color baseColor = new Color(0.55f, 0.35f, 0.15f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 수평 나무 결: y축에 주기적 노이즈
                    float ny = (float)y / h * 20f;
                    float nx = (float)x / w * 4f;
                    float ring = Mathf.PerlinNoise(nx, ny) * 0.5f + 0.5f;
                    float grain = Mathf.Sin(ny * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float blend = (ring + grain) * 0.5f;
                    float r = Mathf.Lerp(baseColor.r * 0.8f, baseColor.r * 1.2f, blend);
                    float g = Mathf.Lerp(baseColor.g * 0.8f, baseColor.g * 1.2f, blend);
                    float b = Mathf.Lerp(baseColor.b * 0.7f, baseColor.b * 1.1f, blend);
                    tex.SetPixel(x, y, new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>회색 + 거친 Perlin 텍스처</summary>
        private static Texture2D GenerateStone(int w, int h)
        {
            var tex = new Texture2D(w, h);
            Color baseColor = new Color(0.45f, 0.42f, 0.40f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w * 8f;
                    float ny = (float)y / h * 8f;
                    float noise = Mathf.PerlinNoise(nx, ny);
                    // 거친 질감
                    float rough = noise * 0.25f;
                    float r = Mathf.Clamp01(baseColor.r + rough - 0.1f);
                    float g = Mathf.Clamp01(baseColor.g + rough * 0.8f - 0.08f);
                    float b = Mathf.Clamp01(baseColor.b + rough * 0.6f - 0.06f);
                    // 작은 균열
                    float crack = Mathf.PerlinNoise(nx * 3f, ny * 3f);
                    if (crack > 0.7f)
                    {
                        r *= 0.85f;
                        g *= 0.85f;
                        b *= 0.85f;
                    }
                    tex.SetPixel(x, y, new Color(r, g, b, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>금속 브러시드 수평선</summary>
        private static Texture2D GenerateMetal(int w, int h)
        {
            var tex = new Texture2D(w, h);
            Color baseColor = new Color(0.6f, 0.6f, 0.62f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 브러시드: 수평 방향 미세 노이즈
                    float ny = (float)y / h * 40f;
                    float nx = (float)x / w * 2f;
                    float brush = Mathf.PerlinNoise(nx, ny) * 0.08f;
                    // 금속 광택
                    float shine = Mathf.Sin((float)x / w * 60f + (float)y / h * 5f) * 0.03f;
                    float value = 0.5f + brush + shine;
                    float r = Mathf.Clamp01(baseColor.r + (value - 0.5f) * 0.5f);
                    float g = Mathf.Clamp01(baseColor.g + (value - 0.5f) * 0.5f);
                    float b = Mathf.Clamp01(baseColor.b + (value - 0.5f) * 0.5f);
                    tex.SetPixel(x, y, new Color(r, g, b, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>반투명 + 미세 격자</summary>
        private static Texture2D GenerateGlass(int w, int h)
        {
            var tex = new Texture2D(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 반투명 베이스
                    float alpha = 0.25f;
                    // 미세 격자
                    float gridX = (x % 16 == 0) ? 1f : 0f;
                    float gridY = (y % 16 == 0) ? 1f : 0f;
                    float grid = Mathf.Max(gridX, gridY) * 0.3f;
                    // 미세 반사
                    float reflection = Mathf.PerlinNoise((float)x / w * 20f, (float)y / h * 20f) * 0.1f;
                    float r = Mathf.Clamp01(0.7f + grid + reflection);
                    float g = Mathf.Clamp01(0.75f + grid + reflection);
                    float b = Mathf.Clamp01(0.8f + grid + reflection * 1.2f);
                    alpha = Mathf.Clamp01(alpha + grid);
                    tex.SetPixel(x, y, new Color(r, g, b, alpha));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
