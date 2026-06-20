using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Linq;

namespace ProjectName.Editor
{
    /// <summary>
    /// Phase G1-02: SSAO + Shadow quality optimization.
    /// Adds SSAO Renderer Feature and configures shadow cascades (4 cascades, 4096 resolution, PCF 7x7).
    /// </summary>
    public static class PhaseG1_SSAO_ShadowSetup
    {
        private const string URPAssetPath = "Assets/Scenes/New Universal Render Pipeline Asset.asset";
        private const string RendererPath = "Assets/Scenes/New Universal Render Pipeline Asset_Renderer.asset";

        // ================================================================
        //  Apply SSAO & Shadow Settings
        // ================================================================

        /// <summary>
        /// Applies SSAO Renderer Feature + shadow cascade / resolution / PCF quality settings.
        /// </summary>
        [MenuItem("Tools/Phase G1/Apply SSAO & Shadow")]
        public static void ApplySSAOAndShadow()
        {
            ConfigureURPAsset();
            AddSSAORendererFeature();
            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG1] ✅ SSAO & Shadow settings applied.");
            EditorUtility.DisplayDialog("Phase G1-02", "SSAO & Shadow settings applied successfully.", "OK");
        }

        [MenuItem("Tools/Phase G1/Apply SSAO & Shadow", true)]
        private static bool ValidateApplySSAOAndShadow() => true;

        // ================================================================
        //  Reset Graphics Defaults
        // ================================================================

