#if false
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Linq;
using ProjectName.Systems;
using ProjectName.Core;

namespace ProjectName.Editor
{
    /// <summary>
    /// Phase G1-03: Reflection Probes + Screen Space Reflections setup.
    /// Places 6 Reflection Probes across the map, adds a ScreenSpaceReflections
    /// Renderer Feature to the URP Renderer, and upgrades water materials
    /// with reflection keywords and smoothness/metalness.
    /// </summary>
    public static class PhaseG1_ReflectionSetup
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string RendererPath = "Assets/Scenes/New Universal Render Pipeline Asset_Renderer.asset";

        // Probe names for identification/cleanup
        private const string ProbePrefix = "G1_ReflectionProbe_";
        private const string ProbeEast = ProbePrefix + "East";
        private const string ProbeWest = ProbePrefix + "West";
        private const string ProbeSouth = ProbePrefix + "South";
        private const string ProbeNorth = ProbePrefix + "North";
        private const string ProbeEmpire = ProbePrefix + "Empire";
        private const string ProbeCenter = ProbePrefix + "Center";

        // ================================================================
        //  Apply Reflections
        // ================================================================

        /// <summary>
        /// Places 6 reflection probes, adds SSR renderer feature, upgrades water materials.
        /// </summary>
        [MenuItem("Tools/Phase G1/Apply Reflections")]
        public static void ApplyReflections()
        {
            var scene = GetOrOpenMainScene();
            if (scene == null)
            {
                Debug.LogError("[PhaseG1] MainScene not found.");
                return;
            }

            // 1) Place reflection probes
            PlaceReflectionProbes();

            // 2) Add SSR Renderer Feature
            AddSSRRendererFeature();

            // 3) Upgrade water materials
            UpgradeWaterMaterials();

            // Save scene
            string scenePath = scene.Value.path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = MainScenePath;
            EditorSceneManager.SaveScene(scene.Value, scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG1] ✅ Reflections applied: 6 probes + SSR + water materials upgraded.");
            EditorUtility.DisplayDialog("Phase G1-03",
                "6 Reflection Probes placed.\nSSR Renderer Feature added.\nWater materials upgraded.",
                "OK");
        }

        [MenuItem("Tools/Phase G1/Apply Reflections", true)]
        private static bool ValidateApplyReflections() => true;

        // ================================================================
        //  Clear Reflections
        // ================================================================

        /// <summary>
        /// Removes all Phase G1 reflection probes and SSR renderer feature.
        /// </summary>
        [MenuItem("Tools/Phase G1/Clear Reflections")]
        public static void ClearReflections()
        {
            var scene = GetOrOpenMainScene();
            if (scene == null)
            {
                Debug.LogError("[PhaseG1] MainScene not found.");
                return;
            }

            // 1) Remove reflection probes
            int removedProbes = RemoveReflectionProbes();

            // 2) Remove SSR Renderer Feature
            bool removedSsr = RemoveSSRRendererFeature();

            // Save scene
            string scenePath = scene.Value.path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = MainScenePath;
            EditorSceneManager.SaveScene(scene.Value, scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PhaseG1] ✅ Reflections cleared: {removedProbes} probes removed, SSR removed={removedSsr}.");
            EditorUtility.DisplayDialog("Phase G1-03",
                $"{removedProbes} probes removed.\nSSR removed: {removedSsr}.",
                "OK");
        }

        [MenuItem("Tools/Phase G1/Clear Reflections", true)]
        private static bool ValidateClearReflections() => true;

        // ================================================================
        //  Reflection Probe Placement
        // ================================================================

        private static void PlaceReflectionProbes()
        {
            // East (+250, 10, 0) — green/grassland
            CreateProbe(ProbeEast, new Vector3(250f, 10f, 0f), 128, false, 0.3f, 500f);
            // West (-250, 10, 0) — yellow/desert
            CreateProbe(ProbeWest, new Vector3(-250f, 10f, 0f), 128, false, 0.3f, 500f);
            // South (0, 10, -250) — red/volcanic
            CreateProbe(ProbeSouth, new Vector3(0f, 10f, -250f), 128, false, 0.3f, 500f);
            // North (0, 10, 250) — gray/tundra
            CreateProbe(ProbeNorth, new Vector3(0f, 10f, 250f), 128, false, 0.3f, 500f);
            // Empire (0, 15, 0) — golden/marble (higher res)
            CreateProbe(ProbeEmpire, new Vector3(0f, 15f, 0f), 256, false, 0.3f, 500f);
            // Center fallback (0, 50, 0) — generic sky
            CreateProbe(ProbeCenter, new Vector3(0f, 50f, 0f), 128, false, 0.3f, 500f);
        }

        private static void CreateProbe(string name, Vector3 position, int resolution,
            bool boxProjection, float nearClip, float farClip)
        {
            // Check if probe already exists
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Debug.Log($"[PhaseG1] Probe '{name}' already exists. Skipping.");
                return;
            }

            GameObject go = new GameObject(name);
            go.transform.position = position;

            ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution = resolution;
            probe.boxProjection = boxProjection;
            probe.nearClipPlane = nearClip;
            probe.farClipPlane = farClip;
            probe.size = Vector3.one * 500f;
            probe.boxSize = Vector3.one * 500f;
            probe.hdr = true;
            probe.shadowDistance = 100f;
            probe.cullingMask = -1; // Everything

            // Set importance: Empire highest, Center second, rest default
            if (name == ProbeEmpire)
                probe.importance = 2;
            else if (name == ProbeCenter)
                probe.importance = 1;
            else
                probe.importance = 0;

