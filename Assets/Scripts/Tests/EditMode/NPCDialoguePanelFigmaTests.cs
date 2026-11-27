using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [74차] NPC/병사 대화 패널 피그마 163:23/187:14 정합 계약 (소스 어서션 — SleepPanelFigmaTests 선례).
    /// ①진실치 마커 ②병사 대화 진입점(OpenForGuard) ③배치 계약(PlaceAtAnchor — [151차 P2] 항상 중앙 하단 고정
    /// → [154차 P1] y 이중 반전 수리 + 배치 산식 순수 함수 ComputeBottomCenterLayout 재편) ④라우팅 연결.
    /// </summary>
    public class NPCDialoguePanelFigmaTests
    {
        private static string Dlg() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs");
        private static string Panel() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs");

        [Test]
        public void Panel_FigmaTruth_GeometryAndColors()
        {
            var src = Dlg();
            Assert.That(src, Does.Contain("528f"), "패널 528폭(진실치).");
            Assert.That(src, Does.Contain("148.8f"), "대화 전용 높이 148.8.");
            Assert.That(src, Does.Contain("171.8f"), "선택지 포함 높이 171.8.");
            Assert.That(src, Does.Contain("new Color(0.086f, 0.106f, 0.133f, 0.45f)"),
                "bg #161B22/45% [144차 재실측 정정].");
            Assert.That(src, Does.Contain("9.6f"), "radius 9.6.");
            Assert.That(src, Does.Contain("new Color(0.2f, 0.898f, 0.851f)"),
                "인디케이터/이름 #33E5D9 [144차 LSB 정정].");
            Assert.That(src, Does.Contain("7.2f"), "인디케이터 7.2px 원.");
            Assert.That(src, Does.Contain("13.2f"), "NpcName 13.2/700.");
            Assert.That(src, Does.Contain("15.6f"), "대사 폰트 15.6.");
            Assert.That(src, Does.Contain("new Color(0.922f, 0.929f, 0.941f)"),
                "1행 #EBEDF0 [144차 LSB 정정].");
            Assert.That(src, Does.Contain("new Color(0.345f, 0.651f, 1f)"),
                "ContinueHint #58A6FF [144차 LSB 정정].");
            Assert.That(src, Does.Contain("계속하려면 클릭 ▶"), "힌트 텍스트.");
        }

        [Test]
        public void ChoiceCards_3Column_FigmaContract()
        {
            var src = Dlg();
            Assert.That(src, Does.Contain("159.2f"), "선택 카드 159.2 고정폭(3열).");
            Assert.That(src, Does.Contain("28.8f"), "카드 높이 28.8.");
            Assert.That(src, Does.Contain("new Color(1f, 1f, 1f, 0.031f)"), "카드 bg #FFFFFF/3.1% [144차 정정].");
            Assert.That(src, Does.Contain("7.2f"), "카드 radius 7.2.");
            Assert.That(src, Does.Contain("Wrap.NoWrap"), "3열 1행(랩 없음).");
            Assert.That(src, Does.Contain("\"선택 \" + number"), "선택 N 라벨.");
            Assert.That(src, Does.Contain("new Color(0.922f, 0.929f, 0.941f)"),
                "카드 본문 #EBEDF0.");
        }

        [Test]
        public void GuardEntry_AndLoyaltyLine()
        {
            var src = Dlg();
            Assert.That(src, Does.Contain("OpenForGuard(GuardPlaceholder guard)") | Does.Contain("OpenForGuard(ProjectName.Systems.GuardPlaceholder guard)"),
                "병사 대화 진입점.");
            Assert.That(src, Does.Contain("체감 분위기: {grade} — 호감도 {loyalty:F0}"),
                "2행 = 호감 단계 안내(실측).");
            Assert.That(src, Does.Contain("GuardChoices"), "병사 선택지 3개.");
            Assert.That(src, Does.Contain("\"안녕하세요.\""), "선택지 계약.");
        }

        [Test]
        public void PlacedAtTarget_AnchorOverload()
        {
            var src = Dlg();
            Assert.That(src, Does.Contain("PlaceAtAnchor"), "배치 경로(시그니처 호환 — [151차 P2] 항상 중앙 하단).");
            // [151차 P2] 계약 진화 — 구 앵커 투영(TargetScreenProjector.TryProject/HasVisualBounds 분기)은 폐기되고
            //   항상 중앙 하단(50%, 85%)으로 일원화(Play 로그 투영 무효 반복 — 사용자 지시 "항상 중앙 하단").
            Assert.That(src, Does.Contain("[151차 P2]"), "[151차 P2] 항상 중앙 하단 고정 마커 주석.");
            Assert.That(src, Does.Not.Contain("TargetScreenProjector.TryProject(anchor, upM, out Vector2 screen, out _)"),
                "[151차 P2] 구 공용 유틸 앵커 투영 호출 폐기(폴백 산식 상시화).");
            Assert.That(src, Does.Not.Contain("TargetScreenProjector.HasVisualBounds(anchor) ? HeadAnchorUpM : NoBoundsHeadM"),
                "[151차 P2] 구 bounds 분기 상수 폐기(HeadAnchorUpM/NoBoundsHeadM 소멸).");
            Assert.That(src, Does.Contain("[92차 결함2]"), "[92차 결함2] 마커 주석(ApplyAnchorLayout 루트 클램프 계약 유지).");
            Assert.That(src, Does.Contain("public static void Open(NPCInstance npc, Transform anchor)"),
                "NPC 개방 anchor 오버로드(시그니처 호환 유지 — anchor 무시).");
        }

        /// <summary>
        /// [154차 P1] 대화 패널 중앙 하단 고정 — 배치 산식 순수 함수 재편(구 [151차 P2] 소스 계약 승계).
        /// ①단일 경로(PlaceAtAnchor → ApplyBottomCenterPlacement → ApplyAnchorLayout → ComputeBottomCenterLayout)
        /// ②bottom-up 입력 핀(Screen.height*0.15f = 위에서 85% 지점 — 구 0.85 입력은 ApplyAnchorLayout 반전 후
        ///   0.15(상단)이 되는 y축 이중 반전 결함으로 폐기) ③ComputeBottomCenterLayout 순수 검증 —
        ///   screen 1240x828의 50%/85% 지점(top-origin 620, 703.8) + panelW 528 + panelH 148.8(DialogueHeight)
        ///   + root 1920x1080 → top ≥ rootH 60%(하단 밴드 착지) + left 중앙 정렬
        /// ④구 투영/재배치 경로 폐기 회귀 핀([151차 P2] 승계).
        /// </summary>
        [Test]
        public void DialoguePanel_BottomCenterComputed_154()
        {
            // ── ① 소스 계약 — 배치 단일 경로 + bottom-up 입력([154차 P1] 이중 반전 수리 핀). ──
            var src = Dlg();
            Assert.That(src, Does.Contain("[154차 P1]"), "[154차 P1] 마커 주석.");
            Assert.That(src, Does.Contain("ApplyBottomCenterPlacement(rootW, rootH);"),
                "[151차 P2 승계] PlaceAtAnchor 본문 — 중앙 하단 배치 단일 호출(투영/클램프/프록시 경로 부재).");
            Assert.That(src, Does.Contain("Screen.width * 0.5f, Screen.height * 0.15f"),
                "[154차 P1] 배치 입력 — bottom-up 0.15(위에서 85% 지점). 구 0.85 입력(반전 후 상단) 폐기.");
            Assert.That(src, Does.Contain("internal static (float left, float top) ComputeBottomCenterLayout("),
                "[154차 P1] 배치 산식 순수 함수 — 캔버스 환산+ScaleRect+클램프 체인 단일소스.");

            // ── ② 순수 산식 검증 — 최종 좌표 계약(구 [151차 P2] 상수 존재 류 검증은 폐기, 결과 검증으로 대체). ──
            // screen 1240x828 50%/85% 지점(top-origin 620, 703.8) + panelW 528 + panelH DialogueHeight(148.8)
            // + root 1920x1080. 스크린 비율은 캔버스 환산에서 상쇄 — 캔버스 앵커는 화면 크기 무관 (960, 918) 수렴.
            (float left, float top) = ProjectName.UI.Toolkit.NPCDialoguePanelUTK.ComputeBottomCenterLayout(
                620f, 703.8f, 1240f, 828f, 528f, 148.8f, 1920f, 1080f);
            Assert.That(top, Is.GreaterThanOrEqualTo(1080f * 0.6f),
                "[154차 P1] 하단 밴드 착지 — top ≥ rootH 60%(구 결함: 상단 0 클램프).");
            Assert.That(top, Is.EqualTo(769.2f).Within(0.5f),
                "[154차 P1] top = 918(캔버스 85%) − panelH 148.8.");
            Assert.That(left, Is.EqualTo(696f).Within(0.5f),
                "[154차 P1] left = 960(캔버스 50%) − panelW/2 — 중앙 정렬.");
            Assert.That(left + 528f * 0.5f, Is.EqualTo(960f).Within(0.5f),
                "[154차 P1] 패널 중앙 x = 루트 중앙(960).");

            // ── ③ 구 경로 폐기 회귀 핀([151차 P2] 승계 — 리터럴 분할 유지). ──
            Assert.That(src, Does.Not.Contain("TargetScreenProjector"),
                "[151차 P2 승계] 앵커 투영 경로 폐기 — TargetScreenProjector 역참조 부재.");
            Assert.That(src, Does.Not.Contain("Reacquire" + "Placement"),
                "[151차 P2 승계] 구 재배치 루프 폐기(항상 고정이라 무의미).");
            Assert.That(src, Does.Not.Contain("_place" + "Failed"),
                "[151차 P2 승계] 구 배치 실패 플래그 소멸.");
            Assert.That(src, Does.Not.Contain("_acqPlace" + "Target"),
                "[151차 P2 승계] 구 재배치 대상 캐시 소멸.");
            Assert.That(src, Does.Not.Contain("_placeRetry" + "Left"),
                "[151차 P2 승계] 구 재시도 카운터 소멸.");
        }

        [Test]
        public void Routing_Connected()
        {
            var src = Panel();
            Assert.That(src, Does.Contain("NPCDialoguePanelUTK.OpenForGuard(talkGuard);"),
                "병사 대화 = 카메라 확대+대화 패널 연결.");
            Assert.That(src, Does.Contain("NPCDialoguePanelUTK.Open(npc.NPCData, npc.transform);"),
                "NPC 대화도 대상 앞 배치(anchor).");
        }
    }
}
