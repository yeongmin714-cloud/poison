#pragma warning disable 0414
#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase G2-02: HDRI Skybox 교체 구현.
/// Configures RenderSettings.skybox with a Procedural Skybox material,
/// adjusts Directional Light and Fog color to harmonize with the sky.
/// Provides Day/Night toggle menu items.
/// Menu: Tools/Phase G2/Set Skybox
/// </summary>
public static class PhaseG2_SkyboxSetup
{
    private const string SkyboxMaterialPath = "Assets/Materials/PhaseG2_ProceduralSkybox.mat";
    private static bool _isNight = false;

    // ================================================================
    //  Set Skybox
    // ================================================================

    [MenuItem("Tools/Phase G2/Set Skybox")]
    public static void SetSkybox()
    {
        EditorApplication.delayCall += () =>
        {
            _isNight = false;
            ApplySkyboxSetup(isNight: false);
            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG2-02] ✅ Skybox set to Procedural Skybox (Daytime).");
            EditorUtility.DisplayDialog("Phase G2-02", "Skybox set to Procedural Skybox (Daytime).", "OK");
        };
    }

    [MenuItem("Tools/Phase G2/Set Skybox", true)]
    private static bool ValidateSetSkybox() => true;

    // ================================================================
    //  Reset Skybox
    // ================================================================

    [MenuItem("Tools/Phase G2/Reset Skybox")]
    public static void ResetSkybox()
    {
        EditorApplication.delayCall += () =>
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            _isNight = false;

            // Reset Directional Light color to default (white / slightly warm)
            Light dirLight = FindDirectionalLight();
            if (dirLight != null)
            {
                dirLight.color = Color.white;
                dirLight.intensity = 1.0f;
                dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG2-02] ✅ Skybox reset to default.");
            EditorUtility.DisplayDialog("Phase G2-02", "Skybox reset to default.", "OK");
        };
    }

    [MenuItem("Tools/Phase G2/Reset Skybox", true)]
    private static bool ValidateResetSkybox() => true;

    // ================================================================
    //  Day / Night Toggle
    // ================================================================

    [MenuItem("Tools/Phase G2/Toggle Night Skybox")]
    public static void ToggleNightSkybox()
    {
        EditorApplication.delayCall += () =>
        {
            _isNight = true;
            ApplySkyboxSetup(isNight: true);
            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG2-02] 🌙 Night skybox applied.");
            EditorUtility.DisplayDialog("Phase G2-02", "Night skybox applied.", "OK");
        };
    }

    [MenuItem("Tools/Phase G2/Toggle Night Skybox", true)]
    private static bool ValidateToggleNightSkybox() => true;

    [MenuItem("Tools/Phase G2/Toggle Day Skybox")]
    public static void ToggleDaySkybox()
    {
        EditorApplication.delayCall += () =>
        {
            _isNight = false;
            ApplySkyboxSetup(isNight: false);
            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG2-02] ☀️ Day skybox applied.");
            EditorUtility.DisplayDialog("Phase G2-02", "Day skybox applied.", "OK");
        };
    }

    [MenuItem("Tools/Phase G2/Toggle Day Skybox", true)]
    private static bool ValidateToggleDaySkybox() => true;

    // ================================================================
    //  Core Setup
    // ================================================================

    /// <summary>
    /// Applies the full skybox setup: material, directional light, and fog.
    /// </summary>
    private static void ApplySkyboxSetup(bool isNight)
    {
        // Create or load procedural skybox material
        Material skyboxMat = GetOrCreateProceduralSkyboxMaterial(isNight);
        if (skyboxMat == null) return;

        RenderSettings.skybox = skyboxMat;

        // Configure directional light to match the skybox
        ConfigureDirectionalLight(isNight);

        // Configure fog to match the skybox horizon color
        ConfigureFog(isNight);

        // Update camera clear flags
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.Skybox;
        }
    }

    // ================================================================
    //  Skybox Material
    // ================================================================

    /// <summary>
    /// Loads existing skybox material or creates a new Procedural Skybox material.
    /// </summary>
    private static Material GetOrCreateProceduralSkyboxMaterial(bool isNight)
    {
        // Try to load existing material
        Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
        if (existingMat != null)
        {
            UpdateSkyboxMaterialProperties(existingMat, isNight);
            return existingMat;
        }

        // Find the Procedural skybox shader
        Shader proceduralShader = Shader.Find("Skybox/Procedural");
        if (proceduralShader == null)
        {
            Debug.LogError("[PhaseG2-02] Skybox/Procedural shader not found. " +
                "Ensure the built-in shader is available.");
            return null;
        }

        Material mat = new Material(proceduralShader);
        mat.name = "PhaseG2_ProceduralSkybox";
        UpdateSkyboxMaterialProperties(mat, isNight);

        // Save as asset
        System.IO.Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.CreateAsset(mat, SkyboxMaterialPath);
        Debug.Log($"[PhaseG2-02] Created Procedural Skybox material at '{SkyboxMaterialPath}'.");

        return mat;
    }

    /// <summary>
    /// Updates the Procedural Skybox material parameters.
    /// </summary>
    private static void UpdateSkyboxMaterialProperties(Material mat, bool isNight)
    {
        if (isNight)
        {
            // --- Night parameters ---
            mat.SetFloat("_SunSize", 0.02f);
            mat.SetFloat("_SunSizeConvergence", 5f);
            mat.SetFloat("_AtmosphereThickness", 1.2f);
            mat.SetColor("_SkyTint", new Color(0.05f, 0.05f, 0.15f));  // Deep night blue
            mat.SetColor("_GroundColor", new Color(0.1f, 0.1f, 0.12f)); // Dark ground
            mat.SetFloat("_Exposure", 0.5f);
        }
        else
        {
            // --- Day parameters (as specified) ---
            mat.SetFloat("_SunSize", 0.04f);
            mat.SetFloat("_SunSizeConvergence", 5f);
            mat.SetFloat("_AtmosphereThickness", 0.8f);
            mat.SetColor("_SkyTint", new Color(0.4f, 0.6f, 0.9f));        // Bright blue sky
            mat.SetColor("_GroundColor", new Color(0.5f, 0.5f, 0.5f));    // Gray ground tint
            mat.SetFloat("_Exposure", 1.0f);
        }

        EditorUtility.SetDirty(mat);
    }

    // ================================================================
    //  Directional Light
    // ================================================================

    /// <summary>
    /// Finds the primary Directional Light in the scene and adjusts its
    /// color, intensity, and rotation to harmonize with the skybox.
    /// </summary>
    private static void ConfigureDirectionalLight(bool isNight)
    {
        Light dirLight = FindDirectionalLight();
        if (dirLight == null)
        {
            Debug.LogWarning("[PhaseG2-02] No Directional Light found in scene. Creating one.");
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            dirLight = go.AddComponent<Light>();
            dirLight.type = LightType.Directional;
        }

        if (isNight)
        {
            // Moon light: cool, dim
            dirLight.color = new Color(0.4f, 0.45f, 0.7f); // Cool blue moonlight
            dirLight.intensity = 0.3f;
            dirLight.transform.rotation = Quaternion.Euler(30f, -60f, 0f); // Lower angle
        }
        else
        {
            // Sun light: warm, bright
            dirLight.color = new Color(1.0f, 0.95f, 0.85f); // Warm sunlight
            dirLight.intensity = 1.2f;
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f); // Mid-high angle
        }

        // Ensure shadow settings are reasonable
        dirLight.shadows = LightShadows.Soft;
        EditorUtility.SetDirty(dirLight.gameObject);
    }

    // ================================================================
    //  Fog
    // ================================================================

    /// <summary>
    /// Configures fog color and density to match the skybox tone.
    /// </summary>
    private static void ConfigureFog(bool isNight)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.008f;

        if (isNight)
        {
            // Dark fog
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.12f);
        }
        else
        {
            // Light blue fog matching sky
            RenderSettings.fogColor = new Color(0.5f, 0.65f, 0.85f);
        }
    }

    // ================================================================
    //  Helpers
    // ================================================================

    /// <summary>
    /// Finds the primary Directional Light in the scene.
    /// Prefers the one tagged as the sun via RenderSettings.sun.
    /// Falls back to the first directional light found.
    /// </summary>
    private static Light FindDirectionalLight()
    {
        // Check RenderSettings.sun first
        if (RenderSettings.sun != null)
            return RenderSettings.sun;

        // Fallback: find first directional light
        Light[] lights = Object.FindObjectsByType<Light>();
        foreach (Light l in lights)
        {
            if (l.type == LightType.Directional)
                return l;
        }

        return null;
    }

    /// <summary>
    /// Returns whether the skybox is currently set to night mode.
    /// </summary>
    public static bool IsNightMode => _isNight;
}

#endif // UNITY_EDITOR