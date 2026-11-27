using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호5/P3] Time Paradox 통합 계약 — PERFECT 패링 타임 스톱(Test_10 opt-in) + 소유권 분리.
    /// 계획: docs/plans/2026-11-07_attack-parry-premium-timeparadox-plan.md §P3.
    /// 소스 계약(문자열 어서션) 원칙 — TelegraphPremiumTests 선례.
    /// </summary>
    public class ParadoxTimeStopTests
    {
        [Test]
        public void ParadoxTimeStop_OptInGateAndLocalZonesOnly()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ParadoxTimeStop.cs");
            Assert.That(src, Does.Contain("Test_10_TerritoryCombat"),
                "[P3-E] Test_10 한정 opt-in 게이트 — 타 씬 무동작.");
            Assert.That(src, Does.Contain("EngineMode.LocalZonesOnly"),
                "[P3-C] LocalZonesOnly — 전역 timescale 무접촉(플레이어 이동 보존).");
            Assert.That(src, Does.Contain("EffectiveTimeScale <= 0.01f"),
                "상태 판정은 에셋 소유 EffectiveTimeScale 읽기 전용.");
            Assert.That(src, Does.Not.Contain("Time.timeScale ="),
                "전역 timescale 직접 대입 금지 — 소유권 분리 계약(LocalZonesOnly 경유).");
        }

        [Test]
        public void AnimalAI_RespectsParadoxStasisGate()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(src, Does.Contain("ParadoxTimeStop.IsActiveFor(this)"),
                "[P3] 몬스터 AI 게이트 — 스테이시스 중 이동/어그로 스킵.");
            Assert.That(src, Does.Contain("[무번호5/P3] Time Paradox 스테이시스 게이트"),
                "게이트 마커 존재(IndoorActive 선례 동일 패턴).");
        }

        [Test]
        public void PlayerCombat_PerfectParryTriggersTimeStop()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("ParadoxTimeStop.Trigger(transform.position)"),
                "[P3-D] PERFECT 패링 → 타임 스톱 연출 훅.");
            Assert.That(src, Does.Contain("MagicTimeUser"),
                "[소유권 공존] 기존 패링 개인 슬로우(MagicTime, 95차 F1) 유지 — 1종 제거 없음.");
            Assert.That(src, Does.Not.Contain("Time.timeScale ="),
                "전역 timescale 직접 대입 금지(신규 계약 — 기존 로컬 슬로우 경로 무수정).");
        }

        [Test]
        public void SystemsAsmdef_ReferencesTimeWarpRuntime()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/ProjectName.Systems.asmdef");
            Assert.That(src, Does.Contain("\"TimeWarp.Runtime\""),
                "Systems→TimeWarp.Runtime 참조 — MagicTime 직접 참조 선례 준용.");
        }

        [Test]
        public void TimeWarpManager_SourceInstalled()
        {
            string src = System.IO.File.ReadAllText("Assets/Time Paradox Engine/Core/Managers/TimeWarpManager.cs");
            Assert.That(src, Does.Contain("class TimeWarpManager"),
                "엔진 코어 매니저 임포트 확인.");
            string st = System.IO.File.ReadAllText("Assets/Time Paradox Engine/Core/TimeState.cs");
            Assert.That(st, Does.Contain("LocalZonesOnly"),
                "엔진 EngineMode 임포트 확인.");
        }
    }
}
