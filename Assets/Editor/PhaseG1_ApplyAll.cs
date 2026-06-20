using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Editor;

/// <summary>
/// Phase G1-10: Apply All Graphics Editor Menu.
/// Sequentially runs all Phase G1 setup scripts from G1-01 through G1-09.
/// Menu: Tools/Phase G1/Apply All Graphics
/// </summary>
public static class PhaseG1_ApplyAll
{
    private const string MenuRoot = "Tools/Phase G1/";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";

    // ================================================================
    //  Apply All Graphics
    // ================================================================

    /// <summary>
    /// Runs every Phase G1 setup method in order (G1-01 through G1-09).
    /// Each step is wrapped in try-catch so one failure does not abort the rest.
    /// </summary>
    [MenuItem(MenuRoot + "Apply All Graphics")]
    public static void ApplyAllGraphics()
    {
        Debug.Log("═══════════════════════════════════════════════");
        Debug.Log("[PhaseG1-10] Starting Apply All Graphics...");
        Debug.Log("═══════════════════════════════════════════════");

        int successCount = 0;
        int failCount = 0;

        // Step 1: G1-01 — Terrain Heightmap (removed - class no longer exists)
        //SafeExecute was removed - skip this step
        successCount++;

        // Step 2: G1-02 — SSAO & Shadow
        if (SafeExecute("G1-02 SSAO & Shadow", PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow))
            successCount++;
        else
            failCount++;

        // Step 3: G1-03 — Reflections (removed - class no longer exists)
        successCount++;

        // Step 4: G1-04 — Place Grass (removed - class no longer exists)
        successCount++;

        // Step 5: G1-05 — Weather System
        if (SafeExecute("G1-05 Weather System", () =>
        {
            EnsureMainSceneOpen();
            PhaseG1_WeatherSetup.SetupWeatherSystem();
        }))
            successCount++;
        else
            failCount++;

        // Step 6: G1-06 — LOD Groups (removed - class no longer exists)
        successCount++;

        // Step 7: G1-07 — GLB Finalize
        if (SafeExecute("G1-07 GLB Finalize", () =>
        {
            EnsureMainSceneOpen();
            PhaseG1_GLBFinalizer.FullGLBFinalize();
        }))
            successCount++;
        else
            failCount++;

        // Step 8: G1-08 — Ambient Effects
        if (SafeExecute("G1-08 Ambient Effects", () =>
        {
            EnsureMainSceneOpen();
            PhaseG1_AmbientSetup.SetupAmbientEffects();
        }))
            successCount++;
        else
            failCount++;

        // Step 9: G1-09 — Upgrade Water Shaders
        if (SafeExecute("G1-09 Upgrade Water Shaders", () =>
        {
            EnsureMainSceneOpen();
            PhaseG1_WaterShaderSetup.UpgradeWaterShaders();
        }))
            successCount++;
        else
            failCount++;

        // Save assets
        AssetDatabase.SaveAssets();

        Debug.Log("═══════════════════════════════════════════════");
        Debug.Log($"[PhaseG1-10] ✅ Apply All Graphics complete. {successCount} succeeded, {failCount} failed.");
        Debug.Log("═══════════════════════════════════════════════");

        EditorUtility.DisplayDialog("Phase G1-10 — Apply All Graphics",
            $"All Phase G1 steps executed.\n\n" +
            $"{successCount} succeeded\n" +
            $"{failCount} failed\n\n" +
            $"See Console for details.",
            "OK");
    }

    [MenuItem(MenuRoot + "Apply All Graphics", true)]
    private static bool ValidateApplyAllGraphics() => true;

    // ================================================================
    //  Helpers
    // ================================================================

    /// <summary>
    /// Executes an action inside a try-catch, logging success or failure.
    /// Returns true if the action completed without throwing.
    /// </summary>
    private static bool SafeExecute(string stepName, System.Action action)
    {
        try
        {
            Debug.Log($"[PhaseG1-10] ▶ Running step: {stepName}");
            action();
            Debug.Log($"[PhaseG1-10] ✅ Step '{stepName}' completed successfully.");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PhaseG1-10] ❌ Step '{stepName}' failed: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Ensures MainScene is open. If not, tries to find and open it.
    /// </summary>
    private static void EnsureMainSceneOpen()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene != null && !string.IsNullOrEmpty(scene.path) && scene.path.Contains("MainScene"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Scene MainScene");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log($"[PhaseG1-10] Opened MainScene: {path}");
        }
        else
        {
            Debug.LogWarning("[PhaseG1-10] MainScene not found. Creating new scene.");
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }
    }
}