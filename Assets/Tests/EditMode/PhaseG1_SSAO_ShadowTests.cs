using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ProjectName.Editor;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase G1-02: EditMode tests for SSAO + Shadow quality setup.
    /// Tests cover:
    ///   - PhaseG1_SSAO_ShadowSetup class existence and menu items
    ///   - URP Asset shadow settings (cascade count, resolution, PCF quality)
    ///   - SSAO Renderer Feature addition and configuration
    ///   - Reset functionality
    ///   - Edge cases (duplicate SSAO, missing asset)
    /// </summary>
    public class PhaseG1_SSAO_ShadowTests
    {
        private const string URPAssetPath = "Assets/Scenes/New Universal Render Pipeline Asset.asset";
        private const string RendererPath = "Assets/Scenes/New Universal Render Pipeline Asset_Renderer.asset";

        // ================================================================
        // Part 1: Editor Class & Menu Item Tests
        // ================================================================

        [Test]
        public void PhaseG1_SSAO_ShadowSetup_ClassExists()
        {
            var type = typeof(PhaseG1_SSAO_ShadowSetup);
            Assert.IsNotNull(type, "PhaseG1_SSAO_ShadowSetup class must exist.");
        }

        [Test]
        public void PhaseG1_SSAO_ShadowSetup_ApplySSAOAndShadow_MenuItemExists()
        {
            var type = typeof(PhaseG1_SSAO_ShadowSetup);
            var method = type.GetMethod("ApplySSAOAndShadow",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "ApplySSAOAndShadow public static method must exist.");

            var attr = System.Attribute.GetCustomAttribute(method, typeof(MenuItem));
            Assert.IsNotNull(attr, "MenuItem attribute must exist on ApplySSAOAndShadow.");

            var menuItem = attr as MenuItem;
            Assert.IsNotNull(menuItem, "MenuItem attribute must be of type MenuItem.");
            Assert.AreEqual("Tools/Phase G1/Apply SSAO & Shadow", menuItem.menuItem,
                "Menu path must be 'Tools/Phase G1/Apply SSAO & Shadow'.");
        }

        [Test]
        public void PhaseG1_SSAO_ShadowSetup_ResetGraphicsDefaults_MenuItemExists()
        {
            var type = typeof(PhaseG1_SSAO_ShadowSetup);
            var method = type.GetMethod("ResetGraphicsDefaults",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "ResetGraphicsDefaults public static method must exist.");

            var attr = System.Attribute.GetCustomAttribute(method, typeof(MenuItem));
            Assert.IsNotNull(attr, "MenuItem attribute must exist on ResetGraphicsDefaults.");

            var menuItem = attr as MenuItem;
            Assert.IsNotNull(menuItem, "MenuItem attribute must be of type MenuItem.");
            Assert.AreEqual("Tools/Phase G1/Reset Graphics Defaults", menuItem.menuItem,
                "Menu path must be 'Tools/Phase G1/Reset Graphics Defaults'.");
        }

        // ================================================================
        // Part 2: URP Asset Existence & Properties
        // ================================================================

        [Test]
        public void URPAsset_ExistsAtExpectedPath()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            Assert.IsNotNull(urpAsset, $"URP Asset must exist at '{URPAssetPath}'.");
        }

        [Test]
        public void URPAsset_HasRendererAtExpectedPath()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Assert.IsNotNull(rendererData, $"Renderer must exist at '{RendererPath}'.");
        }

        [Test]
        public void URPAsset_ApplyIncreasesMainLightShadowResolutionTo4096()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            Assert.IsNotNull(urpAsset);

            // Record initial state
            var so = new SerializedObject(urpAsset);
            var resProp = so.FindProperty("m_MainLightShadowmapResolution");
            Assert.IsNotNull(resProp, "Property m_MainLightShadowmapResolution must exist.");
            int initialRes = resProp.intValue;

            // Apply
            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();

            // Re-serialize and check
            so.Update();
            int newRes = resProp.intValue;
            Assert.AreEqual(4096, newRes,
                $"Main light shadow resolution should be 4096 after apply (was {initialRes}).");

            // Cleanup: restore initial
            SetProperty(urpAsset, "m_MainLightShadowmapResolution", initialRes);
        }

        [Test]
        public void URPAsset_ApplySetsFourCascadesWithCorrectSplits()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            Assert.IsNotNull(urpAsset);

            var so = new SerializedObject(urpAsset);
            int initialCascadeCount = so.FindProperty("m_ShadowCascadeCount").intValue;
            var initialSplit = so.FindProperty("m_Cascade4Split").vector3Value;

            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();

            so.Update();
            Assert.AreEqual(4, so.FindProperty("m_ShadowCascadeCount").intValue,
                "Shadow cascade count should be 4 after apply.");

            Vector3 expectedSplit = new Vector3(0.15f, 0.30f, 0.55f);
            Vector3 actualSplit = so.FindProperty("m_Cascade4Split").vector3Value;
            Assert.AreEqual(expectedSplit.x, actualSplit.x, 0.001f, "Cascade split X should be 0.15.");
            Assert.AreEqual(expectedSplit.y, actualSplit.y, 0.001f, "Cascade split Y should be 0.30.");
            Assert.AreEqual(expectedSplit.z, actualSplit.z, 0.001f, "Cascade split Z should be 0.55.");

            // Restore
            SetProperty(urpAsset, "m_ShadowCascadeCount", initialCascadeCount);
            SetVector3Property(urpAsset, "m_Cascade4Split", initialSplit);
        }

        [Test]
        public void URPAsset_ApplyEnablesSoftShadowsPCF7x7()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            Assert.IsNotNull(urpAsset);

            var so = new SerializedObject(urpAsset);
            bool initialSoftShadows = so.FindProperty("m_SoftShadowsSupported").boolValue;
            int initialQuality = so.FindProperty("m_SoftShadowQuality").intValue;

            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();

            so.Update();
            Assert.IsTrue(so.FindProperty("m_SoftShadowsSupported").boolValue,
                "Soft shadows should be enabled after apply.");
            Assert.AreEqual(3, so.FindProperty("m_SoftShadowQuality").intValue,
                "Soft shadow quality should be 3 (PCF 7x7 / High).");

            // Restore
            SetProperty(urpAsset, "m_SoftShadowsSupported", initialSoftShadows);
            SetProperty(urpAsset, "m_SoftShadowQuality", initialQuality);
        }

        [Test]
        public void URPAsset_ApplyEnablesAdditionalLightShadowsWith4096Resolution()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            Assert.IsNotNull(urpAsset);

            var so = new SerializedObject(urpAsset);
            bool initialAdditional = so.FindProperty("m_AdditionalLightShadowsSupported").boolValue;
            int initialRes = so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue;

            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();

            so.Update();
            Assert.IsTrue(so.FindProperty("m_AdditionalLightShadowsSupported").boolValue,
                "Additional light shadows should be enabled after apply.");
            Assert.AreEqual(4096, so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue,
                "Additional lights shadow resolution should be 4096 after apply.");

            // Restore
            SetProperty(urpAsset, "m_AdditionalLightShadowsSupported", initialAdditional);
            SetProperty(urpAsset, "m_AdditionalLightsShadowmapResolution", initialRes);
        }

        // ================================================================
        // Part 3: SSAO Renderer Feature Tests
        // ================================================================

        [Test]
        public void SSAO_ApplyAddsScreenSpaceAmbientOcclusionFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Assert.IsNotNull(rendererData);

            // Remove SSAO first if present to ensure clean test
            RemoveAllSSAOFeatures(rendererData);

            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();

            // Check feature was added
            bool hasSSAO = false;
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion)
                {
                    hasSSAO = true;
                    Assert.AreEqual("SSAO", feature.name, "SSAO feature should be named 'SSAO'.");
                    break;
                }
            }
            Assert.IsTrue(hasSSAO, "SSAO Renderer Feature should be present after apply.");

            // Cleanup - remove the SSAO we added
            RemoveAllSSAOFeatures(rendererData);
        }

        [Test]
        public void SSAO_ApplyConfiguresSettingsCorrectly()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Assert.IsNotNull(rendererData);

            RemoveAllSSAOFeatures(rendererData);

            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();

            // Find SSAO feature
            ScreenSpaceAmbientOcclusion ssao = null;
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion ssaoFeature)
                {
                    ssao = ssaoFeature;
                    break;
                }
            }
            Assert.IsNotNull(ssao, "SSAO feature must exist after apply.");

            // Verify settings via SerializedObject
            var ssaoSo = new SerializedObject(ssao);
            Assert.AreEqual(1.0f, ssaoSo.FindProperty("m_Settings.Intensity").floatValue, 0.001f,
                "SSAO Intensity should be 1.0.");
            Assert.AreEqual(0.5f, ssaoSo.FindProperty("m_Settings.Radius").floatValue, 0.001f,
                "SSAO Radius should be 0.5.");
            Assert.AreEqual(0.25f, ssaoSo.FindProperty("m_Settings.DirectLightingStrength").floatValue, 0.001f,
                "SSAO DirectLightingStrength should be 0.25.");
            Assert.AreEqual(1, ssaoSo.FindProperty("m_Settings.Samples").intValue,
                "SSAO Samples should be Medium (1).");
            Assert.AreEqual(1, ssaoSo.FindProperty("m_Settings.Source").intValue,
                "SSAO Source should be DepthNormals (1).");

            // Cleanup
            RemoveAllSSAOFeatures(rendererData);
        }

        [Test]
        public void SSAO_ApplyDoesNotAddDuplicateFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Assert.IsNotNull(rendererData);

            RemoveAllSSAOFeatures(rendererData);

            // First apply
            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();
            int countAfterFirst = CountSSAOFeatures(rendererData);

            // Second apply (should be idempotent)
            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();
            int countAfterSecond = CountSSAOFeatures(rendererData);

            Assert.AreEqual(1, countAfterFirst, "Should have exactly 1 SSAO feature after first apply.");
            Assert.AreEqual(1, countAfterSecond, "Should still have exactly 1 SSAO feature after second apply (no duplicates).");

            // Cleanup
            RemoveAllSSAOFeatures(rendererData);
        }

        // ================================================================
        // Part 4: Reset Tests
        // ================================================================

        [Test]
        public void Reset_RemovesSSAOFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Assert.IsNotNull(rendererData);

            // First apply to add SSAO
            RemoveAllSSAOFeatures(rendererData);
            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();
            Assert.AreEqual(1, CountSSAOFeatures(rendererData), "SSAO should be present after apply.");

            // Reset
            PhaseG1_SSAO_ShadowSetup.ResetGraphicsDefaults();

            Assert.AreEqual(0, CountSSAOFeatures(rendererData),
                "SSAO feature should be removed after reset.");
        }

        [Test]
        public void Reset_RestoresShadowResolutionTo2048()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            Assert.IsNotNull(urpAsset);

            var so = new SerializedObject(urpAsset);
            int initialRes = so.FindProperty("m_MainLightShadowmapResolution").intValue;

            // Apply changes
            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();
            so.Update();
            Assert.AreEqual(4096, so.FindProperty("m_MainLightShadowmapResolution").intValue,
                "Should be 4096 after apply.");

            // Reset
            PhaseG1_SSAO_ShadowSetup.ResetGraphicsDefaults();
            so.Update();

            Assert.AreEqual(2048, so.FindProperty("m_MainLightShadowmapResolution").intValue,
                "Main light shadow resolution should be 2048 after reset.");

            // Restore original
            SetProperty(urpAsset, "m_MainLightShadowmapResolution", initialRes);
        }

        [Test]
        public void Reset_DisablesSoftShadows()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            Assert.IsNotNull(urpAsset);

            var so = new SerializedObject(urpAsset);
            bool initialSoft = so.FindProperty("m_SoftShadowsSupported").boolValue;

            PhaseG1_SSAO_ShadowSetup.ApplySSAOAndShadow();
            PhaseG1_SSAO_ShadowSetup.ResetGraphicsDefaults();
            so.Update();

            Assert.IsFalse(so.FindProperty("m_SoftShadowsSupported").boolValue,
                "Soft shadows should be disabled after reset.");

            // Restore
            SetProperty(urpAsset, "m_SoftShadowsSupported", initialSoft);
        }

        // ================================================================
        // Part 5: Edge Cases
        // ================================================================

        [Test]
        public void SSAO_ResetWithoutSSAO_DoesNotThrow()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Assert.IsNotNull(rendererData);

            RemoveAllSSAOFeatures(rendererData);

            // Reset when no SSAO exists — should not throw
            Assert.DoesNotThrow(() => PhaseG1_SSAO_ShadowSetup.ResetGraphicsDefaults(),
                "Reset should not throw when no SSAO feature exists.");
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static int CountSSAOFeatures(UniversalRendererData rendererData)
        {
            int count = 0;
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion)
                    count++;
            }
            return count;
        }

        private static void RemoveAllSSAOFeatures(UniversalRendererData rendererData)
        {
            var so = new SerializedObject(rendererData);
            var featuresProp = so.FindProperty("m_RendererFeatures");
            if (featuresProp == null) return;

            for (int i = featuresProp.arraySize - 1; i >= 0; i--)
            {
                var element = featuresProp.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue is ScreenSpaceAmbientOcclusion)
                {
                    featuresProp.DeleteArrayElementAtIndex(i);
                }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);
        }

        private static void SetProperty(UniversalRenderPipelineAsset asset, string propertyPath, bool value)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty(propertyPath);
            if (prop != null) prop.boolValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetProperty(UniversalRenderPipelineAsset asset, string propertyPath, int value)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty(propertyPath);
            if (prop != null) prop.intValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetVector3Property(UniversalRenderPipelineAsset asset, string propertyPath, Vector3 value)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty(propertyPath);
            if (prop != null) prop.vector3Value = value;
            so.ApplyModifiedProperties();
        }
    }
}