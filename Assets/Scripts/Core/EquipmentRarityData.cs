using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 장비 등급별 스탯 배율, 분산, 표시 이름, 색상 데이터를 제공하는 정적 클래스.
    /// </summary>
    public static class EquipmentRarityData
    {
        // ===================== 스탯 배율 =====================

        /// <summary>
        /// 등급별 스탯 배율을 반환합니다.
        /// Common: 1.0배, Uncommon: 1.3배, Rare: 1.7배, Epic: 2.2배, Legendary: 3.0배, Unique: 4.0배
        /// </summary>
        public static float GetStatMultiplier(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common:    return 1.0f;
                case ItemRarity.Uncommon:  return 1.3f;
                case ItemRarity.Rare:      return 1.7f;
                case ItemRarity.Epic:      return 2.2f;
                case ItemRarity.Legendary: return 3.0f;
                case ItemRarity.Unique:    return 4.0f;
                default:                   return 1.0f;
            }
        }

        // ===================== 스탯 분산 (±%) =====================

        /// <summary>
        /// 등급별 스탯 분산(±%)을 반환합니다.
        /// Common/Uncommon: ±10%, Rare/Epic: ±15%, Legendary/Unique: ±5%
        /// </summary>
        public static float GetStatVariance(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common:
                case ItemRarity.Uncommon:
                    return 0.10f;

                case ItemRarity.Rare:
                case ItemRarity.Epic:
                    return 0.15f;

                case ItemRarity.Legendary:
                case ItemRarity.Unique:
                    return 0.05f;

                default:
                    return 0.10f;
            }
        }

        // ===================== 한국어 표시 이름 =====================

        /// <summary>
        /// 등급의 한국어 표시 이름을 반환합니다.
        /// 일반 / 고급 / 희귀 / 영웅 / 전설 / 유니크
        /// </summary>
        public static string GetRarityDisplayName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common:    return "일반";
                case ItemRarity.Uncommon:  return "고급";
                case ItemRarity.Rare:      return "희귀";
                case ItemRarity.Epic:      return "영웅";
                case ItemRarity.Legendary: return "전설";
                case ItemRarity.Unique:    return "유니크";
                default:                   return "알 수 없음";
            }
        }

        // ===================== 등급 색상 =====================

        /// <summary>
        /// 등급별 표시 색상을 반환합니다.
        /// 회색 / 초록 / 파랑 / 보라 / 빨강 / 황금
        /// </summary>
        public static Color GetRarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common:    return new Color(0.6f, 0.6f, 0.6f, 1f);  // 회색
                case ItemRarity.Uncommon:  return new Color(0.2f, 0.8f, 0.2f, 1f);  // 초록
                case ItemRarity.Rare:      return new Color(0.2f, 0.4f, 1.0f, 1f);  // 파랑
                case ItemRarity.Epic:      return new Color(0.6f, 0.2f, 1.0f, 1f);  // 보라
                case ItemRarity.Legendary: return new Color(1.0f, 0.2f, 0.2f, 1f);  // 빨강
                case ItemRarity.Unique:    return new Color(1.0f, 0.85f, 0.0f, 1f);  // 황금
                default:                   return new Color(0.6f, 0.6f, 0.6f, 1f);  // 회색 (기본)
            }
        }
    }
}