            Debug.Log($"[PhaseG1] Created ReflectionProbe '{name}' at {position}, resolution={resolution}.");
        }

        // ================================================================
        //  Reflection Probe Removal
        // ================================================================

        private static int RemoveReflectionProbes()
        {
            int count = 0;
            string[] probeNames = { ProbeEast, ProbeWest, ProbeSouth, ProbeNorth, ProbeEmpire, ProbeCenter };

            foreach (string name in probeNames)
            {
                GameObject go = GameObject.Find(name);
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                    count++;
                    Debug.Log($"[PhaseG1] Removed probe '{name}'.");
                }
            }
            return count;
        }

        // ================================================================
        //  SSR Renderer Feature
        // ================================================================

        private static void AddSSRRendererFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"[PhaseG1] Renderer not found at '{RendererPath}'.");
                return;
            }

            // Check if SSR already exists
            if (rendererData.rendererFeatures.Any(f => f is ScreenSpaceReflections))
            {
                Debug.Log("[PhaseG1] SSR feature already exists. Skipping.");
                return;
            }

            // Create the SSR feature
            var ssr = ScriptableObject.CreateInstance<ScreenSpaceReflections>();
            ssr.name = "ScreenSpaceReflections";

            // Configure SSR settings
            var ssrSettings = ssr.Settings;
            ssrSettings.Resolution = ScreenSpaceReflectionsSettings.ResolutionMode.Half;
            ssrSettings.MaxRaySteps = 64;
            ssrSettings.RayLength = 0.5f;
            ssrSettings.Thickness = 1.0f;
            ssrSettings.Quality = ScreenSpaceReflectionsSettings.QualityMode.High;

            // Add feature to renderer via SerializedObject
            var rendererSo = new SerializedObject(rendererData);
            var featuresProp = rendererSo.FindProperty("m_RendererFeatures");
            if (featuresProp == null)
            {
                Debug.LogError("[PhaseG1] Could not find m_RendererFeatures on renderer data.");
                return;
            }

            featuresProp.arraySize++;
            var newFeature = featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1);
            newFeature.objectReferenceValue = ssr;
            rendererSo.ApplyModifiedProperties();

            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(ssr);

            Debug.Log("[PhaseG1] ✅ SSR Renderer Feature added and configured.");
        }

        private static bool RemoveSSRRendererFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"[PhaseG1] Renderer not found at '{RendererPath}'.");
                return false;
            }

            var rendererSo = new SerializedObject(rendererData);
            var featuresProp = rendererSo.FindProperty("m_RendererFeatures");
            if (featuresProp == null) return false;

            bool removed = false;
            for (int i = featuresProp.arraySize - 1; i >= 0; i--)
            {
                var element = featuresProp.GetArrayElementAtIndex(i);
                var featureRef = element.objectReferenceValue;
                if (featureRef is ScreenSpaceReflections)
                {
                    featuresProp.DeleteArrayElementAtIndex(i);
                    removed = true;
                }
            }

            if (removed)
            {
                rendererSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(rendererData);
                Debug.Log("[PhaseG1] ✅ SSR Renderer Feature removed.");
            }
            else
            {
                Debug.Log("[PhaseG1] No SSR feature found to remove.");
            }

            return removed;
        }

        // ================================================================
        //  Water Material Upgrades
        // ================================================================

        /// <summary>
        /// Scenes all WaterBody and LakeGenerator instances in the scene
        /// and upgrades their water surface materials with reflection keywords
        /// and metallic/smoothness settings via their public API.
        /// </summary>
        private static void UpgradeWaterMaterials()
        {
            int upgradedCount = 0;

            // Upgrade WaterBody materials
            var waterBodies = Object.FindObjectsByType<WaterBody>(FindObjectsInactive.Include);
            foreach (var wb in waterBodies)
            {
                wb.UpgradeReflectionMaterial();
                upgradedCount++;
                Debug.Log($"[PhaseG1] Upgraded material on WaterBody '{wb.name}'.");
            }

            // Upgrade LakeGenerator materials
            var lakeGens = Object.FindObjectsByType<LakeGenerator>(FindObjectsInactive.Include);
            foreach (var lg in lakeGens)
            {
                lg.UpgradeReflectionMaterial();
                upgradedCount++;
                Debug.Log($"[PhaseG1] Upgraded material on LakeGenerator '{lg.name}'.");
            }

            Debug.Log($"[PhaseG1] ✅ Upgraded {upgradedCount} water materials with reflection settings.");
        }

        // ================================================================
        //  Probe Count Query (for tests)
        // ================================================================

        /// <summary>
        /// Returns the number of Phase G1 reflection probes currently in the scene.
        /// Used by EditMode tests.
        /// </summary>
        public static int CountActiveProbes()
        {
            int count = 0;
            string[] probeNames = { ProbeEast, ProbeWest, ProbeSouth, ProbeNorth, ProbeEmpire, ProbeCenter };
            foreach (string name in probeNames)
            {
                if (GameObject.Find(name) != null)
                    count++;
            }
            return count;
        }

        // ================================================================
        //  Helpers
        // ================================================================

        private static UnityEngine.SceneManagement.Scene? GetOrOpenMainScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path) && scene.path.Contains("MainScene"))
                return scene;

            string[] guids = AssetDatabase.FindAssets("t:Scene MainScene");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            Debug.LogWarning("[PhaseG1] MainScene not found. Creating new scene...");
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            newScene.name = "MainScene";
            return newScene;
        }
    }
}
#endif