using UnityEditor;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Editor
{
    /// <summary>
    /// Phase G1-09: Editor tooling for upgrading WaterBody and LakeGenerator
    /// water surface materials to high-quality URP Lit water shader materials.
    /// Provides menu items to upgrade all water shaders or reset them to simple materials.
    /// </summary>
    public static class PhaseG1_WaterShaderSetup
    {
        private const string MenuRoot = "Tools/Phase G1/";

        // ================================================================
        //  Upgrade Water Shaders
        // ================================================================

        /// <summary>
        /// Finds all WaterBody and LakeGenerator objects in the scene and replaces
        /// their surface materials with the upgraded URP Lit water material.
        /// </summary>
        [MenuItem(MenuRoot + "Upgrade Water Shaders")]
        public static void UpgradeWaterShaders()
        {
            int upgradedCount = 0;
            int skippedCount = 0;

            // Upgrade WaterBody instances
            var waterBodies = Object.FindObjectsByType<WaterBody>(FindObjectsInactive.Include);
            foreach (var wb in waterBodies)
            {
                if (TryUpgradeWaterBodyMaterial(wb))
                    upgradedCount++;
                else
                    skippedCount++;
            }

            // Upgrade LakeGenerator instances
            var lakeGens = Object.FindObjectsByType<LakeGenerator>(FindObjectsInactive.Include);
            foreach (var lg in lakeGens)
            {
                if (TryUpgradeLakeGeneratorMaterial(lg))
                    upgradedCount++;
                else
                    skippedCount++;
            }

            Debug.Log($"[PhaseG1-09] ✅ Upgraded {upgradedCount} water shaders ({skippedCount} skipped).");
            if (upgradedCount > 0 || skippedCount > 0)
            {
                EditorUtility.DisplayDialog("Phase G1-09",
                    $"Water shader upgrade complete.\n{upgradedCount} upgraded\n{skippedCount} skipped",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Phase G1-09",
                    "No WaterBody or LakeGenerator objects found in the scene.",
                    "OK");
            }
        }

        [MenuItem(MenuRoot + "Upgrade Water Shaders", true)]
        private static bool ValidateUpgradeWaterShaders() => true;

        // ================================================================
        //  Reset Water Shaders
        // ================================================================

        /// <summary>
        /// Finds all WaterBody and LakeGenerator objects and restores their
        /// surface materials to the original simple transparent material.
        /// </summary>
        [MenuItem(MenuRoot + "Reset Water Shaders")]
        public static void ResetWaterShaders()
        {
            int resetCount = 0;

            // Reset WaterBody instances
            var waterBodies = Object.FindObjectsByType<WaterBody>(FindObjectsInactive.Include);
            foreach (var wb in waterBodies)
            {
                if (TryResetWaterBodyMaterial(wb))
                    resetCount++;
            }

            // Reset LakeGenerator instances
            var lakeGens = Object.FindObjectsByType<LakeGenerator>(FindObjectsInactive.Include);
            foreach (var lg in lakeGens)
            {
                if (TryResetLakeGeneratorMaterial(lg))
                    resetCount++;
            }

            Debug.Log($"[PhaseG1-09] 🔄 Reset {resetCount} water shaders to simple materials.");
            EditorUtility.DisplayDialog("Phase G1-09",
                $"Water shader reset complete.\n{resetCount} materials restored.",
                "OK");
        }

        [MenuItem(MenuRoot + "Reset Water Shaders", true)]
        private static bool ValidateResetWaterShaders() => true;

        // ================================================================
        //  Internal Helpers
        // ================================================================

        private static bool TryUpgradeWaterBodyMaterial(WaterBody waterBody)
        {
            if (waterBody == null) return false;

            // Access the existing material via reflection-safe public API
            Material existingMat = waterBody.SurfaceMaterial;
            if (existingMat == null) return false;

            // Check if already upgraded
            if (WaterMaterialUpgrader.IsUpgradedWaterMaterial(existingMat))
            {
                Debug.Log($"[PhaseG1-09] WaterBody '{waterBody.name}' already upgraded. Skipping.");
                return false;
            }

            // Create upgraded material
            Material upgradedMat = WaterMaterialUpgrader.CreateUpgradedWaterMaterial(
                $"{waterBody.name}_WaterMat_Upgraded",
                shallowWeight: 0.5f
            );

            // Replace material on the surface renderer
            var surfaceRenderer = waterBody.WaterSurface?.GetComponent<MeshRenderer>();
            if (surfaceRenderer != null)
            {
                surfaceRenderer.material = upgradedMat;

                // Clean up old material if it was dynamically created
                if (existingMat != upgradedMat)
                {
                    Object.DestroyImmediate(existingMat);
                }

                Debug.Log($"[PhaseG1-09] ✅ Upgraded WaterBody '{waterBody.name}' material.");
                return true;
            }

            Object.DestroyImmediate(upgradedMat);
            return false;
        }

        private static bool TryUpgradeLakeGeneratorMaterial(LakeGenerator lakeGenerator)
        {
            if (lakeGenerator == null) return false;

            Material existingMat = lakeGenerator.SurfaceMaterial;
            if (existingMat == null) return false;

            if (WaterMaterialUpgrader.IsUpgradedWaterMaterial(existingMat))
            {
                Debug.Log($"[PhaseG1-09] LakeGenerator '{lakeGenerator.name}' already upgraded. Skipping.");
                return false;
            }

            Material upgradedMat = WaterMaterialUpgrader.CreateUpgradedWaterMaterial(
                $"{lakeGenerator.name}_LakeMat_Upgraded",
                shallowWeight: 0.5f
            );

            var surfaceRenderer = lakeGenerator.WaterSurface?.GetComponent<MeshRenderer>();
            if (surfaceRenderer != null)
            {
                surfaceRenderer.material = upgradedMat;

                if (existingMat != upgradedMat)
                {
                    Object.DestroyImmediate(existingMat);
                }

                Debug.Log($"[PhaseG1-09] ✅ Upgraded LakeGenerator '{lakeGenerator.name}' material.");
                return true;
            }

            Object.DestroyImmediate(upgradedMat);
            return false;
        }

        private static bool TryResetWaterBodyMaterial(WaterBody waterBody)
        {
            if (waterBody == null) return false;

            Material existingMat = waterBody.SurfaceMaterial;
            Color originalColor = existingMat != null
                ? existingMat.color
                : new Color(0.2f, 0.5f, 0.8f, 0.6f);

            Material simpleMat = WaterMaterialUpgrader.CreateSimpleWaterMaterial(
                $"{waterBody.name}_WaterMat_Simple",
                originalColor
            );

            var surfaceRenderer = waterBody.WaterSurface?.GetComponent<MeshRenderer>();
            if (surfaceRenderer != null)
            {
                surfaceRenderer.material = simpleMat;

                if (existingMat != null && existingMat != simpleMat)
                {
                    Object.DestroyImmediate(existingMat);
                }

                Debug.Log($"[PhaseG1-09] 🔄 Reset WaterBody '{waterBody.name}' material to simple.");
                return true;
            }

            Object.DestroyImmediate(simpleMat);
            return false;
        }

        private static bool TryResetLakeGeneratorMaterial(LakeGenerator lakeGenerator)
        {
            if (lakeGenerator == null) return false;

            Material existingMat = lakeGenerator.SurfaceMaterial;
            Color originalColor = existingMat != null
                ? existingMat.color
                : new Color(0.2f, 0.5f, 0.8f, 0.6f);

            Material simpleMat = WaterMaterialUpgrader.CreateSimpleWaterMaterial(
                $"{lakeGenerator.name}_LakeMat_Simple",
                originalColor
            );

            var surfaceRenderer = lakeGenerator.WaterSurface?.GetComponent<MeshRenderer>();
            if (surfaceRenderer != null)
            {
                surfaceRenderer.material = simpleMat;

                if (existingMat != null && existingMat != simpleMat)
                {
                    Object.DestroyImmediate(existingMat);
                }

                Debug.Log($"[PhaseG1-09] 🔄 Reset LakeGenerator '{lakeGenerator.name}' material to simple.");
                return true;
            }

            Object.DestroyImmediate(simpleMat);
            return false;
        }
    }
}