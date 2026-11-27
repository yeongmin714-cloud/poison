using NUnit.Framework;
using System.IO;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [193차] 폭탄 투척 플루언트 프리뷰 계약 — 소스 문자열 어서션(FxCleanup193Tests 관례).
    /// </summary>
    public class BombFluidPreview193Tests
    {
        private const string ControllerPath =
            "Assets/Scripts/Systems/BombArmController.cs";

        private string Src()
        {
            return System.IO.File.ReadAllText(ControllerPath);
        }

        [Test]
        public void Arc_Has_Gradient_Smooth_To_Fade()
        {
            // [193차] 호 그라디언트 — 손(선명) → 착지(페이드)
            Assert.That(Src(), Does.Contain("startColor = AimPreviewGold"),
                "[193] 호 시작 색 = 선명 골드");
            Assert.That(Src(), Does.Contain("new Color(AimPreviewGold.r, AimPreviewGold.g, AimPreviewGold.b, 0.15f)"),
                "[193] 호 끝 색 = 알파 0.15 페이드 — 비행 방향 흐름 계약");
        }

        [Test]
        public void Arc_Has_Flowing_Dots()
        {
            // [193차] 도트 흐름 — 3개 프리미티브, 시간 기반 위상
            Assert.That(Src(), Does.Contain("EnsureAimDots()"),
                "[193] 흐름 도트 생성 호출");
            Assert.That(Src(), Does.Contain("private const int AimDotCount = 3"),
                "[193] 도트 수 = 3");
            Assert.That(Src(), Does.Contain("float flight = Time.unscaledTime * AimDotFlowSpeed"),
                "[193] 도트 흐름 = unscaledTime 기반 위상");
            Assert.That(Src(), Does.Contain("Mathf.Repeat(flight + i / (float)AimDotCount, 1f)"),
                "[193] 도트 위상 = Repeat 순환(경로 루프)");
        }

        [Test]
        public void Landing_Ring_Is_Red()
        {
            // [193차] 폭발 범위 = 빨간색(위험 구역 — 사용자 지시)
            Assert.That(Src(), Does.Contain("private static readonly Color AimPreviewRed = new Color(1f, 0.25f, 0.2f, 0.85f)"),
                "[193] 폭발 범위 링 색 = 빨간색");
            Assert.That(Src(), Does.Contain("ConfigureAimLine(_aimLandingLine, AimLandingSegments, 0.06f, AimPreviewRed, loop: true)"),
                "[193] 착지 링 = AimPreviewRed 적용");
            Assert.That(Src(), Does.Not.Contain("ConfigureAimLine(_aimLandingLine, AimLandingSegments, 0.06f, AimPreviewGoldRing"),
                "[193] 착지 링 골드 잔존 금지 — 빨간색 전용으로 교체");
        }

        [Test]
        public void Landing_Ring_Has_Pulse_And_Fill()
        {
            // [193차] 펄스 링 + 내부 필
            Assert.That(Src(), Does.Contain("float pulse = 1f + AimPulseAmplitude * Mathf.Sin(Time.unscaledTime * AimPulseSpeed)"),
                "[193] 펄스 링 = 반지름 ±6% 진동");
            Assert.That(Src(), Does.Contain("EnsureAimLandingFill()"),
                "[193] 착지 내부 필 생성");
            Assert.That(Src(), Does.Contain("AimPreviewRedFill"),
                "[193] 내부 필 = 투명 빨간 디스크");
            Assert.That(Src(), Does.Contain("float d = radius * 2f;"),
                "[193] 내부 필 반지름 = 펄스 링 동기");
        }

        [Test]
        public void Preview_Cleanup_Nulls_New_References()
        {
            // [193차] 프리뷰 파괴 시 신규 참조 정리
            Assert.That(Src(), Does.Contain("_aimDots = null;"),
                "[193] 도트 참조 정리");
            Assert.That(Src(), Does.Contain("_aimLandingFill = null;"),
                "[193] 필 참조 정리");
        }
    }
}
