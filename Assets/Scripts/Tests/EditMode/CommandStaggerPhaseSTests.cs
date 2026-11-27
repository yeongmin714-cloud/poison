using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 28-S3] 그룹 이동 명령 스태거 계약 — Source 잠금.
    /// RTSCommandSystem.IssueMoveCommandCore의 유닛별 0.04s 지연 발화(세대 가드 지연 코루틴),
    /// 동기화 계열 무손상(IssueSynchronized*에 스태거 심볼 부재), isActiveAndEnabled 폴백,
    /// 결정론(UnityEngine.Random 0건)을 소스 문자열로 검증한다. 실행 판정은 QA 서브에이전트 몫.
    /// </summary>
    public class CommandStaggerPhaseSTests
    {
        private const string RtsPath = "Assets/Scripts/Systems/RTSCommandSystem.cs";

        [Test]
        public void T1_Source_StaggerGen_Field_And_GenerationGuard()
        {
            string src = System.IO.File.ReadAllText(RtsPath);
            Assert.That(src, Does.Contain("private int _moveStaggerGeneration;"), "스태거 세대 가드 필드.");
            Assert.That(src.IndexOf("if (generation != _moveStaggerGeneration) yield break;", System.StringComparison.Ordinal),
                Is.GreaterThan(-1), "세대 불일치 → 지연분 폐기 가드.");
        }

        [Test]
        public void T2_Source_DelayedRoutine_Definition_And_DelayedTargetCall()
        {
            string src = System.IO.File.ReadAllText(RtsPath);
            Assert.That(src, Does.Contain("private System.Collections.IEnumerator DelayedMoveCommand"), "지연 코루틴 정의.");
            Assert.That(src, Does.Contain("WaitForSeconds(delay)"), "인덱스 기반 결정론 지연 대기.");
            Assert.That(src, Does.Contain("guard.SetCommandTarget(position, false);"), "지연 후 이동 발화(attack=false).");
        }

        [Test]
        public void T3_Source_Interval_Is_Planned_0_04()
        {
            string src = System.IO.File.ReadAllText(RtsPath);
            Assert.That(src, Does.Contain("private const float StaggerStepSeconds = 0.04f;"), "계획서 확정값 0.04s 간격.");
            Assert.That(src, Does.Contain("i * StaggerStepSeconds"), "유닛 인덱스 × 간격 지연 계산.");
        }

        [Test]
        public void T4_Source_SynchronizedCommands_Free_Of_Stagger_Symbols()
        {
            string src = System.IO.File.ReadAllText(RtsPath);
            string attackBlock = ExtractBlock(src,
                "private void IssueSynchronizedAttackCommand", "private void IssueMoveCommand(");
            string moveBlock = ExtractBlock(src,
                "private void IssueSynchronizedMoveCommand", "private IReadOnlyList<GuardPlaceholder> GetFallbackGuards()");

            foreach (var block in new[] { attackBlock, moveBlock })
            {
                Assert.That(block, Does.Not.Contain("_moveStaggerGeneration"), "동기화 계열 — 세대 가드 심볼 부재.");
                Assert.That(block, Does.Not.Contain("DelayedMoveCommand"), "동기화 계열 — 지연 코루틴 부재.");
                Assert.That(block, Does.Not.Contain("StaggerStepSeconds"), "동기화 계열 — 스태거 간격 부재.");
                Assert.That(block, Does.Not.Contain("WaitForSeconds"), "동기화 계열 — 지연 대기 부재(동시 출발 목적 유지).");
            }
        }

        [Test]
        public void T5_Source_Fallback_When_Not_Active_And_Enabled()
        {
            string src = System.IO.File.ReadAllText(RtsPath);
            int fallbackIdx = src.IndexOf("if (!isActiveAndEnabled)", System.StringComparison.Ordinal);
            int syncCallIdx = src.IndexOf("aliveGuards[i].SetCommandTarget(spread[i], false);", System.StringComparison.Ordinal);
            Assert.That(fallbackIdx, Is.GreaterThan(-1), "비활성 폴백 분기 존재.");
            Assert.That(syncCallIdx, Is.GreaterThan(fallbackIdx),
                "폴백 분기 이후 기존 동기 발화 경로 유지(코루틴 불가 상황에서 기존 동작 등가).");
        }

        [Test]
        public void T6_Source_Marker_And_No_Random()
        {
            string src = System.IO.File.ReadAllText(RtsPath);
            Assert.That(src, Does.Contain("[무번호 28-S3]"), "작업 마커 존재.");
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "신규 코드 Random 부재(결정론).");
        }

        private static string ExtractBlock(string src, string startMarker, string endMarker)
        {
            int start = src.IndexOf(startMarker, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(-1), $"{startMarker} 존재 전제.");
            int end = src.IndexOf(endMarker, start + 1, System.StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), $"{endMarker} 경계 존재 전제.");
            return src.Substring(start, end - start);
        }
    }
}
