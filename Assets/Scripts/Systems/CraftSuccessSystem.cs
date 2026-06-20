using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 제작 성공/실패 결과 열거형
    /// </summary>
    public enum CraftResult
    {
        Success,
        Fail_MaterialPreserved,
        Fail_MaterialDestroyed,
        Fail_Burned
    }

    /// <summary>
    /// 제작 성공/실패 시스템 (Phase 3.8e/f)
    /// 재료 등급 기반 성공률 계산 및 제작 실행
    /// </summary>
    public static class CraftSuccessSystem
    {
        /// <summary>
        /// 재료 등급별 기본 성공률
        /// </summary>
        public static float GetBaseSuccessRate(string ingredientGrade)
        {
            switch (ingredientGrade)
            {
                case "Common":    return 0.90f;
                case "Uncommon":  return 0.75f;
                case "Rare":      return 0.60f;
                case "Epic":      return 0.45f;
                case "Legendary": return 0.30f;
                default:          return 0.90f; // 기본 Common
            }
        }

        /// <summary>
        /// 연금술 성공률 보정 (0~1.0)
        /// PlayerStats.FinalAlchemyBonus 값 반환
        /// </summary>
        public static float GetAlchemyBonus()
        {
            if (PlayerStats.Instance != null)
                return PlayerStats.Instance.FinalAlchemyBonus;
            return 0f;
        }

        /// <summary>
        /// 요리 성공률 보정 (0~1.0)
        /// PlayerStats.FinalCookingBonus 값 반환
        /// </summary>
        public static float GetCookingBonus()
        {
            if (PlayerStats.Instance != null)
                return PlayerStats.Instance.FinalCookingBonus;
            return 0f;
        }

        /// <summary>
        /// 최종 성공률 계산
        /// (재료1 기본 성공률 + 재료2 기본 성공률) / 2 + 직업 보정
        /// </summary>
        public static float GetFinalSuccessRate(bool isAlchemy, string grade1, string grade2)
        {
            float baseRate1 = GetBaseSuccessRate(grade1);
            float baseRate2 = GetBaseSuccessRate(grade2);
            float avgBaseRate = (baseRate1 + baseRate2) / 2f;

            float bonus = isAlchemy ? GetAlchemyBonus() : GetCookingBonus();

            float finalRate = avgBaseRate + bonus;
            return Mathf.Clamp01(finalRate);
        }

        /// <summary>
        /// 제작 실행
        /// 성공/실패 결과 반환
        /// </summary>
        public static CraftResult ExecuteCraft(bool isAlchemy, string grade1, string grade2)
        {
            float successRate = GetFinalSuccessRate(isAlchemy, grade1, grade2);

            float roll = Random.value;

            if (roll < successRate)
            {
                return CraftResult.Success;
            }

            // 실패 결과 랜덤分配
            float failRoll = Random.value;
            if (failRoll < 0.40f)
                return CraftResult.Fail_MaterialPreserved;   // 40% 재료 보존
            else if (failRoll < 0.80f)
                return CraftResult.Fail_MaterialDestroyed;   // 40% 재료 1개 소멸
            else
                return CraftResult.Fail_Burned;              // 20% 재료 전소
        }

        /// <summary>
        /// 아이템 ID로부터 등급 추정
        /// id 접두사 또는 패턴 기반
        /// </summary>
        public static string GetGradeFromItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return "Common";

            // ID 기반 등급 추정 (필요에 따라 확장)
            if (itemId.Contains("legendary") || itemId.Contains("epic_") || itemId.Contains("rare_"))
            {
                if (itemId.Contains("legendary")) return "Legendary";
                if (itemId.Contains("epic"))      return "Epic";
                if (itemId.Contains("rare"))      return "Rare";
            }

            // displayName 기반 추정
            return "Common";
        }
    }
}