        /// <summary>
        /// Resets URP Asset shadow settings to defaults and removes SSAO Renderer Feature.
        /// </summary>
        [MenuItem("Tools/Phase G1/Reset Graphics Defaults")]
        public static void ResetGraphicsDefaults()
        {
            ResetURPAsset();
            RemoveSSAORendererFeature();
            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG1] ✅ Graphics defaults reset.");
            EditorUtility.DisplayDialog("Phase G1-02", "Graphics defaults reset.", "OK");
        }

        [MenuItem("Tools/Phase G1/Reset Graphics Defaults", true)]
        private static bool ValidateResetGraphicsDefaults() => true;

        // ================================================================
        //  URP Asset Shadow Configuration
        // ================================================================

        private static void ConfigureURPAsset()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            if (urpAsset == null)
            {
                Debug.LogError($"[PhaseG1] URP Asset not found at '{URPAssetPath}'.");
                return;
            }

            var so = new SerializedObject(urpAsset);

            // Main light shadow settings
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetInt(so, "m_MainLightShadowmapResolution", 4096);

            // Additional light shadow settings
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 4096);

            // Cascade count = 4 with splits: 15% / 30% / 55% / 100%
            SetInt(so, "m_ShadowCascadeCount", 4);
            SetVector3(so, "m_Cascade4Split", new Vector3(0.15f, 0.30f, 0.55f));

            // Shadow distance
            SetFloat(so, "m_ShadowDistance", 50f);

            // Soft shadows: PCF 7x7 (SoftShadowQuality.High = 3)
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_SoftShadowQuality", 3);

            // Shadow atlas for additional lights
            SetInt(so, "m_ShadowAtlasResolution", 4096);

            // Depth/normal bias
            SetFloat(so, "m_ShadowDepthBias", 1f);
            SetFloat(so, "m_ShadowNormalBias", 1f);

            // Cascade border
            SetFloat(so, "m_CascadeBorder", 0.2f);

            so.ApplyModifiedProperties();
        }

        private static void ResetURPAsset()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            if (urpAsset == null)
            {
                Debug.LogError($"[PhaseG1] URP Asset not found at '{URPAssetPath}'.");
                return;
            }

            var so = new SerializedObject(urpAsset);

            // Restore defaults
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetInt(so, "m_MainLightShadowmapResolution", 2048);
            SetBool(so, "m_AdditionalLightShadowsSupported", false);
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 2048);
            SetInt(so, "m_ShadowCascadeCount", 1);
            SetVector3(so, "m_Cascade4Split", new Vector3(0.067f, 0.2f, 0.467f));
            SetFloat(so, "m_ShadowDistance", 50f);
            SetBool(so, "m_SoftShadowsSupported", false);
            SetInt(so, "m_SoftShadowQuality", 2);
            SetInt(so, "m_ShadowAtlasResolution", 256);
            SetFloat(so, "m_ShadowDepthBias", 1f);
            SetFloat(so, "m_ShadowNormalBias", 1f);
            SetFloat(so, "m_CascadeBorder", 0.2f);

            so.ApplyModifiedProperties();
        }

        // ================================================================
        //  SSAO Renderer Feature
        // ================================================================

        /// <summary>
        /// Adds a ScreenSpaceAmbientOcclusion Renderer Feature to the URP Renderer.
        /// Configures Intensity=1.0, Radius=0.5, Samples=Medium, DirectLightingStrength=0.25.
        /// </summary>
        private static void AddSSAORendererFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"[PhaseG1] Renderer not found at '{RendererPath}'.");
                return;
            }

            // Check if SSAO already exists
            if (rendererData.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion))
            {
                Debug.Log("[PhaseG1] SSAO feature already exists. Skipping.");
                return;
            }

            // Create the SSAO feature
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";

            // Configure SSAO settings via SerializedObject (Settings class is internal)
            var ssaoSo = new SerializedObject(ssao);
            SetFloat(ssaoSo, "m_Settings.Intensity", 1.0f);
            SetFloat(ssaoSo, "m_Settings.Radius", 0.5f);
            SetFloat(ssaoSo, "m_Settings.DirectLightingStrength", 0.25f);
            // AOSampleOption: High=0, Medium=1, Low=2
            SetInt(ssaoSo, "m_Settings.Samples", 1); // Medium
            // DepthSource: Depth=0, DepthNormals=1
            SetInt(ssaoSo, "m_Settings.Source", 1); // DepthNormals
            // NormalQuality: Low=0, Medium=1, High=2
            SetInt(ssaoSo, "m_Settings.NormalSamples", 2); // High
            // BlurQualityOptions: High=0, Medium=1, Low=2
            SetInt(ssaoSo, "m_Settings.BlurQuality", 0); // High
            // AOMethodOptions: BlueNoise=0, InterleavedGradient=1
            SetInt(ssaoSo, "m_Settings.AOMethod", 0); // BlueNoise
            SetBool(ssaoSo, "m_Settings.Downsample", false);
            SetBool(ssaoSo, "m_Settings.AfterOpaque", false);
            SetFloat(ssaoSo, "m_Settings.Falloff", 100f);
            ssaoSo.ApplyModifiedProperties();

            // Add feature to renderer
            var rendererSo = new SerializedObject(rendererData);
            var featuresProp = rendererSo.FindProperty("m_RendererFeatures");
            if (featuresProp == null)
            {
                Debug.LogError("[PhaseG1] Could not find m_RendererFeatures on renderer data.");
                return;
            }

            featuresProp.arraySize++;
            var newFeature = featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1);
            newFeature.objectReferenceValue = ssao;
            rendererSo.ApplyModifiedProperties();

            // Mark the SSAO sub-asset as dirty so it gets saved with the renderer
            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(ssao);

            Debug.Log("[PhaseG1] ✅ SSAO Renderer Feature added and configured.");
        }

        /// <summary>
        /// Removes the SSAO Renderer Feature from the URP Renderer if present.
        /// </summary>
        private static void RemoveSSAORendererFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"[PhaseG1] Renderer not found at '{RendererPath}'.");
                return;
            }

            var rendererSo = new SerializedObject(rendererData);
            var featuresProp = rendererSo.FindProperty("m_RendererFeatures");
            if (featuresProp == null) return;

            int removedCount = 0;
            for (int i = featuresProp.arraySize - 1; i >= 0; i--)
            {
                var element = featuresProp.GetArrayElementAtIndex(i);
                var featureRef = element.objectReferenceValue;
                if (featureRef is ScreenSpaceAmbientOcclusion)
                {
                    featuresProp.DeleteArrayElementAtIndex(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                rendererSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(rendererData);
                Debug.Log($"[PhaseG1] ✅ Removed {removedCount} SSAO feature(s).");
            }
            else
            {
                Debug.Log("[PhaseG1] No SSAO feature found to remove.");
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
