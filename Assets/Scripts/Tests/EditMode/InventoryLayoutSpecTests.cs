using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-11-23] 인벤토리 창 첫 오픈 그리드 버그 수리 + 여백 Figma 정합 — 소스계약 테스트
    /// (EquipFigmaSpecTests 선례: 소스 문자열 어서션, IMGUI InventoryWindow.cs 대상).
    ///
    /// 진실치: Figma 키 02vPXOGzFJUUYTaEPSVz3R · 프레임 15:4 fluent-game-inventory-ui ·
    /// 1920×1080 캔버스(= _uiScale 1 지점). 여백 토큰: 그리드 슬롯 갭(행/열 공통) 9.6 ·
    /// 패널 콘텐츠 인셋 24(InventoryPanel pad — 플러시 시작) · 장비칸 섹션 인셋 14.4 ·
    /// 장비칸 셀 갭 4.8(5×81.6+4×4.8=427.2 합산) · 탭 갭 4.8.
    ///
    /// 첫 오픈 그리드 버그 계약: OnGUI 내 RefreshUIScale()이 Rect winRect 산출 이전에 실행되어야 한다.
    /// (구 결함: 세션 최초 프레임의 winRect/드롭섀도우/닫기버튼이 _uiScale 초기값 1.0으로 그려지고
    /// 타이틀·장비칸·그리드만 갱신 스케일로 그려져 1920×1080 이외 해상도에서 첫 프레임만 어긋남)
    ///
    /// 1080p 검산: 메인 슬롯 (628−48−48)/6=88.67 · 갭 9.6 · 인셋 24 / 장비 cellW 106.4 ·
    /// cellH 58.52 · totalH 2×(22+58.52+14.4)=189.84(구 190.48).
    /// </summary>
    public class InventoryLayoutSpecTests
    {
        private static string Src() => System.IO.File.ReadAllText("Assets/Scripts/UI/InventoryWindow.cs");

        // ===================================================================
        // (1) Figma 여백 토큰 상수 소스계약
        // ===================================================================

        [Test]
        public void FigmaSpacingTokens_AreDeclared_WithMeasuredValues()
        {
            var src = Src();
            Assert.That(src, Does.Contain("private static float SLOT_MARGIN => 9.6f * _uiScale;"),
                "그리드 슬롯 갭(행/열 공통)은 Figma 실측 9.6이어야 한다(행피치 91.2 = 81.6 + 9.6).");
            Assert.That(src, Does.Contain("private static float PANEL_CONTENT_PAD => 24f * _uiScale;"),
                "패널 콘텐츠 인셋(외곽)은 Figma InventoryPanel pad 24(플러시 시작)여야 한다.");
            Assert.That(src, Does.Contain("private static float EQUIP_SECTION_PAD => 14.4f * _uiScale;"),
                "장비칸 섹션 인셋은 Figma 실측 14.4여야 한다.");
            Assert.That(src, Does.Contain("private static float EQUIP_CELL_GAP => 4.8f * _uiScale;"),
                "장비칸 셀 갭은 Figma 실측 4.8(5×81.6+4×4.8=427.2 합산)이어야 한다.");
            Assert.That(src, Does.Contain("private static float TAB_GAP => 4.8f * _uiScale;"),
                "탭 갭은 Figma 실측 4.8이어야 한다.");
            Assert.That(src, Does.Not.Contain("private static float SLOT_MARGIN => 6f * _uiScale;"),
                "구 슬롯 갭 6은 폐기되어야 한다.");
        }

        // ===================================================================
        // (2) 열수 무수정 계약 존속 — 상수를 건드리지 않았음을 보장
        //     (Figma 메인 그리드는 5열×81.6·갭 9.6이나 GRID_COLUMNS=6은 무수정 계약 —
        //      불일치는 사용자 보고용 근거로만 기록, 코드 변경 없음)
        // ===================================================================

        [Test]
        public void ColumnContracts_RemainUnmodified()
        {
            var src = Src();
            Assert.That(src, Does.Contain("private const int GRID_COLUMNS = 6;"),
                "메인 인벤 6열 무수정 계약 유지.");
            Assert.That(src, Does.Contain("private const int LOOT_COLUMNS = 5;"),
                "전리품 5열 무수정 계약 유지.");
            Assert.That(src, Does.Contain("private const int EQUIP_GRID_COLS = 5;"),
                "장비칸 5열 무수정 계약 유지.");
            Assert.That(src, Does.Contain("private const int GRID_ROWS_PER_PAGE = 5;"),
                "페이지당 5행 무수정 계약 유지.");
        }

        // ===================================================================
        // (3) 첫 오픈 그리드 버그 — RefreshUIScale() 순서 단정 계약
        // ===================================================================

        [Test]
        public void OnGUI_RefreshUIScale_RunsBeforeWinRect_Calculation()
        {
            var src = Src();
            int onGui = src.IndexOf("protected override void OnGUI()");
            Assert.That(onGui, Is.GreaterThanOrEqualTo(0), "InventoryWindow.OnGUI 진입점이 존재해야 한다.");
            var body = src.Substring(onGui);

            int refresh = body.IndexOf("RefreshUIScale();");
            int winRect = body.IndexOf("Rect winRect = new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT);");
            int initStyles = body.IndexOf("InitStyles();");
            int baseOnGui = body.IndexOf("base.OnGUI();");
            int closeButton = body.IndexOf("UIStyleManager.DrawCloseButton(winRect)");

            Assert.That(refresh, Is.GreaterThanOrEqualTo(0), "OnGUI 내 RefreshUIScale 호출이 존재해야 한다.");
            Assert.That(winRect, Is.GreaterThanOrEqualTo(0), "winRect 산출이 존재해야 한다.");
            Assert.That(refresh, Is.LessThan(winRect),
                "첫 오픈 그리드 버그 계약: RefreshUIScale()은 Rect winRect 산출 이전에 실행되어야 한다" +
                "(그렇지 않으면 첫 프레임 winRect/드롭섀도우/닫기버튼이 _uiScale=1.0 초기값으로 그려진다).");
            Assert.That(initStyles, Is.GreaterThanOrEqualTo(0));
            Assert.That(initStyles, Is.LessThan(winRect),
                "InitStyles()도 winRect 산출 이전이어야 한다(스케일 정합 스타일로 창박스 렌더).");
            Assert.That(baseOnGui, Is.GreaterThan(refresh),
                "base.OnGUI()는 스케일 갱신 이후에 실행되어야 한다.");
            Assert.That(closeButton, Is.GreaterThan(refresh),
                "DrawCloseButton은 스케일 갱신 이후에 실행되어야 한다.");
        }

        [Test]
        public void OnShow_RefreshesUIScale_ForStaticScaleConsumers()
        {
            var src = Src();
            int onShow = src.IndexOf("protected override void OnShow()");
            int onHide = src.IndexOf("protected override void OnHide()");
            Assert.That(onShow, Is.GreaterThanOrEqualTo(0), "OnShow가 존재해야 한다.");
            Assert.That(onHide, Is.GreaterThan(onShow), "OnHide가 OnShow 뒤에 존재해야 한다.");
            var body = src.Substring(onShow, onHide - onShow);
            Assert.That(body, Does.Contain("RefreshUIScale();"),
                "OnShow에서 스케일 선행 갱신이 있어야 한다(GetContextX/PanelWidth 등 정적 소비자 보호).");
        }

        // ===================================================================
        // (4) 선행 마진 패턴 소멸 + 플러시 산식 존재 (Figma GridRow 규약 —
        //     선행 마진 없음, 행은 콘텐츠 좌상단 플러시)
        // ===================================================================

        [Test]
        public void SlotWidth_Formula_IsFlushSum_WithoutLeadingMargin()
        {
            var src = Src();
            // 플러시 합산 산식 존재
            Assert.That(src, Does.Contain("(slotTotalWidth - SLOT_MARGIN * (GRID_COLUMNS - 1)) / GRID_COLUMNS"),
                "메인 그리드 슬롯 폭은 플러시 합산 (inner − 갭×(열수−1))/열수 여야 한다.");
            Assert.That(src, Does.Contain("(innerWidth - SLOT_MARGIN * (LOOT_COLUMNS - 1)) / LOOT_COLUMNS"),
                "전리품 그리드도 동일 플러시 산식이어야 한다.");
            Assert.That(src, Does.Contain("(innerWidth - EQUIP_CELL_GAP * (EQUIP_GRID_COLS - 1)) / EQUIP_GRID_COLS"),
                "장비칸 셀 폭도 동일 플러시 산식이어야 한다.");
            Assert.That(src, Does.Contain("(innerWidth - SLOT_MARGIN * (GRID_COLUMNS - 1)) / GRID_COLUMNS"),
                "창고 그리드도 메인과 동일 플러시 산식이어야 한다.");

            // 구 선행 마진/후행 갭 패턴 소멸
            Assert.That(src, Does.Not.Contain("SLOT_MARGIN * (GRID_COLUMNS + 1)"),
                "구 (열수+1) 선행 마진 산식은 폐기되어야 한다.");
            Assert.That(src, Does.Not.Contain("SLOT_MARGIN * (LOOT_COLUMNS + 1)"),
                "전리품 구 (열수+1) 산식은 폐기되어야 한다.");
            Assert.That(src, Does.Not.Contain("SLOT_MARGIN * (EQUIP_GRID_COLS + 1)"),
                "장비칸 구 (열수+1) 산식은 폐기되어야 한다.");
            Assert.That(src, Does.Not.Contain("float gx = SLOT_MARGIN + gCol"),
                "가이드 그리드 선행 마진은 폐기(플러시 시작)되어야 한다.");
            Assert.That(src, Does.Not.Contain("float sx = SLOT_MARGIN + col"),
                "아이템 슬롯 선행 마진은 폐기(플러시 시작)되어야 한다.");
            Assert.That(src, Does.Not.Contain("float cx = innerX + SLOT_MARGIN + col"),
                "장비칸 선행 마진은 폐기(플러시 시작)되어야 한다.");
            Assert.That(src, Does.Not.Contain("contentHeight = guideRows * rowHeight + SLOT_MARGIN"),
                "contentHeight 후행 갭 가산은 폐기되어야 한다.");
            Assert.That(src, Does.Contain("contentHeight = guideRows * rowHeight - SLOT_MARGIN"),
                "contentHeight는 후행 갭을 뺀 플러시 합산이어야 한다(Figma 섹션 446.4 규약).");
        }

        [Test]
        public void EquipmentGrid_UsesSectionPad_AndFigmaRowRhythm()
        {
            var src = Src();
            Assert.That(src, Does.Contain("float innerX = panelX + PANEL_CONTENT_PAD + EQUIP_SECTION_PAD;"),
                "장비칸 x는 패널 인셋 24 위에 섹션 인셋 14.4가 겹친 위치여야 한다.");
            Assert.That(src, Does.Contain("innerWidth = GRID_AREA_WIDTH - (PANEL_CONTENT_PAD + EQUIP_SECTION_PAD) * 2f"),
                "장비칸 가용폭은 (24+14.4) 좌우 대칭 플러시 기준이어야 한다(1080p: 551.2 → cellW 106.4).");
            Assert.That(src, Does.Contain("rowBlock = EQUIP_BADGE_HEIGHT + cellH + EQUIP_SECTION_PAD"),
                "장비칸 행 리듬은 섹션 인셋 14.4를 간격으로 사용해야 한다.");
            Assert.That(src, Does.Not.Contain("totalH = EQUIP_GRID_ROWS * rowBlock + SLOT_MARGIN"),
                "구 totalH 후행 갭 가산은 폐기되어야 한다.");
            Assert.That(src, Does.Contain("float totalH = EQUIP_GRID_ROWS * rowBlock;"),
                "totalH는 후행 갭 없는 플러시 합산이어야 한다(1080p: 2×94.92=189.84, 구 190.48).");
        }

        [Test]
        public void CategoryTabs_UseTabGap48_FlushSum()
        {
            var src = Src();
            Assert.That(src, Does.Contain("TAB_GAP * (tabNames.Length - 1)"),
                "탭 폭 산식에 TAB_GAP 4.8이 플러시 합산으로 반영되어야 한다.");
            Assert.That(src, Does.Contain("tx += tabWidth + TAB_GAP;"),
                "탭 x 진행은 탭폭+갭이어야 한다.");
            Assert.That(src, Does.Not.Contain("tabWidth - 2, TAB_BAR_HEIGHT"),
                "구 암시적 탭 갭 2(tabWidth−2)는 폐기되어야 한다.");
            Assert.That(src, Does.Not.Contain("float tabWidth = (WINDOW_WIDTH - 8) / tabNames.Length;"),
                "구 탭 폭 산식(WINDOW_WIDTH−8 균등 분할)은 폐기되어야 한다.");
        }

        [Test]
        public void Panels_UsePanelContentPad24_NoHardcodedInsets()
        {
            var src = Src();
            Assert.That(src, Does.Contain("float innerX = panelX + PANEL_CONTENT_PAD;"),
                "메인 그리드 x는 패널 콘텐츠 인셋 24여야 한다.");
            Assert.That(src, Does.Contain("innerWidth = GRID_AREA_WIDTH - PANEL_CONTENT_PAD * 2f"),
                "메인 그리드 가용폭은 좌우 인셋 24 기준이어야 한다(1080p: 580 → 슬롯 88.67).");
            Assert.That(src, Does.Contain("float sx = panelX + PANEL_CONTENT_PAD + col * (slotWidth + SLOT_MARGIN);"),
                "전리품 슬롯은 좌측 인셋 24 후 플러시여야 한다.");
            Assert.That(src, Does.Contain("new Rect(panelX + PANEL_CONTENT_PAD, gridY + 2, innerWidth, gridHeight - 4)"),
                "창고 스크롤뷰도 좌측 인셋 24를 써야 한다.");
            Assert.That(src, Does.Not.Contain("float innerX = panelX + 4;"),
                "구 +4 하드코딩 인셋은 폐기되어야 한다.");
            Assert.That(src, Does.Not.Contain("float tx = panelX + 4;"),
                "탭의 구 +4 하드코딩 시작도 tabInset으로 대체되어야 한다.");
            Assert.That(src, Does.Not.Contain("innerWidth = GRID_AREA_WIDTH - 8;"),
                "메인 그리드 구 −8 하드코딩 폭은 폐기되어야 한다.");
            Assert.That(src, Does.Not.Contain("float innerWidth = WINDOW_WIDTH - 8;"),
                "창고 그리드 구 −8 하드코딩 폭은 폐기되어야 한다.");
            Assert.That(src, Does.Not.Contain("float innerWidth = lootW - 8;"),
                "전리품 패널 구 −8 하드코딩 폭은 폐기되어야 한다.");
        }
    }
}
