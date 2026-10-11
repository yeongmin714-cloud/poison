using NUnit.Framework;
using UnityEngine;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-11 사용자 요구 — 게이지 시계방향 소모] HP/스태미나 게이지 소모 방향 계약.
    /// 만원 → 소모 시 공백이 12시부터 시계방향으로 성장(채움 시작 에지가 시계방향 이동, 끝 12시 고정).
    /// BowAimReticle 파워 링은 opt-in 미적용(픽셀 무변경 보호).
    /// </summary>
    public class HudGaugeDrainTests
    {
        [Test]
        public void ArcStartDegrees_DrainMode_MovesClockwiseAsFractionDrops()
        {
            // 순수 헬퍼 — Unity 패널 y 하향 기준 +각도가 시계방향(12시=-90°, 3시=0°, 6시=90°).
            Assert.AreEqual(-90f, UTKCircularGauge.ArcStartDegrees(1f, true), 0.0001f, "만원 → 12시 시작");
            Assert.AreEqual(0f, UTKCircularGauge.ArcStartDegrees(0.75f, true), 0.0001f,
                "75% → 소모 경계가 3시(12시부터 시계방향 1/4 공백)");
            Assert.AreEqual(90f, UTKCircularGauge.ArcStartDegrees(0.5f, true), 0.0001f,
                "50% → 소모 경계가 6시(시계방향 반파 공백)");
            Assert.AreEqual(270f, UTKCircularGauge.ArcStartDegrees(0f, true), 0.0001f,
                "0% → 전원 공백(12시 wrap)");
            // 일반 모드 — 12시 고정(기존 P22-5/BowAimReticle 계약).
            Assert.AreEqual(-90f, UTKCircularGauge.ArcStartDegrees(0.75f, false), 0.0001f);
            Assert.AreEqual(-90f, UTKCircularGauge.ArcStartDegrees(0f, false), 0.0001f);
        }

        [Test]
        public void GaugePolling_HasDirectMonoKeeperTick()
        {
            // [17차 수리] 스케줄러(부착 전 생성)만으로는 폴링이 실행되지 않아 게이지가 0 고정 —
            // MonoKeeper 0.25s 직접 틱(PollTick→RefreshAll) 이중 보험 + 첫 폴링 진단 로그 계약.
            string hud = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/HUDUTK.cs");
            Assert.That(hud, Does.Contain("public void PollTick()"), "직접 폴링 진입점.");
            Assert.That(hud, Does.Contain("if (hud != null) hud.PollTick();"), "MonoKeeper 틱 배선.");
            Assert.That(hud, Does.Contain("_pollAccum >= 0.25f"), "0.25s 틱 주기.");
            Assert.That(hud, Does.Contain("첫 폴링"), "첫 폴링 진단 로그(Play 판별용).");
        }

        [Test]
        public void HudWiresDrainClockwise_ReticleStaysDefault()
        {
            string hud = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/HUDUTK.cs");
            Assert.That(hud, Does.Contain("DrainClockwise = true"),
                "HUD HP/스태미나 게이지는 시계방향 소모(opt-in)여야 한다.");
            string reticle = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/BowAimReticleUTK.cs");
            Assert.That(reticle, Does.Not.Contain("DrainClockwise"),
                "BowAimReticle 파워 링은 기본값(12시 고정 채움) 유지 — 픽셀 무변경 보호.");
            string gauge = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/UTKCircularGauge.cs");
            Assert.That(gauge, Does.Contain("public static float ArcStartDegrees(float fraction, bool drainClockwise)"),
                "호 시작 각도 순수 헬퍼 계약.");
            Assert.That(gauge, Does.Contain("ArcStartDegrees(fraction, _drainClockwise)"),
                "WriteRing이 방향 스위치를 경유해야 한다.");
        }
    }
}
