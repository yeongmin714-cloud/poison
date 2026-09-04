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
        const float BOUND_MAX = 950f;
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
        const float FLOWER_CELL = 4.5f;
        const float FLOWER_MIN_DIST = 3.5f;
        const float MEADOW_SPACING = 26f;
        const float MEADOW_JITTER = 8f;
        const float MEADOW_MIN_DIST = 22f;
        const float TRUNK_CLEAR = 2.5f;

        const float FLOWER_MASK_HI = 0.60f;
        const float FANTASY_MASK_HI = 0.50f;

        const float LAKE_TREE_MARGIN = 1.2f;
        const float LAKE_SHORE_IN = 0.97f;
        const float LAKE_SHORE_OUT = 1.20f;
        const float LAKE_LILY_IN = 0.30f;
        const float LAKE_LILY_OUT = 0.80f;
        const float LAKE_TREE_IN = 1.24f;
        const float LAKE_TREE_OUT = 1.75f;
        const int REEDS_PER_LAKE = 46;
        const int LILIES_PER_LAKE = 8;
        const int LAKE_TREES_PER_LAKE = 7;

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
            public List<GameObject> bushes;
            public List<GameObject> rockBig, rockMed, rockSmall;
            public List<GameObject> cattail, reeds, lilyPads, waterLily;
            public List<GameObject> flowerYellow, flowerWhite, flowerRed, flowerPurple, flowerPink, flowerBlue;
            public List<GameObject> meadowWhite, meadowRed, meadowRedOrange, meadowPurple, meadowPink, meadowBlue;
        }

        static int NationSeed(NationType n) { return T_R4_BASE + (int)n * 1000; }

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
            var lakes = TerrainGenerator.Lakes;
            if (lakes != null)
            {
                for (int i = 0; i < lakes.Count; i++)
                {
                    var lk = lakes[i];
                    reedsPlaced += PlaceLakeshoreReeds(lk, cat, shoreT, treeHash, lakeRng);
                    lilyPlaced += PlaceLakeshoreLilies(lk, cat, waterT, lakeRng);
                    lakeTreePlaced += PlaceLakeshoreTrees(lk, cat, forestT, treeHash, propHash, lakeRng);
                }
            }

            int[] treeCnt = new int[8], rockCnt = new int[8], bushCnt = new int[8];
            int[] flowerCnt = new int[8], meadowCnt = new int[8];
            for (int i = 0; i < profiles.Length; i++)
            {
                PlaceNation(profiles[i], cat, origin,
                    forestT, rocksT, bushesT, flowersT, meadowsT,
                    treeHash, propHash, treeCnt, rockCnt, bushCnt, flowerCnt, meadowCnt);
            }

            var empireRng = new System.Random(NationSeed(NationType.Empire));
            int empirePlaced = PlaceEmpireGarden(origin, cat, forestT, bushesT, treeHash, propHash, empireRng);

            EnableGPUInstancing(root);

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
            Debug.Log("[IdyllicDecoPlacer][T-R4] Deterministic seed = 20260904+nationId*1000. LayoutHash for 2-boot compare (same seed->same hash).");
            Debug.Log("[IdyllicDecoPlacer][T-R4] Culling radii (no existing group - log only): tree 150m / rock 200m / grass-flower-bush 60m.");
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
                    // Z4: 숲 군락 여부 (군락 내 나무 밀도 ×4 = 2×2 서브그리드)
                    float fx = gx, fz = gz;
                    bool forest = TerrainShape.GetForestPatchMask(fx, fz, p.nation, T_R4_BASE) > 0.50f;
                    int subs = forest ? 2 : 1;
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
            if (IsNearLakeWater(x, z, LAKE_TREE_MARGIN)) return false;
            // Z3: 경사 30° 초과 지점 데코 배치 스킵 (절벽 위 나무 금지 — 자연 지면 스냅 유지)
            if (TerrainSplatBaker.EstimateSlopeDegrees(x, z) > 30f) return false;
            var p2 = new Vector2(x, z);
            if (!treeHash.IsFree(p2, TREE_MIN_DIST)) return false;
            float sub = TerrainShape.GetFantasySubzoneMask(x, z, p.nation, T_R4_BASE);
            WPrefab entry = PickWeighted(
                (sub > FANTASY_MASK_HI && p.fantasyTrees != null && p.fantasyTrees.Count > 0)
                    ? p.fantasyTrees : p.trees, rng);
            float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
            GameObject go = Place(entry.prefab, x, y, z, RandomRange(rng, entry.scaleMin, entry.scaleMax), rng, parent);
            if (entry.collider) AddTreeCollider(go);
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
                    var p2 = new Vector2(x, z);
                    if (!treeHash.IsFree(p2, TRUNK_CLEAR)) continue;
                    if (!propHash.IsFree(p2, ROCK_MIN_DIST)) continue;
                    WPrefab entry = PickWeighted(p.rocks, rng);
                    float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                    GameObject go = Place(entry.prefab, x, y, z, RandomRange(rng, entry.scaleMin, entry.scaleMax), rng, parent);
                    if (entry.collider) AddRockCollider(go);
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
                    if (TerrainShape.GetFlowerPatchMask(x, z) < FLOWER_MASK_HI) continue;
                    if (flowerCnt[(int)nation] >= p.flowerCap) continue;
                    if (IsNearLakeWater(x, z, 1.05f)) continue;
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
            p.treeCap = 1900;
            p.rockCap = 400;
            p.bushCap = 650;
            p.flowerCap = 4200;
            p.meadowCap = 220;
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
                    AddPool(p.flowers, cat.flowerPink, 1f, 0.8f, 1.2f, false);
                    AddPool(p.flowers, cat.flowerWhite, 1f, 0.8f, 1.2f, false);
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
                FlowerOf(flowerCnt, name), 4200,
                MeadowOf(meadowCnt, name), 220));
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