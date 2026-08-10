using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;

/// <summary>
/// G1-05: Weather System Editor Setup.
/// Menu items under Tools/Phase G1/ for configuring the weather system in the scene.
/// </summary>
public static class PhaseG1_WeatherSetup
{
    // ================================================================
    // Menu Items
    // ================================================================

    [MenuItem("Tools/Phase G1/Setup Weather System")]
    public static void SetupWeatherSystem()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene == null || string.IsNullOrEmpty(scene.path))
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene = EditorSceneManager.GetActiveScene();
        }

        Undo.IncrementCurrentGroup();
        int groupIndex = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Weather System");

        // ----- WeatherManager -----
        var existingManager = Object.FindAnyObjectByType<WeatherManager>();
        if (existingManager == null)
        {
            var mgrGo = new GameObject("WeatherManager");
            Undo.RegisterCreatedObjectUndo(mgrGo, "Create WeatherManager");
            var manager = mgrGo.AddComponent<WeatherManager>();
            EditorUtility.SetDirty(mgrGo);

            // Try to find a directional light
            var dirLight = Object.FindAnyObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                var serializedObj = new SerializedObject(manager);
                var lightProp = serializedObj.FindProperty("_directionalLight");
                if (lightProp != null)
                {
                    lightProp.objectReferenceValue = dirLight;
                    serializedObj.ApplyModifiedProperties();
                }
            }

            Debug.Log("[PhaseG1] WeatherManager created.");
        }
        else
        {
            Debug.Log("[PhaseG1] WeatherManager already exists in scene.");
        }

        // ----- WeatherParticleController -----
        var existingParticles = Object.FindAnyObjectByType<WeatherParticleController>();
        if (existingParticles == null)
        {
            var particleGo = new GameObject("WeatherParticleController");
            Undo.RegisterCreatedObjectUndo(particleGo, "Create WeatherParticleController");
            particleGo.AddComponent<WeatherParticleController>();
            EditorUtility.SetDirty(particleGo);
            Debug.Log("[PhaseG1] WeatherParticleController created.");
        }
        else
        {
            Debug.Log("[PhaseG1] WeatherParticleController already exists in scene.");
        }

        // ----- WindZone -----
        var existingWindZone = Object.FindAnyObjectByType<WindZone>();
        if (existingWindZone == null)
        {
            var windGo = new GameObject("WindZone");
            Undo.RegisterCreatedObjectUndo(windGo, "Create WindZone");
            var windZone = windGo.AddComponent<WindZone>();
            windZone.windMain = 0f;
            windZone.windTurbulence = 0.5f;
            windZone.windPulseMagnitude = 0.5f;
            windZone.windPulseFrequency = 0.5f;
            windZone.mode = WindZoneMode.Directional;
            EditorUtility.SetDirty(windGo);
            Debug.Log("[PhaseG1] WindZone created with default settings.");
        }
        else
        {
            Debug.Log("[PhaseG1] WindZone already exists in scene.");
        }

        // ----- Enable Fog -----
        if (!RenderSettings.fog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogMode = FogMode.Exponential;
            Debug.Log("[PhaseG1] Fog enabled in RenderSettings.");
        }

        Undo.CollapseUndoOperations(groupIndex);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[PhaseG1] Weather System setup complete.");
    }

    [MenuItem("Tools/Phase G1/Clear Weather")]
    public static void SetClearWeather()
    {
        var mgr = Object.FindAnyObjectByType<WeatherManager>();
        if (mgr != null)
        {
            mgr.SetWeather(WeatherManager.WeatherType.Clear);
            Debug.Log("[PhaseG1] Weather set to Clear.");
        }
        else
        {
            Debug.LogWarning("[PhaseG1] WeatherManager not found in scene.");
        }
    }

    [MenuItem("Tools/Phase G1/Rain Weather")]
    public static void SetRainWeather()
    {
        var mgr = Object.FindAnyObjectByType<WeatherManager>();
        if (mgr != null)
        {
            mgr.SetWeather(WeatherManager.WeatherType.Rain);
            Debug.Log("[PhaseG1] Weather set to Rain.");
        }
        else
        {
            Debug.LogWarning("[PhaseG1] WeatherManager not found in scene.");
        }
    }

    [MenuItem("Tools/Phase G1/Snow Weather")]
    public static void SetSnowWeather()
    {
        var mgr = Object.FindAnyObjectByType<WeatherManager>();
        if (mgr != null)
        {
            mgr.SetWeather(WeatherManager.WeatherType.Snow);
            Debug.Log("[PhaseG1] Weather set to Snow.");
        }
        else
        {
            Debug.LogWarning("[PhaseG1] WeatherManager not found in scene.");
        }
    }

    [MenuItem("Tools/Phase G1/Fog Weather")]
    public static void SetFogWeather()
    {
        var mgr = Object.FindAnyObjectByType<WeatherManager>();
        if (mgr != null)
        {
            mgr.SetWeather(WeatherManager.WeatherType.Fog);
            Debug.Log("[PhaseG1] Weather set to Fog.");
        }
        else
        {
            Debug.LogWarning("[PhaseG1] WeatherManager not found in scene.");
        }
    }

    [MenuItem("Tools/Phase G1/Strong Wind Weather")]
    public static void SetWindWeather()
    {
        var mgr = Object.FindAnyObjectByType<WeatherManager>();
        if (mgr != null)
        {
            mgr.SetWeather(WeatherManager.WeatherType.StrongWind);
            Debug.Log("[PhaseG1] Weather set to StrongWind.");
        }
        else
        {
            Debug.LogWarning("[PhaseG1] WeatherManager not found in scene.");
        }
    }
}