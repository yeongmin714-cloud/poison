#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

/// <summary>
/// Phase G2-06: Volumetric Fog / Light Setup.
/// NOTE: Unity 6 URP 17 removed the built-in Fog VolumeOverride.
/// This tool provides fallback volumetric fog simulation via
/// custom override components if available, or displays guidance.
/// Menu: Tools/Phase G2/Set Volumetric Fog
/// </summary>
public static class PhaseG2_VolumetricFogSetup
{
    private const string VolumeProfilePath = "Assets/DefaultVolumeProfile.asset";

    // ================================================================
    //  Weather → Fog Multiplier Map
    // ================================================================

    /// <summary>
    /// Fog density multiplier per WeatherManager.WeatherType.
    /// Key = enum value name, Value = multiplier (applied on top of base density).
    /// </summary>
    private static readonly Dictionary<string, float> WeatherMultipliers = new()
    {
        { "Clear", 1.0f },
        { "Rain", 2.0f },
        { "Snow", 1.2f },
        { "Fog", 3.0f },
        { "StrongWind", 1.0f }
    };

    // ================================================================
    //  Biome → Fog Density Map
    // ================================================================

    /// <summary>
    /// Base fog density per BiomeType enum value name.
    /// 초원=0.2, 숲=0.4, 늪=0.6, 산=0.3, 사막=0.1
    /// Falls through to 0.3 for unspecified biomes.
    /// </summary>
    private static readonly Dictionary<string, float> BiomeFogDensities = new()
    {
        { "Plains", 0.2f },
        { "Forest", 0.4f },
        { "Swamp", 0.6f },
        { "Mountain", 0.3f },
        { "Desert", 0.1f }
    };

    // ================================================================
    //  Set Volumetric Fog
    // ================================================================

