using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using System.Reflection;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class GasSprayerPhase4ATests
    {
        private GameObject _inventoryObject;
        private GameObject _controllerObject;
        private PlayerInventory _inventory;
        private GasSprayerController _controller;
        private System.Func<PlayerInventory.ItemData, bool> _previousConsumableOverride;
        private GameObject _legacyPlayer;
        private readonly List<GameObject> _legacyClouds = new List<GameObject>();
        private readonly List<GasCloudField> _cloudsBeforeTest = new List<GasCloudField>();

        [SetUp]
        public void SetUp()
        {
            _legacyClouds.Clear();
            _cloudsBeforeTest.Clear();
            foreach (var field in GameObject.FindObjectsOfType<GasCloudField>())
                _cloudsBeforeTest.Add(field);
            _previousConsumableOverride = ConsumableSystem.PreUseOverride;
            PlayerInventory.ResetInstance();
            _inventoryObject = new GameObject("Gas dose test inventory");
            _inventory = _inventoryObject.AddComponent<PlayerInventory>();
            InitializeInventoryForTest();
            _controllerObject = new GameObject("Gas dose test controller");
            _controller = _controllerObject.AddComponent<GasSprayerController>();
            EnsureAwakeInitialized(_controller);
            _controller.Equip(GasSprayerGrade.Wood);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _legacyClouds.Count; i++)
                if (_legacyClouds[i] != null) Object.DestroyImmediate(_legacyClouds[i]);
            if (_legacyPlayer != null) Object.DestroyImmediate(_legacyPlayer);
            ConsumableSystem.PreUseOverride = _previousConsumableOverride;
            if (_controllerObject != null) Object.DestroyImmediate(_controllerObject);
            PlayerInventory.ResetInstance();
            if (_inventoryObject != null) Object.DestroyImmediate(_inventoryObject);
        }

        [Test]
        public void Test10GasVerifyScene_EquipsAndLoadsOnePotionForHeldSprayWithoutAutoStarting()
        {
            string setupSource = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath, "Scripts/Systems/TestTerritoryCombatSetup.cs"));
            int setupStart = setupSource.IndexOf("private void SetupGasVerifyScene()", System.StringComparison.Ordinal);
            Assert.That(setupStart, Is.GreaterThanOrEqualTo(0), "Test_10 must retain its focused gas verification setup.");
            int setupEnd = setupSource.IndexOf("\n        }", setupStart, System.StringComparison.Ordinal);
            Assert.That(setupEnd, Is.GreaterThan(setupStart), "SetupGasVerifyScene must have a bounded method body.");
            string setup = setupSource.Substring(setupStart, setupEnd - setupStart);

            int equip = setup.IndexOf("ctrl.Equip(GasSprayerGrade.Wood)", System.StringComparison.Ordinal);
            int poisonSeed = setup.IndexOf("id = \"Poison_TestPotion\"", System.StringComparison.Ordinal);
            int load = setup.IndexOf("GasPotionLoader.LoadPotion(ctrl, \"Poison_TestPotion\")", System.StringComparison.Ordinal);
            Assert.That(setup, Does.Contain("player.GetComponent<GasSprayer>() == null"),
                "Test_10 must retain the direct G-held plume input owner.");
            Assert.That(setup, Does.Not.Contain("SprayInputHandler"),
                "Test_10 must not attach the legacy Mouse1/right-click handler that starts the one-shot spray effect.");
            Assert.That(setup, Does.Contain("\"ProjectName.UI.Toolkit.GasSprayUTK\", \"Ensure\""),
                "Test_10 needs the gas spray HUD.");
            Assert.That(equip, Is.GreaterThanOrEqualTo(0));
            Assert.That(poisonSeed, Is.GreaterThan(equip), "The poison potion must be seeded after Wood is equipped.");
            Assert.That(load, Is.GreaterThan(poisonSeed), "Load exactly one after adding the potion to the seeded inventory.");
            Assert.That(setup, Does.Not.Contain("SetGInputHeld(true)"), "Setup must not auto-start spraying.");
            Assert.That(setup, Does.Not.Contain("StartSpray()"), "Setup must not auto-start spraying.");
        }

        [Test]
        public void Test10RightClickRemainsRTSInput_WhileLegacyMouse1SprayRemainsAvailableElsewhere()
        {
            string setupSource = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath, "Scripts/Systems/TestTerritoryCombatSetup.cs"));
            string legacyHandlerSource = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath, "Scripts/Systems/SprayInputHandler.cs"));
            int setupStart = setupSource.IndexOf("private void SetupGasVerifyScene()", System.StringComparison.Ordinal);
            Assert.That(setupStart, Is.GreaterThanOrEqualTo(0));
            int setupEnd = setupSource.IndexOf("\n        }", setupStart, System.StringComparison.Ordinal);
            Assert.That(setupEnd, Is.GreaterThan(setupStart));
            string setup = setupSource.Substring(setupStart, setupEnd - setupStart);

            Assert.That(setup, Does.Not.Contain("SprayInputHandler"),
                "Test_10 must leave right-click available for RTS commands and camera interaction.");
            StringAssert.Contains("KeyCode.Mouse1", legacyHandlerSource,
                "The legacy Mouse1 spray binding must remain available in scenes that use it.");
            StringAssert.Contains("_controller.StartSpray();", legacyHandlerSource,
                "Legacy Mouse1 must retain its public one-shot-compatible spray start path.");
        }

        [Test]
        public void LoadPotion_RemovesExactlyOnePotionAndStartsOneFullDose()
        {
            AddPotion("herb_red", 3);

            Assert.AreEqual(1, GasPotionLoader.LoadPotion(_controller, "herb_red"));

            Assert.AreEqual(2, _inventory.GetItemCount("herb_red"));
            Assert.AreEqual("herb_red", _controller.LoadedPotionId);
            Assert.AreEqual(1, _controller.LoadedPotionCount);
            Assert.AreEqual(_controller.PotionDoseDuration, _controller.PotionDoseTimeRemaining);
        }

        [Test]
        public void CanLoadPotion_RejectsSameTypeWhenDoseAlreadyLoaded()
        {
            AddPotion("herb_red", 2);
            Assert.IsTrue(GasPotionLoader.CanLoadPotion(_controller, "herb_red"));
            Assert.AreEqual(1, GasPotionLoader.LoadPotion(_controller, "herb_red"));

            Assert.IsFalse(GasPotionLoader.CanLoadPotion(_controller, "herb_red"));
            Assert.IsFalse(GasPotionLoader.CanLoadPotion(_controller, "herb_blue"));
            Assert.AreEqual(1, _controller.LoadedPotionCount);
            Assert.AreEqual(1, _inventory.GetItemCount("herb_red"));
        }

        [Test]
        public void DoseTime_DepletesOnlyWhileSpraying_AndAutomaticallyLoadsNextSameTypeDose()
        {
            AddPotion("herb_red", 2);
            Assert.AreEqual(2, _inventory.GetItemCount("herb_red"), "The test starts with two inventory items.");
            GasPotionLoader.LoadPotion(_controller, "herb_red");
            _controller.CurrentSprayTimeRemaining = 100f;

            Assert.AreEqual(1, _inventory.GetItemCount("herb_red"), "Loading consumes one inventory item immediately.");
            InvokeControllerStartMethod();
            _controller.AdvanceSprayTime(_controller.PotionDoseDuration * 0.4f);
            float pausedDoseTime = _controller.PotionDoseTimeRemaining;
            _controller.StopSpray();
            _controller.AdvanceSprayTime(100f);

            Assert.AreEqual(pausedDoseTime, _controller.PotionDoseTimeRemaining, 0.001f);
            Assert.AreEqual(1, _inventory.GetItemCount("herb_red"), "The initial load already consumed one item.");

            InvokeControllerStartMethod();
            _controller.AdvanceSprayTime(pausedDoseTime);

            Assert.IsTrue(_controller.IsSpraying);
            Assert.AreEqual("herb_red", _controller.LoadedPotionId);
            Assert.AreEqual(1, _controller.LoadedPotionCount);
            Assert.AreEqual(0, _inventory.GetItemCount("herb_red"), "The last inventory item is consumed to auto-load the next dose.");
            Assert.AreEqual(_controller.PotionDoseDuration, _controller.PotionDoseTimeRemaining);
        }

        [Test]
        public void DoseDepletionWithoutStock_StopsSprayingAndClearsDoseWithoutChangingGasAccounting()
        {
            AddPotion("herb_red", 1);
            GasPotionLoader.LoadPotion(_controller, "herb_red");
            _controller.CurrentSprayTimeRemaining = 100f;
            float gasBefore = _controller.CurrentSprayTimeRemaining;
            InvokeControllerStartMethod();

            _controller.AdvanceSprayTime(_controller.PotionDoseDuration);

            Assert.IsFalse(_controller.IsSpraying);
            Assert.AreEqual(string.Empty, _controller.LoadedPotionId);
            Assert.AreEqual(0, _controller.LoadedPotionCount);
            Assert.AreEqual(0f, _controller.PotionDoseTimeRemaining);
            Assert.Less(_controller.CurrentSprayTimeRemaining, gasBefore);
        }

        [Test]
        public void SimultaneousDoseAndFuelDepletion_ConsumesDoseAndStopsWithReload()
        {
            AddPotion("herb_red", 1);
            GasPotionLoader.LoadPotion(_controller, "herb_red");
            float simultaneousBoundary = _controller.PotionDoseDuration;
            _controller.CurrentSprayTimeRemaining = simultaneousBoundary * 2f;
            int depletedEvents = 0;
            _controller.OnPotionDoseDepleted += () => depletedEvents++;
            InvokeControllerStartMethod();

            _controller.AdvanceSprayTime(simultaneousBoundary);

            Assert.IsFalse(_controller.IsSpraying);
            Assert.IsTrue(_controller.IsReloading, "Fuel depletion at the same instant still starts canister reload.");
            Assert.AreEqual(0f, _controller.CurrentSprayTimeRemaining, 0.001f);
            Assert.AreEqual(1, depletedEvents, "The dose boundary must be resolved even when fuel empties simultaneously.");
            Assert.AreEqual(string.Empty, _controller.LoadedPotionId);
            Assert.AreEqual(0, _controller.LoadedPotionCount);
            Assert.AreEqual(0f, _controller.PotionDoseTimeRemaining);
            Assert.AreEqual(0, _inventory.GetItemCount("herb_red"));
        }

        [Test]
        public void SimultaneousBoundaries_AutoReloadDoseFromInventoryWithoutExhaustedState()
        {
            AddPotion("herb_red", 2);
            GasPotionLoader.LoadPotion(_controller, "herb_red");
            _controller.CurrentSprayTimeRemaining = _controller.PotionDoseDuration * 2f;
            int depletedEvents = 0;
            _controller.OnPotionDoseDepleted += () => depletedEvents++;
            InvokeControllerStartMethod();

            _controller.AdvanceSprayTime(_controller.PotionDoseDuration);

            Assert.IsFalse(_controller.IsSpraying);
            Assert.IsTrue(_controller.IsReloading);
            Assert.AreEqual(1, depletedEvents);
            Assert.AreEqual("herb_red", _controller.LoadedPotionId);
            Assert.AreEqual(1, _controller.LoadedPotionCount);
            Assert.AreEqual(_controller.PotionDoseDuration, _controller.PotionDoseTimeRemaining, 0.001f);
            Assert.AreEqual(0, _inventory.GetItemCount("herb_red"), "Exactly one matching inventory item is auto-loaded.");
        }

        [Test]
        public void ExhaustedDoseHud_IsTransientAccessibleAndManualGRequestHidesImmediately()
        {
            string hudSource = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath, "Scripts/UI/Toolkit/GasSprayUTK.cs"));
            string sprayerSource = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath, "Scripts/Systems/GasSprayer.cs"));

            StringAssert.Contains("ShowDoseExhaustedStatus", hudSource);
            StringAssert.Contains("Dose exhausted — reload a dose to continue", hudSource);
            StringAssert.Contains("_doseLabel.tooltip", hudSource);
            StringAssert.Contains("StartingIn(1800)", hudSource);
            StringAssert.Contains("!_doseExhaustedStatusMode", hudSource);
            StringAssert.Contains("OnGPressRequested += HandleGPressRequested", sprayerSource);
            StringAssert.Contains("HandleGPressRequested", sprayerSource);
            StringAssert.Contains("InvokeGasSprayUIAction(\"HideSprayStatus\")", sprayerSource);
            StringAssert.Contains("LoadedPotionCount <= 0", sprayerSource);
        }

        [Test]
        public void GHeldInputHasOneOwner_AndLegacyHandlerDoesNotReadG()
        {
            string sprayerSource = System.IO.File.ReadAllText("Assets/Scripts/Systems/GasSprayer.cs");
            string handlerSource = System.IO.File.ReadAllText("Assets/Scripts/Systems/SprayInputHandler.cs");

            int heldDeclaration = sprayerSource.IndexOf("bool gHeld");
            int keyboardGuard = sprayerSource.IndexOf("keyboard != null", heldDeclaration);
            int heldRead = sprayerSource.IndexOf("gKey.isPressed", heldDeclaration);
            int heldForward = sprayerSource.IndexOf("SetGInputHeld(gHeld)", heldDeclaration);
            Assert.That(heldDeclaration, Is.GreaterThanOrEqualTo(0), "The input owner must store the held G state.");
            Assert.That(keyboardGuard, Is.GreaterThan(heldDeclaration), "The keyboard null guard belongs to the held-state read.");
            Assert.That(heldRead, Is.GreaterThan(keyboardGuard), "The G key state must be read after the keyboard guard.");
            Assert.That(heldForward, Is.GreaterThan(heldRead), "The held G state must be forwarded after it is read.");
            StringAssert.DoesNotContain("gKey.wasPressedThisFrame", sprayerSource);
            StringAssert.DoesNotContain("ToggleSpray", sprayerSource);
            StringAssert.DoesNotContain("gKey", handlerSource);
            StringAssert.DoesNotContain("_sprayToggleKey", handlerSource);
        }

        [Test]
        public void GHeldPlumeBypassesOneShotButManualAndLegacyMouseHoldRetainIt()
        {
            string controllerSource = System.IO.File.ReadAllText("Assets/Scripts/Systems/GasSprayerController.cs");
            string legacyHandlerSource = System.IO.File.ReadAllText("Assets/Scripts/Systems/SprayInputHandler.cs");

            StringAssert.Contains("StartSpray(false);", controllerSource,
                "The G-held continuous plume must not trigger SpecialEffectsController's legacy one-shot cloud.");
            StringAssert.Contains("public void StartSpray()\n        {\n            StartSpray(true);", controllerSource,
                "Manual/public StartSpray must preserve the existing one-shot effect behavior.");
            StringAssert.Contains("_controller.StartSpray();", legacyHandlerSource,
                "The legacy Mouse1 hold must continue through the public effect-triggering start path.");
        }

        [Test]
        public void GInputHold_StartsOnceWhileHeld_AndReleasePausesExactDoseAndFuelForResume()
        {
            AddPotion("herb_red", 1);
            GasPotionLoader.LoadPotion(_controller, "herb_red");
            _controller.CurrentSprayTimeRemaining = 100f;
            float startingFuel = _controller.CurrentSprayTimeRemaining;

            InvokeControllerHeldMethod(true);
            Assert.IsTrue(_controller.IsSpraying, "A G press should start emission immediately.");
            _controller.AdvanceSprayTime(_controller.PotionDoseDuration * 0.4f);
            float pausedDose = _controller.PotionDoseTimeRemaining;
            float pausedFuel = _controller.CurrentSprayTimeRemaining;

            InvokeControllerHeldMethod(false);
            Assert.IsFalse(_controller.IsSpraying, "G release should stop emission immediately.");
            _controller.AdvanceSprayTime(100f);
            Assert.AreEqual(pausedDose, _controller.PotionDoseTimeRemaining, 0.001f,
                "Dose time must remain exactly paused after release.");
            Assert.AreEqual(pausedFuel, _controller.CurrentSprayTimeRemaining, 0.001f,
                "Canister fuel must remain exactly paused after release.");

            InvokeControllerHeldMethod(true);
            Assert.IsTrue(_controller.IsSpraying, "Re-pressing G resumes the same active dose.");
            _controller.AdvanceSprayTime(pausedDose);
            Assert.IsFalse(_controller.IsSpraying, "Emission stops when the resumed dose is exhausted.");
            Assert.AreEqual(string.Empty, _controller.LoadedPotionId);
            Assert.AreEqual(0, _controller.LoadedPotionCount);
            Assert.AreEqual(0f, _controller.PotionDoseTimeRemaining, 0.001f);
            Assert.AreEqual(startingFuel - _controller.PotionDoseDuration * 2f,
                _controller.CurrentSprayTimeRemaining, 0.001f,
                "Fuel drains only during active emission, at the existing loaded-potion rate.");
        }

        [Test]
        public void GInputHold_IndoorTransitionSuspendsUntilPhysicalReleaseAndFreshPress()
        {
            bool wasIndoor = UITransitionState.IndoorActive;
            try
            {
                _controller.CurrentSprayTimeRemaining = 100f;
                UITransitionState.IndoorActive = false;
                InvokeControllerHeldMethod(true);
                Assert.IsTrue(_controller.IsSpraying, "An outdoor G press should start spraying.");

                UITransitionState.IndoorActive = true;
                InvokeControllerHeldMethod(true);
                Assert.IsFalse(_controller.IsSpraying, "Entering indoor mode must suspend a held spray.");

                UITransitionState.IndoorActive = false;
                InvokeControllerHeldMethod(true);
                Assert.IsFalse(_controller.IsSpraying,
                    "Leaving indoor mode while G remains held must not silently resume spraying.");

                InvokeControllerHeldMethod(false);
                InvokeControllerHeldMethod(true);
                Assert.IsTrue(_controller.IsSpraying,
                    "A physical release followed by a fresh press should allow spraying again.");
            }
            finally
            {
                UITransitionState.IndoorActive = wasIndoor;
            }
        }

        [Test]
        public void GInputHold_DoesNotToggleOffWhileKeyRemainsHeld()
        {
            int changes = 0;
            _controller.OnSprayingChanged += _ => changes++;

            InvokeControllerHeldMethod(true);
            InvokeControllerHeldMethod(true);
            InvokeControllerHeldMethod(true);

            Assert.IsTrue(_controller.IsSpraying, "A held key means continuous spray, not repeated toggle calls.");
            Assert.AreEqual(1, changes, "Only the press edge starts emission.");

            InvokeControllerHeldMethod(false);
            InvokeControllerHeldMethod(false);
            Assert.IsFalse(_controller.IsSpraying);
            Assert.AreEqual(2, changes, "Only the release edge stops emission.");
        }

        [Test]
        public void GInputHasOnePhysicalConsumer_IncludingTestSetup()
        {
            string sprayerSource = System.IO.File.ReadAllText("Assets/Scripts/Systems/GasSprayer.cs");
            string handlerSource = System.IO.File.ReadAllText("Assets/Scripts/Systems/SprayInputHandler.cs");
            string testSetupSource = System.IO.File.ReadAllText("Assets/Scripts/Systems/TestGasBombSetup.cs");

            int heldDeclaration = sprayerSource.IndexOf("bool gHeld");
            int keyboardGuard = sprayerSource.IndexOf("keyboard != null", heldDeclaration);
            int heldRead = sprayerSource.IndexOf("gKey.isPressed", heldDeclaration);
            int heldForward = sprayerSource.IndexOf("SetGInputHeld(gHeld)", heldDeclaration);
            Assert.That(heldDeclaration, Is.GreaterThanOrEqualTo(0), "The input owner must store the held G state.");
            Assert.That(keyboardGuard, Is.GreaterThan(heldDeclaration), "The keyboard null guard belongs to the held-state read.");
            Assert.That(heldRead, Is.GreaterThan(keyboardGuard), "The G key state must be read after the keyboard guard.");
            Assert.That(heldForward, Is.GreaterThan(heldRead), "The held G state must be forwarded after it is read.");
            StringAssert.DoesNotContain("gKey.wasPressedThisFrame", sprayerSource);
            StringAssert.DoesNotContain("ToggleSpray", sprayerSource);
            StringAssert.DoesNotContain("gKey", handlerSource);
            StringAssert.DoesNotContain("gKey", testSetupSource);
            StringAssert.DoesNotContain("KeyCode.G", testSetupSource);
        }

        [Test]
        public void GInputHold_RequiresEquippedAndReloadComplete_AndReleaseResetsRejectedPress()
        {
            bool wasIndoor = UITransitionState.IndoorActive;
            try
            {
                UITransitionState.IndoorActive = false;
                _controller.Unequip();
                InvokeControllerHeldMethod(true);
                Assert.IsFalse(_controller.IsSpraying, "G must not spray while equipment is unequipped.");

                _controller.Equip(GasSprayerGrade.Wood);
                _controller.CurrentSprayTimeRemaining = 0f;
                _controller.StartReload();
                Assert.IsTrue(_controller.IsReloading, "The test requires an active reload.");
                InvokeControllerHeldMethod(false);
                InvokeControllerHeldMethod(true);
                Assert.IsFalse(_controller.IsSpraying, "G must not spray during reload.");

                InvokeControllerCompleteReloadMethod();
                InvokeControllerHeldMethod(true);
                Assert.IsFalse(_controller.IsSpraying, "A rejected held press must not become a deferred start when reload completes.");
                InvokeControllerHeldMethod(false);
                InvokeControllerHeldMethod(true);
                Assert.IsTrue(_controller.IsSpraying, "A fresh press after reload completes starts spray.");
            }
            finally
            {
                UITransitionState.IndoorActive = wasIndoor;
            }
        }

        [Test]
        public void GInputHold_IsReleasedWhenInputOwnerDisablesOrLosesFocus()
        {
            string sprayerSource = System.IO.File.ReadAllText("Assets/Scripts/Systems/GasSprayer.cs");
            StringAssert.Contains("if (_controller != null) _controller.SetGInputHeld(false);", sprayerSource);
            StringAssert.Contains("private void OnApplicationFocus(bool hasFocus)", sprayerSource);
            StringAssert.Contains("if (!hasFocus && _controller != null)", sprayerSource);
            StringAssert.Contains("_controller.SetGInputHeld(false);", sprayerSource);
        }

        [Test]
        public void GInputHold_DoesNotStopSprayOwnedByAnotherInputOnRelease()
        {
            InvokeControllerStartMethod();
            InvokeControllerHeldMethod(true);
            InvokeControllerHeldMethod(false);
            Assert.IsTrue(_controller.IsSpraying,
                "Releasing G must not stop a spray that was already active before this G hold.");
        }


        [Test]
        public void DoseHud_BindsDoseRemainingAndDurationToStatusValues()
        {
            string hudSource = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath, "Scripts/UI/Toolkit/GasSprayUTK.cs"));

            StringAssert.Contains("PotionDoseTimeRemaining", hudSource);
            StringAssert.Contains("PotionDoseDuration", hudSource);
            StringAssert.Contains("_doseBarFill.style.width", hudSource);
            StringAssert.Contains("_doseLabel.text", hudSource);
        }

        [Test]
        public void ContinuousSprayUsesBoundedWorldSpacePlumePoolAndBakedTextures()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/Systems/GasSprayer.cs");
            StringAssert.Contains("main.simulationSpace = ParticleSystemSimulationSpace.World", source);
            StringAssert.Contains("particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);", source,
                "The default system must be fully stopped before main.duration is changed in Unity 6.");
            Assert.That(source.IndexOf("particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);", System.StringComparison.Ordinal),
                Is.LessThan(source.IndexOf("main.duration = 1f", System.StringComparison.Ordinal)),
                "Stop/clear must happen before the main duration mutation.");
            StringAssert.Contains("CreatePlumeLayer(\"GasSprayer_Soft\", \"UI/GasPlumeSoft\"", source);
            StringAssert.Contains("CreatePlumeLayer(\"GasSprayer_Puff\", \"UI/GasPlumeLobe\"", source);
            StringAssert.Contains("CreatePlumeLayer(\"GasSprayer_Wisp\", \"UI/GasPlumeWisp\"", source);
            StringAssert.Contains("CreatePlumeLayer(\"GasSprayer_Soft\", \"UI/GasPlumeSoft\", 48f, 3f, 0.78f, 0.4f, 0.9f, 0.24f)", source);
            StringAssert.Contains("CreatePlumeLayer(\"GasSprayer_Puff\", \"UI/GasPlumeLobe\", 24f, 3f, 0.62f, 0.75f, 1.2f, 0.42f)", source);
            StringAssert.Contains("CreatePlumeLayer(\"GasSprayer_Wisp\", \"UI/GasPlumeWisp\", 18f, 3f, 0.34f, 0.3f, 0.75f, 0.55f)", source);
            StringAssert.Contains("main.maxParticles = layerName.Contains(\"Puff\") ? 96 : layerName.Contains(\"Soft\") ? 192 : 72", source);
            StringAssert.Contains("ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime", source,
                "The module is retrieved through a local value before changing its enabled state.");
            StringAssert.Contains("velocity.enabled = false", source,
                "Initial cone speed is sampled at birth; lifetime velocity must not rotate with the nozzle.");
            StringAssert.Contains("ParticleSystemSimulationSpace.World", source,
                "Existing particles must stay in world space while the emitter follows the player.");
            StringAssert.Contains("ParticleSystemStopBehavior.StopEmitting", source);
            StringAssert.Contains("private static readonly Dictionary<Texture2D, Material> SharedGasMaterials", source);
            StringAssert.Contains("SharedGasMaterials.TryGetValue(texture, out Material cached)", source);
            StringAssert.Contains("RuntimeInitializeLoadType.SubsystemRegistration", source);
            StringAssert.Contains("SharedGasMaterialUsers", source);
            StringAssert.Contains("RetainGasMaterial(texture)", source);
            StringAssert.Contains("ReleaseGasMaterial(_plumeTextures[i])", source);
            StringAssert.DoesNotContain("CreatePrimitive(PrimitiveType.Quad)", source);
            StringAssert.DoesNotContain("fog.transform.LookAt(_cameraTransform)", source);
            StringAssert.DoesNotContain("new Material(Shader.Find(\"Universal Render Pipeline/Unlit\"))", source);
            StringAssert.DoesNotContain("SpawnFogEffect()", source);

            foreach (string textureName in new[] { "GasPlumeSoft", "GasPlumeLobe", "GasPlumeWisp" })
            {
                string texturePath = System.IO.Path.Combine(Application.dataPath, "Resources/UI/" + textureName + ".png");
                Assert.IsTrue(System.IO.File.Exists(texturePath), "Missing baked gas plume texture: " + textureName);

                string metaPath = texturePath + ".meta";
                Assert.IsTrue(System.IO.File.Exists(metaPath), "Missing Unity texture importer metadata: " + textureName);
                string meta = System.IO.File.ReadAllText(metaPath);
                StringAssert.Contains("isReadable: 1", meta, "The baked mask importer must remain readable.");
                StringAssert.Contains("alphaIsTransparency: 1", meta, "The particle mask must import its true alpha.");
            }
        }

        [Test]
        public void ContinuousSprayPuffsExpandAndFadeAcrossAnApproximatelyThreeSecondLifetime()
        {
            var sprayerObject = new GameObject("GasSprayer plume lifetime test");
            try
            {
                GasSprayerController controller = _controllerObject.GetComponent<GasSprayerController>();
                var controllerField = typeof(GasSprayer).GetField("_controller",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(controllerField);
                GasSprayer sprayer = sprayerObject.AddComponent<GasSprayer>();
                controllerField.SetValue(sprayer, controller);
                var createPool = typeof(GasSprayer).GetMethod("CreatePlumePool",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(createPool);
                createPool.Invoke(sprayer, null);
                var systemsField = typeof(GasSprayer).GetField("_plumeSystems",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(systemsField, "The visual pool remains owned by GasSprayer.");
                var systems = systemsField.GetValue(sprayer) as List<ParticleSystem>;
                Assert.IsNotNull(systems);
                Assert.AreEqual(3, systems.Count, "All three continuous plume layers should exist.");

                float[] expectedRates = { 48f, 24f, 18f };
                int[] expectedCapacities = { 192, 96, 72 };
                for (int i = 0; i < systems.Count; i++)
                {
                    ParticleSystem particles = systems[i];
                    ParticleSystem.MainModule main = particles.main;
                    Assert.AreEqual(expectedCapacities[i], main.maxParticles,
                        "The " + particles.name + " particle cap must support the denser plume throughout its lifetime.");
                    Assert.AreEqual(expectedRates[i], particles.emission.rateOverTime.constant, 0.001f,
                        "The " + particles.name + " layer must emit at the increased target rate.");
                }

                foreach (ParticleSystem particles in systems)
                {
                    ParticleSystem.MainModule main = particles.main;
                    Assert.AreEqual(ParticleSystemCurveMode.TwoConstants, main.startLifetime.mode);
                    Assert.That((main.startLifetime.constantMin + main.startLifetime.constantMax) * 0.5f,
                        Is.InRange(2.5f, 3.5f), "Each puff should live about three seconds before fading out.");
                    Assert.LessOrEqual(main.startLifetime.constantMin, 3f);
                    Assert.GreaterOrEqual(main.startLifetime.constantMax, 3f);
                    Assert.AreEqual(ParticleSystemCurveMode.TwoConstants, main.startRotation.mode,
                        "Randomized baked puff orientation avoids a repeated stamp-like trail.");
                    Assert.AreEqual(0f, main.startRotation.constantMin, 0.001f);
                    Assert.AreEqual(Mathf.PI * 2f, main.startRotation.constantMax, 0.001f);
                    Assert.IsTrue(particles.sizeOverLifetime.enabled,
                        "Puffs should broaden over their lifetime before the alpha fade removes them.");
                    Assert.IsTrue(particles.colorOverLifetime.enabled,
                        "The broadened puff should fade away over that lifetime.");
                }
            }
            finally
            {
                Object.DestroyImmediate(sprayerObject);
            }
        }

        [Test]
        public void ContinuousSprayPoolMaterials_AreConfiguredForTransparentRendering()
        {
            var sprayerObject = new GameObject("GasSprayer transparent material test");
            try
            {
                GasSprayer sprayer = sprayerObject.AddComponent<GasSprayer>();
                var controllerField = typeof(GasSprayer).GetField("_controller",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(controllerField);
                controllerField.SetValue(sprayer, _controller);

                var systemsField = typeof(GasSprayer).GetField("_plumeSystems",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(systemsField);
                var systems = systemsField.GetValue(sprayer) as List<ParticleSystem>;
                Assert.IsNotNull(systems);
                if (systems.Count == 0)
                {
                    var createPool = typeof(GasSprayer).GetMethod("CreatePlumePool",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.IsNotNull(createPool);
                    createPool.Invoke(sprayer, null);
                }

                Assert.AreEqual(3, systems.Count, "All three plume layers should share configured runtime materials.");
                foreach (ParticleSystem particles in systems)
                {
                    ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
                    Assert.IsNotNull(renderer);
                    Material material = renderer.sharedMaterial;
                    Assert.IsNotNull(material, "Every plume layer needs its shared gas material.");

                    AssertMaterialFloat(material, "_Surface", 1f);
                    AssertMaterialFloat(material, "_Blend", 0f);
                    AssertMaterialFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    AssertMaterialFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    AssertMaterialFloat(material, "_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                    AssertMaterialFloat(material, "_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    AssertMaterialFloat(material, "_ZWrite", 0f);
                    Assert.IsTrue(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),
                        "URP Particles Unlit must enable its transparent surface variant.");
                    Assert.AreEqual("Transparent", material.GetTag("RenderType", false),
                        "Gas plume materials must be tagged as transparent.");
                    Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, material.renderQueue,
                        "Gas plume materials must render in the transparent queue.");

                    Texture2D texture = material.HasProperty("_BaseMap")
                        ? material.GetTexture("_BaseMap") as Texture2D
                        : material.GetTexture("_MainTex") as Texture2D;
                    Assert.IsNotNull(texture, "Every plume layer must use its baked partial-alpha texture.");
                    bool hasPartialAlpha = false;
                    foreach (Color pixel in texture.GetPixels())
                    {
                        if (pixel.a > 0f && pixel.a < 1f)
                        {
                            hasPartialAlpha = true;
                            break;
                        }
                    }
                    Assert.IsTrue(hasPartialAlpha, "Each plume layer texture must retain partially transparent pixels.");
                }
            }
            finally
            {
                Object.DestroyImmediate(sprayerObject);
            }
        }

        [Test]
        public void GHeldPlume_UsesIncreasedEmissionRatesAndCapsThatAvoidLifetimePlateau()
        {
            var sprayerObject = new GameObject("GasSprayer emission tuning test");
            try
            {
                GasSprayer sprayer = sprayerObject.AddComponent<GasSprayer>();
                var controllerField = typeof(GasSprayer).GetField("_controller",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(controllerField);
                controllerField.SetValue(sprayer, _controller);

                var createPool = typeof(GasSprayer).GetMethod("CreatePlumePool",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(createPool);
                createPool.Invoke(sprayer, null);

                var systemsField = typeof(GasSprayer).GetField("_plumeSystems",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(systemsField);
                var systems = systemsField.GetValue(sprayer) as List<ParticleSystem>;
                Assert.IsNotNull(systems);
                Assert.AreEqual(3, systems.Count, "The existing three-layer G-held plume remains intact.");

                var expectedRates = new Dictionary<string, float>
                {
                    { "GasSprayer_Soft", 48f },
                    { "GasSprayer_Puff", 24f },
                    { "GasSprayer_Wisp", 18f }
                };
                var expectedCaps = new Dictionary<string, int>
                {
                    { "GasSprayer_Soft", 192 },
                    { "GasSprayer_Puff", 96 },
                    { "GasSprayer_Wisp", 72 }
                };

                foreach (ParticleSystem particles in systems)
                {
                    string layerName = particles.gameObject.name;
                    Assert.IsTrue(expectedRates.ContainsKey(layerName), "The established plume layers should be preserved.");
                    Assert.AreEqual(expectedRates[layerName], particles.emission.rateOverTime.constant, 0.001f,
                        "The G-held visual amount should use the approved bounded emission rate.");
                    ParticleSystem.MainModule main = particles.main;
                    Assert.AreEqual(expectedCaps[layerName], main.maxParticles,
                        "Each layer needs enough capacity to avoid capping its maximum-lifetime steady-state emission.");
                    Assert.AreEqual(3f, main.startLifetime.constantMin, 0.001f,
                        "Visual amount tuning must not shorten or lengthen plume lifetime.");
                    Assert.AreEqual(3.5f, main.startLifetime.constantMax, 0.001f,
                        "Visual amount tuning must preserve the existing lifetime range.");
                    Assert.GreaterOrEqual(main.maxParticles,
                        Mathf.CeilToInt(expectedRates[layerName] * main.startLifetime.constantMax),
                        "The cap must exceed steady-state particles at the maximum lifetime.");
                }
            }
            finally
            {
                Object.DestroyImmediate(sprayerObject);
            }
        }

        [Test]
        public void PoisonPlume_UsesModestlyDenserLayersAndDarkerGreenWithoutChangingOtherPotionColors()
        {
            var sprayerObject = new GameObject("GasSprayer poison plume tuning test");
            try
            {
                GasSprayer sprayer = sprayerObject.AddComponent<GasSprayer>();
                var controllerField = typeof(GasSprayer).GetField("_controller",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(controllerField);
                controllerField.SetValue(sprayer, _controller);

                var systemsField = typeof(GasSprayer).GetField("_plumeSystems",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(systemsField);
                var systems = systemsField.GetValue(sprayer) as List<ParticleSystem>;
                Assert.IsNotNull(systems);
                if (systems.Count == 0)
                {
                    var createPool = typeof(GasSprayer).GetMethod("CreatePlumePool",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.IsNotNull(createPool);
                    createPool.Invoke(sprayer, null);
                }

                Assert.AreEqual(3, systems.Count, "The three established plume layers remain in use.");
                var expectedRates = new Dictionary<string, float>
                {
                    { "GasSprayer_Soft", 48f },
                    { "GasSprayer_Puff", 24f },
                    { "GasSprayer_Wisp", 18f }
                };
                var expectedCaps = new Dictionary<string, int>
                {
                    { "GasSprayer_Soft", 192 },
                    { "GasSprayer_Puff", 96 },
                    { "GasSprayer_Wisp", 72 }
                };
                foreach (ParticleSystem particles in systems)
                {
                    string layerName = particles.gameObject.name;
                    Assert.IsTrue(expectedRates.ContainsKey(layerName),
                        "The existing plume layer design should be preserved.");
                    Assert.AreEqual(expectedRates[layerName],
                        particles.emission.rateOverTime.constant, 0.001f,
                        "Each visual layer should have the modestly increased shared emission rate.");
                    ParticleSystem.MainModule main = particles.main;
                    Assert.AreEqual(expectedCaps[layerName], main.maxParticles,
                        "Only the Soft and Puff caps should grow enough to prevent a plateau at the existing lifetime.");
                    Assert.AreEqual(3f, main.startLifetime.constantMin, 0.001f,
                        "Emission tuning must preserve the existing minimum lifetime.");
                    Assert.AreEqual(3.5f, main.startLifetime.constantMax, 0.001f,
                        "Emission tuning must preserve the existing maximum lifetime.");
                }

                Color poison = ReadFogColor(sprayer, "_poisonFogColor");
                Assert.AreEqual(new Color(0.2f, 0.65f, 0.08f, 0.5f), poison,
                    "Poison should use the darker green tint without changing its established alpha.");
                Assert.AreEqual(new Color(0.6f, 0.15f, 0.85f, 0.45f), ReadFogColor(sprayer, "_mentalFogColor"));
                Assert.AreEqual(new Color(0.15f, 0.85f, 0.15f, 0.45f), ReadFogColor(sprayer, "_healFogColor"));
                Assert.AreEqual(new Color(0.15f, 0.35f, 0.95f, 0.45f), ReadFogColor(sprayer, "_buffFogColor"));

                _controller.LoadPotion("Poison_test", 1);
                AeroGasSprayPayload publishedPayload = default;
                bool receivedSpray = false;
                System.Action<AeroGasSprayPayload> captureSpray = payload =>
                {
                    publishedPayload = payload;
                    receivedSpray = true;
                };
                AeroGasSprayBridge.SprayUpdated += captureSpray;
                try
                {
                    MethodInfo emitPlume = typeof(GasSprayer).GetMethod("EmitPlume",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.IsNotNull(emitPlume);
                    emitPlume.Invoke(sprayer, null);
                    Assert.IsTrue(receivedSpray, "An active poison plume should publish its rendering payload.");
                    Assert.AreEqual(0.78f, publishedPayload.Density, 0.001f,
                        "The published visual plume density should match the approved modest increase.");
                    Assert.AreEqual(poison, publishedPayload.Tint,
                        "Only the poison payload should receive the darker green tint.");
                }
                finally
                {
                    AeroGasSprayBridge.SprayUpdated -= captureSpray;
                    AeroGasSprayBridge.Stop();
                }
            }
            finally
            {
                Object.DestroyImmediate(sprayerObject);
            }
        }

        [Test]
        public void ContinuousSprayVisualDoesNotChangeGameplayTickOwnerOrInterval()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/Systems/GasSprayer.cs");
            StringAssert.Contains("_effectTimer += Time.deltaTime", source);
            StringAssert.Contains("while (_effectTimer >= _effectInterval)", source);
            StringAssert.Contains("_effectTimer -= _effectInterval", source);
            StringAssert.Contains("ApplySprayEffects();", source);
            StringAssert.Contains("if (_controller != null) _controller.SetGInputHeld(false);", source);
        }

        [Test]
        public void IsolatedCustomRendererIsNeverEnabledByOrdinarySprayInput()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/Systems/GasSprayer.cs");
            StringAssert.DoesNotContain("SetRendererSessionEnabled(true)", source,
                "The custom project-owned renderer is only enabled by the separate opt-in isolated session.");
            StringAssert.Contains("AeroGasSprayBridge.Publish(new AeroGasSprayPayload(transform, radius, height, density, tint))", source,
                "Active gameplay spray publishes bounded, player-following, potion-tinted data.");
            StringAssert.Contains("Mathf.Clamp(_controller.GetCurrentSprayerData().sprayRange, 0.1f, 8f)", source);
            StringAssert.Contains("Mathf.Clamp(radius * 0.75f, 0.1f, 6f)", source);
            StringAssert.Contains("const float density = 0.78f", source);
            StringAssert.Contains("AeroGasSprayBridge.Stop();", source,
                "Stopping or releasing plume emission must stop the published spray state.");
        }

        [Test]
        public void LegacyCloudConsumption_ConsumesOneLoadedItemAndResetsDoseForRemainingStack()
        {
            _legacyPlayer = new GameObject("Legacy gas-cloud player");
            _legacyPlayer.tag = "Player";
            _controller.LoadPotion("Poison_legacy", 3);
            float gasBefore = _controller.CurrentSprayTimeRemaining;
            GasCloudLauncher.RegisterHook();

            bool handled = ConsumableSystem.PreUseOverride(new PlayerInventory.ItemData
            {
                id = "Poison_legacy",
                displayName = "Poison legacy",
                category = PlayerInventory.ItemCategory.Potion
            });

            Assert.IsTrue(handled);
            Assert.AreEqual("Poison_legacy", _controller.LoadedPotionId);
            Assert.AreEqual(2, _controller.LoadedPotionCount);
            Assert.AreEqual(_controller.PotionDoseDuration, _controller.PotionDoseTimeRemaining);
            Assert.AreEqual(gasBefore - 3f, _controller.CurrentSprayTimeRemaining, 0.001f);

            CaptureCreatedLegacyClouds();
        }

        [Test]
        public void LegacyCloudConsumption_ConsumesLastDoseAndClearsActiveDoseState()
        {
            _legacyPlayer = new GameObject("Legacy gas-cloud player");
            _legacyPlayer.tag = "Player";
            _controller.LoadPotion("Poison_legacy", 1);
            GasCloudLauncher.RegisterHook();

            bool handled = ConsumableSystem.PreUseOverride(new PlayerInventory.ItemData
            {
                id = "Poison_legacy",
                displayName = "Poison legacy",
                category = PlayerInventory.ItemCategory.Potion
            });

            Assert.IsTrue(handled);
            Assert.AreEqual(string.Empty, _controller.LoadedPotionId);
            Assert.AreEqual(0, _controller.LoadedPotionCount);
            Assert.AreEqual(0f, _controller.PotionDoseTimeRemaining);

            CaptureCreatedLegacyClouds();
        }

        private void CaptureCreatedLegacyClouds()
        {
            int createdCount = 0;
            foreach (var field in GameObject.FindObjectsOfType<GasCloudField>())
            {
                if (_cloudsBeforeTest.Contains(field)) continue;
                _legacyClouds.Add(field.gameObject);
                createdCount++;
            }
            Assert.AreEqual(1, createdCount, "Legacy consumable should create one gas cloud.");
        }

        private static Color ReadFogColor(GasSprayer sprayer, string fieldName)
        {
            FieldInfo field = typeof(GasSprayer).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "GasSprayer should retain serialized fog color " + fieldName + ".");
            return (Color)field.GetValue(sprayer);
        }

        private static void AssertMaterialFloat(Material material, string property, float expected)
        {
            Assert.IsTrue(material.HasProperty(property), "Plume shader must expose " + property + ".");
            Assert.AreEqual(expected, material.GetFloat(property), 0.001f,
                "Unexpected plume material GPU state for " + property + ".");
        }

        private void AddPotion(string id, int count)
        {
            bool added = _inventory.AddItem(new PlayerInventory.ItemData
            {
                id = id,
                displayName = id,
                category = PlayerInventory.ItemCategory.Herb,
                maxStack = 99
            }, count);
            Assert.IsTrue(added, $"Could not add test potion '{id}' x{count} to the initialized inventory.");
        }

        private void InitializeInventoryForTest()
        {
            Assert.IsNotNull(_inventory, "Test inventory component should be created.");

            // AddComponent may or may not invoke Awake in EditMode. Invoke it only if
            // this component did not already claim the singleton, avoiding a second
            // initialization or accidentally accepting a stale inventory instance.
            if (PlayerInventory.Instance != _inventory)
                EnsureAwakeInitialized(_inventory);

            FieldInfo slotsField = typeof(PlayerInventory).GetField(
                "_slots", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(slotsField, "PlayerInventory should define its private _slots field.");
            if (slotsField.GetValue(_inventory) == null)
            {
                FieldInfo maxSlotsField = typeof(PlayerInventory).GetField(
                    "_maxSlots", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(maxSlotsField, "PlayerInventory should define its private _maxSlots field.");
                int maxSlots = (int)maxSlotsField.GetValue(_inventory);
                Assert.Greater(maxSlots, 0, "Test inventory must have at least one slot.");
                slotsField.SetValue(_inventory,
                    System.Array.CreateInstance(typeof(PlayerInventory.ItemSlot), maxSlots));
            }
            Assert.IsNotNull(slotsField.GetValue(_inventory),
                "Test inventory slots must be initialized before inventory operations.");

            Assert.AreSame(_inventory, PlayerInventory.Instance,
                "Test inventory must be the active PlayerInventory instance after initialization.");
        }

        private static void EnsureAwakeInitialized(MonoBehaviour component)
        {
            MethodInfo awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(awake, $"{component.GetType().Name} should define Awake for fixture initialization.");
            awake.Invoke(component, null);
        }

        private void InvokeControllerHeldMethod(bool held)
        {
            MethodInfo method = typeof(GasSprayerController).GetMethod(
                "SetGInputHeld", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(bool), typeof(bool) }, null);
            Assert.IsNotNull(method, "SetGInputHeld(bool, bool) should remain an internal deterministic test seam.");
            method.Invoke(_controller, new object[] { held, false });
        }

        private void InvokeControllerStartMethod()
        {
            MethodInfo method = typeof(GasSprayerController).GetMethod(
                "StartSpray", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(bool) }, null);
            Assert.IsNotNull(method, "StartSpray(bool) should remain an internal deterministic test seam.");
            method.Invoke(_controller, new object[] { false });
        }

        private void InvokeControllerCompleteReloadMethod()
        {
            MethodInfo method = typeof(GasSprayerController).GetMethod(
                "CompleteReload", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "CompleteReload should remain the controller's private reload-completion path.");
            method.Invoke(_controller, null);
        }
    }
}
