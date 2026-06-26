using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// AB-01: 화살 아이템 데이터.
    /// 활(Bow) 무기 사용 시 소모되는 탄약입니다.
    /// 3티어: 일반/강화/마법 화살.
    /// </summary>
    [System.Serializable]
    public class ArrowData
    {
        public enum ArrowType
        {
            Regular,      // 일반 화살
            Reinforced,   // 강화 화살
            Magic         // 마법 화살
        }

        public ArrowType arrowType { get; private set; }
        public string displayName { get; private set; }
        public int damageBonus { get; private set; }
        public string description { get; private set; }
        public ItemRarity rarity { get; private set; }
        public int goldCost { get; private set; }       // 상점 구매 비용
        public Color trailColor { get; private set; }   // 궤적 색상

        public ArrowData(ArrowType type)
        {
            arrowType = type;
            switch (type)
            {
                case ArrowType.Regular:
                    displayName = "일반 화살";
                    damageBonus = 0;
                    description = "기본 화살. 특별한 효과 없음.";
                    rarity = ItemRarity.Common;
                    goldCost = 5;
                    trailColor = new Color(0.55f, 0.35f, 0.15f); // 갈색
                    break;
                case ArrowType.Reinforced:
                    displayName = "강화 화살";
                    damageBonus = 5;
                    description = "철촉이 달린 강화 화살. +5 데미지.";
                    rarity = ItemRarity.Uncommon;
                    goldCost = 15;
                    trailColor = new Color(0.75f, 0.75f, 0.80f); // 은색
                    break;
                case ArrowType.Magic:
                    displayName = "마법 화살";
                    damageBonus = 15;
                    description = "마력이 깃든 화살. +15 데미지.";
                    rarity = ItemRarity.Rare;
                    goldCost = 50;
                    trailColor = new Color(0.7f, 0.2f, 0.9f); // 보라색
                    break;
            }
        }

        public static readonly ArrowData Regular = new ArrowData(ArrowType.Regular);
        public static readonly ArrowData Reinforced = new ArrowData(ArrowType.Reinforced);
        public static readonly ArrowData Magic = new ArrowData(ArrowType.Magic);

        public string GetItemId() => $"arrow_{arrowType.ToString().ToLower()}";
    }
}
