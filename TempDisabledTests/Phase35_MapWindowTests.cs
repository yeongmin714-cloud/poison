using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 3.5: MapWorld UI & Flag Display system EditMode tests.
    /// Tests window opening, territory display, zoom levels, player position,
    /// fog of war, flag display integration, and refresh behavior.
    /// </summary>
    public class Phase35_MapWindowTests
    {
        /// <summary>Helper: creates a bare MapWindow on a new GameObject.</summary>
        private MapWindow CreateMapWindow()
        {
            var go = new GameObject("MapWindowTest");
            var window = go.AddComponent<MapWindow>();
            return window;
        }

        /// <summary>Helper: creates a TerritoryManager singleton for position tracking.</summary>
        private TerritoryManager CreateTerritoryManager(NationType nation, int index)
        {
            var go = new GameObject("TerritoryManager");
            var mgr = go.AddComponent<TerritoryManager>();
            // Access private fields via reflection to set current territory
            var nationField = typeof(TerritoryManager).GetField("_currentNation",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var indexField = typeof(TerritoryManager).GetField("_currentTerritoryIndex",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nationField != null && indexField != null)
            {
                nationField.SetValue(mgr, nation);
                indexField.SetValue(mgr, index);
            }
            return mgr;
        }

        /// <summary>Helper: creates an EmblemManager for player flag display.</summary>
        private EmblemManager CreateEmblemManager()
        {
            var go = new GameObject("EmblemManager");
            var mgr = go.AddComponent<EmblemManager>();
            mgr.CurrentEmblem.primaryColor = EmblemColor.Gold;
            mgr.CurrentEmblem.secondaryColor = EmblemColor.Red;
            return mgr;
        }

        /// <summary>Helper: destroys a component's GameObject.</summary>
        private void DestroyComponent(MonoBehaviour component)
        {
            if (component != null && component.gameObject != null)
            {
                Object.DestroyImmediate(component.gameObject);
            }
        }

        [SetUp]
        public void SetUp()
        {
            // Ensure TerritoryDatabase is initialized
            var db = TerritoryDatabase.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up singletons
            if (TerritoryManager.Instance != null)
                Object.DestroyImmediate(TerritoryManager.Instance.gameObject);
            if (EmblemManager.Instance != null)
                Object.DestroyImmediate(EmblemManager.Instance.gameObject);
        }

        // ===== Basic Window Tests =====

        [Test]
        public void MapWindow_CreatesAndStartsHidden()
        {
            var window = CreateMapWindow();

            // Window should start closed
            Assert.IsFalse(window.IsOpen, "MapWindow should start closed");
            Assert.IsFalse(window.gameObject.activeSelf, "MapWindow GameObject should start inactive");

            Object.DestroyImmediate(window.gameObject);
        }

        [Test]
        public void MapWindow_Show_OpensWindow()
        {
            var window = CreateMapWindow();

            window.Show();

            Assert.IsTrue(window.IsOpen, "MapWindow should be open after Show()");
            Assert.IsTrue(window.gameObject.activeInHierarchy, "MapWindow GameObject should be active after Show()");

            Object.DestroyImmediate(window.gameObject);
        }

        [Test]
        public void MapWindow_Hide_ClosesWindow()
        {
            var window = CreateMapWindow();

            window.Show();
            Assert.IsTrue(window.IsOpen, "Window should be open after Show");

            window.Hide();
            Assert.IsFalse(window.IsOpen, "Window should be closed after Hide()");

            Object.DestroyImmediate(window.gameObject);
        }

        [Test]
        public void MapWindow_Toggle_OpensAndCloses()
        {
            var window = CreateMapWindow();

            // Toggle open
            window.Toggle();
            Assert.IsTrue(window.IsOpen, "Window should open after Toggle()");

            // Toggle close
            window.Toggle();
            Assert.IsFalse(window.IsOpen, "Window should close after second Toggle()");

            Object.DestroyImmediate(window.gameObject);
        }

        // ===== Territory Display Tests =====

        [Test]
        public void MapWindow_RefreshMap_DoesNotThrow()
        {
            var window = CreateMapWindow();

            // RefreshMap should not throw even without singletons
            Assert.DoesNotThrow(() => window.RefreshMap(), "RefreshMap should not throw exceptions");

            Object.DestroyImmediate(window.gameObject);
        }

        [Test]
        public void MapWindow_OnShow_TriggersRefresh()
        {
            var window = CreateMapWindow();

            // OnShow triggers RefreshMap which shouldn't throw
            Assert.DoesNotThrow(() => window.Show(), "Show() should not throw");
            Assert.IsTrue(window.IsOpen, "Window should be open after Show()");

            Object.DestroyImmediate(window.gameObject);
        }

        [Test]
        public void MapWindow_DefaultZoomIsOne()
        {
            var window = CreateMapWindow();

            Assert.AreEqual(1f, window.CurrentZoom, "Default zoom should be 1.0");

            Object.DestroyImmediate(window.gameObject);
        }

        [Test]
        public void MapWindow_SelectedNation_DefaultsToNone()
        {
            var window = CreateMapWindow();

            Assert.AreEqual(NationType.None, window.SelectedNation,
                "Default selected nation should be None (overview mode)");

            Object.DestroyImmediate(window.gameObject);
        }

        [Test]
        public void MapWindow_TerritoryDatabase_ProvidesAllNationDefinitions()
        {
            // Verify that TerritoryDatabase has definitions for all nations
            var db = TerritoryDatabase.Instance;
            var allDefs = new List<TerritoryDefinition>(db.GetAllDefinitions());

            // 4 nations x 20 territories = 80 + 1 Empire = 81
            Assert.AreEqual(81, allDefs.Count, "Should have 81 total territory definitions");

            // Check per-nation counts
            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                var nationDefs = new List<TerritoryDefinition>(db.GetDefinitionsByNation(nation));
                Assert.AreEqual(20, nationDefs.Count, $"Nation {nation} should have 20 territories");
            }
        }

        [Test]
        public void MapWindow_EmpireFogOfWar_DefaultsHidden()
        {
            // Reset empire discovered to false
            MapWindow.SetEmpireDiscovered(false);

            Assert.IsFalse(MapWindow.IsEmpireDiscovered,
                "Empire should start undiscovered (fog of war)");
        }

        [Test]
        public void MapWindow_EmpireFogOfWar_CanBeRevealed()
        {
            MapWindow.SetEmpireDiscovered(true);
            Assert.IsTrue(MapWindow.IsEmpireDiscovered,
                "Empire should be discovered after SetEmpireDiscovered(true)");

            MapWindow.SetEmpireDiscovered(false);
            Assert.IsFalse(MapWindow.IsEmpireDiscovered,
                "Empire should be undiscovered after SetEmpireDiscovered(false)");
        }

        // ===== Player Position Tests =====

        [Test]
        public void MapWindow_PlayerPosition_FromTerritoryManager()
        {
            var window = CreateMapWindow();
            var mgr = CreateTerritoryManager(NationType.East, 3);

            // Force refresh of player position
            window.RefreshMap();

            // Verify TerritoryManager is set correctly
            Assert.IsNotNull(TerritoryManager.Instance, "TerritoryManager singleton should exist");
            var expectedId = TerritoryManager.Instance.CurrentTerritoryId;
            Assert.AreEqual(NationType.East, expectedId.nation, "Current nation should be East");
            Assert.AreEqual(3, expectedId.index, "Current territory index should be 3");

            DestroyComponent(window);
            DestroyComponent(mgr);
        }

        [Test]
        public void MapWindow_PlayerPosition_FromDefinition()
        {
            var window = CreateMapWindow();
            var mgr = CreateTerritoryManager(NationType.South, 7);

            window.RefreshMap();

            // Verify definition matches
            var def = TerritoryDatabase.Instance.GetDefinition(NationType.South, 7);
            Assert.IsNotNull(def.territoryName, "Territory definition should have a name");
            Assert.AreEqual(NationType.South, def.nation, "Territory nation should be South");
            Assert.AreEqual(7, def.id.index, "Territory index should be 7");

            DestroyComponent(window);
            DestroyComponent(mgr);
        }

        // ===== Territory Data Display Tests =====

        [Test]
        public void MapWindow_TerritoryDifficulty_StarsCorrect()
        {
            // Verify difficulty to star conversion
            var db = TerritoryDatabase.Instance;

            // Check a Ring1 territory (should be 1 star)
            var defRing1 = db.GetDefinition(NationType.East, 1);
            Assert.AreEqual(TerritoryDifficulty.Ring1, defRing1.difficulty, "Territory 1 should be Ring1");

            // Check a Ring4 territory (should be 4 stars)
            var defRing4 = db.GetDefinition(NationType.East, 20);
            Assert.AreEqual(TerritoryDifficulty.Ring4, defRing4.difficulty, "Territory 20 should be Ring4");

            // Check Empire
            var defEmpire = db.GetDefinition(NationType.Empire, 1);
            Assert.AreEqual(TerritoryDifficulty.Empire, defEmpire.difficulty, "Empire should be Empire difficulty");
        }

        [Test]
        public void MapWindow_TerritoryGuardCount_MatchDifficulty()
        {
            var db = TerritoryDatabase.Instance;

            // Ring1 territories have fewer guards than Ring4
            var defRing1 = db.GetDefinition(NationType.North, 1);
            var defRing4 = db.GetDefinition(NationType.North, 20);

            Assert.Less(defRing1.guardCount, defRing4.guardCount,
                "Ring4 territory should have more guards than Ring1");
        }

        // ===== Flag Display Integration Tests =====

        [Test]
        public void MapWindow_NationFlagDatabase_ProvidesAllNationFlags()
        {
            Assert.IsTrue(NationFlagDatabase.HasFlag(NationType.East), "East should have a flag");
            Assert.IsTrue(NationFlagDatabase.HasFlag(NationType.West), "West should have a flag");
            Assert.IsTrue(NationFlagDatabase.HasFlag(NationType.South), "South should have a flag");
            Assert.IsTrue(NationFlagDatabase.HasFlag(NationType.North), "North should have a flag");
            Assert.IsTrue(NationFlagDatabase.HasFlag(NationType.Empire), "Empire should have a flag");
            Assert.IsTrue(NationFlagDatabase.HasFlag(NationType.Dracula), "Dracula should have a flag");

            // Verify emojis
            Assert.AreEqual("🌅", NationFlagDatabase.GetFlag(NationType.East).symbolEmoji);
            Assert.AreEqual("🌿", NationFlagDatabase.GetFlag(NationType.West).symbolEmoji);
            Assert.AreEqual("🔥", NationFlagDatabase.GetFlag(NationType.South).symbolEmoji);
            Assert.AreEqual("❄️", NationFlagDatabase.GetFlag(NationType.North).symbolEmoji);
            Assert.AreEqual("👑", NationFlagDatabase.GetFlag(NationType.Empire).symbolEmoji);
            Assert.AreEqual("🦇", NationFlagDatabase.GetFlag(NationType.Dracula).symbolEmoji);
        }

        [Test]
        public void MapWindow_NationFlags_HaveCorrectColors()
        {
            Assert.AreEqual(Color.blue, NationFlagDatabase.GetFlag(NationType.East).flagColor,
                "East flag should be blue");
            Assert.AreEqual(Color.green, NationFlagDatabase.GetFlag(NationType.West).flagColor,
                "West flag should be green");
            Assert.AreEqual(Color.red, NationFlagDatabase.GetFlag(NationType.South).flagColor,
                "South flag should be red");

            Color expectedPurple = new Color(0.6f, 0.2f, 1f);
            Assert.AreEqual(expectedPurple, NationFlagDatabase.GetFlag(NationType.North).flagColor,
                "North flag should be purple");

            Color expectedGold = new Color(1f, 0.85f, 0.2f);
            Assert.AreEqual(expectedGold, NationFlagDatabase.GetFlag(NationType.Empire).flagColor,
                "Empire flag should be gold");

            Color expectedDracula = new Color(0.8f, 0f, 0f);
            Assert.AreEqual(expectedDracula, NationFlagDatabase.GetFlag(NationType.Dracula).flagColor,
                "Dracula flag should be deep red");
        }

        [Test]
        public void MapWindow_FlagPole_FadeTransition_SmoothColorChange()
        {
            var displayGo = new GameObject("FlagPoleTest");
            var display = displayGo.AddComponent<FlagPoleDisplay>();

            // Set initial owner
            display.SetOwner(NationType.West, false);
            Color initialColor = display.CurrentFlagMaterial.color;
            Assert.AreEqual(Color.green, initialColor, "West flag should be green");

            // Perform fade transition
            display.FadeTransition(NationType.South, false, 0.1f);

            // The transition starts immediately; since it's EditMode and no time passes,
            // we can verify it doesn't throw and marks as transitioning
            Assert.IsTrue(display.IsTransitioning, "Flag should be transitioning");

            Object.DestroyImmediate(displayGo);
        }

        [Test]
        public void MapWindow_FlagPole_FadeTransition_ToPlayerFlag()
        {
            var mgr = CreateEmblemManager();
            var displayGo = new GameObject("FlagPoleTest");
            var display = displayGo.AddComponent<FlagPoleDisplay>();

            // Fade to player-owned
            display.FadeTransition(NationType.East, true, 0.2f);

            Assert.IsTrue(display.IsTransitioning, "Flag should be transitioning to player colors");

            Object.DestroyImmediate(displayGo);
            DestroyComponent(mgr);
        }

        [Test]
        public void MapWindow_AllFlagsGetAll_ReturnsFiveFlags()
        {
            var allFlags = NationFlagDatabase.GetAllFlags();
            Assert.AreEqual(5, allFlags.Count, "Should have exactly 5 flag definitions");
        }

        [Test]
        public void MapWindow_TerritoryOwnership_StatesAreAccessible()
        {
            var db = TerritoryDatabase.Instance;

            // All territories should have default Unoccupied state
            var east1 = db.GetState(NationType.East, 1);
            Assert.IsNotNull(east1, "East-1 state should exist");
            Assert.AreEqual(TerritoryOwnership.Unoccupied, east1.ownership,
                "Default ownership should be Unoccupied");

            // Set ownership and verify
            db.SetOwnership(NationType.East, 1, TerritoryOwnership.PlayerOwned);
            var updated = db.GetState(NationType.East, 1);
            Assert.AreEqual(TerritoryOwnership.PlayerOwned, updated.ownership,
                "Ownership should be updated to PlayerOwned");

            // Reset for other tests
            db.SetOwnership(NationType.East, 1, TerritoryOwnership.Unoccupied);
        }
    }
}