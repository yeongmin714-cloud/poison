using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 2026-09-17: 밀매(Smuggle) 시스템.
    /// 화술(Speech) 스탯에 연동되어, 화술이 일정 수준 이상일 때 밀매가 가능하고
    /// 화술이 높을수록 밀매 숙련도/판매 프리미엄이 상승한다.
    /// </summary>
    public static class SmuggleSystem
    {
        /// <summary>밀매 숙련 (0~1) — 플레이어 화술 스탯에서 파생</summary>
        public static float SmuggleSkill => PlayerStats.Instance?.SmuggleSkill ?? 0.05f;

        /// <summary>밀매 판매 프리미엄 (1.0~1.5) — 플레이어 화술 스탯에서 파생</summary>
        public static float SmuggleGainMultiplier => PlayerStats.Instance?.SmuggleGainMultiplier ?? 1f;

        /// <summary>화술 일정 수준(숙련 0.30) 이상일 때 밀매 가능</summary>
        public static bool CanSmuggle => PlayerStats.Instance != null && PlayerStats.Instance.SmuggleSkill >= 0.30f;

        /// <summary>
        /// 아이템 카테고리 기준 추정가 산정.
        /// (ShopWindow.CalculateSellPrice 패턴 참고 — 밀매는 암시장 가치를 추정한다)
        /// </summary>
        public static int EstimateBaseValue(PlayerInventory.ItemData item)
        {
            switch (item.category)
            {
                case PlayerInventory.ItemCategory.Material: return 5;
                case PlayerInventory.ItemCategory.Herb: return 8;
                case PlayerInventory.ItemCategory.Meat: return 12;
                case PlayerInventory.ItemCategory.Potion: return 20;
                case PlayerInventory.ItemCategory.Tool: return 25;
                case PlayerInventory.ItemCategory.Weapon: return 30;
                case PlayerInventory.ItemCategory.Armor: return 28;
                default: return 10;
            }
        }
    }
}