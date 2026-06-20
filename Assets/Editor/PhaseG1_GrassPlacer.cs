#if false
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using ProjectName.Systems;
using ProjectName.Core;

namespace ProjectName.Editor
{
    /// <summary>
    /// Phase G1-04: Grass/Detail Vegetation System editor placement.
    /// Scans terrain for biome regions and places 5000~10000 grass blades
    /// with biome-appropriate density and color.
    /// Menu: Tools/Phase G1/Place Grass
    /// </summary>
    public static class PhaseG1_GrassPlacer
    {
        private const string GrassSystemName = "GrassSystem";
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string GroundName = "Ground";
        private const int GrassSeed = 4242;

        // Target blade counts per biome
        private static readonly Dictionary<BiomeType, int> BiomeDensity = new Dictionary<BiomeType, int>
        {
            { BiomeType.Plains, 2000 },     // Grassland = dense green
            { BiomeType.Forest, 1200 },      // Forest = medium
            { BiomeType.Lake, 200 },
            { BiomeType.Rocky, 300 },
            { BiomeType.Swamp, 1000 },       // Swamp = dark green
            { BiomeType.Desert, 400 },       // Desert = sparse yellow
            { BiomeType.Volcanic, 100 },
            { BiomeType.Tundra, 500 },
            { BiomeType.Mountain, 300 },
            { BiomeType.Empire, 600 },
            { BiomeType.Reed, 800 }
        };

        // Grass colors per biome
        private static readonly Dictionary<BiomeType, Color> BiomeGrassColor = new Dictionary<BiomeType, Color>
        {
            { BiomeType.Plains,    new Color(0.2f, 0.7f, 0.15f) },  // 밝은 녹색
            { BiomeType.Forest,    new Color(0.15f, 0.5f, 0.1f) },  // 중간 짙은 녹색
            { BiomeType.Lake,      new Color(0.3f, 0.6f, 0.25f) },  // 습지 녹색
            { BiomeType.Rocky,     new Color(0.4f, 0.5f, 0.3f) },  // 회갈색
            { BiomeType.Swamp,     new Color(0.1f, 0.35f, 0.1f) },  // 짙은 녹색
            { BiomeType.Desert,    new Color(0.7f, 0.65f, 0.3f) },  // 노란색
            { BiomeType.Volcanic,  new Color(0.3f, 0.15f, 0.1f) },  // 붉은 갈색
            { BiomeType.Tundra,    new Color(0.6f, 0.7f, 0.6f) },   // 회백색
            { BiomeType.Mountain,  new Color(0.4f, 0.5f, 0.35f) },  // 산악 회녹색
            { BiomeType.Empire,    new Color(0.5f, 0.7f, 0.2f) },   // 황금 녹색
            { BiomeType.Reed,      new Color(0.8f, 0.7f, 0.2f) }    // 갈대 노랑
        };

        // Wind settings per biome
        private static readonly Dictionary<BiomeType, (float speed, float amount)> BiomeWind =
            new Dictionary<BiomeType, (float, float)>
        {
            { BiomeType.Plains,    (1.5f, 7f) },
            { BiomeType.Forest,    (1.0f, 3f) },
            { BiomeType.Lake,      (1.2f, 5f) },
            { BiomeType.Rocky,     (1.8f, 4f) },
            { BiomeType.Swamp,     (0.8f, 2f) },
            { BiomeType.Desert,    (2.0f, 6f) },
            { BiomeType.Volcanic,  (1.0f, 3f) },
            { BiomeType.Tundra,    (2.5f, 8f) },
            { BiomeType.Mountain,  (2.0f, 6f) },
            { BiomeType.Empire,    (1.2f, 4f) },
            { BiomeType.Reed,      (1.5f, 5f) }
        };

        // ================================================================
        //  Place Grass Menu
        // ================================================================

