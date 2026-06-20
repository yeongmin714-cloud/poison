using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 행운의 롤 시스템 — 등급 승격 기회를 제공합니다.
    /// 5% 확률로 1티어 승격, 성공 시 추가 5% 확률로 2티어 승격.
    /// 최대 승격은 Legendary까지 (Unique 제외).
    /// Legendary나 Unique는 승격되지 않음.
    /// </summary>
    public static class LuckyRollSystem
    {
        /// <summary>1차 행운 롤 확률 (5%)</summary>
        public const float LUCKY_ROLL_CHANCE = 0.05f;

        /// <summary>2차 행운 롤 확률 (5%, 1차 성공 시에만 발동)</summary>
        public const float DOUBLE_LUCKY_CHANCE = 0.05f;

        /// <summary>
        /// 등급 행운 롤을 시도합니다.
        /// Common → Uncommon → Rare → Epic → Legendary 순서로 최대 2회 승격.
        /// Legendary 또는 Unique 등급은 승격되지 않습니다.
        /// </summary>
        /// <param name="rarity">원본 등급</param>
        /// <returns>승격된 등급 (변경되지 않을 수 있음)</returns>
        public static ItemRarity TryLuck(ItemRarity rarity)
        {
            // Legendary 이상은 승격 없음
            if (rarity >= ItemRarity.Legendary)
                return rarity;

            // 1차 행운 롤
            if (Random.value < LUCKY_ROLL_CHANCE)
            {
                rarity = (ItemRarity)((int)rarity + 1);

                // 1차 성공 시 Legendary 도달 여부 확인
                if (rarity >= ItemRarity.Legendary)
                    return ItemRarity.Legendary;

                // 2차 행운 롤 (더블 럭)
                if (Random.value < DOUBLE_LUCKY_CHANCE)
                {
                    rarity = (ItemRarity)((int)rarity + 1);

                    // 최대 Legendary
                    if (rarity >= ItemRarity.Legendary)
                        return ItemRarity.Legendary;
                }
            }

            return rarity;
        }
    }
}