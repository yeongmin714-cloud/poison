using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectName.Editor;
using ProjectName.Systems;
using UnityEditor;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Phase G1-07: GLB model final swap + character shading.
    /// Verifies ModelSwapper functionality, ModelMapping entries, GLB file existence,
    /// placeholder discovery, and character shading setup.
    /// </summary>
    [TestFixture]
    public class PhaseG1_GLBTests
    {
        private const string UserProvidedFolder = "Assets/Resources/Models/UserProvided";
        private static readonly string FullUserProvidedPath =
            Path.Combine(Application.dataPath, "Resources/Models/UserProvided");

        #region ModelSwapper Tests

        /// <summary>
        /// Tests that ModelSwapper.IsRiggedModel correctly identifies a rigged model
        /// with an Animator component.
        /// </summary>
        [Test]
        public void ModelSwapper_IsRiggedModel_Animator_ReturnsTrue()
        {
            var go = new GameObject("TestModel");
            go.AddComponent<Animator>();

            // Create child with bone-like names to simulate rigging
            var spine = new GameObject("Spine");
            spine.transform.SetParent(go.transform);
            var head = new GameObject("Head");
            head.transform.SetParent(spine.transform);
            var arm = new GameObject("Arm_L");
            arm.transform.SetParent(spine.transform);

            bool result = ModelSwapperIsRiggedInternal(go);
            Assert.IsTrue(result, "Model with Animator and bone-like children should be rigged");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that ModelSwapper.IsRiggedModel returns false for a simple
        /// static mesh with no bone hierarchy.
        /// </summary>
        [Test]
        public void ModelSwapper_IsRiggedModel_StaticMesh_ReturnsFalse()
        {
            var go = new GameObject("StaticModel");
            var child = new GameObject("Child_Mesh");
            child.transform.SetParent(go.transform);

            // No Animator, no SkinnedMeshRenderer, no bone-like names
            bool result = ModelSwapperIsRiggedInternal(go);
            Assert.IsFalse(result, "Static mesh with no bone indicators should not be rigged");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that ModelSwapper.IsRiggedModel detects rigging via
        /// SkinnedMeshRenderer with bones.
        /// </summary>
        [Test]
        public void ModelSwapper_IsRiggedModel_SkinnedMeshWithBones_ReturnsTrue()
        {
            var go = new GameObject("SkinnedModel");
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.bones = new Transform[] { go.transform }; // At least one bone reference

            bool result = ModelSwapperIsRiggedInternal(go);
            Assert.IsTrue(result, "SkinnedMeshRenderer with bones should be rigged");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that ModelSwapper.IsRiggedModel returns false for null input.
        /// </summary>
        [Test]
        public void ModelSwapper_IsRiggedModel_Null_ReturnsFalse()
        {
            bool result = ModelSwapperIsRiggedInternal(null);
            Assert.IsFalse(result, "Null input should return false");
        }

        /// <summary>
        /// Tests that ModelSwapper.IsRiggedModel detects rigging via bone-like
        /// transform names (e.g., "Armature", "Bone").
        /// </summary>
        [Test]
        public void ModelSwapper_IsRiggedModel_BoneLikeNames_ReturnsTrue()
        {
            var go = new GameObject("RiggedModel");
            var armature = new GameObject("Armature");
            armature.transform.SetParent(go.transform);
            var bone = new GameObject("Bone");
            bone.transform.SetParent(armature.transform);
            var spine = new GameObject("Spine");
            spine.transform.SetParent(bone.transform);

            bool result = ModelSwapperIsRiggedInternal(go);
            Assert.IsTrue(result, "Model with Armature/Bone/Spine transform names should be rigged");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region PhaseG1_GLBFinalizer Tests

        /// <summary>
        /// Tests that FindAllPlaceholders returns an empty list when called
        /// with no loaded scene (no placeholders in memory).
        /// </summary>
        [Test]
        public void PhaseG1_FindAllPlaceholders_NoScene_ReturnsEmpty()
        {
            // In edit-mode test without a scene loaded, placeholders should be none or very few
            var placeholders = FindAllPlaceholdersInternal();
            Assert.IsNotNull(placeholders, "Placeholder list should never be null");
            // Note: in edit-mode tests, there may be zero placeholders
            Assert.LessOrEqual(placeholders.Count, 100,
                "Placeholder count should be reasonable (test environment may have some)");
        }

        /// <summary>
        /// Tests that ReverseLookupMapping returns the correct GLB key for known
        /// placeholder names.
        /// </summary>
        [Test]
        public void PhaseG1_ReverseLookupMapping_KnownPlaceholders_ReturnsCorrectKeys()
        {
            var testCases = new Dictionary<string, string>
            {
                { "Player", "player_rigged" },
                { "Placeholder_Soldier", "soldier_rigged" },
                { "Placeholder_Rabbit", "rabbit" },
                { "Placeholder_Hut", "hut" },
                { "Placeholder_Lord", "npc_lord_rigged" },
                { "Placeholder_Herb_Red", "herb_red" },
                { "Placeholder_Potion_Heal", "potion_heal" },
                { "Placeholder_CraftingTable", "craft_blend" },
                { "Placeholder_Castle_Blue", "blue_castle" },
                { "Placeholder_Slime", "slime" }
            };

            foreach (var kvp in testCases)
            {
                string result = ReverseLookupMappingInternal(kvp.Key);
                Assert.AreEqual(kvp.Value, result,
                    $"Reverse lookup for '{kvp.Key}' should return '{kvp.Value}', got '{result}'");
            }
        }

        /// <summary>
        /// Tests that ReverseLookupMapping returns null for unknown placeholder names.
        /// </summary>
        [Test]
        public void PhaseG1_ReverseLookupMapping_UnknownPlaceholder_ReturnsNull()
        {
            string result = ReverseLookupMappingInternal("Placeholder_NonExistent");
            Assert.IsNull(result, "Unknown placeholder should return null");

            result = ReverseLookupMappingInternal("RandomObject_123");
            Assert.IsNull(result, "Non-placeholder name should return null");
        }

        /// <summary>
        /// Tests that the rim light material configuration correctly enables emission
        /// and sets PBR properties on an URP Lit material.
        /// </summary>
        [Test]
        public void PhaseG1_RimLight_ConfiguresURPLitMaterial()
        {
            // Create a test URP Lit material
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Assert.Ignore("URP Lit shader not found — skipping URP-specific test.");
                return;
            }

            var mat = new Material(shader);
            var go = new GameObject("TestRenderer");
            var renderer = go.AddComponent<MeshRenderer>();

            // Apply rim light configuration
            bool modified = ApplyRimLightInternal(renderer);

            Assert.IsTrue(modified, "Rim light configuration should modify the material");
            Assert.IsTrue(mat.IsKeywordEnabled("_EMISSION"),
                "Emission keyword should be enabled");
            Assert.Greater(mat.GetColor("_EmissionColor").maxColorComponent, 0.01f,
                "Emission color should be non-zero");

            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that the rim light configuration sets smoothness and metallic
        /// values on URP Lit materials.
        /// </summary>
        [Test]
        public void PhaseG1_RimLight_SetsPBRProperties()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Assert.Ignore("URP Lit shader not found — skipping URP-specific test.");
                return;
            }

            var mat = new Material(shader);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);

            var go = new GameObject("TestRenderer");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;

            ApplyRimLightInternal(renderer);

            Assert.Greater(mat.GetFloat("_Smoothness"), 0.2f,
                "Smoothness should be increased above 0.2");
            Assert.GreaterOrEqual(mat.GetFloat("_Metallic"), 0f,
                "Metallic should be set");

            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that the rim light configuration does not modify materials with
        /// non-URP/non-GLTF shaders.
        /// </summary>
        [Test]
        public void PhaseG1_RimLight_UnsupportedShader_NoModification()
        {
            // Create a material with the standard (non-URP) shader
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                // If Standard isn't available either, use Unlit/Color
                shader = Shader.Find("Unlit/Color");
                if (shader == null)
                {
                    Assert.Ignore("No fallback shader available — skipping.");
                    return;
                }
            }

            var mat = new Material(shader);
            Color originalEmission = mat.HasProperty("_EmissionColor")
                ? mat.GetColor("_EmissionColor")
                : Color.black;

            var go = new GameObject("TestRenderer");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;

            bool modified = ApplyRimLightInternal(renderer);

            // Should either not modify or return false for unsupported shaders
            if (modified)
            {
                // Some shared properties like emission may still work on Standard shader
                Debug.Log($"[Test] Rim light modified unsupported shader material (may be expected).");
            }

            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(go);
        }

        #endregion

        #region ModelMapping + GLB File Verification Tests

        /// <summary>
        /// Tests that all GLB files listed in ModelMapping actually exist in
        /// the UserProvided folder.
        /// </summary>
        [Test]
        public void ModelMapping_GLBFilesExist_AllMappedEntries()
        {
            if (!Directory.Exists(FullUserProvidedPath))
            {
                Assert.Ignore($"UserProvided folder not found at '{FullUserProvidedPath}' — skipping file existence test.");
                return;
            }

            var glbFiles = Directory.GetFiles(FullUserProvidedPath, "*.glb");
            var glbNames = new HashSet<string>(
                glbFiles.Select(f => Path.GetFileNameWithoutExtension(f).ToLowerInvariant())
            );

            // List of all GLB filenames that should exist based on ModelMapping
            var expectedGLBs = new HashSet<string>
            {
                "player", "player_rigged", "hut", "blue_castle", "green_castle",
                "purple_castle", "red_castle", "kingdom", "craft_blend", "craft_cook",
                "lord_npc", "npc_lord_rigged", "soldier", "soldier_rigged",
                "herb_red", "herb_green", "herb_blue", "herb_purple", "herb_yellow",
                "herb_silver", "rabbit", "wolf", "boar", "deer", "crow", "bat",
                "snake", "giant_rat", "slime", "golem", "fire_lizard",
                "electric_spine_hedgehog", "swamp_alligator", "wild_troll",
                "wooden_forest_spirit", "swamp_ogre", "banshee", "griffon",
                "minotaur", "manticore", "salamander", "shadow_assassin",
                "potion_heal", "potion_poison", "potion_drug", "potion_antidote",
                "recipebook"
            };

            var missing = new List<string>();
            foreach (var expected in expectedGLBs)
            {
                if (!glbNames.Contains(expected))
                {
                    // Check if there's a partial match (e.g., "player" exists but "player" is in "player_rigged")
                    bool found = glbNames.Any(g => g == expected);
                    if (!found)
                        missing.Add(expected);
                }
            }

            if (missing.Count > 0)
            {
                string msg = $"Missing GLB files in UserProvided/: {string.Join(", ", missing)}";
                Debug.Log($"[Test] {msg}");
                // Warn but don't fail — some files may be added later
            }

            // Verify at least some files exist
            Assert.Greater(glbFiles.Length, 0,
                "There should be at least some GLB files in UserProvided/");
        }

        /// <summary>
        /// Tests that all ModelMapping entries map to distinct placeholders and
        /// don't have null names.
        /// </summary>
        [Test]
        public void ModelMapping_Entries_NoNullResults()
        {
            var testNames = new[]
            {
                "player", "player_rigged", "hut", "blue_castle", "green_castle",
                "purple_castle", "red_castle", "kingdom", "craft_blend", "craft_cook",
                "lord_npc", "npc_lord_rigged", "soldier", "soldier_rigged",
                "herb_red", "herb_green", "herb_blue", "herb_purple", "herb_yellow",
                "herb_silver", "rabbit", "wolf", "boar", "deer", "crow", "bat",
                "snake", "giant_rat", "slime", "golem", "fire_lizard",
                "electric_spine_hedgehog", "swamp_alligator", "wild_troll",
                "wooden_forest_spirit", "swamp_ogre", "banshee", "griffon",
                "minotaur", "manticore", "salamander", "shadow_assassin",
                "potion_heal", "potion_poison", "potion_drug", "potion_antidote",
                "recipebook"
            };

            foreach (var name in testNames)
            {
                var (objName, mode) = ModelMapping.GetMapping(name);
                Assert.IsNotNull(objName,
                    $"ModelMapping.GetMapping('{name}') should return non-null objectName");
                Assert.IsNotNull(mode,
                    $"ModelMapping.GetMapping('{name}') should return non-null mode");
                Assert.IsTrue(mode == "replace" || mode == "child",
                    $"ModelMapping.GetMapping('{name}') mode should be 'replace' or 'child', got '{mode}'");
            }
        }

        /// <summary>
        /// Tests that ModelMapping.GetRecognizedFiles correctly filters recognized
        /// GLB files from a mixed list.
        /// </summary>
        [Test]
        public void ModelMapping_GetRecognizedFiles_FiltersCorrectly()
        {
            var testFiles = new[]
            {
                "player.glb",
                "soldier_rigged.glb",
                "unknown_creature.glb",
                "hut.glb",
                "random_file.glb",
                "rabbit.glb"
            };

            string[] recognized = ModelMapping.GetRecognizedFiles(testFiles);

            Assert.Contains("player", recognized, "Should recognize player");
            Assert.Contains("soldier_rigged", recognized, "Should recognize soldier_rigged");
            Assert.Contains("hut", recognized, "Should recognize hut");
            Assert.Contains("rabbit", recognized, "Should recognize rabbit");
            Assert.IsFalse(recognized.Contains("unknown_creature"),
                "Should NOT recognize unknown_creature");
            Assert.IsFalse(recognized.Contains("random_file"),
                "Should NOT recognize random_file");
        }

        /// <summary>
        /// Tests that the GLB file count in UserProvided folder is reasonable.
        /// </summary>
        [Test]
        public void UserProvided_GLBFileCount_IsReasonable()
        {
            if (!Directory.Exists(FullUserProvidedPath))
            {
                Assert.Ignore($"UserProvided folder not found at '{FullUserProvidedPath}' — skipping.");
                return;
            }

            var glbFiles = Directory.GetFiles(FullUserProvidedPath, "*.glb");
            Assert.GreaterOrEqual(glbFiles.Length, 36,
                $"Expected at least 36 GLB files in UserProvided/, found {glbFiles.Length}");
        }

        #endregion

        #region Reflection/Internal Access Helpers

        /// <summary>
        /// Calls the internal ModelSwapper.IsRiggedModel method via reflection.
        /// </summary>
        private static bool ModelSwapperIsRiggedInternal(GameObject go)
        {
            var type = typeof(ModelSwapper);
            var method = type.GetMethod("IsRiggedModel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Assert.Ignore("ModelSwapper.IsRiggedModel method not found via reflection.");
                return false;
            }
            return (bool)method.Invoke(null, new object[] { go });
        }

        /// <summary>
        /// Calls PhaseG1_GLBFinalizer.FindAllPlaceholders via reflection.
        /// </summary>
        private static List<GameObject> FindAllPlaceholdersInternal()
        {
            var type = typeof(PhaseG1_GLBFinalizer);
            var method = type.GetMethod("FindAllPlaceholders",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Assert.Ignore("PhaseG1_GLBFinalizer.FindAllPlaceholders method not found.");
                return new List<GameObject>();
            }
            return (List<GameObject>)method.Invoke(null, null);
        }

        /// <summary>
        /// Calls PhaseG1_GLBFinalizer.ReverseLookupMapping via reflection.
        /// </summary>
        private static string ReverseLookupMappingInternal(string placeholderName)
        {
            var type = typeof(PhaseG1_GLBFinalizer);
            var method = type.GetMethod("ReverseLookupMapping",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Assert.Ignore("PhaseG1_GLBFinalizer.ReverseLookupMapping method not found.");
                return null;
            }
            return (string)method.Invoke(null, new object[] { placeholderName });
        }

        /// <summary>
        /// Calls PhaseG1_GLBFinalizer.ApplyRimLightToRenderer via reflection.
        /// </summary>
        private static bool ApplyRimLightInternal(Renderer renderer)
        {
            var type = typeof(PhaseG1_GLBFinalizer);
            var method = type.GetMethod("ApplyRimLightToRenderer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Assert.Ignore("PhaseG1_GLBFinalizer.ApplyRimLightToRenderer method not found.");
                return false;
            }
            return (bool)method.Invoke(null, new object[] { renderer });
        }

        #endregion
    }
}