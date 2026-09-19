using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// O2 C-O2-02: EconomyPricing 정적 로직 테스트.
    /// 판매 스프레드(40%), 최소 1G, 무가격/널 판매 불가(0), 등급 가격표
    /// (Common 20 ~ Unique 2500), 카테고리 배율(Weapon 1.5 등), basePrice 우선,
    /// 차익 불변식, 협상 할인 클램프(±20% 밴드), 구매가 최소 1.
    /// </summary>
    public class EconomyPricingTests
    {
        // ── 헬퍼 ─────────────────────────────────────────────────────────

        private static PlayerInventory.ItemData MakeItem(
            PlayerInventory.ItemCategory category = PlayerInventory.ItemCategory.Tool,
            ItemRarity rarity = ItemRarity.Common,
            int basePrice = 0,
            int maxDurability = 0)
        {
            return new PlayerInventory.ItemData
            {
                id = "test_item",
                displayName = "테스트 아이템",
                category = category,
                rarity = rarity,
                basePrice = basePrice,
                maxDurability = maxDurability,
            };
        }

        // ── 상수 ─────────────────────────────────────────────────────────

        [Test]
        public void Constants_Spread40_Negotiation20()
        {
            Assert.AreEqual(0.4f, EconomyPricing.SellSpread);
            Assert.AreEqual(0.2f, EconomyPricing.MaxNegotiationDiscount);
        }

        // ── 판매 스프레드 ────────────────────────────────────────────────

        [Test]
        public void GetSellPrice_BasePrice100_Returns40()
        {
            var item = MakeItem(basePrice: 100);
            Assert.AreEqual(40, EconomyPricing.GetSellPrice(item));
        }

        [Test]
        public void GetSellPrice_RoundsUp_Ceil()
        {
            // 123 × 0.4 = 49.2 → 50
            var item = MakeItem(basePrice: 123);
            Assert.AreEqual(50, EconomyPricing.GetSellPrice(item));
        }

        [Test]
        public void GetSellPrice_MinimumOne()
        {
            // basePrice 1 → 1 × 0.4 = 0.4 → 최소 1G 보장
            var item = MakeItem(basePrice: 1);
            Assert.AreEqual(1, EconomyPricing.GetSellPrice(item));
        }

        [Test]
        public void GetSellPrice_NullItem_ReturnsZero_CannotSell()
        {
            Assert.AreEqual(0, EconomyPricing.GetSellPrice(null));
        }

        [Test]
        public void GetSellPrice_TablePricedItem_NeverZero()
        {
            // basePrice=0 아이템도 등급표로 가치가 산출되면 판매 가능 (0G 판매 방지)
            var item = MakeItem(category: PlayerInventory.ItemCategory.Weapon, maxDurability: 20);
            Assert.Greater(EconomyPricing.GetSellPrice(item), 0);
        }

        [Test]
        public void GetSellPrice_CommonWeapon_SellsAt12()
        {
            // 등급표 20 × 무기 1.5 = 30 → 30 × 0.4 = 12
            var item = MakeItem(category: PlayerInventory.ItemCategory.Weapon, maxDurability: 20);
            Assert.AreEqual(12, EconomyPricing.GetSellPrice(item));
        }

        // ── 등급 가격표 (Tool ×1.0 기준) ────────────────────────────────

        [Test]
        public void GetBasePrice_RarityTable_Common_Tool()
        {
            Assert.AreEqual(20, EconomyPricing.GetBasePrice(MakeItem(rarity: ItemRarity.Common)));
        }

        [Test]
        public void GetBasePrice_RarityTable_Uncommon_Tool()
        {
            Assert.AreEqual(60, EconomyPricing.GetBasePrice(MakeItem(rarity: ItemRarity.Uncommon)));
        }

        [Test]
        public void GetBasePrice_RarityTable_Rare_Tool()
        {
            Assert.AreEqual(150, EconomyPricing.GetBasePrice(MakeItem(rarity: ItemRarity.Rare)));
        }

        [Test]
        public void GetBasePrice_RarityTable_Epic_Tool()
        {
            Assert.AreEqual(400, EconomyPricing.GetBasePrice(MakeItem(rarity: ItemRarity.Epic)));
        }

        [Test]
        public void GetBasePrice_RarityTable_Legendary_Tool()
        {
            Assert.AreEqual(1000, EconomyPricing.GetBasePrice(MakeItem(rarity: ItemRarity.Legendary)));
        }

        [Test]
        public void GetBasePrice_RarityTable_Unique_Tool()
        {
            Assert.AreEqual(2500, EconomyPricing.GetBasePrice(MakeItem(rarity: ItemRarity.Unique)));
        }

        [Test]
        public void GetBasePrice_Null_ReturnsZero()
        {
            Assert.AreEqual(0, EconomyPricing.GetBasePrice(null));
        }

        [Test]
        public void GetBasePrice_MinimumOne()
        {
            // 산출 가능한 아이템의 기본가는 항상 최소 1G
            var item = MakeItem(basePrice: 0, maxDurability: 20);
            Assert.GreaterOrEqual(EconomyPricing.GetBasePrice(item), 1);
        }

        // ── 카테고리 배율 (Common 등급 기준) ────────────────────────────

        [Test]
        public void GetBasePrice_CategoryMultiplier_Weapon_1_5()
        {
            // 20 × 1.5 = 30 (장비 maxDurability > 0 — 등급표 기반)
            var item = MakeItem(category: PlayerInventory.ItemCategory.Weapon, maxDurability: 20);
            Assert.AreEqual(30, EconomyPricing.GetBasePrice(item));
        }

        [Test]
        public void GetBasePrice_CategoryMultiplier_Armor_1_2()
        {
            // 20 × 1.2 = 24
            var item = MakeItem(category: PlayerInventory.ItemCategory.Armor, maxDurability: 30);
            Assert.AreEqual(24, EconomyPricing.GetBasePrice(item));
        }

        [Test]
        public void GetBasePrice_CategoryMultiplier_Tool_1_0()
        {
            var item = MakeItem(category: PlayerInventory.ItemCategory.Tool, maxDurability: 30);
            Assert.AreEqual(20, EconomyPricing.GetBasePrice(item));
        }

        [Test]
        public void GetBasePrice_CategoryMultiplier_Potion_0_5()
        {
            // 소모품 — 20 × 0.5 = 10
            var item = MakeItem(category: PlayerInventory.ItemCategory.Potion);
            Assert.AreEqual(10, EconomyPricing.GetBasePrice(item));
        }

        [Test]
        public void GetBasePrice_CategoryMultiplier_Material_0_3()
        {
            // 재료 — 20 × 0.3 = 6
            var item = MakeItem(category: PlayerInventory.ItemCategory.Material);
            Assert.AreEqual(6, EconomyPricing.GetBasePrice(item));
        }

        [Test]
        public void GetBasePrice_CategoryMultiplier_Other_0_5()
        {
            // 기타(Herb 등) — 20 × 0.5 = 10
            var item = MakeItem(category: PlayerInventory.ItemCategory.Herb);
            Assert.AreEqual(10, EconomyPricing.GetBasePrice(item));
        }

        // ── basePrice 우선 ───────────────────────────────────────────────

        [Test]
        public void GetBasePrice_CustomBasePrice_OverridesTable()
        {
            var item = MakeItem(category: PlayerInventory.ItemCategory.Weapon, basePrice: 123, maxDurability: 20);
            Assert.AreEqual(123, EconomyPricing.GetBasePrice(item));
            // 판매가 = ceil(123 × 0.4) = 50
            Assert.AreEqual(50, EconomyPricing.GetSellPrice(item));
        }

        [Test]
        public void GetBasePrice_CustomBasePrice_BeatsHighRarity()
        {
            // 커스텀 50 < Unique 등급표 2500 — basePrice 우선 확인
            var item = MakeItem(rarity: ItemRarity.Unique, basePrice: 50);
            Assert.AreEqual(50, EconomyPricing.GetBasePrice(item));
        }

        // ── 차익 불변식 ──────────────────────────────────────────────────

        [Test]
        public void ArbitrageInvariant_Holds_True()
        {
            // 최대 할인 구매가 비율 0.8 > 최대 판매 비율 0.4 → true
            Assert.IsTrue(EconomyPricing.ArbitrageInvariantHolds());
            Assert.Greater(1f - EconomyPricing.MaxNegotiationDiscount, EconomyPricing.SellSpread);
        }

        // ── 구매가: 협상 할인 클램프 + 최소 1 ───────────────────────────

        [Test]
        public void GetBuyPrice_NoDiscount_ReturnsBasePrice()
        {
            Assert.AreEqual(100, EconomyPricing.GetBuyPrice(100, 0f));
        }

        [Test]
        public void GetBuyPrice_MaxDiscount_80Pct()
        {
            Assert.AreEqual(80, EconomyPricing.GetBuyPrice(100, EconomyPricing.MaxNegotiationDiscount));
        }

        [Test]
        public void GetBuyPrice_OverDiscount_ClampedTo20Pct()
        {
            // 30% 요청 → 20%로 클램프 → 80
            Assert.AreEqual(80, EconomyPricing.GetBuyPrice(100, 0.3f));
        }

        [Test]
        public void GetBuyPrice_NegativeDiscount_ClampedToZero()
        {
            // 음수 할인(추가 요금) 요청 → 0% 클램프 → 정가
            Assert.AreEqual(100, EconomyPricing.GetBuyPrice(100, -0.5f));
        }

        [Test]
        public void GetBuyPrice_MinimumOne()
        {
            Assert.AreEqual(1, EconomyPricing.GetBuyPrice(1, 0.2f));  // ceil(0.8) = 1
            Assert.AreEqual(1, EconomyPricing.GetBuyPrice(0, 0f));    // Max(1, 0)
        }

        [Test]
        public void GetBuyPrice_RoundsUp_Ceil()
        {
            // 55 × 0.8 = 44.0 → 44, 56 × 0.8 = 44.8 → 45
            Assert.AreEqual(44, EconomyPricing.GetBuyPrice(55, 0.2f));
            Assert.AreEqual(45, EconomyPricing.GetBuyPrice(56, 0.2f));
        }
    }
}
