using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Heightmap 지형에 UserProvided GLB 환경 모델을 GPU Instancing으로 배치
    /// 3링(거리 기반) + 국가별 텍스처 구역 + 국가 경계 블렌딩 존 지원
    /// </summary>
    public static class TerrainModelPlacer
    {
        // Ring 정의: 거리 범위 + 모델 개수 + 스케일 범위
        static readonly RingConfig[] RingConfigs = new RingConfig[]
        {
            new RingConfig { innerRadius = 0f,   outerRadius = 350f,  grassCount = 500, treeCount = 80, rockCount = 100, grassScale = 0.8f, treeScale = 1f, rockScale = 1.2f },
            new RingConfig { innerRadius = 350f, outerRadius = 700f,  grassCount = 300, treeCount = 50, rockCount = 60,  grassScale = 0.9f, treeScale = 1.1f, rockScale = 1.3f },
            new RingConfig { innerRadius = 700f, outerRadius = 1000f, grassCount = 150, treeCount = 30, rockCount = 40,  grassScale = 1f,   treeScale = 1.2f, rockScale = 1.5f },
        };

        struct RingConfig
        {
            public float innerRadius;
            public float outerRadius;
            public int grassCount;
            public int treeCount;
            public int rockCount;
            public float grassScale;
            public float treeScale;
            public float rockScale;
        }

        // 국가별 모델 프리픽스 매핑
        static readonly Dictionary<string, string[]> NationGrassPrefix = new()
        {
            { "East",  new[] { "east_grass" } },
            { "West",  new[] { "west_grass" } },
            { "South", new[] { "south_grass" } },
            { "North", new[] { "north_grass" } },
            { "Empire", new[] { "empire_grass" } }
        };

        public static void Place(GameObject ground)
        {
            if (ground == null) return;

            var groundCollider = ground.GetComponent<MeshCollider>();
            if (groundCollider == null) return;

            var envParent = new GameObject("Environment");
            envParent.transform.SetParent(ground.transform);

            // GLB 모델 로드
            var grassModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/grass");
            var rockModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/rocks");
            var treeModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/trees");

            // 국가별 텍스처 컨트롤러 가져오기
                        var nationController = ground.GetComponent<ProjectName.Systems.NationTerrainController>();
                        string currentNation = nationController != null ? nationController.CurrentNation.ToString() : "East";

                        // 현재 바이옴 가져오기 (지형 생성에 사용된 것과 동일)
                        var biome = ProjectName.Core.Data.BiomeType.Plains;
                        int terrainSeed = 42;

                        // DEBUG: Log loaded models
                        Debug.Log($"[TerrainModelPlacer] grassModels: {grassModels.Length}, rockModels: {rockModels.Length}, treeModels: {treeModels.Length}");
                        for (int i = 0; i < grassModels.Length; i++) Debug.Log($"  grass[{i}]: {grassModels[i].name}");
                        for (int i = 0; i < rockModels.Length; i++) Debug.Log($"  rock[{i}]: {rockModels[i].name}");
                        for (int i = 0; i < treeModels.Length; i++) Debug.Log($"  tree[{i}]: {treeModels[i].name}");

                        if (grassModels.Length == 0 || rockModels.Length == 0 || treeModels.Length == 0)
                        {
                            Debug.LogError("[TerrainModelPlacer] GLB terrain models not found in Resources/Models/UserProvided/terrain/");
                            return;
                        }

                        Debug.Log($"[TerrainModelPlacer] Current nation: {currentNation}, Biome: {biome}, Seed: {terrainSeed}");

                        // 링별 배치
                        int totalPlaced = 0;
                        foreach (var ring in RingConfigs)
                        {
                            int placed = PlaceModelsInRing(envParent, groundCollider, grassModels, rockModels, treeModels, ring, currentNation, biome, terrainSeed);
                            totalPlaced += placed;
                        }

                        Debug.Log($"[TerrainModelPlacer] Total models placed: {totalPlaced}, Environment children: {envParent.transform.childCount}");

                        // 국가 경계 블렌딩 존 (선택적 - NationTerrainController가 처리)

                        // GPU Instancing 활성화
                        EnableGPUInstancing(envParent);

                        Debug.Log($"[TerrainModelPlacer] Environment placement complete. Children: {envParent.transform.childCount}");
        }

        static int PlaceModelsInRing(GameObject parent, MeshCollider groundCollider,
            GameObject[] grassModels, GameObject[] rockModels, GameObject[] treeModels,
            RingConfig ring, string nation, BiomeType biome, int seed)
        {
            int placed = 0;
            // 국가별 잔디 모델 필터링
            var nationGrass = FilterModelsByNation(grassModels, nation, "grass");

            // 잔디 배치
            placed += PlaceModelsInstanced(parent, nationGrass.Length > 0 ? nationGrass : grassModels,
                ring.innerRadius, ring.outerRadius, ring.grassCount, 0.05f, 0.2f, ring.grassScale, biome, seed);

            // 나무 배치
            placed += PlaceModelsInstanced(parent, treeModels,
                ring.innerRadius, ring.outerRadius, ring.treeCount, 0f, 0f, ring.treeScale, biome, seed);

            // 바위 배치
            placed += PlaceModelsInstanced(parent, rockModels,
                ring.innerRadius, ring.outerRadius, ring.rockCount, 0f, 0.1f, ring.rockScale, biome, seed);

            return placed;
        }

        static GameObject[] FilterModelsByNation(GameObject[] models, string nation, string type)
        {
            var prefixList = new List<string>();
            if (NationGrassPrefix.TryGetValue(nation, out var prefixes))
            {
                prefixList.AddRange(prefixes);
            }

            var filtered = new List<GameObject>();
            foreach (var m in models)
            {
                foreach (var prefix in prefixList)
                {
                    if (m.name.StartsWith(prefix))
                    {
                        filtered.Add(m);
                        break;
                    }
                }
            }
            return filtered.Count > 0 ? filtered.ToArray() : models;
        }

        static int PlaceModelsInstanced(GameObject parent,
            GameObject[] models, float innerR, float outerR, int count,
            float yMinOffset, float yMaxOffset, float baseScale, BiomeType biome, int seed)
        {
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                for (int attempts = 0; attempts < 5; attempts++)
                {
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float radius = Random.Range(innerR, outerR);
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;

                    // Perlin Noise로 높이 직접 샘플링 (Raycast 대신)
                    float height = ProjectName.Systems.TerrainGenerator.GetHeightAt(x, z, biome, seed);
                    float y = height + Random.Range(yMinOffset, yMaxOffset);

                    var model = models[Random.Range(0, models.Length)];
                    var go = Object.Instantiate(model, parent.transform);
                    go.transform.position = new Vector3(x, y, z);
                    go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    go.transform.localScale *= Random.Range(baseScale * 0.8f, baseScale * 1.2f);

                    // LODGroup 추가 (거리별 컬링)
                    var lodGroup = go.AddComponent<LODGroup>();
                    var renderers = go.GetComponentsInChildren<Renderer>();
                    var lods = new LOD[]
                    {
                        new LOD(0.6f, renderers), // 0-60% 거리: 고품질
                        new LOD(0.3f, new Renderer[0]), // 60-30%: 중간
                        new LOD(0.0f, new Renderer[0])  // 30%+: 컬링
                    };
                    lodGroup.SetLODs(lods);
                    lodGroup.RecalculateBounds();

                    placed++;
                    break;
                }
            }
            return placed;
        }

        static void EnableGPUInstancing(GameObject parent)
        {
            var renderers = parent.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.sharedMaterial != null)
                {
                    r.sharedMaterial.enableInstancing = true;
                }
            }
        }

        // ================================================================
        // Editor: 수동 실행 메뉴
        // ================================================================
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Poison/Place Environment Models")]
        public static void PlaceEnvironmentModelsEditor()
        {
            var ground = GameObject.Find("Ground_Inner");
            if (ground != null)
            {
                Place(ground);
                UnityEditor.EditorUtility.SetDirty(ground);
            }
        }
#endif
    }
}