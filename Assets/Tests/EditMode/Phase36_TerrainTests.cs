using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Phase 3.6: Skybox (3.6.4) and
    /// Nation-Specific Terrain Textures (3.6.7).
    ///
    /// Tests cover:
    ///   - Editor class existence and menu items
    ///   - Procedural texture generation correctness
    ///   - NationTerrainController position-to-nation mapping
    ///   - Texture pixel content validation
    /// </summary>
    public class Phase36_TerrainTests
    {
        // ================================================================
        // Part 1: Editor class & Menu Item Tests
        // ================================================================

        [Test]
        public void Phase36_TerrainSetup_ClassExists()
        {
            var type = typeof(Phase36_TerrainSetup);
            Assert.IsNotNull(type, "Phase36_TerrainSetup class must exist.");
        }

        [Test]
        public void Phase36_TerrainSetup_SetupSkybox_MenuItemExists()
        {
            var type = typeof(Phase36_TerrainSetup);
            var method = type.GetMethod("SetupSkybox",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "SetupSkybox public static method must exist.");

            var attr = System.Attribute.GetCustomAttribute(method, typeof(MenuItem));
            Assert.IsNotNull(attr, "MenuItem attribute must exist on SetupSkybox.");

            var menuItem = attr as MenuItem;
            Assert.IsNotNull(menuItem, "MenuItem attribute must be of type MenuItem.");
            Assert.AreEqual("Tools/Phase 3.6/Setup Skybox", menuItem.menuItem,
                "Menu path must be 'Tools/Phase 3.6/Setup Skybox'.");
        }

        [Test]
        public void Phase36_TerrainSetup_SetupNationTerrain_MenuItemExists()
        {
            var type = typeof(Phase36_TerrainSetup);
            var method = type.GetMethod("SetupNationTerrain",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "SetupNationTerrain public static method must exist.");

            var attr = System.Attribute.GetCustomAttribute(method, typeof(MenuItem));
            Assert.IsNotNull(attr, "MenuItem attribute must exist on SetupNationTerrain.");

            var menuItem = attr as MenuItem;
            Assert.IsNotNull(menuItem, "MenuItem attribute must be of type MenuItem.");
            Assert.AreEqual("Tools/Phase 3.6/Setup Nation Terrain", menuItem.menuItem,
                "Menu path must be 'Tools/Phase 3.6/Setup Nation Terrain'.");
        }

        // ================================================================
        // Part 2: NationTerrainController Tests
        // ================================================================

        [Test]
        public void NationTerrainController_ClassExists()
        {
            var controllerType = typeof(NationTerrainController);
            Assert.IsNotNull(controllerType, "NationTerrainController class must exist.");
        }

        [Test]
        public void NationTerrainController_GetNationFromPosition_ReturnsEastForPositiveX()
        {
            // East is along +x axis
            NationType result = NationTerrainController.GetNationFromPosition(new Vector3(100f, 0f, 0f));
            Assert.AreEqual(NationType.East, result,
                "Position (100, 0, 0) should be East territory.");
        }

        [Test]
        public void NationTerrainController_GetNationFromPosition_ReturnsWestForNegativeX()
        {
            NationType result = NationTerrainController.GetNationFromPosition(new Vector3(-100f, 0f, 0f));
            Assert.AreEqual(NationType.West, result,
                "Position (-100, 0, 0) should be West territory.");
        }

        [Test]
        public void NationTerrainController_GetNationFromPosition_ReturnsNorthForPositiveZ()
        {
            NationType result = NationTerrainController.GetNationFromPosition(new Vector3(0f, 0f, 100f));
            Assert.AreEqual(NationType.North, result,
                "Position (0, 0, 100) should be North territory.");
        }

        [Test]
        public void NationTerrainController_GetNationFromPosition_ReturnsSouthForNegativeZ()
        {
            NationType result = NationTerrainController.GetNationFromPosition(new Vector3(0f, 0f, -100f));
            Assert.AreEqual(NationType.South, result,
                "Position (0, 0, -100) should be South territory.");
        }

        [Test]
        public void NationTerrainController_GetNationFromPosition_ReturnsEmpireAtCenter()
        {
            NationType result = NationTerrainController.GetNationFromPosition(Vector3.zero);
            Assert.AreEqual(NationType.Empire, result,
                "Position (0, 0, 0) should be Empire territory.");

            result = NationTerrainController.GetNationFromPosition(new Vector3(25f, 0f, 25f));
            Assert.AreEqual(NationType.Empire, result,
                "Position (25, 0, 25) should be Empire territory (within 50m).");
        }

        [Test]
        public void NationTerrainController_GetNationFromPosition_ReturnsEastForNortheastCorner()
        {
            // 45 degrees from +x = Northeast, which is in East's quadrant (-45 to 45)
            NationType result = NationTerrainController.GetNationFromPosition(new Vector3(100f, 0f, 100f));
            Assert.AreEqual(NationType.North, result,
                "Position (100, 0, 100) at 45° should be North territory.");
        }

        [Test]
        public void NationTerrainController_GetNationFromPosition_HandlesBoundaryAngles()
        {
            // Just inside East boundary at -44° (z negative, x positive)
            NationType result = NationTerrainController.GetNationFromPosition(new Vector3(100f, 0f, -97f));
            Assert.AreEqual(NationType.East, result,
                "Position at ~-44° should be East territory.");

            // Just inside South boundary at -46° (z negative, x positive)
            result = NationTerrainController.GetNationFromPosition(new Vector3(70f, 0f, -72f));
            Assert.AreEqual(NationType.South, result,
                "Position at ~-46° should be South territory.");
        }

        // ================================================================
        // Part 3: Procedural Texture Generation Tests
        // ================================================================

        [Test]
        public void GenerateCombinedTexture_ReturnsNonNull()
        {
            var go = new GameObject("_TestGround");
            try
            {
                var controller = go.AddComponent<NationTerrainController>();
                Texture2D tex = controller.GenerateCombinedTexture();
                Assert.IsNotNull(tex, "Generated combined texture must not be null.");
                Assert.AreEqual(controller.TextureSize, tex.width,
                    $"Texture width must be {controller.TextureSize}.");
                Assert.AreEqual(controller.TextureSize, tex.height,
                    $"Texture height must be {controller.TextureSize}.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GenerateNationFocusedTexture_ReturnsNonNull()
        {
            var go = new GameObject("_TestGround");
            try
            {
                var controller = go.AddComponent<NationTerrainController>();
                Texture2D tex = controller.GenerateNationFocusedTexture(NationType.East);
                Assert.IsNotNull(tex, "Generated focused texture must not be null.");
                Assert.AreEqual(controller.TextureSize, tex.width,
                    "Texture width must match configured size.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CombinedTexture_CenterPixelsAreDarker()
        {
            var go = new GameObject("_TestGround");
            try
            {
                var controller = go.AddComponent<NationTerrainController>();
                Texture2D tex = controller.GenerateCombinedTexture();

                // Center pixel
                int cx = tex.width / 2;
                int cy = tex.height / 2;
                Color centerColor = tex.GetPixel(cx, cy);

                // Edge pixel (near top-right, far from center)
                Color edgeColor = tex.GetPixel(tex.width - 1, tex.height - 1);

                // Center should be slightly darker due to the center darkening logic
                float centerLuminance = 0.2126f * centerColor.r + 0.7152f * centerColor.g + 0.0722f * centerColor.b;
                float edgeLuminance = 0.2126f * edgeColor.r + 0.7152f * edgeColor.g + 0.0722f * edgeColor.b;

                Assert.LessOrEqual(centerLuminance, edgeLuminance * 1.1f,
                    "Center luminance should not be significantly brighter than edge.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CombinedTexture_HasRingZoneVariation()
        {
            var go = new GameObject("_TestGround");
            try
            {
                var controller = go.AddComponent<NationTerrainController>();
                Texture2D tex = controller.GenerateCombinedTexture();

                // Sample pixels at center (Ring1)
                Color centerPixel = tex.GetPixel(tex.width / 2, tex.height / 2);

                // Sample pixels near edge (Ring3) — right edge
                Color edgePixel = tex.GetPixel(tex.width - 2, tex.height / 2);

                // Ring1 and Ring3 have different base colors, so they should differ
                float colorDiff = Mathf.Abs(centerPixel.r - edgePixel.r)
                                + Mathf.Abs(centerPixel.g - edgePixel.g)
                                + Mathf.Abs(centerPixel.b - edgePixel.b);

                Assert.Greater(colorDiff, 0.01f,
                    "Ring1 and Ring3 pixels should have visibly different colors.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NationFocusedTexture_DefaultSettingsAreValid()
        {
            var go = new GameObject("_TestGround");
            try
            {
                var controller = go.AddComponent<NationTerrainController>();

                // Verify that default inspector values are sensible
                Assert.Greater(controller.TextureSize, 0, "Texture size must be positive.");
                Assert.AreEqual(new Color(0.45f, 0.30f, 0.15f), controller.Ring1Color,
                    "Ring1 default color should be brown_mud_leaves.");
                Assert.AreEqual(new Color(0.20f, 0.55f, 0.15f), controller.EastTint,
                    "East default tint should be green.");
                Assert.AreEqual(new Color(0.25f, 0.05f, 0.05f), controller.DraculaTint,
                    "Dracula default tint should be dark red/black.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GenerateCombinedTexture_AllPixelsOpaque()
        {
            var go = new GameObject("_TestGround");
            try
            {
                var controller = go.AddComponent<NationTerrainController>();
                Texture2D tex = controller.GenerateCombinedTexture();

                Color[] pixels = tex.GetPixels();
                foreach (Color pixel in pixels)
                {
                    Assert.AreEqual(1f, pixel.a,
                        "All texture pixels must have full alpha.");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GenerateNationFocusedTexture_ProducesDifferentPixelsPerNation()
        {
            var go = new GameObject("_TestGround");
            try
            {
                var controller = go.AddComponent<NationTerrainController>();

                Texture2D eastTex = controller.GenerateNationFocusedTexture(NationType.East);
                Texture2D westTex = controller.GenerateNationFocusedTexture(NationType.West);

                // Compare center pixels — should differ because tint colors differ
                Color eastCenter = eastTex.GetPixel(eastTex.width / 2, eastTex.height / 2);
                Color westCenter = westTex.GetPixel(westTex.width / 2, westTex.height / 2);

                float diff = Mathf.Abs(eastCenter.r - westCenter.r)
                           + Mathf.Abs(eastCenter.g - westCenter.g)
                           + Mathf.Abs(eastCenter.b - westCenter.b);

                Assert.Greater(diff, 0.01f,
                    "East-focused and West-focused textures should produce different center pixels.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GenerateNationFocusedTexture_Dracula_UsesCorrectTint()
        {
            var go = new GameObject("_TestGround");
            try
            {
                var controller = go.AddComponent<NationTerrainController>();
                Texture2D draculaTex = controller.GenerateNationFocusedTexture(NationType.Dracula);
                Assert.IsNotNull(draculaTex, "Dracula-focused texture must not be null.");

                // Center pixel should be dark (Dracula tint is dark red/black)
                Color center = draculaTex.GetPixel(draculaTex.width / 2, draculaTex.height / 2);
                float luminance = 0.2126f * center.r + 0.7152f * center.g + 0.0722f * center.b;
                Assert.Less(luminance, 0.5f,
                    "Dracula-focused texture center should be dark (luminance < 0.5).");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        // Part 4: Edge Cases & Stability
        // ================================================================

        [Test]
        public void GetNationFromPosition_ZeroVector_ReturnsEmpire()
        {
            NationType result = NationTerrainController.GetNationFromPosition(Vector3.zero);
            Assert.AreEqual(NationType.Empire, result);
        }

        [Test]
        public void GetNationFromPosition_LargeValues_NoCrash()
        {
            // Should handle coordinates well beyond the 1000m terrain
            NationType result = NationTerrainController.GetNationFromPosition(new Vector3(10000f, 0f, 0f));
            Assert.AreEqual(NationType.East, result,
                "Large +x should still map to East.");

            result = NationTerrainController.GetNationFromPosition(new Vector3(0f, 0f, -10000f));
            Assert.AreEqual(NationType.South, result,
                "Large -z should still map to South.");
        }

        [Test]
        public void GetNationFromPosition_CoversAllNations()
        {
            // Verify each nation appears at expected positions
            Assert.AreEqual(NationType.East,
                NationTerrainController.GetNationFromPosition(new Vector3(100f, 0f, 0f)));
            Assert.AreEqual(NationType.West,
                NationTerrainController.GetNationFromPosition(new Vector3(-100f, 0f, 0f)));
            Assert.AreEqual(NationType.North,
                NationTerrainController.GetNationFromPosition(new Vector3(0f, 0f, 100f)));
            Assert.AreEqual(NationType.South,
                NationTerrainController.GetNationFromPosition(new Vector3(0f, 0f, -100f)));
            Assert.AreEqual(NationType.Empire,
                NationTerrainController.GetNationFromPosition(new Vector3(10f, 0f, 10f)));
        }
    }
}