using NUnit.Framework;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class LuckyRollSystemTests
    {
        [Test]
        public void TryLuck_Common_ReturnsCommonOrHigher()
        {
            var result = LuckyRollSystem.TryLuck(ItemRarity.Common);
            Assert.That(result, Is.AtLeast(ItemRarity.Common));
        }

        [Test]
        public void TryLuck_Legendary_ReturnsLegendary()
        {
            var result = LuckyRollSystem.TryLuck(ItemRarity.Legendary);
            Assert.AreEqual(ItemRarity.Legendary, result);
        }

        [Test]
        public void TryLuck_Unique_ReturnsUnique()
        {
            var result = LuckyRollSystem.TryLuck(ItemRarity.Unique);
            Assert.AreEqual(ItemRarity.Unique, result);
        }

        [Test]
        public void TryLuck_NeverExceedsLegendary()
        {
            for (int i = 0; i < 100; i++)
            {
                var r = LuckyRollSystem.TryLuck(ItemRarity.Epic);
                Assert.That(r, Is.AtMost(ItemRarity.Legendary));
            }
        }

        [Test]
        public void TryLuck_CanPromoteByAtMost2Tiers()
        {
            var diff = LuckyRollSystem.TryLuck(ItemRarity.Common) - ItemRarity.Common;
            Assert.That(diff, Is.InRange(0, 2));
        }

        [Test]
        public void Constants_AreReasonable()
        {
            Assert.AreEqual(0.05f, LuckyRollSystem.LUCKY_ROLL_CHANCE);
            Assert.AreEqual(0.05f, LuckyRollSystem.DOUBLE_LUCKY_CHANCE);
        }

        [Test]
        public void TryLuck_NonDecreasing()
        {
            for (int i = 0; i < 100; i++)
            {
                var r = LuckyRollSystem.TryLuck(ItemRarity.Rare);
                Assert.That(r, Is.AtLeast(ItemRarity.Rare));
            }
        }
    }
}
