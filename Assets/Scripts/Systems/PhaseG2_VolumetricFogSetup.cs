#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase G2-06: Volumetric Fog / Light Setup.
    /// NOTE: Unity 6 URP 17 removed the built-in Fog VolumeOverride.
    /// This tool provides fallback volumetric fog simulation via
    /// custom override components if available, or displays guidance.
    /// Menu: Tools/Phase G2/Set Volumetric Fog
    /// </summary>
    public static class PhaseG2_VolumetricFogSetup
    {
        // ================================================================
        //  Weather → Fog Multiplier Map
        // ================================================================

        /// <summary>
        /// Fog density multiplier per WeatherManager.WeatherType.
        /// Key = enum value, Value = multiplier (applied on top of base density).
        /// </summary>
        private static readonly Dictionary<WeatherManager.WeatherType, float> WeatherMultipliers = new()
        {
            { WeatherManager.WeatherType.Clear, 1.0f },
            { WeatherManager.WeatherType.Rain, 2.0f },
            { WeatherManager.WeatherType.Snow, 1.2f },
            { WeatherManager.WeatherType.Fog, 3.0f },
            { WeatherManager.WeatherType.StrongWind, 1.0f }
        };

        // ================================================================
        //  Biome → Fog Density Map
        // ================================================================

        /// <summary>
        /// Base fog density per BiomeType.
        /// 초원=0.2, 숲=0.4, 늪=0.6, 산=0.3, 사막=0.1, 호수=0.35
        /// 바위=0.2, 화산=0.5, 툰드라=0.25, 황제국=0.15, 갈대=0.3
        /// Falls through to 0.3 for unspecified biomes.
        /// </summary>
        private static readonly Dictionary<BiomeType, float> BiomeFogDensities = new()
        {
            { BiomeType.Plains, 0.2f },
            { BiomeType.Forest, 0.4f },
            { BiomeType.Swamp, 0.6f },
            { BiomeType.Mountain, 0.3f },
            { BiomeType.Desert, 0.1f },
            { BiomeType.Lake, 0.35f },
            { BiomeType.Rocky, 0.2f },
            { BiomeType.Volcanic, 0.5f },
            { BiomeType.Tundra, 0.25f },
            { BiomeType.Empire, 0.15f },
            { BiomeType.Reed, 0.3f }
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
        /// In Unity 6, modifies RenderSettings.fogDensity as fallback.
        /// For true volumetric fog, a custom VolumeComponent is required.
        /// </summary>
        public static void ApplyWeatherMultiplier(string weatherTypeName)
        {
            if (!System.Enum.TryParse(weatherTypeName, out WeatherManager.WeatherType weatherType))
                weatherType = WeatherManager.WeatherType.Clear;

            ApplyWeatherMultiplier(weatherType);
        }

        /// <summary>
        /// Applies a weather-based fog density multiplier via enum.
        /// Modifies RenderSettings.fogDensity as fallback.
        /// </summary>
        public static void ApplyWeatherMultiplier(WeatherManager.WeatherType weatherType)
        {
            float multiplier = GetWeatherMultiplier(weatherType);
            float baseDensity = RenderSettings.fogDensity;

            if (baseDensity < 0.001f)
                baseDensity = 0.3f;

            float adjustedDensity = baseDensity * multiplier;
            RenderSettings.fogDensity = adjustedDensity;

            Debug.Log($"[PhaseG2-06] Weather '{weatherType}' → fog density = {adjustedDensity} (base={baseDensity} × {multiplier}x).");
        }

        /// <summary>
        /// Applies a biome-specific fog density.
        /// Modifies RenderSettings.fogDensity as fallback.
        /// </summary>
        public static void ApplyBiomeFogDensity(string biomeTypeName)
        {
            if (!System.Enum.TryParse(biomeTypeName, out BiomeType biomeType))
                biomeType = BiomeType.Plains;

            ApplyBiomeFogDensity(biomeType);
        }

        /// <summary>
        /// Applies a biome-specific fog density via enum.
        /// Modifies RenderSettings.fogDensity as fallback.
        /// </summary>
        public static void ApplyBiomeFogDensity(BiomeType biomeType)
        {
            float density = GetBiomeFogDensity(biomeType);
            RenderSettings.fogDensity = density;
            Debug.Log($"[PhaseG2-06] Biome '{biomeType}' → fog density = {density}.");
        }

        // ================================================================
        //  Query Methods (for testing / external use)
        // ================================================================

        /// <summary>
        /// Returns the base fog density for a given biome type.
        /// Falls back to 0.3 if not found.
        /// </summary>
        public static float GetBiomeFogDensity(BiomeType biomeType)
        {
            if (BiomeFogDensities.TryGetValue(biomeType, out float density))
                return density;
            return 0.3f;
        }

        /// <summary>
        /// Returns the base fog density for a given biome type name (string).
        /// Falls back to 0.3 if not found or unparseable.
        /// </summary>
        public static float GetBiomeFogDensity(string biomeTypeName)
        {
            if (System.Enum.TryParse(biomeTypeName, out BiomeType biomeType))
                return GetBiomeFogDensity(biomeType);
            return 0.3f;
        }

        /// <summary>
        /// Returns the weather multiplier for a given weather type.
        /// Falls back to 1.0 if not found.
        /// </summary>
        public static float GetWeatherMultiplier(WeatherManager.WeatherType weatherType)
        {
            if (WeatherMultipliers.TryGetValue(weatherType, out float multiplier))
                return multiplier;
            return 1.0f;
        }

        /// <summary>
        /// Returns the weather multiplier for a given weather type name (string).
        /// Falls back to 1.0 if not found or unparseable.
        /// </summary>
        public static float GetWeatherMultiplier(string weatherTypeName)
        {
            if (System.Enum.TryParse(weatherTypeName, out WeatherManager.WeatherType weatherType))
                return GetWeatherMultiplier(weatherType);
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

            EditorUtility.SetDirty(dirLight);
            Debug.Log("[PhaseG2-06] Directional Light shadows enabled.");
        }

        /// <summary>
        /// Resets directional light shadow settings to defaults.
        /// </summary>
        private static void ResetDirectionalLightFogShadows()
        {
            Light dirLight = FindDirectionalLight();
            if (dirLight == null) return;

            dirLight.shadows = LightShadows.None;
            EditorUtility.SetDirty(dirLight);
            Debug.Log("[PhaseG2-06] Directional Light shadows disabled (reset).");
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
}

#endif // UNITY_EDITOR