        [MenuItem("Tools/Phase G1/Place Grass")]
        public static void PlaceGrass()
        {
            // Open MainScene
            var scene = GetOrOpenMainScene();
            if (scene == null)
            {
                Debug.LogError("[PhaseG1_GrassPlacer] MainScene not found and could not be opened.");
                EditorUtility.DisplayDialog("Phase G1-04", "MainScene not found.", "OK");
                return;
            }

            // Find or validate Ground
            GameObject ground = GameObject.Find(GroundName);
            if (ground == null)
            {
                Debug.LogError($"[PhaseG1_GrassPlacer] '{GroundName}' not found in scene.");
                EditorUtility.DisplayDialog("Phase G1-04", $"'{GroundName}' not found.", "OK");
                return;
            }

            // Create or clear GrassSystem parent
            GameObject grassSystem = GameObject.Find(GrassSystemName);
            if (grassSystem != null)
            {
                // Remove existing GrassRenderer children
                var existingRenderers = grassSystem.GetComponentsInChildren<GrassRenderer>();
                foreach (var gr in existingRenderers)
                {
                    Object.DestroyImmediate(gr.gameObject);
                }
            }
            else
            {
                grassSystem = new GameObject(GrassSystemName);
            }

            // Create shared grass material
            Material grassMaterial = MaterialHelper.CreateLitMaterial(
                Color.white, "Grass_Instanced_Mat");
            if (grassMaterial == null)
            {
                Debug.LogError("[PhaseG1_GrassPlacer] Failed to create grass material.");
                EditorUtility.DisplayDialog("Phase G1-04", "Failed to create grass material.", "OK");
                return;
            }
            grassMaterial.enableInstancing = true;

            // Create 3 grass blade meshes (procedural quads)
            Mesh meshStraight = CreateGrassBladeMesh("GrassBlade_Straight", 0f);
            Mesh meshBentLeft = CreateGrassBladeMesh("GrassBlade_BentLeft", -8f);
            Mesh meshBentRight = CreateGrassBladeMesh("GrassBlade_BentRight", 8f);

            // Scan terrain and place grass per biome
            int totalPlaced = 0;
            int rendererCount = 0;

            foreach (BiomeType biome in System.Enum.GetValues(typeof(BiomeType)))
            {
                int targetCount = BiomeDensity.ContainsKey(biome) ? BiomeDensity[biome] : 500;
                if (targetCount <= 0) continue;

                // Generate sample positions for this biome
                List<Vector3> positions = SamplePositionsForBiome(ground, biome, targetCount);
                if (positions.Count == 0) continue;

                // Create GrassRenderer for this biome
                GameObject grGo = new GameObject($"Grass_{biome}");
                grGo.transform.SetParent(grassSystem.transform);

                GrassRenderer gr = grGo.AddComponent<GrassRenderer>();
                gr.SetMeshes(meshStraight, meshBentLeft, meshBentRight);
                gr.SetMaterial(grassMaterial);
                gr.SetBaseColor(BiomeGrassColor.ContainsKey(biome)
                    ? BiomeGrassColor[biome]
                    : new Color(0.2f, 0.7f, 0.15f));
                gr.SetColorVariation(0.1f);

                if (BiomeWind.ContainsKey(biome))
                {
                    gr.SetWind(BiomeWind[biome].speed, BiomeWind[biome].amount);
                }

                // Set material color on property block directly via shared material tint
                // (actual per-instance color modulation is done by the material's vertex color)
                grassMaterial.color = BiomeGrassColor.ContainsKey(biome)
                    ? BiomeGrassColor[biome]
                    : new Color(0.2f, 0.7f, 0.15f);

                gr.PlaceBlades(positions, GrassSeed + (int)biome);

                totalPlaced += positions.Count;
                rendererCount++;

                Debug.Log($"[PhaseG1_GrassPlacer] Placed {positions.Count} grass blades for biome '{biome}'.");
            }

            // Save scene
            EditorSceneManager.SaveScene(scene, scene.path);

            string summary = $"Grass placement complete.\n" +
                             $"{totalPlaced} blades placed across {rendererCount} biome renderers.\n" +
                             $"Parent: {GrassSystemName}";
            Debug.Log($"[PhaseG1_GrassPlacer] ✅ {summary}");
            EditorUtility.DisplayDialog("Phase G1-04", summary, "OK");
        }

