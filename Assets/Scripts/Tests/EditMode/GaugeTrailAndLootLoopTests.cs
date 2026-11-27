using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [80차→84차] HUD 피드백 수리 — 소스 문자열 어서션(진실치 마커 검증).
    /// ①원형 게이지 고스트 링 = 앞 링과 동일 반지름/두께(구 ×1.22 돌출 폐기 — 81차, UTKCircularGauge 소비처)
    /// ②전리품 파티클 원샷 강제(Hovl 복사본 looping:1 → 재생 직전 main.loop=false) + AutoDestroy 절대 상한
    /// [83차→84차 승격] HudTrailArc_SameFrame_SameSlot_As_FrontArc는 84차에서 계약 소멸 —
    ///   별도 트레일 요소를 폐기하고 동일 게이지 TrailFraction으로 통합(CanvasFitAndRingQualityTests 참조).
    /// </summary>
    public sealed class GaugeTrailAndLootLoopTests
    {
        [Test]
        public void Gauge_GhostTrail_SameSize_As_Front_Ring()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/UTKCircularGauge.cs");
            Assert.That(src, Does.Contain("WriteRing(mc, midR, thickness, _trailFraction, _trailColor);"),
                "[81차] 뒷줄 호 — 앞줄과 동일 midR/두께(같은 트랙, 각도 갭으로만 표시)");
            Assert.That(src, Does.Not.Contain("thickness * 1.22f"),
                "[81차] 구 ×1.22 돌출 두께 잔존 금지 — '두 줄 크기 안맞음' 원인");
        }

        [Test]
        public void LootSpawnFX_Forces_OneShot_Loop_Off()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/LootSpawnFX.cs");
            Assert.That(src, Does.Contain("main.loop = false;         // [80차] 원샷 강제"),
                "[80차] 재생 직전 loop 해제 — 스폰 1회 버스트 계약");
            Assert.That(src, Does.Not.Contain("ps.main = main;"),
                "[80차] ParticleSystem.main은 읽기 전용 — 재대입 불가(CS0200 실측)");
            Assert.That(src, Does.Contain("ps.Play(true);"),
                "명시 재생 유지(자체 재생 안 하는 팩 프리팹 대응)");
        }

        [Test]
        public void LootAutoDestroy_Has_Absolute_Wait_Cap()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/LootSpawnFX.cs");
            Assert.That(src, Does.Contain("private const float MaxTotalWait = 8f;"),
                "[80차] 절대 상한 — IsAlive 대기 반복 무한 연장 차단");
            Assert.That(src, Does.Contain("_total < MaxTotalWait"),
                "[80차] 상한 판정 — 상한 도달 시 IsAlive와 무관하게 파괴");
        }
    }
}
