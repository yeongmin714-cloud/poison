using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;
using System.Collections.Generic;

/// <summary>
/// Phase 3.6.6 Editor tool: automatically places WaterBody instances across the map
/// at semi-random positions, avoiding territory centers and existing water bodies.
/// Menu: Tools/Phase 3.6/Place Water Bodies
/// </summary>
public static class Phase36_WaterPlacer
{
    private const int WaterBodyCount = 8;
    private const float MinRadius = 2f;
    private const float MaxRadius = 5f;
    private const float SpawnRange = 900f; // within ±900 on X and Z
    private const float ExclusionRadius = 30f; // keep away from territory centers

    /// <summary>
    /// Approximate territory center positions based on the ring-zone layout.
    /// Each nation occupies a quadrant with 5 rings of 4 territories.
    /// These are representative center points to avoid when placing water.
    /// </summary>
    private static readonly Vector3[] TerritoryCenters = new[]
    {
        // East quadrant (+X, -Z)
        new Vector3(100f, 0f, -100f),
        new Vector3(200f, 0f, -200f),
        new Vector3(350f, 0f, -350f),
        new Vector3(500f, 0f, -500f),
        new Vector3(700f, 0f, -700f),

        // West quadrant (-X, -Z)
        new Vector3(-100f, 0f, -100f),
        new Vector3(-200f, 0f, -200f),
        new Vector3(-350f, 0f, -350f),
        new Vector3(-500f, 0f, -500f),
        new Vector3(-700f, 0f, -700f),

        // South quadrant (-X, +Z)
        new Vector3(-100f, 0f, 100f),
        new Vector3(-200f, 0f, 200f),
        new Vector3(-350f, 0f, 350f),
        new Vector3(-500f, 0f, 500f),
        new Vector3(-700f, 0f, 700f),

        // North quadrant (+X, +Z)
        new Vector3(100f, 0f, 100f),
        new Vector3(200f, 0f, 200f),
        new Vector3(350f, 0f, 350f),
        new Vector3(-500f, 0f, 500f),
        new Vector3(-700f, 0f, 700f),

        // Empire (center)
        Vector3.zero,
    };

    [MenuItem("Tools/Phase 3.6/Place Water Bodies")]
    public static void PlaceWaterBodies()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene == null || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[WaterPlacer] No active scene. Open a scene first.");
            return;
        }

        // Clean up any existing water bodies from a previous run
        CleanupExisting();

        // Create a parent GameObject to keep the hierarchy tidy
        var parent = new GameObject("WaterBodies");
        parent.transform.position = Vector3.zero;

        System.Random rng = new System.Random(366); // deterministic seed

        List<Vector3> placedPositions = new List<Vector3>();

        for (int i = 0; i < WaterBodyCount; i++)
        {
            Vector3 position = FindValidPosition(rng, placedPositions);
            if (position == Vector3.zero && placedPositions.Count > 0)
            {
                // Fallback: if we can't find a good position, just use a different random spot
                position = new Vector3(
                    (float)(rng.NextDouble() * SpawnRange * 2f - SpawnRange),
                    0f,
                    (float)(rng.NextDouble() * SpawnRange * 2f - SpawnRange)
                );
            }

            float radius = MinRadius + (float)rng.NextDouble() * (MaxRadius - MinRadius);

            var waterGO = new GameObject($"WaterBody_{i}");
            waterGO.transform.SetParent(parent.transform);
            waterGO.transform.position = position;

            var waterBody = waterGO.AddComponent<WaterBody>();

            // Configure via reflection since fields are serialized
            SerializedObject so = new SerializedObject(waterBody);
            so.FindProperty("_radius").floatValue = radius;
            so.FindProperty("_surfaceY").floatValue = 0f;
            so.FindProperty("_waveSpeed").floatValue = 1.0f + (float)rng.NextDouble() * 1.0f; // 1.0-2.0
            so.FindProperty("_waveAmplitude").floatValue = 0.03f + (float)rng.NextDouble() * 0.05f; // 0.03-0.08
            so.ApplyModifiedProperties();

            placedPositions.Add(position);

            Debug.Log($"[WaterPlacer] Placed WaterBody_{i} at ({position.x:F1}, {position.z:F1}), radius={radius:F1}");
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[WaterPlacer] Placed {WaterBodyCount} water bodies in scene '{scene.name}'.");
    }

    [MenuItem("Tools/Phase 3.6/Place Water Bodies", true)]
    private static bool Validate() => true;

    [MenuItem("Tools/Phase 3.6/Clear Water Bodies")]
    public static void ClearWaterBodies()
    {
        CleanupExisting();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene != null && !string.IsNullOrEmpty(scene.path))
        {
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("[WaterPlacer] Cleared all water bodies.");
    }

    [MenuItem("Tools/Phase 3.6/Clear Water Bodies", true)]
    private static bool ValidateClear() => true;

    private static void CleanupExisting()
    {
        var allGOs = GameObject.FindObjectsByType<GameObject>();
        foreach (var go in allGOs)
        {
            if (go.name == "WaterBodies" || go.name.StartsWith("WaterBody_"))
            {
                GameObject.DestroyImmediate(go);
            }
        }
    }

    /// <summary>
    /// Finds a random position that is not too close to any territory center
    /// and not too close to already-placed water bodies.
    /// </summary>
    private static Vector3 FindValidPosition(System.Random rng, List<Vector3> existing)
    {
        const int maxAttempts = 50;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float x = (float)(rng.NextDouble() * SpawnRange * 2f - SpawnRange);
            float z = (float)(rng.NextDouble() * SpawnRange * 2f - SpawnRange);

            // Avoid the very center (Empire)
            if (Mathf.Abs(x) < 20f && Mathf.Abs(z) < 20f)
                continue;

            Vector3 candidate = new Vector3(x, 0f, z);
            bool valid = true;

            // Check distance to territory centers
            foreach (var center in TerritoryCenters)
            {
                if (Vector3.Distance(candidate, center) < ExclusionRadius)
                {
                    valid = false;
                    break;
                }
            }

            if (!valid) continue;

            // Check distance to already-placed water bodies
            foreach (var pos in existing)
            {
                if (Vector3.Distance(candidate, pos) < MaxRadius * 3f)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                return candidate;
        }

        return Vector3.zero;
    }
}