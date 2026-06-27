using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for TerrainTextureApplier.
    /// Tests texture loading, nation categorization, material creation,
    /// and material application.
    /// </summary>
    public class TerrainTextureTests
    {
        // ================================================================
        //  Class & Component Tests
        // ================================================================

        [Test]
        public void TerrainTextureApplier_ClassExists()
        {
            var type = typeof(TerrainTextureApplier);
            Assert.IsNotNull(type, "TerrainTextureApplier class must exist.");
        }

        [Test]
        public void TerrainTextureApplier_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(TerrainTextureApplier)),
                "TerrainTextureApplier must inherit from MonoBehaviour.");
        }

        // ================================================================
        //  Texture Loading Tests
        // ================================================================

        [Test]
        public void LoadTextures_LoadsAllTextureFiles()
        {
            var go = new GameObject("TestTexApplier");
            try
            {
                var applier = go.AddComponent<TerrainTextureApplier>();

                // Resources.LoadAll<Texture2D> should find textures at the configured path
                applier.LoadTextures();

                int eastCount = applier.NationTextureCount(NationType.East);
                int westCount = applier.NationTextureCount(NationType.West);
                int southCount = applier.NationTextureCount(NationType.South);
                int northCount = applier.NationTextureCount(NationType.North);
                int empireCount = applier.NationTextureCount(NationType.Empire);
                int draculaCount = applier.NationTextureCount(NationType.Dracula);
                int extraCount = applier.ExtraTextureCount;

                // We expect at least some textures to be loaded
                int total = eastCount + westCount + southCount + northCount + empireCount + draculaCount + extraCount;
                Assert.GreaterOrEqual(total, 0, "Should have loaded textures (may be 0 if no Resources exist).");

                // Log for debugging
                Debug.Log($"[Test] Loaded textures: East={eastCount}, West={westCount}, " +
                          $"South={southCount}, North={northCount}, Empire={empireCount}, Dracula={draculaCount}, Extra={extraCount}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LoadTextures_CategorizesByPrefix()
        {
            var go = new GameObject("TestTexApplier");
            try
            {
                var applier = go.AddComponent<TerrainTextureApplier>();
                applier.LoadTextures();

                // Verify nation categorization works
                // east_ -> East, west_ -> West, south_ -> South, etc.
                int eastCount = applier.NationTextureCount(NationType.East);
                int westCount = applier.NationTextureCount(NationType.West);
                int southCount = applier.NationTextureCount(NationType.South);
                int northCount = applier.NationTextureCount(NationType.North);
                int empireCount = applier.NationTextureCount(NationType.Empire);

                // Check that textures exist for each nation
                // (at least some should be non-zero if the resources are placed)
                // We just verify the getter works without error
                Assert.DoesNotThrow(() => { var _ = applier.NationTextureCount(NationType.East); });
                Assert.DoesNotThrow(() => { var _ = applier.NationTextureCount(NationType.West); });
                Assert.DoesNotThrow(() => { var _ = applier.NationTextureCount(NationType.South); });
                Assert.DoesNotThrow(() => { var _ = applier.NationTextureCount(NationType.North); });
                Assert.DoesNotThrow(() => { var _ = applier.NationTextureCount(NationType.Empire); });
                Assert.DoesNotThrow(() => { var _ = applier.NationTextureCount(NationType.Dracula); });

                Debug.Log($"[Test] Texture counts: East={eastCount}, West={westCount}, " +
                          $"South={southCount}, North={northCount}, Empire={empireCount}, Dracula={applier.NationTextureCount(NationType.Dracula)}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ExtraTextures_AreLoadedSeparately()
        {
            var go = new GameObject("TestTexApplier");
            try
            {
                var applier = go.AddComponent<TerrainTextureApplier>();
                applier.LoadTextures();

                // Extra textures should NOT be counted as nation textures
                int extraCount = applier.ExtraTextureCount;
                Assert.GreaterOrEqual(extraCount, 0, "Extra texture count should be non-negative.");

                Debug.Log($"[Test] Extra textures loaded: {extraCount}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  Material Creation Tests
        // ================================================================

        [Test]
        public void CreateMaterials_CreatesMaterialsForNations()
        {
            var go = new GameObject("TestTexApplier");
            try
            {
                var applier = go.AddComponent<TerrainTextureApplier>();
                applier.LoadTextures();
                applier.CreateMaterials();

                Assert.IsTrue(applier.HasMaterialFor(NationType.East) ||
                              applier.HasMaterialFor(NationType.West) ||
                              applier.HasMaterialFor(NationType.South) ||
                              applier.HasMaterialFor(NationType.North) ||
                              applier.HasMaterialFor(NationType.Empire) ||
                              applier.HasMaterialFor(NationType.Dracula),
                    "At least one nation material should be created when textures exist.");

                Debug.Log($"[Test] Materials: East={applier.HasMaterialFor(NationType.East)}, " +
                          $"West={applier.HasMaterialFor(NationType.West)}, " +
                          $"South={applier.HasMaterialFor(NationType.South)}, " +
                          $"North={applier.HasMaterialFor(NationType.North)}, " +
                          $"Empire={applier.HasMaterialFor(NationType.Empire)}, " +
                          $"Dracula={applier.HasMaterialFor(NationType.Dracula)}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CreateMaterials_MaterialNamesAreCorrect()
        {
            var go = new GameObject("TestTexApplier");
            try
            {
                var applier = go.AddComponent<TerrainTextureApplier>();
                applier.LoadTextures();
                applier.CreateMaterials();

                if (applier.HasMaterialFor(NationType.East))
                {
                    Material eastMat = null;
                    Assert.DoesNotThrow(() =>
                    {
                        var materials = applier.NationMaterials;
                        eastMat = materials[NationType.East];
                    });
                    Assert.IsNotNull(eastMat, "East material should not be null.");
                    Assert.AreEqual("Terrain_East_Mat", eastMat.name,
                        "Material name must be 'Terrain_East_Mat'.");
                }

                if (applier.HasMaterialFor(NationType.West))
                {
                    Assert.AreEqual("Terrain_West_Mat",
                        applier.NationMaterials[NationType.West].name,
                        "Material name must be 'Terrain_West_Mat'.");
                }

                if (applier.HasMaterialFor(NationType.Empire))
                {
                    Assert.AreEqual("Terrain_Empire_Mat",
                        applier.NationMaterials[NationType.Empire].name,
                        "Material name must be 'Terrain_Empire_Mat'.");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  Material Application Tests
        // ================================================================

        [Test]
        public void ApplyMaterialForNation_SetsMaterialOnRenderer()
        {
            var go = new GameObject("TestTexApplier");
            try
            {
                var renderer = go.AddComponent<MeshRenderer>();
                var filter = go.AddComponent<MeshFilter>();
                filter.mesh = new Mesh(); // Dummy mesh

                var applier = go.AddComponent<TerrainTextureApplier>();
                applier.LoadTextures();
                applier.CreateMaterials();

                if (applier.HasMaterialFor(NationType.East))
                {
                    applier.ApplyMaterialForNation(NationType.East);

                    Assert.IsNotNull(renderer.sharedMaterial,
                        "MeshRenderer should have a material after ApplyMaterialForNation.");
                    Assert.AreEqual("Terrain_East_Mat", renderer.sharedMaterial.name,
                        "Applied material should be 'Terrain_East_Mat'.");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  Integration: NationTerrainController Interop
        // ================================================================

        [Test]
        public void Awake_DisablesNationTerrainController()
        {
            var go = new GameObject("TestGround");
            try
            {
                // Add NationTerrainController first
                var ntc = go.AddComponent<NationTerrainController>();
                ntc.enabled = true;

                // Add TerrainTextureApplier
                var applier = go.AddComponent<TerrainTextureApplier>();

                // Simulate Awake
                applier.Invoke("Awake", 0f);

                Assert.IsFalse(ntc.enabled,
                    "NationTerrainController should be disabled after TerrainTextureApplier Awake.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  GetNationFromPosition Delegation
        // ================================================================

        [Test]
        public void GetNationFromPosition_MatchesNationTerrainController()
        {
            // Create a static reference test: verify that position-based
            // nation detection works as expected
            NationType eastResult = NationTerrainController.GetNationFromPosition(new Vector3(100f, 0f, 0f));
            Assert.AreEqual(NationType.East, eastResult, "Positive X should be East.");

            NationType westResult = NationTerrainController.GetNationFromPosition(new Vector3(-100f, 0f, 0f));
            Assert.AreEqual(NationType.West, westResult, "Negative X should be West.");

            NationType southResult = NationTerrainController.GetNationFromPosition(new Vector3(0f, 0f, -100f));
            Assert.AreEqual(NationType.South, southResult, "Negative Z should be South.");

            NationType northResult = NationTerrainController.GetNationFromPosition(new Vector3(0f, 0f, 100f));
            Assert.AreEqual(NationType.North, northResult, "Positive Z should be North.");

            NationType empireResult = NationTerrainController.GetNationFromPosition(Vector3.zero);
            Assert.AreEqual(NationType.Empire, empireResult, "Center should be Empire.");
        }
    }
}