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

        // === Phase Y1: 통합 월드 스플랫 — 국가 경계 그라데이션 상수 (국가 색 전용, 높이 쪽과 독립 규격) ===
        // 사용자 요구 "너무 지역이 나눠지지 않게 그라데이션으로" → 경계가 하드 컷이 아닌 부드러운 색 전이.
        // TerrainGenerator.TRANSITION_WIDTH(120m)보다 넉넉히 180m — 높이쪽 규격과의 차이는 의도적 허용.
        /// <summary>방위 경계선(45/135/225/315°) 양옆 그라데이션 폭(미터). 경계선에서 w=0.5, ±fade에서 0/1.</summary>
        public const float NATION_FADE_WIDTH = 180f;
        /// <summary>황제국 중앙 반경(미터) — NationTerrainController.GetNationFromPosition(50m)와 동일.</summary>
        public const float EMPIRE_RADIUS = 50f;

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

        // ================================================================
        //  Phase Y1: 통합 월드 스플랫 (5국가 합성 단일 마스터 맵)
        //  사용자 요구: "4방위를 나눌 때 너무 지역이 나눠지지 않게 그라데이션으로 색 구분"
        //  → 방위 경계선(45/135/225/315°)과 황제국 중앙에서 인접 국가 색을 가중 평균해
        //    하드 컷이 아닌 부드러운 색 전이를 만든다 (결과 보간 — ComputeLayerColor 재사용).
        // ================================================================

        /// <summary>
        /// 전체 세계(2000x2000, 중심 원점)를 5개 국가가 합성된 단일 마스터 스플랫으로 베이크.
        /// 각 픽셀의 세계 좌표에서 국가를 판정하고, 경계 그라데이션 구간에서는 인접 국가 2개의
        /// ComputeLayerColor 결과를 가중 평균해 노이즈/텍스처 상관관계 없이 부드럽게 블렌드한다.
        /// 절벽/수변/흙길/꽃밭 마스크는 ComputeWeights(ComputeLayerColor 내부)가 이미 처리 — 색 정합성 유지.
        /// 결정론적(같은 시드 → 같은 결과). 진행 로그와 소요 시간을 출력한다.
        /// </summary>
        /// <param name="nationTextures">국가별 읽기 가능 텍스처 목록 (Dracula 부재는 skip).</param>
        public static Texture2D BakeWorldSplat(int res, int seed,
            Dictionary<NationType, List<Texture2D>> nationTextures)
        {
            int r = Mathf.Max(32, res);

            // 국가별 5레이어 캐시 구축 (기존 CreateForNation 시그니처 — 텍스처 목록 인자 재사용)
            var layersByNation = new Dictionary<NationType, List<TerrainLayerDef>>();
            int layerCount = 0;
            foreach (var kvp in nationTextures)
            {
                if (kvp.Value == null || kvp.Value.Count == 0) continue;
                var readable = new List<Texture2D>();
                bool any = false;
                foreach (var t in kvp.Value) if (t != null) { readable.Add(t); any = true; }
                if (!any) continue;
                var layers = TerrainLayerDef.CreateForNation(kvp.Key, readable);
                if (layers.Count == 0) continue;
                layersByNation[kvp.Key] = layers;
                layerCount += layers.Count;
            }

            Texture2D tex = new Texture2D(r, r, TextureFormat.RGBA32, false);
            tex.name = "WorldSplat_" + r;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            float t0 = UnityEngine.Time.realtimeSinceStartup;
            Debug.Log($"[TerrainSplatBaker] BakeWorldSplat 시작: {r}x{r} 시드={seed} 국가={layersByNation.Count} 레이어={layerCount}");
            int progressEvery = Mathf.Max(1, r / 10);
            Color[] px = new Color[r * r];
            for (int y = 0; y < r; y++)
            {
                float v = (float)y / r;
                float wz = (v - 0.5f) * WORLD_SIZE;
                int row = y * r;
                for (int x = 0; x < r; x++)
                {
                    float u = (float)x / r;
                    float wx = (u - 0.5f) * WORLD_SIZE;
                    Color c = BlendNationColor(wx, wz, layersByNation, seed);
                    // Y2.3: 골짜기 AO 근사 — 고도 하위 25% 픽셀에 살짝 어두운 틴트(×0.92).
                    // (정점 컬러 AO 대신 높이를 직접 샘플해 베이크에 반영 — 계획 Y2.3)
                    float hv = TerrainGenerator.GetHeightAt(wx, wz, BiomeType.Plains, 42);
                    float nh = Mathf.Clamp01(hv / HEIGHT_NORMALIZE);
                    if (nh < 0.25f)   // 고도 하위 25% = 골짜기
                    {
                        c.r *= 0.92f;
                        c.g *= 0.92f;
                        c.b *= 0.92f;
                    }
                    px[row + x] = c;
                }
                if (y % progressEvery == 0 && y > 0)
                {
                    float pct = (float)y / r * 100f;
                    Debug.Log($"[TerrainSplatBaker] BakeWorldSplat 진행 {pct:F0}% ({y}/{r})");
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            float dt = (UnityEngine.Time.realtimeSinceStartup - t0) * 1000f;
            Debug.Log($"[TerrainSplatBaker] BakeWorldSplat 완료: {r}x{r} 소요={dt:F0}ms ({dt / (r * r):F6}ms/px)");
            return tex;
        }

        /// <summary>
        /// 지정 좌표의 국가 색을 결정 — 경계 그라데이션 블렌딩 포함 (순수 함수, 결정론적).
        /// ① 방위 경계선 4개(45/135/225/315°)에서 인접 국가 2색을 가중 평균 (경계선에서 w=0.5,
        ///    ±NATION_FADE_WIDTH에서 0/1, 수직 거리 기반)
        /// ② 황제국 중앙(반경 50m): Empire 색, 반경 50~50+fade 방사형 크로스페이드
        /// ComputeLayerColor 재사용 — 절벽/수변/흙길/고도 마스크가 그대로 공유된다.
        /// </summary>
        public static Color BlendNationColor(float wx, float wz,
            Dictionary<NationType, List<TerrainLayerDef>> layersByNation, int seed)
        {
            if (layersByNation == null || layersByNation.Count == 0)
                return new Color(0.4f, 0.5f, 0.3f);

            Vector3 pos = new Vector3(wx, 0f, wz);
            NationType anchor = NationTerrainController.GetNationFromPosition(pos);
            Color color = NationColor(anchor, wx, wz, layersByNation, seed);

            // ── ① 방위 경계 그라데이션 (TerrainGenerator.ComputeTerrainHeight와 동일 좌표계) ──
            // 내각 경계 각도/법선은 높이쪽 BlendBoundary와 동일한 단위 광선 벡터.
            // 동-북(45°): East(음)↔North(양)  |  북-서(135°): North↔West
            // 서-남(225°): West↔South  |  남-동(315°): South↔East
            BlendColorBoundary(ref color, wx, wz, layersByNation, seed,
                NationType.East, NationType.North,  0.70710678f,  0.70710678f);
            BlendColorBoundary(ref color, wx, wz, layersByNation, seed,
                NationType.North, NationType.West, -0.70710678f,  0.70710678f);
            BlendColorBoundary(ref color, wx, wz, layersByNation, seed,
                NationType.West, NationType.South, -0.70710678f, -0.70710678f);
            BlendColorBoundary(ref color, wx, wz, layersByNation, seed,
                NationType.South, NationType.East,  0.70710678f, -0.70710678f);

            // ── ② 황제국 방사형 크로스페이드 (중앙 반경 50m 순수 Empire → 50+fade에서 방향성 국가) ──
            float dist = pos.magnitude;
            if (dist < EMPIRE_RADIUS + NATION_FADE_WIDTH
                && layersByNation.ContainsKey(NationType.Empire))
            {
                float t = Mathf.Clamp01((dist - EMPIRE_RADIUS) / NATION_FADE_WIDTH);
                if (t < 1f)
                {
                    NationType dir = DirectionNationByAngle(wx, wz);
                    Color cEmpire = NationColor(NationType.Empire, wx, wz, layersByNation, seed);
                    Color cDir = NationColor(dir, wx, wz, layersByNation, seed);
                    color = Color.Lerp(cEmpire, cDir, t);
                }
            }

            return color;
        }

        /// <summary>단일 각도 경계에 대한 국가색 크로스페이드 헬퍼 (결과 보간).</summary>
        static void BlendColorBoundary(ref Color color, float wx, float wz,
            Dictionary<NationType, List<TerrainLayerDef>> layersByNation, int seed,
            NationType negNation, NationType posNation, float ux, float uz)
        {
            NationType anchor = NationTerrainController.GetNationFromPosition(new Vector3(wx, 0f, wz));
            // 점이 해당 경계에 인접한 방향성 국가가 아니면 무시 (황제국 중심 보호)
            if (anchor != negNation && anchor != posNation) return;

            // 광선(u)에 대한 점 p=(wx,wz)의 수직(부호) 거리: cross = ux*wz - uz*wx
            // 경계선에서 cross=0, ±인 쪽으로 각각 음/양 국가 영역.
            float cross = ux * wz - uz * wx;
            // 경계선에서 w=0.5, ±NATION_FADE_WIDTH에서 0/1 (사용자 요구 가중 평균)
            float w = Mathf.Clamp01(0.5f + cross / NATION_FADE_WIDTH * 0.5f);
            if (w > 0f && w < 1f)
            {
                Color cNeg = NationColor(negNation, wx, wz, layersByNation, seed);
                Color cPos = NationColor(posNation, wx, wz, layersByNation, seed);
                color = Color.Lerp(cNeg, cPos, w);
            }
        }

        /// <summary>국가 레이어 캐시에서 해당 좌표의 국가색 반환 (레이어 없으면 어두운 중립).</summary>
        static Color NationColor(NationType nation, float wx, float wz,
            Dictionary<NationType, List<TerrainLayerDef>> layersByNation, int seed)
        {
            var layers = layersByNation[nation];
            if (layers == null || layers.Count == 0)
                return new Color(0.10f, 0.10f, 0.10f);
            return ComputeLayerColor(nation, wx, wz, layers, seed);
        }

        /// <summary>각도(방위, 황제국 제외)만으로 방향성 국가 판정 — TerrainGenerator.GetDirectionalNation과 동일 경계.</summary>
        static NationType DirectionNationByAngle(float wx, float wz)
        {
            float angle = Mathf.Atan2(wz, wx) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            if (angle < 45f || angle >= 315f) return NationType.East;
            if (angle < 135f) return NationType.North;
            if (angle < 225f) return NationType.West;
            return NationType.South;
        }

        /// <summary>두 색의 RGB 유클리드 거리 (그라데이션 검증/디버그용).</summary>
        public static float ColorDistance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
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
