using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 77] 크리 2차 리플 에코 스케줄러 계약 테스트 — FxSecondaryRipple 신규 + CombatFXGate 크리 전용 1줄 배선.
    /// 소스 문자열 계약 방식(FxCombatResidueP71Tests 스타일). 부정 잠금은 코드형 토큰만 사용
    /// (단어형 금지 — 자기 주석 오염 주의). 계획서: docs/plans/2026-11-18_no77-crit-secondary-ripple-plan.md.
    /// </summary>
    public class FxSecondaryRippleP77Tests
    {
        private static string Read(string path)
            => System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        private static int Count(string src, string token)
            => System.Text.RegularExpressions.Regex.Matches(src, System.Text.RegularExpressions.Regex.Escape(token)).Count;

        // ── 스케줄러 계약 — Schedule 시그니처 + EditMode 가드 + 결정론 쿨다운 + Host ──
        [Test]
        public void Source_Scheduler_Contract()
        {
            string src = Read("Assets/Scripts/Systems/FxSecondaryRipple.cs");
            Assert.That(src, Does.Contain("[무번호 77]"), "[77] 마커.");
            Assert.That(src, Does.Contain("public static void Schedule(Vector3 position, float scale, float delaySec)"), "[77] Schedule static 진입점.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return;"), "[77] EditMode 무동작 가드(Burst 내부 가드와 이중 안전).");
            // 동일 지점 중복 스케줄 가드 — 정적 마지막 스케줄 시각 + 쿨다운 0.12s(Time.time 기반 — 결정론).
            Assert.That(src, Does.Contain("private const float CooldownSec = 0.12f;"), "[77] 쿨다운 0.12s.");
            Assert.That(src, Does.Contain("private static float _lastScheduleTime"), "[77] 마지막 스케줄 시각 — 정적 필드.");
            Assert.That(src, Does.Contain("if (Time.time - _lastScheduleTime < CooldownSec) return;"), "[77] 쿨다운 가드(결정론).");
            // Host — HideAndDontSave + 러너 Init 위임(71-L2 호스트 선례).
            Assert.That(src, Does.Contain("new GameObject(\"SecondaryRipple\")"), "[77] 호스트 GameObject 명명.");
            Assert.That(src, Does.Contain("HideFlags.HideAndDontSave"), "[77] HideAndDontSave.");
            Assert.That(src, Does.Contain("Init(position, scale, delaySec)"), "[77] 러너 Init 위임.");
        }

        // ── 러너 — unscaled 지연(23-X1) + Burst 재사용 + 자가 파괴(21-K1) + OnDestroy 안전망 ──
        [Test]
        public void Source_Runner_UnscaledDelay_BurstReuse_SelfDestruct()
        {
            string src = Read("Assets/Scripts/Systems/FxSecondaryRipple.cs");
            Assert.That(src, Does.Contain("using System.Collections;"), "[77] IEnumerator 코루틴 using.");
            Assert.That(src, Does.Contain("private IEnumerator Run(Vector3 position, float scale, float delaySec)"), "[77] IEnumerator 코루틴 러너.");
            Assert.That(src, Does.Contain("yield return new WaitForSecondsRealtime(delaySec);"), "[77] WaitForSecondsRealtime — unscaled 규약(23-X1, 히트스톱 중에도 진행).");
            Assert.That(src, Does.Contain("FxDistortionWave.Burst(position, scale);"), "[77] Burst 재사용 호출 — FxDistortionWave 무수정 경로.");
            Assert.That(src, Does.Contain("if (Application.isPlaying) Object.Destroy(gameObject);"), "[77] 자가 파괴(Play) — 유한 수명(누출 금지).");
            Assert.That(src, Does.Contain("else Object.DestroyImmediate(gameObject);"), "[77] 자가 파괴 분기(21-K1 선례).");
            Assert.That(src, Does.Contain("private void OnDestroy()"), "[77] OnDestroy 안전망.");
            // 부정 잠금 — 코드형 토큰만(결정론).
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[77] 결정론 — 난수 경로 0건.");
            Assert.That(src, Does.Not.Contain("Rigidbody"), "[77] 물리 경로 0건.");
            // 예산 — Burst 내부(동시 상한 4) 단일 소유 — 별도 카운터 신설 금지(초과 조용 스킵 승계).
            Assert.That(src, Does.Not.Contain("static int"), "[77] 신규 카운터 0건.");
            Assert.That(src, Does.Not.Contain("_activeCount"), "[77] Burst 내부 카운터 무참조.");
        }

        // ── 게이트 배선 — 크리 전용 1줄 + 1차 웨이브 직후 배치 + 기존 문자열 카운트 무변경 ──
        [Test]
        public void Source_Gate_CritOnlyWiring_ExistingCountsUnchanged()
        {
            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            Assert.That(gate, Does.Contain("[무번호 77]"), "[77] 배선부 마커.");
            Assert.That(gate, Does.Contain("if (isCrit) FxSecondaryRipple.Schedule(position, 0.55f, 0.12f);   // [무번호 77] 크리 2차 리플 — 1차 웨이브 잔광"), "[77] 크리 전용 1줄 배선(계획서 그대로).");
            // 크리 전용 게이트 — 일반 타격 미발화(if (isCrit) 접두 — 76-S3와 다른 밴드).
            Assert.That(gate, Does.Contain("if (isCrit) FxSecondaryRipple.Schedule"), "[77] 크리 전용 게이트 — if (isCrit) 접두.");
            Assert.That(Count(gate, "FxSecondaryRipple.Schedule"), Is.EqualTo(1), "[77] 발화처 1곳 한정(Internal 단일 통로).");
            // 배치 계약 — 웨이브(1차) 직후 리플(2차).
            int wave = gate.IndexOf("FxDistortionWave.Burst(position, isCrit ? 1.5f : 1f);", System.StringComparison.Ordinal);
            int ripple = gate.IndexOf("FxSecondaryRipple.Schedule", System.StringComparison.Ordinal);
            Assert.That(wave, Is.GreaterThanOrEqualTo(0), "[77] 1차 웨이브 호출부 존재.");
            Assert.That(ripple, Is.GreaterThan(wave), "[77] 2차 리플은 1차 웨이브 직후(계획서 배치 계약).");
            // 기존 문자열 카운트 무변경 — 최소 diff(1줄 삽입 외 접촉 0).
            Assert.That(Count(gate, "FxDistortionWave.Burst(position, isCrit ? 1.5f : 1f);"), Is.EqualTo(1), "[77] [71-L2] 호출부 카운트 무변경.");
            Assert.That(Count(gate, "FxSlashFlipbookFlash.Play(position, isCrit ? 1.5f : 1f, isCrit);"), Is.EqualTo(1), "[77] [37-S3]/[70-U3] 호출부 카운트 무변경.");
            Assert.That(Count(gate, "FxImpactLight.Spike(position, FXPalette.AttackPrimary, isCrit ? 4.2f : 2.2f, 3.8f, 0.18f);"), Is.EqualTo(1), "[77 갱신 79-R20] [33-A1] 라이트 카운트 무변경(통일색).");
            Assert.That(Count(gate, "if (isCrit || damage >= 15f) FxGroundCrackFX.Burst(position);"), Is.EqualTo(1), "[77] [37-H3] 훅 카운트 무변경.");
            // [무번호 79 사용자 결정 진화] 시트 스파크 게이트 배선 해제(11-19 사용자 판정) — 활성 호출 0건.
            Assert.That(Count(gate, "if (isCrit || damage >= 15f) FxImpactSheetSpark.Burst(position, isCrit);"), Is.EqualTo(0),
                "[79 진화] 시트 스파크 게이트 배선 해제(주석 잔코드 ≠ 활성 배선).");
            Assert.That(Count(gate, "if (isCrit || damage >= 15f) FxImpactDustColumn.Spawn(position);"), Is.EqualTo(1), "[77] [76-S3] 먼지 컬럼 훅 카운트 무변경.");
            Assert.That(Count(gate, "[무번호 37-S3]"), Is.EqualTo(1), "[77] [37-S3] 마커 카운트 무변경.");
            Assert.That(Count(gate, "[무번호 71-L2]"), Is.EqualTo(1), "[77] [71-L2] 마커 카운트 무변경.");
            Assert.That(Count(gate, "[무번호 76-S3]"), Is.EqualTo(1), "[77] [76-S3] 마커 카운트 무변경.");
            Assert.That(Count(gate, "[무번호 77]"), Is.EqualTo(1), "[77] 신규 마커 1건(중복 주석 금지).");
        }

        // ── 71-L2 잠금 무손상 — FxDistortionWave 카운트 보존(Burst 진입점·가드·리플렉션) + 무수정 ──
        [Test]
        public void Source_DistortionWave_Locked_CountsPreserved()
        {
            string wave = Read("Assets/Scripts/Systems/FxDistortionWave.cs");
            // Burst 진입점 — 시그니처 카운트 보존.
            Assert.That(Count(wave, "public static void Burst(Vector3 position, float scale)"), Is.EqualTo(1), "[77] Burst 진입점 카운트 보존.");
            // 가드 — EditMode/예산/opaque 스킵 카운트 보존.
            Assert.That(Count(wave, "if (!Application.isPlaying) return;"), Is.EqualTo(1), "[77] EditMode 가드 카운트 보존.");
            Assert.That(Count(wave, "if (_activeCount >= MaxConcurrentWaves) return;"), Is.EqualTo(1), "[77] 예산 가드 카운트 보존(동시 상한 4 공유 — 재사용 경로).");
            Assert.That(Count(wave, "if (!OpaqueTextureSupported()) return;"), Is.EqualTo(1), "[77] 미지원 스킵 가드 카운트 보존.");
            // 리플렉션 — opaque 지원 판정 카운트 보존(fail-closed — 블랙 링 차단).
            Assert.That(Count(wave, "type.GetProperty(\"supportsCameraOpaqueTexture\")"), Is.EqualTo(1), "[77] 리플렉션 가드 — URP 6000.x 속성명 카운트 보존.");
            Assert.That(Count(wave, "type.GetProperty(\"supportsOpaqueTexture\")"), Is.EqualTo(1), "[77] 리플렉션 가드 — 구 URP 폴백 카운트 보존.");
            Assert.That(Count(wave, "catch (System.Exception) { _opaqueSupported = false; }"), Is.EqualTo(1), "[77] fail-closed 카운트 보존.");
            Assert.That(Count(wave, "[무번호 71-L2]"), Is.EqualTo(2), "[77] 71-L2 마커 카운트 보존.");
            // 무수정 간접 확인 — 77 마커 부재 + 결정론 계약 보존.
            Assert.That(wave, Does.Not.Contain("[무번호 77"), "[77] FxDistortionWave 무수정 — 77 마커 부재.");
            Assert.That(wave, Does.Not.Contain("UnityEngine.Random"), "[77] 결정론 계약 보존 — 난수 0건.");
        }
    }
}
