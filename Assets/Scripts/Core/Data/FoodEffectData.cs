using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Phase 5: 음식 효과 등급 데이터
    /// 
    /// 각 등급별 효과 배수와 가변 범위를 정의합니다.
    /// 
    /// 등급 체계:
    /// ⬜ 일반 (Common)    — 배수 1.0×, ±10%, 10±1
    /// 🟩 고급 (Uncommon)  — 배수 1.5×, ±15%, 15±2
    /// 🟡 희귀 (Rare)      — 배수 2.0×, ±20%, 25±3
    /// 🟣 전설 (Epic)      — 배수 3.0×, ±25%, 40±5
    /// 🟠 유니크 (Legendary) — 배수 5.0×, ±30%, 60±8
    /// </summary>
    [System.Serializable]
    public struct FoodEffectGradeData
    {
        public string gradeName;           // 등급 이름 (일반/고급/희귀/전설/유니크)
        public float multiplier;           // 효과 배수
        public float variancePercent;      // 변동 폭 (±%)
        public int baseEffectValue;        // 기본 효과 값
        public int varianceAmount;         // 변동 폭 (±값)

        /// <summary>
        /// 적용될 최종 효과 값 계산 (랜덤 변동 포함)
        /// </summary>
        public int CalculateFinalEffect()
        {
            float baseVal = baseEffectValue * multiplier;
            int variance = Mathf.RoundToInt(varianceAmount * multiplier);
            int min = Mathf.Max(1, Mathf.RoundToInt(baseVal) - variance);
            int max = Mathf.RoundToInt(baseVal) + variance;
            return Random.Range(min, max + 1);
        }

        /// <summary>
        /// 변동 범위 문자열 반환 (e.g. "10±2")
        /// </summary>
        public string GetVarianceString()
        {
            int baseVal = Mathf.RoundToInt(baseEffectValue * multiplier);
            int var = Mathf.RoundToInt(varianceAmount * multiplier);
            return $"{baseVal}±{var}";
        }
    }

    /// <summary>
    /// 음식 효과 등급별 데이터 저장소
    /// </summary>
    public static class FoodEffectData
    {
        // ===== 등급 정의 =====
        // ⬜ 일반 — 배수 1.0×, ±10%, 10±1
        public static readonly FoodEffectGradeData Common = new FoodEffectGradeData
        {
            gradeName = "⬜ 일반",
            multiplier = 1.0f,
            variancePercent = 0.10f,
            baseEffectValue = 10,
            varianceAmount = 1
        };

        // 🟩 고급 — 배수 1.5×, ±15%, 15±2
        public static readonly FoodEffectGradeData Uncommon = new FoodEffectGradeData
        {
            gradeName = "🟩 고급",
            multiplier = 1.5f,
            variancePercent = 0.15f,
            baseEffectValue = 15,
            varianceAmount = 2
        };

        // 🟡 희귀 — 배수 2.0×, ±20%, 25±3
        public static readonly FoodEffectGradeData Rare = new FoodEffectGradeData
        {
            gradeName = "🟡 희귀",
            multiplier = 2.0f,
            variancePercent = 0.20f,
            baseEffectValue = 25,
            varianceAmount = 3
        };

        // 🟣 전설 — 배수 3.0×, ±25%, 40±5
        public static readonly FoodEffectGradeData Epic = new FoodEffectGradeData
        {
            gradeName = "🟣 전설",
            multiplier = 3.0f,
            variancePercent = 0.25f,
            baseEffectValue = 40,
            varianceAmount = 5
        };

        // 🟠 유니크 — 배수 5.0×, ±30%, 60±8
        public static readonly FoodEffectGradeData Legendary = new FoodEffectGradeData
        {
            gradeName = "🟠 유니크",
            multiplier = 5.0f,
            variancePercent = 0.30f,
            baseEffectValue = 60,
            varianceAmount = 8
        };

        // ===== 등급 목록 =====
        private static readonly FoodEffectGradeData[] AllGrades = new FoodEffectGradeData[]
        {
            Common, Uncommon, Rare, Epic, Legendary
        };

        /// <summary>
        /// 모든 등급 데이터를 배열로 반환
        /// </summary>
        public static FoodEffectGradeData[] GetAllGrades() => AllGrades;

        /// <summary>
        /// 등급 이름으로 데이터 조회
        /// </summary>
        public static FoodEffectGradeData GetGradeByName(string name)
        {
            foreach (var grade in AllGrades)
            {
                if (grade.gradeName == name)
                    return grade;
            }
            return Common;
        }

        /// <summary>
        /// ItemRarity enum으로 등급 데이터 조회
        /// </summary>
        public static FoodEffectGradeData GetGradeByRarity(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return Common;
                case ItemRarity.Uncommon: return Uncommon;
                case ItemRarity.Rare: return Rare;
                case ItemRarity.Epic: return Epic;
                case ItemRarity.Legendary: return Legendary;
                default: return Common;
            }
        }

        /// <summary>
        /// 음식 아이템의 최종 효과 값을 계산합니다 (등급 기반 랜덤)
        /// </summary>
        public static int CalculateFoodEffect(ItemRarity rarity)
        {
            var grade = GetGradeByRarity(rarity);
            return grade.CalculateFinalEffect();
        }

        /// <summary>
        /// 음식 아이템의 효과값 문자열을 반환합니다 (e.g. "체력 회복 10±1")
        /// </summary>
        public static string GetFoodEffectString(ItemRarity rarity, string effectName = "체력 회복")
        {
            var grade = GetGradeByRarity(rarity);
            return $"{effectName} {grade.GetVarianceString()}";
        }
    }
}