using NUnit.Framework;
using ProjectName.Core;
using System.Collections.Generic;
using System.Linq;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// RarityProbabilityTable 테스트: 레벨 그룹 분류, 확률 테이블 검증, 랜덤 롤.
    /// </summary>
    public class RarityProbabilityTableTests
    {
        // ===================== GetLevelGroup =====================

        [TestCase(1, RarityProbabilityTable.LevelGroup.Lv1_10)]
        [TestCase(5, RarityProbabilityTable.LevelGroup.Lv1_10)]
        [TestCase(10, RarityProbabilityTable.LevelGroup.Lv1_10)]
        [TestCase(11, RarityProbabilityTable.LevelGroup.Lv11_20)]
        [TestCase(15, RarityProbabilityTable.LevelGroup.Lv11_20)]
        [TestCase(20, RarityProbabilityTable.LevelGroup.Lv11_20)]
        [TestCase(21, RarityProbabilityTable.LevelGroup.Lv21_30)]
        [TestCase(25, RarityProbabilityTable.LevelGroup.Lv21_30)]
        [TestCase(30, RarityProbabilityTable.LevelGroup.Lv21_30)]
        [TestCase(31, RarityProbabilityTable.LevelGroup.Lv31_40)]
        [TestCase(35, RarityProbabilityTable.LevelGroup.Lv31_40)]
        [TestCase(40, RarityProbabilityTable.LevelGroup.Lv31_40)]
        [TestCase(41, RarityProbabilityTable.LevelGroup.Lv41_50)]
        [TestCase(45, RarityProbabilityTable.LevelGroup.Lv41_50)]
        [TestCase(50, RarityProbabilityTable.LevelGroup.Lv41_50)]
        public void GetLevelGroup_ReturnsCorrectGroup(int level, RarityProbabilityTable.LevelGroup expected)
        {
            Assert.AreEqual(expected, RarityProbabilityTable.GetLevelGroup(level));
        }

        [Test]
        public void GetLevelGroup_Below1_ClampsToLv1_10()
        {
            Assert.AreEqual(RarityProbabilityTable.LevelGroup.Lv1_10,
                RarityProbabilityTable.GetLevelGroup(0));
            Assert.AreEqual(RarityProbabilityTable.LevelGroup.Lv1_10,
                RarityProbabilityTable.GetLevelGroup(-5));
        }

        [Test]
        public void GetLevelGroup_Above50_ClampsToLv41_50()
        {
            Assert.AreEqual(RarityProbabilityTable.LevelGroup.Lv41_50,
                RarityProbabilityTable.GetLevelGroup(51));
            Assert.AreEqual(RarityProbabilityTable.LevelGroup.Lv41_50,
                RarityProbabilityTable.GetLevelGroup(100));
        }

        // ===================== GetProbabilities — Lv1-10 =====================

        [Test]
        public void GetProbabilities_Lv1_10_SumToOne()
        {
            var probs = RarityProbabilityTable.GetProbabilities(5);
            float sum = probs.Values.Sum();
            Assert.AreEqual(1.0f, sum, 0.001f);
        }

        [Test]
        public void GetProbabilities_Lv1_10_CommonDominant()
        {
            var probs = RarityProbabilityTable.GetProbabilities(5);
            Assert.AreEqual(0.70f, probs[ItemRarity.Common], 0.001f);
            Assert.AreEqual(0.20f, probs[ItemRarity.Uncommon], 0.001f);
        }

        [Test]
        public void GetProbabilities_Lv1_10_NoLegendaryOrUnique()
        {
            var probs = RarityProbabilityTable.GetProbabilities(5);
            Assert.AreEqual(0f, probs[ItemRarity.Legendary]);
            Assert.AreEqual(0f, probs[ItemRarity.Unique]);
        }

        // ===================== GetProbabilities — Lv11-20 =====================

        [Test]
        public void GetProbabilities_Lv11_20_SumToOne()
        {
            var probs = RarityProbabilityTable.GetProbabilities(15);
            float sum = probs.Values.Sum();
            Assert.AreEqual(1.0f, sum, 0.001f);
        }

        [Test]
        public void GetProbabilities_Lv11_20_UncommonDominant()
        {
            var probs = RarityProbabilityTable.GetProbabilities(15);
            Assert.AreEqual(0.30f, probs[ItemRarity.Common], 0.001f);
            Assert.AreEqual(0.40f, probs[ItemRarity.Uncommon], 0.001f);
        }

        [Test]
        public void GetProbabilities_Lv11_20_LegendaryAvailable()
        {
            var probs = RarityProbabilityTable.GetProbabilities(15);
            Assert.AreEqual(0.02f, probs[ItemRarity.Legendary], 0.001f);
        }

        // ===================== GetProbabilities — Lv21-30 =====================

        [Test]
        public void GetProbabilities_Lv21_30_SumToOne()
        {
            var probs = RarityProbabilityTable.GetProbabilities(25);
            float sum = probs.Values.Sum();
            Assert.AreEqual(1.0f, sum, 0.001f);
        }

        [Test]
        public void GetProbabilities_Lv21_30_RareDominant()
        {
            var probs = RarityProbabilityTable.GetProbabilities(25);
            Assert.AreEqual(0.40f, probs[ItemRarity.Rare], 0.001f);
        }

        [Test]
        public void GetProbabilities_Lv21_30_Legendary10Percent()
        {
            var probs = RarityProbabilityTable.GetProbabilities(25);
            Assert.AreEqual(0.10f, probs[ItemRarity.Legendary], 0.001f);
        }

        // ===================== GetProbabilities — Lv31-40 =====================

        [Test]
        public void GetProbabilities_Lv31_40_SumToOne()
        {
            var probs = RarityProbabilityTable.GetProbabilities(35);
            float sum = probs.Values.Sum();
            Assert.AreEqual(1.0f, sum, 0.001f);
        }

        [Test]
        public void GetProbabilities_Lv31_40_NoCommon()
        {
            var probs = RarityProbabilityTable.GetProbabilities(35);
            Assert.AreEqual(0f, probs[ItemRarity.Common]);
        }

        [Test]
        public void GetProbabilities_Lv31_40_EpicDominant()
        {
            var probs = RarityProbabilityTable.GetProbabilities(35);
            Assert.AreEqual(0.40f, probs[ItemRarity.Epic], 0.001f);
        }

        // ===================== GetProbabilities — Lv41-50 =====================

        [Test]
        public void GetProbabilities_Lv41_50_SumToOne()
        {
            var probs = RarityProbabilityTable.GetProbabilities(45);
            float sum = probs.Values.Sum();
            Assert.AreEqual(1.0f, sum, 0.001f);
        }

        [Test]
        public void GetProbabilities_Lv41_50_NoCommonOrUncommon()
        {
            var probs = RarityProbabilityTable.GetProbabilities(45);
            Assert.AreEqual(0f, probs[ItemRarity.Common]);
            Assert.AreEqual(0f, probs[ItemRarity.Uncommon]);
        }

        [Test]
        public void GetProbabilities_Lv41_50_EpicDominant()
        {
            var probs = RarityProbabilityTable.GetProbabilities(45);
            Assert.AreEqual(0.45f, probs[ItemRarity.Epic], 0.001f);
            Assert.AreEqual(0.40f, probs[ItemRarity.Legendary], 0.001f);
        }

        // ===================== Probability Sums for All Groups =====================

        [Test]
        public void AllLevelGroups_ProbabilitiesSumToOne()
        {
            var groups = new[] {
                RarityProbabilityTable.LevelGroup.Lv1_10,
                RarityProbabilityTable.LevelGroup.Lv11_20,
                RarityProbabilityTable.LevelGroup.Lv21_30,
                RarityProbabilityTable.LevelGroup.Lv31_40,
                RarityProbabilityTable.LevelGroup.Lv41_50
            };

            foreach (var group in groups)
            {
                var probs = RarityProbabilityTable.GetProbabilitiesForGroup(group);
                float sum = probs.Values.Sum();
                Assert.AreEqual(1.0f, sum, 0.001f,
                    $"Group {group} probabilities sum to {sum}, expected 1.0");
            }
        }

        // ===================== Roll — returns valid rarity =====================

        [Test]
        public void Roll_ReturnsValidRarity()
        {
            var validRarities = new HashSet<ItemRarity>
            {
                ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare,
                ItemRarity.Epic, ItemRarity.Legendary, ItemRarity.Unique
            };

            // Many rolls across all level groups — should never return invalid rarity
            int[] testLevels = { 1, 5, 10, 11, 15, 20, 21, 25, 30, 31, 35, 40, 41, 45, 50 };
            for (int i = 0; i < 100; i++)
            {
                foreach (int level in testLevels)
                {
                    ItemRarity result = RarityProbabilityTable.Roll(level);
                    Assert.IsTrue(validRarities.Contains(result),
                        $"Roll(level={level}) returned invalid rarity: {result}");
                }
            }
        }

        [Test]
        public void Roll_Lv1_10_NeverReturnsLegendaryOrUnique()
        {
            // Statistical test: roll 1000 times, should never get Legendary or Unique
            for (int i = 0; i < 1000; i++)
            {
                ItemRarity result = RarityProbabilityTable.Roll(5);
                Assert.AreNotEqual(ItemRarity.Legendary, result,
                    $"Roll(level=5) unexpectedly returned Legendary");
                Assert.AreNotEqual(ItemRarity.Unique, result,
                    $"Roll(level=5) unexpectedly returned Unique");
            }
        }

        [Test]
        public void Roll_Lv41_50_NeverReturnsCommonOrUncommon()
        {
            for (int i = 0; i < 1000; i++)
            {
                ItemRarity result = RarityProbabilityTable.Roll(45);
                Assert.AreNotEqual(ItemRarity.Common, result,
                    $"Roll(level=45) unexpectedly returned Common");
                Assert.AreNotEqual(ItemRarity.Uncommon, result,
                    $"Roll(level=45) unexpectedly returned Uncommon");
            }
        }

        [Test]
        public void Roll_ReturnsCorrectDistributionForLv1_10()
        {
            // Statistical test: roll many times and check approximate distribution
            var counts = new Dictionary<ItemRarity, int>
            {
                { ItemRarity.Common, 0 },
                { ItemRarity.Uncommon, 0 },
                { ItemRarity.Rare, 0 },
                { ItemRarity.Epic, 0 },
            };

            int iterations = 5000;
            for (int i = 0; i < iterations; i++)
            {
                var result = RarityProbabilityTable.Roll(5);
                counts[result]++;
            }

            // Check rough proportions (within reasonable margin)
            float commonRatio = (float)counts[ItemRarity.Common] / iterations;
            float uncommonRatio = (float)counts[ItemRarity.Uncommon] / iterations;

            Assert.Greater(commonRatio, 0.55f, $"Common ratio {commonRatio:F3} too low");
            Assert.Less(commonRatio, 0.85f, $"Common ratio {commonRatio:F3} too high");
            Assert.Greater(uncommonRatio, 0.10f, $"Uncommon ratio {uncommonRatio:F3} too low");
            Assert.Less(uncommonRatio, 0.30f, $"Uncommon ratio {uncommonRatio:F3} too high");
        }

        // ===================== Defensive copy =====================

        [Test]
        public void GetProbabilities_ReturnsCopy_ModificationDoesNotAffectSource()
        {
            var probs1 = RarityProbabilityTable.GetProbabilities(5);
            var probs2 = RarityProbabilityTable.GetProbabilities(5);

            probs1[ItemRarity.Common] = 999f;

            Assert.AreEqual(0.70f, probs2[ItemRarity.Common], 0.001f,
                "Modifying returned dictionary should not affect subsequent calls");
        }

        // ===================== Unknown levels =====================

        [Test]
        public void GetProbabilities_Level0_SameAsLv1_10()
        {
            var probs = RarityProbabilityTable.GetProbabilities(0);
            var expected = RarityProbabilityTable.GetProbabilitiesForGroup(
                RarityProbabilityTable.LevelGroup.Lv1_10);

            foreach (var kvp in expected)
            {
                Assert.AreEqual(kvp.Value, probs[kvp.Key], 0.001f);
            }
        }

        [Test]
        public void GetProbabilities_Level100_SameAsLv41_50()
        {
            var probs = RarityProbabilityTable.GetProbabilities(100);
            var expected = RarityProbabilityTable.GetProbabilitiesForGroup(
                RarityProbabilityTable.LevelGroup.Lv41_50);

            foreach (var kvp in expected)
            {
                Assert.AreEqual(kvp.Value, probs[kvp.Key], 0.001f);
            }
        }

        // ===================== All groups have Unique=0 =====================

        [Test]
        public void AllLevelGroups_UniqueProbabilityIsZero()
        {
            for (int level = 1; level <= 50; level++)
            {
                var probs = RarityProbabilityTable.GetProbabilities(level);
                Assert.AreEqual(0f, probs[ItemRarity.Unique],
                    $"Level {level}: Unique probability should be 0");
            }
        }
    }
}