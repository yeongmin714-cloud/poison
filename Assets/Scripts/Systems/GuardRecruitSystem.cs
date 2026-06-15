using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-15: 병사 포섭 시스템 (호감도 70+ → 영지로 데려오기)
    /// 
    /// ROADMAP 5.3.7 기반:
    /// - 호감도 70+: 포섭 제안 (확정)
    /// - 호감도 50+ 선물 병행: 50% 확률, 실패 시 -10
    /// - 호감도 0+ 위협: 20% 확률, 실패 시 적대
    /// - 호감도 100: 자동 포섭
    /// </summary>
    public static class GuardRecruitSystem
    {
        public struct RecruitResult
        {
            public bool success;
            public string message;
            public string method; // "normal", "gift", "threat", "auto"
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

            // 자동 포섭
            if (loyalty >= 100f)
                return Success($"{guard.GuardName}이(가) 자진해서 따르겠다고 한다!", "auto");

            // 일반 포섭 (호감도 70+)
            if (loyalty >= 70f)
                return Success($"{guard.GuardName}이(가) 당신의 영지로 오겠다고 한다!", "normal");

            // 선물 병행 포섭 (호감도 50+)
            if (loyalty >= 50f)
            {
                float chance = 0.5f;
                if (Random.value < chance)
                    return Success($"{guard.GuardName}: \"선물이 마음에 들었소. 영지로 가겠소!\"", "gift");
                else
                {
                    guard.Loyalty -= 10f;
                    return Fail($"{guard.GuardName}: \"선물 정도로는 부족하오!\" (호감도 -10)");
                }
            }

            // 위협 포섭 (호감도 0+)
            if (loyalty >= 0f)
            {
                float chance = 0.2f;
                if (Random.value < chance)
                    return Success($"{guard.GuardName}: \"...알겠소. 따르겠소.\" (위협)", "threat");
                else
                {
                    guard.Loyalty = Mathf.Max(0, guard.Loyalty - 20f);
                    return Fail($"{guard.GuardName}: \"네놈 따위에게 굴복할 순 없다!\" (호감도 -20, 적대)");
                }
            }

            // 호감도 0 미만
            return Fail($"{guard.GuardName}: \"접근하지 마라!\" (호감도 너무 낮음)");
        }

        /// <summary>
        /// 포섭 최대 인원 계산 (영지 레벨 × 5)
        /// </summary>
        public static int GetMaxRecruits(int territoryLevel)
        {
            return territoryLevel * 5;
        }

        private static RecruitResult Success(string message, string method)
        {
            return new RecruitResult { success = true, message = message, method = method };
        }

        private static RecruitResult Fail(string message)
        {
            return new RecruitResult { success = false, message = message, method = "fail" };
        }
    }
}