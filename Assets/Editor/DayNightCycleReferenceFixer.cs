using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-fix DayNightCycle Sun/Moon Light references when scene opens in Editor.
/// Batchmode cannot persist cross-object references set via SerializedObject/reflection.
/// This runs in Editor (not batchmode) and fixes the references properly.
/// </summary>
[InitializeOnLoad]
public static class DayNightCycleReferenceFixer
{
    static DayNightCycleReferenceFixer()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        // Only run for MainScene
        if (scene.name != "MainScene") return;

        var dnc = Object.FindFirstObjectByType<ProjectName.Systems.DayNightCycle>();
        if (dnc == null) return;

        // Use reflection to check private fields
        var dncType = dnc.GetType();
        var sunField = dncType.GetField("_sunLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var moonField = dncType.GetField("_moonLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        bool needsFix = false;

        if (sunField != null)
        {
            var sun = sunField.GetValue(dnc) as Light;
            if (sun == null)
            {
                // Find the Sun light by name
                var sunObj = GameObject.Find("Directional Light (Sun)");
                if (sunObj != null)
                {
                    sun = sunObj.GetComponent<Light>();
                    if (sun != null)
                    {
                        sunField.SetValue(dnc, sun);
                        needsFix = true;
                        Debug.Log("[DayNightCycleReferenceFixer] Fixed _sunLight reference");
                    }
                }
            }
        }

        if (moonField != null)
        {
            var moon = moonField.GetValue(dnc) as Light;
            if (moon == null)
            {
                // Find the Moon light by name
                var moonObj = GameObject.Find("Directional Light (Moon)");
                if (moonObj != null)
                {
                    moon = moonObj.GetComponent<Light>();
                    if (moon != null)
                    {
                        moonField.SetValue(dnc, moon);
                        needsFix = true;
                        Debug.Log("[DayNightCycleReferenceFixer] Fixed _moonLight reference");
                    }
                }
            }
        }

        if (needsFix)
        {
            EditorUtility.SetDirty(dnc);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DayNightCycleReferenceFixer] DayNightCycle references fixed and scene saved");
        }
    }
}