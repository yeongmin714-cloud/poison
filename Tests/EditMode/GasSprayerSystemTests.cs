using System;
using NUnit.Framework;
using ProjectName.Systems;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C8-31: 가스 분사기 데이터 시스템 테스트
    /// </summary>
    public class GasSprayerSystemTests
    {
        // ===================== GetGradeData — All Grades =====================

        [Test]
        public void GetGradeData_AllGrades_ReturnsValidData()
        {
            var grades = GasSprayerManager.GetAllGrades();
            Assert.AreEqual(5, grades.Length, "Should have 5 grades");

            foreach (var grade in grades)
            {
                var data = GasSprayerManager.GetGradeData(grade);
                Assert.IsNotNull(data.sprayerName, $"Grade {grade} should have a name");
                Assert.IsFalse(string.IsNullOrEmpty(data.sprayerName), $"Grade {grade} name should not be empty");
                Assert.IsNotNull(data.requiredMaterials, $"Grade {grade} should have materials");
                Assert.IsNotNull(data.requiredMaterialCounts, $"Grade {grade} should have material counts");
                Assert.AreEqual(data.requiredMaterials.Length, data.requiredMaterialCounts.Length,
                    $"Grade {grade} materials and counts arrays should match");
            }
        }

        // ===================== Per-Grade Specifications =====================

        [Test]
        public void GetGradeData_Wood_HasCorrectSpecs()
        {
            var data = GasSprayerManager.GetGradeData(GasSprayerGrade.Wood);
            Assert.AreEqual("나무 가스 분사기", data.sprayerName);
            Assert.AreEqual(10f, data.maxSprayTime);
            Assert.AreEqual(3f, data.sprayRange);
            Assert.AreEqual(1.0f, data.sprayTimeMultiplier);
            Assert.AreEqual("Back", data.equippedSlotName);
            Assert.IsFalse(data.isUnlimited);
        }

        [Test]
        public void GetGradeData_Stone_HasCorrectSpecs()
        {
            var data = GasSprayerManager.GetGradeData(GasSprayerGrade.Stone);
            Assert.AreEqual("돌 가스 분사기", data.sprayerName);
            Assert.AreEqual(25f, data.maxSprayTime);
            Assert.AreEqual(4f, data.sprayRange);
            Assert.AreEqual(0.8f, data.sprayTimeMultiplier);
            Assert.AreEqual("Back", data.equippedSlotName);
            Assert.IsFalse(data.isUnlimited);
        }

        [Test]
        public void GetGradeData_Iron_HasCorrectSpecs()
        {
            var data = GasSprayerManager.GetGradeData(GasSprayerGrade.Iron);
            Assert.AreEqual("철 가스 분사기", data.sprayerName);
            Assert.AreEqual(45f, data.maxSprayTime);
            Assert.AreEqual(5f, data.sprayRange);
            Assert.AreEqual(0.6f, data.sprayTimeMultiplier);
            Assert.AreEqual("Back", data.equippedSlotName);
            Assert.IsFalse(data.isUnlimited);
        }

        [Test]
        public void GetGradeData_Reinforced_HasCorrectSpecs()
        {
            var data = GasSprayerManager.GetGradeData(GasSprayerGrade.Reinforced);
            Assert.AreEqual("강화 가스 분사기", data.sprayerName);
            Assert.AreEqual(90f, data.maxSprayTime);
            Assert.AreEqual(7f, data.sprayRange);
            Assert.AreEqual(0.4f, data.sprayTimeMultiplier);
            Assert.AreEqual("Back", data.equippedSlotName);
            Assert.IsFalse(data.isUnlimited);
        }

        [Test]
        public void GetGradeData_SpecialAlloy_HasCorrectSpecs()
        {
            var data = GasSprayerManager.GetGradeData(GasSprayerGrade.SpecialAlloy);
            Assert.AreEqual("특수합금 분사기", data.sprayerName);
            Assert.AreEqual(0f, data.maxSprayTime);
            Assert.AreEqual(10f, data.sprayRange);
            Assert.AreEqual(0f, data.sprayTimeMultiplier);
            Assert.AreEqual("Back", data.equippedSlotName);
            Assert.IsTrue(data.isUnlimited);
        }

        [Test]
        public void GetGradeData_SpecialAlloy_IsUnlimited()
        {
            var data = GasSprayerManager.GetGradeData(GasSprayerGrade.SpecialAlloy);
            Assert.IsTrue(data.isUnlimited, "SpecialAlloy should be unlimited");
            Assert.AreEqual(0f, data.maxSprayTime, "SpecialAlloy maxSprayTime should be 0 (unlimited)");
        }

        // ===================== GetGradeBySprayerName =====================

        [Test]
        public void GetGradeBySprayerName_ValidNames_ReturnsCorrectGrade()
        {
            Assert.AreEqual(GasSprayerGrade.Wood, GasSprayerManager.GetGradeBySprayerName("나무 가스 분사기"));
            Assert.AreEqual(GasSprayerGrade.Stone, GasSprayerManager.GetGradeBySprayerName("돌 가스 분사기"));
            Assert.AreEqual(GasSprayerGrade.Iron, GasSprayerManager.GetGradeBySprayerName("철 가스 분사기"));
            Assert.AreEqual(GasSprayerGrade.Reinforced, GasSprayerManager.GetGradeBySprayerName("강화 가스 분사기"));
            Assert.AreEqual(GasSprayerGrade.SpecialAlloy, GasSprayerManager.GetGradeBySprayerName("특수합금 분사기"));
        }

        [Test]
        public void GetGradeBySprayerName_InvalidName_Throws()
        {
            Assert.Throws<ArgumentException>(() => GasSprayerManager.GetGradeBySprayerName("없는 분사기"));
            Assert.Throws<ArgumentException>(() => GasSprayerManager.GetGradeBySprayerName(""));
            Assert.Throws<ArgumentException>(() => GasSprayerManager.GetGradeBySprayerName(null));
        }

        // ===================== CanCraftSprayer =====================

        [Test]
        public void CanCraftSprayer_SufficientMaterials_ReturnsTrue()
        {
            // Wood sprayer: "나무"×3, "가죽"×2 — provide exactly enough
            Func<string, int> sufficient = id =>
            {
                switch (id)
                {
                    case "나무": return 3;
                    case "가죽": return 2;
                    default: return 0;
                }
            };
            Assert.IsTrue(GasSprayerManager.CanCraftSprayer(GasSprayerGrade.Wood, sufficient));
        }

        [Test]
        public void CanCraftSprayer_InsufficientMaterials_ReturnsFalse()
        {
            // Wood sprayer: "나무"×3, "가죽"×2 — provide less than needed
            Func<string, int> insufficient = id =>
            {
                switch (id)
                {
                    case "나무": return 2;  // need 3
                    case "가죽": return 2;
                    default: return 0;
                }
            };
            Assert.IsFalse(GasSprayerManager.CanCraftSprayer(GasSprayerGrade.Wood, insufficient));
        }

        [Test]
        public void CanCraftSprayer_NullInventory_ReturnsFalse()
        {
            PlayerInventory nullInventory = null;
            Assert.IsFalse(GasSprayerManager.CanCraftSprayer(GasSprayerGrade.Wood, nullInventory));
        }

        [Test]
        public void CanCraftSprayer_NullGetter_ReturnsFalse()
        {
            Assert.IsFalse(GasSprayerManager.CanCraftSprayer(GasSprayerGrade.Wood, (Func<string, int>)null));
        }

        // ===================== IsBackSlotAvailable =====================

        [Test]
        public void IsBackSlotAvailable_AlwaysTrue()
        {
            Assert.IsTrue(GasSprayerManager.IsBackSlotAvailable());
            // Call multiple times to ensure idempotent
            Assert.IsTrue(GasSprayerManager.IsBackSlotAvailable());
        }

        // ===================== CalculateSprayDuration =====================

        [Test]
        public void CalculateSprayDuration_Wood_With1Potion_ReturnsCorrect()
        {
            // Wood: maxSprayTime=10s, multiplier=1.0x
            // duration = 1 * 10 / 1.0 = 10
            float duration = GasSprayerManager.CalculateSprayDuration(GasSprayerGrade.Wood, 1);
            Assert.AreEqual(10f, duration, 0.001f);
        }

        [Test]
        public void CalculateSprayDuration_Iron_With3Potions_ReturnsCorrect()
        {
            // Iron: maxSprayTime=45s, multiplier=0.6x
            // duration = 3 * 45 / 0.6 = 225
            float duration = GasSprayerManager.CalculateSprayDuration(GasSprayerGrade.Iron, 3);
            Assert.AreEqual(225f, duration, 0.001f);
        }

        [Test]
        public void CalculateSprayDuration_Stone_With2Potions_ReturnsCorrect()
        {
            // Stone: maxSprayTime=25s, multiplier=0.8x
            // duration = 2 * 25 / 0.8 = 62.5
            float duration = GasSprayerManager.CalculateSprayDuration(GasSprayerGrade.Stone, 2);
            Assert.AreEqual(62.5f, duration, 0.001f);
        }

        [Test]
        public void CalculateSprayDuration_SpecialAlloy_ReturnsMaxValue()
        {
            // SpecialAlloy: unlimited
            float duration = GasSprayerManager.CalculateSprayDuration(GasSprayerGrade.SpecialAlloy, 100);
            Assert.AreEqual(float.MaxValue, duration);
        }

        [Test]
        public void CalculateSprayDuration_Reinforced_ReturnsCorrect()
        {
            // Reinforced: maxSprayTime=90s, multiplier=0.4x
            // duration = 5 * 90 / 0.4 = 1125
            float duration = GasSprayerManager.CalculateSprayDuration(GasSprayerGrade.Reinforced, 5);
            Assert.AreEqual(1125f, duration, 0.001f);
        }

        // ===================== GetReloadTime =====================

        [Test]
        public void GetReloadTime_AllGrades_ReturnsCorrect()
        {
            Assert.AreEqual(3.0f, GasSprayerManager.GetReloadTime(GasSprayerGrade.Wood));
            Assert.AreEqual(2.5f, GasSprayerManager.GetReloadTime(GasSprayerGrade.Stone));
            Assert.AreEqual(2.0f, GasSprayerManager.GetReloadTime(GasSprayerGrade.Iron));
            Assert.AreEqual(1.5f, GasSprayerManager.GetReloadTime(GasSprayerGrade.Reinforced));
            Assert.AreEqual(0f, GasSprayerManager.GetReloadTime(GasSprayerGrade.SpecialAlloy));
        }

        [Test]
        public void GetReloadTime_HigherGrade_FasterReload()
        {
            float wood = GasSprayerManager.GetReloadTime(GasSprayerGrade.Wood);
            float stone = GasSprayerManager.GetReloadTime(GasSprayerGrade.Stone);
            float iron = GasSprayerManager.GetReloadTime(GasSprayerGrade.Iron);
            float reinforced = GasSprayerManager.GetReloadTime(GasSprayerGrade.Reinforced);
            float special = GasSprayerManager.GetReloadTime(GasSprayerGrade.SpecialAlloy);

            Assert.Greater(wood, stone, "Wood reload should be slower than Stone");
            Assert.Greater(stone, iron, "Stone reload should be slower than Iron");
            Assert.Greater(iron, reinforced, "Iron reload should be slower than Reinforced");
            Assert.Greater(reinforced, special, "Reinforced reload should be slower than Special");
            Assert.AreEqual(0f, special, "SpecialAlloy should have instant reload");
        }

        // ===================== GetAllSprayerNames =====================

        [Test]
        public void GetAllSprayerNames_ReturnsAll5Names()
        {
            var names = GasSprayerManager.GetAllSprayerNames();
            Assert.AreEqual(5, names.Length);
            Assert.Contains("나무 가스 분사기", names);
            Assert.Contains("돌 가스 분사기", names);
            Assert.Contains("철 가스 분사기", names);
            Assert.Contains("강화 가스 분사기", names);
            Assert.Contains("특수합금 분사기", names);
        }

        // ===================== CanInsertPotion =====================

        [Test]
        public void CanInsertPotion_AnyPotion_ReturnsTrue()
        {
            Assert.IsTrue(GasSprayerManager.CanInsertPotion(GasSprayerGrade.Wood, "potion_heal"));
            Assert.IsTrue(GasSprayerManager.CanInsertPotion(GasSprayerGrade.Stone, "potion_poison"));
            Assert.IsTrue(GasSprayerManager.CanInsertPotion(GasSprayerGrade.Iron, "potion_speed"));
            Assert.IsTrue(GasSprayerManager.CanInsertPotion(GasSprayerGrade.Reinforced, ""));
            Assert.IsTrue(GasSprayerManager.CanInsertPotion(GasSprayerGrade.SpecialAlloy, null));
        }

        // ===================== Wood Material Specs =====================

        [Test]
        public void GetGradeData_Wood_Materials_Correct()
        {
            var data = GasSprayerManager.GetGradeData(GasSprayerGrade.Wood);
            Assert.AreEqual(new[] { "나무", "가죽" }, data.requiredMaterials);
            Assert.AreEqual(new[] { 3, 2 }, data.requiredMaterialCounts);
        }

        [Test]
        public void GetGradeData_SpecialAlloy_Materials_Correct()
        {
            var data = GasSprayerManager.GetGradeData(GasSprayerGrade.SpecialAlloy);
            Assert.AreEqual(new[] { "철", "용비늘" }, data.requiredMaterials);
            Assert.AreEqual(new[] { 20, 1 }, data.requiredMaterialCounts);
        }

        // ===================== BACK_SLOT_NAME Constant =====================

        [Test]
        public void BackSlotName_IsCorrect()
        {
            Assert.AreEqual("Back", GasSprayerManager.BACK_SLOT_NAME);
        }

        // ===================== Sprayer Data Integrity =====================

        [Test]
        public void AllSprayers_HaveBackSlot()
        {
            foreach (var grade in GasSprayerManager.GetAllGrades())
            {
                var data = GasSprayerManager.GetGradeData(grade);
                Assert.AreEqual("Back", data.equippedSlotName,
                    $"Grade {grade} should use Back slot");
            }
        }

        [Test]
        public void CalculateSprayDuration_ZeroPotions_ReturnsZero()
        {
            float duration = GasSprayerManager.CalculateSprayDuration(GasSprayerGrade.Wood, 0);
            Assert.AreEqual(0f, duration, 0.001f);
        }

        [Test]
        public void GetGradeData_UnknownGrade_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                GasSprayerManager.GetGradeData((GasSprayerGrade)99));
        }
    }
}