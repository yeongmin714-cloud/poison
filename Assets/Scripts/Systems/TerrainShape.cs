using UnityEngine;
using System.Collections.Generic;
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
                // T-B1 09-06: 지형 드라마 강화 (예시2~13 컨셉) — 진폭/절벽 낙차 상향 + 방위별 격차 확대.
                //   East=구릉 초원 / West=절벽 협곡 / South=평탄 적토 / North=험준 설산 / Empire=평탄 정원.
                //   구릉 최대경사는 CC slopeLimit(45°) 이하 유지(amp×freq×2π ≈ 0.6→31°), 절벽은 의도적 등반 불가
                //   (스폰/성/호수/경계는 기존 cliffSuppression이 보호). 메사(대지) 레이어는 NationHeight 참조.
                case NationType.East:   return new NationParams { nation = nation, amplitudeA = 13f, freq0 = 0.005f, cliffDropC = 8f,  ridgeGate = 0.60f, terraceStep = 2f   };
                case NationType.West:   return new NationParams { nation = nation, amplitudeA = 14f, freq0 = 0.006f, cliffDropC = 14f, ridgeGate = 0.55f, terraceStep = 3f   };
                case NationType.South:  return new NationParams { nation = nation, amplitudeA = 7f,  freq0 = 0.005f, cliffDropC = 4f,  ridgeGate = 0.62f, terraceStep = 2.5f };
                case NationType.North:  return new NationParams { nation = nation, amplitudeA = 16f, freq0 = 0.006f, cliffDropC = 16f, ridgeGate = 0.54f, terraceStep = 3.5f };
                case NationType.Empire: return new NationParams { nation = nation, amplitudeA = 2.5f, freq0 = 0.003f, cliffDropC = 1.5f, ridgeGate = 0.78f, terraceStep = 0f   };
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

            // ── 4.5) 협곡 강화: 서(험준 협곡)·남(화산 협곡) 절벽 낙차 추가 — cliffSuppression 보호 유지 ──
            //      m(RidgeCliffMask)은 보호 구역(cliffSuppression=0)에서 0이므로 스폰/성/호수/경계에 자동 무해.
            //      저주파(0.5×freq0) Fbm으로 협곡 깊이에 공간 변주 — 시드 고정(nseed+777)으로 결정론 보장.
            if (nation == NationType.West || nation == NationType.South)
            {
                float canyonBoost = (nation == NationType.West) ? 4f : 2.5f;
                h += canyonBoost * m * (0.5f + 0.5f * Fbm(wx * p.freq0 * 0.5f, wz * p.freq0 * 0.5f, 2, LACUNARITY, GAIN, nseed + 777)) * cliffSuppression;
            }

            // ── 5) Mesa/대지 (예시2 단차): 셀 기반 평탄 대지 — 가장자리는 급경사 절벽 느낌 ──
            //      cliffSuppression을 곱해 스폰/성/호수/경계 보호 구역에는 생성 금지.
            h += MesaLift(wx, wz, nseed) * MESA_HEIGHT * cliffSuppression;

            return h;
        }

        // ── Mesa/대지 파라미터 (예시2: 평탄한 대지 + 절벽 가장자리) ──
        const float MESA_HEIGHT = 6f;    // 대지 융기 (m)
        const float MESA_CELL = 140f;    // 셀 크기 (m) — 평균 간격
        const float MESA_CHANCE = 0.35f; // 셀당 메사 생성 확률 (0.15→0.25→0.35 증빈 — 메사/대지 밀도 상향)
        const float MESA_RADIUS = 50f;   // 대지 반경 (m)
        const float MESA_EDGE = 6f;      // 가장자리 전환 폭 (m) — 6m/6m ≈ 급경사

        /// <summary>
        /// 메사(대지) 마스크 [0,1] — 140m 셀 그리드에서 35% 확률로 평탄 대지 생성 (MESA_CHANCE 0.15→0.25→0.35 증빈).
        /// 내부는 층상 단차(StratifyMesa, 중심 1.0 유지), 가장자리 6m에서 급강하(절벽 느낌). 결정론적 해시.
        /// </summary>
        static float MesaLift(float wx, float wz, int nseed)
        {
            int cx = Mathf.FloorToInt(wx / MESA_CELL);
            int cz = Mathf.FloorToInt(wz / MESA_CELL);
            // 주변 1셀까지 검사(셀 경계 걸침 대응)
            float best = 0f;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cellX = cx + dx, cellZ = cz + dz;
                    if (Hash2(cellX, cellZ, nseed) > MESA_CHANCE) continue;
                    // 셀 중심 + 지터(±40m) — 격자 느낌 제거
                    float centerX = cellX * MESA_CELL + MESA_CELL * 0.5f + (Hash2(cellX, cellZ, nseed + 11) - 0.5f) * 40f;
                    float centerZ = cellZ * MESA_CELL + MESA_CELL * 0.5f + (Hash2(cellX, cellZ, nseed + 17) - 0.5f) * 40f;
                    float d = Mathf.Sqrt((wx - centerX) * (wx - centerX) + (wz - centerZ) * (wz - centerZ));
                    float m = 1f - Smoothstep(MESA_RADIUS - MESA_EDGE, MESA_RADIUS, d);
                    if (m > best) best = m;
                }
            }
            // 층상 단차(stratification): 중심부를 계단식 층으로 — 가장자리(m≈0)는 첫 층 램프로 통과
            best = StratifyMesa(best);
            return best;
        }

        // 메사 중심부를 층상 단차로 (stratification) — 가장자리 급경사 + 내부 계단
        const float MESA_STRATA_COUNT = 14f;   // 층 수

        /// <summary>메사 마스크 [0,1]을 계단식 층으로 양자화 — 내부 층단 대지, 층 경계는 미세 램프(하드 컷 방지).</summary>
        static float StratifyMesa(float m)
        {
            if (m <= 0f) return 0f;
            // m (0~1): 0=가장자리, 1=중심. 층 경계마다 m을 잘라 계단 만든다.
            float strata = Mathf.Floor(m * MESA_STRATA_COUNT) / MESA_STRATA_COUNT;
            // 층과 층 사이 미세 램프 (하드 컷 방지) — 각 층 내에서 m의 잔차로 부드럽게
            float frac = (m - strata) / (1f / MESA_STRATA_COUNT);
            return Mathf.Lerp(strata, strata + (1f / MESA_STRATA_COUNT), frac * 0.15f);
        }

        /// <summary>결정론적 2D 해시 [0,1] — 좌표+시드 기반 (메사 셀 선택/지터용).</summary>
        static float Hash2(int x, int z, int s)
        {
            uint h = (uint)(x * 374761393 + z * 668265263 + s * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h = h ^ (h >> 16);
            return h / (float)uint.MaxValue;
        }

        // 경계/보호 구역의 저스트디테일 진폭 계수 (0.35 = ~36% 진폭 → 완만 경사 보증)
        public const float LOW_DETAIL_AMP_FACTOR = 0.35f;

        // ====================================================================
        // Phase T-R3: 야생화 패치 & 판타지 서브존 마스크 (R4가 사용)
        // ====================================================================

        // ── 야생화 패치 (예시의 꽃 들판) ──────────────────────────────────────
        // Z4: 야생화 커버리지 8%→14% (FLOWER_LO 하향 — Smoothstep 통과점을 저주파쪽으로 내림)
        // 현재: FLOWER_FREQ 0.012→0.009→0.007 — 패치 반경 ~83m→~110m→~140m 확대 (FLOWER_LO 0.70 / FLOWER_HI 0.90 유지)
        const float FLOWER_FREQ = 0.007f;       // Z5: 주파수 0.007 (패치 반경 ~140m — 더 넓은 단색 꽃밭)
        const float FLOWER_OX = 3.7f;           // 패치 분포 오프셋 (전 세계 균일, 국가 무관)
        const float FLOWER_OZ = 11.3f;
        const float FLOWER_LO = 0.70f;          // 커버리지 ~22% (Smoothstep(0.70,0.90,n) — 0.78/~14%→0.70/~22% 상향)
        const float FLOWER_HI = 0.90f;

        /// <summary>
        /// 야생화 패치 마스크 [0,1] — 주파수 0.009, 커버리지 약 22% (T-R3 §3 / AA4 / Z5 주파수 0.012→0.009).
        /// 주파수를 낮춰 패치당 면적(반경 ~110m)을 넓히고, FLOWER_LO 0.78→0.70 하향으로
        /// 커버리지를 ≈14%→≈22%로 높였다 → 넓은 단색 꽃밭 연출.
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
        const float FOREST_RADIUS_MIN   = 120f;   // AA4: 군락(클러스터) 반경 120~220m (숲 확대)
        const float FOREST_RADIUS_MAX   = 220f;
        const float FOREST_CENTER_MIN   = 150f;   // 성(0,0)에서 군락 중심 거리 150~500m
        const float FOREST_CENTER_MAX   = 500f;
        const float FOREST_EXCLUDE_DIST = 150f;   // 스폰/성/호수 반경 150m 밖
        const float FOREST_EDGE_SOFT    = 25f;    // 군락 가장자리 부드러운 페이드

        /// <summary>
        /// Z4/AA4: 국가별 숲 군락 마스크 [0,1] — 국가당 3~5개(결정론), 군락 반경 120~220m,
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

        // ====================================================================
        // Phase T-D2 (09-08): 지형 다양화 2차 — 노출 암반 / 대형 분지 / 서쪽 천연 아치 /
        // 대형 꽃 융단 마스크 (예시2~13 Gap G1/G2/G5/G7 충전).
        //   · 모든 위치는 결정론 해시(H01/Hash2) — UnityEngine.Random 미사용
        //   · 높이 델타 적용은 TerrainGenerator.ComputeSubBiomeVariation이 담당
        //     (cliffSuppression 보호 구역 = 스폰/성/호수/경계 자동 무해)
        //   · 마스크 자체는 억제를 모르는 순수 형태 함수 — 텍스처(T2)/데코(T3)에서
        //     TerrainGenerator.SampleCliffSuppression으로 자체 필터링
        // ====================================================================

        // ── 노출 암반 (outcrop — 예시2/4/6/8/9/12/13 전체의 암돔 실루엣) ──
        const float OUTCROP_CELL = 320f;   // 셀 크기 — 메사(140m)보다 성긴 랜드마크 간격

        /// <summary>아웃크롭 사이트 (T3 데코 위성 바위 군집 배치용).</summary>
        public struct OutcropSite
        {
            public Vector2 center;
            public float radius;
        }

        /// <summary>방위별 아웃크롭 파라미터 (셀 확률/반경/층수) — South(사막)는 낮고 작게, Empire 아주 드물게.</summary>
        static void OutcropParams(NationType nation, out float chance, out float radMin, out float radMax, out float strata)
        {
            switch (nation)
            {
                case NationType.East:   chance = 0.50f; radMin = 26f; radMax = 44f; strata = 6f; break;
                case NationType.West:   chance = 0.40f; radMin = 28f; radMax = 48f; strata = 5f; break;
                case NationType.South:  chance = 0.22f; radMin = 20f; radMax = 34f; strata = 6f; break;
                case NationType.North:  chance = 0.45f; radMin = 24f; radMax = 42f; strata = 6f; break;
                case NationType.Empire: chance = 0.12f; radMin = 18f; radMax = 28f; strata = 7f; break;
                default:                chance = 0.50f; radMin = 26f; radMax = 44f; strata = 6f; break;
            }
        }

        /// <summary>
        /// 노출 암반 마스크 [0,1] — 320m 셀 해시 배치 + ±60m 지터, 부드러운 암돔.
        /// 중심부는 층단(5~7층 — StratifyMesa 축소판, 하드 컷 방지 램프 유지 → 예시9 "층 진 단면").
        /// West는 천연 아치(GetWestArchPosition) 받침 바위 2개를 강제 포함.
        /// 높이 반영은 ComputeSubBiomeVariation(×cliffSuppression) — 이 함수는 순수 형태만 반환.
        /// </summary>
        public static float GetOutcropMask(float x, float z, NationType nation, int seed)
        {
            int nseed = seed + NationSeedOffset(nation) + 9001;
            OutcropParams(nation, out float chance, out float radMin, out float radMax, out float strata);

            int cx = Mathf.FloorToInt(x / OUTCROP_CELL);
            int cz = Mathf.FloorToInt(z / OUTCROP_CELL);
            float best = 0f;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cellX = cx + dx, cellZ = cz + dz;
                    if (Hash2(cellX, cellZ, nseed) > chance) continue;
                    float centerX = cellX * OUTCROP_CELL + OUTCROP_CELL * 0.5f + (Hash2(cellX, cellZ, nseed + 11) - 0.5f) * 120f;
                    float centerZ = cellZ * OUTCROP_CELL + OUTCROP_CELL * 0.5f + (Hash2(cellX, cellZ, nseed + 17) - 0.5f) * 120f;
                    float radius = Mathf.Lerp(radMin, radMax, Hash2(cellX, cellZ, nseed + 23));
                    float edge = radius * 0.45f;
                    float dd = Mathf.Sqrt((x - centerX) * (x - centerX) + (z - centerZ) * (z - centerZ));
                    if (dd >= radius) continue;
                    float m = 1f - Smoothstep(radius - edge, radius, dd);
                    if (m > best) best = m;
                }
            }

            // West 아치 받침 바위 2개 (아치 중심 ±15m 수직방향 — 강제 아웃크롭)
            if (nation == NationType.West)
            {
                Vector3 arch = GetWestArchPosition(seed);
                Vector2 arch2 = new Vector2(arch.x, arch.z);
                Vector2 dir = arch2.normalized;            // 원점→아치 방향
                Vector2 perp = new Vector2(-dir.y, dir.x);
                for (int p = 0; p < 2; p++)
                {
                    float side = (p == 0) ? 1f : -1f;
                    Vector2 pc = arch2 + perp * (15f * side);
                    float dd = Mathf.Sqrt((x - pc.x) * (x - pc.x) + (z - pc.y) * (z - pc.y));
                    if (dd < 12f)
                    {
                        float m = 1f - Smoothstep(7f, 12f, dd);
                        if (m > best) best = m;
                    }
                }
            }
            if (best <= 0f) return 0f;

            // 층단 (예시9 "층 진 바위 단면") — StratifyMesa 축소판 (층수 5~7)
            float stratum = Mathf.Floor(best * strata) / strata;
            float frac = (best - stratum) / (1f / strata);
            return Mathf.Lerp(stratum, stratum + (1f / strata), frac * 0.15f);
        }

        /// <summary>
        /// 아웃크롭 중심 열거 (T3 위성 바위 군집용) — GetOutcropMask와 동일 배치 수식+시드.
        /// 방위 부채꼴(baseAng ±42°) 내, 스폰/성(0,0) 250m+ · 호수 300m+ 이격. Empire는 원점 250~500m 링.
        /// </summary>
        public static List<OutcropSite> GetOutcropCenters(NationType nation, int seed)
        {
            var result = new List<OutcropSite>();
            int nseed = seed + NationSeedOffset(nation) + 9001;
            OutcropParams(nation, out float chance, out float radMin, out float radMax, out float _);

            Vector3 spawn = ProjectName.Core.PlayerSpawnConfig.SpawnPosition;
            var lakes = TerrainGenerator.Lakes;
            float baseAng = NationBaseAngle(nation);

            int half = Mathf.CeilToInt(1600f / OUTCROP_CELL) + 1;
            for (int cz2 = -half; cz2 <= half; cz2++)
            {
                for (int cx2 = -half; cx2 <= half; cx2++)
                {
                    if (Hash2(cx2, cz2, nseed) > chance) continue;
                    float centerX = cx2 * OUTCROP_CELL + OUTCROP_CELL * 0.5f + (Hash2(cx2, cz2, nseed + 11) - 0.5f) * 120f;
                    float centerZ = cz2 * OUTCROP_CELL + OUTCROP_CELL * 0.5f + (Hash2(cx2, cz2, nseed + 17) - 0.5f) * 120f;
                    float dist0 = Mathf.Sqrt(centerX * centerX + centerZ * centerZ);
                    if (dist0 > 1500f) continue;

                    if (nation == NationType.Empire)
                    {
                        if (dist0 < 250f || dist0 > 500f) continue;
                    }
                    else
                    {
                        float ang = Mathf.Atan2(centerZ, centerX) * Mathf.Rad2Deg;
                        if (Mathf.Abs(Mathf.DeltaAngle(ang, baseAng)) > 42f) continue;
                    }
                    if (Vector2.Distance(new Vector2(centerX, centerZ), new Vector2(spawn.x, spawn.z)) < 250f) continue;
                    if (dist0 < 250f) continue;
                    bool nearLake = false;
                    if (lakes != null)
                    {
                        for (int i = 0; i < lakes.Count; i++)
                        {
                            float dx2 = centerX - lakes[i].center.x, dz2 = centerZ - lakes[i].center.z;
                            if (dx2 * dx2 + dz2 * dz2 < 300f * 300f) { nearLake = true; break; }
                        }
                    }
                    if (nearLake) continue;
                    result.Add(new OutcropSite { center = new Vector2(centerX, centerZ), radius = Mathf.Lerp(radMin, radMax, Hash2(cx2, cz2, nseed + 23)) });
                }
            }
            return result;
        }

        // ── 대형 분지 (예시9: 병풍 절벽 둘러싼 분지) ──
        public const float BASIN_RADIUS_MIN = 90f;
        public const float BASIN_RADIUS_MAX = 130f;
        public const float BASIN_EXCLUDE = 300f;   // 스폰/성/호수 이격 (대형 카브 밴드 대비)

        // 수동 정의 대형 호수 테이블 — TerrainGenerator.GenerateLakes의 AddHandPlacedLake 3개와 동기화.
        // (재귀 가드: GenerateLakes의 waterLevel 계산 중엔 TerrainGenerator.LakesOrNull이 null이므로
        //  이 고정 테이블로만 배제 — 부팅 단계와 무관하게 동일 배제 결과 = 결정론 유지)
        static readonly Vector3[] HandLakeTable =
        {
            new Vector3(400f, 0f, 300f),     // r180
            new Vector3(-560f, 0f, -520f),   // r150
            new Vector3(423f, 0f, 906f),     // r130 (T-D2)
        };
        static readonly float[] HandLakeRadius = { 180f, 150f, 130f };

        /// <summary>수동 대형 호수 3개 + (가능 시) 절차적 호수 전체에 대한 이격 검사. [T-D2]</summary>
        static bool IsTooCloseToLake(Vector2 c, float dist, System.Collections.Generic.IReadOnlyList<TerrainGenerator.TerrainLakeDef> lakes)
        {
            for (int i = 0; i < HandLakeTable.Length; i++)
            {
                float dx = c.x - HandLakeTable[i].x, dz = c.y - HandLakeTable[i].z;
                float need = dist + HandLakeRadius[i];
                if (dx * dx + dz * dz < need * need) return true;
            }
            if (lakes != null)
            {
                for (int i = 0; i < lakes.Count; i++)
                {
                    float dx = c.x - lakes[i].center.x, dz = c.y - lakes[i].center.z;
                    if (dx * dx + dz * dz < dist * dist) return true;
                }
            }
            return false;
        }

        /// <summary>분지 정보 (높이 경로 O(1)화 — 캐시 필수, 매 샘플 해시 루프 금지).</summary>
        public struct BasinInfo
        {
            public Vector2 center;
            public float radius;
            public float wallAngleRad;
            public bool valid;
        }

        static Dictionary<int, BasinInfo> _basinCache;
        static int _basinCacheSeed = int.MinValue;

        /// <summary>방위 중심 각도(도) — GetForestPatchMask와 동일 규약 (동0/북90/서180/남270).</summary>
        static float NationBaseAngle(NationType nation)
        {
            switch (nation)
            {
                case NationType.North: return 90f;
                case NationType.West:  return 180f;
                case NationType.South: return 270f;
                default:               return 0f;   // East
            }
        }

        /// <summary>
        /// 방위당 1개 대형 분지 중심 (결정론 + (nation,seed) 캐시) — 반경 90~130m,
        /// 스폰/성/호수 300m+ 이격. 8회 결정론 재시도 후 실패 시 valid=false (분지 없음 — 회귀 없음).
        /// 병풍 절벽(wallAngle)은 분지 바깥쪽(원점 반대편 = baseAng 방향) 부채꼴.
        /// </summary>
        public static BasinInfo GetBasinCenter(NationType nation, int seed)
        {
            if (_basinCache == null || _basinCacheSeed != seed)
            {
                _basinCache = new Dictionary<int, BasinInfo>();
                _basinCacheSeed = seed;
            }
            if (_basinCache.TryGetValue((int)nation, out BasinInfo cached)) return cached;

            BasinInfo info = default;
            int nseed = seed + NationSeedOffset(nation) + 9203;
            float baseAng;
            float distMin, distMax;
            if (nation == NationType.Empire) { baseAng = 45f; distMin = 250f; distMax = 380f; }
            else { baseAng = NationBaseAngle(nation); distMin = 500f; distMax = 1100f; }

            Vector3 spawn = ProjectName.Core.PlayerSpawnConfig.SpawnPosition;
            var lakes = TerrainGenerator.Lakes;

            for (int k = 0; k < 8; k++)
            {
                float ang = baseAng + (H01(nseed, 10 + k) - 0.5f) * 70f;
                float dist = distMin + H01(nseed, 30 + k) * (distMax - distMin);
                Vector2 c = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad) * dist, Mathf.Sin(ang * Mathf.Deg2Rad) * dist);
                if (Vector2.Distance(c, new Vector2(spawn.x, spawn.z)) < BASIN_EXCLUDE) continue;
                if (c.magnitude < BASIN_EXCLUDE) continue;   // 성(0,0)
                bool nearLake = false;
                if (lakes != null)
                {
                    for (int i = 0; i < lakes.Count; i++)
                    {
                        float dx = c.x - lakes[i].center.x, dz = c.y - lakes[i].center.z;
                        if (dx * dx + dz * dz < BASIN_EXCLUDE * BASIN_EXCLUDE) { nearLake = true; break; }
                    }
                }
                if (nearLake) continue;
                info.center = c;
                info.radius = BASIN_RADIUS_MIN + H01(nseed, 50) * (BASIN_RADIUS_MAX - BASIN_RADIUS_MIN);
                info.wallAngleRad = baseAng * Mathf.Deg2Rad;
                info.valid = true;
                break;
            }
            _basinCache[(int)nation] = info;
            return info;
        }

        /// <summary>분지 내부 마스크 [0,1] — 중심 1, 가장자리 35m 페이드 (T2 분지 바닥 텍스처용 공개).</summary>
        public static float GetBasinMask(float x, float z, NationType nation, int seed)
        {
            BasinInfo b = GetBasinCenter(nation, seed);
            if (!b.valid) return 0f;
            float d = Vector2.Distance(new Vector2(x, z), b.center);
            return 1f - Smoothstep(b.radius - 35f, b.radius, d);
        }

        // ── 서쪽 천연 아치 (예시5) — 받침 지형 위치. 실제 아치 메시 배치는 T3 데코 담당 ──
        static Vector3? _westArchCache;
        static int _westArchCacheSeed = int.MinValue;

        /// <summary>
        /// 서쪽 천연 아치 설치 위치 (결정론 + 캐시) — West 부채꼴 150~210°, 원점 600~900m,
        /// 스폰/성 250m+ · 호수 300m+ 이격. 12회 시도 후 실패 시 (-700,0) fallback.
        /// GetOutcropMask가 이 위치 ±15m에 받침 바위(지형)를 강제 생성한다.
        /// </summary>
        public static Vector3 GetWestArchPosition(int seed)
        {
            if (_westArchCache.HasValue && _westArchCacheSeed == seed) return _westArchCache.Value;
            int nseed = seed + NationSeedOffset(NationType.West) + 9307;
            Vector3 spawn = ProjectName.Core.PlayerSpawnConfig.SpawnPosition;
            var lakes = TerrainGenerator.Lakes;
            Vector3 result = new Vector3(-700f, 0f, 0f);   // fallback
            for (int k = 0; k < 12; k++)
            {
                float ang = 180f + (H01(nseed, k) - 0.5f) * 60f;          // 서 부채꼴 150~210°
                float dist = 600f + H01(nseed, 20 + k) * 300f;            // 600~900m
                Vector3 c = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * dist, 0f, Mathf.Sin(ang * Mathf.Deg2Rad) * dist);
                if (Vector3.Distance(c, spawn) < 250f) continue;
                if (Vector3.Distance(c, Vector3.zero) < 250f) continue;
                bool nearLake = false;
                if (lakes != null)
                {
                    for (int i = 0; i < lakes.Count; i++)
                    {
                        if (Vector3.Distance(c, lakes[i].center) < 300f) { nearLake = true; break; }
                    }
                }
                if (nearLake) continue;
                result = c;
                break;
            }
            _westArchCache = result;
            _westArchCacheSeed = seed;
            return result;
        }

        // ── 대형 꽃 융단 (예시12/13: 핑크/마젠타 카펫 — Empire/East 중심) ──
        const float MEGA_FLOWER_RADIUS_MIN = 160f;
        const float MEGA_FLOWER_RADIUS_MAX = 200f;
        const float MEGA_FLOWER_EDGE_SOFT  = 30f;

        /// <summary>
        /// 대형 꽃 융단 마스크 [0,1] — 방위당 0~3개 패치(반경 160~200m, 가장자리 30m 페이드).
        /// Empire 3개 / East 2개 / 타 방위 50% 확률 0~1개. 스폰/성/호수 250m+ 이격.
        /// T2(텍스처 핑크 블롯) + T3(꽃 밀도 ×2)가 함께 사용 — GetFlowerPatchMask와 별개(합산 아님).
        /// </summary>
        public static float GetMegaFlowerPatchMask(float x, float z, NationType nation, int seed)
        {
            int nseed = seed + NationSeedOffset(nation) + 9503;
            int count;
            switch (nation)
            {
                case NationType.Empire: count = 3; break;
                case NationType.East:   count = 2; break;
                default:                count = H01(nseed, 0) < 0.5f ? 0 : 1; break;
            }
            if (count == 0) return 0f;

            Vector3 spawn = ProjectName.Core.PlayerSpawnConfig.SpawnPosition;
            var lakes = TerrainGenerator.Lakes;
            float baseAng = (nation == NationType.Empire) ? 45f : NationBaseAngle(nation);
            float distMin = (nation == NationType.Empire) ? 250f : 350f;
            float distMax = (nation == NationType.Empire) ? 450f : 1250f;

            float best = 0f;
            for (int k = 0; k < count; k++)
            {
                float ang = baseAng + (H01(nseed, 10 + k) - 0.5f) * 84f;
                float dist = distMin + H01(nseed, 30 + k) * (distMax - distMin);
                Vector2 c = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad) * dist, Mathf.Sin(ang * Mathf.Deg2Rad) * dist);
                if (Vector2.Distance(c, new Vector2(spawn.x, spawn.z)) < 250f) continue;
                if (c.magnitude < 250f) continue;
                bool nearLake = false;
                if (lakes != null)
                {
                    for (int i = 0; i < lakes.Count; i++)
                    {
                        float dx = c.x - lakes[i].center.x, dz = c.y - lakes[i].center.z;
                        if (dx * dx + dz * dz < 250f * 250f) { nearLake = true; break; }
                    }
                }
                if (nearLake) continue;
                float radius = MEGA_FLOWER_RADIUS_MIN + H01(nseed, 60 + k) * (MEGA_FLOWER_RADIUS_MAX - MEGA_FLOWER_RADIUS_MIN);
                float dd = Vector2.Distance(new Vector2(x, z), c);
                float m = 1f - Smoothstep(radius - MEGA_FLOWER_EDGE_SOFT, radius, dd);
                if (m > best) best = m;
            }
            return Mathf.Clamp01(best);
        }
    }
}