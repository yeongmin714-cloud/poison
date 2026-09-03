using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Perlin Noise 기반 절차적 지형 생성기 (FBM 다중 옥타브 노이즈 + 고원 구역)
    /// BiomeDefinition의 파라미터를 사용해 높이맵 생성 → Mesh 변환
    /// </summary>
    public static class TerrainGenerator
    {
        // === FBM (Fractal Brownian Motion) 하드코딩 상수 ===
        // 노이즈 옥타브 수 — 클수록 디테일이 증가 (20m 정점 간격 한계상 4옥타브가 적정: 5옥타브는 10m 파장으로 앨리어싱)
        private const int FBM_OCTAVES = 4;
        // 주파수 배율 — 옥타브가 올라갈 때마다 주파수가 이 배율만큼 증가
        private const float FBM_LACUNARITY = 2.0f;
        // 진폭 감쇠 — 옥타브가 올라갈 때마다 진폭이 이 배율만큼 감소 (0.5 → 0.55, 고주파 디테일 유지)
        private const float FBM_GAIN = 0.55f;
        // 고원(plateau) 평탄화 시작 임계값
        private const float PLATEAU_THRESHOLD = 0.55f;
        // 고원 평탄화 감쇠 계수
        private const float PLATEAU_SLOPE = 0.2f;
        // === 방위별 지형 블렌딩 상수 ===
        // 국경 전환 폭 (size=2000f 기준 약 size*0.06 ≈ 120m) — 각도 경계 근처에서 이웃 방위와 크로스페이드
        private const float TRANSITION_WIDTH = 120f;
        // 황제국 중앙 영역 반경 (NationTerrainController.GetNationFromPosition 기준 50m)
        private const float EMPIRE_RADIUS = 50f;

        // === 호수 시스템 상수 ===
        // 소품/청동 오브젝트 배치용 고정 시드 결정론적 호수 목록 (Random 언시드 금지)
        private const int LAKE_COUNT = 6;
        private const long LAKE_LCG_SEED = 1234567891L;     // 결정론적 LCG 시드
        private const float LAKE_MIN_DIST = 250f;          // 호수 간 최소 거리
        private const float LAKE_EDGE_MARGIN = 150f;       // 지도 경계(±1000) 여백
        private const float LAKE_EMPIRE_EXCLUDE = 120f;    // 황제국 중앙(0,0,0) 배제 반경
        private const float LAKE_RADIUS_MIN = 40f;
        private const float LAKE_RADIUS_MAX = 70f;
        private const float LAKE_DEPTH_MIN = 3f;
        private const float LAKE_DEPTH_MAX = 5f;
        private const float LAKE_WATER_OFFSET = 1.5f;      // 분지 바닥 위 물 표면 높이
        private const float LAKE_SHORE_FACTOR = 1.3f;      // 경사 완만한 해안 확장 배율 (카브 영향 반경 = radius*이값)

        // === 스폰지 평탄화 상수 ===
        // (SPAWN_X, SPAWN_Z) 대신 PlayerSpawnConfig.SpawnPosition을 사용 (단일 소스).
        // T-R2: 스폰 반경 30m 절대 평탄(Test c 통과 목표) + 30..45m는 원래 지형으로 스무스 페이드.
        private const float SPAWN_FLATTEN_RADIUS = 30f;   // 반경 30m 내 완전 평탄 (상수 고도)
        private const float SPAWN_FLATTEN_FADE = 20f;     // 30..50m 구간 원래 지형으로 부드럽게 복귀

        // === 절벽 보호 구역 상수 (스폰/성/호수/경계) ===
        // 스폰·성·호수 반경 PROTECT_CLIFF_RADIUS 내 절벽 마스크 0 강제 (통행 방지,
        // 계획 리스크 매트릭스 "스폰/성/호수 반경 40m는 절벽 금지"). PROTECT_FADE는 복귀 페이드폭.
        private const float PROTECT_CLIFF_RADIUS = 40f;
        private const float PROTECT_CLIFF_FADE = 15f;
        // 방위 경계선(45/135/225/315°) 반경 BOUNDARY_CLIFF_BAN 안 절벽 금지 —
        // 경계 크로스페이드 구간을 평탄한 구릉으로 유지해 |Δh|<0.5m 연속성 보증 (Test b).
        private const float BOUNDARY_CLIFF_BAN = 35f;
        private const float BOUNDARY_CLIFF_FADE = 20f;

        // === Tundra ridged 믹스 ===
        private const float RIDGED_MIX = 0.3f;

        /// <summary>
        /// 주어진 월드 좌표에서 지형 높이 반환 (T-R2 방위별 스타일라이즈드 형태).
        ///
        /// ⚠ biome 인자는 레거시 호환용이며 이 구현에서 무시된다.
        /// 실제 높이는 위치(x,z) 기반 NationTerrainController.GetNationFromPosition 판정의
        /// 방위별 파라미터(동/서/남/북/황제국) + 경계 결과 보간으로 결정된다.
        /// 시그니처 유지 — 호출부(PlayerMovement/데코/호수/영지 등)는 코드 수정 없이
        /// 자동으로 방위별 지형을 따르게 된다 (계획 §5.3 호환성 핵심).
        /// TerrainModelPlacer 등에서 Raycast 없이 높이 샘플링용.
        /// </summary>
        public static float GetHeightAt(float worldX, float worldZ, BiomeType biome, int seed = 42)
        {
            BiomeDefinition def = BiomeData.GetDefinition(biome);
            return GetHeightAtWithDefinition(worldX, worldZ, def, seed);
        }

        /// <summary>
        /// BiomeDefinition으로 높이 샘플링
        /// </summary>
        public static float GetHeightAtWithDefinition(float worldX, float worldZ, BiomeDefinition def, int seed = 42)
        {
            float height = ComputeTerrainHeight(worldX, worldZ, def.type, seed);

            // waterThreshold가 있으면 물 높이로 클램프 (물 로직 유지)
            if (def.waterThreshold > 0f && height < def.waterThreshold)
            {
                height = def.waterThreshold;
            }
            
            return height;
        }

        /// <summary>
        /// 공통 높이 계산 헬퍼 — FBM 노이즈(0~1) → 고원 변환 → def.noiseAmplitude 곱
        /// </summary>
        private static float ComputeBaseHeight(float x, float z, BiomeDefinition def, int seed)
        {
            // FBM 다중 옥타브 노이즈 (0~1 정규화)
            float fbm = FbmNoise(
                x * def.noiseFrequency,
                z * def.noiseFrequency,
                FBM_OCTAVES,
                FBM_LACUNARITY,
                FBM_GAIN,
                seed);

            // 고원(plateau) 변환으로 상위 높이 구간 평탄화
            float plateau = ApplyPlateau(fbm);

            // 최종 높이 = 평탄화된 노이즈 × 진폭
            return plateau * def.noiseAmplitude;
        }

        /// <summary>
        /// FBM (Fractal Brownian Motion) — 다중 옥타브 Perlin 노이즈를 누적해 더 자연스러운 지형 생성.
        /// 각 옥타브에 seed 기반 오프셋을 섞어 옥타브마다 서로 다른 패턴이 나오게 하고,
        /// amplitude 합으로 정규화해 결과를 0~1 범위로 유지한다.
        /// </summary>
        private static float FbmNoise(float x, float z, int octaves, float lacunarity, float gain, int seed)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float amplitudeSum = 0f;

            for (int o = 0; o < octaves; o++)
            {
                // seed 기반 옥타브별 오프셋 — 옥타브마다 다른 패턴 유도
                float noiseX = x * frequency + seed * 0.371f;
                float noiseZ = z * frequency + seed * 0.713f;

                total += Mathf.PerlinNoise(noiseX, noiseZ) * amplitude;
                amplitudeSum += amplitude;

                frequency *= lacunarity;
                amplitude *= gain;
            }

            // amplitude 합으로 정규화 → 0~1 유지
            return amplitudeSum > 0f ? total / amplitudeSum : 0f;
        }

        /// <summary>
        /// 고원(plateau) 변환 — 상위 높이 구간(t>0.55)을 평탄화해 대지/고원 모양 생성.
        /// 임계값을 초과한 만큼의 일부(0.2배)만 치우쳐 높이가 빠르게 올라가지 않도록 억제.
        /// </summary>
        private static float ApplyPlateau(float t)
        {
            if (t > PLATEAU_THRESHOLD)
            {
                // 임계값 초과분의 0.2배만 반영 — 완만한 고원 평면 유도
                return PLATEAU_THRESHOLD + (t - PLATEAU_THRESHOLD) * PLATEAU_SLOPE;
            }
            return t;
        }

        /// <summary>
        /// 단일 방위 기준 높이 계산 (T-R2).
        /// TerrainShape.NationHeight(FBM base + ridged 절벽 + 도메인워핑 + terrace + 계곡)를
        /// 호출하고, 스폰/호수/성/방위경계 보호 절벽 억제(cliffSuppression)와
        /// 경계부 Base 완만화(baseDetail)를 주입한다.
        /// </summary>
        /// <param name="x">월드 X</param>
        /// <param name="z">월드 Z</param>
        /// <param name="nation">방위</param>
        /// <param name="seed">기저 시드</param>
        /// <param name="cliffSuppression">[0,1] 절벽 억제 마스크 (기본 1=허용)</param>
        /// <param name="baseDetail">[0,1] Base/Valley 디테일 계수 (기본 1=원본)</param>
        private static float ComputeNationHeight(float x, float z, NationType nation, int seed,
            float cliffSuppression = 1f, float baseDetail = 1f)
        {
            return TerrainShape.NationHeight(x, z, nation, seed, cliffSuppression, baseDetail);
        }

        /// <summary>
        /// 각도(방위, 황제국 제외)만으로 방향성 국가 판정.
        /// GetNationFromPosition과 동일한 각도 경계를 사용하되 중앙 Empire 판정을 제외.
        /// </summary>
        private static NationType GetDirectionalNation(float angle)
        {
            // angle은 [0, 360)
            if (angle < 45f || angle >= 315f)
                return NationType.East;
            if (angle < 135f)
                return NationType.North;
            if (angle < 225f)
                return NationType.West;
            return NationType.South;
        }

        /// <summary>
        /// 방위별 절차적 지형 높이 계산.
        /// 월드 좌표 (x, z) → NationType 판정 → 방위별 고유 FBM 파라미터/시드로 높이 산출.
        /// 국경(내각 경계 각도 근처) 및 황제국 경계에서는 이웃 방위와 부드럽게 크로스페이드해
        /// 급격한 절벽/단차가 생기지 않도록 한다.
        /// </summary>
        /// <param name="x">월드 X 좌표</param>
        /// <param name="z">월드 Z 좌표</param>
        /// <param name="biome">기본 Biome (방위 판정이 우선함)</param>
        /// <param name="seed">기저 시드</param>
        private static float ComputeTerrainHeight(float x, float z, BiomeType biome, int seed)
        {
            float dist = Mathf.Sqrt(x * x + z * z);

            // 각도 계산 (0~360)
            float angle = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            NationType nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));

            // 절벽 억제 마스크 + Base 완만화 (스폰/성/호수/방위경계 보호) — 위치 전용, 방위 무관.
            // ComputeTerrainHeight 최상단에서 1회 계산해 모든 ComputeNationHeight 호출에 주입
            // (결과 보간 Lerp 시 두 국가 동일 마스크 유지 → 경계 연속성 |Δh|<0.5 보증).
            float suppression = ComputeCliffSuppression(x, z);
            float baseDetail = ComputeBaseDetail(x, z);

            float h = ComputeNationHeight(x, z, nation, seed, suppression, baseDetail);

            // === 1) 방향성 국가 간 각도 경계 크로스페이드 (T-R2: 두 결과의 보간) ===
            // 내각 경계 각도: 45°(동-북), 135°(북-서), 225°(서-남), 315°(남-동)
            // w = 경계 광선 수직 거리 기반 (경계선에서 w=0.5, 전환폭 ±120m에서 0/1).
            // H = Lerp(H_A, H_B, w) — 파라미터 보간이 아니라 "두 결과의 보간" (위상 뒤틀림 방지).
            float halfWidth = TRANSITION_WIDTH;

            // 동-북 경계 (45°)
            BlendBoundary(
                ref h, x, z, seed, suppression, baseDetail,
                NationType.East, NationType.North,
                0.70710678f, 0.70710678f, halfWidth);

            // 북-서 경계 (135°)
            BlendBoundary(
                ref h, x, z, seed, suppression, baseDetail,
                NationType.North, NationType.West,
                -0.70710678f, 0.70710678f, halfWidth);

            // 서-남 경계 (225°)
            BlendBoundary(
                ref h, x, z, seed, suppression, baseDetail,
                NationType.West, NationType.South,
                -0.70710678f, -0.70710678f, halfWidth);

            // 남-동 경계 (315°)
            BlendBoundary(
                ref h, x, z, seed, suppression, baseDetail,
                NationType.South, NationType.East,
                0.70710678f, -0.70710678f, halfWidth);

            // === 2) 황제국 방사형 경계 크로스페이드 ===
            // 중앙(반경 50m)은 황제국 평탄 지형, 바깥은 방향성 국가 지형.
            // 전환 구간 [EMPIRE_RADIUS - width, EMPIRE_RADIUS + width]에서 부드럽게 혼합.
            float empireT = Mathf.Clamp01((dist - (EMPIRE_RADIUS - TRANSITION_WIDTH)) / (2f * TRANSITION_WIDTH));
            if (empireT < 1f)
            {
                float empireH = ComputeNationHeight(x, z, NationType.Empire, seed, suppression, baseDetail);
                float dirH = ComputeNationHeight(x, z, GetDirectionalNation(angle), seed, suppression, baseDetail);
                h = Mathf.Lerp(empireH, dirH, empireT);
            }

            // === 3) 호수 분지 카브 ===
            // Lake.center에서 radius 이내를 부드럽게 파낸다 (smoothstep 팔오프, 최대 depth).
            // radius..radius*LAKE_SHORE_FACTOR 구간은 경사 완만한 해안(쇼어라인).
            // GetHeightAt / GetHeightAtWithDefinition / 메시 생성을 모두 지나는
            // 공통 관통 경로(ComputeTerrainHeight)에 위치해 모든 경로에 적용된다.
            // (절벽은 ApplyLakeBasins 이전 ComputeNationHeight에서 이미 억제됨 — 호수 중심 반경
            //  40m 절벽 금지로 수면 위 절벽/해안 단차 재발 방지.)
            h = ApplyLakeBasins(x, z, h);

            // === 4) 스폰지 평탄화 ===
            // PlayerSpawnConfig.SpawnPosition 반경 30m 절대 평탄 (상수 고도), 그 밖은 원래 지형.
            h = ApplySpawnFlattening(x, z, h, seed, suppression);

            return h;
        }

        /// <summary>
        /// 단일 각도 경계에 대한 크로스페이드 헬퍼 (T-R2 결과 보간).
        /// 점의 경계 광선 수직 거리가 전환 폭 안에 들어오면 이웃 국가 높이와 Lerp.
        /// 두 인접 국가의 absolute height를 각각 계산해 "결과 보간"한다.
        /// </summary>
        private static void BlendBoundary(
            ref float height, float x, float z, int seed, float cliffSuppression, float baseDetail,
            NationType negNation, NationType posNation,
            float ux, float uz, float halfWidth)
        {
            NationType nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));

            // 점이 해당 경계에 인접한 방향성 국가가 아니면 무시
            if (nation != negNation && nation != posNation)
                return;

            // 광선(u)에 대한 점 p=(x,z)의 수직(부호) 거리: cross = u.x * p.z - u.z * p.x
            float cross = ux * z - uz * x;

            // 블렌드 계수: cross가 [-width, width]일 때 0(음측)~1(양측)
            float t = Mathf.Clamp01(cross / (2f * halfWidth) + 0.5f);

            // 전환 구간 내에서만 실제 블렌딩 (바깥은 클램프로 무의미 → 생략)
            if (t > 0f && t < 1f)
            {
                float hNeg = ComputeNationHeight(x, z, negNation, seed, cliffSuppression, baseDetail);
                float hPos = ComputeNationHeight(x, z, posNation, seed, cliffSuppression, baseDetail);
                height = Mathf.Lerp(hNeg, hPos, t);
            }

        }
        // ================================================================
        // 호수(분지) 시스템
        // ================================================================

        /// <summary>
        /// 호수(분지) 정의.
        /// center: 호수 중심 월드 좌표 / radius: 분지 반경 / depth: 분지 파내는 깊이
        /// waterLevel: 분지 바닥(카브 전 기저 높이 - depth) 위 LAKE_WATER_OFFSET만큼의 물 표면 y
        /// </summary>
        public struct TerrainLakeDef
        {
            public Vector3 center;
            public float radius;
            public float depth;
            public float waterLevel;
        }

        private static System.Collections.Generic.IReadOnlyList<TerrainLakeDef> _lakes = null;

        /// <summary>
        /// 고정 시드 결정론적 호수 목록 (지연 초기화, 6개).
        /// 배치 규칙: 황제국 중앙(0,0,0) 반경 120m 배제, 호수 간 최소 250m,
        /// 지도 경계(±1000)에서 150m 여백, 동쪽(양수 x, 플레이어 시작 (728,-529) 인근) 1~2개,
        /// 반경 40~70m, depth 3~5m. waterLevel은 호수마다 하나의 평면 y.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<TerrainLakeDef> Lakes
        {
            get
            {
                if (_lakes == null)
                {
                    _lakes = GenerateLakes();
                }
                return _lakes;
            }
        }

        /// <summary>
        /// 결정론적 호수 생성 — 인라인 LCG(고정 시드) PRNG로 위치/반경/깊이 산출.
        /// Mathf.PerlinNoise 또는 전역 UnityEngine.Random 시드를 사용하지 않아
        /// 플랫폼/호출 순서와 무관하게 항상 같은 결과를 보장한다 (Random 언시드 금지 준수).
        /// </summary>
        private static System.Collections.Generic.IReadOnlyList<TerrainLakeDef> GenerateLakes()
        {
            // 기본 앵커 — [동쪽-시작 인근, 동쪽, 북쪽, 북쪽, 서쪽, 남쪽] 순
            // 동쪽 첫 호수는 플레이어 시작 (728,-529) 인근이되, 스폰지 평탄화(반경 15m)와
            // 카브 영향(반경≤91m)이 겹치지 않도록 충분히 떨어뜨려 배치.
            Vector3[] anchors =
            {
                new Vector3(600f, 0f, -460f),   // 동쪽 — 시작 인근 (카브/스폰지 평탄화 비중첩)
                new Vector3(400f, 0f, 100f),    // 동쪽
                new Vector3(-400f, 0f, 600f),   // 북쪽
                new Vector3(-150f, 0f, 720f),   // 북쪽
                new Vector3(-700f, 0f, -300f),  // 서쪽
                new Vector3(300f, 0f, -750f),   // 남쪽
            };

            List<TerrainLakeDef> lakes = new List<TerrainLakeDef>();
            for (int i = 0; i < LAKE_COUNT; i++)
            {
                float jx = (LakeRand(i * 4 + 0) - 0.5f) * 50f;
                float jz = (LakeRand(i * 4 + 1) - 0.5f) * 50f;
                float radius = Mathf.Lerp(LAKE_RADIUS_MIN, LAKE_RADIUS_MAX, LakeRand(i * 4 + 2));
                float depth = Mathf.Lerp(LAKE_DEPTH_MIN, LAKE_DEPTH_MAX, LakeRand(i * 4 + 3));

                TerrainLakeDef lake = new TerrainLakeDef();
                lake.center = new Vector3(anchors[i].x + jx, 0f, anchors[i].z + jz);
                lake.radius = radius;
                lake.depth = depth;

                // waterLevel = 분지 바닥(카브 전 기저 높이 - depth) + LAKE_WATER_OFFSET.
                // 주의: ComputeTerrainHeight를 호출하면 카브가 재적용되어 재귀하므로,
                // 카브 전 기저 높이는 ComputeNationHeight(카브 미적용 경로)로 직접 산출한다.
                float baseH = ComputeNationHeight(
                    lake.center.x, lake.center.z, GetNationFromCoord(lake.center.x, lake.center.z), 42);
                lake.waterLevel = baseH - depth + LAKE_WATER_OFFSET;

                lakes.Add(lake);
            }

            return lakes;
        }

        /// <summary>
        /// 위치 → 방향성 국가 판정 (황제국 제외). 호수 waterLevel 산출용 기저 높이 경로.
        /// </summary>
        private static NationType GetNationFromCoord(float x, float z)
        {
            float angle = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            return GetDirectionalNation(angle);
        }

        /// <summary>
        /// 고정 시드 인라인 LCG PRNG — [0,1] 반환 (플랫폼 독립, 결정론적).
        /// </summary>
        private static float LakeRand(int stateIndex)
        {
            long state = LAKE_LCG_SEED;
            for (int i = 0; i <= stateIndex; i++)
            {
                state = (state * 1103515245L + 12345L) & 0x7FFFFFFF;
            }
            return (float)(state % 1000) / 999f;
        }

        /// <summary>
        /// 호수 분지 카브 — lake.center에서 radius 이내를 부드럽게 파낸다.
        /// smoothstep 팔오프로 중심(최대 depth) → radius*LAKE_SHORE_FACTOR(영향 없음),
        /// radius..radius*1.3 구간은 경사 완만한 해안(쇼어라인).
        /// 호수 간 최소 250m 및 카브 영향 반경(최대 70*1.3=91m)끼리 겹치지 않는다.
        /// </summary>
        private static float ApplyLakeBasins(float x, float z, float height)
        {
            var lakes = Lakes;
            for (int i = 0; i < lakes.Count; i++)
            {
                TerrainLakeDef lake = lakes[i];
                float dx = x - lake.center.x;
                float dz = z - lake.center.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                float carveRadius = lake.radius * LAKE_SHORE_FACTOR;
                if (dist >= carveRadius)
                    continue;

                // 0(경계) → 1(중심) smoothstep 팔오프 — 중심에서 최대 depth만큼 파냄
                float t = 1f - Mathf.Clamp01(dist / carveRadius);
                float s = t * t * (3f - 2f * t);

                height -= lake.depth * s;
            }
            return height;
        }

        /// <summary>
        /// 스폰지 평탄화 (T-R2) — PlayerSpawnConfig.SpawnPosition 반경 30m 절대 평탄.
        /// 반경 내는 상수 고도(스폰 중심의 방위 높이)로 고정해 Test c(반경 30m 편차<0.3m) 통과.
        /// 30m 이후 SPAWN_FLATTEN_FADE(20m) 구간에서 원래 지형으로 부드럽게 복귀.
        /// 스폰이 호수 분지 밖이므로(≈139m 이격) 상수 고도가 호수를 덮지 않는다.
        /// </summary>
        private static float ApplySpawnFlattening(float x, float z, float height, int seed, float cliffSuppression)
        {
            float dx = x - PlayerSpawnConfig.SpawnPosition.x;
            float dz = z - PlayerSpawnConfig.SpawnPosition.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            float radius = SPAWN_FLATTEN_RADIUS;               // 30m
            float outer = radius + SPAWN_FLATTEN_FADE;         // 50m
            if (dist >= outer)
                return height;

            float target = GetSpawnFlatHeight(seed);           // 스폰 중심 상수 고도 (캐시)

            if (dist <= radius)
                return target;                                 // 완전 평탄

            // 30..50m — 원래 지형으로 smoothstep 복귀
            float t = (dist - radius) / (outer - radius);
            float blend = t * t * (3f - 2f * t);
            return Mathf.Lerp(target, height, blend);
        }

        // 캐시된 스폰 평탄 상수 고도 (시드별 1회 계산 — 결정론, 성능)
        private static float _spawnFlatTarget;
        private static int _spawnFlatSeed = int.MinValue;

        private static float GetSpawnFlatHeight(int seed)
        {
            if (_spawnFlatSeed != seed)
            {
                var s = PlayerSpawnConfig.SpawnPosition;
                // 스폰은 보호 앵커라 절벽 억제됨 — 상수 고도는 절벽 없는 base+계곡만 사용.
                _spawnFlatTarget = ComputeNationHeight(s.x, s.z, NationType.East, seed, 0f);
                _spawnFlatSeed = seed;
            }
            return _spawnFlatTarget;
        }

        // ================================================================
        // 절벽 보호 (스폰/성/호수/방위경계) — 절벽 마스크 0 강제
        // 리스크 완화(계획 §6): "스폰/성/호수 반경 40m는 절벽 금지" + 경계 연속성(|Δh|<0.5)
        // ================================================================

        /// <summary>
        /// 위치 (x,z)의 절벽 억제 마스크 [0,1].
        ///   ·  스폰 / 각 호수 / 각 성(영지) / 황제국 성 반경 40m        → 0 (절벽 금지)
        ///   ·  40..55m 페이드                                            → 0..1
        ///   ·  방위 경계선(45/135/225/315°) 반경 35m                    → 0 (경계 단차 차단)
        ///   ·  35..55m 페이드                                            → 0..1
        ///   ·  그 외                                                      → 1 (절벽 허용)
        /// ComputeNationHeight 내부의 ridge 마스크 m에 곱해져 절벽 낙차를 없앤다.
        /// </summary>
        private static float ComputeCliffSuppression(float x, float z)
        {
            // 1) 보호 앵커(스폰/성/호수/황제국 성) 반경 내 절벽 금지
            float anchorFactor = 1f;
            var anchors = ProtectionAnchors;
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector3 a = anchors[i];
                float dx = x - a.x;
                float dz = z - a.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < PROTECT_CLIFF_RADIUS)
                    return 0f;                                     // 완전 금지
                if (d < PROTECT_CLIFF_RADIUS + PROTECT_CLIFF_FADE)
                {
                    float t = (d - PROTECT_CLIFF_RADIUS) / PROTECT_CLIFF_FADE;
                    anchorFactor = Mathf.Min(anchorFactor, t * t * (3f - 2f * t));
                }
            }

            // 2) 방위 경계선 반경 내 절벽 금지 (경계 크로스페이드 연속성 보증)
            //    대각 경계 광선 4개에 대한 수직 거리 = 0.707·|z±x| (대칭으로 2개로 충분).
            float d1 = Mathf.Abs(z - x) * 0.70710678f;   // 45°/225° 경계 (z=x, z=-x의 ±)
            float d2 = Mathf.Abs(x + z) * 0.70710678f;   // 135°/315° 경계
            float dBoundary = Mathf.Min(d1, d2);
            float boundaryFactor = 1f;
            if (dBoundary < BOUNDARY_CLIFF_BAN)
                return 0f;                                     // 경계 선상 완전 금지
            if (dBoundary < BOUNDARY_CLIFF_BAN + BOUNDARY_CLIFF_FADE)
            {
                float t = (dBoundary - BOUNDARY_CLIFF_BAN) / BOUNDARY_CLIFF_FADE;
                boundaryFactor = t * t * (3f - 2f * t);
            }

            return Mathf.Min(anchorFactor, boundaryFactor);
        }

        /// <summary>
        /// Phase T-R3: 절벽 억제 마스크 공개 접근자. 지형 "형태"(ComputeNationHeight)와
        /// 색 "스플랫"(TerrainSplatBaker)이 같은 suppression을 써서 스폰/성/호수/경계 근처
        /// 절벽이 형태와 함께 색상(L3 바위)도 억제되도록 단일 소스로 노출한다.
        /// 기존 ComputeCliffSuppression 동작은 그대로 (추가 전용, 회귀 없음).
        /// </summary>
        public static float SampleCliffSuppression(float x, float z) => ComputeCliffSuppression(x, z);

        /// <summary>
        /// 위치 (x,z)의 Base/Valley 완만화 디테일 계수 [0,1] (T-R2).
        /// 경계 크로스페이드 연속성(|Δh|&lt;0.5, Test b)과 보호 앵커(스폰/성/호수/황제국)
        /// 주변의 완만한 구릉을 보증한다.
        ///   ·  절벽 금지 앵커 반경 40m                      → 0 (최저옥타브·저진폭 구릉만)
        ///   ·  40..55m 페이드                               → 0..1
        ///   ·  방위 경계선(45/135/225/315°) 반경 35m       → 0 (경계부 평탄한 구릉 유지)
        ///   ·  35..55m 페이드                               → 0..1
        ///   ·  그 외                                          → 1 (원본 방위 디테일)
        /// ComputeCliffSuppression과 동일한 위치 마스크를 사용해 절벽 억제와 Base 완만화가
        /// 같은 구역에서 함께 작동한다 (형태·색 정합성과 경계 연속성 동시 보증).
        /// </summary>
        private static float ComputeBaseDetail(float x, float z)
        {
            // 1) 보호 앵커(스폰/성/호수/황제국 성) 반경 내 Base 완만화
            float anchorFactor = 1f;
            var anchors = ProtectionAnchors;
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector3 a = anchors[i];
                float dx = x - a.x;
                float dz = z - a.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < PROTECT_CLIFF_RADIUS)
                    return 0f;
                if (d < PROTECT_CLIFF_RADIUS + PROTECT_CLIFF_FADE)
                {
                    float t = (d - PROTECT_CLIFF_RADIUS) / PROTECT_CLIFF_FADE;
                    anchorFactor = Mathf.Min(anchorFactor, t * t * (3f - 2f * t));
                }
            }

            // 2) 방위 경계선 반경 내 Base 완만화 (경계 크로스페이드 연속성)
            float d1 = Mathf.Abs(z - x) * 0.70710678f;   // 45°/225° 경계
            float d2 = Mathf.Abs(x + z) * 0.70710678f;   // 135°/315° 경계
            float dBoundary = Mathf.Min(d1, d2);
            float boundaryFactor = 1f;
            if (dBoundary < BOUNDARY_CLIFF_BAN)
                return 0f;
            if (dBoundary < BOUNDARY_CLIFF_BAN + BOUNDARY_CLIFF_FADE)
            {
                float t = (dBoundary - BOUNDARY_CLIFF_BAN) / BOUNDARY_CLIFF_FADE;
                boundaryFactor = t * t * (3f - 2f * t);
            }

            return Mathf.Min(anchorFactor, boundaryFactor);
        }

        // 절벽 금지 앵커(스폰/성/황제국/호수) — 결정론 정적 캐시 (지연 1회 구축).
        private static List<Vector3> _protectionAnchors;
        private static bool _protectionBuilt = false;

        private static List<Vector3> ProtectionAnchors
        {
            get
            {
                if (_protectionBuilt)
                    return _protectionAnchors;

                _protectionBuilt = true;
                _protectionAnchors = new List<Vector3>();

                try
                {
                    // 스폰지
                    _protectionAnchors.Add(PlayerSpawnConfig.SpawnPosition);

                    // 황제국 중앙 성
                    _protectionAnchors.Add(Vector3.zero);

                    // 성/영지 (TerritoryDatabase worldPosition) — 결정론, null 안전
                    if (ProjectName.Core.Data.TerritoryDatabase.Instance != null)
                    {
                        foreach (var def in ProjectName.Core.Data.TerritoryDatabase.Instance.GetAllDefinitions())
                        {
                            Vector3 p = def.worldPosition;
                            if (p.sqrMagnitude > 0.001f)
                                _protectionAnchors.Add(p);
                        }
                    }

                    // 호수 6개 (LCG 시드 유지 — 위치 불변 원칙)
                    foreach (var lake in Lakes)
                        _protectionAnchors.Add(lake.center);
                }
                catch (System.Exception e)
                {
                    // EditMode/테스트 등 씬 미구성 시에도 지형 생성은 안전해야 함
                    Debug.LogWarning("[TerrainGenerator] 절벽 보호 앵커 구축 실패 (성 등 생략): " + e.Message);
                }

                return _protectionAnchors;
            }
        }

        /// <summary>
        /// Perlin Noise로 지형 메시 + 물 메시 생성
        /// </summary>
    /// <param name="biome">생성할 Biome 타입</param>
    /// <param name="seed">랜덤 시드 (결정론적 생성용)</param>
    /// <param name="resolution">그리드 해상도 (N×N vertices)</param>
    /// <param name="size">지형 크기 (월드 유닛)</param>
    /// <returns>(terrainMesh, waterMesh) — waterMesh는 waterThreshold<=0이면 null</returns>
    public static (Mesh terrainMesh, Mesh waterMesh) GenerateTerrain(
        BiomeType biome, int seed, int resolution = 50, float size = 100f)
    {
        BiomeDefinition def = BiomeData.GetDefinition(biome);
        return GenerateTerrainWithDefinition(def, seed, resolution, size);
    }

    /// <summary>
    /// BiomeDefinition을 직접 전달받아 지형 생성
    /// </summary>
    public static (Mesh terrainMesh, Mesh waterMesh) GenerateTerrainWithDefinition(
        BiomeDefinition def, int seed, int resolution = 50, float size = 100f)
    {
        if (resolution < 2)
        {
            Debug.LogError("[TerrainGenerator] Resolution must be >= 2");
            resolution = 2;
        }

        if (size <= 0f)
        {
            Debug.LogError("[TerrainGenerator] Size must be > 0");
            size = 100f;
        }

        int vertexCount = resolution * resolution;
        int quadCount = (resolution - 1) * (resolution - 1);
        int triangleCount = quadCount * 2;

        // Offset to center the terrain
        float halfSize = size * 0.5f;
        float step = size / (resolution - 1);

        // Vertex arrays
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        int[] triangles = new int[triangleCount * 3];

        float waterThreshold = def.waterThreshold;

        // === 1. FBM + 고원 높이맵 생성 ===
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;

                // UV (0~1)
                float u = (float)x / (resolution - 1);
                float v = (float)z / (resolution - 1);
                uv[index] = new Vector2(u, v);

                // 월드 좌표
                float wx = -halfSize + x * step;
                float wz = -halfSize + z * step;

                // 방위별 FBM + 고원 높이 (월드 좌표 사용 — 해상도 변화에 일관된 결과)
                // waterThreshold 클램프는 하지 않음 (물 메시가 threshold 이하 삼각형 판별 담당)
                float height = ComputeTerrainHeight(wx, wz, def.type, seed);

                vertices[index] = new Vector3(wx, height, wz);
            }
        }

        // === 2. Triangle 인덱스 생성 ===
        int triIndex = 0;
        for (int z = 0; z < resolution - 1; z++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int topLeft = z * resolution + x;
                int topRight = topLeft + 1;
                int bottomLeft = (z + 1) * resolution + x;
                int bottomRight = bottomLeft + 1;

                // Triangle 1: topLeft - bottomLeft - topRight (와인딩 위쪽 +Y 향함 — 뒤집히면 지형이 위에서 안 보임)
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = topRight;

                // Triangle 2: topRight - bottomLeft - bottomRight
                triangles[triIndex++] = topRight;
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = bottomRight;
            }
        }

        // === 3. 노멀 계산 (flat shading) ===
        Vector3[] calculatedNormals = new Vector3[vertexCount];

        for (int i = 0; i < triangleCount; i++)
        {
            int triStart = i * 3;
            int i1 = triangles[triStart];
            int i2 = triangles[triStart + 1];
            int i3 = triangles[triStart + 2];

            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            Vector3 v3 = vertices[i3];

            Vector3 edge1 = v2 - v1;
            Vector3 edge2 = v3 - v1;
            Vector3 normal = Vector3.Cross(edge1, edge2);

            // Degenerate triangle 방어
            if (normal.sqrMagnitude > 0f)
                normal.Normalize();

            calculatedNormals[i1] += normal;
            calculatedNormals[i2] += normal;
            calculatedNormals[i3] += normal;
        }

        // 노멀 정규화 (제로벡터 방어)
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 n = calculatedNormals[i];
            normals[i] = n.sqrMagnitude > 0f ? n.normalized : Vector3.up;
        }

        // === 4. 지형 메시 생성 ===
        Mesh terrainMesh = new Mesh();
        terrainMesh.indexFormat = vertexCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        terrainMesh.vertices = vertices;
        terrainMesh.triangles = triangles;
        terrainMesh.uv = uv;
        terrainMesh.normals = normals;
        terrainMesh.RecalculateBounds();
        terrainMesh.name = $"Terrain_{def.displayName}_{resolution}x{resolution}";

        // === 5. 물 메시 생성 (waterThreshold > 0) ===
        Mesh waterMesh = null;
        if (waterThreshold > 0f)
        {
            waterMesh = GenerateWaterMesh(vertices, triangles, resolution, step, halfSize, size, waterThreshold, def.waterColor);
        }

        return (terrainMesh, waterMesh);
    }

    /// <summary>
    /// 물 메시 생성 — waterThreshold 이하 영역만 물로 처리
    /// </summary>
    private static Mesh GenerateWaterMesh(
        Vector3[] terrainVertices, int[] terrainTriangles,
        int resolution, float step, float halfSize, float size,
        float waterThreshold, Color waterColor)
    {
        // 물 높이: threshold의 절반 정도로 설정하여 지형보다 약간 낮게
        float waterLevel = waterThreshold * 0.5f;

        // waterThreshold 이하인 vertex 판별, 물 메시용 vertex/triangle 수집
        List<Vector3> waterVerts = new List<Vector3>();
        List<int> waterTris = new List<int>();
        Dictionary<int, int> vertexMap = new Dictionary<int, int>(); // terrain vert index → water vert index

        int triCount = terrainTriangles.Length / 3;

        for (int t = 0; t < triCount; t++)
        {
            int i1 = terrainTriangles[t * 3];
            int i2 = terrainTriangles[t * 3 + 1];
            int i3 = terrainTriangles[t * 3 + 2];

            // 세 vertex 모두 waterThreshold 이하인 triangle만 물로
            if (terrainVertices[i1].y <= waterThreshold &&
                terrainVertices[i2].y <= waterThreshold &&
                terrainVertices[i3].y <= waterThreshold)
            {
                int wi1 = GetOrAddWaterVertex(waterVerts, vertexMap, i1, terrainVertices, waterLevel);
                int wi2 = GetOrAddWaterVertex(waterVerts, vertexMap, i2, terrainVertices, waterLevel);
                int wi3 = GetOrAddWaterVertex(waterVerts, vertexMap, i3, terrainVertices, waterLevel);

                waterTris.Add(wi1);
                waterTris.Add(wi2);
                waterTris.Add(wi3);
            }
        }

        if (waterVerts.Count < 3)
            return null;

        Mesh waterMesh = new Mesh();
        waterMesh.indexFormat = waterVerts.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        waterMesh.vertices = waterVerts.ToArray();
        waterMesh.triangles = waterTris.ToArray();

        // 물 UV — 평면 투영
        Vector2[] waterUV = new Vector2[waterVerts.Count];
        for (int i = 0; i < waterVerts.Count; i++)
        {
            Vector3 wv = waterVerts[i];
            waterUV[i] = new Vector2(wv.x / size + 0.5f, wv.z / size + 0.5f);
        }
        waterMesh.uv = waterUV;

        // 물 노멀은 항상 위쪽
        Vector3[] waterNormals = new Vector3[waterVerts.Count];
        for (int i = 0; i < waterVerts.Count; i++)
            waterNormals[i] = Vector3.up;

        waterMesh.normals = waterNormals;
        waterMesh.name = $"Water_{resolution}x{resolution}";

        return waterMesh;
    }

    private static int GetOrAddWaterVertex(
        List<Vector3> waterVerts, Dictionary<int, int> vertexMap,
        int terrainIndex, Vector3[] terrainVertices, float waterLevel)
    {
        if (vertexMap.TryGetValue(terrainIndex, out int existing))
            return existing;

        Vector3 src = terrainVertices[terrainIndex];
        Vector3 waterVertex = new Vector3(src.x, waterLevel, src.z);
        int newIndex = waterVerts.Count;
        waterVerts.Add(waterVertex);
        vertexMap[terrainIndex] = newIndex;
        return newIndex;
    }

    /// <summary>
    /// 기존 GameObject의 MeshFilter/MeshRenderer를 업데이트
    /// </summary>
    /// <param name="groundObject">적용할 GameObject (MeshFilter 보유)</param>
    /// <param name="biome">Biome 타입</param>
    /// <param name="seed">랜덤 시드</param>
    /// <param name="pathCenter">진입로 중심 월드 좌표 (null이면 진입로 미생성)</param>
    /// <param name="pathWidth">진입로 폭 (월드 유닛, 기본 5m)</param>
    /// <param name="pathLength">진입로 길이 (월드 유닛, 기본 40m)</param>
    public static void ApplyTerrainToGameObject(
        GameObject groundObject, BiomeType biome, int seed,
        Vector3? pathCenter = null, float pathWidth = 5f, float pathLength = 40f)
    {
        BiomeDefinition def = BiomeData.GetDefinition(biome);

        // Mesh 생성
        var (terrainMesh, waterMesh) = GenerateTerrainWithDefinition(def, seed);

        // MeshFilter에 지형 메시 할당
        MeshFilter mf = groundObject.GetComponent<MeshFilter>();
        if (mf == null)
        {
            mf = groundObject.AddComponent<MeshFilter>();
        }
        else if (mf.sharedMesh != null)
        {
            // 이전 메시 해제 (메모리 누수 방지)
            Object.Destroy(mf.sharedMesh);
        }
        mf.sharedMesh = terrainMesh;

        // MeshRenderer에 Biome 색상 Material 적용
        MeshRenderer mr = groundObject.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            mr = groundObject.AddComponent<MeshRenderer>();
        }

        // 기본 URP/Lit Material 생성 및 색상 설정
        Material mat = mr.sharedMaterial;
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                mat = new Material(Shader.Find("Standard"));
            }
            mr.sharedMaterial = mat;
        }

        mat.color = def.surfaceColor;
        mat.name = $"Mat_{def.displayName}";

        // === 진입로 (Path) Vertex 색상 적용 ===
        if (pathCenter.HasValue)
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh != null)
            {
                Vector3[] vertices = mesh.vertices;
                int[] pathIndices = TerrainPathGenerator.GetPathVertexIndices(
                    vertices, pathCenter.Value, pathWidth, pathLength);

                if (pathIndices.Length > 0)
                {
                    Color[] vertexColors = TerrainPathGenerator.ApplyPathVertexColors(
                        vertices.Length, pathIndices, biome);
                    mesh.colors = vertexColors;
                }
            }
        }

        // === 물 메시가 있으면 자식 GameObject로 추가 ===
        if (waterMesh != null)
        {
            Transform waterTransform = groundObject.transform.Find("Water");
            GameObject waterObj;
            if (waterTransform == null)
            {
                waterObj = new GameObject("Water");
                waterObj.transform.SetParent(groundObject.transform);
                waterObj.transform.localPosition = Vector3.zero;
            }
            else
            {
                waterObj = waterTransform.gameObject;
            }

            MeshFilter waterMf = waterObj.GetComponent<MeshFilter>();
            if (waterMf == null)
                waterMf = waterObj.AddComponent<MeshFilter>();
            else if (waterMf.sharedMesh != null)
                Object.Destroy(waterMf.sharedMesh);
            waterMf.sharedMesh = waterMesh;

            MeshRenderer waterMr = waterObj.GetComponent<MeshRenderer>();
            if (waterMr == null)
                waterMr = waterObj.AddComponent<MeshRenderer>();

            if (waterMr.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    Material waterMat = new Material(shader);
                    waterMat.color = def.waterColor;

                    // 반투명 설정
                    waterMat.SetFloat("_Surface", 1.0f);  // Transparent
                    waterMat.SetFloat("_Blend", 0.0f);    // Alpha
                    waterMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    waterMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    waterMat.SetInt("_ZWrite", 0);
                    waterMat.renderQueue = 3000;
                    waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                    waterMr.sharedMaterial = waterMat;
                }
                else
                {
                    waterMr.sharedMaterial = new Material(Shader.Find("Standard"))
                    {
                        color = def.waterColor
                    };
                }
            }
        }
        else
        {
            // 기존 Water 자식 제거 (biome이 물 없는 타입으로 바뀐 경우)
            Transform existingWater = groundObject.transform.Find("Water");
            if (existingWater != null)
            {
                Object.Destroy(existingWater.gameObject);
            }
        }
    }
}
}