using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// O2 C-O2-02: 경제 가격 정책 (OpenMMO ECONOMY.md 스프레드 규칙).
    ///
    /// - 구매는 정가(상점 표시가), 판매는 기본가 30~50% 스프레드(기본 40%).
    /// - 아이템 기본가: ItemData.basePrice > 0 이면 그 값 우선,
    ///   없으면 등급(rarity) 표 × 카테고리 배율로 산출.
    ///   장비(maxDurability &gt; 0)는 등급표가 주가 되고(배율 1.0~1.5),
    ///   소모품/재료는 카테고리 배율(0.3~0.5)로 가치가 눌린다.
    /// - 차익 불변식: 최대 할인 구매가 비율(1 - MaxNegotiationDiscount = 0.8)
    ///   이 최대 판매 비율(SellSpread = 0.4)보다 커야 한다 — 사다리꼴 차익 방지.
    ///
    /// Core 전용(static) — Systems/UI 의존 없음. foreach 규약 준수(분기 테이블, 루프 없음).
    /// </summary>
    public static class EconomyPricing
    {
        /// <summary>판매 스프레드 — 정가 대비 판매 비율 40% (30~50% 밴드 기본값).</summary>
        public const float SellSpread = 0.4f;

        /// <summary>구매 협상 할인 상한 (밴드 ±20%).</summary>
        public const float MaxNegotiationDiscount = 0.2f;

        // ── 구매가 ───────────────────────────────────────────────────────

        /// <summary>
        /// 협상 할인이 적용된 구매가. discountPct는 [0, MaxNegotiationDiscount]로 클램프.
        /// 최소 1G 보장 (무료 판매 방지).
        /// </summary>
        public static int GetBuyPrice(int basePrice, float discountPct)
        {
            float clamped = Mathf.Clamp(discountPct, 0f, MaxNegotiationDiscount);
            return Mathf.Max(1, Mathf.CeilToInt(basePrice * (1f - clamped)));
        }

        // ── 기본가 (등급 표 × 카테고리 배율) ─────────────────────────────

        /// <summary>
        /// 아이템 기본가 산출. basePrice &gt; 0 이면 그 값 우선,
        /// 아니면 등급 표 {Common 20 / Uncommon 60 / Rare 150 / Epic 400 /
        /// Legendary 1000 / Unique 2500} × 카테고리 배율 {Weapon 1.5 / Armor 1.2 /
        /// Tool 1.0 / Potion 0.5 / Material 0.3 / 기타 0.5}.
        /// 산출 불가(null)면 0. 최종 Max(1, CeilToInt).
        /// </summary>
        public static int GetBasePrice(PlayerInventory.ItemData item)
        {
            if (item == null) return 0;
            if (item.basePrice > 0)
                return Mathf.Max(1, Mathf.CeilToInt(item.basePrice));

            int rarityPrice;
            switch (item.rarity)
            {
                case ItemRarity.Common:    rarityPrice = 20;   break;
                case ItemRarity.Uncommon:  rarityPrice = 60;   break;
                case ItemRarity.Rare:      rarityPrice = 150;  break;
                case ItemRarity.Epic:      rarityPrice = 400;  break;
                case ItemRarity.Legendary: rarityPrice = 1000; break;
                case ItemRarity.Unique:    rarityPrice = 2500; break;
                default:                   rarityPrice = 20;   break;
            }

            float categoryMult;
            switch (item.category)
            {
                case PlayerInventory.ItemCategory.Weapon:   categoryMult = 1.5f; break;
                case PlayerInventory.ItemCategory.Armor:    categoryMult = 1.2f; break;
                case PlayerInventory.ItemCategory.Tool:     categoryMult = 1.0f; break;
                case PlayerInventory.ItemCategory.Potion:   categoryMult = 0.5f; break;
                case PlayerInventory.ItemCategory.Material: categoryMult = 0.3f; break;
                default:                                    categoryMult = 0.5f; break;
            }

            return Mathf.Max(1, Mathf.CeilToInt(rarityPrice * categoryMult));
        }

        // ── 판매가 ───────────────────────────────────────────────────────

        /// <summary>
        /// 판매가 = GetBasePrice × SellSpread(40%), 반올림 올림.
        /// GetBasePrice 결과가 0이면 0 반환 (판매 불가 — null/무가격).
        /// </summary>
        public static int GetSellPrice(PlayerInventory.ItemData item)
        {
            int basePrice = GetBasePrice(item);
            if (basePrice <= 0)
                return 0; // 판매 불가
            return Mathf.Max(1, Mathf.CeilToInt(basePrice * SellSpread));
        }

        // ── 차익 불변식 ──────────────────────────────────────────────────

        /// <summary>
        /// 차익 불변식: 최대 할인 구매가 비율(1 - MaxNegotiationDiscount = 0.8)
        /// &gt; 최대 판매 비율(SellSpread = 0.4).
        /// 상수가 서로 역전되면 false — 구매 후 판매로 무한 이득 가능 경고용.
        /// </summary>
        public static bool ArbitrageInvariantHolds()
        {
            return (1f - MaxNegotiationDiscount) > SellSpread;
        }
    }
}
