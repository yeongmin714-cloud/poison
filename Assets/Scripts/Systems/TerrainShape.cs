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
                case NationType.East:   return new NationParams { nation = nation, amplitudeA = 7f,  freq0 = 0.004f, cliffDropC = 4f, ridgeGate = 0.65f, terraceStep = 0f   };
                case NationType.West:   return new NationParams { nation = nation, amplitudeA = 9f,  freq0 = 0.005f, cliffDropC = 6f, ridgeGate = 0.62f, terraceStep = 2.5f };
                case NationType.South:  return new NationParams { nation = nation, amplitudeA = 8f,  freq0 = 0.004f, cliffDropC = 5f, ridgeGate = 0.60f, terraceStep = 3f   };
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
        public static float NationHeight(float x, float z, NationType nation, int seed, float cliffSuppression = 1f)
        {
            NationParams p = GetNationParams(nation);
            int nseed = seed + NationSeedOffset(nation);

            // ── 1) 도메인 워핑: 직선처럼 보이는 능선을 유기적으로 굽힌다 ──
            float warpX = Fbm(x * WARP_FREQ + WARP_OX, z * WARP_FREQ + WARP_OZ, OCTAVES, LACUNARITY, GAIN, nseed + 1234);
            float warpZ = Fbm(z * WARP_FREQ + WARP_OZ, x * WARP_FREQ + WARP_OX, OCTAVES, LACUNARITY, GAIN, nseed + 5678);
            float wx = x + WARP_AMOUNT * (warpX - 0.5f) * 2f;
            float wz = z + WARP_AMOUNT * (warpZ - 0.5f) * 2f;

            // ── 2) Base: 완만한 구릉 (F1) ──
            float baseFbm = Fbm(wx * p.freq0, wz * p.freq0, OCTAVES, LACUNARITY, GAIN, nseed);
            float baseH = (baseFbm - 0.5f) * 2f * p.amplitudeA;   // ±A

            // ── 3) Cliff: ridged 능선 마스크 + 낙차 (F2) ──
            float ridgedNoise = Ridge(Fbm(
                wx * p.freq0 * CLIFF_FREQ_SCALE,
                wz * p.freq0 * CLIFF_FREQ_SCALE,
                OCTAVES, LACUNARITY, GAIN, nseed + 2345));
            float m = Smoothstep(p.ridgeGate, p.ridgeGate + CLIFF_GATE_SPAN, ridgedNoise);
            m *= Mathf.Clamp01(cliffSuppression);                 // 보호 구역 절벽 금지

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
            h += -p.amplitudeA * VALLEY_AMP_RATIO * Smoothstep(VALLEY_LO, VALLEY_HI, valleyNoise);

            return h;
        }
    }
}