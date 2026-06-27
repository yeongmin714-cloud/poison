using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 29: 희귀 광석 종류.
    /// </summary>
    public enum GemType
    {
        Ruby,       // ★★★ 무기 공격력 +10, 500g
        Sapphire,   // ★★★ 방어력 +8, 500g
        Emerald,    // ★★★★ 이동속도 +15%, 800g
        Amethyst,   // ★★★★ 독 저항 +30%, 800g
        GoldGem,    // ★★★★★ 모든 능력치 +5, 2000g
        Diamond     // ★★★★★ 전설 장비 재료, 3000g
    }

    /// <summary>
    /// Phase 29: 광석 데이터.
    /// </summary>
    [System.Serializable]
    public struct GemData
    {
        public GemType type;
        public string displayName;
        public int starRating;  // 3~5
        public string effectDescription;
        public int goldValue;
        public Color color;

        public static GemData GetGemData(GemType type)
        {
            return type switch
            {
                GemType.Ruby => new GemData
                {
                    type = GemType.Ruby,
                    displayName = "루비",
                    starRating = 3,
                    effectDescription = "무기 공격력 +10",
                    goldValue = 500,
                    color = new Color(1f, 0.2f, 0.2f)
                },
                GemType.Sapphire => new GemData
                {
                    type = GemType.Sapphire,
                    displayName = "사파이어",
                    starRating = 3,
                    effectDescription = "방어력 +8",
                    goldValue = 500,
                    color = new Color(0.2f, 0.4f, 1f)
                },
                GemType.Emerald => new GemData
                {
                    type = GemType.Emerald,
                    displayName = "에메랄드",
                    starRating = 4,
                    effectDescription = "이동속도 +15%",
                    goldValue = 800,
                    color = new Color(0.2f, 1f, 0.4f)
                },
                GemType.Amethyst => new GemData
                {
                    type = GemType.Amethyst,
                    displayName = "자수정",
                    starRating = 4,
                    effectDescription = "독 저항 +30%",
                    goldValue = 800,
                    color = new Color(0.7f, 0.2f, 1f)
                },
                GemType.GoldGem => new GemData
                {
                    type = GemType.GoldGem,
                    displayName = "황금 보석",
                    starRating = 5,
                    effectDescription = "모든 능력치 +5",
                    goldValue = 2000,
                    color = new Color(1f, 0.85f, 0.2f)
                },
                GemType.Diamond => new GemData
                {
                    type = GemType.Diamond,
                    displayName = "다이아몬드",
                    starRating = 5,
                    effectDescription = "전설 장비 제작 재료",
                    goldValue = 3000,
                    color = new Color(0.8f, 0.9f, 1f)
                },
                _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, $"알 수 없는 GemType: {type}")
            };
        }
    }
}