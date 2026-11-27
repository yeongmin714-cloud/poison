using System.IO;
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [88차 B4 — 음식점 창 Figma 237:792 정합] 소스 문자열 어서션 테스트(RestaurantWindowUTK.cs).
    ///
    /// Figma 진실치(2026-10-16T01:01:11Z 개정본 노드 API 실측, 프레임 237:792 "음식점 판매 관리" 1600×1040)를
    /// 소스 상수/문자열로 고정한다 — 위조 금지 계약의 회귀 방지선.
    ///  · 창/패널 치수·좌표: 1600×1040 / 좌 480×882 @(64,78) / 우 968×882 @(568,78) / 헤더 @(64,36) / 도움말 @(64,984)
    ///  · 색: #13181F/#191F27/#0F141B/#20262E/#253346/#F0F6FC/#8B949E/#586473/#58A6FF/#30363D/#0B0E14/#65768A
    ///  · 열 x 14/394/500/610/680/768/888(리스트 로컬 — [144차 P7-RST #82]) · 행 920×74 · 진행바 66×3 · 지표 221×80 gap12
    ///  · 버튼 250×46·400×46 · 수량조절 116×32 · 탭 81.6×36 gap6 · 슬롯 80×86 행간10·열간8(3행) · 등급바 68×2
    ///  · '판매금 받기' / 'N / 6'(상한 6) / ApplyCanvasFit / PlacementAttempted 구독
    ///
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class RestaurantWindowFigmaTests
    {
        private const string SourcePath = "Assets/Scripts/UI/Toolkit/RestaurantWindowUTK.cs";

        private static string WindowSrc() => File.ReadAllText(SourcePath);

        // ═══════════════ 1. 창/패널 치수·좌표 + ApplyCanvasFit ═══════════════

        [Test]
        public void Window_Source_FigmaBoxesAndCanvasFit()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("[88차]"), "[88차] 진실치 마커 주석.");
            Assert.That(src, Does.Contain("WinW = 1600f").Or.Contain("WinH = 1040f"),
                "창 1600×1040 — Figma 프레임 237:792 실측.");
            Assert.That(src, Does.Contain("LeftW = 480f").Or.Contain("LeftH = 882f"),
                "좌 '음식 인벤토리' 패널 480×882.");
            Assert.That(src, Does.Contain("LeftX = 64f").Or.Contain("LeftY = 78f"),
                "좌 패널 @(64,78).");
            Assert.That(src, Does.Contain("RightW = 968f").Or.Contain("RightH = 882f"),
                "우 '음식점 배치' 패널 968×882.");
            Assert.That(src, Does.Contain("RightX = 568f").Or.Contain("RightY = 78f"),
                "우 패널 @(568,78).");
            Assert.That(src, Does.Contain("HeaderY = 36f"), "헤더 @(64,36) — HeaderY 진실치.");
            Assert.That(src, Does.Contain("HeaderX = 64f").Or.Contain("HeaderW = 1472f"),
                "헤더 1472×18 @(64,36).");
            Assert.That(src, Does.Contain("HelpY = 984f"), "도움말 @(64,984) — HelpY 진실치.");
            Assert.That(src, Does.Contain("HelpX = 64f").Or.Contain("HelpW = 1472f"),
                "도움말 1472×14 @(64,984).");
            Assert.That(src, Does.Contain("ApplyCanvasFit"), "ApplyCanvasFit 84차 등배수 창박스 계약.");
            Assert.That(src, Does.Contain("FigmaCanvasLayout.CanvasWidth").Or.Contain("FigmaCanvasLayout.CanvasHeight"),
                "k=min(rootW/1920, rootH/1080) — FigmaCanvasLayout 캔버스 기준 스케일.");
            Assert.That(src, Does.Contain("transformOrigin"), "내부 raw 유지 + transform.scale=k — transformOrigin (0,0).");
        }

        // ═══════════════ 2. 색/반경 진실치 ═══════════════

        [Test]
        public void Colors_Source_FigmaHexTruth()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("0x16, 0x1B, 0x22"), "[134차 C3 계약 진화] 패널 bg #161B22@0.80 — 플루언트 글래스(Figma 재실측).");
            Assert.That(src, Does.Contain("0x19, 0x1F, 0x27"), "표면 #191F27.");
            Assert.That(src, Does.Contain("0x0F, 0x14, 0x1B"), "입력/어두운 표면 #0F141B.");
            Assert.That(src, Does.Contain("0x20, 0x26, 0x2E"), "슬롯 #20262E.");
            Assert.That(src, Does.Contain("0x25, 0x33, 0x46"), "선택 슬롯 #253346.");
            Assert.That(src, Does.Contain("0xF0, 0xF6, 0xFC"), "본문 텍스트 #F0F6FC.");
            Assert.That(src, Does.Contain("0x8B, 0x94, 0x9E"), "보조 텍스트 #8B949E.");
            Assert.That(src, Does.Contain("0x58, 0x64, 0x73"), "구분자/placeholder #586473.");
            Assert.That(src, Does.Contain("0x58, 0xA6, 0xFF"), "액센트 #58A6FF.");
            Assert.That(src, Does.Contain("0x30, 0x36, 0x3D"), "보더 #30363D.");
            Assert.That(src, Does.Contain("0x0B, 0x0E, 0x14"), "강조 버튼 글자 #0B0E14.");
            // [88차 수리] 소스 표기 준수 — GradeBarIdle은 나눗셈 표기(new Color(0x65 / 255f, ... / 255f, 0.4f))
            Assert.That(src, Does.Contain("0x65 / 255f, 0x76 / 255f, 0x8A / 255f, 0.4f"), "등급바 대기색 #65768A/0.4.");
            Assert.That(src, Does.Contain("AccentAlpha = 0.078f"), "액센트 배경 알파 0.078 — #58A6FF/0.078.");
            Assert.That(src, Does.Contain("RadiusPanel = 14f"), "radius 14(패널).");
            Assert.That(src, Does.Contain("RadiusCard = 10f"), "radius 10(카드).");
            Assert.That(src, Does.Contain("RadiusBox = 6f"), "radius 6(버튼·박스).");
        }

        // ═══════════════ 3. 우 패널 — 열 x/행/진행바/지표/수령 박스 ═══════════════

        [Test]
        public void RightPanel_Source_ColumnsRowsAndCollect()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("ColXName = 14f"), "열 x — 음식 14 (리스트 로컬 [144차 P7-RST #82]).");
            Assert.That(src, Does.Contain("ColXRecommended = 394f"), "열 x — 권장 가격 394.");
            Assert.That(src, Does.Contain("ColXSell = 500f"), "열 x — 판매 가격 500.");
            Assert.That(src, Does.Contain("ColXPlaced = 610f"), "열 x — 배치 610.");
            Assert.That(src, Does.Contain("ColXProgress = 680f"), "열 x — 판매 / 배치 680.");
            Assert.That(src, Does.Contain("ColXGold = 768f"), "열 x — 판매금 768.");
            Assert.That(src, Does.Contain("ColXManage = 888f"), "열 x — 관리 888 — 목록 우단 944 내부.");
            Assert.That(src, Does.Contain("ListH = 330f"), "목록 920×330.");
            Assert.That(src, Does.Contain("ColHeaderH = 34f"), "열헤더 920×34 #0F141B.");
            Assert.That(src, Does.Contain("StockRowH = 74f"), "행 920×74.");
            Assert.That(src, Does.Contain("ProgressW = 66f").Or.Contain("ProgressH = 3f"),
                "진행바 66×3 (#20262E→채움 #58A6FF r2).");
            Assert.That(src, Does.Contain("MetricW = 221f").Or.Contain("MetricH = 80f"),
                "지표 카드 221×80.");
            Assert.That(src, Does.Contain("MetricGap = 12f"), "지표 카드 gap12.");
            Assert.That(src, Does.Contain("NoticeH = 42f"), "추가 배치 안내 920×42.");
            Assert.That(src, Does.Contain("CollectBoxH = 92f"), "판매금 수령 920×92.");
            Assert.That(src, Does.Contain("CollectButtonW = 250f").Or.Contain("CollectButtonH = 46f"),
                "수령 버튼 250×46 #58A6FF r6.");
            Assert.That(src, Does.Contain("판매금 받기"), "수령 버튼 문구 — '판매금 받기'.");
            Assert.That(src, Does.Contain("음식점 배치"), "우 패널 제목 — '음식점 배치'.");
            Assert.That(src, Does.Contain("판매할 음식"), "목록 섹션 제목 — '판매할 음식'.");
            Assert.That(src, Does.Contain("전체 판매 요약"), "정산 섹션 제목 — '전체 판매 요약'.");
            Assert.That(src, Does.Contain("받을 수 있는 판매금"), "수령 박스 문구 — '받을 수 있는 판매금 N G'.");
            Assert.That(src, Does.Contain("영업 중").Or.Contain("영업 종료"),
                "영업 배지 문구 — TimeManager.Hour 8~22 실측 판정.");
        }

        // ═══════════════ 4. 좌 패널 — 탭/슬롯/수량조절/실행 버튼 ═══════════════

        [Test]
        public void LeftPanel_Source_TabsSlotsAndActions()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("TabW = 81.6f").Or.Contain("TabH = 36f"),
                "분류 탭 81.6×36 r6.");
            Assert.That(src, Does.Contain("TabGap = 6f"), "탭 gap6.");
            Assert.That(src, Does.Contain("SlotW = 80f").Or.Contain("SlotH = 86f"),
                "슬롯 80×86 r6.");
            Assert.That(src, Does.Contain("SlotGap = 10f"), "[134차 F 계약 진화] 슬롯 행간격 10 — Figma 237:792 재실측 진실치.");
            Assert.That(src, Does.Contain("SlotColGap = 8f"), "[144차 P7-RST #36] 슬롯 열간격 8 — 피치 88, 5열=5×80+4×8=432(237:830 gap8).");
            Assert.That(src, Does.Contain("SlotCols = 5").Or.Contain("SlotRows = 3"),
                "슬롯 5열×3행.");
            Assert.That(src, Does.Contain("SlotGradeBarW = 68f").Or.Contain("SlotGradeBarH = 2f"),
                "등급바 68×2 (x+6, y).");
            Assert.That(src, Does.Contain("NoteY = 502f"), "안내 문구 @(24,502).");
            Assert.That(src, Does.Contain("SelectBoxW = 432f").Or.Contain("SelectBoxH = 213f"),
                "선택 음식 배치 432×213.");
            Assert.That(src, Does.Contain("StepperW = 116f").Or.Contain("StepperH = 32f"),
                "수량조절 116×32 (−/N/+) — rel 158.5.");
            Assert.That(src, Does.Contain("PlaceButtonW = 400f").Or.Contain("PlaceButtonH = 46f"),
                "실행 버튼 400×46.");
            Assert.That(src, Does.Contain("UsageBarW = 432f").Or.Contain("UsageBarH = 4f"),
                "사용량 바 432×4.");
            Assert.That(src, Does.Contain("인벤토리"), "좌 패널 제목 — '인벤토리'.");
            Assert.That(src, Does.Contain("음식 이름 검색"), "검색 placeholder — '음식 이름 검색'.");
            Assert.That(src, Does.Contain("판매 가능한 음식"), "그리드 섹션 제목 — '판매 가능한 음식'.");
            Assert.That(src, Does.Contain("배치 수량"), "수량행 라벨 — '배치 수량'.");
            Assert.That(src, Does.Contain("개 추가 배치"), "실행 버튼 문구 — 'N개 추가 배치'.");
            Assert.That(src, Does.Contain("판매대에 배치되지 않은 보유 수량입니다"),
                "슬롯 하단 안내 문구 진실치.");
        }

        // ═══════════════ 5. 데이터 배선 — 위조 금지 실측 API ═══════════════

        [Test]
        public void DataWiring_Source_MeasuredApisOnly()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("RestaurantSystem.MaxStockSlots"),
                "'N / 6' 상한 — RestaurantSystem.MaxStockSlots(=6) 실측 단일 소스.");
            Assert.That(src, Does.Contain("GetPendingRevenue"),
                "총 판매금 지표 — VillageDrugState.GetPendingRevenue 실측.");
            Assert.That(src, Does.Contain("CollectRevenue"),
                "'판매금 받기' → RestaurantSystem.CollectRevenue 실측 경로.");
            Assert.That(src, Does.Contain("RestockFromPlayer").Or.Contain("out string reason"),
                "'N개 추가 배치' → RestockFromPlayer out reason 오버로드(사유 토스트 계약).");
            Assert.That(src, Does.Contain("VillageDrugMath.RecommendedPrice"),
                "마약 권장가 — VillageDrugMath.RecommendedPrice(basePrice, addiction) 순수 수학.");
            Assert.That(src, Does.Contain("SetRegisteredPrice"),
                "마약 행 판매 가격 — VillageDrugState.SetRegisteredPrice(기존 계약 유지).");
            Assert.That(src, Does.Contain("GetSlotsByCategory"),
                "좌 슬롯 — PlayerInventory.GetSlotsByCategory(Food)+Drug 실측.");
            Assert.That(src, Does.Contain("GetAllSlots"),
                "'N / 40 슬롯' — GetAllSlots 배열 길이(=_maxSlots 40 실측, Figma 목업치 15 미채용).");
            Assert.That(src, Does.Contain("GetFormattedTime"),
                "헤더 'HH:MM' — TimeManager.GetFormattedTime 실측(73차 수면 패널 선례).");
            Assert.That(src, Does.Contain("hour >= 8 && hour < 22"),
                "영업 배지 판정 — Hour 8≤h<22 실측 게이트.");
            Assert.That(src, Does.Contain("PollMs = 1000"),
                "폴링 1000ms 유지 — 수령/배치 직후 즉시 갱신과 병행.");
            Assert.That(src, Does.Contain("RestaurantPlacementMarker.PlacementAttempted"),
                "PlacementAttempted 구독 — Phase A 정적 이벤트(구독 전 해제 패턴).");
            Assert.That(src, Does.Contain("UTKToastService.Show"),
                "토스트 연계 — UTKToastService.Show(message).");
            Assert.That(src, Does.Contain("OpenRequestedUTK"),
                "진입점 유지 — RestaurantPropInteractable.OpenRequestedUTK 구독(시그니처 불변).");
            Assert.That(src, Does.Contain("KeyDownEvent"),
                "단축키 — KeyDownEvent 경유(창 표시 중만).");
            Assert.That(src, Does.Contain("KeyCode.Escape").Or.Contain("KeyCode.Return"),
                "Enter=배치 실행 / Esc=창 닫기 계약.");
            Assert.That(src, Does.Contain("drugged_"),
                "Drug 카테고리 drugged_ 접두 실측 규약(CookingDrugHelper/D3-A).");
            Assert.That(src, Does.Contain("IsElementOrChildFocused"),
                "가격 TextField 편집 중 갱신 스킵 가드 — 기존 계약(포커스 중 덮어쓰기 방지) 유지.");
        }

        // ═══════════════ [151차 P0] 가격 필드 키보드 전달 수리 계약 ═══════════════

        [Test]
        public void PriceFieldKeyboardBridge_SourceContract_151()
        {
            string src = WindowSrc();

            // 근거 계약 — 전달 계층 차단(레거시 StandaloneInputModule + 상시 OnGUI 오버레이의 IMGUI 키 큐
            // 선소비)으로 UITK 패널에 문자 KeyDownEvent가 미전달([150차 P4] FocusIn 2회/키다운 0회 실측 일치).
            Assert.That(src, Does.Contain("Keyboard.current.onTextInput"),
                "[151차 P0] 최후수단 브리지 — onTextInput 구독(가격 필드 편집 중 스코프드 주입).");
            Assert.That(src, Does.Contain("AttachPriceKeyboardBridge"),
                "[151차 P0] FocusIn 경유 브리지 부착.");
            Assert.That(src, Does.Contain("DetachPriceKeyboardBridge"),
                "[151차 P0] FocusOut/창닫기/자가점검 경유 브리지 해제(멱등 — 구독 전 해제 패턴 준용).");
            Assert.That(src, Does.Contain("backspaceKey.wasPressedThisFrame"),
                "[151차 P0] 백스페이스 수리 — 브리지 틱 경유 값 삭제.");
            Assert.That(src, Does.Contain("enterKey.wasPressedThisFrame"),
                "[151차 P0] Enter 커밋 수리 — Blur → FocusOut 커밋 단일 경로.");

            // 자가 은퇴 프로브 — 실 KeyDownEvent 도달 시 즉시 해제(전달 경로 정상화 시 이중 입력 방지).
            Assert.That(src, Does.Contain("브리지 자가 은퇴"),
                "[151차 P0] 실 KeyDown 도달 감지 → 브리지 자가 은퇴 계약.");

            // 기존 Enter 커밋 KeyDown 핸들러 유지 — StopPropagation 조건부(전달 경로 복원 대비).
            Assert.That(src, Does.Contain("evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter"),
                "가격 필드 Enter 커밋 핸들러 유지(StopPropagation 조건부).");

            // 150차 임시 진단 로그 은퇴 — [151차 P0] 브리지 계약으로 대체(회귀 방지선).
            Assert.That(src, Does.Not.Contain("[150차][P4]"),
                "150차 진단 콜백 은퇴 — 브리지 계약으로 대체됨.");
        }

        [Test]
        public void ShortcutTextFieldGuards_SourceContract_151()
        {
            string src = WindowSrc();

            // Esc 분기 가드 — 가격 편집 중 창 닫기 오발 방지(Enter 분기 기존 가드 준용).
            int escIdx = src.IndexOf("evt.keyCode == KeyCode.Escape", System.StringComparison.Ordinal);
            Assert.That(escIdx, Is.GreaterThan(0), "Esc 단축키 분기 존재.");
            int escGuard = src.IndexOf("if (IsAnyTextFieldFocused()) return;", escIdx, System.StringComparison.Ordinal);
            Assert.That(escGuard, Is.GreaterThan(escIdx),
                "[151차 P0] Esc 분기 TextField 가드 — 가격 편집 중 오닫기 방지.");

            // 폴링 재빌드 스킵 가드 유지 — 기존 계약(포커스 중 우패널 재빌드 방지) 회귀 방지선.
            Assert.That(src, Does.Contain("if (!IsElementOrChildFocused(_stockList))"),
                "가격 TextField 편집 중 폴링 재빌드 스킵 가드 유지.");
        }
    }
}
