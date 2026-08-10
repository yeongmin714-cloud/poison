using NUnit.Framework;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C32-04: LuckyRollSystem EditMode 테스트.
    /// </summary>
    public class C32_LuckyRollSystemTests
    {
        // ===== 상수 검증 =====

        [Test]
        public void LuckyRollChance_IsFivePercent()
        {
            Assert.AreEqual(0.05f, LuckyRollSystem.LUCKY_ROLL_CHANCE, 0.0001f);
        }

        [Test]
        public void DoubleLuckyChance_IsFivePercent()
        {
            Assert.AreEqual(0.05f, LuckyRollSystem.DOUBLE_LUCKY_CHANCE, 0.0001f);
        }

        // ===== Legendary / Unique 승격 없음 =====

        [Test]
        public void TryLuck_Legendary_ReturnsLegendary()
        {
            // Legendary는 승격되지 않음
            ItemRarity result = LuckyRollSystem.TryLuck(ItemRarity.Legendary);
            Assert.AreEqual(ItemRarity.Legendary, result);
        }

        [Test]
        public void TryLuck_Unique_ReturnsUnique()
        {
            // Unique는 승격되지 않음
            ItemRarity result = LuckyRollSystem.TryLuck(ItemRarity.Unique);
            Assert.AreEqual(ItemRarity.Unique, result);
        }

        // ===== Common → Uncommon 승격 가능성 =====

        [Test]
        public void TryLuck_Common_ReturnsCommonOrHigher()
        {
            // Common은 최소 Common 유지
            for (int i = 0; i < 100; i++)
            {
                ItemRarity result = LuckyRollSystem.TryLuck(ItemRarity.Common);
                Assert.IsTrue(result >= ItemRarity.Common);
                Assert.IsTrue(result <= ItemRarity.Legendary);
            }
        }

        // ===== Rare → Epic 승격 가능성 =====

        [Test]
        public void TryLuck_Rare_ReturnsRareOrHigher()
        {
            for (int i = 0; i < 100; i++)
            {
                ItemRarity result = LuckyRollSystem.TryLuck(ItemRarity.Rare);
                Assert.IsTrue(result >= ItemRarity.Rare);
                Assert.IsTrue(result <= ItemRarity.Legendary);
            }
        }

        // ===== Epic → Legendary 승격 가능성 =====

        [Test]
        public void TryLuck_Epic_ReturnsEpicOrLegendary()
        {
            for (int i = 0; i < 100; i++)
            {
                ItemRarity result = LuckyRollSystem.TryLuck(ItemRarity.Epic);
                Assert.IsTrue(result == ItemRarity.Epic || result == ItemRarity.Legendary);
            }
        }

        // ===== 통계 검증: 5% 행운 확률 =====

        [Test]
        public void TryLuck_Common_StatisticalCheck()
        {
            // 10000회 반복하여 대략 5% 정도 승격되는지 확인
            int total = 10000;
            int promoted = 0;

            for (int i = 0; i < total; i++)
            {
                ItemRarity result = LuckyRollSystem.TryLuck(ItemRarity.Common);
                if (result > ItemRarity.Common)
                    promoted++;
            }

            // 5% ± 2% 범위 내 (통계적 허용)
            float rate = (float)promoted / total;
            Assert.IsTrue(rate >= 0.03f, $"승격률 {rate:P2}가 3% 미만 (기대: 5%)");
            Assert.IsTrue(rate <= 0.07f, $"승격률 {rate:P2}가 7% 초과 (기대: 5%)");
        }

        // ===== 더블 럭 통계 검증: 5% * 5% = 0.25% =====

        [Test]
        public void TryLuck_Common_DoubleLuckyStatisticalCheck()
        {
            // 100000회 반복하여 대략 0.25% 정도 2단계 승격되는지 확인
            int total = 100000;
            int doublePromoted = 0;

            for (int i = 0; i < total; i++)
            {
                ItemRarity result = LuckyRollSystem.TryLuck(ItemRarity.Common);
                if ((int)result >= (int)ItemRarity.Common + 2) // Common → Rare 이상
                    doublePromoted++;
            }

            float rate = (float)doublePromoted / total;
            // 0.25% ± 0.15% 범위
            Assert.IsTrue(rate >= 0.001f, $"더블 승격률 {rate:P3}가 0.1% 미만 (기대: 0.25%)");
            Assert.IsTrue(rate <= 0.004f, $"더블 승격률 {rate:P3}가 0.4% 초과 (기대: 0.25%)");
        }

        // ===== Invalid 진입 검증 =====

        [Test]
        public void TryLuck_InvalidCast_DoesNotThrow()
        {
            // 모든 유효 등급에 대해 예외 없이 동작해야 함
            ItemRarity[] rarities = {
                ItemRarity.Common, ItemRarity.Uncommon,
                ItemRarity.Rare, ItemRarity.Epic,
                ItemRarity.Legendary, ItemRarity.Unique
            };

            foreach (var rarity in rarities)
            {
                Assert.DoesNotThrow(() => LuckyRollSystem.TryLuck(rarity));
            }
        }

        // ===== 승격 경로 검증: Common → Uncommon → Rare → Epic → Legendary =====

        [Test]
        public void TryLuck_PromotionPath_IsSequential()
        {
            // Uncommon → Rare (1단계 승격) 또는 Uncommon → Epic (2단계, 더블 럭)까지만 가능
            for (int i = 0; i < 1000; i++)
            {
                ItemRarity result = LuckyRollSystem.TryLuck(ItemRarity.Uncommon);
                int diff = (int)result - (int)ItemRarity.Uncommon;
                Assert.IsTrue(diff >= 0 && diff <= 2,
                    $"Uncommon 승격 결과: {result} (차이: {diff}), 0~2 범위를 벗어남");
            }
        }
    }
}