using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [144차 P7-WTX #1] 주간 세금 창 — 피그마 243:3 "영지별 주간 세금 패널" 624×984 전체 정합.
    ///
    /// 진실치(tax_weekly__243_3.json 재독 2026-11-01):
    ///   · 창 624×984 fill=#13181F/1.0 stroke=#30363D@0.502 1.2 r=14.4, 내부 패딩 24 · 세로 gap 19.2
    ///   · 모서리 장식 4코너 12×12(L자 1.8 #58A6FF) — UTKTheme.AddCornerDecals(size 12, weight 1.8)
    ///   · 패널 제목 (24,24): "주간 세금" 21.6 Bold #F0F6FC + "영지별 세금 징수 현황" 13.2 500 #8B949E — 243:17/18
    ///   · 영지 필터 탭 (24,96) 576×38.4 — 3탭 188.8×38.4 gap 4.8 r 7.2:
    ///     "전체 영지"(활성 fill/stroke #58A6FF · 문구 #0B0E14), "성·도시"/"마을"(fill #13181F · stroke 크롬 · 문구 #8B949E) — 243:19-25
    ///   · 영지 세금 목록 (24,154) 576×662.4 — 행 576×86.4 gap 9.6 r 9.6:
    ///     등급바 4.8×86.4(#BC8CFF/#1F6FEB/#58A6FF/#8B949E — SoldierManagementUTK TierColorForLevel 4색 체계) ·
    ///     상징 원형 52.8(#21262D@0.6 / 크롬 1.2 r26.4) · 이름 16.8 Bold · 유형 12 · 세금 항목 14.4 ·
    ///     주간 징수액 19.2 Geist Mono 700 #58A6FF · "매주 징수" 12 · "상세" 버튼 60×36 r7.2 — 243:26-124
    ///     · 행 1(243:27)은 선택/하이라이트 상태(fill #FFFFFF@0.078 · stroke #58A6FF 1.8 · 버튼 액센트), 나머지(243:41…)
    ///       는 기본 상태(fill #FFFFFF@0.02 · stroke 크롬 1.2 · 버튼 고스트)
    ///   · 주간 세금 요약 (24,835) 576×114.4 — "매주 월요일 00:00 자동 징수" 15.6 #8B949E + "전체 {n}개 영지" 15.6 Bold #58A6FF,
    ///     합계 박스 576×67.6 fill #58A6FF@0.078 / stroke #58A6FF@0.251 1.2 r 9.6:
    ///     "전체 주간 세금 합계" 16.8 Bold + "보유한 {n}개 영지의 주간 징수액 합계" 12 + 합계 26.4 Geist Mono 700 #58A6FF — 243:125-133
    ///
    /// 데이터 소스(위조 금지 — 실측 API 바인딩):
    ///   · 영지 목록/이름/난이도: TerritoryDatabase.GetAllDefinitions() + TerritoryOwnership.PlayerOwned 필터
    ///     (TaxManager.CollectIntoPending :117 과세 대상 계약 동일 — LordOwned는 AITerritoryEconomySystem 전용, 수정 금지)
    ///   · 주간 징수액/합계: TaxManager.GetTaxRate(nation,index) × TaxManager.EstimateWeeklyTax(nation,index,rate)
    ///     (CollectIntoPending과 동일 산식 — VillageEconomySystem.GetWeeklyCollectedTax 경유, half-up)
    ///   · AITerritoryEconomySystem.GetWeeklyTax(TerritoryDifficulty,int taxPercent) :68 실측 — AI LordOwned 전용 산식
    ///     (일세수×7×세율 half-up, 마을 경제 계수 미반영) → 본 창 PlayerOwned 계약과 불일치로 미바인딩(기록만).
    ///   · 세금 항목("주민세 · 상업세" 등): 영지별 세금 항목 API 부재(실측 2026-11-01) → '—' 표기.
    ///   · 영지 유형("중앙 성채" 등): TerritoryDefinition에 settlement 타입 필드 부재 → 선례 준용 "난이도 {difficulty}" 실측값.
    ///   · 필터 탭: settlement 타입 API 부재 → 실측 필터링 불가. 탭 선택 시각 상태만 전환, 목록은 기본 전체 표시(기록).
    ///   · "상세" 버튼: RecommendedTaxWindowUTK.Open(string territoryId) 대상 영지 선택 지원 실측 → 연결(TerritoryId.ToString() = "{nation}_{index:D2}").
    ///   · 창 그림자: 기본 USS 계열 유지(utk-window-shadow · ShadowPad 12 · ShadowDropY 4 — UTKWindowBase).
    ///     Figma 진실치 DROP_SHADOW y38.4 blur38.4 #000000/0.251 — 수치 기록만(선례 244:281 동일 처리).
    ///   · 243:16 제목 프레임 스트로크(하단 구분선) — 스펙 미기재 + 선례(세금 관리 창 제목) 생략 준용.
    ///
    /// 창 열기 경로: [144차 P7-WTX #2] 별도 배선 금지 계약 — Open() 정적 진입점만 제공, UTKWireUp/외부 위임 미수정
    /// (후속 wiring 사이클 대기). [144차 P9] 배선 보류 확정 — 진입 경로는 사용자 지명 대기(단일 영지 창 내부 버튼 vs
    /// 상점NPC 패널 신설 버튼 — 후자는 81:134 Figma 1:1 위반 우려). Figma에 없는 요소 신설 금지 계약상 기록만 유지. UTKWindowManager 등록은 UTKWindowBase.Show() 경유(선례 준수).
    /// 레이아웃: ApplyCanvasFit 84차 계약(창박스=figma×k 등배수, content raw·scale=k — RecommendedTaxWindowUTK 선례).
    /// </summary>
    public class WeeklyTaxWindowUTK : UTKWindowBase
    {
        // ===== 스타일 상수 [Figma 243:3 팔레트 준용] =====
        private static readonly Color PanelBg = new Color(0.075f, 0.094f, 0.122f, 1f);          // #13181F [P7-WTX #3]
        private static readonly Color StrokeAlpha = new Color(0.188f, 0.212f, 0.239f, 0.502f);  // #30363D@0.502 [P7-WTX #4]
        private static readonly Color AccentColor = new Color(0.345f, 0.651f, 1f, 1f);          // #58A6FF
        private static readonly Color TextMainColor = new Color(0.941f, 0.965f, 0.988f, 1f);    // #F0F6FC
        private static readonly Color TextSubColor = new Color(0.545f, 0.580f, 0.620f, 1f);     // #8B949E
        private static readonly Color BtnTextDark = new Color(0.043f, 0.055f, 0.078f, 1f);      // #0B0E14 [P7-WTX #5]
        private static readonly Color DeepBlue = new Color32(0x1F, 0x6F, 0xEB, 0xFF);           // 등급바 딥블루 [P7-WTX #6]
        private static readonly Color Purple = new Color32(0xBC, 0x8C, 0xFF, 0xFF);             // 등급바 퍼플
        private static readonly Color RowFill = new Color(1f, 1f, 1f, 0.02f);                   // #FFFFFF@0.02 [P7-WTX #7]
        private static readonly Color RowFillSelected = new Color(1f, 1f, 1f, 0.078f);          // #FFFFFF@0.078 [P7-WTX #8]
        private static readonly Color EmblemFill = new Color(0.129f, 0.149f, 0.176f, 0.6f);     // #21262D@0.6 [P7-WTX #9]
        private static readonly Color AccentAlphaFill = new Color(0.345f, 0.651f, 1f, 0.078f);  // #58A6FF@0.078 [P7-WTX #10]
        private static readonly Color AccentAlphaStroke = new Color(0.345f, 0.651f, 1f, 0.251f);// #58A6FF@0.251 [P7-WTX #11]

        private static readonly Vector2 WinSize = new Vector2(624, 984);

        // 목록 기하 [피그마 243:26 실측]
        private const float RowHeight = 86.4f;
        private const float RowGap = 9.6f;

        // ===== UI 노드 =====
        private readonly Button[] _filterTabs = new Button[3];
        private VisualElement _rowContainer;
        private Label _countLabel;      // "전체 {n}개 영지"
        private Label _countSubLabel;   // "보유한 {n}개 영지의 주간 징수액 합계"
        private Label _totalValue;      // 합계 26.4 Geist Mono

        // ===== 상태 =====
        private int _activeFilter;      // 0=전체 영지, 1=성·도시, 2=마을 — settlement 타입 API 부재로 시각 상태만 전환 [P7-WTX #12]
        private float _lastFitRootW = -1f;
        private float _lastFitRootH = -1f;

        /// <summary>목록 행 데이터 — 실측값만 보관(위조 금지).</summary>
        private readonly struct RowData
        {
            public readonly NationType Nation;
            public readonly int Index;
            public readonly string Name;
            public readonly TerritoryDifficulty Difficulty;
            public readonly int Weekly;
            public readonly bool HasData;

            public RowData(NationType nation, int index, string name, TerritoryDifficulty difficulty, int weekly)
            {
                Nation = nation; Index = index; Name = name; Difficulty = difficulty; Weekly = weekly; HasData = true;
            }

            /// <summary>데이터 부재 플레이스홀더('—' 표기 전용).</summary>
            public static RowData Empty() => new RowData();
        }

        // =====================================================================
        // 생성자 — UI 구축 (절대 배치 = 피그마 좌표 그대로)
        // =====================================================================
        public WeeklyTaxWindowUTK() : base("💰 주간 세금", WinSize)
        {
            SetChrome(UTKWindowChrome.Frameless);
            // [144차 P7-WTX #13] fill=#13181F/1.0 stroke=#30363D@0.502 1.2 r=14.4 — 243:3 진실치
            style.backgroundColor = PanelBg;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1.2f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = StrokeAlpha;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 14.4f;

            var content = Content;
            if (content == null) return;
            content.style.position = Position.Relative;

            // 창 그림자 — 기본 USS 계열 유지 [P7-WTX #14] (utk-window-shadow · ShadowPad 12 · ShadowDropY 4;
            //   Figma 진실치 DROP_SHADOW y38.4 blur38.4 #000000/0.251 — 수치 기록만)
            UTKTheme.AddCornerDecals(content, 12f, 1.8f, "#58A6FF");   // [P7-WTX #15] 4코너 12×12 L자 1.8 #58A6FF — 243:4/7/10/13

            BuildTitle(content);
            BuildFilterTabs(content);
            BuildTaxList(content);
            BuildSummary(content);
        }

        // ── 공용 라벨 헬퍼(절대 배치) — RecommendedTaxWindowUTK.MakeLabel 선례 ──
        private static Label MakeLabel(VisualElement parent, string text, float x, float y, float fontSize,
            Color color, bool bold = false, float width = -1f)
        {
            var label = new Label(text);
            label.style.position = Position.Absolute;
            label.style.left = x; label.style.top = y;
            if (width >= 0f) label.style.width = width;
            label.style.fontSize = fontSize;
            label.style.color = color;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.pickingMode = PickingMode.Ignore;
            parent.Add(label);
            return label;
        }

        private void BuildTitle(VisualElement content)
        {
            // [144차 P7-WTX #16] 패널 제목 — "주간 세금" 21.6 Bold #F0F6FC @(24,24) + 보조 13.2 500 #8B949E @(121,32) — 243:17/18
            MakeLabel(content, "주간 세금", 24f, 24f, 21.6f, TextMainColor, bold: true).name = "WeeklyTaxTitle";
            MakeLabel(content, "영지별 세금 징수 현황", 121f, 32f, 13.2f, TextSubColor).name = "WeeklyTaxSubtitle";
        }

        private void BuildFilterTabs(VisualElement content)
        {
            // [144차 P7-WTX #17] 영지 필터 탭 (24,96) 576×38.4 — 3탭 188.8×38.4 gap 4.8 r 7.2 — 243:19-25
            var filter = new VisualElement { name = "TerritoryFilter" };
            filter.style.position = Position.Absolute;
            filter.style.left = 24f; filter.style.top = 96f;
            filter.style.width = 576f; filter.style.height = 38.4f;
            content.Add(filter);

            string[] labels = { "전체 영지", "성·도시", "마을" };   // 243:21/23/25 원문
            for (int i = 0; i < 3; i++)
            {
                int index = i;   // 클로저 캡처 — 탭별 인덱스
                var tab = new Button(() => SelectFilter(index)) { text = labels[i] };
                tab.name = $"FilterTab_{i}";
                tab.style.position = Position.Absolute;
                tab.style.left = i * 193.6f;               // 188.8 + gap 4.8
                tab.style.top = 0f;
                tab.style.width = 188.8f; tab.style.height = 38.4f;
                tab.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                tab.style.color = TextSubColor;
                tab.style.fontSize = 14.4f;
                tab.style.unityFontStyleAndWeight = FontStyle.Bold;
                tab.style.unityTextAlign = TextAnchor.MiddleCenter;
                tab.style.paddingLeft = tab.style.paddingRight = 0f;
                tab.style.paddingTop = tab.style.paddingBottom = 0f;
                UTKTheme.ApplyElementBorder(tab);
                UTKTheme.ApplyRadius(tab, 7.2f);
                filter.Add(tab);
                _filterTabs[i] = tab;
            }
            ApplyFilterVisual();
        }

        /// <summary>필터 탭 선택 — 활성 시각 상태 전환(전용) [P7-WTX #18].
        /// settlement 타입 API 부재(실측 — TerritoryDefinition에 타입 필드 없음)로 실측 필터링 불가:
        /// 시각 활성 상태만 전환하고 목록은 기본 전체 표시(기록).</summary>
        public void SelectFilter(int index)
        {
            _activeFilter = Mathf.Clamp(index, 0, 2);
            ApplyFilterVisual();
        }

        private void ApplyFilterVisual()
        {
            for (int i = 0; i < 3; i++)
            {
                var tab = _filterTabs[i];
                if (tab == null) continue;
                bool active = i == _activeFilter;
                // 활성(243:20): fill/stroke #58A6FF · 문구 #0B0E14 — 비활성(243:22/24): fill #13181F · 크롬 스트로크 · 문구 #8B949E
                tab.style.backgroundColor = active ? AccentColor : PanelBg;
                UTKTheme.ApplyElementBorder(tab, active ? AccentColor : StrokeAlpha, 1.2f);
                tab.style.color = active ? BtnTextDark : TextSubColor;
            }
        }

        private void BuildTaxList(VisualElement content)
        {
            // [144차 P7-WTX #19] 영지 세금 목록 (24,154) 576×662.4 — 행 576×86.4 gap 9.6 — 243:26
            //   실측 행 수는 PlayerOwned 영지 수(동적) — 피그마 7행은 정적 목업. 넘침 대비 Fluent 스크롤러(Show 경유).
            var list = new ScrollView { name = "TerritoryTaxList" };
            list.style.position = Position.Absolute;
            list.style.left = 24f; list.style.top = 154f;
            list.style.width = 576f; list.style.height = 662.4f;
            list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            content.Add(list);
            _rowContainer = list.contentContainer;
        }

        private void BuildSummary(VisualElement content)
        {
            // [144차 P7-WTX #20] 주간 세금 요약 (24,835) 576×114.4 — 243:125
            var summary = new VisualElement { name = "TaxSummary" };
            summary.style.position = Position.Absolute;
            summary.style.left = 24f; summary.style.top = 835f;
            summary.style.width = 576f; summary.style.height = 114.4f;
            content.Add(summary);

            // 징수 일정 행 — "매주 월요일 00:00 자동 징수" 15.6 #8B949E + "전체 {n}개 영지" 15.6 Bold #58A6FF — 243:126-128
            MakeLabel(summary, "매주 월요일 00:00 자동 징수", 0f, 15f, 15.6f, TextSubColor).name = "TaxScheduleLabel";
            _countLabel = MakeLabel(summary, "—", 437f, 15f, 15.6f, AccentColor, bold: true, width: 139f);   // 우측 정렬(오른Edge 576)
            _countLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _countLabel.name = "TaxCountLabel";

            // 전체 주간 세금 박스 (0,47) 576×67.6 — fill #58A6FF@0.078 / stroke #58A6FF@0.251 1.2 r 9.6 — 243:129
            var totalBox = new VisualElement { name = "TaxTotalBox" };
            totalBox.style.position = Position.Absolute;
            totalBox.style.left = 0f; totalBox.style.top = 47f;
            totalBox.style.width = 576f; totalBox.style.height = 67.6f;
            totalBox.style.backgroundColor = AccentAlphaFill;
            UTKTheme.ApplyElementBorder(totalBox, AccentAlphaStroke, 1.2f);
            UTKTheme.ApplyRadius(totalBox, 9.6f);
            totalBox.pickingMode = PickingMode.Ignore;
            summary.Add(totalBox);

            MakeLabel(totalBox, "전체 주간 세금 합계", 14.4f, 14f, 16.8f, TextMainColor, bold: true).name = "TaxTotalLabel";
            _countSubLabel = MakeLabel(totalBox, "—", 14.4f, 39f, 12f, TextSubColor, width: 300f);
            _countSubLabel.name = "TaxTotalSub";
            _totalValue = MakeLabel(totalBox, "—", 451f, 17f, 26.4f, AccentColor, bold: true, width: 111f);   // 우측 정렬(오른Edge 562)
            _totalValue.style.unityTextAlign = TextAnchor.MiddleRight;
            _totalValue.style.unityFont = GetGeistMonoFont();   // [P7-WTX #21] Geist Mono — 에셋 부재 시 UIFont 폴백(선례)
            _totalValue.name = "TaxTotalValue";
        }

        // =====================================================================
        // 행 구축 — 243:27(선택)/243:41(기본) 진실치
        // =====================================================================
        private VisualElement BuildRow(RowData row, int rowIndex, bool selected)
        {
            var card = new VisualElement { name = $"TerritoryRow_{rowIndex}" };
            card.style.position = Position.Absolute;
            card.style.left = 0f; card.style.top = rowIndex * (RowHeight + RowGap);
            card.style.width = 576f; card.style.height = RowHeight;
            // 선택(243:27): fill #FFFFFF@0.078 · stroke #58A6FF 1.8 — 기본(243:41): fill #FFFFFF@0.02 · stroke 크롬 1.2
            card.style.backgroundColor = selected ? RowFillSelected : RowFill;
            UTKTheme.ApplyElementBorder(card, selected ? AccentColor : StrokeAlpha, selected ? 1.8f : 1.2f);
            UTKTheme.ApplyRadius(card, 9.6f);
            _rowContainer.Add(card);

            // 등급바 4.8×86.4 — SoldierManagementUTK TierColorForLevel 4색 체계 난이도 매핑 [P7-WTX #22]
            var gradeBar = new VisualElement { name = $"GradeBar_{rowIndex}" };
            gradeBar.style.position = Position.Absolute;
            gradeBar.style.left = 0f; gradeBar.style.top = 0f;
            gradeBar.style.width = 4.8f; gradeBar.style.height = RowHeight;
            gradeBar.style.backgroundColor = row.HasData ? TierColorForDifficulty(row.Difficulty) : TextSubColor;
            gradeBar.pickingMode = PickingMode.Ignore;
            card.Add(gradeBar);

            // 영지 상징 — 원형 52.8 fill #21262D@0.6 / 크롬 1.2 r 26.4, castle/landmark/house 벡터는 유형 API 부재로
            //   선례(RecommendedTaxWindowUTK 🏰) 준용 고정 글리프 [P7-WTX #23]
            var emblemRing = new VisualElement { name = $"Emblem_{rowIndex}" };
            emblemRing.style.position = Position.Absolute;
            emblemRing.style.left = 14.4f; emblemRing.style.top = 16f;
            emblemRing.style.width = 52.8f; emblemRing.style.height = 52.8f;
            emblemRing.style.backgroundColor = EmblemFill;
            UTKTheme.ApplyElementBorder(emblemRing, StrokeAlpha, 1.2f);
            UTKTheme.ApplyRadius(emblemRing, 26.4f);
            emblemRing.pickingMode = PickingMode.Ignore;
            card.Add(emblemRing);
            var emblem = new Label("🏰");
            emblem.style.position = Position.Absolute;
            emblem.style.left = 0f; emblem.style.top = 0f;
            emblem.style.width = 52.8f; emblem.style.height = 52.8f;
            emblem.style.fontSize = 26f;
            emblem.style.unityTextAlign = TextAnchor.MiddleCenter;
            emblem.pickingMode = PickingMode.Ignore;
            emblemRing.Add(emblem);

            // 이름 16.8 Bold #F0F6FC @(82,22) — 유형 12 #8B949E @(158,27)(선례 고정 x 준용)
            MakeLabel(card, row.HasData ? row.Name : "—", 82f, 22f, 16.8f, TextMainColor, bold: true, width: 72f)
                .name = $"TerritoryName_{rowIndex}";
            MakeLabel(card, row.HasData ? $"난이도 {row.Difficulty}" : "—", 158f, 27f, 12f, TextSubColor)
                .name = $"TerritoryType_{rowIndex}";

            // 세금 항목 14.4 #8B949E @(82,47) — 영지별 세금 항목 API 부재(실측) → '—' 표기 [P7-WTX #24]
            MakeLabel(card, "—", 82f, 47f, 14.4f, TextSubColor, width: 280f).name = $"TaxItems_{rowIndex}";

            // 주간 징수액 19.2 Geist Mono 700 #58A6FF — 우측 정렬(오른Edge 487), "매주 징수" 12 #8B949E — 243:37/38
            var amount = MakeLabel(card, row.HasData ? $"{row.Weekly:N0}g" : "—", 417f, 22f, 19.2f, AccentColor, bold: true, width: 70f);
            amount.style.unityTextAlign = TextAnchor.MiddleRight;
            amount.style.unityFont = GetGeistMonoFont();   // [P7-WTX #25] Geist Mono — 에셋 부재 시 UIFont 폴백
            amount.name = $"WeeklyAmount_{rowIndex}";
            MakeLabel(card, "매주 징수", 439f, 49f, 12f, TextSubColor, width: 48f)
                .name = $"WeeklyCycle_{rowIndex}";

            // "상세" 버튼 60×36 r7.2 @(502,25) — 선택행 액센트(243:39), 기본행 고스트(243:53)
            var detail = new Button(row.HasData ? () => OnDetailClicked(row.Nation, row.Index) : null)
                { text = "상세" };
            detail.name = $"DetailButton_{rowIndex}";
            detail.style.position = Position.Absolute;
            detail.style.left = 502f; detail.style.top = 25f;
            detail.style.width = 60f; detail.style.height = 36f;
            detail.style.fontSize = 14.4f;
            detail.style.unityFontStyleAndWeight = FontStyle.Bold;
            detail.style.unityTextAlign = TextAnchor.MiddleCenter;
            detail.style.paddingLeft = detail.style.paddingRight = 0f;
            detail.style.paddingTop = detail.style.paddingBottom = 0f;
            ApplyDetailButtonStyle(detail, selected);
            if (!row.HasData) detail.SetEnabled(false);   // 데이터 부재 행 — 상세 진입 컨텍스트 없음 [P7-WTX #26]
            card.Add(detail);

            return card;
        }

        private void ApplyDetailButtonStyle(Button btn, bool accent)
        {
            if (btn == null) return;
            // 액센트(243:39): fill/stroke #58A6FF · 문구 #0B0E14 — 고스트(243:53): fill #FFFFFF@0.02 · 크롬 · 문구 #F0F6FC
            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = accent ? AccentColor : RowFill;
            btn.style.color = accent ? BtnTextDark : TextMainColor;
            UTKTheme.ApplyElementBorder(btn, accent ? AccentColor : StrokeAlpha, 1.2f);
            UTKTheme.ApplyRadius(btn, 7.2f);
        }

        /// <summary>[144차 P7-WTX #27] 등급바 색 — SoldierManagementUTK TierColorForLevel 4색 체계를 영지 난이도에 매핑
        /// (피그마 7행 등급바 #BC8CFF/#1F6FEB/#58A6FF/#8B949E 순서 정합 — 최상 Ring4/Empire 퍼플 → 최하 Ring1 그레이).</summary>
        private static Color TierColorForDifficulty(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring4:
                case TerritoryDifficulty.Empire:
                    return Purple;              // #BC8CFF — 정예
                case TerritoryDifficulty.Ring3:
                    return DeepBlue;            // #1F6FEB — 딥블루
                case TerritoryDifficulty.Ring2:
                    return AccentColor;         // #58A6FF — 액센트
                default:
                    return TextSubColor;        // #8B949E — 그레이(Ring1)
            }
        }

        // =====================================================================
        // 데이터 갱신 / 액션
        // =====================================================================

        /// <summary>[144차 P7-WTX #28] 실측값으로 목록/요약 갱신(위조 금지 — 소스 없는 값은 "—" 폴백).
        /// 과세 대상 계약(TaxManager.CollectIntoPending :117)과 동일하게 PlayerOwned 영지만 집계한다.</summary>
        public void RefreshData()
        {
            if (_rowContainer == null) return;
            _rowContainer.Clear();

            var db = TerritoryDatabase.Instance;
            var rows = new System.Collections.Generic.List<RowData>();
            if (db != null)
            {
                foreach (var definition in db.GetAllDefinitions())
                {
                    if (definition.id.nation == default && definition.id.index <= 0) continue;   // 빈 정의 방어(CollectIntoPending 동일)
                    var state = db.GetState(definition.id.nation, definition.id.index);
                    if (state == null || state.ownership != TerritoryOwnership.PlayerOwned) continue;

                    int rate = TaxManager.Instance.GetTaxRate(definition.id.nation, definition.id.index);
                    int weekly = TaxManager.Instance.EstimateWeeklyTax(definition.id.nation, definition.id.index, rate);
                    string name = string.IsNullOrEmpty(definition.territoryName)
                        ? definition.id.ToString()
                        : definition.territoryName;
                    rows.Add(new RowData(definition.id.nation, definition.id.index, name, definition.difficulty, weekly));
                }
            }

            // 주간 징수액 내림차순 — 피그마 243:27→124 행 배치(금액 내림차순) 정합 [P7-WTX #29]
            rows.Sort((a, b) => b.Weekly.CompareTo(a.Weekly));

            if (rows.Count == 0)
            {
                // [144차 P7-WTX #30] 보유 영지 부재 — '—' 플레이스홀더 1행(구조 유지, 위조 금지)
                BuildRow(RowData.Empty(), 0, selected: true);
            }
            else
            {
                for (int i = 0; i < rows.Count; i++)
                    BuildRow(rows[i], i, selected: i == 0);   // 첫 행 하이라이트 — 243:27 선택 상태 정합
            }

            int count = rows.Count;
            long sum = 0;
            foreach (var r in rows) sum += r.Weekly;
            _countLabel.text = db == null ? "—" : $"전체 {count}개 영지";
            _countSubLabel.text = db == null ? "—" : $"보유한 {count}개 영지의 주간 징수액 합계";
            _totalValue.text = db == null ? "—" : $"{sum:N0}g";
        }

        /// <summary>[144차 P7-WTX #31] 상세 — RecommendedTaxWindowUTK.Open(string territoryId) 대상 영지 선택 지원 실측
        /// (TryParseTerritoryId 계약 "{nation}_{index:D2}" = TerritoryId.ToString()) → 연결.</summary>
        private void OnDetailClicked(NationType nation, int index)
        {
            RecommendedTaxWindowUTK.Open(new TerritoryId(nation, index).ToString());
        }

        // =====================================================================
        // 공개 진입점 — 루트 부착 + 표시 (고아 가드 — RecommendedTaxWindowUTK 선례)
        // =====================================================================
        public static WeeklyTaxWindowUTK Instance { get; private set; }

        /// <summary>개방 — [144차 P7-WTX #32] 별도 배선 금지 계약(UTKWireUp 미수정, 후속 wiring 사이클 대기) — 정적 진입점만 제공.
        /// UTKWindowManager 등록은 Show() 경유(선례 준수).</summary>
        public static WeeklyTaxWindowUTK Open()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[WeeklyTaxWindowUTK] UI Toolkit 루트 없음 — 부트스트랩 미완료.");
                return null;
            }

            if (Instance != null && Instance.parent == root)
            {
                Instance.Show();
            }
            else
            {
                if (Instance != null)
                    Instance.RemoveFromHierarchy();   // 고아 인스턴스 정리 — 114차 WorldMap Ensure 가드 함정 선례.

                Instance = new WeeklyTaxWindowUTK();
                root.Add(Instance);
                Instance.Show();
            }

            Instance.RefreshData();
            return Instance;
        }

        // =====================================================================
        // UTKWindowBase 훅 + 캔버스핏(84차 등배수 계약 — RecommendedTaxWindowUTK 선례)
        // =====================================================================
        protected override void OnWindowOpen()
        {
            RefreshData();
            schedule.Execute(ApplyCanvasFit).StartingIn(16);
            schedule.Execute(ApplyCanvasFit).StartingIn(120);
            schedule.Execute(ApplyCanvasFit).StartingIn(400);
        }

        protected override void OnWindowClosed()
        {
            if (Instance == this) Instance = null;
        }

        private void ApplyCanvasFit()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float rootW = root.resolvedStyle.width;
            float rootH = root.resolvedStyle.height;
            if (rootW <= 1f || rootH <= 1f) return;
            if (Mathf.Approximately(rootW, _lastFitRootW) && Mathf.Approximately(rootH, _lastFitRootH))
                return;
            float k = Mathf.Min(rootW / FigmaCanvasLayout.CanvasWidth, rootH / FigmaCanvasLayout.CanvasHeight);
            if (k <= 0f) return;

            _lastFitRootW = rootW;
            _lastFitRootH = rootH;
            style.width = WinSize.x * k;
            style.height = WinSize.y * k;
            style.left = Mathf.Max(0f, (rootW - WinSize.x * k) * 0.5f);
            style.top = Mathf.Max(0f, (rootH - WinSize.y * k) * 0.5f);
            Content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            Content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));
        }

        // [144차 P7-WTX #33] Geist Mono 로드(Figma 패밀리 계약) — 에셋 미보유 실측(2026-10-24) → UIFont 폴백
        // (RecommendedTaxWindowUTK/WorldMapWindowUTK/WeaponForgeUTK GetGeistMonoFont 선례). Resources/Fonts/GeistMono 추가 시 자동 적용.
        private static Font _geistMono;

        private static Font GetGeistMonoFont()
        {
            if (_geistMono == null)
                _geistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _geistMono != null ? _geistMono : UIFont.Load();
        }
    }
}