        [MenuItem("Tools/Phase G1/Place Grass", true)]
        private static bool ValidatePlaceGrass() => true;

        // ================================================================
        //  Terrain Scanning
        // ================================================================

        /// <summary>
        /// Samples random positions on the terrain within the area roughly
        /// associated with a given biome.
        /// Uses the terrain's MeshCollider for raycasting.
        /// </summary>
        private static List<Vector3> SamplePositionsForBiome(
            GameObject ground, BiomeType biome, int count)
        {
            List<Vector3> positions = new List<Vector3>(count);

            MeshCollider meshCol = ground.GetComponent<MeshCollider>();
            if (meshCol == null || meshCol.sharedMesh == null)
            {
                Debug.LogWarning($"[PhaseG1_GrassPlacer] Ground has no MeshCollider. " +
                    $"Falling back to flat placement at y=0.");
                return SampleFlatPositions(count, ground.transform);
            }

            // Determine sampling region based on biome
            // We use deterministic pseudo-random position sampling
            int biomeHash = (int)biome * 1000 + GrassSeed;
            System.Random rng = new System.Random(biomeHash);

            // Try to place grass within a reasonable area
            // Interpret biome region by the world quadrant
            float terrainHalfSize = 500f; // half of 1000m terrain
            Vector3 regionCenter;
            float regionRadius;

            switch (biome)
            {
                case BiomeType.Plains:
                case BiomeType.Forest:
                    regionCenter = new Vector3(terrainHalfSize * 0.5f, 0f, terrainHalfSize * 0.5f);
                    regionRadius = terrainHalfSize * 0.7f;
                    break;
                case BiomeType.Swamp:
                case BiomeType.Rocky:
                case BiomeType.Reed:
                    regionCenter = new Vector3(-terrainHalfSize * 0.5f, 0f, terrainHalfSize * 0.5f);
                    regionRadius = terrainHalfSize * 0.7f;
                    break;
                case BiomeType.Desert:
                case BiomeType.Volcanic:
                    regionCenter = new Vector3(0f, 0f, -terrainHalfSize * 0.5f);
                    regionRadius = terrainHalfSize * 0.7f;
                    break;
                case BiomeType.Tundra:
                case BiomeType.Mountain:
                    regionCenter = new Vector3(0f, 0f, terrainHalfSize * 0.5f);
                    regionRadius = terrainHalfSize * 0.7f;
                    break;
                case BiomeType.Empire:
                    regionCenter = Vector3.zero;
                    regionRadius = terrainHalfSize * 0.4f;
                    break;
                case BiomeType.Lake:
                    regionCenter = new Vector3(0f, 0f, 0f);
                    regionRadius = terrainHalfSize * 0.3f;
                    break;
                default:
                    regionCenter = Vector3.zero;
                    regionRadius = terrainHalfSize;
                    break;
            }

            int maxAttempts = count * 10;
            int attempts = 0;

            while (positions.Count < count && attempts < maxAttempts)
            {
                attempts++;

                // Random position within region
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float radius = (float)(rng.NextDouble() * regionRadius);
                float x = regionCenter.x + Mathf.Cos(angle) * radius;
                float z = regionCenter.z + Mathf.Sin(angle) * radius;

                // Raycast down onto terrain
                Vector3 rayOrigin = new Vector3(x, 200f, z);
                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 250f))
                {
                    // Jitter the y slightly above surface for natural look
                    Vector3 pos = hit.point;
                    pos.y += 0.01f + (float)(rng.NextDouble() * 0.05f);
                    positions.Add(pos);
                }
            }

