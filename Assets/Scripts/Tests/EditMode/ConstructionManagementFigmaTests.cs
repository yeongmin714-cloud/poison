using System.IO;
using NUnit.Framework;
using ProjectName.UI.Toolkit;     // ConstructionWindowUTK — 순수 표시 헬퍼/개방 API (RestaurantWindowTests 선례)

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [90차 B3 — 건설 관리 창 Figma 239:683 정합] 소스 문자열 어서션 테스트(ConstructionWindowUTK.cs).
    ///
    /// Figma 진실치(프레임 239:683 "전초기지 건설 관리" 1600×1040)를 소스 상수/문자열로 고정한다 —
    /// 위조 금지 계약의 회귀 방지선(RestaurantWindowFigmaTests 88차 선례).
    ///  · 창/패널 치수·좌표: 1600×1040 / 좌 '건설 목록' 480×882 @(64,78) / 우 '건설 정보' 968×882 @(568,78) / 헤더 @(64,36) / 도움말 @(64,984)
    ///  · 색: #13181F/#191F27/#0F141B/#20262E/#1A2839/#252D38/#F0F6FC/#8B949E/#586473/#58A6FF/#E5AD65/#30363D/#0B0E14
    ///  · 필터 탭 140×36 gap6 / 시설 항목 432×101 gap8 / 배지 76×21 / 재료 요약 5칸 81.6×70 gap6
    ///  · 개요 920×158 / 조건 카드 298.7×60 gap12 / 재료 표 920×288(열헤더 32 + 행 64, 열 x 606/966/1082/1198/1344)
    ///  · 실행 920×106 · '건설하기' 224×42 · '재료 획득처' 108×42 / '유휴 인원'(A5 실측 연계) / Enter-Esc
    ///  · 기타 탭(기존 O8 8종 보존) / FacilityOpenRequested 구독 + 5종 창 라우팅 매핑(training_hall 포함)
    ///
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class ConstructionManagementFigmaTests
    {
        private const string SourcePath = "Assets/Scripts/UI/Toolkit/ConstructionWindowUTK.cs";
        private const string HologramSourcePath = "Assets/Scripts/Systems/ConstructionHologram.cs";

        private static string WindowSrc() => File.ReadAllText(SourcePath);
        private static string HologramSrc() => File.ReadAllText(HologramSourcePath);

        // ═══════════════ 1. 창/패널 치수·좌표 + ApplyCanvasFit ═══════════════

        [Test]
        public void Window_Source_FigmaBoxesAndCanvasFit()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("[90차"), "[90차] 진실치 마커 주석.");
            Assert.That(src, Does.Contain("WinW = 1600f").Or.Contain("WinH = 1040f"),
                "창 1600×1040 — Figma 프레임 239:683 실측.");
            Assert.That(src, Does.Contain("LeftW = 480f").Or.Contain("LeftH = 882f"),
                "좌 '건설 목록' 패널 480×882.");
            Assert.That(src, Does.Contain("LeftX = 64f").Or.Contain("LeftY = 78f"),
                "좌 패널 @(64,78).");
            Assert.That(src, Does.Contain("RightW = 968f").Or.Contain("RightH = 882f"),
                "우 '건설 정보' 패널 968×882.");
            Assert.That(src, Does.Contain("RightX = 568f").Or.Contain("RightY = 78f"),
                "우 패널 @(568,78).");
            Assert.That(src, Does.Contain("HeaderY = 36f"), "헤더 @(64,36) — HeaderY 진실치.");
            Assert.That(src, Does.Contain("HeaderX = 64f").Or.Contain("HeaderW = 1472f"),
                "헤더 1472×18 @(64,36).");
            Assert.That(src, Does.Contain("HelpY = 984f"), "도움말 @(64,984) — HelpY 진실치.");
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

            Assert.That(src, Does.Contain("0x13, 0x18, 0x1F"), "패널 bg #13181F.");
            Assert.That(src, Does.Contain("0x19, 0x1F, 0x27"), "표면 #191F27.");
            Assert.That(src, Does.Contain("0x0F, 0x14, 0x1B"), "어두운 표면 #0F141B.");
            Assert.That(src, Does.Contain("0x20, 0x26, 0x2E"), "아이콘 박스 #20262E.");
            Assert.That(src, Does.Contain("0x1A, 0x28, 0x39"), "선택 항목 bg #1A2839.");
            Assert.That(src, Does.Contain("0x25, 0x2D, 0x38"), "비활성 버튼 #252D38.");
            Assert.That(src, Does.Contain("0xF0, 0xF6, 0xFC"), "본문 텍스트 #F0F6FC.");
            Assert.That(src, Does.Contain("0x8B, 0x94, 0x9E"), "보조 텍스트 #8B949E.");
            Assert.That(src, Does.Contain("0x58, 0x64, 0x73"), "잠금/구분자 #586473.");
            Assert.That(src, Does.Contain("0x58, 0xA6, 0xFF"), "액센트 #58A6FF.");
            Assert.That(src, Does.Contain("0xE5, 0xAD, 0x65"), "경고(재료 부족) #E5AD65.");
            Assert.That(src, Does.Contain("0x30, 0x36, 0x3D"), "보더 #30363D.");
            Assert.That(src, Does.Contain("0x0B, 0x0E, 0x14"), "강조 버튼 글자 #0B0E14.");
            Assert.That(src, Does.Contain("AccentAlpha = 0.078f"), "액센트 배경 알파 0.078 — #58A6FF/0.078.");
            Assert.That(src, Does.Contain("WarnAlpha = 0.063f"), "경고 배경 알파 0.063 — #E5AD65/0.063.");
            Assert.That(src, Does.Contain("WarnStrokeAlpha = 0.22f"), "부족 행/박스 stroke 0.22.");
            Assert.That(src, Does.Contain("AccentStrokeAlpha = 0.25f"), "실행 박스 충족 stroke 0.25.");
            Assert.That(src, Does.Contain("RadiusPanel = 14f"), "radius 14(패널).");
            Assert.That(src, Does.Contain("RadiusCard = 10f"), "radius 10(카드).");
            Assert.That(src, Does.Contain("RadiusBox = 6f"), "radius 6(버튼·박스).");
        }

        // ═══════════════ 3. 좌 패널 — 필터 탭/시설 항목/재료 요약/기타 탭 ═══════════════

        [Test]
        public void LeftPanel_Source_FilterTabsItemsAndSummary()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("건설 목록"), "좌 패널 제목 — '건설 목록'.");
            Assert.That(src, Does.Contain("전초기지 시설"), "좌 패널 부제 — '전초기지 시설'.");
            Assert.That(src, Does.Contain("FilterTabW = 140f").Or.Contain("FilterTabH = 36f"),
                "필터 탭 140×36 r6.");
            Assert.That(src, Does.Contain("FilterTabGap = 6f"), "필터 탭 gap6.");
            Assert.That(src, Does.Contain("FilterTabY = 88f"), "필터 탭 @(88,166) figma → 로컬 y 88.");
            Assert.That(src, Does.Contain("전체 "), "필터 탭 '전체 N' — 활성 #58A6FF·글자 #0B0E14.");
            Assert.That(src, Does.Contain("건설 가능 "), "필터 탭 '건설 가능 N'.");
            Assert.That(src, Does.Contain("재료 부족 "), "필터 탭 '재료 부족 M'.");
            Assert.That(src, Does.Contain("기타 "), "필터 탭 '기타 8' — 신규 보존 탭(기존 O8 8종).");
            Assert.That(src, Does.Contain("FacilityItemW = 432f").Or.Contain("FacilityItemH = 101f"),
                "시설 항목 432×101 r6.");
            Assert.That(src, Does.Contain("FacilityItemGap = 8f"), "시설 항목 gap8 @(88,246) — 로컬 y 168.");
            Assert.That(src, Does.Contain("FacilityListY = 168f"), "시설 항목 y 246 figma → 로컬 168.");
            Assert.That(src, Does.Contain("ItemIconSize = 36f"), "항목 아이콘 36×36 #20262E r6.");
            Assert.That(src, Does.Contain("StatusBadgeW = 76f").Or.Contain("StatusBadgeH = 21f"),
                "상태 배지 76×21.");
            Assert.That(src, Does.Contain("건설 가능\""), "배지 문구 '건설 가능'(#58A6FF + check).");
            Assert.That(src, Does.Contain("재료 부족\""), "배지 문구 '재료 부족'(#E5AD65 + circle-alert).");
            Assert.That(src, Does.Contain("SummaryBoxW = 432f").Or.Contain("SummaryBoxH = 110f"),
                "보유 재료 요약 432×110 @(88,826) — 로컬 y 748.");
            Assert.That(src, Does.Contain("SummaryBoxY = 748f"), "보유 재료 요약 y 826 figma → 로컬 748.");
            Assert.That(src, Does.Contain("SummaryCellW = 81.6f").Or.Contain("SummaryCellH = 70f"),
                "요약 5칸 81.6×70 gap6 @(88,866).");
            Assert.That(src, Does.Contain("SummaryCellGap = 6f"), "요약 칸 gap6.");
            Assert.That(src, Does.Contain("SummaryCellY = 40f"), "요약 칸 y 866 figma → 박스 로컬 40.");
            Assert.That(src, Does.Contain("공용 창고"), "요약 제목 — '공용 창고'.");
            Assert.That(src, Does.Contain("건설 전 보유량"), "요약 부제 — '건설 전 보유량'.");
            // 보유량 실측 — Figma 목업 보유치(180/120/24/18/8) 미채용, GetItemCount 단일소스.
            Assert.That(src, Does.Contain("GetItemCount"), "보유량 = PlayerInventory.GetItemCount 실측(위조 금지).");
            Assert.That(src, Does.Not.Contain("나무 180").And.Not.Contain("180/60"),
                "Figma 목업 수치(180/120/24/18/8) 미채용 가드.");
        }

        [Test]
        public void OthersTab_Source_LegacyBlueprintsPreserved()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("FacilityTab.Others"), "기타 탭 enum — 기존 8종 보존.");
            Assert.That(src, Does.Contain("BuildOtherItem"), "기타 항목 빌더 — 시설 항목 스타일 재사용.");
            Assert.That(src, Does.Contain("IsFacilityBlueprint(def.id)"), "기타 탭 = 시설 6종 제외 나머지([152차 P0] 훈련소 포함).");
            Assert.That(src, Does.Contain("설치 완료").Or.Contain("미설치"),
                "기타 항목 배지 = 설치 여부 표기.");
            Assert.That(src, Does.Contain("재료 요구 없음"), "기타 항목 재료 표기 없음.");
            Assert.That(src, Does.Contain("OnPlaceBlueprint(def.id)"), "기타 항목 배치 버튼 — 기존 OnPlaceBlueprint 흐름.");
            Assert.That(src, Does.Contain("BlueprintData.Count - FacilityIds.Length"),
                "기타 탭 카운트 = 전체 14 − 시설 6 = 8 실측. [152차 P0]");
        }

        // ═══════════════ 4. 우 패널 — 개요/조건 카드/재료 표(열 x)/실행 박스 ═══════════════

        [Test]
        public void RightPanel_Source_OverviewConditionsAndMaterialTable()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("건설 정보"), "우 패널 제목 — '건설 정보'.");
            Assert.That(src, Does.Contain("선택한 시설의 재료 및 조건"), "우 패널 부제.");
            Assert.That(src, Does.Contain("OverviewW = 920f").Or.Contain("OverviewH = 158f"),
                "선택 시설 개요 920×158.");
            Assert.That(src, Does.Contain("BlueprintBoxW = 234f").Or.Contain("BlueprintBoxH = 98f"),
                "설계도 박스 234×98 #0F141B r6.");
            Assert.That(src, Does.Contain("FACILITY / LV.1"), "설계도 박스 라벨 'FACILITY / LV.1' 8/400.");
            // [144차 P7-CON #50] Figma 239:824 부지 표기 텍스트 '3 × 3' — '부지 ' 접두어 폐기(같은 차 갱신).
            Assert.That(src, Does.Contain("_bpFootprintLabel.text = FormatFootprint(def.footprintRadius)"),
                "설계도 박스 부지 '3 × 3' 9/400 #58A6FF @박스로컬 (199,80) — 접두어 폐기(#50).");
            Assert.That(src, Does.Contain("ConditionCardW = 298.7f").Or.Contain("ConditionCardH = 60f"),
                "건설 조건 3카드 298.7×60.");
            Assert.That(src, Does.Contain("ConditionGap = 12f"), "조건 카드 gap12.");
            Assert.That(src, Does.Contain("건설 시간"), "조건 카드 1 — '건설 시간' mm:ss.");
            Assert.That(src, Does.Contain("필요 부지"), "조건 카드 2 — '필요 부지' '{N} × {N} 칸'.");
            Assert.That(src, Does.Contain("필요 작업 인원"), "조건 카드 3 — '필요 작업 인원' '1 명 · 유휴 인원 N명'.");
            Assert.That(src, Does.Contain("유휴 인원"), "유휴 인원 문구 — [90차 A5] 가용 병사 실측 연계.");
            Assert.That(src, Does.Contain("필요 재료"), "재료 표 섹션 제목 — '필요 재료'.");
            Assert.That(src, Does.Contain("MaterialTableW = 920f").Or.Contain("MaterialTableH = 288f"),
                "필요 재료 표 920×288 r10.");
            Assert.That(src, Does.Contain("MatColHeaderH = 32f"), "열헤더 920×32 #0F141B.");
            Assert.That(src, Does.Contain("MaterialRowH = 64f"), "행 920×64.");
            // 열 x — [144차 P7-CON #59] Figma 열 제목(239:849~853) 매핑: 캔버스 606=재료(헤더 리터럴 14),
            // 966=보유량 / 1082=필요량 / 1198=잔여 / 1344=상태 (패널 로컬 = −592 → 14/374/490/606/752).
            // 구 어서션(606=보유량 …)은 한 열씩 어긋난 구 구현을 고정하고 있었던 것 — 소스 재매핑과 동일 차 갱신.
            Assert.That(src, Does.Contain("(\"재료\", 14f)"), "열 x 재료 — Figma 606(로컬 14), '보유량'과 x=14 겹침 폐기(#59).");
            Assert.That(src, Does.Contain("966f - MatTableCanvasX"), "열 x 보유량 — Figma 966(로컬 374) [#59/#65].");
            Assert.That(src, Does.Contain("1082f - MatTableCanvasX"), "열 x 필요량 — Figma 1082(로컬 490) [#59/#66].");
            Assert.That(src, Does.Contain("1198f - MatTableCanvasX"), "열 x 건설 후 잔여 — Figma 1198(로컬 606) [#59/#67].");
            Assert.That(src, Does.Contain("1344f - MatTableCanvasX"), "열 x 충족 상태 — Figma 1344(로컬 752) [#59].");
            Assert.That(src, Does.Not.Contain("ColXSource"), "예비 열 상수 폐기 — 5열 체계 확정(#59).");
            // [144차 P7-CON] P0 수리 고정 — #11 좌 닫기 제거 / #48 설계도 벡터 / #52 조건 카드 아이콘 /
            // #56 충족 수(실데이터) / #70 부족 안내 아이콘 / #81 유의사항 info 아이콘.
            Assert.That(src, Does.Not.Contain("\"LeftCloseButton\""), "[144차 P7-CON #11] 좌 닫기 버튼 제거 — Figma 239:696 무닫기(우측 닫기 유지).");
            Assert.That(src, Does.Contain("BlueprintGridRect").And.Contain("BlueprintIsoRect"),
                "[144차 P7-CON #48] 설계 그리드 234×98 @0.047 + 입체 설계 160×88 w1.3 벡터 프레임.");
            Assert.That(src, Does.Contain("BuildGridGlyph").And.Contain("BuildUsersGlyph").And.Contain("BuildClockGlyph(19f)"),
                "[144차 P7-CON #52] 조건 카드 아이콘군 timer/grid-3x3/users 19×19 @(12,21) 글리프 근사.");
            Assert.That(src, Does.Contain("_matSatisfyLabel"), "[144차 P7-CON #56] 충족 수 라벨 'N / M' — 실데이터(IsMaterialSufficient 합산).");
            Assert.That(src, Does.Not.Contain("\"3 / 4\""), "[144차 P7-CON #56] 고정 가짜값 '3 / 4' 금지 — 실데이터 산출.");
            Assert.That(src, Does.Contain("BuildInfoGlyph(13f)"), "[144차 P7-CON #81] 유의사항 info 아이콘 13×13 @(24,843).");
            Assert.That(src, Does.Contain("BuildAlertGlyph(15f, Warn)"), "[144차 P7-CON #70] 부족 안내 circle-alert 15×15 @(16,21) #E5AD65.");
            Assert.That(src, Does.Contain("MatIconSize = 32f"), "행 아이콘 32×32.");
            Assert.That(src, Does.Contain("충분"), "충족 상태 check '충분' #58A6FF.");
            Assert.That(src, Does.Contain("개 부족"), "부족 상태 circle-alert 'N개 부족' #E5AD65.");

            // [144차 P7-CON] P1 후반 수리 고정 — #40/#51/#58 크롬 스트로크 / #41~#46 개요 배열 / #43 배지 (184,32) /
            // #47 설계도 x670 / #49/#50 설계도 표기 / #53/#54 조건 텍스트 x43 / #61 충분 행 / #68 상태 열 /
            // #75/#76/#77 불가 버튼 / #79 충족·미충족 병기.
            Assert.That(src, Does.Contain("ApplyElementBorder(overview)").And.Contain("ApplyElementBorder(tableHost)"),
                "[144차 P7-CON #40/#58] 개요·재료 표 stroke #30363D/0.502 신설(V2 크롬).");
            Assert.That(src, Does.Contain("ApplyElementBorder(card)"), "[144차 P7-CON #51] 조건 카드 stroke #30363D/0.502 신설.");
            Assert.That(src, Does.Contain("_selBadgeHost.style.left = 184f"), "[144차 P7-CON #43] 개요 배지 @개요로컬 (184,32) — 이름행 하단 (24,48) 폐기.");
            Assert.That(src, Does.Contain("bpBox.style.left = 670f"), "[144차 P7-CON #47] 설계도 박스 @개요로컬 x670 — 구 662 폐기.");
            Assert.That(src, Does.Contain("cardTitle.style.left = 43f"), "[144차 P7-CON #53/#54] 조건 항목/수치 @카드로컬 x43 — 아이콘(12..31) 우 배치.");
            Assert.That(src, Does.Contain("sufficient ? SurfaceCol : WarnBg"), "[144차 P7-CON #61] 충분 행 fill #191F27 + 크롬 보더(투명 무보더 폐기).");
            Assert.That(src, Does.Contain("BuildCheckGlyph(13f, Accent)"), "[144차 P7-CON #68] 상태 열 check 13×13 + 11/500(구 10×10 13/400 폐기).");
            Assert.That(src, Does.Contain("BuildLockGlyph(17f)"), "[144차 P7-CON #76] lock-keyhole 17×17 @(42,13) — 구 12 @(74,15) 폐기.");
            Assert.That(src, Does.Contain("13f, 700, TextDim, \"DisabledBuildLabel\""), "[144차 P7-CON #77] 불가 버튼 문구 13/700 #586473 — 구 12/500 폐기.");
            Assert.That(src, Does.Contain("_checkWarnLabel"), "[144차 P7-CON #79] 미충족 조건 병기 라인 '! 재료 N종 부족' @패널로컬 x185.");
        }

        [Test]
        public void ExecuteBox_Source_BuildAndShortageStates()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("ExecuteW = 920f").Or.Contain("ExecuteH = 106f"),
                "건설 실행 920×106 @(592,796) — 로컬 y 718.");
            Assert.That(src, Does.Contain("ExecuteY = 718f"), "실행 박스 y 796 figma → 로컬 718.");
            Assert.That(src, Does.Contain("BuildButtonW = 224f").Or.Contain("BuildButtonH = 42f"),
                "'건설하기' 224×42 #58A6FF r6 글자 #0B0E14.");
            Assert.That(src, Does.Contain("건설하기"), "실행 버튼 문구 — '건설하기'.");
            Assert.That(src, Does.Contain("SourceButtonW = 108f").Or.Contain("SourceButtonH = 42f"),
                "'재료 획득처' 108×42 #58A6FF/0.078 r6.");
            Assert.That(src, Does.Contain("재료 획득처"), "획득처 버튼 문구 — '재료 획득처' (클릭 → 토스트 안내).");
            Assert.That(src, Does.Contain("건설 불가 · 재료 부족"), "비활성 버튼 224×42 #252D38 r6 lock — '건설 불가 · 재료 부족'.");
            Assert.That(src, Does.Contain("개가 더 필요합니다"), "사유 문구 '{재료} {N}개가 더 필요합니다' 14/700.");
            // [144차 P7-CON #72] Figma 239:905 '철광산에서 재료를 확보하면 건설할 수 있습니다.'(동적 예시) —
            // 정적 문구 폐기, 실측 획득처 조립 동적 문구(FormatResolveHint)로 갱신(같은 차).
            Assert.That(src, Does.Contain("FormatResolveHint").And.Contain("확보하면 건설할 수 있습니다"),
                "해결 방법 11/400 — 동적 문구(239:905 실측 구현, #72).");
            Assert.That(src, Does.Contain("부지 확보").Or.Contain("작업 인원 충분"),
                "조건 확인 '✓ 부지 확보 ✓ 작업 인원 충분' 10/400.");
            Assert.That(src, Does.Contain("재료가 즉시 차감됩니다"), "유의사항 '건설 시작 시 재료가 즉시 차감됩니다…' 11/400.");
            Assert.That(src, Does.Contain("UTKToastService.Show(result)"),
                "건설하기 결과 토스트 — TryPlace 반환 메시지 그대로.");
            Assert.That(src, Does.Contain("BuildMaterialSourceToast"), "획득처 안내 토스트 — 실측 아이템 description 기반 문구.");
        }

        // ═══════════════ 5. 데이터 규약 — 실측 단일소스(위조 금지) ═══════════════

        [Test]
        public void DataSource_IsMeasuredSingleSource()
        {
            string src = WindowSrc();

            // 재료 id — ConstructionManager 상수 단일소스([90차 A1] 실측)
            Assert.That(src, Does.Contain("ConstructionManager.MaterialWoodItemId"), "나무 id 단일소스(mat_wood).");
            Assert.That(src, Does.Contain("ConstructionManager.MaterialStoneItemId"), "돌 id 단일소스(mat_stone).");
            Assert.That(src, Does.Contain("ConstructionManager.MaterialIronItemId"), "철광석 id 단일소스(iron_ore — [90차 A1]).");
            Assert.That(src, Does.Contain("ConstructionManager.MaterialLeatherItemId"), "가죽 id 단일소스(mat_boar_leather).");
            Assert.That(src, Does.Contain("ConstructionManager.MaterialManaItemId"), "수정석 id 단일소스(crystal_shard).");
            // 충족 판정 — HasEnoughMaterials 10인자 규약 단일소스
            Assert.That(src, Does.Contain("HasEnoughMaterials"), "충족 판정 = ConstructionManager.HasEnoughMaterials 10인자.");
            // 유휴 인원 — 가용 병사 실측(IsRecruited+IsAlive)
            Assert.That(src, Does.Contain("CountAvailableGuards"), "유휴 인원 = 가용 병사 실측.");
            Assert.That(src, Does.Contain("IsRecruited").Or.Contain("IsAlive"),
                "가용 병사 판정 — IsRecruited && IsAlive(GetAllPlayerGuards 소비처 실측 준용).");
            // 건설 시간/부지 — BlueprintDef 실측
            Assert.That(src, Does.Contain("def.buildTimeSeconds"), "건설 시간 = BlueprintDef.buildTimeSeconds.");
            Assert.That(src, Does.Contain("def.footprintRadius"), "부지 = BlueprintDef.footprintRadius 산출.");
        }

        // ═══════════════ 6. 시설 6종 데이터 + FacilityOpenRequested 라우팅 ═══════════════

        [Test]
        public void FacilityDataAndRouting_Source_SixItemsAndOpenMapping()
        {
            string src = WindowSrc();

            // 시설 6종 — Figma 시설 목록(요리/장비/음식점/병사배치/연금술) + [152차 P0] 훈련소
            Assert.That(src, Does.Contain("\"bp_cooking\""), "시설 1 — 요리 크래프트(bp_cooking).");
            Assert.That(src, Does.Contain("\"bp_forge\""), "시설 2 — 장비 크래프트(bp_forge).");
            Assert.That(src, Does.Contain("\"bp_restaurant\""), "시설 3 — 음식점(bp_restaurant).");
            Assert.That(src, Does.Contain("\"bp_barracks\""), "시설 4 — 병사배치(bp_barracks).");
            Assert.That(src, Does.Contain("\"bp_alchemy\""), "시설 5 — 연금술 크래프트(bp_alchemy).");
            Assert.That(src, Does.Contain("\"bp_traininghall\""), "시설 6 — 훈련소(bp_traininghall) [152차 P0].");
            Assert.That(src, Does.Contain("식사와 회복 음식 제작"), "역할 문구 — 요리 크래프트.");
            Assert.That(src, Does.Contain("무기와 방어구 제작"), "역할 문구 — 장비 크래프트.");
            Assert.That(src, Does.Contain("음식 판매 및 수익 창출"), "역할 문구 — 음식점.");
            Assert.That(src, Does.Contain("병력 주둔과 거점 수비"), "역할 문구 — 병사배치.");
            Assert.That(src, Does.Contain("물약과 강화 재료 제작"), "역할 문구 — 연금술 크래프트.");
            Assert.That(src, Does.Contain("병사 훈련과 전투 훈련"), "역할 문구 — 훈련소 [152차 P0].");
            Assert.That(src, Does.Contain("완공 후 E키로 시설을 사용하세요"), "운영 조건 실측 문구.");

            // [90차 B2] FacilityOpenRequested 구독(구독 전 해제) + effectId → 창 Open() 매핑
            Assert.That(src, Does.Contain("FacilityStationInteractable.FacilityOpenRequested -= OnFacilityOpenRequested"),
                "구독 전 해제 — 도메인 리로드 중복 방지(실측: 이벤트는 FacilityStationInteractable 소유).");
            Assert.That(src, Does.Contain("FacilityStationInteractable.FacilityOpenRequested += OnFacilityOpenRequested"),
                "FacilityOpenRequested 구독 — Systems→UI 순환참조 회피 정적 이벤트.");
            Assert.That(src, Does.Contain("case \"cooking_facility\": CookingWindowUTK.Open();"),
                "매핑 — cooking_facility → CookingWindowUTK.Open().");
            Assert.That(src, Does.Contain("case \"forge\": CraftingWindowUTK.Open();"),
                "매핑 — forge → CraftingWindowUTK.Open().");
            Assert.That(src, Does.Contain("case \"barracks\": AttackFormationUTK.Open();"),
                "매핑 — barracks → AttackFormationUTK.Open().");
            Assert.That(src, Does.Contain("case \"alchemy_facility\": AlchemyBenchUTK.Open();"),
                "매핑 — alchemy_facility → AlchemyBenchUTK.Open().");
        }

        // ═══════════════ 7. 배치 흐름/단축키/Updater 계약 유지 ═══════════════

        [Test]
        public void FlowAndShortcuts_Source_PlaceFlowEnterEscUpdater()
        {
            string src = WindowSrc();

            // 기존 배치 흐름 — GetPlacePosition → ResolveTerritoryAt → TryPlace
            Assert.That(src, Does.Contain("GetPlacePosition"), "배치 위치 — Camera.main forward×5(기존 계약).");
            Assert.That(src, Does.Contain("ResolveTerritoryAt"), "영지 자동 판정 — ResolveTerritoryAt(기존 계약).");
            // [104차 수리2] 배치 실행 — 즉시 TryPlace → 홀로그램 모드 전환(위치 선택 모드로 이관).
            Assert.That(src, Does.Contain("ConstructionHologram.BeginPlacement(blueprintId);"),
                "배치 실행 — 홀로그램 모드 전환(104차).");
            // 홀로그램 확정 경로 — 좌클릭 → ResolveTerritoryAt → TryPlace(기존 계약 경로 유지)
            string holo = HologramSrc();
            Assert.That(holo, Does.Contain("ResolveTerritoryAt"), "홀로그램 확정 — ResolveTerritoryAt(기존 계약).");
            Assert.That(holo, Does.Contain("TryPlace(territoryId, _blueprintId, pos)"), "홀로그램 확정 — TryPlace 기존 시그니처.");
            // 단축키 — Enter 건설 실행 / Esc 닫기 / U 토글(Updater 계약)
            Assert.That(src, Does.Contain("KeyCode.Return").Or.Contain("KeyCode.KeypadEnter"),
                "Enter = 선택 시설 건설 실행.");
            Assert.That(src, Does.Contain("KeyCode.Escape"), "Esc = 창 닫기.");
            Assert.That(src, Does.Contain("uKey.wasPressedThisFrame"), "U키 토글 — 기존 Updater 계약 유지.");
            Assert.That(src, Does.Contain("OnRootKeyDown"), "KeyDownEvent 진입 — IsOpen+TopWindow 게이트.");
            // Updater/구독 계약
            Assert.That(src, Does.Contain("RefreshInterval = 0.5f"), "폴링 500ms — 스펙 B1.");
            Assert.That(src, Does.Contain("StructureCompleted += OnStructureCompleted"), "StructureCompleted 구독.");
            Assert.That(src, Does.Contain("StructureCompleted -= OnStructureCompleted"), "StructureCompleted 해제(Subscribe/Unsubscribe 쌍).");
            // 진입점 — [90차 B2] SelectBlueprint 신설(음식점 자동 선택용)
            Assert.That(src, Does.Contain("public static void SelectBlueprint(string blueprintId)"),
                "SelectBlueprint(string) 신설 — AttackFormationUTK '음식점 배치' 진입점.");
            Assert.That(src, Does.Contain("IsFacilityBlueprint(blueprintId) ? FacilityTab.All : FacilityTab.Others"),
                "SelectBlueprint — 시설이면 전체 탭/기타면 기타 탭 선택.");
        }

        // ═══════════════ 8. 순수 헬퍼 동작 검증 (런타임 오브젝트 불필요 — 결정론) ═══════════════

        [Test]
        public void PureHelpers_FormatAndCompute()
        {
            // 건설 시간 mm:ss — BlueprintDef 실측 포맷
            Assert.That(ConstructionWindowUTK.FormatBuildTime(45), Is.EqualTo("00:45"), "bp_cooking 45초.");
            Assert.That(ConstructionWindowUTK.FormatBuildTime(90), Is.EqualTo("01:30"), "bp_forge 90초.");
            Assert.That(ConstructionWindowUTK.FormatBuildTime(120), Is.EqualTo("02:00"), "bp_barracks 120초.");
            Assert.That(ConstructionWindowUTK.FormatBuildTime(0), Is.EqualTo("00:00"), "0 방어.");

            // 부지 '{N} × {N}' — footprintRadius 절상 산출
            Assert.That(ConstructionWindowUTK.FormatFootprint(3.0f), Is.EqualTo("3 × 3"), "r=3.0 → 3 × 3.");
            Assert.That(ConstructionWindowUTK.FormatFootprint(2.5f), Is.EqualTo("3 × 3"), "r=2.5 → 절상 3 × 3.");
            Assert.That(ConstructionWindowUTK.FormatFootprint(3.5f), Is.EqualTo("4 × 4"), "r=3.5 → 4 × 4.");

            // 작업 인원 '1 명 · 유휴 인원 N명'
            Assert.That(ConstructionWindowUTK.FormatWorkforce(0), Is.EqualTo("1 명 · 유휴 인원 0명"));
            Assert.That(ConstructionWindowUTK.FormatWorkforce(3), Is.EqualTo("1 명 · 유휴 인원 3명"));

            // 부족/충족 판정 — 요구 ≤ 0 = 무관 충족 규약(HasEnoughMaterials 동일)
            Assert.That(ConstructionWindowUTK.ComputeShortage(2, 8), Is.EqualTo(6));
            Assert.That(ConstructionWindowUTK.ComputeShortage(8, 8), Is.EqualTo(0));
            Assert.That(ConstructionWindowUTK.ComputeShortage(0, 0), Is.EqualTo(0), "요구 0 = 무관.");
            Assert.That(ConstructionWindowUTK.IsMaterialSufficient(5, 0), Is.True, "요구 0 → 충족.");
            Assert.That(ConstructionWindowUTK.IsMaterialSufficient(4, 5), Is.False);

            // 재료 행 '나무 B/N'
            Assert.That(ConstructionWindowUTK.FormatMaterialPair("나무", 2, 8), Is.EqualTo("나무 2/8"));

            // 부족 사유 '{재료} {N}개가 더 필요합니다'
            Assert.That(ConstructionWindowUTK.FormatShortfallReason(new[] { "철광석" }, new[] { 3 }),
                Is.EqualTo("철광석 3개가 더 필요합니다"));
            Assert.That(ConstructionWindowUTK.FormatShortfallReason(new[] { "철광석", "멧돼지 가죽" }, new[] { 3, 2 }),
                Is.EqualTo("철광석 3개 · 멧돼지 가죽 2개가 더 필요합니다"));

            // 획득처 안내 — 실측 아이템 description 기반 문구
            Assert.That(ConstructionWindowUTK.FormatMaterialSourceHint("iron_ore"), Does.Contain("채광"));
            Assert.That(ConstructionWindowUTK.FormatMaterialSourceHint("mat_boar_leather"), Does.Contain("사냥"));
            Assert.That(ConstructionWindowUTK.BuildMaterialSourceToast(new[] { "iron_ore", "crystal_shard" }),
                Does.Contain("철광석").And.Contain("수정석").And.Contain(" / "), "복수 재료 ' / ' 연결.");
        }

        [Test]
        public void FacilityIds_Source_SixFacilitiesInFigmaOrder()
        {
            Assert.That(ConstructionWindowUTK.FacilityIds, Has.Length.EqualTo(6), "시설 6종 — Figma 5항목 + [152차 P0] 훈련소.");
            Assert.That(ConstructionWindowUTK.FacilityIds[0], Is.EqualTo("bp_cooking"), "순서 1 — 요리.");
            Assert.That(ConstructionWindowUTK.FacilityIds[1], Is.EqualTo("bp_forge"), "순서 2 — 장비.");
            Assert.That(ConstructionWindowUTK.FacilityIds[2], Is.EqualTo("bp_restaurant"), "순서 3 — 음식점.");
            Assert.That(ConstructionWindowUTK.FacilityIds[3], Is.EqualTo("bp_barracks"), "순서 4 — 병사배치.");
            Assert.That(ConstructionWindowUTK.FacilityIds[4], Is.EqualTo("bp_alchemy"), "순서 5 — 연금술.");
            Assert.That(ConstructionWindowUTK.FacilityIds[5], Is.EqualTo("bp_traininghall"), "순서 6 — 훈련소 [152차 P0].");
            Assert.That(ConstructionWindowUTK.IsFacilityBlueprint("bp_restaurant"), Is.True);
            Assert.That(ConstructionWindowUTK.IsFacilityBlueprint("bp_traininghall"), Is.True, "훈련소 = 시설 [152차 P0].");
            Assert.That(ConstructionWindowUTK.IsFacilityBlueprint("bp_wall"), Is.False, "기존 8종은 시설 아님.");
        }

        /// <summary>
        /// [100차 F-4] 소스 어서션 — 건설 관리 창 figma 239:683 재실측 확정 + 채움률 수치 판정. 근거:
        /// 도움말 높이 14→20 재실측(HelpH) + 콘텐츠-창박스 채움률 검증 로그(영상 눈대실 % 대신 수치 판정 —
        /// 사이클마다 기록). 4블록(HeaderBar/LeftPanel/RightPanel/HelpBar) worldBound 면적/창박스 비율 +
        /// 유니온(4블록 bbox) 선형 채움 + 설계 rect(figma 좌표×k + 창박스 원점) 대비 1.5px 초과 이탈 판정.
        /// </summary>
        [Test]
        public void Window_Source_100F4_Remasure_And_FillRateProbe()
        {
            string src = WindowSrc();

            Assert.That(src, Does.Contain("[100차 F-4]"), "[100차 F-4] 마커 주석.");

            // ① 재실측 확정치 — 도움말 @(64,984) 1472×20(구 14 재실측 20).
            Assert.That(src, Does.Contain("HelpH = 20f"), "[100차 F-4] 도움말 높이 14→20 재실측치.");
            Assert.That(src, Does.Contain("14→20 재실측치"), "[100차 F-4] 재실측 근거 주석 유지.");

            // ② 채움률 실측 호출 — MeasureGeometryAndDefend 사이클(개방 즉시+16/120/400ms+폴링)마다 기록.
            Assert.That(src, Does.Contain("LogContentFillRate(k);   // [100차 F-4]"),
                "[100차 F-4] 채움률 실측 호출 — 사이클마다 기록(영상 눈대실 % 대신 수치 판정).");
            Assert.That(src, Does.Contain("private void LogContentFillRate(float k)"),
                "[100차 F-4] 콘텐츠-창박스 정렬 검증 메서드 존재.");

            // ③ 4블록 실측 — HeaderBar/LeftPanel/RightPanel/HelpBar worldBound → 창박스 대비 채움률.
            Assert.That(src, Does.Contain("string[] blockNames = { \"HeaderBar\", \"LeftPanel\", \"RightPanel\", \"HelpBar\" };"),
                "[100차 F-4] 4블록 명칭 — HeaderBar/LeftPanel/RightPanel/HelpBar.");
            Assert.That(src, Does.Contain("float[] bw = { HeaderW, LeftW, RightW, HelpW };"),
                "[100차 F-4] 설계 rect — figma 재실측 로컬 좌표(프레임 원점 차감)×k 환산.");
            Assert.That(src, Does.Contain("Rect designed = new Rect(box.x + bx[i] * k, box.y + by[i] * k, bw[i] * k, bh[i] * k);"),
                "[100차 F-4] 설계 rect 산식 — 창박스 원점 + figma 좌표×k(등배수 계약 환산).");

            // ④ 로그 형식 — 블록별 면적 채움 % + 유니온(가로x세로 %) + 이탈 판정.
            Assert.That(src, Does.Contain("[100차 F-4] 채움 — {fills[0]} {fills[1]} {fills[2]} {fills[3]}"),
                "[100차 F-4] 채움 로그 — 4블록 worldBound 면적/창박스 %.");
            Assert.That(src, Does.Contain("유니온 {(ux1 - ux0) / box.width * 100f:F0}%x{(uy1 - uy0) / box.height * 100f:F0}%"),
                "[100차 F-4] 유니온(4블록 bbox) 선형 채움(가로x세로 %) — 영상 눈대실 직접 대비 판정값.");
            Assert.That(src, Does.Contain("string verdict = mismatched.Count == 0 ? \"정렬\" : $\"이탈({string.Join(\",\", mismatched)})\";"),
                "[100차 F-4] 판정 — 정렬/이탈(1.5px 초과 블록 나열 — TolPx 1.5 허용차).");
        }
    }
}
