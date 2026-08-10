#if false
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Editor
{
    /// <summary>
    /// Phase G1-10: Quality Presets for the Poison game.
    /// Provides three quality levels (Low / Medium / High) accessible from
    /// the Editor menu under Tools/Phase G1/Quality/.
    ///
    /// Each preset configures:
    ///   - Shadow cascade count and resolution
    ///   - SSAO Renderer Feature (on/off)
    ///   - SSR Renderer Feature (on/off)
    ///   - Grass system presence
    /// </summary>
    public static class PhaseG1_QualityPresets
    {
        private const string URPAssetPath = "Assets/Scenes/New Universal Render Pipeline Asset.asset";
        private const string RendererPath = "Assets/Scenes/New Universal Render Pipeline Asset_Renderer.asset";
        private const string GrassSystemName = "GrassSystem";

        // ================================================================
        //  Low Quality Preset
        // ================================================================

        /// <summary>
        /// 1 cascade, 1024 shadow resolution, no SSAO, no SSR, no grass.
        /// </summary>
        [MenuItem("Tools/Phase G1/Quality/Low")]
        public static void ApplyLowQuality()
        {
            ApplyShadowSettings(cascadeCount: 1, shadowResolution: 1024);
            SetSSAOFeature(enabled: false);
            SetSSRFeature(enabled: false);
            SetGrassActive(active: false);

            AssetDatabase.SaveAssets();
            Debug.Log("[PhaseG1-10] ✅ Quality preset: Low applied (1 cascade, 1024 shadows, no SSAO/SSR, no grass).");
            EditorUtility.DisplayDialog("Phase G1-10 — Quality", "Low quality preset applied.", "OK");
        }

        [MenuItem("Tools/Phase G1/Quality/Low", true)]
        private static bool ValidateLowQuality() => true;

        // ================================================================
        //  Medium Quality Preset
        // ================================================================

        /// <summary>
        /// 2 cascades, 2048 shadow resolution, SSAO on, no SSR, grass on.
        /// </summary>
        [MenuItem("Tools/Phase G1/Quality/Medium")]
        public static void ApplyMediumQuality()
        {
            ApplyShadowSettings(cascadeCount: 2, shadowResolution: 2048);
            SetSSAOFeature(enabled: true);
            SetSSRFeature(enabled: false);
            SetGrassActive(active: true);

            AssetDatabase.SaveAssets();
            Debug.Log("[PhaseG1-10] ✅ Quality preset: Medium applied (2 cascades, 2048 shadows, SSAO on, no SSR, grass on).");
            EditorUtility.DisplayDialog("Phase G1-10 — Quality", "Medium quality preset applied.", "OK");
        }

        [MenuItem("Tools/Phase G1/Quality/Medium", true)]
        private static bool ValidateMediumQuality() => true;

        // ================================================================
        //  High Quality Preset
        // ================================================================

        /// <summary>
        /// 4 cascades, 4096 shadow resolution, SSAO + SSR on, full grass.
        /// </summary>
        [MenuItem("Tools/Phase G1/Quality/High")]
        public static void ApplyHighQuality()
        {
            ApplyShadowSettings(cascadeCount: 4, shadowResolution: 4096);
            SetSSAOFeature(enabled: true);
            SetSSRFeature(enabled: true);
            SetGrassActive(active: true);

            AssetDatabase.SaveAssets();
            Debug.Log("[PhaseG1-10] ✅ Quality preset: High applied (4 cascades, 4096 shadows, SSAO+SSR on, grass on).");
            EditorUtility.DisplayDialog("Phase G1-10 — Quality", "High quality preset applied.", "OK");
        }

        [MenuItem("Tools/Phase G1/Quality/High", true)]
        private static bool ValidateHighQuality() => true;

        // ================================================================
        //  Shadow / Cascade Settings
        // ================================================================

        private static void ApplyShadowSettings(int cascadeCount, int shadowResolution)
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            if (urpAsset == null)
            {
                Debug.LogError($"[PhaseG1-10] URP Asset not found at '{URPAssetPath}'.");
                return;
            }

            var so = new SerializedObject(urpAsset);

            // Main light shadow
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetInt(so, "m_MainLightShadowmapResolution", shadowResolution);

            // Additional light shadow
            SetBool(so, "m_AdditionalLightShadowsSupported", cascadeCount > 1);
            SetInt(so, "m_AdditionalLightsShadowmapResolution", shadowResolution);

            // Cascade count
            SetInt(so, "m_ShadowCascadeCount", cascadeCount);

            // Cascade splits based on count
            if (cascadeCount == 1)
            {
                SetVector3(so, "m_Cascade4Split", new Vector3(1f, 1f, 1f));
            }
            else if (cascadeCount == 2)
            {
                SetVector3(so, "m_Cascade4Split", new Vector3(0.33f, 1f, 1f));
            }
            else // 4 cascades
            {
                SetVector3(so, "m_Cascade4Split", new Vector3(0.15f, 0.30f, 0.55f));
            }

            // Shadow distance
            SetFloat(so, "m_ShadowDistance", cascadeCount == 1 ? 30f : 50f);

            // Soft shadows: on for Medium and High, off for Low
            SetBool(so, "m_SoftShadowsSupported", cascadeCount >= 2);
            SetInt(so, "m_SoftShadowQuality", cascadeCount >= 4 ? 3 : 2);

            // Shadow atlas
            SetInt(so, "m_ShadowAtlasResolution", shadowResolution);

            // Depth/normal bias
            SetFloat(so, "m_ShadowDepthBias", 1f);
            SetFloat(so, "m_ShadowNormalBias", 1f);
            SetFloat(so, "m_CascadeBorder", 0.2f);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(urpAsset);

            Debug.Log($"[PhaseG1-10] Shadow settings: {cascadeCount} cascade(s), {shadowResolution}px resolution.");
        }

        // ================================================================
        //  SSAO Feature Toggle
        // ================================================================

        private static void SetSSAOFeature(bool enabled)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"[PhaseG1-10] Renderer not found at '{RendererPath}'.");
                return;
            }

            var rendererSo = new SerializedObject(rendererData);
            var featuresProp = rendererSo.FindProperty("m_RendererFeatures");
            if (featuresProp == null) return;

            bool hasSSAO = false;
            for (int i = 0; i < featuresProp.arraySize; i++)
            {
                var element = featuresProp.GetArrayElementAtIndex(i);
                var featureRef = element.objectReferenceValue;
                if (featureRef is ScreenSpaceAmbientOcclusion)
                {
                    hasSSAO = true;
                    if (!enabled)
                    {
                        featuresProp.DeleteArrayElementAtIndex(i);
                        rendererSo.ApplyModifiedProperties();
                        EditorUtility.SetDirty(rendererData);
                        Debug.Log("[PhaseG1-10] SSAO feature removed.");
                    }
                    break;
                }
            }

            if (enabled && !hasSSAO)
            {
                // Create and add SSAO feature
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";

                var ssaoSo = new SerializedObject(ssao);
                SetFloat(ssaoSo, "m_Settings.Intensity", 1.0f);
                SetFloat(ssaoSo, "m_Settings.Radius", 0.5f);
                SetFloat(ssaoSo, "m_Settings.DirectLightingStrength", 0.25f);
                SetInt(ssaoSo, "m_Settings.Samples", 1); // Medium
                SetInt(ssaoSo, "m_Settings.Source", 1);  // DepthNormals
                SetInt(ssaoSo, "m_Settings.NormalSamples", 2); // High
                SetInt(ssaoSo, "m_Settings.BlurQuality", 0); // High
                SetInt(ssaoSo, "m_Settings.AOMethod", 0); // BlueNoise
                SetBool(ssaoSo, "m_Settings.Downsample", false);
                SetBool(ssaoSo, "m_Settings.AfterOpaque", false);
                SetFloat(ssaoSo, "m_Settings.Falloff", 100f);
                ssaoSo.ApplyModifiedProperties();

                featuresProp.arraySize++;
                var newFeature = featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1);
                newFeature.objectReferenceValue = ssao;
                rendererSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(rendererData);
                EditorUtility.SetDirty(ssao);

                Debug.Log("[PhaseG1-10] SSAO feature added.");
            }
        }

        // ================================================================
        //  SSR Feature Toggle
        // ================================================================

        private static void SetSSRFeature(bool enabled)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"[PhaseG1-10] Renderer not found at '{RendererPath}'.");
                return;
            }

            var rendererSo = new SerializedObject(rendererData);
            var featuresProp = rendererSo.FindProperty("m_RendererFeatures");
            if (featuresProp == null) return;

            bool hasSSR = false;
            for (int i = 0; i < featuresProp.arraySize; i++)
            {
                var element = featuresProp.GetArrayElementAtIndex(i);
                var featureRef = element.objectReferenceValue;
                if (featureRef != null && featureRef.GetType().Name == "ScreenSpaceReflections")
                {
                    hasSSR = true;
                    if (!enabled)
                    {
                        featuresProp.DeleteArrayElementAtIndex(i);
                        rendererSo.ApplyModifiedProperties();
                        EditorUtility.SetDirty(rendererData);
                        Debug.Log("[PhaseG1-10] SSR feature removed.");
                    }
                    break;
                }
            }

            if (enabled && !hasSSR)
            {
                // Create and add SSR feature using our custom ScreenSpaceReflections
                var ssr = ScriptableObject.CreateInstance<ScreenSpaceReflections>();
                ssr.name = "ScreenSpaceReflections";

                var ssrSettings = ssr.Settings;
                ssrSettings.Resolution = ScreenSpaceReflectionsSettings.ResolutionMode.Half;
                ssrSettings.MaxRaySteps = 64;
                ssrSettings.RayLength = 0.5f;
                ssrSettings.Thickness = 1.0f;
                ssrSettings.Quality = ScreenSpaceReflectionsSettings.QualityMode.High;

                featuresProp.arraySize++;
                var newFeature = featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1);
                newFeature.objectReferenceValue = ssr;
                rendererSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(rendererData);
                EditorUtility.SetDirty(ssr);

                Debug.Log("[PhaseG1-10] SSR feature added.");
            }
        }

        // ================================================================
        //  Grass Toggle
        // ================================================================

        private static void SetGrassActive(bool active)
        {
            GameObject grassSystem = GameObject.Find(GrassSystemName);
            if (grassSystem != null)
            {
                grassSystem.SetActive(active);
                Debug.Log($"[PhaseG1-10] GrassSystem {(active ? "activated" : "deactivated")}.");
            }
            else
            {
                Debug.Log($"[PhaseG1-10] GrassSystem not found in scene. {(active ? "Run Place Grass first to enable grass." : "Grass already absent.")}");
            }
        }

        // ================================================================
        //  Serialized Property Helpers
        // ================================================================

        private static void SetBool(SerializedObject so, string propertyPath, bool value)
        {
            var prop = so.FindProperty(propertyPath);
            if (prop != null) prop.boolValue = value;
        }

        private static void SetInt(SerializedObject so, string propertyPath, int value)
        {
            var prop = so.FindProperty(propertyPath);
            if (prop != null) prop.intValue = value;
        }

        private static void SetFloat(SerializedObject so, string propertyPath, float value)
        {
            var prop = so.FindProperty(propertyPath);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetVector3(SerializedObject so, string propertyPath, Vector3 value)
        {
            var prop = so.FindProperty(propertyPath);
            if (prop != null) prop.vector3Value = value;
        }
    }
}
#endif