using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase T-R4: Idyllic deco per-nation random variation (NationDecoProfile).
    /// Deterministic (fixed nation seed = 20260904 + nationId*1000), weighted random
    /// prefab pick, jitter grid + spatial-hash min-dist (no overcrowding), perf caps.
    /// Flower patch mask (TerrainShape.GetFlowerPatchMask) -> dense flowers.
    /// Fantasy subzone (TerrainShape.GetFantasySubzoneMask) -> Meadows + pink/purple trees.
    /// Empire: symmetric garden grid within 120m (cherry50/garden30/bush20).
    /// Lake +8m shore band (reeds/lilies) uses TerrainGenerator.Lakes (LCG anchors).
    /// T-D3 B2 (09-08): lake surface rocks (y=waterLevel-0.6, 1.5~3.0x) / lily pad colonies (1~3/lake) /
    /// shore willow arc (r~r+8m, 20~30% arc, Pink=Empire only) / mega flower spread 18~30m equiv (cap 60~80/nation).
    /// Colliders: tree trunk + big rocks only. SpawnAreaDeco (PlaceSpawnProps) replaces TerrainPropPlacer.
    /// Culling radii (tree150/rock200) logged only (no existing culling group).
    /// Uses System.Random only (deterministic); y = GROUND_BASE + GetHeightAt.
    /// </summary>
    public static class IdyllicDecoPlacer
    {
        const string ROOT_NAME = "IdyllicDeco";
        const string SPAWN_ROOT_NAME = "IdyllicSpawnProps";
        const int SEED = 20260902;
        const int T_R4_BASE = 20260904;
        const float GROUND_BASE = 1f;
        // 청크 지형 ±1600m 커버에 맞춘 데코 배치 범위 확장 (950 → 1550)
        const float BOUND_MAX = 1550f;
        const float EMPIRE_GARDEN_RADIUS = 120f;
        const float EMPIRE_CORE_EXCLUDE = 8f;
        const float EMPIRE_GARDEN_CAP = 90;
        const float SPAWN_EXCLUDE = 6f;
        const float SPAWN_PROP_RADIUS = 80f;
        const int SPAWN_PROPS_TREES = 10;

        const float TREE_SPACING = 30f;      // 1/900 sqm
        const float TREE_JITTER = 7f;
        const float TREE_MIN_DIST = 9f;
        const float ROCK_SPACING = 50f;      // 1/2500 sqm
        const float ROCK_JITTER = 12f;
        const float ROCK_MIN_DIST = 14f;
        const float ROCK_CLUSTER_CHANCE = 0.10f;
        const int ROCK_CLUSTER_SIZE = 3;
        const float BUSH_SPACING = 38f;
        const float BUSH_JITTER = 10f;
        const float BUSH_MIN_DIST = 6f;
        const float FLOWER_CELL = 3.6f;      // B2: 4.5→3.6m — 꽃밭 밀도 ×1.56 (최소간격 2.8m와 정합)
        const float FLOWER_MIN_DIST = 2.8f;  // B2: 3.5→2.8m — 밀도 상향에 맞춘 완화
        const float MEADOW_SPACING = 26f;
        const float MEADOW_JITTER = 8f;
        const float MEADOW_MIN_DIST = 22f;
        const float TRUNK_CLEAR = 2.5f;
        // 길 확보: 흙길(반폭 3.5m) 중심선에서 이 반경 이내 나무/바위 배치 제외
        const float DIRT_PATH_CLEAR = 7f;

        // B4: 흙길 가장자리 소형 데코 — 7m 금지 벨트 바로 바깥(7~9m)에만 자갈/허브/들꽃 소량.
        // 나무/대형 바위 금지 규칙은 그대로 유지(벨트는 7m 바깥이므로 길 통행 방해 없음).
        const float PATH_EDGE_INNER = 7f;
        const float PATH_EDGE_OUTER = 9f;
        const float PATH_EDGE_STEP = 7f;      // 세그먼트 걷기 간격 (m)
        const float PATH_EDGE_KEEP = 2.0f;    // 가장자리 데코 최소간격 (m)
        const int   PATH_EDGE_CAP = 150;      // 국가당 상한 (소량)

        // T-D2 (09-08): 노출 암반 위성 바위 + 서쪽 천연 아치 + 대형 꽃 융단 데코 (예시2~13 Gap 충전)
        const int   OUTCROP_BIG_MIN = 2, OUTCROP_BIG_MAX = 4;   // 사이트당 rockBig 개수 범위
        const int   OUTCROP_MED_MIN = 4, OUTCROP_MED_MAX = 8;   // 사이트당 rockMed 개수 범위
        const float OUTCROP_SCALE_BIG_MIN = 1.1f, OUTCROP_SCALE_BIG_MAX = 1.8f;
        const float OUTCROP_SCALE_MED_MIN = 0.7f, OUTCROP_SCALE_MED_MAX = 1.2f;
        const float ARCH_SCALE = 2.4f;          // 아치 랜드마크 스케일 (거대 실루엣)
        const float MEGA_SCAN_CELL = 26f;       // 꽃 융단 마스크 스캔 격자 (FM_SPACING과 동일 규모)
        const int   MEGA_CLUSTER_SUB = 4;       // 히트 셀 내 4×4 밀집 클러스터
        const float MEGA_CLUSTER_STEP = 1.8f;   // 클러스터 내 간격 (m)
        // T-D3 B2 (09-08): 꽃 융단 — 클러스터 산포 확대(체감 지름 18~30m 등가) + 국가당 개수 220→60~80
        //   (패치 대형화로 총면적 유지·증가, 과밀 방지). 마스크 자체 스케일업은 TerrainShape 스트림 담당.
        const float MEGA_SPREAD_MIN = 9f, MEGA_SPREAD_MAX = 15f;  // 위성 소군집 산포 반경 9~15m (체감 지름 18~30m 등가)
        const int   MEGA_SAT_MIN = 2, MEGA_SAT_MAX = 3;           // 중심 4×4 클러스터 외 위성 3×3 소군집 수
        const int   MEGA_CAP_MIN = 60, MEGA_CAP_MAX = 80;         // 국가당 융단 데코 상한 (MegaCapFor 결정론 해시로 60~80 배정)
        const int   MEGA_GLOBAL_VALVE = MEGA_CAP_MAX * 5;         // 전역 안전 밸브 (5국가 합산 상한)

        const float FLOWER_MASK_HI = 0.55f;       // B2: 0.60→0.55 — 꽃밭 면적 확대
        const float FLOWER_MASK_FOCUS = 0.52f;    // B2: 동/남 방위 집중 게이트 (면적 추가 확대)
        const float FANTASY_MASK_HI = 0.50f;

        // AA5: 잔디 커버 + FlowerMeadow 꽃밭 패치 셋업 (예시 8 — 지형에 잔디가 깔리고 중간중간 꽃)
        // AA6 09-05: BB1 밀도 상향 — GRASS_SPACING 10→6.5(일반 1/42.25㎡), 대량 배치를 위해 cap 4500→8000.
        // CC1 09-05: 동적 잔디 커버(IdyllicGrassCover)가 플레이어 반경 45m를 밀집 커버하므로
        //   정적 잔디는 멀리 떨어진 개활지 포인트만 유지한다 — cap 8000→2000 (먼 곳 개활지 포인트).
        const float GRASS_SPACING = 6.5f;    // 일반 지면 1/42.25㎡ (GRASS_MIN_DIST 1.5m 물리 제약 하 최대 가용)
        const float GRASS_JITTER = 3f;
        const float GRASS_MIN_DIST = 1.5f;    // 잔디는 완화된 최소간격
        const float GRASS_TILT_DEG = 8f;      // 기울기 ±8°
        const int   GRASS_NATION_CAP = 2000;  // CC1: 8000→2000 (먼 곳 개활지 포인트, 동적 커버와 분담)
        const float GRASS_MASK_HI = 0.50f;    // 꽃밭/숲 마스크 고밀도 임계
        const float GRASS_DENSE_SUB = 4f;     // 마스크 내부 4×4 서브그리드 (GRASS_SPACING 6.5m 셀 어디서든 최대 포장 — 4/㎡ 근사)

        const float FM_SPACING = 26f;         // FlowerMeadow 패치 마스크 스캔 격자
        const float FM_JITTER = 6f;
        const float FM_MIN_DIST = 20f;        // 패치간 최소간격
        const float FM_MASK_HI = 0.58f;       // B2: 0.62→0.58 — 패치 후보 면적 확대
        const float FM_SCALE_MIN = 7f, FM_SCALE_MAX = 13f; // B2: 6~12→7~13m — 패치 크기 상향
        const int   FM_CAP = 160;             // B2: 110→160 — 패치 수 ×1.45

        const float LAKE_TREE_MARGIN = 1.2f;
        const float LAKE_SHORE_IN = 0.97f;
        const float LAKE_SHORE_OUT = 1.20f;
        const float LAKE_LILY_IN = 0.30f;
        const float LAKE_LILY_OUT = 0.80f;
        const float LAKE_TREE_IN = 1.24f;
        const float LAKE_TREE_OUT = 1.75f;
        const int REEDS_PER_LAKE = 128;     // CC2: 46→85 → T-D3 B2: 85→128 (1.5배 상한, Cattail_01~03 혼입 유지)
        const int LILIES_PER_LAKE = 8;
        const int LAKE_TREES_PER_LAKE = 10; // CC2: 7→10 (호수 인근 나무 밀도 ×1.5)

        // T-D3 B2 (09-08): 수면 바위(T2-1) / 연잎·연꽃 군집(T2-2) / 수변 수양버들 밴드(T2-4)
        const float LAKE_ROCK_IN = 0.30f, LAKE_ROCK_OUT = 0.70f;      // 수면 바위 반경 위치 (호수 중심 0.3~0.7r)
        const float LAKE_ROCK_SINK = 0.6f;                            // y = waterLevel - 0.6 — 수면 기준 배치(규약 예외, 함수 주석 참조)
        const float LAKE_ROCK_SCALE_MIN = 1.5f, LAKE_ROCK_SCALE_MAX = 3.0f; // rockBig 스케일 변형
        const int   LILY_PER_CLUSTER_MIN = 5, LILY_PER_CLUSTER_MAX = 10;    // 군집당 연잎 개수
        const int   WATERLILY_PER_CLUSTER_MIN = 1, WATERLILY_PER_CLUSTER_MAX = 2; // 군집당 연꽃 개수
        const float LILY_SCATTER_MIN = 3f, LILY_SCATTER_MAX = 7f;     // 군집 산점 반경 (m)
        const float LILY_CENTER_IN = 0.60f, LILY_CENTER_OUT = 0.95f;  // 군집 중심: 가장자리~0.6r (랜덤각, 만 의존 없음)
        const float WILLOW_BAND_WIDTH = 8f;                           // 수변 버들 밴드 폭: r ~ r+8m
        const float WILLOW_ARC_MIN = 0.20f, WILLOW_ARC_MAX = 0.30f;   // 둘레의 20~30% 호 구간 (랜덤 시작각)
        const int   WILLOW_MIN = 4, WILLOW_MAX = 8;                   // 호수당 수양버들 그루 수
        const float WILLOW_PINK_RATIO = 0.20f;                        // Pink 비율 20% (황제국 호수만, Green 기본)

        // T-D5 (09-08): 호수 주변 꾸미기 데코 3종 — 수변 관목/덤불 / 수변 바위·자갈 언덕 / 수변 습지 습초지 클러스터
        // 기존 LAKE_ROCK_IN/OUT(수면 바위 0.30~0.70r)와 이름 충돌을 피하기 위해 LAKE_SHORE_ROCK_* 사용.
        const float LAKE_SHRUB_IN = 1.02f, LAKE_SHRUB_OUT = 1.55f;    // 수변 관목 밴드 (호수 중심 배수)
        const float LAKE_SHRUBS_PER_LAKE = 6;                          // 기본 목표 (대형 8~12 / 소형 4~6)
        const float LAKE_SHORE_ROCK_IN = 1.02f, LAKE_SHORE_ROCK_OUT = 1.70f; // 수변 바위 밴드
        const float LAKE_ROCKS_PER_LAKE = 7;                           // 기본 목표 (5~10)
        const float LAKE_WET_IN = 0.95f, LAKE_WET_OUT = 1.12f;         // 습지 습초지 밴드 (수면 가장자리~얕은 습지)
        const float LAKE_WET_CLUSTERS_PER_LAKE = 5;                    // 호수당 습초지 클러스터 수

        static readonly float SPAWN_POS_X = ProjectName.Core.PlayerSpawnConfig.SpawnPosition.x;
        static readonly float SPAWN_POS_Z = ProjectName.Core.PlayerSpawnConfig.SpawnPosition.z;

        internal class WPrefab
        {
            public GameObject prefab;
            public float weight = 1f;
            public float scaleMin = 0.85f;
            public float scaleMax = 1.15f;
            public bool collider;
        }

        internal class NationDecoProfile
        {
            public NationType nation;
            public List<WPrefab> trees = new List<WPrefab>();
            public List<WPrefab> bushes = new List<WPrefab>();
            public List<WPrefab> rocks = new List<WPrefab>();
            public List<WPrefab> flowers = new List<WPrefab>();
            public List<WPrefab> fantasyTrees = new List<WPrefab>();
            public List<WPrefab> meadows = new List<WPrefab>();
            public float treeSpacing = TREE_SPACING;
            public int treeCap, rockCap, bushCap, flowerCap, meadowCap;
        }

        internal class CategoriesR4
        {
            public List<GameObject> willow, broadGreen, broadPurple, broadRed, fir, blossom;
            public List<GameObject> willowGreen, willowPink;   // T-D3 B2: 수변 수양버들 색 분리 (Pink=황제국 전용)
            public List<GameObject> bushes;
            public List<GameObject> rockBig, rockMed, rockSmall;
            public List<GameObject> cattail, reeds, lilyPads, waterLily;
            public List<GameObject> flowerYellow, flowerWhite, flowerRed, flowerPurple, flowerPink, flowerBlue;
            public List<GameObject> meadowWhite, meadowRed, meadowRedOrange, meadowPurple, meadowPink, meadowBlue;
            public List<GameObject> grass;   // AA5: 잔디 풋 (Grass_01/02/03)
        }

        static int NationSeed(NationType n) { return T_R4_BASE + (int)n * 1000; }

        /// <summary>T-D3 B2: 국가당 꽃 융단 상한 60~80 — NationSeed 기반 결정론 해시 (UnityEngine.Random 미사용).</summary>
        static int MegaCapFor(NationType nat)
        {
            uint h = (uint)(NationSeed(nat) * 31 + 17);
            h ^= (h >> 16) * 0x45d9f3bu;
            h ^= (h >> 13) * 0x45d9f3bu;
            return MEGA_CAP_MIN + (int)(h % (uint)(MEGA_CAP_MAX - MEGA_CAP_MIN + 1));
        }

        public static void PlaceAll(Transform center, Transform parent)
        {
            if (parent == null) return;
            if (FindDirectChild(parent, ROOT_NAME) != null)
            {
                Debug.Log("[IdyllicDecoPlacer] Already placed - skipping.");
                return;
            }

            var cat = BuildCategoriesR4();
            if (cat.willow.Count + cat.broadGreen.Count + cat.fir.Count == 0)
            {
                Debug.LogError("[IdyllicDecoPlacer] No IdyllicPrefabs found - placement skipped.");
                return;
            }

            var root = new GameObject(ROOT_NAME);
            root.transform.SetParent(parent, false);
            root.layer = 0;
            var forestT = NewChild(root, "Forest");
            var rocksT = NewChild(root, "Rocks");
            var bushesT = NewChild(root, "Bushes");
            var flowersT = NewChild(root, "Flowers");
            var meadowsT = NewChild(root, "Meadows");
            var shoreT = NewChild(root, "Lakeshore");
            var waterT = NewChild(root, "WaterPlants");
            var waterRockT = NewChild(root, "WaterRocks");   // T-D3 B2: 수면 바위 전용 계층
            var grassT = NewChild(root, "Grass");       // AA5: 잔디 풋 커버
            var fmPatchT = NewChild(root, "FlowerMeadow"); // AA5: 꽃밭 패치

            Vector3 origin = center != null ? center.position : Vector3.zero;
            var treeHash = new SpatialHash(TREE_MIN_DIST);
            var propHash = new SpatialHash(BUSH_MIN_DIST);

            var east = BuildProfile(NationType.East, cat);
            var west = BuildProfile(NationType.West, cat);
            var south = BuildProfile(NationType.South, cat);
            var north = BuildProfile(NationType.North, cat);
            var profiles = new NationDecoProfile[] { east, west, south, north };

            var lakeRng = new System.Random(SEED);
            int reedsPlaced = 0, lilyPlaced = 0, lakeTreePlaced = 0;
            int surfaceRockPlaced = 0, lilyClusterPlaced = 0, willowPlaced = 0;   // T-D3 B2
            int shrubPlaced = 0, shoreRockPlaced = 0, wetClusterPlaced = 0;       // T-D5: 호수 주변 꾸미기 3종
            var lakes = TerrainGenerator.Lakes;
            if (lakes != null)
            {
                for (int i = 0; i < lakes.Count; i++)
                {
                    var lk = lakes[i];
                    reedsPlaced += PlaceLakeshoreReeds(lk, cat, shoreT, treeHash, lakeRng);
                    lilyPlaced += PlaceLakeshoreLilies(lk, cat, waterT, lakeRng);
                    lakeTreePlaced += PlaceLakeshoreTrees(lk, cat, forestT, treeHash, propHash, lakeRng);
                    // T-D3 B2: 수면/수변 추가 데코 — 기존 lakeRng 스트림 소비 순서를 보존하기 위해
                    // 호수 인덱스 고정 시드의 별도 rng를 사용 (기존 배치 결과 불변, 결정론 유지)
                    surfaceRockPlaced += PlaceLakeSurfaceRocks(lk, cat, waterRockT, new System.Random(SEED + 41 + i * 7));
                    lilyClusterPlaced += PlaceLilyClusters(lk, cat, waterT, new System.Random(SEED + 53 + i * 7));
                    willowPlaced += PlaceLakeshoreWillows(lk, cat, forestT, treeHash, propHash, new System.Random(SEED + 67 + i * 7));
                    // T-D5: 호수 주변 꾸미기 3종 — 기존 스트림 보존 규약 동일 (호수 인덱스 고정 시드 별도 rng)
                    shrubPlaced += PlaceLakeshoreShrubs(lk, cat, bushesT, treeHash, propHash, new System.Random(SEED + 79 + i * 7));
                    shoreRockPlaced += PlaceLakeshoreRocks(lk, cat, rocksT, treeHash, propHash, new System.Random(SEED + 83 + i * 7));
                    wetClusterPlaced += PlaceLakeshoreWetlandClusters(lk, cat, shoreT, treeHash, new System.Random(SEED + 89 + i * 7));
                }
            }

            int[] treeCnt = new int[8], rockCnt = new int[8], bushCnt = new int[8];
            int[] flowerCnt = new int[8], meadowCnt = new int[8];
            var grassHash = new SpatialHash(GRASS_MIN_DIST);  // AA5
            var fmHash = new SpatialHash(FM_MIN_DIST);        // AA5
            for (int i = 0; i < profiles.Length; i++)
            {
                PlaceNation(profiles[i], cat, origin,
                    forestT, rocksT, bushesT, flowersT, meadowsT,
                    treeHash, propHash, treeCnt, rockCnt, bushCnt, flowerCnt, meadowCnt);
            }

            // AA5: 잔디 커버 + FlowerMeadow 꽃밭 패치 (국가별, 결정론 시드 +7)
            int grassCnt = 0, fmPatchCnt = 0;
            for (int i = 0; i < profiles.Length; i++)
            {
                grassCnt += PlaceGrassCover(profiles[i], cat, origin, grassT, grassHash,
                    treeHash, propHash, new System.Random(NationSeed(profiles[i].nation) + 7));
                fmPatchCnt += PlaceFlowerMeadowPatches(profiles[i], cat, origin, fmPatchT, fmHash,
                    new System.Random(NationSeed(profiles[i].nation) + 11));
            }

            // B4: 흙길 가장자리 소형 데코 (자갈/허브/들꽃) — 국가별 결정론 시드 +13,
            //     최소간격 PATH_EDGE_KEEP(2m) SpatialHash, 국가당 상한 PATH_EDGE_CAP(150).
            int pathEdgeCnt = 0;
            for (int i = 0; i < profiles.Length; i++)
            {
                pathEdgeCnt += PlacePathEdgeDeco(profiles[i], cat, origin, flowersT, grassT, rocksT,
                    new SpatialHash(PATH_EDGE_KEEP),
                    new System.Random(NationSeed(profiles[i].nation) + 13));
            }

            var empireRng = new System.Random(NationSeed(NationType.Empire));
            int empirePlaced = PlaceEmpireGarden(origin, cat, forestT, bushesT, treeHash, propHash, empireRng);

            // T-D2 (09-08): 노출 암반 위성 바위 군집 + 서쪽 천연 아치 + 대형 꽃 융단 데코
            int outcropRockCnt = PlaceOutcropRocks(cat, rocksT, treeHash, propHash);
            int archCnt = PlaceWestArch(cat, rocksT);
            int megaFlowerCnt = PlaceMegaFlowerPatches(cat, flowersT, treeHash, propHash);

            EnableGPUInstancing(root);

            // AA5: 잔디/꽃밭 컬링 — 플레이어 반경 60m 외 SetActive(false) (Update 0.5초 주기)
            var culler = grassT.gameObject.AddComponent<IdyllicGrassCuller>();
            culler.Configure(grassT, fmPatchT);

            DensityLog("East", treeCnt, rockCnt, bushCnt, flowerCnt, meadowCnt, east.treeCap, east.rockCap);
            DensityLog("West", treeCnt, rockCnt, bushCnt, flowerCnt, meadowCnt, west.treeCap, west.rockCap);
            DensityLog("South", treeCnt, rockCnt, bushCnt, flowerCnt, meadowCnt, south.treeCap, south.rockCap);
            DensityLog("North", treeCnt, rockCnt, bushCnt, flowerCnt, meadowCnt, north.treeCap, north.rockCap);

            long layoutHash = HashTreeLayout(forestT) ^ HashTreeLayout(rocksT);
            Debug.Log(string.Format(
                "[IdyllicDecoPlacer][T-R4] Total Trees={0}||Rocks={1}||Bushes={2}||Flowers={3}||Meadows={4}||EmpireGarden={5}||" +
                "ShoreReeds={6}||WaterLilies={7}||LakeTrees={8}||LayoutHash={9:X8}",
                Sum(treeCnt), Sum(rockCnt), Sum(bushCnt), Sum(flowerCnt), Sum(meadowCnt),
                empirePlaced, reedsPlaced, lilyPlaced, lakeTreePlaced, layoutHash));
            Debug.Log(string.Format(
                "[IdyllicDecoPlacer][AA5] GrassTusks={0}||FlowerMeadowPatches={1}||GrassCap={2}/nation||" +
                "EstDrawCalls={3} (GPU instancing on: 1 mesh per 1 draw-call batch)",
                grassCnt, fmPatchCnt, GRASS_NATION_CAP, grassCnt + fmPatchCnt));
            // B4: 흙길 가장자리 데코 배치 합계 (AA5 로그와 동일한 || 구분 형식)
            Debug.Log(string.Format(
                "[IdyllicDecoPlacer][B4] PathEdgeDeco={0}||Cap={1}/nation||Keep={2}m||Band={3}~{4}m||Mix=rock40/grass30/flower30",
                pathEdgeCnt, PATH_EDGE_CAP, PATH_EDGE_KEEP, PATH_EDGE_INNER, PATH_EDGE_OUTER));
            // T-D3 B2: 수면/수변 데코 배치 합계 (호수 전체 — TerrainGenerator.Lakes 캐시 기준)
            Debug.Log(string.Format(
                "[IdyllicDecoPlacer][T-D3] LakeSurfaceRocks={0}||LilyColonies={1}||ShoreWillows={2}||ReedsCapPerLake={3}(x1.5)||" +
                "MegaSpreadDiam=18~30m||MegaCapNation={4}~{5}",
                surfaceRockPlaced, lilyClusterPlaced, willowPlaced, REEDS_PER_LAKE, MEGA_CAP_MIN, MEGA_CAP_MAX));
            // T-D5: 호수 주변 꾸미기 데코 3종 배치 합계 (호수 전체)
            Debug.Log(string.Format(
                "[IdyllicDecoPlacer][T-D5] LakeshoreShrubs={0}||LakeshoreRocks={1}||WetlandClusterPts={2}||" +
                "Bands=shrub{3}~{4}r/rock{5}~{6}r/wet{7}~{8}r",
                shrubPlaced, shoreRockPlaced, wetClusterPlaced,
                LAKE_SHRUB_IN, LAKE_SHRUB_OUT, LAKE_SHORE_ROCK_IN, LAKE_SHORE_ROCK_OUT, LAKE_WET_IN, LAKE_WET_OUT));
            Debug.Log("[IdyllicDecoPlacer][T-R4] Deterministic seed = 20260904+nationId*1000. LayoutHash for 2-boot compare (same seed->same hash).");
            Debug.Log("[IdyllicDecoPlacer][T-R4] Culling radii (no existing group - log only): tree 150m / rock 200m / grass-flower-bush 60m.");
            Debug.Log("[IdyllicDecoPlacer][AA5] Culling = simple distance check(Update 0.5s) player radius 60m -> grass/FlowerMeadow SetActive(false) outside.");
        }

        static int PlaceLakeshoreReeds(TerrainGenerator.TerrainLakeDef lake,
            CategoriesR4 cat, Transform parent, SpatialHash treeHash, System.Random rng)
        {
            int placed = 0;
            var reedHash = new SpatialHash(3f);
            int attempts = REEDS_PER_LAKE * 6;
            for (int i = 0; i < attempts && placed < REEDS_PER_LAKE; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = lake.radius * Mathf.Lerp(LAKE_SHORE_IN, LAKE_SHORE_OUT, (float)rng.NextDouble());
                float x = lake.center.x + Mathf.Cos(ang) * d;
                float z = lake.center.z + Mathf.Sin(ang) * d;
                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                if (y < lake.waterLevel + 0.10f) continue;
                if (y > lake.waterLevel + 2.4f) continue;
                var p = new Vector2(x, z);
                if (!reedHash.IsFree(p, 2.2f)) continue;
                if (!treeHash.IsFree(p, 1.2f)) continue;
                var model = (rng.Next(2) == 0 && cat.cattail.Count > 0)
                    ? cat.cattail[rng.Next(cat.cattail.Count)]
                    : cat.reeds[rng.Next(cat.reeds.Count)];
                Place(model, x, y, z, RandomRange(rng, 0.8f, 1.35f), rng, parent);
                reedHash.Insert(p);
                placed++;
            }
            return placed;
        }

        static int PlaceLakeshoreLilies(TerrainGenerator.TerrainLakeDef lake,
            CategoriesR4 cat, Transform parent, System.Random rng)
        {
            int placed = 0;
            var lilyHash = new SpatialHash(3f);
            int attempts = LILIES_PER_LAKE * 8;
            for (int i = 0; i < attempts && placed < LILIES_PER_LAKE; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = lake.radius * Mathf.Lerp(LAKE_LILY_IN, LAKE_LILY_OUT, (float)rng.NextDouble());
                float x = lake.center.x + Mathf.Cos(ang) * d;
                float z = lake.center.z + Mathf.Sin(ang) * d;
                float terrainY = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                if (terrainY > lake.waterLevel - 0.25f) continue;
                var p = new Vector2(x, z);
                if (!lilyHash.IsFree(p, 3.5f)) continue;
                var model = (rng.Next(3) == 0 && cat.waterLily.Count > 0)
                    ? cat.waterLily[rng.Next(cat.waterLily.Count)]
                    : cat.lilyPads[rng.Next(cat.lilyPads.Count)];
                Place(model, x, lake.waterLevel + 0.03f, z, RandomRange(rng, 0.85f, 1.2f), rng, parent);
                lilyHash.Insert(p);
                placed++;
            }
            return placed;
        }

        static int PlaceLakeshoreTrees(TerrainGenerator.TerrainLakeDef lake,
            CategoriesR4 cat, Transform parent, SpatialHash treeHash, SpatialHash propHash, System.Random rng)
        {
            int placed = 0;
            int attempts = LAKE_TREES_PER_LAKE * 12;
            for (int i = 0; i < attempts && placed < LAKE_TREES_PER_LAKE; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = lake.radius * Mathf.Lerp(LAKE_TREE_IN, LAKE_TREE_OUT, (float)rng.NextDouble());
                float x = lake.center.x + Mathf.Cos(ang) * d;
                float z = lake.center.z + Mathf.Sin(ang) * d;
                if (IsInSpawnExclusion(x, z)) continue;
                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                if (y < lake.waterLevel + 0.8f) continue;
                var p = new Vector2(x, z);
                if (!treeHash.IsFree(p, TREE_MIN_DIST)) continue;
                if (!propHash.IsFree(p, ROCK_MIN_DIST)) continue;
                var model = (rng.NextDouble() < 0.6 && cat.willow.Count > 0)
                    ? cat.willow[rng.Next(cat.willow.Count)]
                    : cat.broadGreen[rng.Next(cat.broadGreen.Count)];
                Place(model, x, y, z, RandomRange(rng, 0.85f, 1.15f), rng, parent);
                treeHash.Insert(p);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// T-D3 B2 (T2-1): 호수 수면 바위 — rockBig 스케일 변형(1.5~3.0×)을 호수 중심 0.3~0.7r 수면에 배치.
        /// [규약 예외] 기존 y = GROUND_BASE + GetHeightAt 대신 수면 기준 배치: y = waterLevel - 0.6m.
        /// 바위 하단은 침수(바닥 잠김), 프리팹 높이에 따라 수면 위 0.3~0.6m 노출되는 의도.
        /// 개수: 대형 호수(r≥90m, T-D2 승격 100~120m 포함) 10~12, 중형(40~70m) 6~8. 18개 호수 전체 적용.
        /// 수면 바위는 수심 게이트 불필요(항상 물 안) — 나무/암반 데코와는 밴드가 겹치지 않는다.
        /// </summary>
        static int PlaceLakeSurfaceRocks(TerrainGenerator.TerrainLakeDef lake,
            CategoriesR4 cat, Transform parent, System.Random rng)
        {
            var pool = cat.rockBig.Count > 0 ? cat.rockBig : cat.rockMed;
            if (pool.Count == 0) return 0;
            int target = lake.radius >= 90f ? 10 + rng.Next(3) : 6 + rng.Next(3);
            int placed = 0;
            int attempts = target * 8;
            for (int a = 0; a < attempts && placed < target; a++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = lake.radius * RandomRange(rng, LAKE_ROCK_IN, LAKE_ROCK_OUT);
                float x = lake.center.x + Mathf.Cos(ang) * d;
                float z = lake.center.z + Mathf.Sin(ang) * d;
                // 수면 기준 배치 (위 규약 예외 주석 참조) — GROUND_BASE/GetHeightAt 미사용
                Place(pool[rng.Next(pool.Count)], x, lake.waterLevel - LAKE_ROCK_SINK, z,
                    RandomRange(rng, LAKE_ROCK_SCALE_MIN, LAKE_ROCK_SCALE_MAX), rng, parent);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// T-D3 B2 (T2-2): 연잎/연꽃 군집 — 호수당 1~3개 (수면 데코 총면적 ≤20% 제한을 면적 비례 캡으로 충족:
        /// 대형 r≥90m → 3, 중형 r≥55m → 2, 소형 → 1. 군집 3개 × (연잎 10 × ~2.5㎡ + 산점) ≈ ≤200㎡로
        /// 소형 호수(r=40m, 5,024㎡)의 20%(1,004㎡) 이하). 군집 중심 = 가장자리~0.6r 랜덤각
        /// (만(bay) 좌표 의존성 없는 순수 랜덤 산포). 군집당 LilyPads_01~03 랜덤 5~10개(산점 3~7m,
        /// y = waterLevel + 0.02) + Waterlily 1~2개(군집 중심 근처 0.5~3m).
        /// </summary>
        static int PlaceLilyClusters(TerrainGenerator.TerrainLakeDef lake,
            CategoriesR4 cat, Transform parent, System.Random rng)
        {
            if (cat.lilyPads.Count == 0) return 0;
            int clusters = lake.radius >= 90f ? 3 : (lake.radius >= 55f ? 2 : 1);
            int placed = 0;
            var padHash = new SpatialHash(2f);
            for (int c = 0; c < clusters; c++)
            {
                float cang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float cd = lake.radius * RandomRange(rng, LILY_CENTER_IN, LILY_CENTER_OUT);
                float cx = lake.center.x + Mathf.Cos(cang) * cd;
                float cz = lake.center.z + Mathf.Sin(cang) * cd;
                float terrainY = GROUND_BASE + TerrainGenerator.GetHeightAt(cx, cz, BiomeType.Plains, 42);
                if (terrainY > lake.waterLevel - 0.25f) continue;   // 기존 lilies 수심 게이트와 동일
                if (IsInSpawnExclusion(cx, cz)) continue;
                int padCnt = LILY_PER_CLUSTER_MIN + rng.Next(LILY_PER_CLUSTER_MAX - LILY_PER_CLUSTER_MIN + 1);
                for (int p = 0; p < padCnt; p++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float d = RandomRange(rng, LILY_SCATTER_MIN, LILY_SCATTER_MAX);
                    float x = cx + Mathf.Cos(ang) * d;
                    float z = cz + Mathf.Sin(ang) * d;
                    var pv = new Vector2(x, z);
                    if (!padHash.IsFree(pv, 1.8f)) continue;
                    Place(cat.lilyPads[rng.Next(cat.lilyPads.Count)], x, lake.waterLevel + 0.02f, z,
                        RandomRange(rng, 0.85f, 1.2f), rng, parent);
                    padHash.Insert(pv);
                    placed++;
                }
                if (cat.waterLily.Count == 0) continue;
                int wlCnt = WATERLILY_PER_CLUSTER_MIN + rng.Next(WATERLILY_PER_CLUSTER_MAX - WATERLILY_PER_CLUSTER_MIN + 1);
                for (int w = 0; w < wlCnt; w++)
                {
                    float wang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float wd = RandomRange(rng, 0.5f, LILY_SCATTER_MIN);
                    Place(cat.waterLily[rng.Next(cat.waterLily.Count)], cx + Mathf.Cos(wang) * wd, lake.waterLevel + 0.02f,
                        cz + Mathf.Sin(wang) * wd, RandomRange(rng, 0.85f, 1.2f), rng, parent);
                    placed++;
                }
            }
            return placed;
        }

        /// <summary>
        /// T-D3 B2 (T2-4): 수변 수양버들 — 호수 둘레 밴드(r ~ r+8m)의 20~30% 호 구간(랜덤 시작각)에 4~8그루.
        /// WillowTree Green 위주, 20%는 Pink — 국가 색 규칙상 황제국(Empire) 호수만 Pink 허용(Green 기본).
        /// 참고: Resources/IdyllicPrefabs/Trees는 현재 WillowTree_01~05_Green만 포함 — Pink 프리팹이
        /// Resources 세트에 추가되면 willowPink 필터에 자동 반영되고, 없으면 Green으로 폴백한다.
        /// 지면 게이트 y > waterLevel + 0.3m (수변 습지 대응 — 기존 lake tree +0.8m보다 완화).
        /// </summary>
        static int PlaceLakeshoreWillows(TerrainGenerator.TerrainLakeDef lake,
            CategoriesR4 cat, Transform parent, SpatialHash treeHash, SpatialHash propHash, System.Random rng)
        {
            if (cat.willowGreen.Count == 0 && cat.willow.Count == 0) return 0;
            int target = WILLOW_MIN + rng.Next(WILLOW_MAX - WILLOW_MIN + 1);
            float startAng = (float)rng.NextDouble() * Mathf.PI * 2f;
            float arcSpan = RandomRange(rng, WILLOW_ARC_MIN, WILLOW_ARC_MAX) * Mathf.PI * 2f;
            int placed = 0;
            int attempts = target * 12;
            for (int a = 0; a < attempts && placed < target; a++)
            {
                float ang = startAng + (float)rng.NextDouble() * arcSpan;
                float d = lake.radius + RandomRange(rng, 0f, WILLOW_BAND_WIDTH);
                float x = lake.center.x + Mathf.Cos(ang) * d;
                float z = lake.center.z + Mathf.Sin(ang) * d;
                if (Mathf.Abs(x) > BOUND_MAX || Mathf.Abs(z) > BOUND_MAX) continue;
                if (IsInSpawnExclusion(x, z)) continue;
                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                if (y < lake.waterLevel + 0.3f) continue;
                var p = new Vector2(x, z);
                if (!treeHash.IsFree(p, TREE_MIN_DIST)) continue;
                if (!propHash.IsFree(p, ROCK_MIN_DIST)) continue;
                var nat = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                var pool = (nat == NationType.Empire && cat.willowPink.Count > 0 && rng.NextDouble() < WILLOW_PINK_RATIO)
                    ? cat.willowPink : cat.willowGreen;
                if (pool.Count == 0) pool = cat.willow;   // Green 필터 실패 시 기존 willow 풀 폴백
                Place(pool[rng.Next(pool.Count)], x, y, z, RandomRange(rng, 0.9f, 1.2f), rng, parent);
                treeHash.Insert(p);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// T-D5 (09-08): 호수 수변 관목/덤불 — 밴드 1.02~1.55r에 bushes 프리팹 배치.
        /// 대형 호수(r≥100m) 8~12개, 소형 4~6개. 수면 게이트 y > waterLevel + 0.5m,
        /// treeHash(TREE_MIN_DIST) + propHash(ROCK_MIN_DIST) 충돌 회피, 스케일 0.9~1.4.
        /// </summary>
        static int PlaceLakeshoreShrubs(TerrainGenerator.TerrainLakeDef lake,
            CategoriesR4 cat, Transform parent, SpatialHash treeHash, SpatialHash propHash, System.Random rng)
        {
            if (cat.bushes == null || cat.bushes.Count == 0) return 0;
            int target = lake.radius >= 100f ? 8 + rng.Next(5) : 4 + rng.Next(3);
            int placed = 0;
            var shrubHash = new SpatialHash(3f);
            int attempts = target * 12;
            for (int a = 0; a < attempts && placed < target; a++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = lake.radius * Mathf.Lerp(LAKE_SHRUB_IN, LAKE_SHRUB_OUT, (float)rng.NextDouble());
                float x = lake.center.x + Mathf.Cos(ang) * d;
                float z = lake.center.z + Mathf.Sin(ang) * d;
                if (Mathf.Abs(x) > BOUND_MAX || Mathf.Abs(z) > BOUND_MAX) continue;
                if (IsInSpawnExclusion(x, z)) continue;
                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                if (y < lake.waterLevel + 0.5f) continue;
                var p = new Vector2(x, z);
                if (!shrubHash.IsFree(p, 3f)) continue;
                if (!treeHash.IsFree(p, TREE_MIN_DIST)) continue;
                if (!propHash.IsFree(p, ROCK_MIN_DIST)) continue;
                Place(cat.bushes[rng.Next(cat.bushes.Count)], x, y, z, RandomRange(rng, 0.9f, 1.4f), rng, parent);
                shrubHash.Insert(p);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// T-D5 (09-08): 호수 수변 바위·자갈 언덕 — 밴드 1.02~1.70r에 rock 계열 배치.
        /// 대형 호수(r≥100m)는 rockBig 혼입, 소형은 rockMed/rockSmall 위주. 5~10개.
        /// 수면 게이트 y > waterLevel + 0.4m, propHash 충돌 회피, 스케일 0.7~1.6.
        /// </summary>
        static int PlaceLakeshoreRocks(TerrainGenerator.TerrainLakeDef lake,
            CategoriesR4 cat, Transform parent, SpatialHash treeHash, SpatialHash propHash, System.Random rng)
        {
            var med = cat.rockMed.Count > 0 ? cat.rockMed : cat.rockSmall;
            if (med.Count == 0) return 0;
            var small = cat.rockSmall.Count > 0 ? cat.rockSmall : med;
            var big = cat.rockBig.Count > 0 ? cat.rockBig : med;
            int target = 5 + rng.Next(6);   // 5~10
            int placed = 0;
            var rockHash = new SpatialHash(3f);
            int attempts = target * 12;
            for (int a = 0; a < attempts && placed < target; a++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = lake.radius * Mathf.Lerp(LAKE_SHORE_ROCK_IN, LAKE_SHORE_ROCK_OUT, (float)rng.NextDouble());
                float x = lake.center.x + Mathf.Cos(ang) * d;
                float z = lake.center.z + Mathf.Sin(ang) * d;
                if (Mathf.Abs(x) > BOUND_MAX || Mathf.Abs(z) > BOUND_MAX) continue;
                if (IsInSpawnExclusion(x, z)) continue;
                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                if (y < lake.waterLevel + 0.4f) continue;
                var p = new Vector2(x, z);
                if (!rockHash.IsFree(p, 3f)) continue;
                if (!treeHash.IsFree(p, 1.2f)) continue;
                if (!propHash.IsFree(p, ROCK_MIN_DIST)) continue;
                GameObject model;
                if (lake.radius >= 100f && rng.Next(3) == 0) model = big[rng.Next(big.Count)];
                else model = (rng.Next(2) == 0) ? med[rng.Next(med.Count)] : small[rng.Next(small.Count)];
                Place(model, x, y, z, RandomRange(rng, 0.7f, 1.6f), rng, parent);
                rockHash.Insert(p);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// T-D5 (09-08): 호수 수변 습지 습초지 클러스터 — 밴드 0.95~1.12r(수면 가장자리~얕은 습지)에
        /// reeds/cattail(1.3~2.0m 스케일) + bushes(0.8~1.1m) 혼합 뭉치를 호수당 5개 클러스터,
        /// 클러스터당 3~6 포인트 산점(반경 2~5m). 수면 게이트 -0.1 < y < waterLevel + 1.2,
        /// treeHash 회피. SpatialHash 간격 3m로 과밀 방지.
        /// </summary>
        static int PlaceLakeshoreWetlandClusters(TerrainGenerator.TerrainLakeDef lake,
            CategoriesR4 cat, Transform parent, SpatialHash treeHash, System.Random rng)
        {
            if ((cat.reeds == null || cat.reeds.Count == 0) && (cat.cattail == null || cat.cattail.Count == 0))
                return 0;
            int clusters = (int)LAKE_WET_CLUSTERS_PER_LAKE;
            int placed = 0;
            var wetHash = new SpatialHash(3f);
            for (int c = 0; c < clusters; c++)
            {
                float cang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float cd = lake.radius * Mathf.Lerp(LAKE_WET_IN, LAKE_WET_OUT, (float)rng.NextDouble());
                float cx = lake.center.x + Mathf.Cos(cang) * cd;
                float cz = lake.center.z + Mathf.Sin(cang) * cd;
                int pts = 3 + rng.Next(4);   // 클러스터당 3~6 포인트
                for (int p = 0; p < pts; p++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float dd = RandomRange(rng, 0f, 5f);
                    float x = cx + Mathf.Cos(ang) * dd;
                    float z = cz + Mathf.Sin(ang) * dd;
                    if (Mathf.Abs(x) > BOUND_MAX || Mathf.Abs(z) > BOUND_MAX) continue;
                    if (IsInSpawnExclusion(x, z)) continue;
                    float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                    if (y < lake.waterLevel - 0.1f) continue;
                    if (y > lake.waterLevel + 1.2f) continue;
                    var pv = new Vector2(x, z);
                    if (!wetHash.IsFree(pv, 3f)) continue;
                    if (!treeHash.IsFree(pv, 1.2f)) continue;
                    if (rng.Next(3) != 0 && cat.bushes != null && cat.bushes.Count > 0)
                    {
                        // 습초지 수풀: reeds/cattail 1.3~2.0m 스케일
                        var rmodel = (rng.Next(2) == 0 && cat.cattail.Count > 0)
                            ? cat.cattail[rng.Next(cat.cattail.Count)]
                            : cat.reeds[rng.Next(cat.reeds.Count)];
                        Place(rmodel, x, y, z, RandomRange(rng, 1.3f, 2.0f), rng, parent);
                    }
                    else
                    {
                        // 습지 덤불: bushes 0.8~1.1m 스케일
                        Place(cat.bushes[rng.Next(cat.bushes.Count)], x, y, z, RandomRange(rng, 0.8f, 1.1f), rng, parent);
                    }
                    wetHash.Insert(pv);
                    placed++;
                }
            }
            return placed;
        }

        static void PlaceNation(NationDecoProfile p, CategoriesR4 cat, Vector3 origin,
            Transform forestT, Transform rocksT, Transform bushesT, Transform flowersT, Transform meadowsT,
            SpatialHash treeHash, SpatialHash propHash,
            int[] treeCnt, int[] rockCnt, int[] bushCnt, int[] flowerCnt, int[] meadowCnt)
        {
            var rng = new System.Random(NationSeed(p.nation));
            PlaceNationTrees(p, cat, origin, forestT, treeHash, treeCnt, rng);
            PlaceNationRocks(p, cat, origin, rocksT, treeHash, propHash, rockCnt, rng);
            PlaceNationBushes(p, cat, origin, bushesT, treeHash, propHash, bushCnt, rng);
            PlaceNationFlowers(p, cat, origin, flowersT, treeHash, propHash, flowerCnt, rng);
            PlaceFantasyMeadows(p, cat, origin, meadowsT, treeHash, propHash, meadowCnt, rng);
        }

        // ================================================================
        // AA5: 잔디 커버 (GPU Instancing 다량 배치) + FlowerMeadow 꽃밭 패치
        // ================================================================

        /// <summary>
        /// AA5: 국가별 지면 잔디 풋 커버 — 결정론 그리드(시드 +7) + 꽃밭/숲 마스크 내부 4×4 고밀도.
        /// 스폰/성/호수 수변/경사 30° 제외, 최소간격 1.5m(완화), 스케일 0.7~1.3 × 위치해시, 기울기 ±8°.
        /// </summary>
        static int PlaceGrassCover(NationDecoProfile p, CategoriesR4 cat, Vector3 origin,
            Transform parent, SpatialHash grassHash, SpatialHash treeHash, SpatialHash propHash, System.Random rng)
        {
            if (cat.grass == null || cat.grass.Count == 0)
            {
                Debug.LogWarning("[IdyllicDecoPlacer][AA5] No Grass prefabs in IdyllicPrefabs/Grass - grass cover skipped.");
                return 0;
            }
            int placed = 0;
            float lim = BOUND_MAX - GRASS_JITTER;
            for (float gx = -lim; gx <= lim && placed < GRASS_NATION_CAP; gx += GRASS_SPACING)
            {
                for (float gz = -lim; gz <= lim && placed < GRASS_NATION_CAP; gz += GRASS_SPACING)
                {
                    if (!InBounds(gx, gz, origin)) continue;
                    if (IsInSpawnExclusion(gx, gz)) continue;
                    var nation = NationTerrainController.GetNationFromPosition(new Vector3(gx, 0f, gz));
                    if (nation != p.nation) continue;
                    float fx = gx, fz = gz;
                    bool dense = TerrainShape.GetFlowerPatchMask(fx, fz) > GRASS_MASK_HI
                        || TerrainShape.GetForestPatchMask(fx, fz, p.nation, T_R4_BASE) > GRASS_MASK_HI;
                    int subs = dense ? (int)GRASS_DENSE_SUB : 1;
                    for (int si = 0; si < subs * subs && placed < GRASS_NATION_CAP; si++)
                    {
                        int sx = si % subs, sz = si / subs;
                        float off = GRASS_SPACING / (subs + 1f);
                        float x = gx + (sx + 1 - (subs + 1f) * 0.5f) * off + RandomRange(rng, -GRASS_JITTER * 0.4f, GRASS_JITTER * 0.4f);
                        float z = gz + (sz + 1 - (subs + 1f) * 0.5f) * off + RandomRange(rng, -GRASS_JITTER * 0.4f, GRASS_JITTER * 0.4f);
                        if (TryPlaceGrass(cat, origin, parent, grassHash, treeHash, propHash, rng, x, z))
                            placed++;
                    }
                }
            }
            return placed;
        }

        /// <summary>AA5: 잔디 단일 배치 (국가/호수/경사/최소간격 검사 후). 참조용 트리/프롭 최소간격은 완화 적용.</summary>
        static bool TryPlaceGrass(CategoriesR4 cat, Vector3 origin,
            Transform parent, SpatialHash grassHash, SpatialHash treeHash, SpatialHash propHash, System.Random rng, float x, float z)
        {
            if (!InBounds(x, z, origin)) return false;
            var p2 = new Vector2(x, z);
            if (!grassHash.IsFree(p2, GRASS_MIN_DIST)) return false;
            if (!treeHash.IsFree(p2, TRUNK_CLEAR * 0.5f)) return false;
            if (!propHash.IsFree(p2, GRASS_MIN_DIST)) return false;
            if (IsNearLakeWater(x, z, 1.02f)) return false;
            // Z3 계승: 경사 30° 초과 지점 잔디 스킵 (절벽 위 잔디 금지)
            if (TerrainSplatBaker.EstimateSlopeDegrees(x, z) > 30f) return false;
            float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42) + 0.04f;
            GameObject model = cat.grass[rng.Next(cat.grass.Count)];
            float scale = RandomRange(rng, 0.7f, 1.3f) * ScaleVariation(x, z, 0xAA51, 0.9f, 1.1f);
            PlaceGrass(model, x, y, z, scale, rng, parent);
            grassHash.Insert(p2);
            return true;
        }

        /// <summary>AA5: 잔디 배치는 yaw 랜덤 + 기울기 ±8°를 준다.</summary>
        static GameObject PlaceGrass(GameObject model, float x, float y, float z, float scale, System.Random rng, Transform parent)
        {
            var go = Object.Instantiate(model, parent);
            go.layer = 0;
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(
                RandomRange(rng, -GRASS_TILT_DEG, GRASS_TILT_DEG),
                (float)rng.NextDouble() * 360f,
                RandomRange(rng, -GRASS_TILT_DEG, GRASS_TILT_DEG));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>
        /// AA5: FlowerMeadow 꽃밭 패치 — 꽃 마스크(GetFlowerPatchMask) 격자 스캔 후 고밀도 클러스터의 중심에
        /// 국가 선호 색 패치 1개 배치(스케일 6~12m). 확인된 Resources/IdyllicPrefabs/Meadows 8색.
        /// 국가 선호: 동=혼합, 북=보라, 남=빨강, 서=노랑(Orange).
        /// </summary>
        static int PlaceFlowerMeadowPatches(NationDecoProfile p, CategoriesR4 cat, Vector3 origin,
            Transform parent, SpatialHash fmHash, System.Random rng)
        {
            if (p.meadows == null || p.meadows.Count == 0) return 0;
            var meadows = LoadSet("IdyllicPrefabs/Meadows");
            // 국가 선호 프리팹 선정 (부재 시 p.meadows 기본)
            // p.meadows는 List<WPrefab>, Filter는 List<GameObject> 반환 → WPrefab으로 변환해 사용 (PickWeighted 필요).
            var prefPool = p.meadows;
            switch (p.nation)
            {
                case NationType.North:
                    var northPool = FilterPrefabs(meadows, "FlowerMeadow", "Purple");
                    prefPool = northPool.Count > 0 ? northPool : p.meadows;
                    break;
                case NationType.South:
                    var southPool = FilterPrefabs(meadows, "FlowerMeadow", "Red");
                    prefPool = southPool.Count > 0 ? southPool : p.meadows;
                    break;
                case NationType.West:
                    var westPool = FilterPrefabs(meadows, "FlowerMeadow", "Orange");
                    prefPool = westPool.Count > 0 ? westPool : p.meadows;
                    break;
                case NationType.East:
                default:
                    var mixed = FilterPrefabs(meadows, "FlowerMeadow", "OrangePinkRedPurpleBlue");
                    prefPool = mixed.Count > 0 ? mixed : p.meadows;
                    break;
            }
            int placed = 0;
            float lim = BOUND_MAX - FM_JITTER;
            for (float gx = -lim; gx <= lim && placed < FM_CAP; gx += FM_SPACING)
            {
                for (float gz = -lim; gz <= lim && placed < FM_CAP; gz += FM_SPACING)
                {
                    if (!InBounds(gx, gz, origin)) continue;
                    if (IsInSpawnExclusion(gx, gz)) continue;
                    var nation = NationTerrainController.GetNationFromPosition(new Vector3(gx, 0f, gz));
                    if (nation != p.nation) continue;
                    // 패치 중심 = 마스크 피크 격자 셀 (주변 4방향보다 마스크 높은 곳)
                    float m = TerrainShape.GetFlowerPatchMask(gx, gz);
                    if (m < FM_MASK_HI) continue;
                    if (m < TerrainShape.GetFlowerPatchMask(gx + FM_SPACING, gz)
                        || m < TerrainShape.GetFlowerPatchMask(gx - FM_SPACING, gz)
                        || m < TerrainShape.GetFlowerPatchMask(gx, gz + FM_SPACING)
                        || m < TerrainShape.GetFlowerPatchMask(gx, gz - FM_SPACING)) continue;
                    if (IsNearLakeWater(gx, gz, 1.05f)) continue;
                    if (TerrainSplatBaker.EstimateSlopeDegrees(gx, gz) > 30f) continue;
                    var p2 = new Vector2(gx, gz);
                    if (!fmHash.IsFree(p2, FM_MIN_DIST)) continue;
                    float x = gx + RandomRange(rng, -FM_JITTER, FM_JITTER);
                    float z = gz + RandomRange(rng, -FM_JITTER, FM_JITTER);
                    float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42) + 0.05f;
                    WPrefab entry = PickWeighted(prefPool, rng);
                    float scale = RandomRange(rng, FM_SCALE_MIN, FM_SCALE_MAX) * ScaleVariation(x, z, 0xAA52, 0.85f, 1.1f);
                    Place(entry.prefab, x, y, z, scale, rng, parent);
                    fmHash.Insert(p2);
                    placed++;
                }
            }
            return placed;
        }

        static void PlaceNationTrees(NationDecoProfile p, CategoriesR4 cat, Vector3 origin,
            Transform parent, SpatialHash treeHash, int[] treeCnt, System.Random rng)
        {
            if (p.trees == null || p.trees.Count == 0) return;
            int placed = 0;
            float lim = BOUND_MAX - TREE_JITTER;
            for (float gx = -lim; gx <= lim && placed < p.treeCap; gx += p.treeSpacing)
            {
                for (float gz = -lim; gz <= lim; gz += p.treeSpacing)
                {
                    // 숲 군락/개활지 분리(예시 컨셉): 트리 후보 위치마다 Fbm 숲 마스크 노이즈 게이트
                    // (TerrainShape.Fbm은 public — 결정론 3옥타브 FBM, 시드 7777 고정).
                    // Z6: 마스크 주파수 0.005 (숲 덩어리 특성 길이 ~200m) — B3에서도 유지(덩어리 보존).
                    // B3: 밀집 게이트 완화(0.42→0.36) + 심부 밀집(0.48+) 3×3 서브그리드 — 군락 내 밀도 ×1.5~2.25.
                    //   forestMask > 0.48     심부 밀집 — 3×3 서브그리드 (간격 ~10m)
                    //   forestMask > 0.36     밀집 숲 — 2×2 서브그리드 (간격 절반)
                    //   0.30 ~ 0.36           정상 간격
                    //   forestMask < 0.35     개활지 — 30% 확률로 스킵 (유지)
                    float forestMask = TerrainShape.Fbm(gx * 0.005f, gz * 0.005f, 3, 2f, 0.5f, 7777);
                    if (forestMask < 0.35f && rng.NextDouble() < 0.30) continue;   // 개활지 스킵
                    bool denseForest = forestMask > 0.36f;                         // B3: 0.42→0.36 완화
                    bool coreForest = forestMask > 0.48f;                          // B3: 심부 밀집 신설

                    // Z4: 숲 군락 여부 — B3: 내부 게이트 0.50→0.42 완화 + 가장자리(25m 페이드 밴드)
                    // 밀도 감쇠(단일 간격 + 25% 스킵)로 숲→초원 자연 전환.
                    float fx = gx, fz = gz;
                    float fm = TerrainShape.GetForestPatchMask(fx, fz, p.nation, T_R4_BASE);
                    bool forest = fm > 0.42f;
                    bool forestEdge = fm > 0.06f && fm <= 0.42f;
                    if (forestEdge && rng.NextDouble() < 0.25f) continue;   // B3: 가장자리 스킵
                    int subs = coreForest ? 3 : ((forest || denseForest) ? 2 : 1);
                    if (forestEdge && subs > 1) subs = 1;                   // B3: 가장자리 단일 간격
                    for (int si = 0; si < subs * subs && placed < p.treeCap; si++)
                    {
                        int sx = si % subs, sz = si / subs;
                        float ox = (subs == 2) ? ((sx == 0 ? -1f : 1f) * p.treeSpacing * 0.25f) : 0f;
                        float oz = (subs == 2) ? ((sz == 0 ? -1f : 1f) * p.treeSpacing * 0.25f) : 0f;
                        float jit = forest ? TREE_JITTER * 0.5f : TREE_JITTER;
                        float x = gx + ox + RandomRange(rng, -jit, jit);
                        float z = gz + oz + RandomRange(rng, -jit, jit);
                        if (TryPlaceTree(p, cat, origin, parent, treeHash, treeCnt, rng, x, z))
                            placed++;
                    }
                }
            }
        }

        /// <summary>최근접 호수 수면 가장자리까지 거리(T-D3 T3-1). 호수 없으면 float.MaxValue.</summary>
        static float NearestLakeShoreDist(float x, float z)
        {
            var lakes = TerrainGenerator.LakesOrNull;   // 재귀 가드 패턴 준수(생성 중 null)
            if (lakes == null) return float.MaxValue;
            float best = float.MaxValue;
            for (int i = 0; i < lakes.Count; i++)
            {
                var lk = lakes[i];
                float ddx = x - lk.center.x, ddz = z - lk.center.z;
                float d = Mathf.Sqrt(ddx * ddx + ddz * ddz) - lk.radius;
                if (d < best) best = d;
            }
            return best;
        }

        static List<WPrefab> _firPool, _shorePool;   // T-D3 T3-1 수종 규칙 풀(지연 1회 구성)

        /// <summary>능선 침엽 풀(침엽 80/활엽 20).</summary>
        static List<WPrefab> FirPool(CategoriesR4 cat)
        {
            if (_firPool == null)
            {
                _firPool = new List<WPrefab>();
                if (cat.fir != null) AddPool(_firPool, cat.fir, 80f, 0.8f, 1.1f, true);
                if (cat.broadGreen != null) AddPool(_firPool, cat.broadGreen, 20f, 0.85f, 1.1f, true);
            }
            return _firPool;
        }

        /// <summary>수변 혼합 풀(수양버들 40/활엽 60 — 예시6 비율).</summary>
        static List<WPrefab> ShorePool(CategoriesR4 cat)
        {
            if (_shorePool == null)
            {
                _shorePool = new List<WPrefab>();
                if (cat.willow != null) AddPool(_shorePool, cat.willow, 40f, 0.85f, 1.2f, true);
                if (cat.broadGreen != null) AddPool(_shorePool, cat.broadGreen, 60f, 0.9f, 1.2f, true);
            }
            return _shorePool;
        }

        /// <summary>나무 단일 배치 (국가/상한/호수/최소간격/경사 검사 후). true = 배치됨.</summary>
        static bool TryPlaceTree(NationDecoProfile p, CategoriesR4 cat, Vector3 origin,
            Transform parent, SpatialHash treeHash, int[] treeCnt, System.Random rng, float x, float z)
        {
            if (!InBounds(x, z, origin)) return false;
            float dx = x - origin.x, dz = z - origin.z;
            if (dx * dx + dz * dz < EMPIRE_GARDEN_RADIUS * EMPIRE_GARDEN_RADIUS) return false;
            if (IsInSpawnExclusion(x, z)) return false;
            var nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
            if (nation != p.nation) return false;
            if (treeCnt[(int)nation] >= p.treeCap) return false;
            // 길 확보: 흙길(중심선 반경 7m) 위 나무 배치 제외
            if (IsNearDirtPath(x, z, DIRT_PATH_CLEAR)) return false;
            if (IsNearLakeWater(x, z, LAKE_TREE_MARGIN)) return false;
            // Z3: 경사 30° 초과 지점 데코 배치 스킵 (절벽 위 나무 금지 — 자연 지면 스냅 유지)
            if (TerrainSplatBaker.EstimateSlopeDegrees(x, z) > 30f) return false;
            var p2 = new Vector2(x, z);
            if (!treeHash.IsFree(p2, TREE_MIN_DIST)) return false;
            float sub = TerrainShape.GetFantasySubzoneMask(x, z, p.nation, T_R4_BASE);
            // T-D3 T3-1 수종 규칙: 능선(리지부스트>0.4)=침엽 80/활엽 20, 호수 수변 인접(≤18m)=수양버들 40/활엽 60.
            // fantasyTrees(이국 소군집) 우선순위는 기존 유지. 풀은 nation 무관(틴트는 ApplyNationTreeTint가 담당).
            List<WPrefab> pickPool = (sub > FANTASY_MASK_HI && p.fantasyTrees != null && p.fantasyTrees.Count > 0)
                ? p.fantasyTrees : p.trees;
            if (pickPool == p.trees)
            {
                if (TerrainShape.GetRidgeBoostMask(x, z, p.nation, T_R4_BASE) > 0.4f && cat.fir != null && cat.fir.Count > 0)
                    pickPool = FirPool(cat);
                else if (NearestLakeShoreDist(x, z) <= 18f && cat.willow != null && cat.willow.Count > 0)
                    pickPool = ShorePool(cat);
            }
            WPrefab entry = PickWeighted(pickPool, rng);
            float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
            // AA4: 위치 기반 해시 스케일 변주 ×0.8~1.3 (프리팹 원본 스케일에 곱 — 수목 크기 다양화)
            float baseScale = RandomRange(rng, entry.scaleMin, entry.scaleMax);
            float scale = baseScale * ScaleVariation(x, z, 0xAA41, 0.8f, 1.3f);
            GameObject go = Place(entry.prefab, x, y, z, scale, rng, parent);
            if (entry.collider) AddTreeCollider(go);
            // CC3: 국가 방위색 틴트 (잎사귀 재료 복제 — 공유 금지, instancing은 유지)
            ApplyNationTreeTint(go, p.nation, entry.prefab);
            // CC3: 30% 나무 블롭 섀도우 (결정론 선별 — 반경 1.2m)
            if (ShouldShadow(x, z, 0xCC31, 0.30f))
                BlobShadow.AttachStatic(go, 1.2f);
            treeHash.Insert(p2);
            treeCnt[(int)nation]++;
            return true;
        }

        static void PlaceNationRocks(NationDecoProfile p, CategoriesR4 cat, Vector3 origin,
            Transform parent, SpatialHash treeHash, SpatialHash propHash, int[] rockCnt, System.Random rng)
        {
            if (p.rocks == null || p.rocks.Count == 0) return;
            int placed = 0;
            float lim = BOUND_MAX - ROCK_JITTER;
            for (float gx = -lim; gx <= lim && placed < p.rockCap; gx += ROCK_SPACING)
            {
                for (float gz = -lim; gz <= lim; gz += ROCK_SPACING)
                {
                    float x = gx + RandomRange(rng, -ROCK_JITTER, ROCK_JITTER);
                    float z = gz + RandomRange(rng, -ROCK_JITTER, ROCK_JITTER);
                    if (!InBounds(x, z, origin)) continue;
                    if (IsInSpawnExclusion(x, z)) continue;
                    var nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                    if (nation != p.nation) continue;
                    if (rockCnt[(int)nation] >= p.rockCap) continue;
                    if (IsNearLakeWater(x, z, 1.1f)) continue;
                    // 길 확보: 흙길(중심선 반경 7m) 위 바위 배치 제외
                    if (IsNearDirtPath(x, z, DIRT_PATH_CLEAR)) continue;
                    var p2 = new Vector2(x, z);
                    if (!treeHash.IsFree(p2, TRUNK_CLEAR)) continue;
                    if (!propHash.IsFree(p2, ROCK_MIN_DIST)) continue;
                    WPrefab entry = PickWeighted(p.rocks, rng);
                    float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                    // AA4: 위치 기반 해시 스케일 변주 ×0.85~1.25 (프리팹 원본 스케일에 곱 — 바위 크기 다양화)
                    float scale = RandomRange(rng, entry.scaleMin, entry.scaleMax)
                        * ScaleVariation(x, z, 0xAA42, 0.85f, 1.25f);
                    GameObject go = Place(entry.prefab, x, y, z, scale, rng, parent);
                    if (entry.collider) { AddRockCollider(go); BlobShadow.AttachStatic(go, 0.9f); } // CC3: 대형 바위 블롭 섀도우
                    propHash.Insert(p2);
                    rockCnt[(int)nation]++;
                    placed++;
                    if (rng.NextDouble() < ROCK_CLUSTER_CHANCE && cat.rockSmall.Count > 0)
                    {
                        for (int c2 = 0; c2 < ROCK_CLUSTER_SIZE && placed < p.rockCap; c2++)
                        {
                            float ca = (float)rng.NextDouble() * Mathf.PI * 2f;
                            float cd = RandomRange(rng, 2.5f, 6f);
                            float cx = x + Mathf.Cos(ca) * cd;
                            float cz = z + Mathf.Sin(ca) * cd;
                            if (!InBounds(cx, cz, origin)) continue;
                            if (IsInSpawnExclusion(cx, cz)) continue;
                            var cp = new Vector2(cx, cz);
                            if (IsNearDirtPath(cx, cz, DIRT_PATH_CLEAR)) continue; // 길 확보
                            if (!propHash.IsFree(cp, ROCK_MIN_DIST)) continue;
                            if (!treeHash.IsFree(cp, TRUNK_CLEAR)) continue;
                            float cy = GROUND_BASE + TerrainGenerator.GetHeightAt(cx, cz, BiomeType.Plains, 42);
                            Place(cat.rockSmall[rng.Next(cat.rockSmall.Count)], cx, cy, cz,
                                RandomRange(rng, 0.55f, 0.9f), rng, parent);
                            propHash.Insert(cp);
                            rockCnt[(int)nation]++;
                            placed++;
                        }
                    }
                }
            }
        }

        static void PlaceNationBushes(NationDecoProfile p, CategoriesR4 cat, Vector3 origin,
            Transform parent, SpatialHash treeHash, SpatialHash propHash, int[] bushCnt, System.Random rng)
        {
            if (p.bushes == null || p.bushes.Count == 0) return;
            int placed = 0;
            float lim = BOUND_MAX - BUSH_JITTER;
            for (float gx = -lim; gx <= lim && placed < p.bushCap; gx += BUSH_SPACING)
            {
                for (float gz = -lim; gz <= lim; gz += BUSH_SPACING)
                {
                    float x = gx + RandomRange(rng, -BUSH_JITTER, BUSH_JITTER);
                    float z = gz + RandomRange(rng, -BUSH_JITTER, BUSH_JITTER);
                    if (!InBounds(x, z, origin)) continue;
                    if (IsInSpawnExclusion(x, z)) continue;
                    var nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                    if (nation != p.nation) continue;
                    if (bushCnt[(int)nation] >= p.bushCap) continue;
                    if (IsNearLakeWater(x, z, 1.1f)) continue;
                    var p2 = new Vector2(x, z);
                    if (!treeHash.IsFree(p2, TRUNK_CLEAR)) continue;
                    if (!propHash.IsFree(p2, BUSH_MIN_DIST)) continue;
                    WPrefab entry = PickWeighted(p.bushes, rng);
                    float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                    Place(entry.prefab, x, y, z, RandomRange(rng, entry.scaleMin, entry.scaleMax), rng, parent);
                    propHash.Insert(p2);
                    bushCnt[(int)nation]++;
                    placed++;
                }
            }
        }

        static void PlaceNationFlowers(NationDecoProfile p, CategoriesR4 cat, Vector3 origin,
            Transform parent, SpatialHash treeHash, SpatialHash propHash, int[] flowerCnt, System.Random rng)
        {
            if (p.flowers == null || p.flowers.Count == 0) return;
            int placed = 0;
            float lim = BOUND_MAX - FLOWER_CELL;
            for (float gx = -lim; gx <= lim && placed < p.flowerCap; gx += FLOWER_CELL)
            {
                for (float gz = -lim; gz <= lim; gz += FLOWER_CELL)
                {
                    float x = gx + RandomRange(rng, -FLOWER_CELL * 0.4f, FLOWER_CELL * 0.4f);
                    float z = gz + RandomRange(rng, -FLOWER_CELL * 0.4f, FLOWER_CELL * 0.4f);
                    if (!InBounds(x, z, origin)) continue;
                    if (IsInSpawnExclusion(x, z)) continue;
                    var nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                    if (nation != p.nation) continue;
                    // B2: 동/남 방위 꽃밭 집중 — 집중 게이트(0.52), 나머지 0.55 (방위색 규칙 유지)
                    float gate = (p.nation == NationType.East || p.nation == NationType.South)
                        ? FLOWER_MASK_FOCUS : FLOWER_MASK_HI;
                    if (TerrainShape.GetFlowerPatchMask(x, z) < gate) continue;
                    if (flowerCnt[(int)nation] >= p.flowerCap) continue;
                    if (IsNearLakeWater(x, z, 1.05f)) continue;
                    if (TerrainSplatBaker.EstimateSlopeDegrees(x, z) > 30f) continue;  // B2: 급사면 화단 금지
                    var p2 = new Vector2(x, z);
                    if (!treeHash.IsFree(p2, TRUNK_CLEAR)) continue;
                    if (!propHash.IsFree(p2, FLOWER_MIN_DIST)) continue;
                    WPrefab entry = PickWeighted(p.flowers, rng);
                    float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42) + 0.05f;
                    Place(entry.prefab, x, y, z, RandomRange(rng, entry.scaleMin, entry.scaleMax), rng, parent);
                    propHash.Insert(p2);
                    flowerCnt[(int)nation]++;
                    placed++;
                }
            }
        }

        /// <summary>
        /// B4: 흙길 가장자리 소형 데코 — 흙길 세그먼트를 PATH_EDGE_STEP(7m) 간격으로 걷으며
        /// 세그먼트 법선 좌우 랜덤측, 길 중심선에서 7~9m(PATH_EDGE_INNER~OUTER, 7m 금지 벨트 바로 바깥)에
        /// 자갈(rockSmall 0.3~0.6) 40% / 허브 역할 잔디 풋(0.8~1.1) 30% / 들꽃(0.7~1.0) 30% 배치.
        /// 들꽃은 PlaceNationFlowers 방식(국가 선호색 p.flowers 가중 랜덤) 재사용.
        /// 최소간격 PATH_EDGE_KEEP(2m) SpatialHash + 국가당 상한 PATH_EDGE_CAP(150).
        /// 경계: ±1550m 클램프 스킵 / 스폰 지점 제외 / 타 세그먼트 7m 접점 스킵(이중 방어).
        /// 호수 마진은 미검사(자갈/잔디/꽃 모두 육지 프리팹). 결정론 rng만 사용.
        /// </summary>
        static int PlacePathEdgeDeco(NationDecoProfile p, CategoriesR4 cat, Vector3 origin,
            Transform flowersT, Transform grassT, Transform rocksT, SpatialHash hash, System.Random rng)
        {
            var paths = NationTerrainController.DirtPaths;
            if (paths == null || paths.Count == 0) return 0;
            if ((cat.rockSmall == null || cat.rockSmall.Count == 0)
                && (cat.grass == null || cat.grass.Count == 0)
                && (p.flowers == null || p.flowers.Count == 0)) return 0;

            int placed = 0;
            for (int i = 0; i < paths.Count && placed < PATH_EDGE_CAP; i++)
            {
                var s = paths[i];
                float vx = s.X1 - s.X0, vz = s.Z1 - s.Z0;
                float len = Mathf.Sqrt(vx * vx + vz * vz);
                if (len <= 0.01f) continue;
                float nx = -vz / len, nz = vx / len;   // 세그먼트 법선(길 직진 방향의 좌우)
                int steps = Mathf.Max(1, Mathf.FloorToInt(len / PATH_EDGE_STEP));
                for (int k = 0; k < steps && placed < PATH_EDGE_CAP; k++)
                {
                    // 등간격 중간점 샘플 — 인접 세그먼트 끝점과의 겹침 방지
                    float t = (k + 0.5f) / steps;
                    float cx = s.X0 + vx * t;
                    float cz = s.Z0 + vz * t;
                    // 좌우 중 랜덤 한쪽, 길 중심선에서 7~9m 지점
                    float side = rng.Next(2) == 0 ? -1f : 1f;
                    float d = RandomRange(rng, PATH_EDGE_INNER, PATH_EDGE_OUTER);
                    float x = cx + nx * side * d;
                    float z = cz + nz * side * d;

                    if (!InBounds(x, z, origin)) continue;               // origin ±1550m 경계 스킵
                    if (IsInSpawnExclusion(x, z)) continue;              // 스폰 지점 확보
                    var nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                    if (nation != p.nation) continue;                    // 국가 영역 밖 스킵(프로필별 1회)
                    if (IsNearDirtPath(x, z, PATH_EDGE_INNER)) continue; // 이중 방어 — 다른 세그먼트 7m 이내 스킵
                    var p2 = new Vector2(x, z);
                    if (!hash.IsFree(p2, PATH_EDGE_KEEP)) continue;      // 가장자리 데코 최소간격 2m

                    float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                    // 종류 추첨: 자갈 40% / 허브(잔디 풋) 30% / 들꽃 30% (부재 시 순차 폴백)
                    float roll = (float)rng.NextDouble();
                    if (roll < 0.40f && cat.rockSmall.Count > 0)
                    {
                        // 자갈: rockSmall 0.3~0.6 — 소형 프리팹(충돌체/섀도우 없음, 통행 방해 없음)
                        Place(cat.rockSmall[rng.Next(cat.rockSmall.Count)], x, y, z,
                            RandomRange(rng, 0.3f, 0.6f), rng, rocksT);
                    }
                    else if (roll < 0.70f && cat.grass != null && cat.grass.Count > 0)
                    {
                        // 허브 역할: 잔디 풋 0.8~1.1 (PlaceGrass = yaw 랜덤 + ±8° 기울기)
                        PlaceGrass(cat.grass[rng.Next(cat.grass.Count)], x, y + 0.04f, z,
                            RandomRange(rng, 0.8f, 1.1f), rng, grassT);
                    }
                    else if (p.flowers != null && p.flowers.Count > 0)
                    {
                        // 들꽃: PlaceNationFlowers 방식 — 국가 선호색 꽃 가중 랜덤, 0.7~1.0
                        WPrefab entry = PickWeighted(p.flowers, rng);
                        Place(entry.prefab, x, y + 0.05f, z, RandomRange(rng, 0.7f, 1.0f), rng, flowersT);
                    }
                    else if (cat.rockSmall.Count > 0)
                    {
                        Place(cat.rockSmall[rng.Next(cat.rockSmall.Count)], x, y, z,
                            RandomRange(rng, 0.3f, 0.6f), rng, rocksT);
                    }
                    else continue;

                    hash.Insert(p2);
                    placed++;
                }
            }
            return placed;
        }

        static void PlaceFantasyMeadows(NationDecoProfile p, CategoriesR4 cat, Vector3 origin,
            Transform parent, SpatialHash treeHash, SpatialHash propHash, int[] meadowCnt, System.Random rng)
        {
            if (p.meadows == null || p.meadows.Count == 0) return;
            int placed = 0;
            float lim = BOUND_MAX - MEADOW_JITTER;
            for (float gx = -lim; gx <= lim && placed < p.meadowCap; gx += MEADOW_SPACING)
            {
                for (float gz = -lim; gz <= lim; gz += MEADOW_SPACING)
                {
                    float x = gx + RandomRange(rng, -MEADOW_JITTER, MEADOW_JITTER);
                    float z = gz + RandomRange(rng, -MEADOW_JITTER, MEADOW_JITTER);
                    if (!InBounds(x, z, origin)) continue;
                    if (IsInSpawnExclusion(x, z)) continue;
                    var nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                    if (nation != p.nation) continue;
                    if (TerrainShape.GetFantasySubzoneMask(x, z, p.nation, T_R4_BASE) < FANTASY_MASK_HI) continue;
                    if (meadowCnt[(int)nation] >= p.meadowCap) continue;
                    var p2 = new Vector2(x, z);
                    if (!treeHash.IsFree(p2, TRUNK_CLEAR)) continue;
                    if (!propHash.IsFree(p2, MEADOW_MIN_DIST)) continue;
                    WPrefab entry = PickWeighted(p.meadows, rng);
                    float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42) + 0.1f;
                    Place(entry.prefab, x, y, z, RandomRange(rng, 0.9f, 1.3f), rng, parent);
                    propHash.Insert(p2);
                    meadowCnt[(int)nation]++;
                    placed++;
                }
            }
        }

        static int PlaceEmpireGarden(Vector3 origin, CategoriesR4 cat,
            Transform forestT, Transform bushesT, SpatialHash treeHash, SpatialHash propHash, System.Random rng)
        {
            int placed = 0;
            float grid = 22f;
            float lim = EMPIRE_GARDEN_RADIUS;
            for (float gx = -lim; gx <= lim && placed < EMPIRE_GARDEN_CAP; gx += grid)
            {
                for (float gz = -lim; gz <= lim; gz += grid)
                {
                    float x = origin.x + gx + RandomRange(rng, -4f, 4f);
                    float z = origin.z + gz + RandomRange(rng, -4f, 4f);
                    float dx = x - origin.x, dz = z - origin.z;
                    float rc2 = dx * dx + dz * dz;
                    if (rc2 < EMPIRE_CORE_EXCLUDE * EMPIRE_CORE_EXCLUDE) continue;
                    if (rc2 > EMPIRE_GARDEN_RADIUS * EMPIRE_GARDEN_RADIUS) continue;
                    if (placed >= (int)EMPIRE_GARDEN_CAP) return placed;
                    if (IsInSpawnExclusion(x, z)) continue;
                    var p2 = new Vector2(x, z);
                    if (!treeHash.IsFree(p2, TREE_MIN_DIST)) continue;
                    if (!propHash.IsFree(p2, BUSH_MIN_DIST)) continue;
                    float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                    double r = rng.NextDouble();
                    if (r < 0.50 && cat.blossom.Count > 0)
                        Place(cat.blossom[rng.Next(cat.blossom.Count)], x, y, z, RandomRange(rng, 0.7f, 1.0f), rng, forestT);
                    else if (r < 0.80 && cat.broadGreen.Count > 0)
                        Place(cat.broadGreen[rng.Next(cat.broadGreen.Count)], x, y, z, RandomRange(rng, 0.5f, 0.75f), rng, forestT);
                    else if (cat.bushes.Count > 0)
                        Place(cat.bushes[rng.Next(cat.bushes.Count)], x, y, z, RandomRange(rng, 0.6f, 0.85f), rng, bushesT);
                    else if (cat.blossom.Count > 0)
                        Place(cat.blossom[rng.Next(cat.blossom.Count)], x, y, z, 0.8f, rng, forestT);
                    treeHash.Insert(p2);
                    propHash.Insert(p2);
                    placed++;
                }
            }
            return placed;
        }

        // ================================================================
        // T-D2 (09-08): 노출 암반 위성 바위 군집 + 서쪽 천연 아치 + 대형 꽃 융단 데코
        // ================================================================

        /// <summary>
        /// T-D2: 노출 암반(TerrainShape.GetOutcropCenters) 주위 위성 바위 군집 —
        /// 사이트당 rockBig 2~4 + rockMed 4~8을 반경 0.3~1.1r에 결정론 배치 (예시2/4/8의 암돔+위성 바위).
        /// 보호구역(SampleCliffSuppression&lt;0.5)/수면(1.15r)/흙길 7m/최소간격 검사. 기존 rockCap과 별도.
        /// </summary>
        static int PlaceOutcropRocks(CategoriesR4 cat, Transform rocksT, SpatialHash treeHash, SpatialHash propHash)
        {
            if (cat.rockBig.Count == 0 && cat.rockMed.Count == 0) return 0;
            var nations = new NationType[] { NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire };
            int total = 0;
            for (int n = 0; n < nations.Length; n++)
            {
                var sites = TerrainShape.GetOutcropCenters(nations[n], 42);
                if (sites == null || sites.Count == 0) continue;
                var rng = new System.Random(NationSeed(nations[n]) + 17);
                for (int s = 0; s < sites.Count; s++)
                {
                    var site = sites[s];
                    int bigCount = OUTCROP_BIG_MIN + rng.Next(OUTCROP_BIG_MAX - OUTCROP_BIG_MIN + 1);
                    int medCount = OUTCROP_MED_MIN + rng.Next(OUTCROP_MED_MAX - OUTCROP_MED_MIN + 1);
                    int target = bigCount + medCount;
                    int placed = 0;
                    int attempts = target * 8;
                    for (int a = 0; a < attempts && placed < target; a++)
                    {
                        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float rr = site.radius * Mathf.Lerp(0.3f, 1.1f, (float)rng.NextDouble());
                        float x = site.center.x + Mathf.Cos(ang) * rr;
                        float z = site.center.y + Mathf.Sin(ang) * rr;
                        if (Mathf.Abs(x) > BOUND_MAX || Mathf.Abs(z) > BOUND_MAX) continue;
                        if (TerrainGenerator.SampleCliffSuppression(x, z) < 0.5f) continue;
                        if (IsNearLakeWater(x, z, 1.15f)) continue;
                        if (IsNearDirtPath(x, z, DIRT_PATH_CLEAR)) continue;
                        var p2 = new Vector2(x, z);
                        if (!treeHash.IsFree(p2, TRUNK_CLEAR)) continue;
                        if (!propHash.IsFree(p2, ROCK_MIN_DIST)) continue;
                        bool big = placed < bigCount && cat.rockBig.Count > 0;
                        var pool = big ? cat.rockBig : (cat.rockMed.Count > 0 ? cat.rockMed : cat.rockBig);
                        if (pool.Count == 0) continue;
                        float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                        float scale = big
                            ? RandomRange(rng, OUTCROP_SCALE_BIG_MIN, OUTCROP_SCALE_BIG_MAX)
                            : RandomRange(rng, OUTCROP_SCALE_MED_MIN, OUTCROP_SCALE_MED_MAX);
                        Place(pool[rng.Next(pool.Count)], x, y, z, scale, rng, rocksT);
                        propHash.Insert(p2);
                        placed++; total++;
                    }
                }
            }
            Debug.Log($"[IdyllicDecoPlacer][T-D2] OutcropRocks={total} (암반 사이트 위성 군집)");
            return total;
        }

        /// <summary>
        /// T-D2: 서쪽 천연 아치(예시5) — GetWestArchPosition에 rockBig을 ARCH_SCALE로 배치.
        /// 지형 쪽에 받침 암돔 2개(GetOutcropMask West 강제)가 이미 있어 함께 아치 실루엣을 이룬다.
        /// 적합한 프리팹 부재/보호구역이면 스킵 (강제 금지).
        /// </summary>
        static int PlaceWestArch(CategoriesR4 cat, Transform rocksT)
        {
            if (cat.rockBig.Count == 0)
            {
                Debug.Log("[IdyllicDecoPlacer][T-D2] WestArch: rockBig 프리팹 없음 — 스킵.");
                return 0;
            }
            Vector3 archPos = TerrainGenerator.GetWestArchPosition(42);
            if (TerrainGenerator.SampleCliffSuppression(archPos.x, archPos.z) < 0.5f)
            {
                Debug.Log("[IdyllicDecoPlacer][T-D2] WestArch: 보호구역 — 스킵.");
                return 0;
            }
            var rng = new System.Random(NationSeed(NationType.West) + 23);
            float y = GROUND_BASE + TerrainGenerator.GetHeightAt(archPos.x, archPos.z, BiomeType.Plains, 42);
            var go = Object.Instantiate(cat.rockBig[rng.Next(cat.rockBig.Count)], rocksT);
            go.layer = 0;
            go.transform.position = new Vector3(archPos.x, y, archPos.z);
            // 원점→아치 방향에 수직인 yaw — 지형 받침 바위 2개(±15m 수직방향)와 나란히 정렬
            float dirAng = Mathf.Atan2(archPos.z, archPos.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, dirAng + 90f, 0f);
            go.transform.localScale = Vector3.one * ARCH_SCALE;
            Debug.Log($"[IdyllicDecoPlacer][T-D2] WestArch=({archPos.x:F0},{archPos.z:F0}) y={y:F1} scale={ARCH_SCALE}");
            return 1;
        }

        /// <summary>
        /// T-D2: 대형 꽃 융단(TerrainShape.GetMegaFlowerPatchMask, 예시12/13 핑크/마젠타 카펫) 내부
        /// 밀집 꽃 데코 — MEGA_SCAN_CELL 격자로 마스크 히트 시 4×4 클러스터 배치.
        /// 팔레트는 소유 방위와 무관하게 핑크+퍼플(융단 정체성). MEGA_CAP_PER_NATION 별도 상한.
        /// 물(1.05r)/스폰/급사면/최소간격 검사. GetMegaFlowerPatchMask는 소유 방위 호출에 황제국 공유
        /// 세트가 포함되므로 소유 방위 단일 스캔으로 전체 패치가 정확히 1회씩 커버된다.
        /// </summary>
        static int PlaceMegaFlowerPatches(CategoriesR4 cat, Transform flowersT, SpatialHash treeHash, SpatialHash propHash)
        {
            var palette = new List<GameObject>();
            palette.AddRange(cat.flowerPink);
            palette.AddRange(cat.flowerPurple);
            if (palette.Count == 0) return 0;

            int placed = 0;
            int[] cnt = new int[8];
            float lim = BOUND_MAX - MEGA_SCAN_CELL;
            for (float gx = -lim; gx <= lim; gx += MEGA_SCAN_CELL)
            {
                for (float gz = -lim; gz <= lim; gz += MEGA_SCAN_CELL)
                {
                    if (cnt[0] + cnt[1] + cnt[2] + cnt[3] + cnt[4] >= MegaCapFor(NationType.East) * 4) break;   // 전역 안전 밸브
                    float x = gx + RandomRange(new System.Random((int)(gx * 31 + gz)), -MEGA_SCAN_CELL * 0.4f, MEGA_SCAN_CELL * 0.4f);
                    float z = gz + RandomRange(new System.Random((int)(gx * 17 + gz * 7)), -MEGA_SCAN_CELL * 0.4f, MEGA_SCAN_CELL * 0.4f);
                    var nat = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                    int ni = (int)nat;
                    if (ni < 0 || ni >= cnt.Length) continue;
                    if (cnt[ni] >= MegaCapFor(nat)) continue;
                    if (TerrainShape.GetMegaFlowerPatchMask(x, z, nat, 42) < 0.5f) continue;
                    if (IsInSpawnExclusion(x, z)) continue;
                    if (IsNearLakeWater(x, z, 1.05f)) continue;
                    if (TerrainSplatBaker.EstimateSlopeDegrees(x, z) > 30f) continue;
                    var rng = new System.Random(NationSeed(nat) + 29 + (int)(x * 3.1f) * 7 + (int)(z * 2.3f) * 13);
                    for (int si = 0; si < MEGA_CLUSTER_SUB * MEGA_CLUSTER_SUB && cnt[ni] < MegaCapFor(nat); si++)
                    {
                        int sx = si % MEGA_CLUSTER_SUB, sz = si / MEGA_CLUSTER_SUB;
                        float fx = x + (sx + 1 - (MEGA_CLUSTER_SUB + 1) * 0.5f) * MEGA_CLUSTER_STEP + RandomRange(rng, -0.4f, 0.4f);
                        float fz = z + (sz + 1 - (MEGA_CLUSTER_SUB + 1) * 0.5f) * MEGA_CLUSTER_STEP + RandomRange(rng, -0.4f, 0.4f);
                        var p2 = new Vector2(fx, fz);
                        if (!treeHash.IsFree(p2, TRUNK_CLEAR)) continue;
                        if (!propHash.IsFree(p2, 1.2f)) continue;
                        if (IsNearLakeWater(fx, fz, 1.02f)) continue;
                        float y = GROUND_BASE + TerrainGenerator.GetHeightAt(fx, fz, BiomeType.Plains, 42) + 0.05f;
                        Place(palette[rng.Next(palette.Count)], fx, y, fz, RandomRange(rng, 0.7f, 1.0f), rng, flowersT);
                        propHash.Insert(p2);
                        cnt[ni]++; placed++;
                    }
                }
            }
            int e = cnt[(int)NationType.East], w = cnt[(int)NationType.West], so = cnt[(int)NationType.South];
            int no = cnt[(int)NationType.North], em = cnt[(int)NationType.Empire];
            Debug.Log($"[IdyllicDecoPlacer][T-D2] MegaFlower Total={placed} (E{e}/W{w}/S{so}/N{no}/Emp{em}, cap 60~80/nation)");
            return placed;
        }

        public static void PlaceSpawnProps(Transform parent)
        {
            if (parent == null) return;
            if (FindDirectChild(parent, SPAWN_ROOT_NAME) != null)
            {
                Debug.Log("[IdyllicDecoPlacer][SpawnProps] Already placed - skipping.");
                return;
            }
            var trees = LoadSet("IdyllicPrefabs/Trees");
            if (trees.Count == 0)
            {
                Debug.LogWarning("[IdyllicDecoPlacer][SpawnProps] No tree prefabs - spawn props skipped.");
                return;
            }
            var root = new GameObject(SPAWN_ROOT_NAME);
            root.transform.SetParent(parent, false);
            root.layer = 0;
            var rng = new System.Random(20260904 + 9000);
            Vector3 spawn = ProjectName.Core.PlayerSpawnConfig.SpawnPosition;
            int placed = 0;
            int attempts = SPAWN_PROPS_TREES * 8;
            for (int i = 0; i < attempts && placed < SPAWN_PROPS_TREES; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = RandomRange(rng, 30f, SPAWN_PROP_RADIUS);
                float x = spawn.x + Mathf.Cos(ang) * radius;
                float z = spawn.z + Mathf.Sin(ang) * radius;
                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                var model = trees[rng.Next(trees.Count)];
                var go = Place(model, x, y, z, RandomRange(rng, 0.8f, 1.4f), rng, root.transform);
                AddTreeCollider(go);
                placed++;
            }
            Debug.Log(string.Format(
                "[IdyllicDecoPlacer][SpawnProps] Spawn radius {0}m Idyllic trees {1} (with colliders) - TerrainPropPlacer replaced.",
                SPAWN_PROP_RADIUS, placed));
        }
        static int Sum(int[] counts)
        {
            int s = 0;
            for (int i = 0; i < counts.Length; i++) s += counts[i];
            return s;
        }

        static bool InBounds(float x, float z, Vector3 origin)
        {
            return Mathf.Abs(x - origin.x) <= BOUND_MAX && Mathf.Abs(z - origin.z) <= BOUND_MAX;
        }

        static bool IsInSpawnExclusion(float x, float z)
        {
            float dx = x - SPAWN_POS_X;
            float dz = z - SPAWN_POS_Z;
            return dx * dx + dz * dz < SPAWN_EXCLUDE * SPAWN_EXCLUDE;
        }

        /// <summary>호수 수면 인근(관리 밴드) 판정 — marginFactor*radius 이내면 true.</summary>
        static bool IsNearLakeWater(float x, float z, float marginFactor)
        {
            var lakes = TerrainGenerator.Lakes;
            for (int i = 0; i < lakes.Count; i++)
            {
                var lake = lakes[i];
                float dx = x - lake.center.x;
                float dz = z - lake.center.z;
                float m = lake.radius * marginFactor;
                if (dx * dx + dz * dz < m * m)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 흙길 위 배치 금지 판정 — 세그먼트 AABB 사전 필터(반경 여유 포함) 후 점-선분 최단거리 검사.
        /// NationTerrainController.DirtPaths (결정론 캐시, 스포크 4 + 링 48 + 스폰 1 = 53 세그먼트).
        /// 배치 시점에 1회 호출이라 O(후보×53) 허용.
        /// </summary>
        static bool IsNearDirtPath(float x, float z, float radius)
        {
            var paths = NationTerrainController.DirtPaths;
            if (paths == null || paths.Count == 0) return false;
            int count = paths.Count;
            float r2 = radius * radius;
            for (int i = 0; i < count; i++)
            {
                var s = paths[i];
                // 1차 필터: 세그먼트 AABB(+반폭 여유) + 반경 — 벗어나면 즉시 스킵
                if (x < s.MinX - radius || x > s.MaxX + radius
                    || z < s.MinZ - radius || z > s.MaxZ + radius)
                    continue;
                // 2차: 점-선분 최단거리 제곱 < 반경 제곱이면 길 위
                float vx = s.X1 - s.X0, vz = s.Z1 - s.Z0;
                float wx = x - s.X0, wz = z - s.Z0;
                float len2 = vx * vx + vz * vz;
                float t = len2 > 0f ? Mathf.Clamp01((wx * vx + wz * vz) / len2) : 0f;
                float dx = s.X0 + vx * t - x;
                float dz = s.Z0 + vz * t - z;
                if (dx * dx + dz * dz < r2) return true;
            }
            return false;
        }

        static GameObject Place(GameObject model, float x, float y, float z, float scale, System.Random rng, Transform parent)
        {
            var go = Object.Instantiate(model, parent);
            go.layer = 0; // Default — 스폰 raycast(Ground|Terrain 마스크) 자동 무시
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static Transform NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.layer = 0;
            return go.transform;
        }

        static List<GameObject> LoadSet(string folder)
        {
            var loaded = Resources.LoadAll<GameObject>(folder);
            var list = new List<GameObject>();
            if (loaded == null) return list;
            foreach (var g in loaded) if (g != null) list.Add(g);
            return list;
        }

        /// <summary>이름 필터 — prefix를 포함하고(옵션) suffix로 끝나는 프리팹만.</summary>
        static List<GameObject> Filter(List<GameObject> src, string prefix, string suffix)
        {
            var list = new List<GameObject>();
            foreach (var g in src)
            {
                if (g == null) continue;
                var n = g.name;
                if (prefix != null && !n.Contains(prefix)) continue;
                if (suffix != null && !n.EndsWith(suffix)) continue;
                list.Add(g);
            }
            return list;
        }

        /// <summary>Filter 결과(List<GameObject>)를 PickWeighted가 요구하는 List<WPrefab>로 변환.</summary>
        static List<WPrefab> FilterPrefabs(List<GameObject> src, string prefix, string suffix)
        {
            var list = new List<WPrefab>();
            foreach (var g in src)
            {
                if (g == null) continue;
                var n = g.name;
                if (prefix != null && !n.Contains(prefix)) continue;
                if (suffix != null && !n.EndsWith(suffix)) continue;
                list.Add(new WPrefab { prefab = g, weight = 1f, scaleMin = 1f, scaleMax = 1f });
            }
            return list;
        }

        static float RandomRange(System.Random rng, float min, float max)
        {
            return (float)(rng.NextDouble() * (max - min) + min);
        }

        /// <summary>
        /// AA4: (x,z) 위치 기반 결정론 해시 스케일 변주 [min,max] — 프리팹 원본 스케일에 곱한다.
        /// UnityEngine.Random을 호출하지 않아 rng 시퀀스와 배치 결정론을 바꾸지 않는다 (같은 위치→같은 변주).
        /// </summary>
        static float ScaleVariation(float x, float z, int salt, float min, float max)
        {
            uint hx = System.BitConverter.ToUInt32(System.BitConverter.GetBytes(x), 0);
            uint hz = System.BitConverter.ToUInt32(System.BitConverter.GetBytes(z), 0);
            uint h = hx ^ (hz * 0x9E3779B9u) ^ ((uint)salt * 0x85EBCA6Bu);
            h = (h ^ (h >> 16)) * 0x85EBCA6Bu;
            h = (h ^ (h >> 13)) * 0xC2B2AE35u;
            h ^= h >> 16;
            float t = (h & 0xFFFFFF) / 16777216f;
            return min + t * (max - min);
        }

        // ================================================================
        // CC3: 나무 방위색 틴트 + 데코 블롭 섀도우
        // ================================================================

        /// <summary>원본 머티리얼×(국가) 키 → 틴트 복제 캐시. 같은 원본+색은 1클론 공유 → instancing 유지.</summary>
        static readonly Dictionary<int, Material> _nationTintCache = new Dictionary<int, Material>();
        // _Custom_Color 타입 불일치 경고 1회 출력용 플래그(재질마다 반복 출력되는 경고 스팸 방지)
        static bool _customColorTypeMismatchWarned;

        /// <summary>
        /// CC3: 국가별 나무 잎사귀 틴트. 팩 Vegetation 셰이더(_Custom_Color/_Color)를 쓰는 재료를
        /// 복제(공유 금지) 후 해당 국가 색 세팅 → 해당 나무 renderer에 할당.
        /// 색: 동 라임그린 / 서 올리브 / 남 짙은 녹(붉은 프리팹은 포인트 유지) / 북 설빙. 중앙(황제국)은 프리팹 원본.
        /// </summary>
        static void ApplyNationTreeTint(GameObject go, NationType nation, GameObject prefab)
        {
            if (nation != NationType.East && nation != NationType.West
                && nation != NationType.South && nation != NationType.North) return;

            Color col;
            switch (nation)
            {
                case NationType.East:  col = new Color(0.55f, 0.85f, 0.25f); break; // 라임그린
                case NationType.West:  col = new Color(0.55f, 0.65f, 0.28f); break; // 올리브
                case NationType.South: col = new Color(0.25f, 0.55f, 0.20f); break; // 짙은 녹
                default:               col = new Color(0.88f, 0.94f, 1.00f); break; // 북 설빙
            }

            // 남: 기존 붉은 나무 프리팹(Broadleaf*Red)은 붉은 포인트로 유지 — 틴트 스킵
            if (nation == NationType.South && prefab != null
                && prefab.name.IndexOf("Red", System.StringComparison.OrdinalIgnoreCase) >= 0) return;

            var renderers = go.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader == null) continue;
                    if (!m.HasProperty("_Custom_Color") && !m.HasProperty("_Color")) continue;
                    string mn = m.name == null ? "" : m.name;
                    // 줄기/가지/뿌리는 색 유지 — 잎사귀만 틴트
                    if (mn.IndexOf("Bark", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || mn.IndexOf("Branch", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || mn.IndexOf("Trunk", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || mn.IndexOf("Wood", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    var clone = GetNationTintClone(m, col, nation);
                    var arr = (Material[])mats.Clone();
                    if (i < arr.Length) arr[i] = clone;
                    r.sharedMaterials = arr;
                }
            }
        }

        static Material GetNationTintClone(Material original, Color tint, NationType nation)
        {
            int key = original.GetInstanceID() * 31 + (int)nation;
            Material cached;
            if (_nationTintCache.TryGetValue(key, out cached) && cached != null) return cached;
            Material clone = new Material(original);
            clone.name = original.name + "_Tint" + nation;
            // CC3: 팩 Vegetation 셰이더는 _Custom_Color(잎 혼합) 우선, 없으면 URP Lit _Color/_BaseColor.
            // 셋 다 없으면 스킵(원본 유지) + 로그 — 틴트 가능한 재료만 복제한다.
            // 경고 스팸("Property _Custom_Color already exists ... different type: 0") 방지:
            // 셰이더가 _Custom_Color를 Color형으로 노출할 때만 SetColor. idx 미노출 또는
            // 타입 불일치(Vector/Float 등)면 SetColor가 매 호출 실패+스팸 → 기존 폴백(_Color→_BaseColor).
            if (clone.HasProperty("_Custom_Color"))
            {
                var idx = clone.shader != null ? clone.shader.FindPropertyIndex("_Custom_Color") : -1;
                if (idx < 0 || clone.shader.GetPropertyType(idx) != UnityEngine.Rendering.ShaderPropertyType.Color)
                {
                    // 셰이더가 속성을 노출하지 않거나(직렬화 잔존) Color형이 아니면
                    // SetColor 스팸 없이 _Color/_BaseColor로 폴백. 타입 불일치 경고는 1회만.
                    if (idx >= 0 && !_customColorTypeMismatchWarned)
                    {
                        _customColorTypeMismatchWarned = true;
                        Debug.LogWarning($"[IdyllicDecoPlacer] _Custom_Color가 Color형 아님({clone.shader.GetPropertyType(idx)}) → _Color/_BaseColor 폴백(이 경고는 1회만 출력): {clone.shader.name}");
                    }
                    if (clone.HasProperty("_Color")) clone.SetColor("_Color", tint);
                    else if (clone.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", tint);
                }
                else
                {
                    clone.SetColor("_Custom_Color", tint);
                }
            }
            else if (clone.HasProperty("_Color")) clone.SetColor("_Color", tint);
            else if (clone.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", tint);
            else
            {
                Debug.LogWarning($"[IdyllicDecoPlacer] 틴트 속성(_Custom_Color/_Color/_BaseColor) 없음 → 원본 유지: {original.name}");
                Object.Destroy(clone);
                return original;
            }
            _nationTintCache[key] = clone;
            return clone;
        }

        /// <summary>위치 기반 결정론 [0,1) 샘플이 chance 미만이면 true (데코 섀도우 선별용).</summary>
        static bool ShouldShadow(float x, float z, int salt, float chance)
        {
            return ScaleVariation(x, z, salt, 0f, 1f) < chance;
        }

        static GameObject FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c != null && c.gameObject.name == name) return c.gameObject;
            }
            return null;
        }

        static void EnableGPUInstancing(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && !mats[i].enableInstancing)
                        mats[i].enableInstancing = true;
                }
            }
        }

        /// <summary>결정론적 최소 간격 검사용 균일 격자 공간 해시.</summary>
        class SpatialHash
        {
            readonly float _cell;
            readonly Dictionary<long, List<Vector2>> _map = new Dictionary<long, List<Vector2>>();

            public SpatialHash(float cell) { _cell = Mathf.Max(0.01f, cell); }

            static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

            public bool IsFree(Vector2 p, float minDist)
            {
                int cx = Mathf.FloorToInt(p.x / _cell);
                int cz = Mathf.FloorToInt(p.y / _cell);
                int r = Mathf.CeilToInt(minDist / _cell);
                float minSq = minDist * minDist;
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        List<Vector2> bucket;
                        if (!_map.TryGetValue(Key(cx + dx, cz + dz), out bucket)) continue;
                        for (int i = 0; i < bucket.Count; i++)
                        {
                            float ddx = bucket[i].x - p.x;
                            float ddz = bucket[i].y - p.y;
                            if (ddx * ddx + ddz * ddz < minSq) return false;
                        }
                    }
                }
                return true;
            }

            public void Insert(Vector2 p)
            {
                int cx = Mathf.FloorToInt(p.x / _cell);
                int cz = Mathf.FloorToInt(p.y / _cell);
                long k = Key(cx, cz);
                List<Vector2> bucket;
                if (!_map.TryGetValue(k, out bucket))
                {
                    bucket = new List<Vector2>();
                    _map[k] = bucket;
                }
                bucket.Add(p);
            }
        }

        // ================================================================
        // T-R4: 카테고리 로드 및 국가별 NationDecoProfile 구성
        // ================================================================

        /// <summary>IdyllicPrefabs/{Trees,Rocks,Bushes,Shore,Water,Flowers,Meadows} 이름 매핑으로 카테고리 구성.</summary>
        static CategoriesR4 BuildCategoriesR4()
        {
            var c = new CategoriesR4();
            var trees = LoadSet("IdyllicPrefabs/Trees");
            var rocks = LoadSet("IdyllicPrefabs/Rocks");
            var bushes = LoadSet("IdyllicPrefabs/Bushes");
            var shore = LoadSet("IdyllicPrefabs/Shore");
            var water = LoadSet("IdyllicPrefabs/Water");
            var flowers = LoadSet("IdyllicPrefabs/Flowers");
            var meadows = LoadSet("IdyllicPrefabs/Meadows");
            var grass = LoadSet("IdyllicPrefabs/Grass");   // AA5

            c.willow = Filter(trees, "WillowTree", null);
            c.broadGreen = Filter(trees, "BroadleafTree", "Green");
            c.broadPurple = Filter(trees, "BroadleafTree", "Purple");
            c.broadRed = Filter(trees, "BroadleafTree", "Red");
            c.fir = Filter(trees, "Fir", null);
            c.blossom = Filter(trees, "BlossomTree", null);

            c.rockBig = Filter(rocks, "Rock_Big", null);
            c.rockMed = Filter(rocks, "Rock_Medium", null);
            c.rockSmall = Filter(rocks, "Rock_Small", null);
            c.bushes = bushes;

            c.cattail = Filter(shore, "Cattail", null);
            c.reeds = Filter(shore, "Reeds", null);
            c.lilyPads = Filter(water, "LilyPads", null);
            c.waterLily = Filter(water, "Waterlily", null);

            c.flowerYellow = Filter(flowers, "Flower_Yellow", null);
            c.flowerWhite = Filter(flowers, "Flower_White", null);
            c.flowerRed = Filter(flowers, "Flower_Red", null);
            c.flowerPurple = Filter(flowers, "Flower_Purple", null);
            c.flowerPink = Filter(flowers, "Flower_Pink", null);
            c.flowerBlue = Filter(flowers, "Flower_Blue", null);

            c.meadowWhite = Filter(meadows, "FlowerMeadow", "White");
            c.meadowRed = Filter(meadows, "FlowerMeadow", "Red");
            c.meadowRedOrange = Filter(meadows, "FlowerMeadow", "RedOrange");
            c.meadowPurple = Filter(meadows, "FlowerMeadow", "Purple");
            c.meadowPink = Filter(meadows, "FlowerMeadow", "Pink");
            c.meadowBlue = Filter(meadows, "FlowerMeadow", "Blue");
            c.grass = grass;   // AA5: Grass_01/02/03 전부
            return c;
        }

        /// <summary>프리팹 풀 전체를 가중치 단위로 평탄화해 WPrefab 목록에 추가.</summary>
        static void AddPool(List<WPrefab> dst, List<GameObject> pool, float weight,
            float sMin, float sMax, bool collider)
        {
            if (pool == null) return;
            foreach (var g in pool)
            {
                if (g == null) continue;
                dst.Add(new WPrefab { prefab = g, weight = weight, scaleMin = sMin, scaleMax = sMax, collider = collider });
            }
        }

        static WPrefab PickWeighted(List<WPrefab> list, System.Random rng)
        {
            double total = 0;
            for (int i = 0; i < list.Count; i++) total += list[i].weight;
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < list.Count; i++)
            {
                roll -= list[i].weight;
                if (roll <= 0) return list[i];
            }
            return list[list.Count - 1];
        }

        /// <summary>타깃 밀도(1/900㎡ 나무, 1/38² 관목, 1/2500㎡ 바위, 4.5² 꽃) 기준 방위 기본 상한.</summary>
        static void DefaultCaps(NationDecoProfile p)
        {
            // treeCap=1900: Z4 숲 군락 ×4 밀도(군락 내 4/900㎡) 반영 — 전국가(1/900㎡≈1000)+
            // 숲 밴드 3~5개(반경 100~180m, ≈820 추가) 합계 ≈1820가 cap=1150에 잘리지 않도록 여유 상향.
            // B3: treeCap 1900→2600 — 숲 밀도 ×1.5(심부 3×3) 반영 상한.
            // B2: flowerCap 4200→6800 (×1.62), meadowCap 220→280.
            p.treeCap = 2600;
            p.rockCap = 400;
            p.bushCap = 650;
            p.flowerCap = 6800;
            p.meadowCap = 280;
        }

        /// <summary>T-R4 국가별 NationDecoProfile (체크리스트 1).</summary>
        static NationDecoProfile BuildProfile(NationType nation, CategoriesR4 cat)
        {
            var p = new NationDecoProfile { nation = nation };
            DefaultCaps(p);

            switch (nation)
            {
                case NationType.East:
                    // 버드나무40/활엽40/침엽20, 꽃 3색(노랑/초록/파랑 — 화사한 시작지)
                    AddPool(p.trees, cat.willow, 40f, 0.85f, 1.15f, true);
                    AddPool(p.trees, cat.broadGreen, 40f, 0.9f, 1.2f, true);
                    AddPool(p.trees, cat.fir, 20f, 0.8f, 1.1f, true);
                    AddPool(p.fantasyTrees, cat.broadPurple, 1f, 0.85f, 1.15f, true);
                    AddPool(p.fantasyTrees, cat.blossom, 1f, 0.7f, 0.95f, true);
                    AddPool(p.bushes, cat.bushes, 1f, 0.7f, 1.0f, false);
                    AddPool(p.rocks, cat.rockBig, 1f, 0.8f, 1.1f, true);
                    AddPool(p.rocks, cat.rockMed, 2f, 0.7f, 1.0f, false);
                    AddPool(p.rocks, cat.rockSmall, 3f, 0.55f, 0.85f, false);
                    AddPool(p.flowers, cat.flowerYellow, 1f, 0.8f, 1.2f, false);
                    AddPool(p.flowers, cat.flowerWhite, 1f, 0.8f, 1.2f, false);
                    AddPool(p.flowers, cat.flowerBlue, 1f, 0.8f, 1.2f, false);
                    // B2: 색 혼합 — 동 꽃밭에 핑크/레드 소량 추가 (화사한 혼합 화단)
                    AddPool(p.flowers, cat.flowerPink, 0.5f, 0.8f, 1.2f, false);
                    AddPool(p.flowers, cat.flowerRed, 0.35f, 0.8f, 1.2f, false);
                    AddPool(p.meadows, cat.meadowWhite, 1f, 0.9f, 1.3f, false);
                    AddPool(p.meadows, cat.meadowBlue, 1f, 0.9f, 1.3f, false);
                    break;

                case NationType.West:
                    // 활엽60/침엽30/관목10, 이끼(대형)바위 ↑
                    AddPool(p.trees, cat.broadGreen, 60f, 0.9f, 1.2f, true);
                    AddPool(p.trees, cat.fir, 30f, 0.85f, 1.15f, true);
                    AddPool(p.trees, cat.bushes, 10f, 0.8f, 1.1f, false);
                    AddPool(p.fantasyTrees, cat.broadPurple, 1f, 0.85f, 1.15f, true);
                    AddPool(p.fantasyTrees, cat.blossom, 1f, 0.7f, 0.95f, true);
                    AddPool(p.bushes, cat.bushes, 1f, 0.8f, 1.1f, false);
                    AddPool(p.rocks, cat.rockBig, 3f, 0.9f, 1.2f, true);      // 이끼바위↑
                    AddPool(p.rocks, cat.rockMed, 3f, 0.8f, 1.1f, false);
                    AddPool(p.rocks, cat.rockSmall, 3f, 0.6f, 0.9f, false);
                    p.rockCap = 520;
                    AddPool(p.flowers, cat.flowerWhite, 1f, 0.8f, 1.2f, false);
                    AddPool(p.flowers, cat.flowerYellow, 1f, 0.8f, 1.2f, false);
                    AddPool(p.meadows, cat.meadowWhite, 1f, 0.9f, 1.3f, false);
                    AddPool(p.meadows, cat.meadowRedOrange, 1f, 0.9f, 1.3f, false);
                    break;

                case NationType.South:
                    // 활엽(붉은) + 붉은꽃 + 화산암(대형 바위)
                    AddPool(p.trees, cat.broadRed, 60f, 0.9f, 1.2f, true);
                    AddPool(p.trees, cat.fir, 25f, 0.85f, 1.15f, true);
                    AddPool(p.trees, cat.broadGreen, 15f, 0.9f, 1.2f, true);
                    AddPool(p.fantasyTrees, cat.broadPurple, 1f, 0.85f, 1.15f, true);
                    AddPool(p.fantasyTrees, cat.broadRed, 1f, 0.9f, 1.2f, true);
                    AddPool(p.bushes, cat.bushes, 1f, 0.7f, 1.0f, false);
                    AddPool(p.rocks, cat.rockBig, 5f, 0.9f, 1.25f, true);     // 화산암↑
                    AddPool(p.rocks, cat.rockMed, 3f, 0.8f, 1.1f, false);
                    AddPool(p.rocks, cat.rockSmall, 2f, 0.6f, 0.9f, false);
                    AddPool(p.flowers, cat.flowerRed, 2f, 0.8f, 1.2f, false);
                    AddPool(p.flowers, cat.flowerPink, 1.5f, 0.8f, 1.2f, false);   // B2: 1→1.5
                    AddPool(p.flowers, cat.flowerWhite, 1f, 0.8f, 1.2f, false);
                    // B2: 색 혼합 — 남 꽃밭에 퍼플 소량 추가
                    AddPool(p.flowers, cat.flowerPurple, 0.6f, 0.8f, 1.2f, false);
                    AddPool(p.meadows, cat.meadowRed, 2f, 0.9f, 1.3f, false);
                    AddPool(p.meadows, cat.meadowRedOrange, 1f, 0.9f, 1.3f, false);
                    break;

                case NationType.North:
                    // 침엽70/활엽20, 바위↑, 보라꽃
                    AddPool(p.trees, cat.fir, 70f, 0.85f, 1.2f, true);
                    AddPool(p.trees, cat.broadGreen, 20f, 0.9f, 1.15f, true);
                    AddPool(p.trees, cat.willow, 10f, 0.85f, 1.15f, true);
                    AddPool(p.fantasyTrees, cat.broadPurple, 1f, 0.85f, 1.2f, true);
                    AddPool(p.fantasyTrees, cat.blossom, 1f, 0.7f, 0.95f, true);
                    AddPool(p.bushes, cat.bushes, 1f, 0.7f, 1.0f, false);
                    AddPool(p.rocks, cat.rockBig, 4f, 0.9f, 1.25f, true);     // 바위↑
                    AddPool(p.rocks, cat.rockMed, 3f, 0.8f, 1.15f, false);
                    AddPool(p.rocks, cat.rockSmall, 3f, 0.6f, 0.9f, false);
                    p.rockCap = 520;
                    AddPool(p.flowers, cat.flowerPurple, 2f, 0.8f, 1.2f, false);
                    AddPool(p.flowers, cat.flowerBlue, 1f, 0.8f, 1.2f, false);
                    AddPool(p.flowers, cat.flowerWhite, 1f, 0.8f, 1.2f, false);
                    AddPool(p.meadows, cat.meadowPurple, 1f, 0.9f, 1.3f, false);
                    AddPool(p.meadows, cat.meadowBlue, 1f, 0.9f, 1.3f, false);
                    break;

                default:
                    // Empire 등 — 일반 안전 기본값
                    AddPool(p.trees, cat.broadGreen, 1f, 0.9f, 1.2f, true);
                    AddPool(p.trees, cat.fir, 1f, 0.85f, 1.15f, true);
                    AddPool(p.fantasyTrees, cat.broadPurple, 1f, 0.85f, 1.15f, true);
                    AddPool(p.bushes, cat.bushes, 1f, 0.7f, 1.0f, false);
                    AddPool(p.rocks, cat.rockBig, 1f, 0.9f, 1.2f, true);
                    AddPool(p.rocks, cat.rockMed, 2f, 0.8f, 1.1f, false);
                    AddPool(p.rocks, cat.rockSmall, 3f, 0.6f, 0.9f, false);
                    AddPool(p.flowers, cat.flowerWhite, 1f, 0.8f, 1.2f, false);
                    AddPool(p.meadows, cat.meadowWhite, 1f, 0.9f, 1.3f, false);
                    break;
            }

            return p;
        }

        // ================================================================
        // T-R4: 검증 로그
        // ================================================================

        /// <summary>국가별 밀도 로그 (배치 카운트 vs cap). 타깃 밀도와 cap 대비 비율로 ±20% 판정.</summary>
        static void DensityLog(string name, int[] treeCnt, int[] rockCnt, int[] bushCnt, int[] flowerCnt, int[] meadowCnt,
            int treeCap, int rockCap)
        {
            int t = treeCnt[NameIdx(name)];
            Debug.Log(string.Format(
                "[IdyllicDecoPlacer][T-R4] {0}: Trees={1}/{2} Rocks={3}/{4}|Bushes={5}/{6} Flowers={7}/{8}|Meadows={9}/{10}",
                name, t, treeCap,
                RockOf(rockCnt, name), rockCap,
                BushOf(bushCnt, name), 650,
                FlowerOf(flowerCnt, name), 6800,
                MeadowOf(meadowCnt, name), 280));
        }

        static int NameIdx(string name)
        {
            switch (name)
            {
                case "East": return (int)NationType.East;
                case "West": return (int)NationType.West;
                case "South": return (int)NationType.South;
                case "North": return (int)NationType.North;
                default: return (int)NationType.East;
            }
        }
        static int RockOf(int[] a, string name) { return a[NameIdx(name)]; }
        static int BushOf(int[] a, string name) { return a[NameIdx(name)]; }
        static int FlowerOf(int[] a, string name) { return a[NameIdx(name)]; }
        static int MeadowOf(int[] a, string name) { return a[NameIdx(name)]; }

        /// <summary>배치 최종 트리/바위 위치 기반 결정론 해시 — 2부트 동일 여부 검증.</summary>
        static long HashTreeLayout(Transform root)
        {
            long h = 1469598103934665603L; // FNV offset basis
            for (int i = 0; i < root.childCount; i++)
            {
                var t = root.GetChild(i);
                Vector3 p = t.transform.position;
                h ^= (long)(uint)FloatBits(p.x);
                h *= 1099511628211L;
                h ^= (long)(uint)FloatBits(p.z);
                h *= 1099511628211L;
            }
            return h;
        }

        static uint FloatBits(float f)
        {
            return System.BitConverter.ToUInt32(System.BitConverter.GetBytes(f), 0);
        }

        // ================================================================
        // T-R4: 콜라이더 정책 (나무 몸통 + 대형 바위만)
        // ================================================================
        static void AddTreeCollider(GameObject go)
        {
            AddTrunkOrBox(go, 0.4f, 2.2f);
        }

        static void AddRockCollider(GameObject go)
        {
            AddTrunkOrBox(go, 0.55f, 1.6f);
        }

        static void AddTrunkOrBox(GameObject go, float radius, float vScale)
        {
            if (go == null) return;
            var existing = go.GetComponent<Collider>();
            if (existing != null) return;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = 2f * radius * vScale;
            capsule.center = new Vector3(0f, radius * vScale, 0f);
            capsule.isTrigger = true; // 스폰/이동 raycast와 충돌하지 않도록 (콜라이더는 네비/이동 블록 용도)
        }

        // ================================================================
        // Editor: 수동 실행 메뉴
        // ================================================================
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Poison/Place Idyllic DecoT-R4")]
        public static void PlaceIdyllicDecoEditor()
        {
            var deco = GameObject.Find("TerrainDeco");
            if (deco == null)
            {
                Debug.LogError("[IdyllicDecoPlacer] TerrainDeco 오브젝트가 없습니다 — 먼저 플레이 또는 TerrainDeco 생성 후 실행.");
                return;
            }
            PlaceAll(deco.transform, deco.transform);
            UnityEditor.EditorUtility.SetDirty(deco);
        }
#endif
    }
}