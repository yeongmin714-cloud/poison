using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase G1-09 EditMode tests for WaterMaterialUpgrader utility:
    /// upgraded water material properties, 2-axis wave animation,
    /// depth-based color, vertex color normal effect, and reset behavior.
    /// </summary>
    public class PhaseG1_WaterShaderTests
    {
        private const float Epsilon = 0.001f;
        private Material _upgradedMat;
        private Material _simpleMat;
        private Material _shallowMat;
        private Material _deepMat;

        [SetUp]
        public void SetUp()
        {
            _upgradedMat = WaterMaterialUpgrader.CreateUpgradedWaterMaterial("Test_Upgraded_Water", 0.5f);
            _simpleMat = WaterMaterialUpgrader.CreateSimpleWaterMaterial("Test_Simple_Water", new Color(0.2f, 0.5f, 0.8f, 0.6f));
            _shallowMat = WaterMaterialUpgrader.CreateUpgradedWaterMaterial("Test_Shallow_Water", 1.0f);
            _deepMat = WaterMaterialUpgrader.CreateUpgradedWaterMaterial("Test_Deep_Water", 0.0f);
        }

        [TearDown]
        public void TearDown()
        {
            DestroyMaterial(ref _upgradedMat);
            DestroyMaterial(ref _simpleMat);
            DestroyMaterial(ref _shallowMat);
            DestroyMaterial(ref _deepMat);
        }

        private static void DestroyMaterial(ref Material mat)
        {
            if (mat != null)
            {
                Object.DestroyImmediate(mat);
                mat = null;
            }
        }

        // ================================================================
        //  Test 1: Render queue is 3000 (Transparent)
        // ================================================================

        [Test]
        public void UpgradedMaterial_RenderQueue_Is3000()
        {
            Assert.IsNotNull(_upgradedMat, "Upgraded material should be created");
            Assert.AreEqual(3000, _upgradedMat.renderQueue,
                "Upgraded water material render queue should be 3000 (Transparent)");
        }

        // ================================================================
        //  Test 2: Material has Transparent surface type keyword
        // ================================================================

        [Test]
        public void UpgradedMaterial_HasTransparentSurfaceKeyword()
        {
            Assert.IsNotNull(_upgradedMat);
            Assert.IsTrue(_upgradedMat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),
                "Upgraded water material should have _SURFACE_TYPE_TRANSPARENT keyword enabled");
        }

        // ================================================================
        //  Test 3: Material _Surface float is 1 (Transparent)
        // ================================================================

        [Test]
        public void UpgradedMaterial_SurfaceFloat_IsTransparent()
        {
            Assert.IsNotNull(_upgradedMat);
            Assert.AreEqual(1f, _upgradedMat.GetFloat("_Surface"), Epsilon,
                "_Surface should be 1.0 for Transparent rendering");
        }

        // ================================================================
        //  Test 4: Smoothness is 0.8
        // ================================================================

        [Test]
        public void UpgradedMaterial_Smoothness_IsZeroPointEight()
        {
            Assert.IsNotNull(_upgradedMat);
            Assert.IsTrue(_upgradedMat.HasProperty("_Smoothness"),
                "Material should have _Smoothness property");
            Assert.AreEqual(0.8f, _upgradedMat.GetFloat("_Smoothness"), Epsilon,
                "Smoothness should be 0.8 for clear reflections");
        }

        // ================================================================
        //  Test 5: Metallic is 0.0
        // ================================================================

        [Test]
        public void UpgradedMaterial_Metallic_IsZero()
        {
            Assert.IsNotNull(_upgradedMat);
            Assert.IsTrue(_upgradedMat.HasProperty("_Metallic"),
                "Material should have _Metallic property");
            Assert.AreEqual(0.0f, _upgradedMat.GetFloat("_Metallic"), Epsilon,
                "Metallic should be 0.0 for non-metallic reflection");
        }

        // ================================================================
        //  Test 6: _REFLECTION_PROBE_BLENDING keyword is enabled
        // ================================================================

        [Test]
        public void UpgradedMaterial_HasReflectionProbeBlending()
        {
            Assert.IsNotNull(_upgradedMat);
            Assert.IsTrue(_upgradedMat.IsKeywordEnabled("_REFLECTION_PROBE_BLENDING"),
                "_REFLECTION_PROBE_BLENDING keyword should be enabled for reflection probe support");
        }

        // ================================================================
        //  Test 7: _REFLECTION_PROBE_BOX_PROJECTION keyword is enabled
        // ================================================================

        [Test]
        public void UpgradedMaterial_HasReflectionProbeBoxProjection()
        {
            Assert.IsNotNull(_upgradedMat);
            Assert.IsTrue(_upgradedMat.IsKeywordEnabled("_REFLECTION_PROBE_BOX_PROJECTION"),
                "_REFLECTION_PROBE_BOX_PROJECTION keyword should be enabled for box projection");
        }

        // ================================================================
        //  Test 8: 2-axis wave animation creates position delta
        // ================================================================

        [Test]
        public void TwoAxisWave_CreatesPositionDelta()
        {
            // Compute wave offsets at different time values
            float offset1 = WaterMaterialUpgrader.Compute2AxisWaveOffset(0f, 1.5f, 0.05f, 0f, 0f);
            float offset2 = WaterMaterialUpgrader.Compute2AxisWaveOffset(1f, 1.5f, 0.05f, 0f, 0f);
            float offset3 = WaterMaterialUpgrader.Compute2AxisWaveOffset(2f, 1.5f, 0.05f, 0f, 0f);

            // Verify that the wave produces varying offsets over time
            bool hasDelta = Mathf.Abs(offset2 - offset1) > Epsilon ||
                            Mathf.Abs(offset3 - offset1) > Epsilon ||
                            Mathf.Abs(offset3 - offset2) > Epsilon;

            Assert.IsTrue(hasDelta,
                "2-axis wave animation should produce different offsets at different times");
        }

        // ================================================================
        //  Test 9: 2-axis wave produces different results for different X positions
        // ================================================================

        [Test]
        public void TwoAxisWave_DifferentXPositions_ProduceDifferentOffsets()
        {
            float offsetAtX0 = WaterMaterialUpgrader.Compute2AxisWaveOffset(1.0f, 1.5f, 0.05f, 0f, 0f);
            float offsetAtX5 = WaterMaterialUpgrader.Compute2AxisWaveOffset(1.0f, 1.5f, 0.05f, 5f, 0f);
            float offsetAtX10 = WaterMaterialUpgrader.Compute2AxisWaveOffset(1.0f, 1.5f, 0.05f, 10f, 0f);

            bool hasXVariation = Mathf.Abs(offsetAtX5 - offsetAtX0) > Epsilon ||
                                 Mathf.Abs(offsetAtX10 - offsetAtX0) > Epsilon;

            Assert.IsTrue(hasXVariation,
                "2-axis wave should produce different offsets for different X positions");
        }

        // ================================================================
        //  Test 10: 2-axis wave produces different results for different Z positions
        // ================================================================

        [Test]
        public void TwoAxisWave_DifferentZPositions_ProduceDifferentOffsets()
        {
            float offsetAtZ0 = WaterMaterialUpgrader.Compute2AxisWaveOffset(1.0f, 1.5f, 0.05f, 0f, 0f);
            float offsetAtZ5 = WaterMaterialUpgrader.Compute2AxisWaveOffset(1.0f, 1.5f, 0.05f, 0f, 5f);
            float offsetAtZ10 = WaterMaterialUpgrader.Compute2AxisWaveOffset(1.0f, 1.5f, 0.05f, 0f, 10f);

            bool hasZVariation = Mathf.Abs(offsetAtZ5 - offsetAtZ0) > Epsilon ||
                                 Mathf.Abs(offsetAtZ10 - offsetAtZ0) > Epsilon;

            Assert.IsTrue(hasZVariation,
                "2-axis wave should produce different offsets for different Z positions");
        }

        // ================================================================
        //  Test 11: Depth-based color blends shallow and deep
        // ================================================================

        [Test]
        public void UpgradedMaterial_DepthBasedColor_IsBetweenShallowAndDeep()
        {
            Assert.IsNotNull(_upgradedMat);

            // At shallowWeight=0.5, color should be midway between shallow and deep
            Color shallow = WaterMaterialUpgrader.ShallowColor;
            Color deep = WaterMaterialUpgrader.DeepColor;
            Color midpoint = Color.Lerp(deep, shallow, 0.5f);

            Color actualColor = _upgradedMat.color;

            Assert.AreEqual(midpoint.r, actualColor.r, 0.02f,
                "Red channel should match midpoint of shallow and deep at weight=0.5");
            Assert.AreEqual(midpoint.g, actualColor.g, 0.02f,
                "Green channel should match midpoint of shallow and deep at weight=0.5");
            Assert.AreEqual(midpoint.b, actualColor.b, 0.02f,
                "Blue channel should match midpoint of shallow and deep at weight=0.5");
        }

        // ================================================================
        //  Test 12: Shallow-only material is visibly different from deep-only
        // ================================================================

        [Test]
        public void DepthBasedColor_ShallowVsDeep_AreVisiblyDifferent()
        {
            Assert.IsNotNull(_shallowMat);
            Assert.IsNotNull(_deepMat);

            Color shallowColor = _shallowMat.color;
            Color deepColor = _deepMat.color;

            // Shallow should be brighter/blue, deep should be darker
            Assert.Greater(shallowColor.r, deepColor.r,
                "Shallow water should have higher red component than deep water");
            Assert.Greater(shallowColor.g, deepColor.g,
                "Shallow water should have higher green component than deep water");
            Assert.Greater(shallowColor.b, deepColor.b,
                "Shallow water should have higher blue component than deep water");
        }

        // ================================================================
        //  Test 13: Simple material is different from upgraded material
        // ================================================================

        [Test]
        public void SimpleMaterial_DoesNotHaveReflectionBlending()
        {
            Assert.IsNotNull(_simpleMat);

            // Simple material should NOT have reflection probe keywords
            Assert.IsFalse(_simpleMat.IsKeywordEnabled("_REFLECTION_PROBE_BLENDING"),
                "Simple water material should NOT have _REFLECTION_PROBE_BLENDING keyword");
            Assert.IsFalse(_simpleMat.IsKeywordEnabled("_REFLECTION_PROBE_BOX_PROJECTION"),
                "Simple water material should NOT have _REFLECTION_PROBE_BOX_PROJECTION keyword");
        }

        // ================================================================
        //  Test 14: IsUpgradedWaterMaterial correctly identifies upgraded
        // ================================================================

        [Test]
        public void IsUpgradedWaterMaterial_ReturnsTrue_ForUpgraded()
        {
            Assert.IsNotNull(_upgradedMat);
            Assert.IsTrue(WaterMaterialUpgrader.IsUpgradedWaterMaterial(_upgradedMat),
                "IsUpgradedWaterMaterial should return true for upgraded material");
        }

        // ================================================================
        //  Test 15: IsUpgradedWaterMaterial correctly identifies non-upgraded
        // ================================================================

        [Test]
        public void IsUpgradedWaterMaterial_ReturnsFalse_ForSimple()
        {
            Assert.IsNotNull(_simpleMat);
            Assert.IsFalse(WaterMaterialUpgrader.IsUpgradedWaterMaterial(_simpleMat),
                "IsUpgradedWaterMaterial should return false for simple (non-upgraded) material");
        }

        // ================================================================
        //  Test 16: IsUpgradedWaterMaterial returns false for null
        // ================================================================

        [Test]
        public void IsUpgradedWaterMaterial_ReturnsFalse_ForNull()
        {
            Assert.IsFalse(WaterMaterialUpgrader.IsUpgradedWaterMaterial(null),
                "IsUpgradedWaterMaterial should return false for null input");
        }

        // ================================================================
        //  Test 17: Vertex color normal effect modifies mesh colors
        // ================================================================

        [Test]
        public void VertexColorNormalEffect_ModifiesMeshColors()
        {
            var mesh = new Mesh();

            // Create a simple quad mesh
            mesh.vertices = new Vector3[]
            {
                new Vector3(-1, 0, -1),
                new Vector3( 1, 0, -1),
                new Vector3(-1, 0,  1),
                new Vector3( 1, 0,  1)
            };
            mesh.triangles = new int[] { 0, 1, 2, 1, 3, 2 };
            mesh.RecalculateNormals();

            // Before: mesh should not have vertex colors
            Assert.IsNull(mesh.colors, "Mesh should have no vertex colors before applying effect");

            // Apply normal effect
            WaterMaterialUpgrader.ApplyVertexColorNormalEffect(mesh);

            // After: mesh should have vertex colors
            Assert.IsNotNull(mesh.colors, "Mesh should have vertex colors after applying effect");
            Assert.AreEqual(4, mesh.colors.Length, "All 4 vertices should have colors assigned");

            // Colors should be close to 0.5 mid-gray (neutral normal)
            foreach (Color c in mesh.colors)
            {
                Assert.Greater(c.r, 0.4f, "Red channel should be above 0.4 (near neutral)");
                Assert.Less(c.r, 0.6f, "Red channel should be below 0.6 (near neutral)");
                Assert.Greater(c.g, 0.4f, "Green channel should be above 0.4");
                Assert.Less(c.g, 0.6f, "Green channel should be below 0.6");
                Assert.Greater(c.b, 0.4f, "Blue channel should be above 0.4");
                Assert.Less(c.b, 0.6f, "Blue channel should be below 0.6");
            }

            Object.DestroyImmediate(mesh);
        }

        // ================================================================
        //  Test 18: Vertex color effect handles null mesh gracefully
        // ================================================================

        [Test]
        public void VertexColorNormalEffect_NullMesh_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                WaterMaterialUpgrader.ApplyVertexColorNormalEffect(null);
            }, "ApplyVertexColorNormalEffect should not throw on null mesh");
        }

        // ================================================================
        //  Test 19: Upgraded material at shallowWeight=0 uses DeepColor
        // ================================================================

        [Test]
        public void UpgradedMaterial_ShallowWeightZero_UsesDeepColor()
        {
            Assert.IsNotNull(_deepMat);
            Color expected = WaterMaterialUpgrader.DeepColor;
            Color actual = _deepMat.color;

            Assert.AreEqual(expected.r, actual.r, 0.02f, "Deep-only red should match DeepColor");
            Assert.AreEqual(expected.g, actual.g, 0.02f, "Deep-only green should match DeepColor");
            Assert.AreEqual(expected.b, actual.b, 0.02f, "Deep-only blue should match DeepColor");
        }

        // ================================================================
        //  Test 20: Simple material has transparent render queue
        // ================================================================

        [Test]
        public void SimpleMaterial_RenderQueue_Is3000()
        {
            Assert.IsNotNull(_simpleMat);
            Assert.AreEqual(3000, _simpleMat.renderQueue,
                "Simple water material render queue should also be 3000 (Transparent)");
        }
    }
}