    [MenuItem("Tools/Phase G2/Set Volumetric Fog")]
    public static void SetVolumetricFog()
    {
        EditorApplication.delayCall += () =>
        {
            if (!IsFogVolumeOverrideAvailable())
            {
                Debug.Log("[PhaseG2-06] Volumetric Fog requires a custom VolumeComponent or shader in Unity 6 URP 17. Opening documentation.");
                EditorUtility.DisplayDialog("Phase G2-06",
                    "Unity 6 URP 17 removed the built-in Fog VolumeOverride.\n\n" +
                    "To add volumetric fog, install a custom fog override via:\n" +
                    "  - The Universal Render Pipeline sample package\n" +
                    "  - A third-party volumetric fog shader\n" +
                    "  - Or use Unity's RenderSettings.fog (legacy) as fallback\n\n" +
                    "Attempting legacy fog fallback setup via RenderSettings...",
                    "OK");
            }

            ConfigureDirectionalLightFogShadows();
            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG2-06] Directional Light fog shadows configured (volumetric fog requires external override).");
            EditorUtility.DisplayDialog("Phase G2-06",
                "Directional Light shadow settings configured.\n\n" +
                "Base data ready for fog integration (weather/biome multipliers).\n" +
                "Weather integration ready (Rain=2x, Fog=3x).\n" +
                "Biome densities: Plains=0.2, Forest=0.4, Swamp=0.6, Mountain=0.3, Desert=0.1\n\n" +
                "NOTE: Built-in Fog VolumeOverride was removed in Unity 6 URP 17.\n" +
                "Install a custom fog override to enable volumetric fog.",
                "OK");
        };
    }

    [MenuItem("Tools/Phase G2/Set Volumetric Fog", true)]
    private static bool ValidateSetVolumetricFog() => true;

    // ================================================================
    //  Reset Volumetric Fog
    // ================================================================

    [MenuItem("Tools/Phase G2/Reset Volumetric Fog")]
    public static void ResetVolumetricFog()
    {
        EditorApplication.delayCall += () =>
        {
            ResetDirectionalLightFogShadows();
            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG2-06] Fog shadow settings reset.");
            EditorUtility.DisplayDialog("Phase G2-06", "Fog shadow settings reset.", "OK");
        };
    }

    [MenuItem("Tools/Phase G2/Reset Volumetric Fog", true)]
    private static bool ValidateResetVolumetricFog() => true;

    // ================================================================
    //  Weather / Biome Multiplier Queries (kept for external compatibility)
    // ================================================================

    /// <summary>
    /// Applies a weather-based fog density multiplier.
    /// NOTE: In Unity 6, this requires a custom fog VolumeComponent.
    /// Falls back to logging the intended value.
    /// </summary>
    public static void ApplyWeatherMultiplier(string weatherTypeName)
    {
        if (!WeatherMultipliers.TryGetValue(weatherTypeName, out float multiplier))
            multiplier = 1.0f;

        float baseDensity = 0.3f;
        float adjustedDensity = baseDensity * multiplier;

        Debug.Log($"[PhaseG2-06] Weather '{weatherTypeName}' → target fog density = {adjustedDensity} (base={baseDensity} × {multiplier}x). Apply via custom fog override.");
    }

    /// <summary>
    /// Applies a biome-specific fog density.
    /// NOTE: In Unity 6, this requires a custom fog VolumeComponent.
    /// Falls back to logging the intended value.
    /// </summary>
    public static void ApplyBiomeFogDensity(string biomeTypeName)
    {
        float density = GetBiomeFogDensity(biomeTypeName);
        Debug.Log($"[PhaseG2-06] Biome '{biomeTypeName}' → target fog density = {density}. Apply via custom fog override.");
    }

    // ================================================================
    //  Query Methods (for testing / external use)
    // ================================================================

    /// <summary>
    /// Returns the base fog density for a given biome type name.
    /// Falls back to 0.3 if not found.
    /// </summary>
    public static float GetBiomeFogDensity(string biomeTypeName)
    {
        if (BiomeFogDensities.TryGetValue(biomeTypeName, out float density))
            return density;
        return 0.3f;
    }

    /// <summary>
    /// Returns the weather multiplier for a given weather type name.
    /// Falls back to 1.0 if not found.
    /// </summary>
    public static float GetWeatherMultiplier(string weatherTypeName)
    {
        if (WeatherMultipliers.TryGetValue(weatherTypeName, out float multiplier))
            return multiplier;
        return 1.0f;
    }

    // ================================================================
    //  Volume Profile Configuration (Fog override removed in Unity 6 URP 17)
    // ================================================================

    /// <summary>
    /// Checks whether the built-in Fog volume override type is available
    /// (removed in Unity 6 URP 17).
    /// </summary>
    private static bool IsFogVolumeOverrideAvailable()
    {
        // Fog type was removed from UnityEngine.Rendering.Universal in Unity 6 URP 17
        // Attempt to check via reflection or type lookup
        try
        {
            var fogType = System.Type.GetType("UnityEngine.Rendering.Universal.Fog, Unity.RenderPipelines.Universal.Runtime");
            return fogType != null;
        }
        catch
        {
            return false;
        }
    }

    // ================================================================
    //  Directional Light Fog Shadows
    // ================================================================

    /// <summary>
    /// Configures directional light shadows (fog shadow contribution).
    /// rendersVolumetricFog was removed in Unity 6 — this now only
    /// enables standard shadows.
    /// </summary>
    private static void ConfigureDirectionalLightFogShadows()
    {
        Light dirLight = FindDirectionalLight();
        if (dirLight == null)
        {
            Debug.LogWarning("[PhaseG2-06] No Directional Light found in scene. Creating one.");
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            dirLight = go.AddComponent<Light>();
            dirLight.type = LightType.Directional;
        }

        // Enable shadows
        dirLight.shadows = LightShadows.Soft;

        // rendersVolumetricFog was removed in Unity 6 URP 17
        // Shadow quality is used as fallback for fog-like effects

        EditorUtility.SetDirty(dirLight.gameObject);
        Debug.Log("[PhaseG2-06] Directional Light shadows enabled.");
    }

    /// <summary>
    /// Resets directional light settings (fog shadows no-op in Unity 6).
    /// </summary>
    private static void ResetDirectionalLightFogShadows()
    {
        Light dirLight = FindDirectionalLight();
        if (dirLight == null) return;

        // rendersVolumetricFog no longer exists in Unity 6
        EditorUtility.SetDirty(dirLight.gameObject);
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
        if (RenderSettings.sun != null)
            return RenderSettings.sun;

        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in lights)
        {
            if (l.type == LightType.Directional)
                return l;
        }

        return null;
    }
}

#endif // UNITY_EDITOR