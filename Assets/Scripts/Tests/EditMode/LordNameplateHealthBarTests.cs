using System.IO;
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [102차] 영주(InvadedLordEncounter) 이름표 체력바 — 소스 문자열 어서션 테스트
    /// (VillageNpcFKeyTests 선례 — [102차] 마커 + Does.Contain).
    /// 스펙: NameplateOverlayUTK의 BindNpc에서 NameplateDisplay 부모 체인의 InvadedLordEncounter를
    /// 판정해 영주 슬롯으로 승격(병사와 동일 이름+HP바 2행), RefreshNpc에서 HP 비율 갱신 + 고스트
    /// 트레일 + Resolved/IsDead 즉시 숨김.
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class LordNameplateHealthBarTests
    {
        private static string ReadOverlaySource()
        {
            return File.ReadAllText("Assets/Scripts/UI/Toolkit/NameplateOverlayUTK.cs");
        }

        [Test]
        public void Overlay_BindsInvadedLordEncounter_InBindNpc()
        {
            string src = ReadOverlaySource();

            Assert.That(src, Does.Contain("InvadedLordEncounter"),
                "[102차] NameplateOverlayUTK가 InvadedLordEncounter를 참조해야 한다.");
            Assert.That(src, Does.Contain("GetComponentInParent<InvadedLordEncounter>()"),
                "[102차] BindNpc에서 NameplateDisplay 부모 체인의 영주 컴포넌트를 판정해야 한다.");
        }

        [Test]
        public void Overlay_HasRoundMarker_AndLordSlotField()
        {
            string src = ReadOverlaySource();

            Assert.That(src, Does.Contain("[102차]"), "[102차] 마커 주석.");
            Assert.That(src, Does.Contain("public InvadedLordEncounter lord"),
                "[102차] Slot 클래스에 영주 컴포넌트 참조 저장용 필드가 있어야 한다.");
        }

        [Test]
        public void Overlay_RefreshNpc_UpdatesLordHpRatio_AndGhostTrail()
        {
            string src = ReadOverlaySource();

            // RefreshNpc 내부 영주 분기 — CurrentHP/MaxHP 비율 계산 + TickGhost 고스트 계약
            Assert.That(src, Does.Contain("lord.CurrentHP").And.Contain("lord.MaxHP"),
                "[102차] RefreshNpc 영주 분기에서 CurrentHP/MaxHP 비율을 계산해야 한다.");
            Assert.That(src, Does.Contain("TickGhost(slot, lord, lordRatio)"),
                "[102차] RefreshNpc 영주 분기에서 고스트 트레일 TickGhost(ghostOwner 계약)를 호출해야 한다.");
            Assert.That(src, Does.Contain("SetFillWidth(slot, lordFront)"),
                "[102차] RefreshNpc 영주 분기에서 SetFillWidth로 채움 폭을 갱신해야 한다.");
        }

        [Test]
        public void Overlay_LordSlot_HidesOnResolvedOrDead_AndUsesGuardGradient()
        {
            string src = ReadOverlaySource();

            // 종결/사망 즉시 숨김 — RefreshNpc 내부에서 처리(RefreshSlot의 SlotAlive 경로와 충돌 없게)
            Assert.That(src, Does.Contain("lord.IsDead").Or.Contain("lord.State"),
                "[102차] RefreshNpc 영주 분기에서 IsDead/State 판정이 있어야 한다.");
            Assert.That(src, Does.Contain("InvadedLordEncounter.EncounterState.Resolved"),
                "[102차] EncounterState.Resolved 종결 상태 판정이 있어야 한다.");

            // [102차] 영주=병사 그라디언트 계승 주석 명시
            Assert.That(src, Does.Contain("영주=병사 그라디언트 계승"),
                "[102차] 영주 HP바가 병사용 그라디언트(_gradGuard/_gradGuardWarn)를 계승함을 주석으로 명시.");
        }

        [Test]
        public void Overlay_PoolReuse_ResetsLordReference_OnGuardAndMonsterBind()
        {
            string src = ReadOverlaySource();

            // [80차] 풀 재사용 오염 방지 패턴 계승 — BindGuard/BindMonster에서 lord 리셋
            Assert.That(src, Does.Contain("slot.lord = null"),
                "[102차] BindGuard/BindMonster(및 Slot.Reset)에서 lord 참조를 null로 리셋해야 한다(풀 재사용 오염 방지).");
        }
    }
}
