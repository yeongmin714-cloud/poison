using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-15: 병사 포섭 시스템 (호감도 70+ → 영지로 데려오기)
    /// 
    /// ROADMAP 5.3.7 기반:
    /// - 호감도 100: 자동 포섭
    /// - 호감도 70+: 포섭 제안 (확정)
    /// - 호감도 50+ 선물 병행: 50% 확률, 실패 시 -10
    /// - 호감도 0+ 위협: 20% 확률, 실패 시 적대 (호감도 -20)
    /// </summary>
    public static class GuardRecruitSystem
    {
        // ===== 포섭 임계값 =====
        private const float LOYALTY_AUTO = 100f;
        private const float LOYALTY_NORMAL = 70f;
        private const float LOYALTY_GIFT = 50f;
        private const float LOYALTY_THREAT = 0f;

        // ===== 확률 =====
        private const float GIFT_CHANCE = 0.5f;
        private const float THREAT_CHANCE = 0.2f;

        // ===== 실패 시 호감도 감소량 =====
        private const float GIFT_FAIL_PENALTY = 10f;
        private const float THREAT_FAIL_PENALTY = 20f;

        // ===== 최대 포섭 인원 계수 =====
        private const int RECRUITS_PER_LEVEL = 5;
        private const int MIN_TERRITORY_LEVEL = 1;

        /// <summary>
        /// 포섭 결과
        /// </summary>
        /// <param name="success">성공 여부</param>
        /// <param name="message">결과 메시지</param>
        /// <param name="method">포섭 방식: "auto", "normal", "gift", "threat", "fail"</param>
        public readonly struct RecruitResult
        {
            public readonly bool success;
            public readonly string message;
            public readonly string method;

            public RecruitResult(bool success, string message, string method)
            {
                this.success = success;
                this.message = message;
                this.method = method;
            }
        }

        /// <summary>
        /// 포섭 시도
        /// </summary>
        public static RecruitResult AttemptRecruit(GuardPlaceholder guard)
        {
            if (guard == null)
                return Fail("대상이 없습니다.");

            if (!guard.IsAlive)
                return Fail("죽은 병사는 포섭할 수 없습니다.");

            float loyalty = guard.Loyalty;

            // 자동 포섭 (호감도 100)
            if (loyalty >= LOYALTY_AUTO)
                return Success($"{guard.GuardName}이(가) 자진해서 따르겠다고 한다!", "auto");

            // 일반 포섭 (호감도 70+)
            if (loyalty >= LOYALTY_NORMAL)
                return Success($"{guard.GuardName}이(가) 당신의 영지로 오겠다고 한다!", "normal");

            // 선물 병행 포섭 (호감도 50+)
            if (loyalty >= LOYALTY_GIFT)
            {
                if (Random.value < GIFT_CHANCE)
                    return Success($"{guard.GuardName}: \"선물이 마음에 들었소. 영지로 가겠소!\"", "gift");

                guard.Loyalty -= GIFT_FAIL_PENALTY;
                return Fail($"{guard.GuardName}: \"선물 정도로는 부족하오!\" (호감도 -{(int)GIFT_FAIL_PENALTY})");
            }

            // 위협 포섭 (호감도 0+)
            if (loyalty >= LOYALTY_THREAT)
            {
                if (Random.value < THREAT_CHANCE)
                    return Success($"{guard.GuardName}: \"...알겠소. 따르겠소.\" (위협)", "threat");

                guard.Loyalty -= THREAT_FAIL_PENALTY;
                return Fail($"{guard.GuardName}: \"네놈 따위에게 굴복할 순 없다!\" (호감도 -{(int)THREAT_FAIL_PENALTY}, 적대)");
            }

            // 호감도 0 미만
            return Fail($"{guard.GuardName}: \"접근하지 마라!\" (호감도 너무 낮음)");
        }

        /// <summary>
        /// 포섭 최대 인원 계산 (영지 레벨 × 5)
        /// </summary>
        public static int GetMaxRecruits(int territoryLevel)
        {
            return Mathf.Max(MIN_TERRITORY_LEVEL, territoryLevel) * RECRUITS_PER_LEVEL;
        }

        private static RecruitResult Success(string message, string method)
        {
            return new RecruitResult(true, message, method);
        }

        private static RecruitResult Fail(string message)
        {
            return new RecruitResult(false, message, "fail");
        }
    }
}