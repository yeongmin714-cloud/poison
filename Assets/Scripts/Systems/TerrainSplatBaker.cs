using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 절차적 멀티레어 스플랫 베이커.
    /// 각 픽셀의 세계 좌표에서 높이/경사/노이즈로 레이어 가중치를 계산해
    /// 레이어 알베도를 블렌드한 단일 스플랫 맵(Texture2D)을 생성한다.
    /// 결정론적 시드 기반이라 같은 입력 → 같은 결과를 보장한다.
    /// </summary>
    public static class TerrainSplatBaker
    {
        public const float WORLD_SIZE = 2000f;
        const float HEIGHT_NORMALIZE = 20f;   // nh = clamp(h/20) — 최대 진폭(북 16m) 상정
        const float SLOPE_SAMPLE_H = 5f;

        /// <summary>지정 좌표의 경사각(도). 0 이상 반환.</summary>
        public static float EstimateSlopeDegrees(float wx, float wz)
        {
            float c = TerrainGenerator.GetHeightAt(wx, wz, BiomeType.Plains, 42);
            float dx = TerrainGenerator.GetHeightAt(wx + SLOPE_SAMPLE_H, wz, BiomeType.Plains, 42) - c;
            float dz = TerrainGenerator.GetHeightAt(wx, wz + SLOPE_SAMPLE_H, BiomeType.Plains, 42) - c;
            float horiz = Mathf.Sqrt(dx * dx + dz * dz);
            return Mathf.Atan2(horiz, SLOPE_SAMPLE_H) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 전체 세계(2000x2000, 중심 원점)에 대해 지정 국가의 레이어를 스플랫한 단일 맵 생성.
        /// 다른 국가 픽셀은 어두운 중립색으로 채움(이 국가용 맵의 스플랫 영역만 유효).
        /// </summary>
        public static Texture2D BakeSplatMap(NationType nation, List<Texture2D> nationTextures, int resolution, int seed)
        {
            var layers = TerrainLayerDef.CreateForNation(nation, nationTextures);
            int res = Mathf.Max(32, resolution);
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.name = "Splat_" + nation + "_" + res;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            Color[] px = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / res;
                    float v = (float)y / res;
                    float wx = (u - 0.5f) * WORLD_SIZE;
                    float wz = (v - 0.5f) * WORLD_SIZE;
                    NationType at = NationTerrainController.GetNationFromPosition(new Vector3(wx, 0f, wz));
                    px[y * res + x] = (at == nation)
                        ? ComputeLayerColor(nation, wx, wz, layers, seed)
                        : new Color(0.10f, 0.12f, 0.15f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>레이어 알베도 스플랫 블렌드 색 반환 (순수 함수).</summary>
        public static Color ComputeLayerColor(NationType nation, float wx, float wz, List<TerrainLayerDef> layers, int seed)
        {
            if (layers == null || layers.Count == 0) return new Color(0.4f, 0.5f, 0.3f);
            float[] w = ComputeWeights(wx, wz, layers, seed);
            Color acc = Color.black;
            for (int i = 0; i < layers.Count; i++)
            {
                var L = layers[i];
                if (w[i] <= 0f || L.albedo == null) continue;
                Color c = SampleAlbedo(L.albedo, wx, wz, L.tiling);
                acc += c * w[i];
            }
            acc.a = 1f;
            return acc;
        }

        /// <summary>정규화된 레이어 가중치(합=1) 반환 (순수 함수).</summary>
        public static float[] ComputeWeights(float wx, float wz, List<TerrainLayerDef> layers, int seed)
        {
            int n = layers.Count;
            float[] w = new float[n];
            if (n == 0) return w;

            float h = TerrainGenerator.GetHeightAt(wx, wz, BiomeType.Plains, 42);
            float nh = Mathf.Clamp01(h / HEIGHT_NORMALIZE);
            float slope = EstimateSlopeDegrees(wx, wz);
            float sn = Mathf.Clamp01(slope / 45f);
            float noise = Mathf.PerlinNoise(wx * 0.002f + seed * 0.371f, wz * 0.002f + seed * 0.713f);

            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                var L = layers[i];
                float hw = WeightFromCenter(nh, L.heightBlendCenter, L.heightBlendWidth);
                float sw = Mathf.Max(0f, 1f + L.slopePreference * sn);
                float nw = 0.7f + 0.6f * noise;
                w[i] = Mathf.Max(0f, hw * sw * L.strength * nw);
                sum += w[i];
            }
            if (sum <= 0.0001f)
            {
                w[0] = 1f;
                for (int i = 1; i < n; i++) w[i] = 0f;
                return w;
            }
            for (int i = 0; i < n; i++) w[i] /= sum;
            return w;
        }

        /// <summary>값을 정규 높이로 보고 중심에서 떨어진 정도에 따라 1→0 선형 감쇠. 1=중심, 0=벗어남.</summary>
        public static float WeightFromCenter(float value, float center, float width)
        {
            return 1f - Mathf.Clamp01(Mathf.Abs(value - center) / width);
        }

        static Color SampleAlbedo(Texture2D albedo, float wx, float wz, float tiling)
        {
            try
            {
                if (albedo == null || !albedo.isReadable) return new Color(0.4f, 0.45f, 0.5f);
                float u = wx / tiling;
                float v = wz / tiling;
                return albedo.GetPixelBilinear(u, v);
            }
            catch
            {
                return new Color(0.4f, 0.45f, 0.5f);
            }
        }
    }
}
