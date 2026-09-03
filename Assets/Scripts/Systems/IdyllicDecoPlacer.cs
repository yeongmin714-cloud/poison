using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Idyllic Fantasy Nature 프리팹(Resources/IdyllicPrefabs) 국가별 테마 절차 배치 (P-4).
    /// 기존 GLB 데코(TerrainModelPlacer)는 유지하고, 별도 부모 'IdyllicDeco' 아래 보강 배치한다.
    ///
    /// 테마 규칙 (사실주의 / 오버밀도 금지 / 판타지 색은 서·남만 은은히):
    ///   동(East)  초원  : Broadleaf_Green 숲 + Blossom 간간, 부시/들꽃/꽃밭, 바위 소량
    ///   북(North) 설원  : Fir(침엽) 은은 + Blossom 소량, 큰 바위 강조, 흰 꽃 극소
    ///   서(West)  사막  : Broadleaf_Purple/Red 희박한 관목수 + 바위 다수
    ///   남(South) 사막  : Broadleaf_Red 희박 + 바위 위주
    ///   중심(Empire)    : 나무 없음 — 55~110m 링에 바위/부시 최소(대리석 중심가)
    ///   호수 6개        : 물가 갈대(Cattail/Reeds) 밀집 + 수면 LilyPads/Waterlily + 물가 나무 소량
    ///
    /// 오버밀도 방지: 지터 그리드 후보 + 공간해시 최소 간격(나무-나무 9m 이상),
    /// 국가별 목표 상한(초과 배치 없음), 기존 GLB EnvironmentModels 위치와의 중첩 회피.
    /// 결정론: 고정 시드 System.Random + Fisher-Yates 셔플 (UnityEngine.Random 미사용).
    /// 착지: y = TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42) + 1f.
    /// 갈대는 지형 높이가 수면 위 0.1~2.4m 밴드(해안)에만 심는다 — 수면 아래/내륙 제외.
    /// </summary>
    public static class IdyllicDecoPlacer
    {
        // === 배치 상수 ===
        const string ROOT_NAME = "IdyllicDeco";
        const int SEED = 20260902;               // GLB 배치(20260901)와 다른 고정 시드
        const float GROUND_BASE = 1f;            // Ground_Inner 월드 y 기저 (GetHeightAt + 1f)
        const float BOUND_MAX = 950f;            // 지도 경계 (±950m)
        const float EMPIRE_TREE_EXCLUDE = 120f;  // 나무 제외 반경 (GLB 배치와 동일)
        const float SPAWN_X = 728f;
        const float SPAWN_Z = -529f;
        const float SPAWN_EXCLUDE = 6f;          // 스폰지 제외 반경

        // === 밀도 제어 ===
        const float TREE_CELL = 14f;             // 나무 후보 격자 (평균 간격 ≈ 14m → 8~15m 규칙)
        const float TREE_JITTER = 5.5f;          // 격자 지터 (최소 9m은 해시가 강제)
        const float TREE_MIN_DIST = 9f;          // 나무-나무 최소 간격
        const float PROP_CELL = 8f;              // 소형 프로프 후보 격자
        const float PROP_JITTER = 3.2f;
        const float BUSH_MIN_DIST = 6f;
        const float ROCK_MIN_DIST = 7f;
        const float FLOWER_MIN_DIST = 3.5f;
        const float MEADOW_MIN_DIST = 22f;
        const float TRUNK_CLEAR = 2.5f;          // 프로프-나무(줄기) 최소 거리
        const float REED_MIN_DIST = 2.2f;
        const float LILY_MIN_DIST = 3.5f;

        // === 호수 물가 밴드 ===
        const float LAKE_TREE_MARGIN = 1.2f;     // 일반 나무는 radius*1.2 밖에만
        const float LAKE_REED_IN = 0.97f;        // 갈대 밴드: radius*0.97 ~ radius*1.32
        const float LAKE_REED_OUT = 1.32f;
        const float LAKE_LILY_IN = 0.30f;        // 수련 밴드: radius*0.30 ~ radius*0.80
        const float LAKE_LILY_OUT = 0.80f;
        const float LAKE_TREE_IN = 1.24f;        // 물가 나무 밴드: radius*1.24 ~ radius*1.75
        const float LAKE_TREE_OUT = 1.75f;
        const int   REEDS_PER_LAKE = 46;         // 물가 갈대 목표 (밀집이되 해시 간격 유지)
        const int   LILIES_PER_LAKE = 8;
        const int   LAKE_TREES_PER_LAKE = 7;

        // === 국가별 목표 상한 (초과 배치 없음) ===
        const int TREE_CAP_EAST = 190;
        const int TREE_CAP_NORTH = 95;
        const int TREE_CAP_WEST = 38;
        const int TREE_CAP_SOUTH = 20;
        const int BUSH_CAP_EAST = 55,  BUSH_CAP_NORTH = 10, BUSH_CAP_WEST = 16, BUSH_CAP_SOUTH = 10;
        const int ROCK_CAP_EAST = 22,  ROCK_CAP_NORTH = 45, ROCK_CAP_WEST = 55, ROCK_CAP_SOUTH = 38;
        const int FLOWER_CAP_EAST = 70, FLOWER_CAP_NORTH = 20, FLOWER_CAP_WEST = 14, FLOWER_CAP_SOUTH = 8;
        const int MEADOW_CAP_EAST = 10, MEADOW_CAP_NORTH = 4, MEADOW_CAP_WEST = 3, MEADOW_CAP_SOUTH = 2;
        const int EMPIRE_RING_ROCKS = 10;        // 대리석 중심가 화분식 최소
        const int EMPIRE_RING_BUSHES = 8;

        /// <summary>
        /// 진입점. parent 하위 'IdyllicDeco'가 이미 있으면 스킵(중복 실행 가드).
        /// center: 배치 원점 앵커(보통 TerrainDeco, 월드 원점) / parent: IdyllicDeco 부모.
        /// </summary>
        public static void PlaceAll(Transform center, Transform parent)
        {
            if (parent == null) return;

            // 중복 실행 가드 — 'IdyllicDeco' 루트가 이미 있으면 스킵
            if (FindDirectChild(parent, ROOT_NAME) != null)
            {
                Debug.Log("[IdyllicDecoPlacer] Already placed — skipping.");
                return;
            }

            // === 프리팹 로드 (Resources/IdyllicPrefabs) ===
            var trees = LoadSet("IdyllicPrefabs/Trees");
            var rocks = LoadSet("IdyllicPrefabs/Rocks");
            var bushes = LoadSet("IdyllicPrefabs/Bushes");
            var shore = LoadSet("IdyllicPrefabs/Shore");
            var water = LoadSet("IdyllicPrefabs/Water");
            var flowers = LoadSet("IdyllicPrefabs/Flowers");
            var meadows = LoadSet("IdyllicPrefabs/Meadows");
            if (trees.Count == 0 || shore.Count == 0 || rocks.Count == 0)
            {
                Debug.LogError("[IdyllicDecoPlacer] Resources/IdyllicPrefabs 프리팹 없음 — 배치 생략.");
                return;
            }

            var broadGreen = Filter(trees, "BroadleafTree", "_Green");
            var broadPurple = Filter(trees, "BroadleafTree", "_Purple");
            var broadRed = Filter(trees, "BroadleafTree", "_Red");
            var blossom = Filter(trees, "BlossomTree", null);
            var fir = Filter(trees, "Fir_", null);
            var willowGreen = Filter(trees, "WillowTree", "_Green");
            var cattail = Filter(shore, "Cattail", null);
            var reeds = Filter(shore, "Reeds", null);
            var lilyPads = Filter(water, "LilyPads", null);
            var waterlily = Filter(water, "Waterlily", null);
            var rockBig = Filter(rocks, "Rock_Big", null);
            var rockMed = Filter(rocks, "Rock_Medium", null);
            var rockSmall = Filter(rocks, "Rock_Small", null);

            var rng = new System.Random(SEED);

            // === 루트/카테고리 부모 ===
            var root = new GameObject(ROOT_NAME);
            root.transform.SetParent(parent, false);
            root.layer = 0; // Default — 스폰 raycast(Ground|Terrain 마스크) 자동 무시
            var forestT = NewChild(root, "Forest");
            var rocksT = NewChild(root, "Rocks");
            var bushesT = NewChild(root, "Bushes");
            var flowersT = NewChild(root, "Flowers");
            var meadowsT = NewChild(root, "Meadows");
            var shoreT = NewChild(root, "Lakeshore");
            var waterT = NewChild(root, "WaterPlants");

            Vector3 origin = center != null ? center.position : Vector3.zero;

            // === 간격 해시 ===
            var treeHash = new SpatialHash(TREE_MIN_DIST);
            var propHash = new SpatialHash(PROP_CELL);
            var reedHash = new SpatialHash(REED_MIN_DIST);
            var lilyHash = new SpatialHash(LILY_MIN_DIST);

            // 기존 GLB 데코(EnvironmentModels)와 중첩 방지 — 위치만 해시에 선점
            BlockExistingEnvironment(parent, treeHash);

            // 국가별 배치 카운터 (인덱스 = (int)NationType)
            int[] treeCount = new int[8];
            int[] bushCount = new int[8];
            int[] rockCount = new int[8];
            int[] flowerCount = new int[8];
            int[] meadowCount = new int[8];

            // ── 1) 호수 물가 (갈대/수련/물가 나무) — 나무 해시 선점 ─────
            int reedsPlaced = 0, lilyPlaced = 0, lakeTreePlaced = 0;
            var lakes = TerrainGenerator.Lakes;
            for (int i = 0; i < lakes.Count; i++)
            {
                var lake = lakes[i];
                reedsPlaced += PlaceLakeshoreReeds(lake, cattail, reeds, shoreT,
                    reedHash, treeHash, rng, origin);
                lilyPlaced += PlaceLakeshoreLilies(lake, lilyPads, waterlily, waterT, lilyHash, rng);
                lakeTreePlaced += PlaceLakeshoreTrees(lake, willowGreen, broadGreen, forestT,
                    treeHash, propHash, rng);
            }

            // ── 2) 국가별 테마 숲 (지터 그리드 + 최소 간격) ─────────────
            int forestTreeTotal = PlaceNationTrees(origin, broadGreen, broadPurple, broadRed,
                blossom, fir, forestT, treeHash, treeCount, rng);

            // ── 3) 국가별 소형 프로프 (바위/부시/꽃/꽃밭) ───────────────
            int propTotal = PlaceNationProps(origin, bushes, rockBig, rockMed, rockSmall,
                flowers, meadows, rocksT, bushesT, flowersT, meadowsT,
                treeHash, propHash, bushCount, rockCount, flowerCount, meadowCount, rng);

            // ── 4) 중심(Empire) 대리석 중심가 — 바위/부시 최소 ──────────
            int empirePlaced = PlaceEmpireRing(origin, rockMed, rockSmall, bushes,
                rocksT, bushesT, treeHash, propHash, rng);

            // GPU Instancing 활성화 (공유 머티리얼)
            EnableGPUInstancing(root);

            Debug.Log($"[IdyllicDecoPlacer] Trees: E={treeCount[(int)NationType.East]}, N={treeCount[(int)NationType.North]}, " +
                      $"W={treeCount[(int)NationType.West]}, S={treeCount[(int)NationType.South]}, Lake={lakeTreePlaced} " +
                      $"| Rocks={Sum(rockCount)}, Bushes={Sum(bushCount)}, Flowers={Sum(flowerCount)}, " +
                      $"Meadows={Sum(meadowCount)}, Reeds={reedsPlaced}, WaterPlants={lilyPlaced}, EmpireRing={empirePlaced} " +
                      $"| ForestTrees={forestTreeTotal}, TotalPlaced={forestTreeTotal + propTotal + reedsPlaced + lilyPlaced + lakeTreePlaced + empirePlaced}");
        }

        // ================================================================
        // 1) 호수 물가
        // ================================================================

        /// <summary>
        /// 갈대/수초 — 해안 밴드(지형 높이가 수면 위 0.1~2.4m)에 클러스터 밀집.
        /// 수면 아래(y &lt; waterLevel+0.1)와 내륙(y &gt; waterLevel+2.4)은 제외.
        /// </summary>
        static int PlaceLakeshoreReeds(TerrainGenerator.TerrainLakeDef lake,
            List<GameObject> cattail, List<GameObject> reeds, Transform parent,
            SpatialHash reedHash, SpatialHash treeHash, System.Random rng, Vector3 origin)
        {
            int placed = 0;
            int attempts = REEDS_PER_LAKE * 5;
            for (int i = 0; i < attempts && placed < REEDS_PER_LAKE; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = lake.radius * Mathf.Lerp(LAKE_REED_IN, LAKE_REED_OUT, (float)rng.NextDouble());
                float x = lake.center.x + Mathf.Cos(ang) * d;
                float z = lake.center.z + Mathf.Sin(ang) * d;

                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                if (y < lake.waterLevel + 0.10f) continue;   // 수면 아래 — 금지 (메모: 호수 안 심음)
                if (y > lake.waterLevel + 2.4f) continue;    // 내륙 — 물가 아님

                var p = new Vector2(x, z);
                if (!reedHash.IsFree(p, REED_MIN_DIST)) continue;
                if (!treeHash.IsFree(p, 1.2f)) continue;     // 줄기 위 배제

                var model = (rng.Next(2) == 0 && cattail.Count > 0)
                    ? cattail[rng.Next(cattail.Count)]
                    : reeds[rng.Next(reeds.Count)];
                Place(model, x, y, z, RandomRange(rng, 0.8f, 1.35f), rng, parent);
                reedHash.Insert(p);
                placed++;
            }
            return placed;
        }

        /// <summary>수면 위 LilyPads/Waterlily — 충분히 깊은 수역(radius*0.3~0.8)에만.</summary>
        static int PlaceLakeshoreLilies(TerrainGenerator.TerrainLakeDef lake,
            List<GameObject> lilyPads, List<GameObject> waterlily, Transform parent,
            SpatialHash lilyHash, System.Random rng)
        {
            int placed = 0;
            int attempts = LILIES_PER_LAKE * 8;
            for (int i = 0; i < attempts && placed < LILIES_PER_LAKE; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = lake.radius * Mathf.Lerp(LAKE_LILY_IN, LAKE_LILY_OUT, (float)rng.NextDouble());
                float x = lake.center.x + Mathf.Cos(ang) * d;
                float z = lake.center.z + Mathf.Sin(ang) * d;

                // 수심 확인 — 지형이 수면 아래 0.25m 이상 가라앉은 곳만 (얕은 물/육지 제외)
                float terrainY = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                if (terrainY > lake.waterLevel - 0.25f) continue;

                var p = new Vector2(x, z);
                if (!lilyHash.IsFree(p, LILY_MIN_DIST)) continue;

                var model = (rng.Next(3) == 0 && waterlily.Count > 0)
                    ? waterlily[rng.Next(waterlily.Count)]
                    : lilyPads[rng.Next(lilyPads.Count)];
                Place(model, x, lake.waterLevel + 0.03f, z, RandomRange(rng, 0.85f, 1.2f), rng, parent);
                lilyHash.Insert(p);
                placed++;
            }
            return placed;
        }

        /// <summary>물가 나무 — 버드나무/활엽 혼합, radius*1.24~1.75 육지 밴드.</summary>
        static int PlaceLakeshoreTrees(TerrainGenerator.TerrainLakeDef lake,
            List<GameObject> willow, List<GameObject> broadleaf, Transform parent,
            SpatialHash treeHash, SpatialHash propHash, System.Random rng)
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
                if (y < lake.waterLevel + 0.8f) continue;    // 침수 위험 구간 제외

                var p = new Vector2(x, z);
                if (!treeHash.IsFree(p, TREE_MIN_DIST)) continue;
                if (!propHash.IsFree(p, ROCK_MIN_DIST)) continue;

                var model = (rng.NextDouble() < 0.6 && willow.Count > 0)
                    ? willow[rng.Next(willow.Count)]
                    : broadleaf[rng.Next(broadleaf.Count)];
                Place(model, x, y, z, RandomRange(rng, 0.85f, 1.15f), rng, parent);
                treeHash.Insert(p);
                placed++;
            }
            return placed;
        }

        // ================================================================
        // 2) 국가별 테마 숲
        // ================================================================

        /// <summary>
        /// 지터 그리드 후보를 셔플해 순회하며 국가별 상한/간격/제외존을 통과한 나무만 배치.
        /// 동: Broadleaf_Green 80% + Blossom 20% / 북: Fir 75% + Blossom 25%(설경 은은)
        /// 서: Broadleaf_Purple 60% + Red 40% (작게, 희박) / 남: Broadleaf_Red (가장 희박)
        /// </summary>
        static int PlaceNationTrees(Vector3 origin,
            List<GameObject> broadGreen, List<GameObject> broadPurple, List<GameObject> broadRed,
            List<GameObject> blossom, List<GameObject> fir,
            Transform parent, SpatialHash treeHash, int[] treeCount, System.Random rng)
        {
            var candidates = new List<Vector2>();
            float lim = BOUND_MAX - TREE_JITTER;
            for (float gx = -lim; gx <= lim; gx += TREE_CELL)
            {
                for (float gz = -lim; gz <= lim; gz += TREE_CELL)
                {
                    candidates.Add(new Vector2(
                        gx + RandomRange(rng, -TREE_JITTER, TREE_JITTER),
                        gz + RandomRange(rng, -TREE_JITTER, TREE_JITTER)));
                }
            }
            Shuffle(candidates, rng);

            int placed = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                float x = candidates[i].x;
                float z = candidates[i].y;
                if (!InBounds(x, z, origin)) continue;
                float dx = x - origin.x, dz = z - origin.z;
                if (dx * dx + dz * dz < EMPIRE_TREE_EXCLUDE * EMPIRE_TREE_EXCLUDE) continue; // 중심 도심 제외
                if (IsInSpawnExclusion(x, z)) continue;

                var nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                if (nation == NationType.None || nation == NationType.Empire || nation == NationType.Dracula) continue;
                if (treeCount[(int)nation] >= TreeCap(nation)) continue;

                // 호수 수면/해안 제외 (갈대·물가 나무 전용 구역)
                if (IsNearLakeWater(x, z, LAKE_TREE_MARGIN)) continue;

                var p = new Vector2(x, z);
                if (!treeHash.IsFree(p, TREE_MIN_DIST)) continue;

                GameObject model;
                float sMin, sMax;
                switch (nation)
                {
                    case NationType.East:   // 활엽 낙엽수 숲 + 벚꽃 간간
                        model = rng.NextDouble() < 0.80 ? broadGreen[rng.Next(broadGreen.Count)] : blossom[rng.Next(blossom.Count)];
                        sMin = 0.85f; sMax = 1.25f;
                        break;
                    case NationType.North:  // 설원 침엽 + 벚꽃 은은 (판타지 과잉 금지)
                        model = rng.NextDouble() < 0.75 ? fir[rng.Next(fir.Count)] : blossom[rng.Next(blossom.Count)];
                        sMin = 0.90f; sMax = 1.30f;
                        break;
                    case NationType.West:   // 사막 관목수 — 판타지 색 은은히, 작게
                        model = rng.NextDouble() < 0.60 ? broadPurple[rng.Next(broadPurple.Count)] : broadRed[rng.Next(broadRed.Count)];
                        sMin = 0.60f; sMax = 0.95f;
                        break;
                    default:                // South — 가장 희박한 사막 관목수
                        model = broadRed[rng.Next(broadRed.Count)];
                        sMin = 0.60f; sMax = 0.95f;
                        break;
                }

                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                Place(model, x, y, z, RandomRange(rng, sMin, sMax), rng, parent);
                treeHash.Insert(p);
                treeCount[(int)nation]++;
                placed++;
            }
            return placed;
        }

        // ================================================================
        // 3) 국가별 소형 프로프 (바위/부시/꽃/꽃밭)
        // ================================================================

        /// <summary>
        /// 후보 순회 → 국가별 우선순위(동: 꽃/부시 중심, 북·서·남: 바위 중심)로
        /// 상한·간격을 지키며 배치. 바위는 North/West에서 큰 바위 강조.
        /// </summary>
        static int PlaceNationProps(Vector3 origin,
            List<GameObject> bushes, List<GameObject> rockBig, List<GameObject> rockMed, List<GameObject> rockSmall,
            List<GameObject> flowers, List<GameObject> meadows,
            Transform rocksT, Transform bushesT, Transform flowersT, Transform meadowsT,
            SpatialHash treeHash, SpatialHash propHash,
            int[] bushCount, int[] rockCount, int[] flowerCount, int[] meadowCount, System.Random rng)
        {
            var candidates = new List<Vector2>();
            float lim = BOUND_MAX - PROP_JITTER;
            for (float gx = -lim; gx <= lim; gx += PROP_CELL)
            {
                for (float gz = -lim; gz <= lim; gz += PROP_CELL)
                {
                    candidates.Add(new Vector2(
                        gx + RandomRange(rng, -PROP_JITTER, PROP_JITTER),
                        gz + RandomRange(rng, -PROP_JITTER, PROP_JITTER)));
                }
            }
            Shuffle(candidates, rng);

            int placed = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                float x = candidates[i].x;
                float z = candidates[i].y;
                if (!InBounds(x, z, origin)) continue;
                if (IsInSpawnExclusion(x, z)) continue;

                var nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                if (nation == NationType.None || nation == NationType.Empire || nation == NationType.Dracula) continue;

                // 호수 해안 밴드는 갈대 전용 — 프로프 배제
                if (IsNearLakeWater(x, z, 1.1f)) continue;

                var p = new Vector2(x, z);
                if (!treeHash.IsFree(p, TRUNK_CLEAR)) continue;   // 줄기 중첩 방지

                // 국가별 타입 우선순위 (0=꽃, 1=부시, 2=바위, 3=꽃밭)
                for (int slot = 0; slot < 4; slot++)
                {
                    int type = PropOrder(nation, slot);
                    if (type == 0 && flowerCount[(int)nation] < FlowerCap(nation) && propHash.IsFree(p, FLOWER_MIN_DIST))
                    {
                        float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                        Place(flowers[rng.Next(flowers.Count)], x, y, z, RandomRange(rng, 0.85f, 1.2f), rng, flowersT);
                        propHash.Insert(p);
                        flowerCount[(int)nation]++;
                        placed++;
                        break;
                    }
                    if (type == 1 && bushCount[(int)nation] < BushCap(nation) && propHash.IsFree(p, BUSH_MIN_DIST))
                    {
                        float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                        Place(bushes[rng.Next(bushes.Count)], x, y, z, RandomRange(rng, 0.7f, 1.1f), rng, bushesT);
                        propHash.Insert(p);
                        bushCount[(int)nation]++;
                        placed++;
                        break;
                    }
                    if (type == 2 && rockCount[(int)nation] < RockCap(nation) && propHash.IsFree(p, ROCK_MIN_DIST))
                    {
                        var rock = PickRock(nation, rockBig, rockMed, rockSmall, rng);
                        float rMin = 0.75f, rMax = 1.25f;
                        if (nation == NationType.North || nation == NationType.West) { rMin += 0.35f; rMax += 0.4f; } // 설산/화산 큰 바위
                        float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                        Place(rock, x, y, z, RandomRange(rng, rMin, rMax), rng, rocksT);
                        propHash.Insert(p);
                        rockCount[(int)nation]++;
                        placed++;
                        break;
                    }
                    if (type == 3 && meadowCount[(int)nation] < MeadowCap(nation) && propHash.IsFree(p, MEADOW_MIN_DIST))
                    {
                        float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                        Place(meadows[rng.Next(meadows.Count)], x, y, z, RandomRange(rng, 0.9f, 1.3f), rng, meadowsT);
                        propHash.Insert(p);
                        meadowCount[(int)nation]++;
                        placed++;
                        break;
                    }
                }
            }
            return placed;
        }

        /// <summary>
        /// 중심(Empire) 대리석 중심가 — 나무 없이 바위/부시만 최소(55~110m 링).
        /// </summary>
        static int PlaceEmpireRing(Vector3 origin,
            List<GameObject> rockMed, List<GameObject> rockSmall, List<GameObject> bushes,
            Transform rocksT, Transform bushesT, SpatialHash treeHash, SpatialHash propHash, System.Random rng)
        {
            int placed = 0;
            int target = EMPIRE_RING_ROCKS + EMPIRE_RING_BUSHES;
            int attempts = target * 30;
            for (int i = 0; i < attempts && placed < target; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = RandomRange(rng, 55f, 110f);
                float x = origin.x + Mathf.Cos(ang) * d;
                float z = origin.z + Mathf.Sin(ang) * d;
                if (!InBounds(x, z, origin)) continue;
                if (IsInSpawnExclusion(x, z)) continue;

                var p = new Vector2(x, z);
                if (!treeHash.IsFree(p, TRUNK_CLEAR) || !propHash.IsFree(p, BUSH_MIN_DIST)) continue;

                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                if (placed % 2 == 0)
                {
                    var rock = (rng.Next(2) == 0 && rockMed.Count > 0)
                        ? rockMed[rng.Next(rockMed.Count)]
                        : rockSmall[rng.Next(rockSmall.Count)];
                    Place(rock, x, y, z, RandomRange(rng, 0.6f, 0.95f), rng, rocksT); // 화분식 소형
                }
                else
                {
                    Place(bushes[rng.Next(bushes.Count)], x, y, z, RandomRange(rng, 0.65f, 0.9f), rng, bushesT);
                }
                propHash.Insert(p);
                placed++;
            }
            return placed;
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        static int TreeCap(NationType n)
        {
            switch (n)
            {
                case NationType.East: return TREE_CAP_EAST;
                case NationType.North: return TREE_CAP_NORTH;
                case NationType.West: return TREE_CAP_WEST;
                case NationType.South: return TREE_CAP_SOUTH;
                default: return 0;
            }
        }

        static int RockCap(NationType n)
        {
            switch (n)
            {
                case NationType.East: return ROCK_CAP_EAST;
                case NationType.North: return ROCK_CAP_NORTH;
                case NationType.West: return ROCK_CAP_WEST;
                case NationType.South: return ROCK_CAP_SOUTH;
                default: return 0;
            }
        }

        static int BushCap(NationType n)
        {
            switch (n)
            {
                case NationType.East: return BUSH_CAP_EAST;
                case NationType.North: return BUSH_CAP_NORTH;
                case NationType.West: return BUSH_CAP_WEST;
                case NationType.South: return BUSH_CAP_SOUTH;
                default: return 0;
            }
        }

        static int FlowerCap(NationType n)
        {
            switch (n)
            {
                case NationType.East: return FLOWER_CAP_EAST;
                case NationType.North: return FLOWER_CAP_NORTH;
                case NationType.West: return FLOWER_CAP_WEST;
                case NationType.South: return FLOWER_CAP_SOUTH;
                default: return 0;
            }
        }

        static int MeadowCap(NationType n)
        {
            switch (n)
            {
                case NationType.East: return MEADOW_CAP_EAST;
                case NationType.North: return MEADOW_CAP_NORTH;
                case NationType.West: return MEADOW_CAP_WEST;
                case NationType.South: return MEADOW_CAP_SOUTH;
                default: return 0;
            }
        }

        /// <summary>국가별 프로프 타입 우선순위 (0=꽃, 1=부시, 2=바위, 3=꽃밭).</summary>
        static int PropOrder(NationType n, int slot)
        {
            // 사실주의: 초원(East)은 초화/부시 중심, 건조/설원(N·W·S)은 바위 중심.
            switch (n)
            {
                case NationType.East:
                    switch (slot) { case 0: return 0; case 1: return 1; case 2: return 2; default: return 3; }
                case NationType.North:
                    switch (slot) { case 0: return 2; case 1: return 0; case 2: return 1; default: return 3; }
                default: // West, South — 바위 우선
                    switch (slot) { case 0: return 2; case 1: return 1; case 2: return 0; default: return 3; }
            }
        }

        static GameObject PickRock(NationType n, List<GameObject> big, List<GameObject> med, List<GameObject> small, System.Random rng)
        {
            double r = rng.NextDouble();
            bool hasBig = big.Count > 0, hasMed = med.Count > 0;
            // 북(설원)/서(화산): 큰 바위 강조 / 동: 중·소형 위주 / 남: 중형+소형
            if (n == NationType.North || n == NationType.West)
            {
                if (r < 0.45 && hasBig) return big[rng.Next(big.Count)];
                return r < 0.85 && hasMed ? med[rng.Next(med.Count)] : small[rng.Next(small.Count)];
            }
            if (r < 0.30 && hasBig) return big[rng.Next(big.Count)];
            return r < 0.65 && hasMed ? med[rng.Next(med.Count)] : small[rng.Next(small.Count)];
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
            float dx = x - SPAWN_X;
            float dz = z - SPAWN_Z;
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

        static void Place(GameObject model, float x, float y, float z, float scale, System.Random rng, Transform parent)
        {
            var go = Object.Instantiate(model, parent);
            go.layer = 0; // Default — 스폰 raycast(Ground|Terrain 마스크) 자동 무시
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            go.transform.localScale = Vector3.one * scale;
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

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        static float RandomRange(System.Random rng, float min, float max)
        {
            return (float)(rng.NextDouble() * (max - min) + min);
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

        /// <summary>기존 GLB 데코(EnvironmentModels) 위치를 나무 해시에 선점해 중첩 방지.</summary>
        static void BlockExistingEnvironment(Transform parent, SpatialHash treeHash)
        {
            var env = FindDirectChild(parent, "EnvironmentModels");
            if (env == null) env = GameObject.Find("EnvironmentModels");
            if (env == null) return;
            var t = env.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c == null) continue;
                treeHash.Insert(new Vector2(c.position.x, c.position.z));
            }
            Debug.Log($"[IdyllicDecoPlacer] 기존 GLB 데코 {t.childCount}개 위치를 간격 해시에 반영.");
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
        // Editor: 수동 실행 메뉴
        // ================================================================
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Poison/Place Idyllic Deco")]
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
