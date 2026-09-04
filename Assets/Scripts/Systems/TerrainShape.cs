using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 🌄 Phase T-R2: 방위별 스타일라이즈드 지형 "형태" 수학 헬퍼.
    ///
    /// 예시 스크린샷(09-04)의 형태 언어를 결정론적 FBM으로 구현한다:
    ///   F1 완만한 구릉  -> Base: FBM 4옥타브 (lacunarity 2.0, gain 0.55)
    ///   F2 층진 절벽    -> Ridged FBM + smoothstep 게이트 마스크 m, 낙차 C×m (+ terrace 계단)
    ///   유기적 능선     -> Domain warp: x' = x + 40×FBM(...)
    ///   F3 계곡/저지대  -> Valley: -A×0.35×smoothstep(0.7,0.9,valleyNoise)
    ///
    /// 모든 입력은 고정 시드(nationId×1000 오프셋) 기반으로 결정론적이며
    /// UnityEngine.Random을 전혀 사용하지 않는다 (Random 언시드 금지 준수).
    /// 이 클래스는 순수 계산만 담당 — 절벽 억제(스폰/호수/성/경계)는
    /// TerrainGenerator가 cliffSuppression 인자로 주입한다.
    /// </summary>
    public static class TerrainShape
    {
        // ── FBM 공통 상수 (기존 TerrainGenerator 사양과 동일) ──────────────
        public const int OCTAVES = 4;
        public const float LACUNARITY = 2.0f;
        public const float GAIN = 0.55f;

        // ── 도메인 워핑 ───────────────────────────────────────────────────
        public const float WARP_AMOUNT = 40f;      // ±40m 공간 오프셋
        public const float WARP_FREQ = 0.008f;     // 능선 파장 ~125m
        public const float WARP_OX = 13.7f;
        public const float WARP_OZ = 7.1f;

        // ── 절벽 ──────────────────────────────────────────────────────────
        public const float CLIFF_GATE_SPAN = 0.16f;   // ridgeGate..gate+0.16 상위 ~15%만 절벽화
        public const float CLIFF_FREQ_SCALE = 1.7f;   // 능선 노이즈 주파수 배율 (경사 좌우 분리)

        // ── terrace (계단) ────────────────────────────────────────────────
        public const float TERRACE_BLEND_LO = 0.72f;  // 각 계단 상단 도달 시 부드럽게 라운딩
        public const float TERRACE_BLEND_HI = 0.95f;

        // ── valley (계곡) ─────────────────────────────────────────────────
        public const float VALLEY_AMP_RATIO = 0.35f;
        public const float VALLEY_LO = 0.7f;
        public const float VALLEY_HI = 0.9f;
        public const float VALLEY_FREQ_SCALE = 0.8f;

        // ── 국가별 시드 오프셋 = nationId × 1000 (결정론) ─────────────────
        public static int NationSeedOffset(NationType nation) => (int)nation * 1000;

        /// <summary>
        /// 방위별 지형 파라미터 행. A(진폭) / f0(주파수) / C(절벽낙차) /
        /// ridgeGate(절벽 게이트) / terraceStep(계단, 0=off).
        /// (계획 5.4 표 — 동 완만 → 북 험준 → 황제국 평탄 정원)
        /// </summary>
        public struct NationParams
        {
            public NationType nation;
            public float amplitudeA;     // Base FBM 진폭 (m)
            public float freq0;          // Base FBM 기본 주파수 (1/m) → 파장 200~250m
            public float cliffDropC;     // 절벽 낙차 (m)
            public float ridgeGate;      // smoothstep 게이트 하한 (상위 ~15%만 절벽)
            public float terraceStep;    // terrace 계단(m), 0 = off
        }

        public static NationParams GetNationParams(NationType nation)
        {
            switch (nation)
            {
                // Z3: 진폭 증폭(East 7→10, West 9→11, South 8→10; North 13/절벽낙차·Empire 3 유지)
                //     + 파장 밀도 증가(freq0 ↑, 파장 200→160m) — 탑다운 "구릉" 가독 확보.
                case NationType.East:   return new NationParams { nation = nation, amplitudeA = 10f, freq0 = 0.005f, cliffDropC = 4f, ridgeGate = 0.65f, terraceStep = 0f   };
                case NationType.West:   return new NationParams { nation = nation, amplitudeA = 11f, freq0 = 0.006f, cliffDropC = 6f, ridgeGate = 0.62f, terraceStep = 2.5f };
                case NationType.South:  return new NationParams { nation = nation, amplitudeA = 10f, freq0 = 0.005f, cliffDropC = 5f, ridgeGate = 0.60f, terraceStep = 3f   };
                case NationType.North:  return new NationParams { nation = nation, amplitudeA = 13f, freq0 = 0.006f, cliffDropC = 9f, ridgeGate = 0.58f, terraceStep = 3f   };
                case NationType.Empire: return new NationParams { nation = nation, amplitudeA = 3f,  freq0 = 0.003f, cliffDropC = 2f, ridgeGate = 0.75f, terraceStep = 2f   };
                default:
                    // 미소속(None)/Dracula — East(시작지) 기본값 계승
                    return GetNationParams(NationType.East);
            }
        }

        /// <summary>smoothstep 유틸 (게이트/밸리/페이드 공용).</summary>
        public static float Smoothstep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Ridge 변환: 1 - |2t - 1| (0~1 정점을 날카롭게, 능선/절벽 표현).</summary>
        public static float Ridge(float t) => 1f - Mathf.Abs(t * 2f - 1f);

        /// <summary>
        /// FBM 다중 옥타브 Perlin 노이즈 (0~1 정규화).
        /// 옥타브마다 seed 기반 오프셋을 섞어 옥타브별 패턴을 분리하고,
        /// amplitude 합으로 정규화해 결과를 0~1로 유지한다 (기존 TerrainGenerator 규격).
        /// </summary>
        public static float Fbm(float x, float z, int octaves, float lacunarity, float gain, int seed)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float amplitudeSum = 0f;

            for (int o = 0; o < octaves; o++)
            {
                float noiseX = x * frequency + seed * 0.371f;
                float noiseZ = z * frequency + seed * 0.713f;

                total += Mathf.PerlinNoise(noiseX, noiseZ) * amplitude;
                amplitudeSum += amplitude;

                frequency *= lacunarity;
                amplitude *= gain;
            }

            return amplitudeSum > 0f ? total / amplitudeSum : 0f;
        }

        /// <summary>
        /// terrace 변환: 값을 step 간격 계단으로 만들되 상단에서 smooth 라운딩.
        /// 예) cử층진 절벽(예시 9,10) — 계단 사이 경계가 계단 위쪽에서 부드럽게 물러난다.
        /// </summary>
        public static float ApplyTerrace(float h, float step)
        {
            if (step <= 0f) return h;
            float stepped = Mathf.Floor(h / step) * step;
            float fracToTop = h - stepped;             // 0..step (해당 계단 내 정상까지 남은 높이)
            float t = fracToTop / step;
            float s = Smoothstep(TERRACE_BLEND_LO, TERRACE_BLEND_HI, t);
            return stepped + step * s;                 // 계단 상단 근처에서만 다음 층으로 라운딩
        }

        /// <summary>
        /// 도메인 워핑: 직선처럼 보이는 능선을 유기적으로 굽힌다.
        /// (RidgeCliffMask/CliffMask와 NationHeight가 같은 변환을 공유해야
        ///  형태와 색 스플랫이 같은 데이터에서 나온다 — 단일 소스.)
        /// </summary>
        static void Warp(float x, float z, int nseed, out float wx, out float wz)
        {
            float warpX = Fbm(x * WARP_FREQ + WARP_OX, z * WARP_FREQ + WARP_OZ, OCTAVES, LACUNARITY, GAIN, nseed + 1234);
            float warpZ = Fbm(z * WARP_FREQ + WARP_OZ, x * WARP_FREQ + WARP_OX, OCTAVES, LACUNARITY, GAIN, nseed + 5678);
            wx = x + WARP_AMOUNT * (warpX - 0.5f) * 2f;
            wz = z + WARP_AMOUNT * (warpZ - 0.5f) * 2f;
        }

        /// <summary>
        /// 워핑 좌표에서 ridged 능선 게이트 절벽 마스크 m∈[0,1] 계산 (F2 핵심).
        /// NationHeight의 절벽 낙차와 스플랫 L3(바위·절벽)가 **정확히 동일한** 마스크를 쓰도록
        /// 공유하는 단일 소스. m>0.5 일 때 절벽으로 간주한다.
        /// </summary>
        static float RidgeCliffMask(float wx, float wz, NationParams p, int nseed, float suppression)
        {
            float ridgedNoise = Ridge(Fbm(
                wx * p.freq0 * CLIFF_FREQ_SCALE,
                wz * p.freq0 * CLIFF_FREQ_SCALE,
                OCTAVES, LACUNARITY, GAIN, nseed + 2345));
            float m = Smoothstep(p.ridgeGate, p.ridgeGate + CLIFF_GATE_SPAN, ridgedNoise);
            m *= Mathf.Clamp01(suppression);   // 보호 구역(스폰/성/호수/경계) 절벽 금지
            return m;
        }

        /// <summary>
        /// 공개 절벽 마스크 (Phase T-R3 스플랫 L3, R4 꽃밭 등에 사용). 
        /// NationHeight와 100% 동일한 ridge 게이트 마스크를 반환한다.
        /// cliffSuppression(0..1)은 TerrainGenerator.SampleCliffSuppression으로 주입하면
        /// 스폰/성/호수/방위경계 근처 절벽이 형태와 함께 색상도 억제된다.
        /// </summary>
        public static float CliffMask(float x, float z, NationType nation, int seed, float cliffSuppression = 1f)
        {
            NationParams p = GetNationParams(nation);
            int nseed = seed + NationSeedOffset(nation);
            Warp(x, z, nseed, out float wx, out float wz);
            return RidgeCliffMask(wx, wz, p, nseed, cliffSuppression);
        }

        /// <summary>
        /// 방위별 단일 고도 계산 — 핵심 수식 (계획 5.1):
        /// H = Base + Cliff(+terrace) + Valley, 입력 좌표는 domain warp로 굽힌다.
        /// </summary>
        /// <param name="x">월드 X</param>
        /// <param name="z">월드 Z</param>
        /// <param name="nation">방위</param>
        /// <param name="seed">기저 시드</param>
        /// <param name="cliffSuppression">
        ///   [0,1] 절벽 억제 마스크 (0=절벽 금지 구역, 1=허용).
        ///   스폰/호수/성/경계선 보호 — TerrainGenerator가 주입한다.
        /// </param>
        /// <param name="baseDetail">
        ///   [0,1] Base/Valley의 디테일(진폭·옥타브) 계수. 0에 가까울수록 저주파·저진폭으로
        ///   완만해져 경계 크로스페이드 연속성(|Δh|&lt;0.5m, Test b)을 보증한다.
        ///   1 = 원본 방위별 디테일. 경계선/보호 앵커 인근에서 TerrainGenerator가 낮춰 주입.
        /// </param>
        public static float NationHeight(
            float x, float z, NationType nation, int seed,
            float cliffSuppression = 1f, float baseDetail = 1f)
        {
            NationParams p = GetNationParams(nation);
            int nseed = seed + NationSeedOffset(nation);

            // ── 1) 도메인 워핑 (NationHeight ↔ CliffMask가 동일 변환 공유) ──
            Warp(x, z, nseed, out float wx, out float wz);

            float detail = Mathf.Clamp01(baseDetail);

            // ── 2) Base: 완만한 구릉 (F1)
            //      baseDetail<1 → 저옥타브(2옥)와 저진폭으로 블렌드해 경계부 구릉을 완만화 ──
            float fbmFull = Fbm(wx * p.freq0, wz * p.freq0, OCTAVES, LACUNARITY, GAIN, nseed);
            float fbmSmooth = Fbm(wx * p.freq0, wz * p.freq0, 2, LACUNARITY, GAIN, nseed);
            float baseFbm = Mathf.Lerp(fbmSmooth, fbmFull, detail);
            float baseA = p.amplitudeA * Mathf.Lerp(LOW_DETAIL_AMP_FACTOR, 1f, detail);
            float baseH = (baseFbm - 0.5f) * 2f * baseA;   // ±baseA

            // ── 3) Cliff: ridged 능선 마스크 + 낙차 (F2) — RidgeCliffMask 공유(스플랫 L3과 동일 소스) ──
            float m = RidgeCliffMask(wx, wz, p, nseed, cliffSuppression);

            float cliffH = p.cliffDropC * m;

            // terrace: m>0.5인 절벽 영역에만 계단 적용 (층진 절벽 연출)
            if (p.terraceStep > 0f && m > 0.5f)
            {
                cliffH = ApplyTerrace(cliffH, p.terraceStep);
            }

            float h = baseH + cliffH;

            // ── 4) Valley: 연속적 계곡/저지대 바이어스 (F3) ──
            float valleyNoise = Fbm(
                wx * p.freq0 * VALLEY_FREQ_SCALE,
                wz * p.freq0 * VALLEY_FREQ_SCALE,
                OCTAVES, LACUNARITY, GAIN, nseed + 3456);
            float valleyA = p.amplitudeA * VALLEY_AMP_RATIO * Mathf.Lerp(LOW_DETAIL_AMP_FACTOR, 1f, detail);
            h += -valleyA * Smoothstep(VALLEY_LO, VALLEY_HI, valleyNoise);

            return h;
        }

        // 경계/보호 구역의 저스트디테일 진폭 계수 (0.35 = ~36% 진폭 → 완만 경사 보증)
        public const float LOW_DETAIL_AMP_FACTOR = 0.35f;

        // ====================================================================
        // Phase T-R3: 야생화 패치 & 판타지 서브존 마스크 (R4가 사용)
        // ====================================================================

        // ── 야생화 패치 (예시의 꽃 들판) ──────────────────────────────────────
        // Z4: 야생화 커버리지 8%→14% (FLOWER_LO 하향 — Smoothstep 통과점을 저주파쪽으로 내림)
        const float FLOWER_FREQ = 0.02f;        // 주파수 0.02 (패치 크기 ~50m)
        const float FLOWER_OX = 3.7f;           // 패치 분포 오프셋 (전 세계 균일, 국가 무관)
        const float FLOWER_OZ = 11.3f;
        const float FLOWER_LO = 0.78f;          // 커버리지 ~14% (Smoothstep(0.78,0.90,n) 통과점 n≈0.85)
        const float FLOWER_HI = 0.90f;

        /// <summary>
        /// 야생화 패치 마스크 [0,1] — 주파수 0.02, 커버리지 약 8% (Phase T-R3 §3).
        /// R4가 이 마스크로 Idyllic Flowers 프리팹을 고밀도 배치에 사용한다.
        /// 무국가/전 세계 균일 (결정론 — PerlinNoise 고정 빈도).
        /// </summary>
        public static float GetFlowerPatchMask(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * FLOWER_FREQ + FLOWER_OX, z * FLOWER_FREQ + FLOWER_OZ);
            return Smoothstep(FLOWER_LO, FLOWER_HI, n);
        }

        // ── 판타지 서브존 (예시 12,13: 보라/마젠타 잔디 + 꽃 집중) ──────────────
        const int    FANTASY_SUBZONE_MAX   = 2;     // 국가당 최대 2개
        const float  FANTASY_RADIUS_MIN   = 120f;   // 존 반경 120~200m
        const float  FANTASY_RADIUS_MAX   = 200f;
        const float  FANTASY_EXCLUDE_DIST = 150f;   // 스폰/성/호수 반경 150m 밖
        const float  FANTASY_EDGE_SOFT    = 25f;    // 존 가장자리 부드러운 페이드

        /// <summary>
        /// 결정론 정수 혼합 해시 → [0,1) 균등. UnityEngine.Random 미사용 (언시드 금지 준수).
        /// </summary>
        static float H01(int seed, int idx)
        {
            uint h = (uint)seed + (uint)idx * 0x9E3779B9u;
            h = (h ^ (h >> 16)) * 0x85EBCA6Bu;
            h = (h ^ (h >> 13)) * 0xC2B2AE35u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }

        /// <summary>
        /// 국가당 판타지 서브존(보라/마젠타 꽃밭) 마스크 [0,1] — Phase T-R3 §4 (R4가 사용).
        ///   · 개수 0~2, 반경 120~200m, 시드 = 국가 시드(seed + NationSeedOffset(nation)) + 3.
        ///   · 존 중심은 스폰/성(0,0)/호수 반경 150m 밖에서 결정론 선택 (이상한 지역 겹침 방지).
        ///   · 내부 1, 가장자리 FANTASY_EDGE_SOFT m에서 부드럽게 0으로 페이드.
        /// </summary>
        public static float GetFantasySubzoneMask(float x, float z, NationType nation, int seed)
        {
            int nseed = seed + NationSeedOffset(nation) + 3;   // "국가 시드 + 3"
            // 스폰/성/호수 배제 참조
            Vector3 spawn = ProjectName.Core.PlayerSpawnConfig.SpawnPosition;
            var lakes = TerrainGenerator.Lakes;

            int count = H01(nseed, 0) < 0.4f ? 0 : (H01(nseed, 0) < 0.85f ? 1 : FANTASY_SUBZONE_MAX);

            float best = 0f;
            for (int k = 0; k < count; k++)
            {
                // 방향/거리 → 중심 위치 (120~200m)
                float ang = H01(nseed, 10 + k) * 360f * Mathf.Deg2Rad;
                float dist = FANTASY_RADIUS_MIN + H01(nseed, 20 + k) * (FANTASY_RADIUS_MAX - FANTASY_RADIUS_MIN);
                Vector3 c = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);

                // 스폰/성/호수 반경 150m 밖이어야 함 — 위반 시 이 서브존 스킵
                if (Vector3.Distance(c, spawn) < FANTASY_EXCLUDE_DIST) continue;
                if (Vector3.Distance(c, Vector3.zero) < FANTASY_EXCLUDE_DIST) continue;
                bool nearLake = false;
                if (lakes != null)
                {
                    for (int i = 0; i < lakes.Count; i++)
                    {
                        Vector3 lc = new Vector3(lakes[i].center.x, 0f, lakes[i].center.z);
                        if (Vector3.Distance(c, lc) < FANTASY_EXCLUDE_DIST) { nearLake = true; break; }
                    }
                }
                if (nearLake) continue;

                float radius = FANTASY_RADIUS_MIN + H01(nseed, 30 + k) * (FANTASY_RADIUS_MAX - FANTASY_RADIUS_MIN);
                float d = Vector3.Distance(new Vector3(x, 0f, z), c);
                float m = 1f - Smoothstep(radius - FANTASY_EDGE_SOFT, radius, d);
                if (m > best) best = m;
            }
            return Mathf.Clamp01(best);
        }

        // ── Z4: 숲 군락 (Forest Patches) ─────────────────────────────────────
        const float FOREST_RADIUS_MIN   = 100f;   // 군락(클러스터) 반경 100~180m
        const float FOREST_RADIUS_MAX   = 180f;
        const float FOREST_CENTER_MIN   = 150f;   // 성(0,0)에서 군락 중심 거리 150~500m
        const float FOREST_CENTER_MAX   = 500f;
        const float FOREST_EXCLUDE_DIST = 150f;   // 스폰/성/호수 반경 150m 밖
        const float FOREST_EDGE_SOFT    = 25f;    // 군락 가장자리 부드러운 페이드

        /// <summary>
        /// Z4: 국가별 숲 군락 마스크 [0,1] — 국가당 3~5개(결정론), 군락 반경 100~180m,
        /// 스폰/성/호수 반경 150m 밖 배치. IdyllicDecoPlacer가 이 마스크 내 나무 밀도 ×4(1/225㎡)를 적용.
        /// 시드 = 국가 시드(seed + NationSeedOffset(nation)) + 5. 군락 각도는 국가 방위 중심으로 바이어스.
        /// </summary>
        public static float GetForestPatchMask(float x, float z, NationType nation, int seed)
        {
            int nseed = seed + NationSeedOffset(nation) + 5;
            int count = 3 + (int)(H01(nseed, 0) * 3f);   // 3..5개 (H01 ∈ [0,1))
            Vector3 spawn = ProjectName.Core.PlayerSpawnConfig.SpawnPosition;
            var lakes = TerrainGenerator.Lakes;

            float baseAng;
            switch (nation)
            {
                case NationType.North: baseAng = 90f;  break;
                case NationType.West:  baseAng = 180f; break;
                case NationType.South: baseAng = 270f; break;
                default:               baseAng = 0f;   break; // East
            }

            float best = 0f;
            for (int k = 0; k < count; k++)
            {
                // 방위 중심 ±40° 안에서 결정론 각도 → 성에서 dist 만큼의 군락 중심
                float ang = (baseAng + (H01(nseed, 10 + k) - 0.5f) * 80f) * Mathf.Deg2Rad;
                float dist = FOREST_CENTER_MIN + H01(nseed, 20 + k) * (FOREST_CENTER_MAX - FOREST_CENTER_MIN);
                Vector3 c = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);

                // 스폰/성/호수 반경 150m 밖이어야 함 — 위반 시 이 군락 스킵
                if (Vector3.Distance(c, spawn) < FOREST_EXCLUDE_DIST) continue;
                if (Vector3.Distance(c, Vector3.zero) < FOREST_EXCLUDE_DIST) continue;
                bool nearLake = false;
                if (lakes != null)
                {
                    for (int i = 0; i < lakes.Count; i++)
                    {
                        Vector3 lc = new Vector3(lakes[i].center.x, 0f, lakes[i].center.z);
                        if (Vector3.Distance(c, lc) < FOREST_EXCLUDE_DIST) { nearLake = true; break; }
                    }
                }
                if (nearLake) continue;

                float radius = FOREST_RADIUS_MIN + H01(nseed, 30 + k) * (FOREST_RADIUS_MAX - FOREST_RADIUS_MIN);
                float d = Vector3.Distance(new Vector3(x, 0f, z), c);
                float m = 1f - Smoothstep(radius - FOREST_EDGE_SOFT, radius, d);
                if (m > best) best = m;
            }
            return Mathf.Clamp01(best);
        }
    }
}