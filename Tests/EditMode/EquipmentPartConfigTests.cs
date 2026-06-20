using NUnit.Framework;
using ProjectName.Core;
using System.Collections.Generic;
using System.Linq;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EquipmentPartConfig 테스트: 부위 확률, 랜덤 슬롯 롤, 예상 슬롯 개수 검증.
    /// </summary>
    public class EquipmentPartConfigTests
    {
        // ===================== AllParts enumeration =====================

        [Test]
        public void AllParts_HasAllFiveParts()
        {
            Assert.AreEqual(5, EquipmentPartConfig.AllParts.Length);
            CollectionAssert.Contains(EquipmentPartConfig.AllParts,
                EquipmentPartConfig.EquipmentPart.Head);
            CollectionAssert.Contains(EquipmentPartConfig.AllParts,
                EquipmentPartConfig.EquipmentPart.Body);
            CollectionAssert.Contains(EquipmentPartConfig.AllParts,
                EquipmentPartConfig.EquipmentPart.Hands);
            CollectionAssert.Contains(EquipmentPartConfig.AllParts,
                EquipmentPartConfig.EquipmentPart.Feet);
            CollectionAssert.Contains(EquipmentPartConfig.AllParts,
                EquipmentPartConfig.EquipmentPart.Weapon);
        }

        [Test]
        public void EquipmentPart_EnumHasDistinctValues()
        {
            var values = System.Enum.GetValues(typeof(EquipmentPartConfig.EquipmentPart))
                .Cast<int>();
            Assert.AreEqual(5, values.Distinct().Count(),
                "All EquipmentPart enum values should be distinct");
        }

        // ===================== GetSlotProbability by level =====================

        [TestCase(1, 0.25f)]
        [TestCase(5, 0.25f)]
        [TestCase(10, 0.25f)]
        [TestCase(11, 0.45f)]
        [TestCase(15, 0.45f)]
        [TestCase(20, 0.45f)]
        [TestCase(21, 0.65f)]
        [TestCase(25, 0.65f)]
        [TestCase(30, 0.65f)]
        [TestCase(31, 0.80f)]
        [TestCase(35, 0.80f)]
        [TestCase(40, 0.80f)]
        [TestCase(41, 0.90f)]
        [TestCase(45, 0.90f)]
        [TestCase(50, 0.90f)]
        public void GetSlotProbability_ReturnsCorrectValue(int level, float expected)
        {
            Assert.AreEqual(expected,
                EquipmentPartConfig.GetSlotProbability(level), 0.001f);
        }

        [Test]
        public void GetSlotProbability_AllPartsSameForGivenLevel()
        {
            int[] testLevels = { 5, 15, 25, 35, 45 };
            foreach (int level in testLevels)
            {
                float baseProb = EquipmentPartConfig.GetSlotProbability(level);
                foreach (var part in EquipmentPartConfig.AllParts)
                {
                    float partProb = EquipmentPartConfig.GetSlotProbability(level, part);
                    Assert.AreEqual(baseProb, partProb, 0.0001f,
                        $"Level {level}, part {part}: probability should match base");
                }
            }
        }

        [Test]
        public void GetSlotProbability_IncreasesWithLevel()
        {
            int[] levels = { 5, 15, 25, 35, 45 };
            float prev = EquipmentPartConfig.GetSlotProbability(levels[0]);
            for (int i = 1; i < levels.Length; i++)
            {
                float current = EquipmentPartConfig.GetSlotProbability(levels[i]);
                Assert.Less(prev, current,
                    $"Level {levels[i]} probability ({current}) should be higher than level {levels[i-1]} ({prev})");
                prev = current;
            }
        }

        [Test]
        public void GetSlotProbability_BelowLevel1_UsesLv1_10Probability()
        {
            Assert.AreEqual(0.25f, EquipmentPartConfig.GetSlotProbability(0), 0.001f);
            Assert.AreEqual(0.25f, EquipmentPartConfig.GetSlotProbability(-10), 0.001f);
        }

        [Test]
        public void GetSlotProbability_AboveLevel50_UsesLv41_50Probability()
        {
            Assert.AreEqual(0.90f, EquipmentPartConfig.GetSlotProbability(51), 0.001f);
            Assert.AreEqual(0.90f, EquipmentPartConfig.GetSlotProbability(100), 0.001f);
        }

        // ===================== GetExpectedSlotCount =====================

        [TestCase(1, 1)]
        [TestCase(5, 1)]
        [TestCase(10, 1)]
        [TestCase(11, 2)]
        [TestCase(15, 2)]
        [TestCase(20, 2)]
        [TestCase(21, 3)]
        [TestCase(25, 3)]
        [TestCase(30, 3)]
        [TestCase(31, 4)]
        [TestCase(35, 4)]
        [TestCase(40, 4)]
        [TestCase(41, 4)]
        [TestCase(45, 4)]
        [TestCase(50, 4)]
        public void GetExpectedSlotCount_ReturnsCorrectMin(int level, int expected)
        {
            Assert.AreEqual(expected,
                EquipmentPartConfig.GetExpectedSlotCount(level));
        }

        [TestCase(1, 2)]
        [TestCase(5, 2)]
        [TestCase(10, 2)]
        [TestCase(11, 3)]
        [TestCase(15, 3)]
        [TestCase(20, 3)]
        [TestCase(21, 4)]
        [TestCase(25, 4)]
        [TestCase(30, 4)]
        [TestCase(31, 5)]
        [TestCase(35, 5)]
        [TestCase(40, 5)]
        [TestCase(41, 5)]
        [TestCase(45, 5)]
        [TestCase(50, 5)]
        public void GetExpectedSlotCountMax_ReturnsCorrectMax(int level, int expected)
        {
            Assert.AreEqual(expected,
                EquipmentPartConfig.GetExpectedSlotCountMax(level));
        }

        [Test]
        public void GetExpectedSlotCount_IncreasesWithLevel()
        {
            int[] levels = { 5, 15, 25, 35, 45 };
            int prev = EquipmentPartConfig.GetExpectedSlotCount(levels[0]);
            for (int i = 1; i < levels.Length; i++)
            {
                int current = EquipmentPartConfig.GetExpectedSlotCount(levels[i]);
                Assert.LessOrEqual(prev, current,
                    $"Level {levels[i]} expected min ({current}) should be >= level {levels[i-1]} ({prev})");
                prev = current;
            }
        }

        // ===================== RollSlots =====================

        [Test]
        public void RollSlots_AlwaysReturnsAtLeastOnePart()
        {
            for (int level = 1; level <= 50; level++)
            {
                for (int i = 0; i < 10; i++)
                {
                    var slots = EquipmentPartConfig.RollSlots(level);
                    Assert.IsNotNull(slots);
                    Assert.GreaterOrEqual(slots.Count, 1,
                        $"Level {level}: RollSlots should return at least 1 slot");
                }
            }
        }

        [Test]
        public void RollSlots_ReturnsAtMostFiveParts()
        {
            for (int level = 1; level <= 50; level++)
            {
                for (int i = 0; i < 10; i++)
                {
                    var slots = EquipmentPartConfig.RollSlots(level);
                    Assert.LessOrEqual(slots.Count, 5,
                        $"Level {level}: RollSlots should return at most 5 slots");
                }
            }
        }

        [Test]
        public void RollSlots_NoDuplicates()
        {
            for (int level = 1; level <= 50; level++)
            {
                for (int i = 0; i < 10; i++)
                {
                    var slots = EquipmentPartConfig.RollSlots(level);
                    Assert.AreEqual(slots.Count, slots.Distinct().Count(),
                        $"Level {level}: RollSlots should not contain duplicates");
                }
            }
        }

        [Test]
        public void RollSlots_AllPartsAreValidEnumValues()
        {
            var validParts = new HashSet<EquipmentPartConfig.EquipmentPart>(
                EquipmentPartConfig.AllParts);

            for (int level = 1; level <= 50; level++)
            {
                for (int i = 0; i < 10; i++)
                {
                    var slots = EquipmentPartConfig.RollSlots(level);
                    foreach (var part in slots)
                    {
                        Assert.IsTrue(validParts.Contains(part),
                            $"Level {level}: invalid part {part} in RollSlots result");
                    }
                }
            }
        }

        [Test]
        public void RollSlots_HigherLevelsTendToHaveMoreSlots()
        {
            // Statistical test: average slot count should increase with level
            float avgLow = GetAverageSlotCount(5, 200);
            float avgHigh = GetAverageSlotCount(45, 200);

            // At low levels, prob=0.25 => expected ~1.25 slots
            // At high levels, prob=0.90 => expected ~4.5 slots
            Assert.Less(avgLow, avgHigh,
                $"High level avg ({avgHigh:F2}) should exceed low level avg ({avgLow:F2})");
        }

        private float GetAverageSlotCount(int level, int iterations)
        {
            int total = 0;
            for (int i = 0; i < iterations; i++)
            {
                total += EquipmentPartConfig.RollSlots(level).Count;
            }
            return (float)total / iterations;
        }

        // ===================== Boundary levels =====================

        [Test]
        public void RollSlots_Level0_WorksCorrectly()
        {
            var slots = EquipmentPartConfig.RollSlots(0);
            Assert.IsNotNull(slots);
            Assert.GreaterOrEqual(slots.Count, 1);
        }

        [Test]
        public void RollSlots_Level100_WorksCorrectly()
        {
            var slots = EquipmentPartConfig.RollSlots(100);
            Assert.IsNotNull(slots);
            Assert.GreaterOrEqual(slots.Count, 1);
        }
    }
}