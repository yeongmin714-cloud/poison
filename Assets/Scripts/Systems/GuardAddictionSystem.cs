using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-12: 병사 약물 중독 시스템 (1~100% + 효과)
    /// 
    /// 중독도에 따라 병사의 상태, 전투력, 행동에 영향을 줍니다.
    /// 시간 경과 시 중독도가 서서히 감소하고, 100% 초과 시 사망합니다.
    /// </summary>
    public static class GuardAddictionSystem
    {
        // 중독 단계 경계값
        public const float STAGE_0_MAX = 20f;    // 영향 없음
        public const float STAGE_1_MAX = 40f;    // 가벼운 의존
        public const float STAGE_2_MAX = 60f;    // 중독
        public const float STAGE_3_MAX = 80f;    // 심각한 중독
        public const float STAGE_4_MAX = 100f;   // 완전 중독
        // 100% 초과 = 사망

        // 중독 단계별 효과
        public const float STAGE_1_LOYALTY_BONUS = 5f;    // 소폭 호감도 증가
        public const float STAGE_3_COMBAT_PENALTY = 0.3f; // 전투력 30% 감소

        // 자연 감소 (1일 단위로 5% 감소, 여기서는 1초 = 1시간 가정)
        public const float DECAY_PER_SECOND = 5f / 86400f; // 5% / 86400초 (하루)

        // 독약 효과
        public const float POISON_DAMAGE_PER_SECOND = 0.5f; // 초당 0.5 HP 데미지
        public const float POISON_ADDICTION_PER_DOSE = 15f;  // 1회 투여 시 중독도 +15
        public const float POISON_ADDICTION_RATIO = 0.5f;    // 독은 마약보다 중독도 증가율 50%
        public const float POISON_LOYALTY_PENALTY = -10f;    // 호감도 -10

        // 해독제 효과
        public const float ANTIDOTE_REDUCTION = 0.5f;   // 중독도 50% 감소

        // 스테이지 4 완전 중독 보너스
        public const float STAGE_4_LOYALTY_BONUS = 20f;  // 절대 복종 (호감도 +20)

        // 최대 중독도 (과다복용 감지를 위해 100 이상 허용)
        public const float MAX_ADDICTION = 999f;

        /// <summary>
        /// 중독 단계 반환 (0~5, 0=정상, 5=과다복용)
        /// </summary>
        public static int GetAddictionStage(float addiction)
        {
            if (addiction <= STAGE_0_MAX) return 0;
            if (addiction <= STAGE_1_MAX) return 1;
            if (addiction <= STAGE_2_MAX) return 2;
            if (addiction <= STAGE_3_MAX) return 3;
            if (addiction <= STAGE_4_MAX) return 4;
            return 5; // 과다복용
        }

        /// <summary>
        /// 중독 단계 이름 반환
        /// </summary>
        public static string GetStageName(int stage)
        {
            switch (stage)
            {
                case 0: return "정상";
                case 1: return "가벼운 의존";
                case 2: return "중독";
                case 3: return "심각한 중독";
                case 4: return "완전 중독";
                case 5: return "과다복용 ⚠️";
                default: return "알 수 없음";
            }
        }

        /// <summary>
        /// 중독 단계별 전투력 계수 (1.0 = 정상)
        /// </summary>
        public static float GetCombatMultiplier(float addiction)
        {
            int stage = GetAddictionStage(addiction);
            switch (stage)
            {
                case 0: return 1.0f;
                case 1: return 1.0f;  // 영향 없음
                case 2: return 0.9f;  // 약간 감소
                case 3: return 0.7f;  // 30% 감소
                case 4: return 0.4f;  // 60% 감소
                case 5: return 0.0f;  // 사망
                default: return 1.0f;
            }
        }

        /// <summary>
        /// 중독 효과 설명 반환
        /// </summary>
        public static string GetEffectDescription(float addiction)
        {
            int stage = GetAddictionStage(addiction);
            switch (stage)
            {
                case 0: return "영향 없음";
                case 1: return "가벼운 의존, 호감도 소폭 증가";
                case 2: return "중독, 가끔 실수, 정보 누설 위험";
                case 3: return "심각한 중독, 전투력 30% 감소";
                case 4: return "완전 중독, 절대 복종";
                case 5: return "⚠️ 과다복용 — 사망 위험";
                default: return "";
            }
        }

        /// <summary>
        /// 중독도 시간 감소 처리 (매 프레임 호출)
        /// </summary>
        public static void ProcessDecay(GuardPlaceholder guard, float deltaTime)
        {
            if (guard == null || guard.Addiction <= 0) return;
            float decay = DECAY_PER_SECOND * deltaTime;
            guard.Addiction = Mathf.Max(0, guard.Addiction - decay);
        }

        /// <summary>
        /// 과다복용 체크 (100% 초과 시 사망)
        /// </summary>
        public static bool CheckOverdose(GuardPlaceholder guard)
        {
            if (guard == null || !guard.IsAlive) return false;
            if (guard.Addiction > STAGE_4_MAX)
            {
                Debug.Log($"[GuardAddiction] {guard.GuardName} 과다복용 사망! 중독도: {guard.Addiction}");
                guard.TakeDamage(9999f, Vector3.zero, "Overdose");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 독약 투여 효과 적용
        /// </summary>
        public static void ApplyPoison(GuardPlaceholder guard)
        {
            if (guard == null || !guard.IsAlive) return;
            float addictionIncrease = POISON_ADDICTION_PER_DOSE * POISON_ADDICTION_RATIO;
            guard.Addiction += addictionIncrease;
            guard.Loyalty += POISON_LOYALTY_PENALTY;
            Debug.Log($"[GuardAddiction] {guard.GuardName} 독약 투여! 중독도 +{addictionIncrease:F1}, 호감도 {POISON_LOYALTY_PENALTY}");
            CheckOverdose(guard);
        }

        /// <summary>
        /// 중독 지속 데미지 처리 (매 프레임 호출)
        /// </summary>
        public static void ProcessPoisonDamage(GuardPlaceholder guard, float deltaTime)
        {
            if (guard == null || guard.Addiction <= 0 || !guard.IsAlive) return;
            // 중독도가 높을수록 데미지 증가
            float damageMultiplier = guard.Addiction / 100f;
            float damage = POISON_DAMAGE_PER_SECOND * damageMultiplier * deltaTime;
            if (damage > 0.01f)
            {
                guard.TakeDamage(damage, Vector3.zero, "Poison");
            }
        }

        /// <summary>
        /// 해독제 사용 — 중독도 50% 감소
        /// </summary>
        public static void ApplyAntidote(GuardPlaceholder guard)
        {
            if (guard == null) return;
            float before = guard.Addiction;
            guard.Addiction *= (1f - ANTIDOTE_REDUCTION);
            Debug.Log($"[GuardAddiction] {guard.GuardName} 해독제 사용! 중독도 {before:F1} → {guard.Addiction:F1}");
        }

        /// <summary>
        /// 호감도 보정 (가벼운 의존 단계에서 소폭 증가)
        /// </summary>
        public static float GetLoyaltyBonusFromAddiction(float addiction)
        {
            int stage = GetAddictionStage(addiction);
            if (stage == 1) return STAGE_1_LOYALTY_BONUS; // 가벼운 의존 → 호감도 +5
            if (stage == 4) return STAGE_4_LOYALTY_BONUS; // 완전 중독 → 절대 복종 (호감도 +20)
            return 0f;
        }

        /// <summary>
        /// 중독도 기반 행동 확률 (정보 누설, 실수 등)
        /// </summary>
        public static float GetBehaviorErrorChance(float addiction)
        {
            int stage = GetAddictionStage(addiction);
            switch (stage)
            {
                case 0: return 0f;
                case 1: return 0.05f;  // 5%
                case 2: return 0.15f;  // 15%
                case 3: return 0.30f;  // 30%
                case 4: return 0.50f;  // 50%
                case 5: return 1.0f;   // 100%
                default: return 0f;
            }
        }
    }
}