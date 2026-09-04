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

        /// <summary>
        /// 레이어 알베도 스플랫 블렌드 색 반환 (순수 함수).
        /// 합산 후 레이어 대표 colorTint(layers[0], 국가별 방위 테마 색조)를 한 번 곱해
        /// Idyllic 밝은 톤 텍스처에서도 방위 테마 색을 유지한다. 결정론적(같은 시드 → 같은 결과).
        /// </summary>
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

            // 국가별 colorTint 적용 — layers는 국가별로 구성되므로 대표 tint를 합산 후 단일 곱.
            // (모든 레이어가 같은 tint이므로 레이어별 누적 곱과 결과 동일. RGBA32 기록 시 1 이상은 자동 클램프.)
            Color tint = layers[0].colorTint;
            acc.r *= tint.r;
            acc.g *= tint.g;
            acc.b *= tint.b;

            acc.a = 1f;
            return acc;
        }

        /// <summary>
        /// 정규화된 레이어 가중치(합=1) 반환 (순수 함수, 결정론적).
        ///
        /// Phase T-R3 가중치 재설계 — 형상과 색이 같은 데이터에서 나옴:
        ///   · L3 바위·절벽 = TerrainShape.CliffMask (절벽 ridge 게이트 m, 고도 R2와 동일 단일 소스)
        ///   · L5 이끼·수변 = 호수 반경 +8m 밴드 (TerrainGenerator.Lakes LCG 앵커 재사용, 시드 불변)
        ///   · L4 흙길 = TerrainPathGenerator.DirtRoadMask (동일 좌표계)
        ///   · L1/L2 = 고도 3분위 (저지대/중지대), 위 마스크 영역 제외
        /// </summary>
        public static float[] ComputeWeights(float wx, float wz, List<TerrainLayerDef> layers, int seed)
        {
            int n = layers.Count;
            float[] w = new float[n];
            if (n == 0) return w;

            NationType nation = layers[0].nation;
            float h = TerrainGenerator.GetHeightAt(wx, wz, BiomeType.Plains, 42);
            float nh = Mathf.Clamp01(h / HEIGHT_NORMALIZE);

            // ── 공유 마스크 (형태-색 정합성의 핵심) ──
            float cliff = TerrainShape.CliffMask(wx, wz, nation, seed, TerrainGenerator.SampleCliffSuppression(wx, wz));
            float water = LakeBand(wx, wz);
            float path  = TerrainPathGenerator.DirtRoadMask(wx, wz);

            // L1/L2 아래 허용도 — 절벽/수변/흙길 우세 영역 제외
            float unobstructed = Mathf.Clamp01(1f - Mathf.Max(cliff, water, path * 0.9f));

            // 고도 3분위 → L1 저지대 / L2 중지대 (여기선 2층으로 세분, 상한 낮을수록 저지대)
            float lowW = Mathf.Clamp01(1f - nh / 0.5f);          // 저지대 (nh 낮음 → 우세)
            float midW = WeightFromCenter(nh, 0.55f, 0.35f);     // 중지대 (피크 ~0.55)

            float[] raw = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = 0f;
                string nm = layers[i].layerName.ToLowerInvariant();
                if (nm.Contains("lowland"))        v = lowW * unobstructed;
                else if (nm.Contains("midland"))   v = midW * unobstructed;
                else if (nm.Contains("rock"))      v = cliff;                                   // L3 절벽
                else if (nm.Contains("dirt"))      v = path * (1f - Mathf.Clamp01(water));      // L4 흙길 (수변 우선)
                else if (nm.Contains("moss"))      v = water;                                   // L5 이끼·수변
                raw[i] = Mathf.Max(0f, v);
            }

            float sum = 0f;
            for (int i = 0; i < n; i++) sum += raw[i];
            if (sum <= 0.0001f)
            {
                w[0] = 1f;
                for (int i = 1; i < n; i++) w[i] = 0f;
                return w;
            }
            for (int i = 0; i < n; i++) w[i] = raw[i] / sum;
            return w;
        }

        /// <summary>
        /// 수면 인접(호수) 밴드 [0,1] — 호수 반경 안(1, 수면·진흙) → 반경 +8m(0, 일반 지면)로 페이드.
        /// TerrainGenerator.Lakes(기존 LCG 앵커, 시드 불변)를 그대로 사용한다.
        /// </summary>
        static float LakeBand(float wx, float wz)
        {
            var lakes = TerrainGenerator.Lakes;
            if (lakes == null || lakes.Count == 0) return 0f;
            float best = 0f;
            for (int i = 0; i < lakes.Count; i++)
            {
                float dx = wx - lakes[i].center.x;
                float dz = wz - lakes[i].center.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float r = lakes[i].radius;
                // Phase W1: L5 이끼·수변 밴드를 새 수변 밴드(1.0r~1.45r)와 동기화 —
                // TerrainGenerator.LAKE_SHORE_BAND_FACTOR(=1.45) 배율로 페이드.
                float bandOuter = r * TerrainGenerator.LAKE_SHORE_BAND_FACTOR;
                float band = 1f - Mathf.SmoothStep(r, bandOuter, d);
                if (band > best) best = band;
            }
            return Mathf.Clamp01(best);
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
