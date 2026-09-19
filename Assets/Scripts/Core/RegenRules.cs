using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// C-O4-01: 자연 재생 규칙 (OpenMMO COMBAT.md 벤치마크 — docs/reference/openmmo/COMBAT.md L137-167).
    /// - 재생 주기: 16초
    /// - 재생량: max(1, 1 + floor(Lv/5) + conMod), conMod = (CON/VIT 점수 - 10) / 2
    /// - 재생 조건: 생존 + 만전 미만 + 마지막 피격 10초(전투 그레이스) 경과
    /// 순수 계산 규칙(정적) — Systems 의존 없음, foreach 불필요.
    /// </summary>
    public static class RegenRules
    {
        /// <summary>재생 주기(초) — 벤치마크 16초</summary>
        public const float CycleSeconds = 16f;

        /// <summary>전투 그레이스(초) — 마지막 피격 후 이 시간이 지나야 재생 시작. 벤치마크 10초</summary>
        public const float CombatGraceSeconds = 10f;

        /// <summary>
        /// 틱당 자연 재생량 = max(1, 1 + floor(level/5) + conMod).
        /// conMod = (conOrVitScore - 10) / 2.
        ///
        /// ⚠️ 정수 나눗셈 0-절사 주의: C# int 나눗셈은 floor가 아니라 0-쪽 절사(truncation toward zero)다.
        ///   예: (score-10)/2 에서 score=9 → (-1)/2 = 0 (실수 floor라면 -1).
        ///   즉 음수 홀수 차이에서 -1이 -0이 되어 Mathf.FloorToInt((score-10)/2f)와 결과가 달라진다.
        ///   의도적으로 C# int 나눗셈을 그대로 사용하며, 최소 보장(max 1) 덕분에
        ///   절사로 값이 1 낮게 나와도 게임 밸런스상 안전하다.
        /// level/conOrVitScore가 0/음수여도 max(1, ...)로 최소 1 보장(방어).
        /// </summary>
        public static int ComputeAmount(int level, int conOrVitScore)
        {
            int levelTerm = level / 5;                    // 0-절사: Lv1~4 → 0, Lv5~9 → 1, ...
            int conMod = (conOrVitScore - 10) / 2;        // 0-절사 (위 주의 참조)
            return Mathf.Max(1, 1 + levelTerm + conMod);
        }

        /// <summary>
        /// 자연 재생 가능 여부: 생존 && 만전 미만 && 마지막 피격 후 그레이스(10초) 경과.
        /// secondsSinceDamage == CombatGraceSeconds 경계에서는 true(>= 판정).
        /// </summary>
        public static bool CanRegen(bool alive, bool belowMax, float secondsSinceDamage)
        {
            return alive && belowMax && secondsSinceDamage >= CombatGraceSeconds;
        }
    }
}
