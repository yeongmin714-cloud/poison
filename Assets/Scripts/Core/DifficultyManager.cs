using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// C20-02: 난이도 배율 시스템.
    /// DifficultyMode 열거형 값을 받아 몬스터/드랍/리스폰 배율을 반환합니다.
    /// Easy는 낮은 적 능력치/높은 드랍률, Hard는 높은 적 능력치/낮은 드랍률.
    /// </summary>
    public static class DifficultyManager
    {
        // ── 데미지 배율 ──────────────────────────────────────────
        private const float DamageEasy   = 0.6f;
        private const float DamageNormal = 1.0f;
        private const float DamageHard   = 1.5f;

        // ── HP 배율 ─────────────────────────────────────────────
        private const float HpEasy   = 0.7f;
        private const float HpNormal = 1.0f;
        private const float HpHard   = 1.5f;

        // ── 드랍률 배율 ─────────────────────────────────────────
        private const float DropRateEasy   = 1.5f;
        private const float DropRateNormal = 1.0f;
        private const float DropRateHard   = 0.7f;

        // ── 리스폰 속도 배율 ────────────────────────────────────
        private const float RespawnRateEasy   = 0.7f;
        private const float RespawnRateNormal = 1.0f;
        private const float RespawnRateHard   = 1.3f;

        /// <summary>
        /// 난이도별 몬스터 데미지 배율
        /// Easy=0.6, Normal=1.0, Hard=1.5
        /// </summary>
        public static float GetDamageMultiplier(DifficultyMode difficulty)
        {
            return difficulty switch
            {
                DifficultyMode.Easy   => DamageEasy,
                DifficultyMode.Normal => DamageNormal,
                DifficultyMode.Hard   => DamageHard,
                _ => LogAndFallback(nameof(GetDamageMultiplier), difficulty, DamageNormal)
            };
        }

        /// <summary>
        /// 난이도별 몬스터 HP 배율
        /// Easy=0.7, Normal=1.0, Hard=1.5
        /// </summary>
        public static float GetHpMultiplier(DifficultyMode difficulty)
        {
            return difficulty switch
            {
                DifficultyMode.Easy   => HpEasy,
                DifficultyMode.Normal => HpNormal,
                DifficultyMode.Hard   => HpHard,
                _ => LogAndFallback(nameof(GetHpMultiplier), difficulty, HpNormal)
            };
        }

        /// <summary>
        /// 난이도별 드랍률 배율
        /// Easy=1.5, Normal=1.0, Hard=0.7
        /// </summary>
        public static float GetDropRateMultiplier(DifficultyMode difficulty)
        {
            return difficulty switch
            {
                DifficultyMode.Easy   => DropRateEasy,
                DifficultyMode.Normal => DropRateNormal,
                DifficultyMode.Hard   => DropRateHard,
                _ => LogAndFallback(nameof(GetDropRateMultiplier), difficulty, DropRateNormal)
            };
        }

        /// <summary>
        /// 난이도별 리스폰 속도 배율
        /// Easy=0.7, Normal=1.0, Hard=1.3
        /// </summary>
        public static float GetRespawnRateMultiplier(DifficultyMode difficulty)
        {
            return difficulty switch
            {
                DifficultyMode.Easy   => RespawnRateEasy,
                DifficultyMode.Normal => RespawnRateNormal,
                DifficultyMode.Hard   => RespawnRateHard,
                _ => LogAndFallback(nameof(GetRespawnRateMultiplier), difficulty, RespawnRateNormal)
            };
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────

        /// <summary>
        /// 난이도 열거형에 정의되지 않은 값이 들어오면 경고를 로그하고 기본값을 반환합니다.
        /// </summary>
        private static float LogAndFallback(string methodName, DifficultyMode difficulty, float fallback)
        {
            Debug.LogWarning(
                $"[DifficultyManager] {methodName}: 정의되지 않은 난이도 값 {(int)difficulty} ({difficulty}). " +
                $"기본값 {fallback}을(를) 반환합니다."
            );
            return fallback;
        }
    }
}