            return positions;
        }

        /// <summary>
        /// Fallback: sample positions on a flat plane when no MeshCollider exists.
        /// </summary>
        private static List<Vector3> SampleFlatPositions(int count, Transform groundTransform)
        {
            List<Vector3> positions = new List<Vector3>(count);
            System.Random rng = new System.Random(GrassSeed);

            float halfSize = 500f; // Scale matches typical Ground scale

            for (int i = 0; i < count; i++)
            {
                float x = (float)((rng.NextDouble() - 0.5) * halfSize * 2f);
                float z = (float)((rng.NextDouble() - 0.5) * halfSize * 2f);
                positions.Add(new Vector3(x, 0f, z));
            }

            return positions;
        }

        // ================================================================
        //  Procedural Grass Blade Mesh Creation
        // ================================================================

        /// <summary>
        /// Creates a simple grass blade mesh (3-vertex quad/triangle with a slight bend).
        /// bendAngle: 0 for straight, negative for left bend, positive for right bend.
        /// </summary>
        private static Mesh CreateGrassBladeMesh(string name, float bendAngle)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;

            // Grass blade: ~0.3m tall, ~0.05m wide
            float height = 0.3f;
            float halfWidth = 0.025f;

            // 4 vertices forming a quad (tall narrow rectangle)
            Vector3[] verts;
            Vector2[] uv;
            int[] tris;
            Vector3[] normals;

            if (Mathf.Abs(bendAngle) < 0.5f)
            {
                // Straight blade: two triangles forming a quad
                verts = new Vector3[]
                {
                    new Vector3(-halfWidth, 0f, 0f),  // bottom-left
                    new Vector3( halfWidth, 0f, 0f),  // bottom-right
                    new Vector3(-halfWidth * 0.5f, height, 0f),  // top-left (narrower)
                    new Vector3( halfWidth * 0.5f, height, 0f)   // top-right (narrower)
                };
                uv = new Vector2[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                };
                tris = new int[] { 0, 1, 2, 1, 3, 2 };
            }
            else
            {
                // Bent blade: top vertices shifted along Z
                float bendOffset = Mathf.Sin(bendAngle * Mathf.Deg2Rad) * height * 0.3f;
                float tipZ = bendOffset;

                verts = new Vector3[]
                {
                    new Vector3(-halfWidth, 0f, 0f),
                    new Vector3( halfWidth, 0f, 0f),
                    new Vector3(-halfWidth * 0.5f, height, tipZ),
                    new Vector3( halfWidth * 0.5f, height, tipZ)
                };
                uv = new Vector2[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                };
                tris = new int[] { 0, 1, 2, 1, 3, 2 };
            }

            normals = new Vector3[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward
            };

            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.normals = normals;

            return mesh;
        }

        // ================================================================
        //  Scene Helpers
        // ================================================================

        private static UnityEngine.SceneManagement.Scene? GetOrOpenMainScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path) && scene.path.Contains("MainScene"))
                return scene;

            string[] guids = AssetDatabase.FindAssets("t:Scene MainScene");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            Debug.LogWarning("[PhaseG1_GrassPlacer] MainScene not found. Creating new scene...");
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "MainScene";
            return scene;
        }

        /// <summary>
        /// Returns the density map for test validation.
        /// </summary>
        public static Dictionary<BiomeType, int> GetBiomeDensityMap() =>
            new Dictionary<BiomeType, int>(BiomeDensity);

        /// <summary>
        /// Returns the grass color map for test validation.
        /// </summary>
        public static Dictionary<BiomeType, Color> GetBiomeGrassColorMap() =>
            new Dictionary<BiomeType, Color>(BiomeGrassColor);

        /// <summary>
        /// Returns the wind settings map for test validation.
        /// </summary>
        public static Dictionary<BiomeType, (float, float)> GetBiomeWindMap() =>
            new Dictionary<BiomeType, (float, float)>(BiomeWind);
    }
}
#endif