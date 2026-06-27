using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

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
        // ── 실패 분배 상수 ──
        private const float FailThreshold_Preserved  = 0.40f; // 40% 재료 보존
        private const float FailThreshold_Destroyed  = 0.80f; // 40% 재료 1개 소멸
        // 나머지 20%: 재료 전소 (Fail_Burned)
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

            // 실패 결과 랜덤 분배
            float failRoll = Random.value;
            if (failRoll < FailThreshold_Preserved)
                return CraftResult.Fail_MaterialPreserved;   // 40% 재료 보존
            else if (failRoll < FailThreshold_Destroyed)
                return CraftResult.Fail_MaterialDestroyed;   // 40% 재료 1개 소멸
            else
                return CraftResult.Fail_Burned;              // 20% 재료 전소
        }

        /// <summary>
        /// 아이템 ID로부터 등급 추정
        /// 소문자 접두사/패턴 매칭 기반
        /// </summary>
        public static string GetGradeFromItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return "Common";

            // 소문자 변환 후 패턴 매칭 (대소문자 구분 없음)
            // legendary는 접두사 제약 없이 매칭, epic/rare는 '_' 접미사 필요 (오탐 방지)
            string idLower = itemId.ToLowerInvariant();

            if (idLower.Contains("legendary")) return "Legendary";
            if (idLower.Contains("epic_"))     return "Epic";
            if (idLower.Contains("rare_"))     return "Rare";

            return "Common";
        }
    }
}