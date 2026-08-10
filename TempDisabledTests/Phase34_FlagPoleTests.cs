using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 34: FlagPoleDisplay EditMode tests.
    /// Tests procedural 3D flag pole creation, owner color changes,
    /// half-mast behavior, player emblem integration, and waving animation.
    /// </summary>
    public class Phase34_FlagPoleTests
    {
        /// <summary>Helper: sets up a bare FlagPoleDisplay on a new GameObject.</summary>
        private FlagPoleDisplay CreateFlagPoleDisplay()
        {
            var go = new GameObject("FlagPoleTest");
            var display = go.AddComponent<FlagPoleDisplay>();
            return display;
        }

        /// <summary>Helper: destroys a FlagPoleDisplay's GameObject.</summary>
        private void DestroyFlagPoleDisplay(FlagPoleDisplay display)
        {
            if (display != null && display.gameObject != null)
            {
                Object.DestroyImmediate(display.gameObject);
            }
        }

        [Test]
        public void FlagPoleDisplay_CreatesPoleAndFlagChildren()
        {
            var display = CreateFlagPoleDisplay();

            // After Awake, the component should have created child GameObjects
            Assert.IsNotNull(display.PoleObject, "PoleObject should not be null after Awake");
            Assert.IsNotNull(display.FlagObject, "FlagObject should not be null after Awake");

            // Verify names
            Assert.AreEqual("FlagPole", display.PoleObject.name, "Pole child name should be 'FlagPole'");
            Assert.AreEqual("Flag", display.FlagObject.name, "Flag child name should be 'Flag'");

            // Verify parent-child relationship
            Assert.AreEqual(display.gameObject, display.PoleObject.transform.parent.gameObject);
            Assert.AreEqual(display.gameObject, display.FlagObject.transform.parent.gameObject);

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_PoleAndFlagHaveMeshRenderers()
        {
            var display = CreateFlagPoleDisplay();

            Assert.IsNotNull(display.PoleObject.GetComponent<MeshRenderer>(), "Pole should have MeshRenderer");
            Assert.IsNotNull(display.FlagObject.GetComponent<MeshRenderer>(), "Flag should have MeshRenderer");

            // Verify primitive types via MeshFilter
            Assert.IsNotNull(display.PoleObject.GetComponent<MeshFilter>().sharedMesh,
                "Pole should have a mesh (Cylinder primitive)");
            Assert.IsNotNull(display.FlagObject.GetComponent<MeshFilter>().sharedMesh,
                "Flag should have a mesh (Cube primitive)");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_DefaultMaterialIsAssigned()
        {
            var display = CreateFlagPoleDisplay();

            // After Awake, both flag and pole should have materials
            Assert.IsNotNull(display.CurrentFlagMaterial, "Flag material should be assigned after Awake");
            Assert.AreEqual(Color.white, display.CurrentFlagMaterial.color,
                "Default flag color should be white");

            var poleRenderer = display.PoleObject.GetComponent<MeshRenderer>();
            Assert.IsNotNull(poleRenderer, "Pole should have MeshRenderer");
            Assert.IsNotNull(poleRenderer.sharedMaterial, "Pole should have a material assigned");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetOwnerEast_SetsBlueFlag()
        {
            var display = CreateFlagPoleDisplay();

            display.SetOwner(NationType.East, false);

            Assert.IsNotNull(display.CurrentFlagMaterial, "Flag material should not be null after SetOwner");
            Assert.AreEqual(Color.blue, display.CurrentFlagMaterial.color,
                "East nation flag should be blue");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetOwnerWest_SetsGreenFlag()
        {
            var display = CreateFlagPoleDisplay();

            display.SetOwner(NationType.West, false);

            Assert.AreEqual(Color.green, display.CurrentFlagMaterial.color,
                "West nation flag should be green");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetOwnerSouth_SetsRedFlag()
        {
            var display = CreateFlagPoleDisplay();

            display.SetOwner(NationType.South, false);

            Assert.AreEqual(Color.red, display.CurrentFlagMaterial.color,
                "South nation flag should be red");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetOwnerNorth_SetsPurpleFlag()
        {
            var display = CreateFlagPoleDisplay();

            display.SetOwner(NationType.North, false);

            Color expectedPurple = new Color(0.6f, 0.2f, 1f);
            Assert.AreEqual(expectedPurple, display.CurrentFlagMaterial.color,
                "North nation flag should be purple");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetOwnerEmpire_SetsGoldFlag()
        {
            var display = CreateFlagPoleDisplay();

            display.SetOwner(NationType.Empire, false);

            Color expectedGold = new Color(1f, 0.85f, 0.2f);
            Assert.AreEqual(expectedGold, display.CurrentFlagMaterial.color,
                "Empire flag should be gold");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetOwnerNone_SetsWhiteFlag()
        {
            var display = CreateFlagPoleDisplay();

            display.SetOwner(NationType.None, false);

            Assert.AreEqual(Color.white, display.CurrentFlagMaterial.color,
                "None nation flag should be white (fallback)");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetHalfMast_LowersFlagPosition()
        {
            var display = CreateFlagPoleDisplay();

            float originalBaseY = display.BaseFlagY;
            float expectedHalfMastY = originalBaseY * 0.5f;

            display.SetHalfMast(true);
            Assert.IsTrue(display.IsHalfMast, "IsHalfMast should be true after SetHalfMast(true)");

            float halfMastY = display.FlagObject.transform.localPosition.y;
            Assert.Less(halfMastY, originalBaseY,
                "Half-mast flag Y should be lower than full-mast Y");
            Assert.AreEqual(expectedHalfMastY, halfMastY, 0.01f,
                "Half-mast Y should be 50% of base Y");

            // Restore to full mast
            display.SetHalfMast(false);
            Assert.IsFalse(display.IsHalfMast, "IsHalfMast should be false after SetHalfMast(false)");

            float restoredY = display.FlagObject.transform.localPosition.y;
            Assert.AreEqual(originalBaseY, restoredY, 0.01f,
                "Flag Y should return to base position after SetHalfMast(false)");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetHalfMast_ToggleUpdatesPosition()
        {
            var display = CreateFlagPoleDisplay();

            float fullMastY = display.FlagObject.transform.localPosition.y;

            display.SetHalfMast(true);
            float loweredY = display.FlagObject.transform.localPosition.y;

            display.SetHalfMast(false);
            float restoredY = display.FlagObject.transform.localPosition.y;

            Assert.AreNotEqual(fullMastY, loweredY, "Full-mast and half-mast Y should differ");
            Assert.AreEqual(fullMastY, restoredY, 0.01f,
                "Flag Y should restore exactly to original full-mast position");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetPlayerFlag_WithEmblemManager_UsesPlayerColors()
        {
            // We need an EmblemManager for player flag to work
            var mgrGo = new GameObject("EmblemManager");
            var mgr = mgrGo.AddComponent<EmblemManager>();
            mgr.CurrentEmblem.primaryColor = EmblemColor.Red;
            mgr.CurrentEmblem.secondaryColor = EmblemColor.Gold;

            var display = CreateFlagPoleDisplay();
            display.SetOwner(NationType.East, true);

            // SetOwner with isPlayer=true calls SetPlayerFlag internally
            Assert.IsNotNull(display.CurrentFlagMaterial, "Flag material should exist after SetPlayerFlag");
            Assert.AreEqual(Color.red, display.CurrentFlagMaterial.color,
                "Player flag should use EmblemManager primary color (Red)");

            // Pole material should be secondary color (Gold = 1, 0.85, 0.2)
            var poleRenderer = display.PoleObject.GetComponent<MeshRenderer>();
            Color expectedGold = new Color(1f, 0.85f, 0.2f);
            Assert.AreEqual(expectedGold, poleRenderer.sharedMaterial.color,
                "Pole should use EmblemManager secondary color (Gold)");

            DestroyFlagPoleDisplay(display);
            Object.DestroyImmediate(mgrGo);
        }

        [Test]
        public void FlagPoleDisplay_FlagWavingAnimation_StartsEnabled()
        {
            var display = CreateFlagPoleDisplay();

            // By default, waving animation should be enabled
            Assert.IsTrue(display.IsWaveEnabled, "Waving animation should be enabled by default");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetWaveEnabled_TogglesAnimation()
        {
            var display = CreateFlagPoleDisplay();

            Assert.IsTrue(display.IsWaveEnabled, "Should start enabled");

            display.SetWaveEnabled(false);
            Assert.IsFalse(display.IsWaveEnabled, "Should be disabled after SetWaveEnabled(false)");

            display.SetWaveEnabled(true);
            Assert.IsTrue(display.IsWaveEnabled, "Should be re-enabled after SetWaveEnabled(true)");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_SetOwnerSwitchesBetweenNations()
        {
            var display = CreateFlagPoleDisplay();

            // Start with East (blue)
            display.SetOwner(NationType.East, false);
            Assert.AreEqual(Color.blue, display.CurrentFlagMaterial.color);

            // Switch to South (red)
            display.SetOwner(NationType.South, false);
            Assert.AreEqual(Color.red, display.CurrentFlagMaterial.color,
                "Flag color should change when switching from East to South");

            // Switch to Empire (gold)
            display.SetOwner(NationType.Empire, false);
            Color expectedGold = new Color(1f, 0.85f, 0.2f);
            Assert.AreEqual(expectedGold, display.CurrentFlagMaterial.color,
                "Flag color should change when switching to Empire");

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_ChildrenFoundByTransformFind()
        {
            var display = CreateFlagPoleDisplay();

            // Verify we can find children by name via transform
            var foundPole = display.gameObject.transform.Find("FlagPole");
            var foundFlag = display.gameObject.transform.Find("Flag");

            Assert.IsNotNull(foundPole, "FlagPole child should be findable via transform.Find");
            Assert.IsNotNull(foundFlag, "Flag child should be findable via transform.Find");

            Assert.AreEqual(display.PoleObject, foundPole.gameObject);
            Assert.AreEqual(display.FlagObject, foundFlag.gameObject);

            DestroyFlagPoleDisplay(display);
        }

        [Test]
        public void FlagPoleDisplay_PoleScaleMatchesDimensions()
        {
            var display = CreateFlagPoleDisplay();

            // The pole is a Cylinder primitive (default height 2, radius 0.5)
            // Our scale is: x = _poleRadius*2, y = _poleHeight*0.5, z = _poleRadius*2
            Vector3 expectedScale = new Vector3(0.08f * 2f, 3f * 0.5f, 0.08f * 2f);

            Vector3 actualScale = display.PoleObject.transform.localScale;
            Assert.AreEqual(expectedScale.x, actualScale.x, 0.001f, "Pole X scale should match poleRadius*2");
            Assert.AreEqual(expectedScale.y, actualScale.y, 0.001f, "Pole Y scale should match poleHeight*0.5");
            Assert.AreEqual(expectedScale.z, actualScale.z, 0.001f, "Pole Z scale should match poleRadius*2");

            DestroyFlagPoleDisplay(display);
        }
    }
}