using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [72차] 병사 상태보기 피그마 2종 패널 계약 (소스 어서션 — GoldEconomyTests 선례).
    /// ①GuardStatusWindowUTK 신설(84:4 equipment-inventory/83:4 character-status) ②Portrait 3D RT 캡처
    /// ③상태보기 라우팅 교체(구창 GuardInfoUTK → 신창) ④Figma 진실치 마커 ⑤위조 금지("-" 폴백).
    /// </summary>
    public class GuardStatusPanelTests
    {
        private static string Win() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/GuardStatusWindowUTK.cs");
        private static string Panel() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs");

        [Test]
        public void StatusWindow_Exists_WithTwoModes()
        {
            var src = Win();
            Assert.That(src, Does.Contain("class GuardStatusWindowUTK"),
                "상태보기 창 신설.");
            Assert.That(src, Does.Contain("PanelW + PanelGap + EquipPanelW"),
                "아군 = CharacterStatusPanel+EquipmentPanel 결합(84:4).");
            Assert.That(src, Does.Contain("ally ? PanelW + PanelGap + EquipPanelW : PanelW"),
                "적병사 = CharacterStatusPanel 단독(83:4).");
            Assert.That(src, Does.Contain("IsAlly"), "모드 분기는 병사 소속(IsAlly) 기준.");
        }

        [Test]
        public void StatusWindow_FigmaTruthMarkers()
        {
            var src = Win();
            Assert.That(src, Does.Contain("576f"), "CharacterStatusPanel 576폭.");
            Assert.That(src, Does.Contain("936f"), "패널 936높이.");
            Assert.That(src, Does.Contain("531.6f"), "EquipmentPanel 531.6폭.");
            Assert.That(src, Does.Contain("\"상태 정보\""), "헤더 타이틀.");
            Assert.That(src, Does.Contain("CHARACTER STATUS"), "헤더 서브타이틀.");
            Assert.That(src, Does.Contain("코드네임: "), "병사 이름 라인.");
            Assert.That(src, Does.Contain("공격력 (ATK)"), "코어 스탯 라벨.");
            Assert.That(src, Does.Contain("호감도 (Affection)"), "특수 상태 게이지 — 호감도.");
            Assert.That(src, Does.Contain("중독도 (Toxicity)"), "특수 상태 게이지 — 중독도.");
            Assert.That(src, Does.Contain("특수 상태 게이지"), "섹션 헤더.");
            Assert.That(src, Does.Contain("SPECIAL VITALS"), "섹션 영문 헤더.");
            Assert.That(src, Does.Contain("장착 장비"), "EquipmentPanel 헤더(84:4).");
            Assert.That(src, Does.Contain("LOADOUT MATRIX"), "장비 섹션 영문.");
            Assert.That(src, Does.Contain("SYS_EQ_STABLE"), "장비 상태 배지.");
            Assert.That(src, Does.Contain("장착 중인 장비"), "장비 그리드 섹션 라벨.");
            Assert.That(src, Does.Contain("전술 배낭"), "TacticalPack 섹션 라벨.");
            Assert.That(src, Does.Contain("TACTICAL PACK"), "TacticalPack 영문.");
        }

        [Test]
        public void Portrait_Is3dRealtimeCapture()
        {
            var src = Win();
            Assert.That(src, Does.Contain("RenderTexture(512, 288"),
                "Portrait = 대상 병사 3D 실물 촬영 RT.");
            Assert.That(src, Does.Contain("CaptureOnce"),
                "촬영 경로(개방 시 + 폴링 재촬영).");
            Assert.That(src, Does.Contain("GetComponentsInChildren<Renderer>"),
                "촬영 프레이밍 = 대상 렌더러 bounds 실측.");
            Assert.That(src, Does.Contain("PortraitBg"),
                "Portrait 배경 #0B0E14(피그마 진실치).");
            Assert.That(src, Does.Contain("SYS_SCAN_ACTIVE"),
                "Portrait 오버레이 좌상단.");
        }

        [Test]
        public void Routing_ReplacedToNewWindow()
        {
            var src = Panel();
            Assert.That(src, Does.Contain("GuardStatusWindowUTK.Open(guard);"),
                "상태보기 라우팅이 피그마 패널 창으로 교체되어야 한다.");
            Assert.That(src, Does.Contain("[72차 Phase A]"),
                "72차 교체 마커.");
        }

        [Test]
        public void NoFakeData_UnderscoreFallback()
        {
            var src = Win();
            Assert.That(src, Does.Contain("\"-\""),
                "위조 금지 — 없는 데이터(EXP/빈 슬롯)는 \"-\" 폴백.");
            Assert.That(src, Does.Contain("GetAttack()"), "스탯은 실측 API.");
            Assert.That(src, Does.Contain("GetMaxHP()"), "최대체력 실측.");
            Assert.That(src, Does.Contain("g.Loyalty"), "호감도 실측.");
            Assert.That(src, Does.Contain("g.Addiction"), "중독도 실측.");
        }
    }
}
