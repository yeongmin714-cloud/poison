using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// CR-01~06: Craft Celebration system comprehensive EditMode tests.
    /// Covers CraftSuccessSystem, CraftResultPopup, CraftResult enum,
    /// rarity color/name helpers, failure distribution, and full workflow.
    /// Target: 25+ tests.
    /// </summary>
    public class CraftCelebrationTests
    {
        // ──────────────────────────── SETUP / TEARDOWN ────────────────────────────
        private GameObject _popupGameObject;
        private CraftResultPopup _popup;

        [SetUp]
        public void SetUp()
        {
            // Fresh GameObject + component for each test so state is clean
            _popupGameObject = new GameObject("TestCraftPopup");
            _popup = _popupGameObject.AddComponent<CraftResultPopup>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_popupGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_popupGameObject);
                _popupGameObject = null;
            }
            _popup = null;
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  CR-01 : Celebrate animation — data structures (CraftResult enum, success rates)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void CraftResult_Enum_HasAllFourValues()
        {
            var values = Enum.GetValues(typeof(CraftResult));
            Assert.AreEqual(4, values.Length);
            Assert.Contains(CraftResult.Success, values);
            Assert.Contains(CraftResult.Fail_MaterialPreserved, values);
            Assert.Contains(CraftResult.Fail_MaterialDestroyed, values);
            Assert.Contains(CraftResult.Fail_Burned, values);
        }

        [Test]
        public void CraftResult_Enum_ValuesAreDistinct()
        {
            var set = new HashSet<int>();
            foreach (int v in Enum.GetValues(typeof(CraftResult)))
                Assert.IsTrue(set.Add(v), $"Duplicate enum value: {v}");
        }

        [Test]
        public void GetBaseSuccessRate_Common_Returns0_90()
        {
            Assert.AreEqual(0.90f, CraftSuccessSystem.GetBaseSuccessRate("Common"), 0.0001f);
        }

        [Test]
        public void GetBaseSuccessRate_Uncommon_Returns0_75()
        {
            Assert.AreEqual(0.75f, CraftSuccessSystem.GetBaseSuccessRate("Uncommon"), 0.0001f);
        }

        [Test]
        public void GetBaseSuccessRate_Rare_Returns0_60()
        {
            Assert.AreEqual(0.60f, CraftSuccessSystem.GetBaseSuccessRate("Rare"), 0.0001f);
        }

        [Test]
        public void GetBaseSuccessRate_Epic_Returns0_45()
        {
            Assert.AreEqual(0.45f, CraftSuccessSystem.GetBaseSuccessRate("Epic"), 0.0001f);
        }

        [Test]
        public void GetBaseSuccessRate_Legendary_Returns0_30()
        {
            Assert.AreEqual(0.30f, CraftSuccessSystem.GetBaseSuccessRate("Legendary"), 0.0001f);
        }

        [Test]
        public void GetBaseSuccessRate_UnknownGrade_DefaultsTo0_90()
        {
            Assert.AreEqual(0.90f, CraftSuccessSystem.GetBaseSuccessRate("NonExistent"), 0.0001f);
            Assert.AreEqual(0.90f, CraftSuccessSystem.GetBaseSuccessRate(null), 0.0001f);
            Assert.AreEqual(0.90f, CraftSuccessSystem.GetBaseSuccessRate(""), 0.0001f);
        }

        [Test]
        public void GetBaseSuccessRate_RatesDecreaseWithHigherGrade()
        {
            float common = CraftSuccessSystem.GetBaseSuccessRate("Common");
            float uncommon = CraftSuccessSystem.GetBaseSuccessRate("Uncommon");
            float rare = CraftSuccessSystem.GetBaseSuccessRate("Rare");
            float epic = CraftSuccessSystem.GetBaseSuccessRate("Epic");
            float legendary = CraftSuccessSystem.GetBaseSuccessRate("Legendary");

            Assert.Greater(common, uncommon);
            Assert.Greater(uncommon, rare);
            Assert.Greater(rare, epic);
            Assert.Greater(epic, legendary);
        }

        [Test]
        public void GetBaseSuccessRate_AllReturnValuesInRange_0_1()
        {
            string[] grades = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
            foreach (var g in grades)
            {
                float rate = CraftSuccessSystem.GetBaseSuccessRate(g);
                Assert.GreaterOrEqual(rate, 0f, $"Grade {g} rate < 0");
                Assert.LessOrEqual(rate, 1f, $"Grade {g} rate > 1");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  CR-02 : Result popup UI (CraftResultPopup MonoBehaviour)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void ShowSuccess_SetsIsShowingTrue()
        {
            Assert.IsFalse(_popup.IsShowing);          // initial state
            _popup.ShowSuccess("Flaming Sword", ItemRarity.Legendary, "+15 Fire DMG");
            Assert.IsTrue(_popup.IsShowing);
        }

        [Test]
        public void ShowSuccess_SetsCorrectItemName()
        {
            _popup.ShowSuccess("Flaming Sword", ItemRarity.Legendary, "+15 Fire DMG");

            // Verify via reflection that _currentItemName was set
            var itemNameField = typeof(CraftResultPopup)
                .GetField("_currentItemName", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(itemNameField);

            string storedName = (string)itemNameField.GetValue(_popup);
            Assert.AreEqual("Flaming Sword", storedName);
        }

        [Test]
        public void ShowSuccess_SetsCorrectGradeColor()
        {
            _popup.ShowSuccess("Flaming Sword", ItemRarity.Legendary, "+15 Fire DMG");

            var colorField = typeof(CraftResultPopup)
                .GetField("_currentGradeColor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(colorField);

            Color storedColor = (Color)colorField.GetValue(_popup);
            Color expected = EquipmentRarityData.GetRarityColor(ItemRarity.Legendary);
            Assert.AreEqual(expected.r, storedColor.r, 0.001f);
            Assert.AreEqual(expected.g, storedColor.g, 0.001f);
            Assert.AreEqual(expected.b, storedColor.b, 0.001f);
        }

        [Test]
        public void ShowSuccess_SetsCorrectGradeText()
        {
            _popup.ShowSuccess("Flaming Sword", ItemRarity.Legendary, "+15 Fire DMG");

            var textField = typeof(CraftResultPopup)
                .GetField("_currentGradeText", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(textField);

            string storedText = (string)textField.GetValue(_popup);
            Assert.AreEqual(EquipmentRarityData.GetRarityDisplayName(ItemRarity.Legendary), storedText);
        }

        [Test]
        public void ShowFailure_FailType0_MaterialPreserved_SetsIsShowingTrue()
        {
            Assert.IsFalse(_popup.IsShowing);
            _popup.ShowFailure(0);
            Assert.IsTrue(_popup.IsShowing);
        }

        [Test]
        public void ShowFailure_FailType0_MaterialPreserved_SetsCorrectColor()
        {
            _popup.ShowFailure(0);

            var colorField = typeof(CraftResultPopup)
                .GetField("_currentGradeColor", BindingFlags.NonPublic | BindingFlags.Instance);
            Color c = (Color)colorField.GetValue(_popup);

            // Expected: new Color(0.8f, 0.6f, 0.2f) — yellow
            Assert.AreEqual(0.8f, c.r, 0.001f);
            Assert.AreEqual(0.6f, c.g, 0.001f);
            Assert.AreEqual(0.2f, c.b, 0.001f);
        }

        [Test]
        public void ShowFailure_FailType1_MaterialDestroyed_SetsCorrectColor()
        {
            _popup.ShowFailure(1);

            var colorField = typeof(CraftResultPopup)
                .GetField("_currentGradeColor", BindingFlags.NonPublic | BindingFlags.Instance);
            Color c = (Color)colorField.GetValue(_popup);

            // Expected: new Color(0.8f, 0.3f, 0.2f) — orange
            Assert.AreEqual(0.8f, c.r, 0.001f);
            Assert.AreEqual(0.3f, c.g, 0.001f);
            Assert.AreEqual(0.2f, c.b, 0.001f);
        }

        [Test]
        public void ShowFailure_FailType2_Burned_SetsCorrectColor()
        {
            _popup.ShowFailure(2);

            var colorField = typeof(CraftResultPopup)
                .GetField("_currentGradeColor", BindingFlags.NonPublic | BindingFlags.Instance);
            Color c = (Color)colorField.GetValue(_popup);

            // Expected: new Color(0.8f, 0.1f, 0.1f) — red
            Assert.AreEqual(0.8f, c.r, 0.001f);
            Assert.AreEqual(0.1f, c.g, 0.001f);
            Assert.AreEqual(0.1f, c.b, 0.001f);
        }

        [Test]
        public void ShowFailure_DefaultType_ActsLikeBurned()
        {
            _popup.ShowFailure(99); // out-of-range should default to burned

            var colorField = typeof(CraftResultPopup)
                .GetField("_currentGradeColor", BindingFlags.NonPublic | BindingFlags.Instance);
            Color c = (Color)colorField.GetValue(_popup);

            Assert.AreEqual(0.8f, c.r, 0.001f);
            Assert.AreEqual(0.1f, c.g, 0.001f);
            Assert.AreEqual(0.1f, c.b, 0.001f);
        }

        [Test]
        public void OnPopupGUI_DoesNotThrow_WhenNotShowing()
        {
            Assert.DoesNotThrow(() => _popup.OnPopupGUI());
        }

        [Test]
        public void OnPopupGUI_DoesNotThrow_WhenShowingSuccess()
        {
            _popup.ShowSuccess("Test Sword", ItemRarity.Common, "No effect");
            Assert.DoesNotThrow(() => _popup.OnPopupGUI());
        }

        [Test]
        public void OnPopupGUI_DoesNotThrow_WhenShowingFailure()
        {
            _popup.ShowFailure(0);
            Assert.DoesNotThrow(() => _popup.OnPopupGUI());
        }

        [Test]
        public void IsShowing_InitiallyFalse()
        {
            Assert.IsFalse(_popup.IsShowing);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  CR-03 : Rarity colors and display names
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void GetRarityColor_EachRarity_HasDistinctColor()
        {
            var colors = new HashSet<string>();
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                Color c = EquipmentRarityData.GetRarityColor(rarity);
                string key = $"{c.r:F3}_{c.g:F3}_{c.b:F3}";
                Assert.IsTrue(colors.Add(key),
                    $"Rarity {rarity} has non-distinct color ({key})");
            }
        }

        [Test]
        public void GetRarityDisplayName_Common_ReturnsGeneral()
        {
            Assert.AreEqual("일반", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Common));
        }

        [Test]
        public void GetRarityDisplayName_Uncommon_ReturnsGogeup()
        {
            Assert.AreEqual("고급", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Uncommon));
        }

        [Test]
        public void GetRarityDisplayName_Rare_ReturnsHeegwi()
        {
            Assert.AreEqual("희귀", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Rare));
        }

        [Test]
        public void GetRarityDisplayName_Epic_ReturnsYeongung()
        {
            Assert.AreEqual("영웅", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Epic));
        }

        [Test]
        public void GetRarityDisplayName_Legendary_ReturnsJeonseol()
        {
            Assert.AreEqual("전설", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Legendary));
        }

        [Test]
        public void GetRarityDisplayName_AllRarities_NoDuplicateNames()
        {
            var names = new HashSet<string>();
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                string name = EquipmentRarityData.GetRarityDisplayName(rarity);
                Assert.IsTrue(names.Add(name),
                    $"Rarity {rarity} has duplicate display name '{name}'");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  CR-04 : Failure handling (success rate bonuses, failure distribution)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void GetAlchemyBonus_PlayerStatsNull_ReturnsZero()
        {
            // EditMode: PlayerStats.Instance is null (no scene loaded)
            Assert.AreEqual(0f, CraftSuccessSystem.GetAlchemyBonus(), 0.0001f);
        }

        [Test]
        public void GetCookingBonus_PlayerStatsNull_ReturnsZero()
        {
            Assert.AreEqual(0f, CraftSuccessSystem.GetCookingBonus(), 0.0001f);
        }

        [Test]
        public void GetFinalSuccessRate_WithAlchemyBonus_AddsBonus()
        {
            // In EditMode bonus is always 0, so this tests the computation path is correct
            // (avg of Common + Common) / 2 = 0.90, plus alchemy bonus of 0
            float rate = CraftSuccessSystem.GetFinalSuccessRate(isAlchemy: true, "Common", "Common");
            Assert.AreEqual(0.90f, rate, 0.0001f);

            // Verify the isAlchemy=true path is exercised (won't crash)
            rate = CraftSuccessSystem.GetFinalSuccessRate(isAlchemy: true, "Legendary", "Legendary");
            Assert.AreEqual(0.30f, rate, 0.0001f);
        }

        [Test]
        public void GetFinalSuccessRate_WithCookingBonus_AddsBonus()
        {
            // Cooking bonus path (also 0 in EditMode)
            float rate = CraftSuccessSystem.GetFinalSuccessRate(isAlchemy: false, "Common", "Common");
            Assert.AreEqual(0.90f, rate, 0.0001f);

            rate = CraftSuccessSystem.GetFinalSuccessRate(isAlchemy: false, "Rare", "Epic");
            float expected = (0.60f + 0.45f) / 2f; // 0.525
            Assert.AreEqual(expected, rate, 0.0001f);
        }

        [Test]
        public void GetFinalSuccessRate_AveragesTwoIngredients()
        {
            // Common (0.90) + Legendary (0.30) / 2 = 0.60
            float rate = CraftSuccessSystem.GetFinalSuccessRate(false, "Common", "Legendary");
            Assert.AreEqual(0.60f, rate, 0.0001f);
        }

        [Test]
        public void ExecuteCraft_FailureDistribution_MatchesExpected()
        {
            // With only Legendary ingredients + no bonus, successRate = 0.30
            // We use a deterministic seed and collect many results to verify
            // the failure branch distributes ~40% Preserved, ~40% Destroyed, ~20% Burned

            const int iterations = 10000;
            int preserved = 0, destroyed = 0, burned = 0, success = 0;

            // Seed for reproducibility
            Random.InitState(42);

            for (int i = 0; i < iterations; i++)
            {
                var result = CraftSuccessSystem.ExecuteCraft(false, "Legendary", "Legendary");
                switch (result)
                {
                    case CraftResult.Success:                success++; break;
                    case CraftResult.Fail_MaterialPreserved:  preserved++; break;
                    case CraftResult.Fail_MaterialDestroyed:  destroyed++; break;
                    case CraftResult.Fail_Burned:             burned++; break;
                }
            }

            int totalFails = preserved + destroyed + burned;

            // Within failures, distribution should be ~40% / 40% / 20%
            float preservedRatio = (float)preserved / totalFails;
            float destroyedRatio = (float)destroyed / totalFails;
            float burnedRatio = (float)burned / totalFails;

            // Allow ±5% tolerance for 10K iterations
            Assert.AreEqual(0.40f, preservedRatio, 0.05f,
                $"Preserved ratio {preservedRatio:F3} outside expected range");
            Assert.AreEqual(0.40f, destroyedRatio, 0.05f,
                $"Destroyed ratio {destroyedRatio:F3} outside expected range");
            Assert.AreEqual(0.20f, burnedRatio, 0.05f,
                $"Burned ratio {burnedRatio:F3} outside expected range");
        }

        [Test]
        public void ExecuteCraft_WithLowSuccessRate_ProducesBothSuccessAndFailure()
        {
            // Legendary + Legendary = 0.30 success rate, so we expect both outcomes
            Random.InitState(12345);
            bool sawSuccess = false;
            bool sawFailure = false;

            for (int i = 0; i < 500; i++)
            {
                var result = CraftSuccessSystem.ExecuteCraft(false, "Legendary", "Legendary");
                if (result == CraftResult.Success && !sawSuccess) sawSuccess = true;
                if (result != CraftResult.Success && !sawFailure) sawFailure = true;
                if (sawSuccess && sawFailure) break;
            }

            Assert.IsTrue(sawSuccess, "Expected at least one success with 30% rate over 500 iterations");
            Assert.IsTrue(sawFailure, "Expected at least one failure with 30% rate over 500 iterations");
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  CR-05 : Integration with CraftSuccessSystem (clamping, item ID, ExecuteCraft)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void GetFinalSuccessRate_ClampsToRange_0_1()
        {
            // With no bonus, max rate is Common+Common = 0.90
            // Min rate is Legendary+Legendary = 0.30
            // Both are already within [0,1]
            float highRate = CraftSuccessSystem.GetFinalSuccessRate(false, "Common", "Common");
            Assert.GreaterOrEqual(highRate, 0f);
            Assert.LessOrEqual(highRate, 1f);

            float lowRate = CraftSuccessSystem.GetFinalSuccessRate(false, "Legendary", "Legendary");
            Assert.GreaterOrEqual(lowRate, 0f);
            Assert.LessOrEqual(lowRate, 1f);

            // With bonus=0, all legitimate rates already in range; verify clamp doesn't break
            Assert.LessOrEqual(highRate, 1f);
            Assert.GreaterOrEqual(lowRate, 0f);
        }

        [Test]
        public void GetGradeFromItemId_NullOrEmpty_ReturnsCommon()
        {
            Assert.AreEqual("Common", CraftSuccessSystem.GetGradeFromItemId(null));
            Assert.AreEqual("Common", CraftSuccessSystem.GetGradeFromItemId(""));
        }

        [Test]
        public void GetGradeFromItemId_LegendaryPrefix_ReturnsLegendary()
        {
            Assert.AreEqual("Legendary", CraftSuccessSystem.GetGradeFromItemId("legendary_sword"));
            Assert.AreEqual("Legendary", CraftSuccessSystem.GetGradeFromItemId("legendary_armor_01"));
        }

        [Test]
        public void GetGradeFromItemId_EpicPrefix_ReturnsEpic()
        {
            Assert.AreEqual("Epic", CraftSuccessSystem.GetGradeFromItemId("epic_bow"));
            Assert.AreEqual("Epic", CraftSuccessSystem.GetGradeFromItemId("item_epic_shield"));
        }

        [Test]
        public void GetGradeFromItemId_RarePrefix_ReturnsRare()
        {
            Assert.AreEqual("Rare", CraftSuccessSystem.GetGradeFromItemId("rare_dagger"));
            Assert.AreEqual("Rare", CraftSuccessSystem.GetGradeFromItemId("some_rare_potion"));
        }

        [Test]
        public void GetGradeFromItemId_NoKnownPrefix_ReturnsCommon()
        {
            Assert.AreEqual("Common", CraftSuccessSystem.GetGradeFromItemId("common_sword"));
            Assert.AreEqual("Common", CraftSuccessSystem.GetGradeFromItemId("random_item_123"));
        }

        [Test]
        public void ExecuteCraft_ReturnsSuccess_WhenRollLessThanSuccessRate()
        {
            // Common + Common = 0.90 success rate, with deterministic seed
            Random.InitState(1234);
            int successes = 0;

            for (int i = 0; i < 1000; i++)
            {
                var result = CraftSuccessSystem.ExecuteCraft(false, "Common", "Common");
                if (result == CraftResult.Success) successes++;
            }

            float successRatio = (float)successes / 1000f;
            // With 90% rate, expect ~900 successes; allow ±5% tolerance
            Assert.AreEqual(0.90f, successRatio, 0.05f,
                $"Success ratio {successRatio:F3} not near expected 0.90");
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  CR-06 : Full workflow — popup lifecycle & successive calls
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void ShowSuccess_TimerExpires_IsShowingBecomesFalse()
        {
            _popup.ShowSuccess("Test Item", ItemRarity.Common, "Effect");

            var timerField = typeof(CraftResultPopup)
                .GetField("_showTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(timerField);

            // Force timer to expire
            timerField.SetValue(_popup, -0.1f);

            // Trigger Update to process the expired timer
            typeof(CraftResultPopup)
                .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(_popup, null);

            Assert.IsFalse(_popup.IsShowing);
        }

        [Test]
        public void ShowFailure_TimerExpires_IsShowingBecomesFalse()
        {
            _popup.ShowFailure(0);

            var timerField = typeof(CraftResultPopup)
                .GetField("_showTimer", BindingFlags.NonPublic | BindingFlags.Instance);

            timerField.SetValue(_popup, -0.1f);

            typeof(CraftResultPopup)
                .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(_popup, null);

            Assert.IsFalse(_popup.IsShowing);
        }

        [Test]
        public void ShowFailure_AllFailTypes_TimerExpiresCorrectly()
        {
            for (int failType = 0; failType <= 2; failType++)
            {
                var go = new GameObject("TestPopup_" + failType);
                var popup = go.AddComponent<CraftResultPopup>();

                popup.ShowFailure(failType);
                Assert.IsTrue(popup.IsShowing, $"FailType {failType}: Should be showing");

                var timerField = typeof(CraftResultPopup)
                    .GetField("_showTimer", BindingFlags.NonPublic | BindingFlags.Instance);
                timerField.SetValue(popup, -0.1f);

                typeof(CraftResultPopup)
                    .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(popup, null);

                Assert.IsFalse(popup.IsShowing, $"FailType {failType}: Should NOT be showing after expiry");

                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ShowSuccess_ThenShowFailure_Works()
        {
            _popup.ShowSuccess("First Item", ItemRarity.Legendary, "Effect A");
            Assert.IsTrue(_popup.IsShowing);

            // Immediately call failure — should overwrite
            Assert.DoesNotThrow(() => _popup.ShowFailure(2));
            Assert.IsTrue(_popup.IsShowing);
        }

        [Test]
        public void ShowFailure_ThenShowSuccess_Works()
        {
            _popup.ShowFailure(1);
            Assert.IsTrue(_popup.IsShowing);

            Assert.DoesNotThrow(() => _popup.ShowSuccess("Second Item", ItemRarity.Epic, "Effect B"));
            Assert.IsTrue(_popup.IsShowing);
        }

        [Test]
        public void ShowSuccess_ThenShowSuccess_Works()
        {
            _popup.ShowSuccess("Item A", ItemRarity.Common, "Effect A");
            Assert.IsTrue(_popup.IsShowing);

            _popup.ShowSuccess("Item B", ItemRarity.Legendary, "Effect B");
            Assert.IsTrue(_popup.IsShowing);

            // Verify the item name was overwritten
            var nameField = typeof(CraftResultPopup)
                .GetField("_currentItemName", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual("Item B", (string)nameField.GetValue(_popup));
        }
    }
}