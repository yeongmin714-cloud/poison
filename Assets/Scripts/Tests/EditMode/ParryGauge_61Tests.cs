using NUnit.Framework;
using System.IO;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 61] 패망 고품질 잔여분 계약 — 47계획 §8 P3의 미착지 4항목.
    /// ①창 아크 게이지(UTKGaugeStripArc 배선 — HUDUTK) ②흰 단일 게이지([171차] 등급=강도)
    /// ③카메라 방향성 오프셋(37-H2 소화 — 고정 forward 폐기) ④실패/빈 패링 림 자연 페이드.
    /// 소스계약(핀) 테스트 — 런타임 행위는 Play 게이트(별도).
    /// </summary>
    public static class ParryGauge_61Tests
    {
        private static string Read(string path)
        {
            var full = Path.Combine(UnityEngine.Application.dataPath, path.Replace("Assets/", ""));
            return File.Exists(full) ? File.ReadAllText(full) : "";
        }

        // ── ① 창 아크 게이지 — HUDUTK 배선 ──

        [Test]
        public static void Hud_ParryGauge_IsStripArc_Wired()
        {
            string src = Read("Assets/Scripts/UI/Toolkit/HUDUTK.cs");
            // UTKGaugeStripArc 계약(84차 벡터메시 금지) — UTKCircularGauge 사용 금지.
            Assert.That(src, Does.Contain("private UTKGaugeStripArc _parryGauge"),
                "[61] 패망 게이지 = UTKGaugeStripArc(벡터메시 금지 84차 계약).");
            Assert.That(src, Does.Contain("\"ParryWindowArc\""),
                "[61] 게이지 요소명 핀 — PositionHost 기하 갱신 대상 식별.");
            Assert.That(src, Does.Not.Contain("new UTKCircularGauge"),
                "[61] 벡터메시 게이지 금지(84차) — 인스턴스화 없음(주석 언급은 계약 배경).");
            // 50ms 고빈도 폴링 — 0.45s 창을 250ms 폴링으로는 2틱뿐.
            Assert.That(src, Does.Contain("private const int ParryPollMs = 50"),
                "[61] 창 0.45s 감지 — 50ms 고빈도 폴링.");
            Assert.That(src, Does.Contain("schedule.Execute(UpdateParryGauge).Every(ParryPollMs)"),
                "[61] 패망 폴링 스케줄 배선.");
            Assert.That(src, Does.Contain("if (_parryPollTask != null)"),
                "[61] StopPolling 짝 정리 — 폴링 누수 방지.");
        }

        // ── ② PERFECT→GOOD 색 틱 — FXPalette 단일 소스 ──

        [Test]
        public static void Hud_ParryGauge_Tick_UsesPaletteSources()
        {
            string src = Read("Assets/Scripts/UI/Toolkit/HUDUTK.cs");
            Assert.That(src, Does.Contain("FXPalette.ParryWhite"),
                "[61] 게이지 색 = FXPalette.ParryWhite — 흰 단일([171차] 등급=강도).");
            Assert.That(src, Does.Not.Contain("FXPalette.ParryCyan"),
                "[171차] 청 틱 폐기 — 흰 단일 게이지.");
            Assert.That(src, Does.Contain("perfect != _parryGaugePerfectPhase"),
                "[61] 색 틱 1회 전환 가드 — 틱마다 StyleColor 재할당 낭비 방지.");
        }

        // ── ③ PlayerCombat 창 상태 노출 — HUD 소비 계약 ──

        [Test]
        public static void PlayerCombat_Exposes_ParryWindowState()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("public bool ParryWindowActive"),
                "[61] 창 활성 노출 — HUD 표시 게이트.");
            Assert.That(src, Does.Contain("public float ParryWindowRemaining01"),
                "[61] 잔여 비율 노출 — 게이지 채움.");
            Assert.That(src, Does.Contain("public bool ParryInPerfectPhase"),
                "[61] PERFECT 구간 노출 — 색 틱 판정(ParryPerfectWindow 기준).");
        }

        // ── ③b 37-H2 소화 — 카메라 방향성 오프셋 ──

        [Test]
        public static void PlayerCombat_CameraPunch_IsDirectional_37H2()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("private void TriggerParryCameraPunch(Vector3 impactDir)"),
                "[61/37-H2] 펀치에 타격 방향 인자 — 고정 중립 폐기.");
            Assert.That(src, Does.Contain("TriggerParryCameraPunch(kbDir)"),
                "[61/37-H2] 호출부가 kbDir(공격자→플레이어) 전달.");
            Assert.That(src, Does.Contain("InverseTransformDirection"),
                "[61/37-H2] 월드→카메라 로컬 역변환 — GenerateImpulse 로컬 공간 계약.");
            Assert.That(src, Does.Contain("local.y = 0f"),
                "[61/37-H2] 수평 킥만 — 수직 각도 뒤틀림 방지.");
            Assert.That(src, Does.Not.Contain("_impulseSource.GenerateImpulse(Vector3.forward * ParryCameraShakeImpulse)"),
                "[61/37-H2] 고정 forward impulse 폐기 — 방향성 킥으로 대체.");
        }

        // ── ④ 실패/빈 패링 림 자연 페이드 ──

        [Test]
        public static void PlayerCombat_EndParryWindow_FadesRim()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("private System.Collections.IEnumerator ParryRimFadeRoutine(GameObject rim)"),
                "[61] 림 페이드 코루틴 — 즉시 파괴 대신 강도 수렴.");
            Assert.That(src, Does.Contain("StartCoroutine(ParryRimFadeRoutine(_parryRimFadeTarget))"),
                "[61] 만료 경로(EndParryWindow)가 페이드 시작.");
            Assert.That(src, Does.Contain("FxRimOverlay.Release(_parryRimFadeTarget)"),
                "[61] 재패링/비활성화 시 구 림 즉시 해제 — 이중 림 방지.");
            Assert.That(src, Does.Contain("Time.unscaledDeltaTime"),
                "[61] 페이드 = unscaled 시간(패망 창 unscaledTime 계약 — 95차 F1).");
            // 성공 경로는 림 선행 해제 유지(28차 플래시 림 이어받기 — 이중 림 방지 계약).
            Assert.That(src, Does.Contain("ReleaseParryGuardRim();   // [무번호 36-PW1] 성공 — 방어 림 즉시 해제"),
                "[61] 성공 경로 림 즉시 해제 유지 — 페이드는 만료(실패/빈 패링) 전용.");
        }
    }
}
