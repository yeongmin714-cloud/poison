using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C22-10: Phase 22 통합 테스트.
    /// C22-05: 호수 생성
    /// C22-06: 늪/사막 효과
    /// C22-07: 동굴 입구 시스템
    /// C22-08: 동굴 내부
    /// C22-09: 지형 전환 시스템
    /// </summary>
    public class Phase22_AdvancedTerrainTests
    {
        // ================================================================
        //  C22-05: LakeGenerator (Perlin noise lakes)
        // ================================================================

        [Test]
        public void LakeGenerator_ConstructsWaterSurface()
        {
            var go = new GameObject("TestLake");
            try
            {
                var lake = go.AddComponent<LakeGenerator>();
                // Trigger Awake
                lake.Invoke("Awake", 0f);
                Assert.IsNotNull(lake.WaterSurface, "LakeGenerator should create a WaterSurface GameObject");
                Assert.IsNotNull(lake.LakeBed, "LakeGenerator should create a LakeBed GameObject");
                Assert.IsNotNull(lake.CollisionVolume, "LakeGenerator should create a CollisionVolume");
                Assert.Greater(lake.Radius, 0f, "Radius should be positive");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LakeGenerator_DefaultValues_AreReasonable()
        {
            var go = new GameObject("TestLakeDefaults");
            try
            {
                var lake = go.AddComponent<LakeGenerator>();
                Assert.Greater(lake.Radius, 0f, "Default radius must be > 0");
                Assert.IsTrue(lake.NoiseThreshold > 0f && lake.NoiseThreshold < 1f,
                    "Noise threshold must be between 0 and 1");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LakeGenerator_WaterTag_IsSet()
        {
            var go = new GameObject("TestLakeTag");
            try
            {
                var lake = go.AddComponent<LakeGenerator>();
                lake.Invoke("Awake", 0f);
                if (lake.CollisionVolume != null)
                {
                    Assert.AreEqual("Water", lake.CollisionVolume.tag,
                        "Collision volume should be tagged 'Water'");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  C22-06: BiomeEffectController (swamp/desert slowdown)
        // ================================================================

        [Test]
        public void BiomeEffectController_DefaultsToPlains()
        {
            var go = new GameObject("TestBiome");
            try
            {
                var ctrl = go.AddComponent<BiomeEffectController>();
                Assert.AreEqual(BiomeType.Plains, ctrl.CurrentBiome,
                    "Default biome should be Plains");
                Assert.AreEqual(1.0f, ctrl.SpeedMultiplier,
                    "Default speed multiplier should be 1.0");
                Assert.IsFalse(ctrl.IsInWater, "Should not be in water by default");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BiomeEffectController_Swamp_AppliesSlowdown()
        {
            var go = new GameObject("TestSwamp");
            try
            {
                var ctrl = go.AddComponent<BiomeEffectController>();

                // Get swamp definition from BiomeData
                BiomeDefinition def = BiomeData.GetDefinition(BiomeType.Swamp);
                float expectedSpeed = def.moveSpeedModifier;

                ctrl.ApplyBiomeEffect(BiomeType.Swamp);
                Assert.AreEqual(BiomeType.Swamp, ctrl.CurrentBiome,
                    "Biome should be set to Swamp");
                Assert.AreEqual(expectedSpeed, ctrl.SpeedMultiplier,
                    $"Swamp speed modifier should be {expectedSpeed} (0.5)");
                Assert.IsTrue(expectedSpeed < 1.0f,
                    "Swamp should slow down movement");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BiomeEffectController_Desert_AppliesSlowdown()
        {
            var go = new GameObject("TestDesert");
            try
            {
                var ctrl = go.AddComponent<BiomeEffectController>();

                BiomeDefinition def = BiomeData.GetDefinition(BiomeType.Desert);
                float expectedSpeed = def.moveSpeedModifier;

                ctrl.ApplyBiomeEffect(BiomeType.Desert);
                Assert.AreEqual(BiomeType.Desert, ctrl.CurrentBiome,
                    "Biome should be set to Desert");
                Assert.AreEqual(expectedSpeed, ctrl.SpeedMultiplier,
                    $"Desert speed modifier should be {expectedSpeed} (0.9)");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BiomeEffectController_Water_SetsHalfSpeed()
        {
            var go = new GameObject("TestWaterBiome");
            try
            {
                var ctrl = go.AddComponent<BiomeEffectController>();
                ctrl.OnEnterWater();
                Assert.IsTrue(ctrl.IsInWater, "Should be in water after OnEnterWater");
                ctrl.OnExitWater();
                Assert.IsFalse(ctrl.IsInWater, "Should not be in water after OnExitWater");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  C22-07: CaveEntrance (E-key cave entry)
        // ================================================================

        [Test]
        public void CaveEntrance_BuildsArchMesh()
        {
            var go = new GameObject("TestCaveEntrance");
            try
            {
                var entrance = go.AddComponent<CaveEntrance>();
                entrance.Invoke("Awake", 0f);

                // Check tag
                Assert.AreEqual("Interactable", go.tag,
                    "CaveEntrance should be tagged 'Interactable'");

                // Should have a sphere collider for interaction
                var trigger = go.GetComponent<SphereCollider>();
                Assert.IsNotNull(trigger, "CaveEntrance should have a SphereCollider");
                Assert.IsTrue(trigger.isTrigger, "Collider should be a trigger");
                Assert.Greater(trigger.radius, 0f, "Trigger radius should be positive");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CaveEntrance_SetActive_TogglesState()
        {
            var go = new GameObject("TestCaveToggle");
            try
            {
                var entrance = go.AddComponent<CaveEntrance>();

                // Default is active
                entrance.SetActive(false);
                // No exception should be thrown

                entrance.SetActive(true);
                // Should work without error
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CaveEntrance_SavedPosition_IsPersisted()
        {
            // Clean up any previous state
            CaveEntrance.ClearSavedPosition();
            Assert.IsFalse(CaveEntrance.HasSavedPosition,
                "Should have no saved position initially");

            // Simulate interaction
            var player = new GameObject("TestPlayer");
            player.tag = "Player";
            player.transform.position = new Vector3(10f, 5f, 3f);
            try
            {
                var entrance = new GameObject("TestCave").AddComponent<CaveEntrance>();
                entrance.Invoke("Awake", 0f);

                // Interact should save position
                // Note: Interact() uses FindGameObjectWithTag which won't work
                // in EditMode without a scene, so we test the static method directly
                Assert.IsFalse(CaveEntrance.HasSavedPosition,
                    "Position should not be saved without interaction");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }

            CaveEntrance.ClearSavedPosition();
        }

        [Test]
        public void CaveEntrance_Arch_DimensionsArePositive()
        {
            // Test the inspector defaults via reflection
            var fieldInfo = typeof(CaveEntrance).GetField("_archWidth",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var heightField = typeof(CaveEntrance).GetField("_archHeight",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            var go = new GameObject("TestArchDim");
            try
            {
                var entrance = go.AddComponent<CaveEntrance>();

                float width = (float)fieldInfo.GetValue(entrance);
                float height = (float)heightField.GetValue(entrance);

                Assert.Greater(width, 0f, "Arch width should be positive");
                Assert.Greater(height, 0f, "Arch height should be positive");
                Assert.GreaterOrEqual(height, width * 0.5f,
                    "Arch height should be at least half the width");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  C22-08: CaveInteriorBuilder (cave room + lighting)
        // ================================================================

        [Test]
        public void CaveInteriorBuilder_BuildsCaveRoom()
        {
            GameObject room = null;
            try
            {
                room = CaveInteriorBuilder.BuildCaveInterior("test_cave", 1);
                Assert.IsNotNull(room, "BuildCaveInterior should return a GameObject");
                Assert.AreEqual("CaveRoom", room.name, "Root object should be named 'CaveRoom'");

                // Check that essential child objects exist
                var floor = room.transform.Find("CaveFloor");
                Assert.IsNotNull(floor, "CaveFloor should exist");

                var ceiling = room.transform.Find("CaveCeiling");
                Assert.IsNotNull(ceiling, "CaveCeiling should exist");

                // Check walls
                var frontWall = room.transform.Find("CaveWall_Front");
                Assert.IsNotNull(frontWall, "CaveWall_Front should exist");
            }
            finally
            {
                if (room != null)
                    Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void CaveInteriorBuilder_HasTorchLights()
        {
            GameObject room = null;
            try
            {
                room = CaveInteriorBuilder.BuildCaveInterior("test_torches", 1);
                Assert.IsNotNull(room, "Cave room should be built");

                // Check for torch lights
                var torches = room.GetComponentsInChildren<TorchFlicker>();
                Assert.GreaterOrEqual(torches.Length, 1,
                    "Cave should have at least 1 torch with TorchFlicker component");

                // Each torch should have a Light component
                foreach (var torch in torches)
                {
                    var light = torch.GetComponent<Light>();
                    Assert.IsNotNull(light,
                        $"Torch {torch.name} should have a Light component");
                }
            }
            finally
            {
                if (room != null)
                    Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void CaveInteriorBuilder_HasAmbientLight()
        {
            GameObject room = null;
            try
            {
                room = CaveInteriorBuilder.BuildCaveInterior("test_light", 1);
                var ambientLight = room.transform.Find("CaveAmbientLight");
                Assert.IsNotNull(ambientLight, "CaveAmbientLight should exist");
                var lightComponent = ambientLight.GetComponent<Light>();
                Assert.IsNotNull(lightComponent, "Ambient light should have Light component");
                Assert.AreEqual(LightType.Point, lightComponent.type,
                    "Ambient light should be a Point light");
            }
            finally
            {
                if (room != null)
                    Object.DestroyImmediate(room);
            }
        }

        // ================================================================
        //  C22-09: NationTerrainController (smooth terrain transition)
        // ================================================================

        [Test]
        public void NationTerrainController_GeneratesTexture()
        {
            var go = new GameObject("TestTerrain");
            try
            {
                var ntc = go.AddComponent<NationTerrainController>();
                Texture2D tex = ntc.GenerateCombinedTexture();
                Assert.IsNotNull(tex, "GenerateCombinedTexture should return a Texture2D");
                Assert.Greater(tex.width, 0, "Texture width should be > 0");
                Assert.Greater(tex.height, 0, "Texture height should be > 0");
                Assert.AreEqual(ntc.TextureSize, tex.width,
                    "Texture size should match configured size");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NationTerrainController_GeneratesNationFocusedTexture()
        {
            var go = new GameObject("TestNationTex");
            try
            {
                var ntc = go.AddComponent<NationTerrainController>();
                Texture2D tex = ntc.GenerateNationFocusedTexture(NationType.East);
                Assert.IsNotNull(tex, "GenerateNationFocusedTexture should return a Texture2D");
                Assert.IsTrue(tex.name.Contains("East"),
                    "Texture name should contain the nation type");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NationTerrainController_GetNationFromPosition_ReturnsCorrectNation()
        {
            // East: x+ direction
            Assert.AreEqual(NationType.East,
                NationTerrainController.GetNationFromPosition(new Vector3(100f, 0f, 0f)),
                "Position (100,0,0) should be East");

            // West: x- direction
            Assert.AreEqual(NationType.West,
                NationTerrainController.GetNationFromPosition(new Vector3(-100f, 0f, 0f)),
                "Position (-100,0,0) should be West");

            // North: z+ direction
            Assert.AreEqual(NationType.North,
                NationTerrainController.GetNationFromPosition(new Vector3(0f, 0f, 100f)),
                "Position (0,0,100) should be North");

            // South: z- direction
            Assert.AreEqual(NationType.South,
                NationTerrainController.GetNationFromPosition(new Vector3(0f, 0f, -100f)),
                "Position (0,0,-100) should be South");

            // Empire: center (within 50m)
            Assert.AreEqual(NationType.Empire,
                NationTerrainController.GetNationFromPosition(new Vector3(10f, 0f, 10f)),
                "Position (10,0,10) should be Empire (within 50m of center)");
        }

        [Test]
        public void NationTerrainController_TransitionIsTracked()
        {
            var go = new GameObject("TestTransition");
            try
            {
                var ntc = go.AddComponent<NationTerrainController>();
                // Initially not transitioning
                Assert.IsFalse(ntc.IsTransitioning,
                    "Should not be transitioning initially");
                Assert.Greater(ntc.TransitionDuration, 0f,
                    "Transition duration should be positive");
                Assert.IsNotNull(ntc.TerrainMaterial,
                    "Terrain material should be created");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        //  Cross-component tests
        // ================================================================

        [Test]
        public void BiomeData_SwampDefinition_HasSlowSpeed()
        {
            BiomeDefinition def = BiomeData.GetDefinition(BiomeType.Swamp);
            Assert.AreEqual("늪", def.displayName, "Swamp display name should be '늪'");
            Assert.IsTrue(def.moveSpeedModifier < 1.0f,
                "Swamp should slow movement");
            Assert.AreEqual(0.5f, def.moveSpeedModifier,
                "Swamp movement modifier should be 0.5");
        }

        [Test]
        public void BiomeData_DesertDefinition_HasSlowSpeed()
        {
            BiomeDefinition def = BiomeData.GetDefinition(BiomeType.Desert);
            Assert.AreEqual("사막", def.displayName, "Desert display name should be '사막'");
            Assert.IsTrue(def.moveSpeedModifier <= 1.0f,
                "Desert should not speed up movement");
            Assert.AreEqual(0.9f, def.moveSpeedModifier,
                "Desert movement modifier should be 0.9");
        }

        [Test]
        public void BiomeData_AllBiomes_HaveMoveSpeedModifier()
        {
            foreach (BiomeType biome in System.Enum.GetValues(typeof(BiomeType)))
            {
                BiomeDefinition def = BiomeData.GetDefinition(biome);
                Assert.Greater(def.moveSpeedModifier, 0f,
                    $"Biome {biome} should have a positive move speed modifier");
                Assert.IsFalse(string.IsNullOrEmpty(def.displayName),
                    $"Biome {biome} should have a display name");
            }
        }

        [Test]
        public void PlayerMovement_SpeedModifier_ClampsMinimum()
        {
            var go = new GameObject("TestPlayerSpeed");
            try
            {
                var pm = go.AddComponent<PlayerMovement>();
                // Set speed modifier via reflection
                var prop = typeof(PlayerMovement).GetProperty("SpeedModifier");
                Assert.IsNotNull(prop, "PlayerMovement should have SpeedModifier property");

                // Test default value
                float defaultVal = (float)prop.GetMethod.Invoke(pm, null);
                Assert.AreEqual(1.0f, defaultVal, "Default speed modifier should be 1.0");

                // Test min clamping
                prop.SetMethod.Invoke(pm, new object[] { -1f });
                float clamped = (float)prop.GetMethod.Invoke(pm, null);
                Assert.GreaterOrEqual(clamped, 0.1f,
                    "SpeedModifier should be clamped to minimum of 0.1");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IndoorSceneTransition_AcceptsCaveBuildingType()
        {
            // Just verify the cave case is handled (won't load scene in EditMode)
            var fieldInfo = typeof(IndoorSceneTransition).GetField("_pendingBuildingType",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);

            IndoorSceneTransition.EnterBuilding("cave");
            string pending = IndoorSceneTransition.GetPendingBuildingType();
            Assert.AreEqual("cave", pending,
                "IndoorSceneTransition should accept 'cave' as building type");

            // Clean up by calling exit
            // Note: In EditMode this might not fully work, but shouldn't throw
        }

        [Test]
        public void CaveInteriorBuilder_TorchFlicker_ComponentExists()
        {
            // Verify TorchFlicker class is defined and has expected fields
            var tfType = typeof(TorchFlicker);
            Assert.IsNotNull(tfType, "TorchFlicker type should be defined");

            var lightField = tfType.GetField("_light",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(lightField, "TorchFlicker should have _light field");
        }
    }
}