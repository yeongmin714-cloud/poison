using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-11 이동 끊김 수리] 병사 걷기 애니 연속성 계약.
    /// 실측 원인 2건: ①RigAnimationController.SetState 매 프레임 크로스페이드 재시작 루프
    /// ②HumanoidClipDriver.UpdateSoldier raw 속도(y 포함·무평활) 블렌드 스래싱.
    /// </summary>
    public class MoveAnimationContinuityTests
    {
        [Test]
        public void SetState_IsIdempotentForSameStateEvenWhileTransitioning()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/RigAnimationController.cs");
            Assert.That(src, Does.Contain("_transitionTargetState"),
                "전환 목표 상태 추적 필드.");
            Assert.That(src, Does.Contain("_transitionTargetState == newState"),
                "동일 상태 재호출 멱등 가드(IsTransitioning 중에도 재시작 금지).");
            Assert.That(src.IndexOf("_transitionTargetState = newState;", System.StringComparison.Ordinal),
                Is.GreaterThan(src.IndexOf("StopCoroutine(_transitionCoroutine);", System.StringComparison.Ordinal)),
                "코루틴 시작 전에 목표 상태 기록(정합 배선).");
        }

        [Test]
        public void UpdateSoldier_MeasuresXzOnly_AndSmoothsSpeed()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(src, Does.Contain("delta.y = 0f;   // 지형 y스냅 급변 제외"),
                "Soldier 속도는 XZ 수평 전용(y 노이즈 제외).");
            Assert.That(src, Does.Contain("1f - Mathf.Exp(-10f * dt)"),
                "Player 모드와 동일 지수 평활 계수.");
            Assert.That(src, Does.Contain("_soldierStallTime >= 0.25f ? 0f : _soldierLastTarget"),
                "미세 정체 홀드(스톨 스냅).");
            Assert.That(src, Does.Not.Contain("_anim.SetFloat(\"Speed\", speed);"),
                "raw 속도 직대입 잔존 금지(평활값 대입으로 교체).");
        }
    }
}
