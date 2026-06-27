using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// 장비 등급별 스탯 배율, 분산, 표시 이름, 색상 데이터를 제공하는 정적 클래스.
    /// 내부 배열 기반 lookup으로 switch 중복을 제거하고 O(1) 조회를 보장합니다.
    /// 새 등급 추가 시 _rarityData 배열만 수정하면 됩니다.
    /// </summary>
    public static class EquipmentRarityData
    {
        // ───────────────────── 내부 데이터 배열 ─────────────────────
        // ItemRarity enum 순서(Common=0, Uncommon=1, Rare=2, Epic=3, Legendary=4, Unique=5)와 동기화됨.

        private static readonly RarityEntry[] _rarityData =
        {
            new(1.0f, 0.10f, "일반",  new Color(0.6f, 0.6f, 0.6f, 1f)),  // Common
            new(1.3f, 0.10f, "고급",  new Color(0.2f, 0.8f, 0.2f, 1f)),  // Uncommon
            new(1.7f, 0.15f, "희귀",  new Color(0.2f, 0.4f, 1.0f, 1f)),  // Rare
            new(2.2f, 0.15f, "영웅",  new Color(0.6f, 0.2f, 1.0f, 1f)),  // Epic
            new(3.0f, 0.05f, "전설",  new Color(1.0f, 0.2f, 0.2f, 1f)),  // Legendary
            new(4.0f, 0.05f, "유니크", new Color(1.0f, 0.85f, 0.0f, 1f)), // Unique
        };

        private static readonly RarityEntry _fallback = new(1.0f, 0.10f, "알 수 없음", new Color(0.6f, 0.6f, 0.6f, 1f));

        // ───────────────────── 공개 API ─────────────────────

        /// <summary>
        /// 등급별 스탯 배율을 반환합니다.
        /// Common: 1.0배, Uncommon: 1.3배, Rare: 1.7배, Epic: 2.2배, Legendary: 3.0배, Unique: 4.0배
        /// </summary>
        public static float GetStatMultiplier(ItemRarity rarity) => GetEntry(rarity).statMultiplier;

        /// <summary>
        /// 등급별 스탯 분산(±%)을 반환합니다.
        /// Common/Uncommon: ±10%, Rare/Epic: ±15%, Legendary/Unique: ±5%
        /// </summary>
        public static float GetStatVariance(ItemRarity rarity) => GetEntry(rarity).statVariance;

        /// <summary>
        /// 등급의 한국어 표시 이름을 반환합니다.
        /// 일반 / 고급 / 희귀 / 영웅 / 전설 / 유니크
        /// </summary>
        public static string GetRarityDisplayName(ItemRarity rarity) => GetEntry(rarity).displayName;

        /// <summary>
        /// 등급별 표시 색상을 반환합니다.
        /// 회색 / 초록 / 파랑 / 보라 / 빨강 / 황금
        /// </summary>
        public static Color GetRarityColor(ItemRarity rarity) => GetEntry(rarity).color;

        // ───────────────────── 내부 ─────────────────────

        private static RarityEntry GetEntry(ItemRarity rarity)
        {
            int index = (int)rarity;
            if (index >= 0 && index < _rarityData.Length)
                return _rarityData[index];

            Debug.LogWarning($"[EquipmentRarityData] 알 수 없는 등급: {rarity} ({(int)rarity}) — fallback 사용");
            return _fallback;
        }

        /// <summary>
        /// 등급별 데이터를 담는 내부 레코드 구조체.
        /// </summary>
        private struct RarityEntry
        {
            public readonly float   statMultiplier;
            public readonly float   statVariance;
            public readonly string  displayName;
            public readonly Color   color;

            public RarityEntry(float statMultiplier, float statVariance, string displayName, Color color)
            {
                this.statMultiplier = statMultiplier;
                this.statVariance   = statVariance;
                this.displayName    = displayName;
                this.color          = color;
            }
        }
    }
}