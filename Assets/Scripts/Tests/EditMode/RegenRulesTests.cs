using NUnit.Framework;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O4-01: RegenRules 정적 로직 테스트 (OpenMMO COMBAT.md 벤치마크 — docs/reference/openmmo/COMBAT.md L137-167).
    /// 재생량 공식 표, 0/음수 방어, C# 정수 나눗셈 0-절사 특성, CanRegen 3조건 조합, 상수 검증.
    /// </summary>
    public class RegenRulesTests
    {
        // ── ComputeAmount: 재생량 표 ─────────────────────────────────────

        [Test]
        public void ComputeAmount_Lv1_Con10_ReturnsMinimum1()
        {
            // 1 + floor(1/5)=0 + (10-10)/2=0 = 1 (최솟값)
            Assert.AreEqual(1, RegenRules.ComputeAmount(1, 10));
        }

        [Test]
        public void ComputeAmount_Lv6_Con12_Returns3_BenchmarkExample()
        {
            // 벤치마크 예시: 1 + floor(6/5)=1 + (12-10)/2=1 = 3
            Assert.AreEqual(3, RegenRules.ComputeAmount(6, 12));
        }

        [Test]
        public void ComputeAmount_Lv25_Con16_Returns9()
        {
            // 1 + floor(25/5)=5 + (16-10)/2=3 = 9
            Assert.AreEqual(9, RegenRules.ComputeAmount(25, 16));
        }

        [Test]
        public void ComputeAmount_Lv50_Con8_NegativeConMod()
        {
            // 1 + floor(50/5)=10 + (8-10)/2=-1 = 10 (음수 conMod 반영)
            Assert.AreEqual(10, RegenRules.ComputeAmount(50, 8));
        }

        [Test]
        public void ComputeAmount_ZeroOrNegativeInputs_ClampedToMinimum1()
        {
            // 0/음수 방어: max(1, ...) 보장
            Assert.AreEqual(1, RegenRules.ComputeAmount(0, 10));    // 1+0+0=1
            Assert.AreEqual(1, RegenRules.ComputeAmount(-5, 1));    // 1-1-4=-4 → clamp 1
            Assert.AreEqual(1, RegenRules.ComputeAmount(1, 0));     // 1+0-5=-4 → clamp 1
            Assert.AreEqual(1, RegenRules.ComputeAmount(-10, -10)); // 극단 음수 → clamp 1
        }

        [Test]
        public void ComputeAmount_OddNegativeConMod_TruncatesTowardZero()
        {
            // C# 정수 나눗셈 0-절사 특성 문서화: (9-10)/2 = -1/2 = 0
            // (실수 floor라면 -1이 되지만 C# int 나눗셈은 0-쪽 절사 → -1이 -0이 됨)
            Assert.AreEqual(0, (9 - 10) / 2);
            Assert.AreEqual(1, RegenRules.ComputeAmount(1, 9)); // 1 + 0 + 0 = 1
            // 짝수 음수는 정확히 나누어 떨어지므로 floor와 동일
            Assert.AreEqual(-1, (8 - 10) / 2);
        }

        // ── CanRegen: 3조건 조합 ─────────────────────────────────────────

        [Test]
        public void CanRegen_AliveBelowMaxAfterGrace_ReturnsTrue()
        {
            Assert.IsTrue(RegenRules.CanRegen(true, true, 10f));
            Assert.IsTrue(RegenRules.CanRegen(true, true, 60f));
        }

        [Test]
        public void CanRegen_Dead_ReturnsFalse_EvenIfOtherConditionsMet()
        {
            Assert.IsFalse(RegenRules.CanRegen(false, true, 10f));
            Assert.IsFalse(RegenRules.CanRegen(false, true, 999f));
        }

        [Test]
        public void CanRegen_AtMaxHealth_ReturnsFalse_EvenIfOtherConditionsMet()
        {
            Assert.IsFalse(RegenRules.CanRegen(true, false, 10f));
            Assert.IsFalse(RegenRules.CanRegen(true, false, 999f));
        }

        [Test]
        public void CanRegen_JustBeforeGrace_ReturnsFalse()
        {
            // 마지막 피격 9.9초 — 그레이스(10초) 미달 → 재생 불가
            Assert.IsFalse(RegenRules.CanRegen(true, true, 9.9f));
        }

        [Test]
        public void CanRegen_ExactGraceBoundary_ReturnsTrue()
        {
            // 경계: 정확히 10초(>= 판정) → 재생 가능
            Assert.IsTrue(RegenRules.CanRegen(true, true, RegenRules.CombatGraceSeconds));
        }

        // ── 상수 검증 ────────────────────────────────────────────────────

        [Test]
        public void Constants_MatchBenchmark()
        {
            // 벤치마크: 주기 16초, 전투 그레이스 10초
            Assert.AreEqual(16f, RegenRules.CycleSeconds);
            Assert.AreEqual(10f, RegenRules.CombatGraceSeconds);
        }
    }
}
