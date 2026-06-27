using System.Collections.Generic;

namespace ProjectName.Core
{
    /// <summary>
    /// 레벨 구간별 아이템 등급 확률 테이블.
    /// 5개 레벨 그룹(Lv1-10, Lv11-20, Lv21-30, Lv31-40, Lv41-50)에 대해
    /// 각 등급의 출현 확률을 정의하고 랜덤 롤을 제공합니다.
    /// </summary>
    public static class RarityProbabilityTable
    {
        // ===================== 레벨 그룹 정의 =====================

        /// <summary>
        /// 10레벨 단위로 구분된 레벨 그룹 (Lv1-10, Lv11-20, ..., Lv41-50).
        /// 각 그룹은 서로 다른 등장 확률 구성을 가집니다.
        /// </summary>
        public enum LevelGroup
        {
            Lv1_10,
            Lv11_20,
            Lv21_30,
            Lv31_40,
            Lv41_50
        }

        /// <summary>
        /// 레벨 값으로부터 해당하는 레벨 그룹을 반환합니다.
        /// </summary>
        public static LevelGroup GetLevelGroup(int level)
        {
            if (level >= 1 && level <= 10)  return LevelGroup.Lv1_10;
            if (level >= 11 && level <= 20) return LevelGroup.Lv11_20;
            if (level >= 21 && level <= 30) return LevelGroup.Lv21_30;
            if (level >= 31 && level <= 40) return LevelGroup.Lv31_40;
            if (level >= 41 && level <= 50) return LevelGroup.Lv41_50;

            // 범위 밖은 가장 가까운 그룹으로 클램핑
            if (level < 1)  return LevelGroup.Lv1_10;
            return LevelGroup.Lv41_50;
        }

        // ===================== 확률 데이터 =====================

        private static readonly Dictionary<LevelGroup, Dictionary<ItemRarity, float>> ProbabilityTable =
            new Dictionary<LevelGroup, Dictionary<ItemRarity, float>>
            {
                [LevelGroup.Lv1_10] = new Dictionary<ItemRarity, float>
                {
                    { ItemRarity.Common,    0.70f },
                    { ItemRarity.Uncommon,  0.20f },
                    { ItemRarity.Rare,      0.08f },
                    { ItemRarity.Epic,      0.02f },
                    { ItemRarity.Legendary, 0f },
                    { ItemRarity.Unique,    0f },
                },
                [LevelGroup.Lv11_20] = new Dictionary<ItemRarity, float>
                {
                    { ItemRarity.Common,    0.30f },
                    { ItemRarity.Uncommon,  0.40f },
                    { ItemRarity.Rare,      0.20f },
                    { ItemRarity.Epic,      0.08f },
                    { ItemRarity.Legendary, 0.02f },
                    { ItemRarity.Unique,    0f },
                },
                [LevelGroup.Lv21_30] = new Dictionary<ItemRarity, float>
                {
                    { ItemRarity.Common,    0.05f },
                    { ItemRarity.Uncommon,  0.25f },
                    { ItemRarity.Rare,      0.40f },
                    { ItemRarity.Epic,      0.20f },
                    { ItemRarity.Legendary, 0.10f },
                    { ItemRarity.Unique,    0f },
                },
                [LevelGroup.Lv31_40] = new Dictionary<ItemRarity, float>
                {
                    { ItemRarity.Common,    0f },
                    { ItemRarity.Uncommon,  0.10f },
                    { ItemRarity.Rare,      0.30f },
                    { ItemRarity.Epic,      0.40f },
                    { ItemRarity.Legendary, 0.20f },
                    { ItemRarity.Unique,    0f },
                },
                [LevelGroup.Lv41_50] = new Dictionary<ItemRarity, float>
                {
                    { ItemRarity.Common,    0f },
                    { ItemRarity.Uncommon,  0f },
                    { ItemRarity.Rare,      0.15f },
                    { ItemRarity.Epic,      0.45f },
                    { ItemRarity.Legendary, 0.40f },
                    { ItemRarity.Unique,    0f },
                },
            };

        // ===================== 공개 API =====================

        /// <summary>
        /// 주어진 레벨에 해당하는 등급별 확률 테이블을 반환합니다.
        /// </summary>
        public static Dictionary<ItemRarity, float> GetProbabilities(int level)
        {
            var group = GetLevelGroup(level);
            if (ProbabilityTable.TryGetValue(group, out var probs))
            {
                // 방어적 복사본 반환
                return new Dictionary<ItemRarity, float>(probs);
            }
            return new Dictionary<ItemRarity, float>();
        }

        /// <summary>
        /// 주어진 레벨 그룹에 해당하는 등급별 확률 테이블을 반환합니다.
        /// </summary>
        public static Dictionary<ItemRarity, float> GetProbabilitiesForGroup(LevelGroup group)
        {
            if (ProbabilityTable.TryGetValue(group, out var probs))
            {
                return new Dictionary<ItemRarity, float>(probs);
            }
            return new Dictionary<ItemRarity, float>();
        }

        /// <summary>
        /// 주어진 레벨에 대해 가중치 기반 랜덤 롤로 하나의 등급을 반환합니다.
        /// 확률 합계가 1.0 미만이면 나머지 확률은 Common으로 처리됩니다.
        /// </summary>
        public static ItemRarity Roll(int level)
        {
            var probs = GetProbabilities(level);
            return RollFromProbabilities(probs);
        }

        /// <summary>
        /// 주어진 레벨 그룹에 대해 가중치 기반 랜덤 롤로 하나의 등급을 반환합니다.
        /// </summary>
        public static ItemRarity RollForGroup(LevelGroup group)
        {
            var probs = GetProbabilitiesForGroup(group);
            return RollFromProbabilities(probs);
        }

        /// <summary>
        /// 확률 딕셔너리를 기반으로 가중치 랜덤 롤을 수행합니다.
        /// </summary>
        public static ItemRarity RollFromProbabilities(Dictionary<ItemRarity, float> probs)
        {
            if (probs == null || probs.Count == 0)
                return ItemRarity.Common;

            float roll = UnityEngine.Random.value;
            float cumulative = 0f;

            foreach (var kvp in probs)
            {
                cumulative += kvp.Value;
                if (roll <= cumulative)
                    return kvp.Key;
            }

            // 확률 합계가 1.0 미만인 경우 나머지는 Common
            return ItemRarity.Common;
        }
    }
}