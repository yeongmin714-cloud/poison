#if false
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core;

/// <summary>
/// Phase G1-01: Terrain Heightmap System.
/// Replaces the flat Ground plane with a Perlin noise-based heightmap mesh
/// (hills/valleys/mountains up to 40m). Repositions all Poly Haven models
/// to match the terrain height.
/// </summary>
public static class PhaseG1_TerrainHeightSetup
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string GroundName = "Ground";
    private const int HeightmapResolution = 50;
    private const float TerrainSize = 1000f;
    private const float MaxHeight = 40f;
    private const float NoiseFrequency = 0.15f;
    private const int TerrainSeed = 1337;

    // Poly Haven model name prefixes to scan for repositioning
    private const string SearchPrefixes = "fir_tree,jacaranda,tree_small,boulder,namaqualand,periwinkle,searsia,Tree_,Rock_,Plant_";
    private static readonly string[] ModelPrefixes = SearchPrefixes.Split(',');

    // Stored flat mesh info for Reset
    private static Mesh _savedFlatMesh;
    private static Vector3 _savedFlatScale;

    // ================================================================
    //  Phase G1-01: Apply Terrain Heightmap
    // ================================================================

    /// <summary>
    /// Replaces the flat Ground plane with a Perlin noise heightmap mesh.
    /// Uses TerrainGenerator with a custom BiomeDefinition configured for
    /// hills/valleys/mountains up to 40m. Repositions Poly Haven models.
    /// </summary>
    [MenuItem("Tools/Phase G1/Apply Terrain Heightmap")]
    public static void ApplyTerrainHeightmap()
    {
        // Open MainScene
        var scene = GetOrOpenMainScene();
        if (scene == null)
        {
            Debug.LogError("[PhaseG1] MainScene not found and could not be opened.");
            return;
        }

        // Find Ground GameObject
        GameObject ground = GameObject.Find(GroundName);
        if (ground == null)
        {
            Debug.LogError($"[PhaseG1] GameObject named '{GroundName}' not found in scene.");
            return;
        }

        // Save current mesh/scale for Reset
        MeshFilter existingMf = ground.GetComponent<MeshFilter>();
        if (existingMf != null && existingMf.sharedMesh != null)
        {
            _savedFlatMesh = existingMf.sharedMesh;
        }
        _savedFlatScale = ground.transform.localScale;

        // Reset scale to 1,1,1 (TerrainGenerator creates mesh at proper world size)
        ground.transform.localScale = Vector3.one;

        // Create custom BiomeDefinition for heightmap terrain (up to 40m)
        BiomeDefinition heightmapDef = new BiomeDefinition
        {
            type = BiomeType.Mountain,
            displayName = "Heightmap",
            surfaceColor = new Color(0.4f, 0.6f, 0.3f),
            noiseAmplitude = MaxHeight,
            noiseFrequency = NoiseFrequency,
            waterThreshold = 0f,
            waterColor = Color.clear,
            moveSpeedModifier = 1.0f
        };

        // Generate heightmap mesh via TerrainGenerator
        var (terrainMesh, _) = TerrainGenerator.GenerateTerrainWithDefinition(
            heightmapDef, TerrainSeed, HeightmapResolution, TerrainSize);

        if (terrainMesh == null)
        {
            Debug.LogError("[PhaseG1] TerrainGenerator returned null mesh.");
            return;
        }

        terrainMesh.name = "Terrain_Heightmap_50x50";

        // Apply mesh to Ground
        MeshFilter mf = ground.GetComponent<MeshFilter>();
        if (mf == null)
        {
            mf = ground.AddComponent<MeshFilter>();
        }
        mf.sharedMesh = terrainMesh;

        // Remove old BoxCollider if present, add MeshCollider
        BoxCollider boxCol = ground.GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            Object.DestroyImmediate(boxCol);
        }

        MeshCollider meshCol = ground.GetComponent<MeshCollider>();
        if (meshCol == null)
        {
            meshCol = ground.AddComponent<MeshCollider>();
        }
        meshCol.sharedMesh = terrainMesh;
        meshCol.convex = false;

        Debug.Log($"[PhaseG1] ✅ Terrain heightmap applied. Mesh has {terrainMesh.vertexCount} vertices, size {TerrainSize}x{TerrainSize}m, max height {MaxHeight}m.");

        // Reposition Poly Haven models to match terrain height
        int repositionedCount = RepositionPolyHavenModels(ground);

        // Save scene
        string scenePath = scene.path;
        if (string.IsNullOrEmpty(scenePath))
            scenePath = MainScenePath;
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log($"[PhaseG1] ✅ Terrain heightmap setup complete. {repositionedCount} models repositioned.");
        EditorUtility.DisplayDialog("Phase G1-01", $"Terrain heightmap applied.\n{repositionedCount} models repositioned.", "OK");
    }

    [MenuItem("Tools/Phase G1/Apply Terrain Heightmap", true)]
    private static bool ValidateApplyTerrainHeightmap() => true;

    // ================================================================
    //  Phase G1-01: Reset Flat Terrain
    // ================================================================

    /// <summary>
    /// Restores the flat Ground plane from saved mesh data or creates a new
    /// default plane primitive mesh.
    /// </summary>
    [MenuItem("Tools/Phase G1/Reset Flat Terrain")]
    public static void ResetFlatTerrain()
    {
        var scene = GetOrOpenMainScene();
        if (scene == null)
        {
            Debug.LogError("[PhaseG1] MainScene not found.");
            return;
        }

        GameObject ground = GameObject.Find(GroundName);
        if (ground == null)
        {
            Debug.LogError($"[PhaseG1] '{GroundName}' not found.");
            return;
        }

        // Remove MeshCollider
        MeshCollider meshCol = ground.GetComponent<MeshCollider>();
        if (meshCol != null)
            Object.DestroyImmediate(meshCol);

        // Restore flat mesh
        MeshFilter mf = ground.GetComponent<MeshFilter>();
        if (mf != null)
        {
            if (_savedFlatMesh != null)
            {
                mf.sharedMesh = _savedFlatMesh;
            }
            else
            {
                // Create a simple flat plane mesh (10x10 default plane)
                mf.sharedMesh = CreateFlatPlaneMesh(10f);
            }
        }

        // Restore or set scale
        if (_savedFlatScale != Vector3.zero)
            ground.transform.localScale = _savedFlatScale;
        else
            ground.transform.localScale = new Vector3(100f, 1f, 100f);

        // Add BoxCollider instead of MeshCollider for flat terrain
        BoxCollider boxCol = ground.GetComponent<BoxCollider>();
        if (boxCol == null)
        {
            boxCol = ground.AddComponent<BoxCollider>();
        }

        // Reset model Y positions to y=0
        int resetCount = 0;
        var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t.gameObject.scene != scene) continue;
            if (t.parent != null) continue; // only root objects
            if (IsPolyHavenModel(t.name))
            {
                Vector3 pos = t.position;
                pos.y = 0f;
                t.position = pos;
                resetCount++;
            }
        }

        string scenePath = scene.path;
        if (string.IsNullOrEmpty(scenePath))
            scenePath = MainScenePath;
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log($"[PhaseG1] ✅ Flat terrain restored. {resetCount} model positions reset.");
        EditorUtility.DisplayDialog("Phase G1-01", $"Flat terrain restored.\n{resetCount} model positions reset.", "OK");
    }

    [MenuItem("Tools/Phase G1/Reset Flat Terrain", true)]
    private static bool ValidateResetFlatTerrain() => true;

    // ================================================================
    //  Poly Haven Model Repositioning
    // ================================================================

    /// <summary>
    /// Scans all root GameObjects for Poly Haven models matching known
    /// name prefixes and repositions them onto the terrain surface.
    /// </summary>
    private static int RepositionPolyHavenModels(GameObject ground)
    {
        int count = 0;
        var scene = ground.scene;
        var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();

        // Ensure collider is enabled and updated
        MeshCollider meshCol = ground.GetComponent<MeshCollider>();
        if (meshCol == null) return 0;

        // Physics.SyncTransforms(); // Ensure physics state is current

        foreach (Transform t in allTransforms)
        {
            if (t.gameObject.scene != scene) continue;
            if (t.parent != null) continue; // only root objects for direct positioning
            if (!IsPolyHavenModel(t.name)) continue;

            Vector3 originalPos = t.position;
            Vector3 rayOrigin = new Vector3(originalPos.x, 200f, originalPos.z);
            float maxDist = 250f;

            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, maxDist))
            {
                // Random small offset for natural look (0.1~0.5m)
                float randomOffset = 0.1f + (float)(TerrainSeed + count * 7) % 41 / 100f;
                Vector3 newPos = hit.point;
                newPos.y += randomOffset;
                t.position = newPos;
                count++;
            }
            else
            {
                Debug.LogWarning($"[PhaseG1] Could not raycast to terrain for model: {t.name} at {originalPos}");
            }
        }

        return count;
    }

    /// <summary>
    /// Checks if a GameObject name matches any known Poly Haven model prefix.
    /// </summary>
    public static bool IsPolyHavenModel(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (string prefix in ModelPrefixes)
        {
            if (name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the count of Poly Haven style model name prefixes.
    /// </summary>
    public static int ModelPrefixCount => ModelPrefixes.Length;

    // ================================================================
    //  Helpers
    // ================================================================

    /// <summary>
    /// Gets the active scene or opens MainScene if available.
    /// </summary>
    private static UnityEngine.SceneManagement.Scene? GetOrOpenMainScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene != null && !string.IsNullOrEmpty(scene.path) && scene.path.Contains("MainScene"))
            return scene;

        string[] guids = AssetDatabase.FindAssets("t:Scene MainScene");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        Debug.LogWarning("[PhaseG1] MainScene not found. Creating new scene...");
        scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "MainScene";
        return scene;
    }

    /// <summary>
    /// Creates a simple flat plane mesh with given dimensions.
    /// Used as fallback when restoring flat terrain.
    /// </summary>
    private static Mesh CreateFlatPlaneMesh(float size)
    {
        Mesh mesh = new Mesh();
        float half = size * 0.5f;

        Vector3[] verts = new Vector3[]
        {
            new Vector3(-half, 0, -half),
            new Vector3( half, 0, -half),
            new Vector3(-half, 0,  half),
            new Vector3( half, 0,  half)
        };

        Vector2[] uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        int[] tris = new int[]
        {
            0, 1, 2,
            1, 3, 2
        };

        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.normals = new Vector3[]
        {
            Vector3.up, Vector3.up, Vector3.up, Vector3.up
        };
        mesh.name = "FlatPlane_Fallback";

        return mesh;
    }
}
#endif