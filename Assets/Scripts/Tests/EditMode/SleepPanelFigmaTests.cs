using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [73차 Phase A] 수면 시스템 패널(피그마 227:262) 계약 (소스 어서션 — GoldEconomyTests 선례).
    /// ①진실치 마커 ②게임 로직 신설(자동저장 스위치/수면 회복/수동 5슬롯/기상 미리보기) ③배선 교체 ④위조 금지.
    /// </summary>
    public class SleepPanelFigmaTests
    {
        private static string Panel() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/SleepPanelUTK.cs");
        private static string Toggle() => System.IO.File.ReadAllText("Assets/Scripts/Systems/SleepAutoSaveToggle.cs");
        private static string Wire() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/UTKWireUp.cs");

        [Test]
        public void Panel_FigmaTruth_Markers()
        {
            var src = Panel();
            Assert.That(src, Does.Contain("수면 시스템"), "패널 제목(22/700).");
            Assert.That(src, Does.Contain("휴식 및 진행 상황 저장"), "보조 제목.");
            Assert.That(src, Does.Contain("전초기지 · 개인 숙소").Or.Contain("장소 이름"),
                "휴식 장소 라인(침대 실측 장소명).");
            Assert.That(src, Does.Contain("안전 지역"), "안전 상태 배지.");
            Assert.That(src, Does.Contain("수면 시간"), "수면 시간 섹션.");
            Assert.That(src, Does.Contain("기상 예정"), "기상 미리보기.");
            Assert.That(src, Does.Contain("회복 예상"), "회복 예상 섹션.");
            Assert.That(src, Does.Contain("수면 전 자동 저장"), "자동 저장 설정.");
            Assert.That(src, Does.Contain("수동 저장 슬롯"), "수동 저장 목록.");
            Assert.That(src, Does.Contain("지금 저장"), "슬롯 행 저장 버튼.");
            Assert.That(src, Does.Contain("저장 후 수면 시작"), "실행 버튼.");
            Assert.That(src, Does.Contain("Enter  확인     ·     Esc  닫기").Or.Contain("Enter"),
                "단축키 안내.");
        }

        [Test]
        public void Panel_ThreeButtons_FigmaContract()
        {
            var src = Panel();
            Assert.That(src, Does.Contain("\"2시간\""), "수면 시간 선택 3버튼 — 피그마(2/4/8).");
            Assert.That(src, Does.Contain("\"4시간\""), "4시간.");
            Assert.That(src, Does.Contain("\"8시간\""), "8시간.");
            Assert.That(src, Does.Contain("짧은 휴식"), "버튼 설명.");
            Assert.That(src, Does.Contain("가벼운 수면"), "버튼 설명.");
            Assert.That(src, Does.Contain("충분한 수면"), "버튼 설명.");
            // 구 SleepUTK의 "아침까지 자기" 5버튼 구조는 폐기 — 신창에 미포함.
        }

        [Test]
        public void GameLogic_AutoSaveToggle_WithPlayerPrefs()
        {
            var src = Toggle();
            Assert.That(src, Does.Contain("public static bool Enabled"), "정적 스위치.");
            Assert.That(src, Does.Contain("poison_sleep_autosave"), "PlayerPrefs 영속 키.");
            var panel = Panel();
            Assert.That(panel, Does.Contain("SaveManager.AutoSave").Or.Contain("AutoSave"),
                "수면 시작 직전 자동 저장(ON일 때).");
        }

        [Test]
        public void GameLogic_SleepRecovery_RealAPIs()
        {
            var src = Panel();
            Assert.That(src, Does.Contain("SleepFor"), "수면 실행 = TimeManager.SleepFor(원본 단일소스).");
            Assert.That(src, Does.Contain("Heal"), "체력 회복 — PlayerHealth.Heal 실측 API.");
            Assert.That(src, Does.Contain("Stamina"), "기력 회복 — PlayerMovement 실측(실내 부재 시 \"-\").");
            Assert.That(src, Does.Contain("0.36f").Or.Contain("36"),
                "회복 계수 8h 기준 36%(피그마 목업치 채용 — 실측 현재/최대 기반 계산).");
        }

        [Test]
        public void GameLogic_ManualSlots_UseSaveManager()
        {
            var src = Panel();
            Assert.That(src, Does.Contain("SaveManager.Instance.Save"), "수동 저장 = SaveManager.Save(slot) 기존 API.");
            Assert.That(src, Does.Contain("HasSave"), "슬롯 메타 조회(저장 유무).");
            Assert.That(src, Does.Contain("최근 저장"), "슬롯 행2 상태 라벨 — 3행 구성 [144차 P7-SLP #2].");
            Assert.That(src, Does.Contain("플레이 시간"), "슬롯 행3 플레이 시간 라벨 [144차 P7-SLP #2].");
            Assert.That(src, Does.Contain("BuildFileCheckGlyph"), "슬롯 file-check-2 글리프 — iconHost 자식 [144차 P7-SLP #3].");
        }

        [Test]
        public void Wiring_BedRoutedToNewPanel()
        {
            var src = Wire();
            Assert.That(src, Does.Contain("SleepPanelUTK.Open(bed);"),
                "침대 → 신창(SleepPanelUTK) 라우팅.");
            Assert.That(src, Does.Contain("[73차"), "교체 마커.");
        }

        [Test]
        public void NoFakeData_UnderscoreFallback()
        {
            var src = Panel();
            Assert.That(src, Does.Contain("\"-\""), "위조 금지 — 없는 데이터는 \"-\".");
        }

        /// <summary>[144차 P7-SLP] P1 수리(#4~#42) 계약 — 테두리/좌표/색 진실치 소스 어서션.</summary>
        [Test]
        public void Panel_FigmaTruth_P1Repairs_144()
        {
            var src = Panel();
            Assert.That(src, Does.Contain("[144차 P7-SLP #4]"), "루트 테두리 #30363D@0.502 sw1.2(227:262).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #9]"), "보조 제목 제목 우측 인라인 (155,8).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #12]"), "닫기 테두리(227:267).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #13]"), "moon #58A6FF + y3.");
            Assert.That(src, Does.Contain("[144차 P7-SLP #16]"), "bed #58A6FF 채색.");
            Assert.That(src, Does.Contain("[144차 P7-SLP #20]"), "shield 14×14.");
            Assert.That(src, Does.Contain("[144차 P7-SLP #24]"), "기상 미리보기 테두리 #58A6FF@0.251.");
            Assert.That(src, Does.Contain("[144차 P7-SLP #26]"), "현재 시간 y34.");
            Assert.That(src, Does.Contain("[144차 P7-SLP #27]"), "선택 시간 (261,26) — 중앙 312 유지.");
            Assert.That(src, Does.Contain("[144차 P7-SLP #29]"), "기상 시간 #58A6FF + y34.");
            Assert.That(src, Does.Contain("[144차 P7-SLP #32/#33]"), "칩 테두리 비활성 #30363D/활성 #58A6FF.");
            Assert.That(src, Does.Contain("[144차 P7-SLP #14/#37]"), "카드 테두리 #30363D@0.502(장소/회복).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #40]"), "현재 수치 (14,41).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #41]"), "예상 수치 (66,41).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #42]"), "회복 게이지 y74.");
            Assert.That(src, Does.Contain("0x30 / 255f, 0x36 / 255f, 0x3D / 255f"), "StrokeSub #30363D 상수.");
            Assert.That(src, Does.Contain("0.502f").And.Contain("0.251f"), "테두리 알파 진실치.");
            Assert.That(src, Does.Not.Contain("BuildMoonIcon(18f)"), "moon 호출부 시그니처 갱신(파라미터화).");
        }

        /// <summary>[144차 P7-SLP] P1 수리 B(#45~#69) 계약 — 안내문구/아이콘색/스위치/스크롤바/슬롯/버튼 진실치 소스 어서션.</summary>
        [Test]
        public void Panel_FigmaTruth_P1RepairsB_144()
        {
            var src = Panel();
            Assert.That(src, Does.Contain("임무는 계속됩니다."), "안내 문구 전문 [144차 P7-SLP #45](227:332).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #48]"), "save 글리프 #58A6FF sw1.6(227:395-396).");
            Assert.That(src, Does.Contain("수면을 시작하면 자동 저장 슬롯에 기록합니다."), "설정 안내 문구 [144차 P7-SLP #51](227:341).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #52]"), "설정 상태 (500,24) 좌측정렬(227:342).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #54]"), "스위치 손잡이 #0B0E14 16×16 (19,3.5)(227:343-344).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #57]"), "스크롤바 track 8×248/thumb 8×96 #58A6FF r999(227:459-460).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #58]"), "슬롯 행 테두리 #30363D@0.502(227:345).");
            Assert.That(src, Does.Contain("수동 저장 {i + 1:00}"), "슬롯 이름 '수동 저장 01' 명명 [144차 P7-SLP #60](227:350).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #63]"), "지금 저장 테두리 #58A6FF@0.251(227:354).");
            Assert.That(src, Does.Contain("MakeLabel(\"지금 저장\", 14f"), "버튼 문구 fs14 [144차 P7-SLP #64](227:355).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #65]"), "취소 버튼 테두리 #30363D@0.502(227:358).");
            Assert.That(src, Does.Contain("[144차 P7-SLP #69]"), "시작버튼 moon #0B0E14 x157(227:401-402).");
        }
    }
}
