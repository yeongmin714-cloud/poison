using System;
using UnityEngine;
#pragma warning disable 0414

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

        /// <summary>기본 화살 인스턴스.</summary>
        public static readonly ArrowData Regular = new ArrowData(ArrowType.Regular);
        /// <summary>강화 화살 인스턴스.</summary>
        public static readonly ArrowData Reinforced = new ArrowData(ArrowType.Reinforced);
        /// <summary>마법 화살 인스턴스.</summary>
        public static readonly ArrowData Magic = new ArrowData(ArrowType.Magic);

        public ArrowType arrowType { get; private set; }
        public string displayName { get; private set; }
        public int damageBonus { get; private set; }
        public string description { get; private set; }
        public ItemRarity rarity { get; private set; }
        public int goldCost { get; private set; }       // 상점 구매 비용
        public Color trailColor { get; private set; }   // 궤적 색상
        /// <summary>[C 고품질] 타겟 적 1기를 추가 관통(멀티히트) — 마법 화살만. 아군/지면은 기존 방지 유지.</summary>
        public bool canPierce { get; private set; }
        /// <summary>[C 고품질] 3티어 시각 파라미터 — 발광 강도/스트릭 색/스파크 유무/헤드 발광색.</summary>
        public float glowStrength { get; private set; }   // 0~1 헤드/트레일 발광
        public Color streakColor { get; private set; }    // 비행 모션 스트릭 색 (트레일 그래디언트 종결색)
        public bool sparkTrail { get; private set; }      // 비행 중 샤프 스파크 잔상
        public Color tipGlow { get; private set; }        // 헤드 발광(마법=보라)

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
                    trailColor = new Color(0.85f, 0.62f, 0.28f); // 밝은 황갈색(가시성)
                    canPierce = false; glowStrength = 0.0f;
                    streakColor = new Color(0.75f, 0.82f, 1f);
                    sparkTrail = false; tipGlow = new Color(0.8f, 0.8f, 0.8f);
                    break;
                case ArrowType.Reinforced:
                    displayName = "강화 화살";
                    damageBonus = 5;
                    description = "철촉이 달린 강화 화살. +5 데미지, 은빛 스파크 잔상.";
                    rarity = ItemRarity.Uncommon;
                    goldCost = 15;
                    trailColor = new Color(0.95f, 0.95f, 1.0f); // 밝은 은백색(가시성)
                    canPierce = false; glowStrength = 0.35f;
                    streakColor = new Color(0.7f, 0.75f, 0.85f);
                    sparkTrail = true; tipGlow = new Color(0.9f, 0.9f, 0.95f);
                    break;
                case ArrowType.Magic:
                    displayName = "마법 화살";
                    damageBonus = 15;
                    description = "마력이 깃든 화살. +15 데미지, 보라 리본+적 1기 관통.";
                    rarity = ItemRarity.Rare;
                    goldCost = 50;
                    trailColor = new Color(0.95f, 0.4f, 1.0f); // 밝은 보라색(가시성)
                    canPierce = true; glowStrength = 1.0f;
                    streakColor = new Color(0.7f, 0.3f, 1f);
                    sparkTrail = false; tipGlow = new Color(1f, 0.5f, 1f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, $"알 수 없는 ArrowType: {type}");
            }
        }

        public string GetItemId() => $"arrow_{arrowType.ToString().ToLower()}";
    }
}
