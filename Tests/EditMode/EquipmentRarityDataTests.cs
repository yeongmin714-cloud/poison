using NUnit.Framework;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EquipmentRarityData 테스트: 등급별 스탯 배율, 분산, 표시 이름, 색상 검증.
    /// </summary>
    public class EquipmentRarityDataTests
    {
        // ===================== GetStatMultiplier =====================

        [Test]
        public void GetStatMultiplier_Common_Returns1f()
        {
            Assert.AreEqual(1.0f, EquipmentRarityData.GetStatMultiplier(ItemRarity.Common), 0.0001f);
        }

        [Test]
        public void GetStatMultiplier_Uncommon_Returns1_3f()
        {
            Assert.AreEqual(1.3f, EquipmentRarityData.GetStatMultiplier(ItemRarity.Uncommon), 0.0001f);
        }

        [Test]
        public void GetStatMultiplier_Rare_Returns1_7f()
        {
            Assert.AreEqual(1.7f, EquipmentRarityData.GetStatMultiplier(ItemRarity.Rare), 0.0001f);
        }

        [Test]
        public void GetStatMultiplier_Epic_Returns2_2f()
        {
            Assert.AreEqual(2.2f, EquipmentRarityData.GetStatMultiplier(ItemRarity.Epic), 0.0001f);
        }

        [Test]
        public void GetStatMultiplier_Legendary_Returns3_0f()
        {
            Assert.AreEqual(3.0f, EquipmentRarityData.GetStatMultiplier(ItemRarity.Legendary), 0.0001f);
        }

        [Test]
        public void GetStatMultiplier_Unique_Returns4_0f()
        {
            Assert.AreEqual(4.0f, EquipmentRarityData.GetStatMultiplier(ItemRarity.Unique), 0.0001f);
        }

        [Test]
        public void GetStatMultiplier_Order_IncreasesWithRarity()
        {
            float common = EquipmentRarityData.GetStatMultiplier(ItemRarity.Common);
            float uncommon = EquipmentRarityData.GetStatMultiplier(ItemRarity.Uncommon);
            float rare = EquipmentRarityData.GetStatMultiplier(ItemRarity.Rare);
            float epic = EquipmentRarityData.GetStatMultiplier(ItemRarity.Epic);
            float legendary = EquipmentRarityData.GetStatMultiplier(ItemRarity.Legendary);
            float unique = EquipmentRarityData.GetStatMultiplier(ItemRarity.Unique);

            Assert.Less(common, uncommon);
            Assert.Less(uncommon, rare);
            Assert.Less(rare, epic);
            Assert.Less(epic, legendary);
            Assert.Less(legendary, unique);
        }

        // ===================== GetStatVariance =====================

        [Test]
        public void GetStatVariance_Common_Returns10Percent()
        {
            Assert.AreEqual(0.10f, EquipmentRarityData.GetStatVariance(ItemRarity.Common), 0.0001f);
        }

        [Test]
        public void GetStatVariance_Uncommon_Returns10Percent()
        {
            Assert.AreEqual(0.10f, EquipmentRarityData.GetStatVariance(ItemRarity.Uncommon), 0.0001f);
        }

        [Test]
        public void GetStatVariance_Rare_Returns15Percent()
        {
            Assert.AreEqual(0.15f, EquipmentRarityData.GetStatVariance(ItemRarity.Rare), 0.0001f);
        }

        [Test]
        public void GetStatVariance_Epic_Returns15Percent()
        {
            Assert.AreEqual(0.15f, EquipmentRarityData.GetStatVariance(ItemRarity.Epic), 0.0001f);
        }

        [Test]
        public void GetStatVariance_Legendary_Returns5Percent()
        {
            Assert.AreEqual(0.05f, EquipmentRarityData.GetStatVariance(ItemRarity.Legendary), 0.0001f);
        }

        [Test]
        public void GetStatVariance_Unique_Returns5Percent()
        {
            Assert.AreEqual(0.05f, EquipmentRarityData.GetStatVariance(ItemRarity.Unique), 0.0001f);
        }

        [Test]
        public void GetStatVariance_LegendaryUnique_HasTighterVariance()
        {
            float lowVariance = EquipmentRarityData.GetStatVariance(ItemRarity.Legendary);
            float highVariance = EquipmentRarityData.GetStatVariance(ItemRarity.Rare);
            Assert.Less(lowVariance, highVariance, "Legendary/Unique variance should be tighter than Rare/Epic");
        }

        // ===================== GetRarityDisplayName =====================

        [Test]
        public void GetRarityDisplayName_Common_ReturnsKorean()
        {
            Assert.AreEqual("일반", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Common));
        }

        [Test]
        public void GetRarityDisplayName_Uncommon_ReturnsKorean()
        {
            Assert.AreEqual("고급", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Uncommon));
        }

        [Test]
        public void GetRarityDisplayName_Rare_ReturnsKorean()
        {
            Assert.AreEqual("희귀", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Rare));
        }

        [Test]
        public void GetRarityDisplayName_Epic_ReturnsKorean()
        {
            Assert.AreEqual("영웅", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Epic));
        }

        [Test]
        public void GetRarityDisplayName_Legendary_ReturnsKorean()
        {
            Assert.AreEqual("전설", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Legendary));
        }

        [Test]
        public void GetRarityDisplayName_Unique_ReturnsKorean()
        {
            Assert.AreEqual("유니크", EquipmentRarityData.GetRarityDisplayName(ItemRarity.Unique));
        }

        [Test]
        public void GetRarityDisplayName_AllUnique_NoDuplicates()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
            {
                string name = EquipmentRarityData.GetRarityDisplayName(rarity);
                Assert.IsTrue(names.Add(name), $"Rarity {rarity} has duplicate display name '{name}'");
            }
        }

        // ===================== GetRarityColor =====================

        [Test]
        public void GetRarityColor_Common_IsGray()
        {
            Color c = EquipmentRarityData.GetRarityColor(ItemRarity.Common);
            Assert.AreEqual(0.6f, c.r, 0.01f);
            Assert.AreEqual(0.6f, c.g, 0.01f);
            Assert.AreEqual(0.6f, c.b, 0.01f);
            Assert.AreEqual(1.0f, c.a, 0.01f);
        }

        [Test]
        public void GetRarityColor_Uncommon_IsGreen()
        {
            Color c = EquipmentRarityData.GetRarityColor(ItemRarity.Uncommon);
            Assert.AreEqual(0.2f, c.r, 0.01f);
            Assert.AreEqual(0.8f, c.g, 0.01f);
            Assert.AreEqual(0.2f, c.b, 0.01f);
        }

        [Test]
        public void GetRarityColor_Rare_IsBlue()
        {
            Color c = EquipmentRarityData.GetRarityColor(ItemRarity.Rare);
            Assert.AreEqual(0.2f, c.r, 0.01f);
            Assert.AreEqual(0.4f, c.g, 0.01f);
            Assert.AreEqual(1.0f, c.b, 0.01f);
        }

        [Test]
        public void GetRarityColor_Epic_IsPurple()
        {
            Color c = EquipmentRarityData.GetRarityColor(ItemRarity.Epic);
            Assert.AreEqual(0.6f, c.r, 0.01f);
            Assert.AreEqual(0.2f, c.g, 0.01f);
            Assert.AreEqual(1.0f, c.b, 0.01f);
        }

        [Test]
        public void GetRarityColor_Legendary_IsRed()
        {
            Color c = EquipmentRarityData.GetRarityColor(ItemRarity.Legendary);
            Assert.AreEqual(1.0f, c.r, 0.01f);
            Assert.AreEqual(0.2f, c.g, 0.01f);
            Assert.AreEqual(0.2f, c.b, 0.01f);
        }

        [Test]
        public void GetRarityColor_Unique_IsGold()
        {
            Color c = EquipmentRarityData.GetRarityColor(ItemRarity.Unique);
            Assert.AreEqual(1.0f, c.r, 0.01f);
            Assert.AreEqual(0.85f, c.g, 0.01f);
            Assert.AreEqual(0.0f, c.b, 0.01f);
        }

        [Test]
        public void GetRarityColor_AllHaveAlphaOne()
        {
            foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
            {
                Color c = EquipmentRarityData.GetRarityColor(rarity);
                Assert.AreEqual(1.0f, c.a, 0.0001f, $"Rarity {rarity} alpha is not 1.0");
            }
        }

        // ===================== Edge cases =====================

        [Test]
        public void GetStatMultiplier_AllRarities_ReturnsPositiveValue()
        {
            foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
            {
                float multiplier = EquipmentRarityData.GetStatMultiplier(rarity);
                Assert.Greater(multiplier, 0f, $"Stat multiplier for {rarity} should be positive");
            }
        }

        [Test]
        public void GetStatVariance_AllRarities_ReturnsPositiveValue()
        {
            foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
            {
                float variance = EquipmentRarityData.GetStatVariance(rarity);
                Assert.Greater(variance, 0f, $"Stat variance for {rarity} should be positive");
            }
        }
    }
}