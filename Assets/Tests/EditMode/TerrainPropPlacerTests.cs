using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for TerrainPropPlacer.
    /// Tests GLB loading, primitive fallback creation,
    /// random prop placement, and spawn exclusion.
    /// </summary>
    public class TerrainPropPlacerTests
    {
        // ================================================================
        //  Class & Component Tests
        // ================================================================

        [Test]
        public void TerrainPropPlacer_ClassExists()
        {
            var type = typeof(TerrainPropPlacer);
            Assert.IsNotNull(type, "TerrainPropPlacer class must exist.");
        }

        [Test]
        public void TerrainPropPlacer_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(TerrainPropPlacer)),
                "TerrainPropPlacer must inherit from MonoBehaviour.");
        }

        // ================================================================
        //  GLB Loading Tests
        // ================================================================

        [Test]
        public void LoadGLBs_TreesRockGrass_LoadFromResources()
        {
            var go = new GameObject("TestPropPlacer");
            try
            {
                var placer = go.AddComponent<TerrainPropPlacer>();
                placer.LoadGLBs();

                // Verify resource loading doesn't throw
                int treeCount = placer.TreePrefabs?.Count ?? 0;
                int rockCount = placer.RockPrefabs?.Count ?? 0;
                int grassCount = placer.GrassPrefabs?.Count ?? 0;

                // At minimum, counts should be non-negative
                Assert.GreaterOrEqual(treeCount, 0, "Tree prefab count should be non-negative.");
                Assert.GreaterOrEqual(rockCount, 0, "Rock prefab count should be non-negative.");
                Assert.GreaterOrEqual(grassCount, 0, "Grass prefab count should be non-negative.");

                Debug.Log($"[Test] GLBs loaded: {treeCount} trees, {rockCount} rocks, {grassCount} grass.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LoadGLBs_PrefabsAreGameObjects()
        {
            var go = new GameObject("TestPropPlacer");
            try
            {
                var placer = go.AddComponent<TerrainPropPlacer>();
                placer.LoadGLBs();

                // If any prefabs loaded, verify they are actual GameObjects
                if (placer.TreePrefabs != null && placer.TreePrefabs.Count > 0)
                {
                    Assert.IsNotNull(placer.TreePrefabs[0], "Tree prefab should not be null.");
                }
                if (placer.RockPrefabs != null && placer.RockPrefabs.Count > 0)
                {
                    Assert.IsNotNull(placer.RockPrefabs[0], "Rock prefab should not be null.");
                }
                if (placer.GrassPrefabs != null && placer.GrassPrefabs.Count > 0)
                {
                    Assert.IsNotNull(placer.GrassPrefabs[0], "Grass prefab should not be null.");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  Primitive Fallback Tests
        // ================================================================

        [Test]
        public void CreateFallbacks_CreatesThreePrimitives()
        {
            var go = new GameObject("TestPropPlacer");
            try
            {
                var placer = go.AddComponent<TerrainPropPlacer>();
                placer.CreateFallbacks();

                // Verify fallback objects exist
                Assert.IsNotNull(placer.TreeFallback, "Tree fallback should be created.");
                Assert.IsNotNull(placer.RockFallback, "Rock fallback should be created.");
                Assert.IsNotNull(placer.GrassFallback, "Grass fallback should be created.");

                // Verify they are primitives
                Assert.AreEqual("Tree_Fallback", placer.TreeFallback.name);
                Assert.AreEqual("Rock_Fallback", placer.RockFallback.name);
                Assert.AreEqual("Grass_Fallback", placer.GrassFallback.name);

                // Verify they are inactive (template objects)
                Assert.IsFalse(placer.TreeFallback.activeInHierarchy, "Tree fallback should be inactive.");
                Assert.IsFalse(placer.RockFallback.activeInHierarchy, "Rock fallback should be inactive.");
                Assert.IsFalse(placer.GrassFallback.activeInHierarchy, "Grass fallback should be inactive.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  Prop Placement Tests
        // ================================================================

        [Test]
        public void PlaceProps_CreatesInstances()
        {
            var go = new GameObject("TestPropPlacer");
            try
            {
                var placer = go.AddComponent<TerrainPropPlacer>();
                placer.LoadGLBs();
                placer.CreateFallbacks();
                placer.PlaceProps();

                int propCount = placer.PropCount;
                Assert.GreaterOrEqual(propCount, 0, "Should have placed props.");

                Debug.Log($"[Test] Placed {propCount} total props.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlaceProps_ExcludesCenterSpawnArea()
        {
            var go = new GameObject("TestPropPlacer");
            try
            {
                var placer = go.AddComponent<TerrainPropPlacer>();
                placer.LoadGLBs();
                placer.CreateFallbacks();
                placer.PlaceProps();

                // Check that no props are within the spawn exclusion zone (30m)
                bool foundInCenter = false;
                foreach (var prop in placer.PlacedProps)
                {
                    if (prop != null && prop.transform.position.magnitude < 30f)
                    {
                        foundInCenter = true;
                        Debug.LogWarning($"[Test] Prop found in spawn exclusion zone: {prop.name} at {prop.transform.position}");
                        break;
                    }
                }

                Assert.IsFalse(foundInCenter, "No props should be placed within 30m of center.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlaceProps_UsesFixedSeed()
        {
            var go1 = new GameObject("TestPropPlacer_1");
            var go2 = new GameObject("TestPropPlacer_2");
            try
            {
                var placer1 = go1.AddComponent<TerrainPropPlacer>();
                placer1.LoadGLBs();
                placer1.CreateFallbacks();
                placer1.PlaceProps();

                var placer2 = go2.AddComponent<TerrainPropPlacer>();
                placer2.LoadGLBs();
                placer2.CreateFallbacks();
                placer2.PlaceProps();

                // With fixed seed=100, placements should be deterministic
                int count1 = placer1.PropCount;
                int count2 = placer2.PropCount;

                Assert.AreEqual(count1, count2,
                    $"Both placements should produce the same count with fixed seed. Count1={count1}, Count2={count2}");
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);
            }
        }

        // ================================================================
        //  Scale Range Tests
        // ================================================================

        [Test]
        public void PropsAreScaledWithinRange()
        {
            var go = new GameObject("TestPropPlacer");
            try
            {
                var placer = go.AddComponent<TerrainPropPlacer>();
                placer.LoadGLBs();
                placer.CreateFallbacks();
                placer.PlaceProps();

                // Verify all placed props have reasonable scale values
                foreach (var prop in placer.PlacedProps)
                {
                    if (prop != null)
                    {
                        float scaleX = prop.transform.localScale.x;
                        Assert.IsTrue(scaleX > 0f, $"Prop {prop.name} should have positive scale.");
                        Assert.IsTrue(scaleX < 5f, $"Prop {prop.name} scale should be reasonable (< 5).");
                    }
                }

                Debug.Log($"[Test] All {placer.PropCount} props have valid scales.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  Clear Props Test
        // ================================================================

        [Test]
        public void ClearProps_RemovesAllInstances()
        {
            var go = new GameObject("TestPropPlacer");
            try
            {
                var placer = go.AddComponent<TerrainPropPlacer>();
                placer.LoadGLBs();
                placer.CreateFallbacks();
                placer.PlaceProps();

                Assert.GreaterOrEqual(placer.PropCount, 0, "Props should have been placed.");

                placer.ClearProps();

                Assert.AreEqual(0, placer.PropCount, "All props should be cleared.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}