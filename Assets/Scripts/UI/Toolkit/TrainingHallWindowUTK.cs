using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [125차 P4] 훈련소 UI — Figma 269:322 "훈련소 병사 훈련 관리" 1600×1040 전정합.
    /// 골격 = 음식점창(RestaurantWindowUTK) 동일: Frameless + 투명 루트 + raw 1600×1040 + ApplyCanvasFit 등배수.
    /// 레이아웃 진실치(Figma rel): 헤더 (64,36) 1472×18 / 좌 병사 명부 (64,78) 480×882 #13181F /
    /// 우 훈련 진행 현황 (568,78) 968×882 #13181F / 도움말 (64,984) 1472×14.
    /// [144차 P6-TRN #1] 루트 pad-top 36 반영 — 4 앵커 y+36 계통 시프트(내부 상대 배치 불변).
    /// 데이터: TrainingHallSystem(정적) + GuardManager(영지 주둔 병사) + TimeManager(게임시간 실데이터).
    /// 단축키(Figma): 클릭 병사 선택 · Enter 완료 확인 · Esc 닫기(UTKWindowBase ESC 스택 규약).
    /// </summary>
    public class TrainingHallWindowUTK : UTKWindowBase
    {
        private const float WinW = 1600f;
        private const float WinH = 1040f;
        private static TrainingHallWindowUTK _instance;

        public static bool IsWindowOpen => _instance != null && _instance.IsOpen;

        // ===== Figma 진실치 색 =====
        private static Color C(float r, float g, float b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);
        private static readonly Color PanelBg = C(0x13, 0x18, 0x1F);            // #13181F 좌우 패널
        private static readonly Color SubBg = C(0x19, 0x1F, 0x27);              // #191F27 운영정보/탭 비활성/닫기
        private static readonly Color InputBg = C(0x0F, 0x14, 0x1B);            // #0F141B 검색/열제목
        private static readonly Color CardBg = C(1f, 1f, 1f, 0.08f);            // #FFFFFF/0.08 병사 항목
        private static readonly Color AvatarBg = C(0x21, 0x26, 0x2D, 0.60f);    // #21262D/0.60
        private static readonly Color IconBg = C(0x20, 0x26, 0x2E);             // #20262E
        private static readonly Color PillBg = C(0f, 0f, 0f, 0.25f);            // 상태 pill
        private static readonly Color TextMain = C(0xF0, 0xF6, 0xFC);           // #F0F6FC
        private static readonly Color TextSub = C(0x8B, 0x94, 0x9E);            // #8B949E
        private static readonly Color TextHint = C(0x58, 0x64, 0x73);           // #586473 검색 placeholder
        private static readonly Color Accent = C(0x58, 0xA6, 0xFF);             // #58A6FF
        private static readonly Color AccentSoft = C(0x58, 0xA6, 0xFF, 0.08f);  // 행 배경
        private static readonly Color AccentSofter = C(0x58, 0xA6, 0xFF, 0.04f);
        private static readonly Color Violet = C(0x8B, 0x5C, 0xF6);             // #8B5CF6 훈련 버튼
        private static readonly Color Green = C(0x22, 0xC5, 0x5E);              // #22C55E 완료 섹션
        private static readonly Color GreenSoft = C(0x22, 0xC5, 0x5E, 0.08f);
        private static readonly Color DarkText = C(0x0B, 0x0E, 0x14);           // 탭 활성 텍스트
        private static readonly Color GhostBtnBg = C(1f, 1f, 1f, 0.02f);            // #FFFFFF/0.02 취소 버튼 (Figma 271:366·271:12) [144차 P6-TRN #8]
        private static readonly Color ChromeBorder = C(0x30, 0x36, 0x3D, 0.502f);   // #30363D/0.502 크롬 테두리 [144차 P6-TRN #4]
        private static readonly Color RowBorderActive = C(0x58, 0xA6, 0xFF, 0.251f); // 행 1-2 테두리 #58A6FF/0.251 (Figma 271:284)
        private static readonly Color RowBorderDim = C(0x30, 0x36, 0x3D, 0.251f);    // 행 3+ 테두리 #30363D/0.251 (Figma 271:399)
        private static readonly Color GreenBorder = C(0x22, 0xC5, 0x5E, 0.251f);     // 완료 섹션/배치 테두리 (Figma 271:272·276)

        private const int Capacity = TrainingHallSystem.Capacity;               // 20 (Figma 문구)
        private const int GarrisonCap = 240;                                    // 주둔 병력 상한 (Figma 240 — 수비 상한 소스 미실측 기본값)

        // ===== 상태 =====
        private int _filter;                       // 0 대기중 / 1 훈련중 / 2 완료
        private string _territoryKey;              // "East_01" (개방 시점 실측 — PollTick 재해석)
        private string _territoryLabel = "영지 밖";
        private string _selectedStableId;
        private string _lastLeftSig;               // 좌 목록 구조 시그니처(변화 시만 rebuild)
        private string _lastRightSig;              // 우 행 구조 시그니처
        private IVisualElementScheduledItem _pollTask;

        // ===== 좌측 refs =====
        private Button _tabWaiting, _tabTraining, _tabDone;
        private TextField _searchField;
        private ScrollView _listScroll;
        private readonly List<SoldierRowView> _soldierRows = new List<SoldierRowView>();

        // ===== 우측 refs =====
        private Label _facilityName, _facilityDesc, _opState;
        private readonly List<TrainingRowView> _trainingRows = new List<TrainingRowView>();
        private Label _completedTitle, _completedDetail;
        private Button _deployBtn;
        private Label _locLabel, _dayLabel, _clockLabel;

        private TrainingHallWindowUTK() : base("훈련소", new Vector2(WinW, WinH), UTKWindowChrome.Frameless)
        {
            // 투명 루트 — Figma 패널이 곧 창(음식점창 골격)
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            _content.style.width = WinW;
            _content.style.height = WinH;
            _content.style.position = Position.Relative;
            _content.style.paddingLeft = 0f; _content.style.paddingRight = 0f;
            _content.style.paddingTop = 0f; _content.style.paddingBottom = 0f;
            _content.style.flexGrow = 0f; _content.style.flexShrink = 0f;

            BuildHeader();
            BuildLeftPanel();
            BuildRightPanel();
            BuildHelpBar();
            RegisterKeyTick();
        }

        // =====================================================================
        //  개방/정적 진입
        // =====================================================================
        public static void Open()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[TrainingHallWindowUTK] UIRoot 미준비 — 훈련소 창 개방 불가");
                return;
            }
            EnsureAttached();
            TrainingHallSystem.EnsureDriver();   // 완료 폴링 드라이버 보장
            if (_instance.IsOpen)
                _instance.RefreshAll();
            else
                _instance.Show();                // OnWindowOpen → ApplyCanvasFit/폴링
            Debug.Log($"[TrainingHallWindowUTK] 훈련소 창 개방 — attached={(_instance.parent != null)} isOpen={_instance.IsOpen}");
        }

        private static void EnsureAttached()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (_instance != null)
            {
                bool orphaned = _instance.parent == null;
                if (!orphaned)
                {
                    var p = _instance.parent;
                    while (p != null && p != root) p = p.parent;
                    orphaned = p != root;
                }
                if (orphaned)
                {
                    Debug.LogWarning("[TrainingHallWindowUTK] 고아 인스턴스 감지 — 제거 후 재생성 ([117차 R3] 선례)");
                    _instance.RemoveFromHierarchy();
                    _instance = null;
                }
            }
            if (_instance == null)
                _instance = new TrainingHallWindowUTK();
            if (_instance.parent == null)
                root.Add(_instance);
        }

        protected override void OnWindowOpen()
        {
            // [88차 B1] ApplyCanvasFit — 개방 즉시 + 지연 대응 재시도(16/120/400ms — 음식점창 선례)
            schedule.Execute(ApplyCanvasFit).StartingIn(16);
            schedule.Execute(ApplyCanvasFit).StartingIn(120);
            schedule.Execute(ApplyCanvasFit).StartingIn(400);
            RefreshAll();
            if (_pollTask == null)
                _pollTask = schedule.Execute(PollTick).Every(300);
        }

        public override void Hide()
        {
            base.Hide();
            Debug.Log("[TrainingHallWindowUTK] 훈련소 창 닫힘 — 훈련은 계속됨(Figma 운영 안내 계약)");
        }

        /// <summary>ApplyCanvasFit — 음식점창 동일 등배수 계약(raw 1600×1040 × k = min(root/1920, root/1080) 센터링).</summary>
        private float _lastFitW = -1f, _lastFitH = -1f;
        private void ApplyCanvasFit()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float rw = root.resolvedStyle.width, rh = root.resolvedStyle.height;
            if (rw <= 1f || rh <= 1f) return;
            if (Mathf.Approximately(rw, _lastFitW) && Mathf.Approximately(rh, _lastFitH)) return;
            float k = Mathf.Min(rw / FigmaCanvasLayout.CanvasWidth, rh / FigmaCanvasLayout.CanvasHeight);
            if (k <= 0f) return;
            _lastFitW = rw; _lastFitH = rh;
            style.width = WinW * k;
            style.height = WinH * k;
            style.left = Mathf.Max(0f, (rw - WinW * k) * 0.5f);
            style.top = Mathf.Max(0f, (rh - WinH * k) * 0.5f);
            _content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));
        }

        // =====================================================================
        //  헤더 (64,36) 1472×18 — 브레드크럼 + 게임 시간 (TimeManager 실데이터)
        // =====================================================================
        private void BuildHeader()
        {
            var header = new VisualElement { name = "TrainingHeader" };
            Abs(header, 64f, 36f, 1472f, 18f);   // [144차 P6-TRN #1] y+36 — Figma 루트 pad-top 36 반영

            // [144차 P6-TRN #2] 현재 위치 브레드크럼 — Figma 269:324-328: 아이콘18 + 위치 fs13 #8B949E + '/' fs12 #586473 + 화면명 fs13 #F0F6FC (gap 12)
            var loc = new VisualElement { name = "Breadcrumb" };
            Abs(loc, 0f, 0f, 18f);
            loc.style.flexDirection = FlexDirection.Row;
            loc.style.alignItems = Align.Center;
            // 전초기지 아이콘 [144차 P6-TRN #2] — VECTOR 269:325(18×18 #8B949E/1.6) → Resources/UI/icons/map-pin.png 근사 매핑
            var pin = IconEl("UI/icons/map-pin", 0f, 0f, 18f, 18f);
            pin.style.position = Position.Relative;
            loc.Add(pin);
            _locLabel = new Label("—");
            _locLabel.style.fontSize = 13f;
            _locLabel.style.color = new StyleColor(TextSub);
            _locLabel.style.marginLeft = 12f;
            _locLabel.style.maxWidth = 700f;
            loc.Add(_locLabel);
            var sep = new Label("/") { name = "BreadcrumbSep" };
            sep.style.fontSize = 12f;
            sep.style.color = new StyleColor(TextHint);
            sep.style.marginLeft = 12f;
            sep.pickingMode = PickingMode.Ignore;
            loc.Add(sep);
            var screen = new Label("훈련소 관리") { name = "BreadcrumbScreen" };
            screen.style.fontSize = 13f;
            screen.style.color = new StyleColor(TextMain);
            screen.style.marginLeft = 12f;
            screen.pickingMode = PickingMode.Ignore;
            loc.Add(screen);
            header.Add(loc);

            // [144차 P6-TRN #3] 게임 시간 — Figma 269:329-332: 시계 아이콘 + 일차 fs12 #8B949E + 시각 fs13 #F0F6FC (gap 10, x 1339)
            var timeBox = new VisualElement { name = "GameTime" };
            Abs(timeBox, 1339f, 0f, 18f);
            timeBox.style.flexDirection = FlexDirection.Row;
            timeBox.style.alignItems = Align.Center;
            timeBox.Add(BuildClockGlyph());
            _dayLabel = new Label("—");
            _dayLabel.style.fontSize = 12f;
            _dayLabel.style.color = new StyleColor(TextSub);
            _dayLabel.style.marginLeft = 10f;
            timeBox.Add(_dayLabel);
            _clockLabel = new Label("—");
            _clockLabel.style.fontSize = 13f;
            _clockLabel.style.color = new StyleColor(TextMain);
            _clockLabel.style.marginLeft = 10f;
            timeBox.Add(_clockLabel);
            header.Add(timeBox);
            _content.Add(header);
        }

        /// <summary>[144차 P6-TRN #3] 시계 VECTOR(269:330, 15×15 #8B949E/1.5) — Resources 내 clock PNG 부재 → 프레임 근사(원 테두리 + 바늘 2개). 근사 기록.</summary>
        private static VisualElement BuildClockGlyph()
        {
            var clock = new VisualElement { name = "Icon_clock_glyph" };
            clock.style.width = 15f; clock.style.height = 15f;
            clock.style.borderTopWidth = 1.5f; clock.style.borderBottomWidth = 1.5f;
            clock.style.borderLeftWidth = 1.5f; clock.style.borderRightWidth = 1.5f;
            var bc = new StyleColor(TextSub);
            clock.style.borderTopColor = bc; clock.style.borderBottomColor = bc;
            clock.style.borderLeftColor = bc; clock.style.borderRightColor = bc;
            Round(clock, 7.5f);
            clock.pickingMode = PickingMode.Ignore;
            // 바늘 — 내부 박스(12×12) 기준: 12시 방향 세로 + 3시 방향 가로
            var handV = new VisualElement { name = "ClockHandV" };
            handV.style.position = Position.Absolute;
            handV.style.left = 5.25f; handV.style.top = 0f;
            handV.style.width = 1.5f; handV.style.height = 6f;
            handV.style.backgroundColor = new StyleColor(TextSub);
            handV.pickingMode = PickingMode.Ignore;
            clock.Add(handV);
            var handH = new VisualElement { name = "ClockHandH" };
            handH.style.position = Position.Absolute;
            handH.style.left = 6f; handH.style.top = 5.25f;
            handH.style.width = 6f; handH.style.height = 1.5f;
            handH.style.backgroundColor = new StyleColor(TextSub);
            handH.pickingMode = PickingMode.Ignore;
            clock.Add(handH);
            return clock;
        }

        // =====================================================================
        //  좌측 병사 명부 (64,78) 480×882 #13181F — 제목/필터/검색/목록
        // =====================================================================
        private void BuildLeftPanel()
        {
            var panel = new VisualElement { name = "SoldierRoster" };
            Abs(panel, 64f, 78f, 480f, 882f);   // [144차 P6-TRN #1] y+36
            panel.style.backgroundColor = new StyleColor(PanelBg);
            Chrome(panel, 14f, ChromeBorder, 1f);   // [144차 P6-TRN #4] 크롬 r14+테두리(그림자: UI Toolkit 미지원 — 기록)
            _content.Add(panel);

            // 패널 제목 (24,24) — users_round + 병사 명부(fs22 [144차 P6-TRN #6]) + 보조제목(157,33) + 닫기
            var icon = IconEl("UI/TrainingHall/Icons/users_round", 24f, 24f, 23f, 23f);
            panel.Add(icon);
            var title = new Label("병사 명부");
            title.style.fontSize = 22f; title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(TextMain);
            Abs(title, 59f, 26f, 26f);
            panel.Add(title);
            var sub = new Label("훈련 중인 병사 목록");
            sub.style.fontSize = 11f; sub.style.color = new StyleColor(TextSub);
            Abs(sub, 157f, 33f, 13f);   // [144차 P6-TRN #6] 제목 우측 배치 (Figma 269:338)
            panel.Add(sub);
            var closeBtn = MakeCloseButton("CloseBtn", () => Close());   // [144차 P6-TRN #5]
            Abs(closeBtn, 426f, 24f, 30f);
            panel.Add(closeBtn);

            // 필터 (24,88) — 대기 중(active)/훈련 중/완료 (140×36, gap 6)
            _tabWaiting = MakeTab("대기 중", 0);
            Abs(_tabWaiting, 24f, 88f, 36f); _tabWaiting.style.width = 140f;
            panel.Add(_tabWaiting);
            _tabTraining = MakeTab("훈련 중", 1);
            Abs(_tabTraining, 170f, 88f, 36f); _tabTraining.style.width = 140f;
            panel.Add(_tabTraining);
            _tabDone = MakeTab("완료", 2);
            Abs(_tabDone, 316f, 88f, 36f); _tabDone.style.width = 140f;
            panel.Add(_tabDone);
            ApplyTabStyle();

            // 병사 검색 (24,140) 432×34 #0F141B — search 아이콘 + 필드 + 정렬 아이콘 [144차 P6-TRN #9] r6+테두리
            var searchBg = new VisualElement { name = "SearchBox" };
            Abs(searchBg, 24f, 140f, 432f, 34f);
            searchBg.style.backgroundColor = new StyleColor(InputBg);
            Chrome(searchBg, 6f, ChromeBorder, 1f);
            panel.Add(searchBg);
            searchBg.Add(IconEl("UI/TrainingHall/Icons/search", 12f, 10f, 15f, 15f));
            _searchField = new TextField("");
            _searchField.name = "SoldierSearch";
            Abs(_searchField, 35f, 10f, 24f);   // [144차 P6-TRN #9] y 5→10 (Figma 269:352)
            _searchField.style.width = 360f;
            _searchField.style.fontSize = 12f;
            _searchField.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            _searchField.style.borderTopWidth = 0f; _searchField.style.borderBottomWidth = 0f;
            _searchField.style.borderLeftWidth = 0f; _searchField.style.borderRightWidth = 0f;
            _searchField.RegisterValueChangedCallback(_ => RebuildSoldierList());
            searchBg.Add(_searchField);
            searchBg.Add(IconEl("UI/TrainingHall/Icons/arrow_down_wide_narrow", 404f, 9f, 16f, 16f));

            // 병사 목록 영역 (24,190) 432×668 — ScrollView [144차 P6-TRN #9] r10+테두리 (Figma 271:390)
            _listScroll = new ScrollView(ScrollViewMode.Vertical) { name = "SoldierList" };
            Abs(_listScroll, 24f, 190f, 432f, 668f);
            _listScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Chrome(_listScroll, 10f, ChromeBorder, 1f);
            panel.Add(_listScroll);
        }

        /// <summary>[144차 P6-TRN #5] 닫기 버튼 — Figma 269:339: 30×30 #191F27 r6 테두리 + 닫기 아이콘 VECTOR 9×9 #8B949E/1.6(교차바 프레임 근사 — 텍스트 치환 해소).</summary>
        private static Button MakeCloseButton(string name, System.Action onClick)
        {
            var b = new Button(onClick) { name = name };
            b.style.width = 30f; b.style.height = 30f;
            b.style.backgroundColor = new StyleColor(SubBg);
            Chrome(b, 6f, ChromeBorder, 1f);
            b.text = string.Empty;
            var glyph = new VisualElement { name = "CloseGlyph" };
            glyph.style.position = Position.Absolute;
            glyph.style.left = 9.5f; glyph.style.top = 9.5f;   // 내부 박스 28×28 중앙에 9×9 아이콘
            glyph.style.width = 9f; glyph.style.height = 9f;
            glyph.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 2; i++)
            {
                var bar = new VisualElement { name = "CloseBar" + i };
                bar.style.position = Position.Absolute;
                bar.style.left = 0f; bar.style.top = 3.7f;
                bar.style.width = 9f; bar.style.height = 1.6f;
                bar.style.backgroundColor = new StyleColor(TextSub);
                bar.style.rotate = new Rotate(new Angle(i == 0 ? 45f : -45f, AngleUnit.Degree));
                bar.pickingMode = PickingMode.Ignore;
                glyph.Add(bar);
            }
            b.Add(glyph);
            return b;
        }

        private Button MakeTab(string text, int filterIndex)
        {
            var b = new Button(() => { _filter = filterIndex; ApplyTabStyle(); RebuildSoldierList(); })
            { name = "FilterTab_" + filterIndex, text = text };
            b.style.height = 36f;
            b.style.fontSize = 12f;                                    // [144차 P6-TRN #6] fs12 700 (Figma 271:344)
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            return b;
        }

        /// <summary>Figma 필터 탭 스타일 — active #58A6FF fill + #0B0E14 텍스트 / inactive #191F27 + #8B949E.</summary>
        private void ApplyTabStyle()
        {
            StyleTab(_tabWaiting, _filter == 0);
            StyleTab(_tabTraining, _filter == 1);
            StyleTab(_tabDone, _filter == 2);
        }

        private static void StyleTab(Button b, bool active)
        {
            if (b == null) return;
            b.style.backgroundColor = new StyleColor(active ? Accent : SubBg);
            b.style.color = new StyleColor(active ? DarkText : TextSub);
            Chrome(b, 6f, active ? Accent : ChromeBorder, 1f);   // [144차 P6-TRN #6] r6 + 테두리(active 자색/inactive #30363D@0.502)
        }

        // =====================================================================
        //  우측 훈련 진행 현황 (568,78) 968×882 #13181F
        // =====================================================================
        private void BuildRightPanel()
        {
            var panel = new VisualElement { name = "TrainingProgress" };
            Abs(panel, 568f, 78f, 968f, 882f);   // [144차 P6-TRN #1] y+36
            panel.style.backgroundColor = new StyleColor(PanelBg);
            Chrome(panel, 14f, ChromeBorder, 1f);   // [144차 P6-TRN #4] 크롬 r14+테두리(그림자: UI Toolkit 미지원 — 기록)
            _content.Add(panel);

            // 패널 제목 — castle + 훈련 진행 현황(fs22 [144차 P6-TRN #6]) + 보조제목(203,33) + 닫기
            panel.Add(IconEl("UI/TrainingHall/Icons/castle", 24f, 24f, 23f, 23f));
            var title = new Label("훈련 진행 현황");
            title.style.fontSize = 22f; title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(TextMain);
            Abs(title, 59f, 26f, 26f);
            panel.Add(title);
            var sub = new Label("훈련 진행률, 레벨, 완료 상태");
            sub.style.fontSize = 11f; sub.style.color = new StyleColor(TextSub);
            Abs(sub, 203f, 33f, 13f);   // [144차 P6-TRN #6] 제목 우측 배치 (Figma 269:434)
            panel.Add(sub);
            var closeBtn = MakeCloseButton("CloseBtn2", () => Close());   // [144차 P6-TRN #5]
            Abs(closeBtn, 914f, 24f, 30f);
            panel.Add(closeBtn);

            // 훈련소 운영 정보 (24,88) 920×62 #191F27 [144차 P6-TRN #10] r10+테두리 (Figma 269:437)
            var op = new VisualElement { name = "FacilityInfo" };
            Abs(op, 24f, 88f, 920f, 62f);
            op.style.backgroundColor = new StyleColor(SubBg);
            Chrome(op, 10f, ChromeBorder, 1f);
            panel.Add(op);
            var fIcon = new VisualElement { name = "FacilityIcon" };
            Abs(fIcon, 16f, 13f, 36f); fIcon.style.height = 36f;
            fIcon.style.backgroundColor = new StyleColor(IconBg);
            Round(fIcon, 6f);   // [144차 P6-TRN #10] r6 (Figma 269:438)
            fIcon.Add(IconEl("UI/TrainingHall/Icons/flag", 8f, 8f, 20f, 20f));
            op.Add(fIcon);
            _facilityName = new Label("—");
            _facilityName.style.fontSize = 14f; _facilityName.style.color = new StyleColor(TextMain);
            _facilityName.style.unityFontStyleAndWeight = FontStyle.Bold;
            Abs(_facilityName, 64f, 15f, 14f);   // [144차 P6-TRN #10] fs14 y15 (Figma 269:441)
            op.Add(_facilityName);
            _facilityDesc = new Label($"한 번에 최대 {Capacity}명 · 훈련 중인 병사와 완료 병력을 확인 · 완료 후 병력 배치 가능");
            _facilityDesc.style.fontSize = 11f; _facilityDesc.style.color = new StyleColor(TextSub);
            Abs(_facilityDesc, 64f, 34f, 13f);
            op.Add(_facilityDesc);
            var opPill = new VisualElement { name = "OpState" };
            Abs(opPill, 842f, 19f, 62f); opPill.style.height = 23f;   // [144차 P6-TRN #10] y19 (Figma 269:443)
            opPill.style.backgroundColor = new StyleColor(AccentSoft);
            Round(opPill, 6f);
            opPill.Add(IconDot(9f, 9f));
            _opState = new Label("대기");
            _opState.style.fontSize = 11f; _opState.style.color = new StyleColor(Accent);
            Abs(_opState, 20f, 5f, 11f);   // [144차 P6-TRN #10] fs11 x20 (Figma 269:445 계통)
            opPill.Add(_opState);
            op.Add(opPill);

            // 열 제목 (24,166) 920×30 #0F141B — 7칼럼 (Figma x: 38/290/424/498/622/748/846 → 패널 rel 14/266/400/474/598/724/822)
            var cols = new VisualElement { name = "ColumnHeader" };
            Abs(cols, 24f, 166f, 920f, 30f);
            cols.style.backgroundColor = new StyleColor(InputBg);
            Round(cols, 4f);
            panel.Add(cols);
            AddCol(cols, "병사", 14f);
            AddCol(cols, "병과", 266f);
            AddCol(cols, "레벨", 400f, 60f, true);      // [144차 P6-TRN #11] fs11 + 우정렬 4열 (Figma 269:450·271:135-137)
            AddCol(cols, "훈련 진행", 474f);
            AddCol(cols, "남은 시간", 598f, 112f, true);
            AddCol(cols, "완료 예정", 724f, 84f, true);
            AddCol(cols, "상태", 822f, 84f, true);

            // 훈련 항목 스크롤 영역 (24,196) 920×434
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "TrainingList" };
            Abs(scroll, 24f, 196f, 920f, 434f);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            panel.Add(scroll);
            _trainingScrollRoot = scroll;

            // 훈련 완료 병력 (24,646) 920×80 #22C55E/0.08 [144차 P6-TRN #12] r10+테두리 (Figma 271:272)
            var done = new VisualElement { name = "CompletedForce" };
            Abs(done, 24f, 646f, 920f, 80f);
            done.style.backgroundColor = new StyleColor(GreenSoft);
            Chrome(done, 10f, GreenBorder, 1f);
            panel.Add(done);
            done.Add(IconEl("UI/TrainingHall/Icons/badge_check", 16f, 27f, 26f, 26f));
            _completedTitle = new Label("훈련 완료 병사 0명");
            _completedTitle.style.fontSize = 13f; _completedTitle.style.color = new StyleColor(TextMain);   // [144차 P6-TRN #12] fs13 400 (Figma 271:274)
            Abs(_completedTitle, 54f, 23f, 16f);
            done.Add(_completedTitle);
            _completedDetail = new Label("배치 후 주둔 병력 — / 240명");
            _completedDetail.style.fontSize = 11f; _completedDetail.style.color = new StyleColor(TextSub);
            Abs(_completedDetail, 54f, 44f, 13f);
            done.Add(_completedDetail);
            _deployBtn = new Button(OnDeployCompleted) { name = "DeployBtn" };
            var dIcon = IconEl("UI/TrainingHall/Icons/user_round_check", 58f, 13f, 17f, 17f);
            dIcon.style.left = 59f; dIcon.style.top = 12f;   // [144차 P6-TRN #12] (59,12) (Figma 271:276)
            dIcon.style.position = Position.Absolute;
            dIcon.pickingMode = PickingMode.Ignore;
            _deployBtn.Add(dIcon);
            var dText = new Label("완료 병력 배치");
            dText.style.fontSize = 13f; dText.style.color = new StyleColor(Green);   // [144차 P6-TRN #12] fs13 700
            dText.style.unityFontStyleAndWeight = FontStyle.Bold;
            dText.style.position = Position.Absolute;
            dText.style.left = 83f; dText.style.top = 13f;
            dText.pickingMode = PickingMode.Ignore;
            _deployBtn.Add(dText);
            Abs(_deployBtn, 684f, 19f, 42f);
            _deployBtn.style.width = 220f;
            _deployBtn.style.backgroundColor = new StyleColor(GreenSoft);
            Chrome(_deployBtn, 6f, GreenBorder, 1f);
            done.Add(_deployBtn);

            // 운영 안내 (24,742) — info + 문구
            var note = new VisualElement { name = "OpNote" };
            Abs(note, 24f, 742f, 920f, 13f);
            panel.Add(note);
            note.Add(IconEl("UI/TrainingHall/Icons/info", 0f, 0f, 13f, 13f));
            var noteText = new Label($"훈련소를 닫아도 훈련은 계속되며, 완료된 병사는 바로 배치할 수 있습니다. (정원 {Capacity}명 · 훈련 1회 게임시간 1시간 · 비용 병사 레벨×20G)");   // [144차 P6-TRN #12] Figma 271:279 문구+마침표 — 괄호부는 게임 확장 기록(유지)
            noteText.style.fontSize = 11f; noteText.style.color = new StyleColor(TextSub);
            Abs(noteText, 20f, 0f, 13f);
            note.Add(noteText);
        }

        private ScrollView _trainingScrollRoot;

        /// <summary>[144차 P6-TRN #11] 열 제목 — fs11 (Figma 269:448), 레벨/남은 시간/완료 예정/상태 4열 우정렬(Figma 269:450·271:135-137).</summary>
        private static void AddCol(VisualElement parent, string text, float x, float w = 0f, bool alignRight = false)
        {
            var l = new Label(text);
            l.style.fontSize = 11f;
            l.style.color = new StyleColor(TextSub);
            Abs(l, x, 8f, 13f);
            if (alignRight && w > 0f)
            {
                l.style.width = w;
                l.style.unityTextAlign = TextAnchor.MiddleRight;
            }
            parent.Add(l);
        }

        // =====================================================================
        //  도움말 바 (64,984) 1472×14
        // =====================================================================
        private void BuildHelpBar()
        {
            var help = new VisualElement { name = "HelpBar" };
            Abs(help, 64f, 984f, 1472f, 14f);   // [144차 P6-TRN #1] y+36
            _content.Add(help);
            var guide = new Label("병사 목록에서 훈련 중인 병사를 확인하고, 오른쪽 창에서 진행률과 완료 상태를 확인하세요.");   // [144차 P6-TRN #13] Figma 269:561 문구
            guide.style.fontSize = 12f; guide.style.color = new StyleColor(TextSub);
            Abs(guide, 0f, 0f, 14f);
            help.Add(guide);
            var keys = new Label("클릭 병사 선택 · Enter 완료 확인 · Esc 닫기");
            keys.style.fontSize = 12f; keys.style.color = new StyleColor(TextSub);
            Abs(keys, 1253f, 0f, 14f);   // [144차 P6-TRN #13] x1253 (Figma 269:562)
            help.Add(keys);
        }

        // =====================================================================
        //  폴링/갱신 (300ms — TrainingHallSystem 300ms 드라이버와 동일 주기)
        // =====================================================================
        private void PollTick()
        {
            if (!IsOpen) return;
            UpdateHeader();
            string leftSig = ComputeLeftSignature();
            if (leftSig != _lastLeftSig)
                RebuildSoldierList();
            else
                RefreshSoldierRowsInPlace();
            string rightSig = ComputeRightSignature();
            if (rightSig != _lastRightSig)
                RebuildTrainingRows();
            else
                RefreshTrainingRowsInPlace();
            RefreshCompletedSection();
        }

        private void RefreshAll()
        {
            UpdateHeader();
            RebuildSoldierList();
            RebuildTrainingRows();
            RefreshCompletedSection();
        }

        private void UpdateHeader()
        {
            ResolveTerritory();
            if (_locLabel != null)
                _locLabel.text = _territoryLabel;   // [144차 P6-TRN #2] 브레드크럼 구조 — 접두어 제거(화면명 라벨 분리)
            var tm = TimeManager.Instance;
            if (_dayLabel != null)
                _dayLabel.text = tm != null ? $"{tm.CurrentDay}일차 · {(tm.IsDay ? "낮" : "밤")}" : "—";
            if (_clockLabel != null)
                _clockLabel.text = tm != null ? $"{tm.Hour:D2}:{tm.Minute:D2}" : "—";
            if (_facilityName != null)
                _facilityName.text = _territoryKey != null ? $"{_territoryLabel} · 제1 훈련소  Lv.1" : "훈련소 미구동";
            if (_opState != null)
                _opState.text = TrainingHallSystem.ActiveSessions.Count > 0 ? "훈련 중" : "대기";
        }

        private void ResolveTerritory()
        {
            _territoryKey = null;
            _territoryLabel = "영지 밖";
            var player = GameObject.FindGameObjectWithTag("Player");
            var db = TerritoryDatabase.Instance;
            if (player == null || db == null) return;
            var tid = db.ResolveTerritoryAt(player.transform.position, 80f);
            if (tid.HasValue)
            {
                var def = db.GetDefinition(tid.Value);
                _territoryKey = tid.Value.ToString();
                _territoryLabel = string.IsNullOrEmpty(def.territoryName) ? _territoryKey : def.territoryName;
            }
        }

        private string ComputeLeftSignature()
        {
            return $"{_filter}|{TrainingHallSystem.ActiveSessions.Count}|{TrainingHallSystem.Completed.Count}|{_selectedStableId}|{_searchField?.value?.Length ?? 0}";
        }

        private string ComputeRightSignature()
        {
            return $"S{TrainingHallSystem.ActiveSessions.Count}";
        }

        // =====================================================================
        //  좌측 병사 목록 — 필터/검색/선택
        // =====================================================================
        private void RebuildSoldierList()
        {
            _lastLeftSig = ComputeLeftSignature();
            if (_listScroll == null) return;
            _listScroll.contentContainer.Clear();
            _soldierRows.Clear();
            string search = _searchField != null ? (_searchField.value ?? string.Empty).Trim() : string.Empty;

            var db = TerritoryDatabase.Instance;
            if (_territoryKey == null || db == null)
            {
                var empty = new Label("영지 밖 — 훈련소가 있는 영지로 이동하세요");
                empty.style.fontSize = 14f; empty.style.color = new StyleColor(TextSub);
                empty.style.marginLeft = 12f; empty.style.marginTop = 8f;
                _listScroll.contentContainer.Add(empty);
                return;
            }

            // 훈련소 보유 판정 — ConstructionManager.HasStructure("training_hall")
            var cm = Object.FindAnyObjectByType<ConstructionManager>();
            if (cm == null || !cm.HasStructure(_territoryKey, ConstructionManager.TrainingHallBlueprintEffectId))
            {
                var nohall = new Label("이 영지에는 훈련소가 없습니다 — 건설 메뉴에서 훈련소를 건설하세요");
                nohall.style.fontSize = 14f; nohall.style.color = new StyleColor(TextSub);
                nohall.style.marginLeft = 12f; nohall.style.marginTop = 8f;
                nohall.style.whiteSpace = WhiteSpace.Normal;
                _listScroll.contentContainer.Add(nohall);
                return;
            }

            if (_filter == 1)
            {
                // 훈련 중 — 세션 목록
                AddSectionTitle(_listScroll, "훈련 중인 병사", $"{TrainingHallSystem.ActiveSessions.Count}명");
                var sessions = TrainingHallSystem.ActiveSessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    var s = sessions[i];
                    if (!MatchSearch(s.GuardName, search)) continue;
                    var guard = TrainingHallSystemResolveGuard(s.StableId);
                    AddSoldierRow(_listScroll, s.GuardName, guard != null ? guard.Level : s.LevelAtStart,
                        JobTitleOf(guard), "훈련 중", TrainingHallSystem.GetTier(s.StableId), s.StableId,
                        SoldierRowView.RowState.Training, guard);
                }
            }
            else if (_filter == 2)
            {
                // 완료 — 수령 대기
                AddSectionTitle(_listScroll, "완료된 병사", $"{TrainingHallSystem.Completed.Count}명");
                var done = TrainingHallSystem.Completed;
                for (int i = 0; i < done.Count; i++)
                {
                    var c = done[i];
                    if (!MatchSearch(c.GuardName, search)) continue;
                    var guard = TrainingHallSystemResolveGuard(c.StableId);
                    AddSoldierRow(_listScroll, c.GuardName, c.NewLevel, JobTitleOf(guard),
                        "완료", TrainingHallSystem.GetTier(c.StableId), c.StableId,
                        c.Deployed ? SoldierRowView.RowState.Deployed : SoldierRowView.RowState.Completed, guard);
                }
            }
            else
            {
                // 대기 중 — 소속 병사 중 미훈련
                var guards = TrainingHallSystem.GetGuardsForTerritory(_territoryKey);
                int waiting = 0;
                var candidates = new List<GuardPlaceholder>();
                for (int i = 0; i < guards.Count; i++)
                {
                    var g = guards[i];
                    if (g == null || g.IsDead) continue;
                    if (TrainingHallSystem.IsTraining(g.StableId)) continue;
                    if (!MatchSearch(g.GuardName, search)) continue;
                    candidates.Add(g);
                    waiting++;
                }
                AddSectionTitle(_listScroll, "대기 중인 병사", $"{waiting}명");
                for (int i = 0; i < candidates.Count; i++)
                {
                    var g = candidates[i];
                    AddSoldierRow(_listScroll, g.GuardName, g.Level, JobTitleOf(g),
                        "대기 중", TrainingHallSystem.GetTier(g.StableId), g.StableId,
                        SoldierRowView.RowState.Waiting, g);
                }
            }
        }

        private static GuardPlaceholder TrainingHallSystemResolveGuard(string stableId)
        {
            // GuardManager 전수에서 StableId 매칭(TrainingHallSystem.FindGuard는 private — UI측 동일 로직)
            if (GuardManager.Instance == null) return null;
            foreach (var kvp in GuardManager.Instance.GetAllRegisteredGuards())
                if (kvp.Value != null && kvp.Value.StableId == stableId) return kvp.Value;
            return null;
        }

        private static string JobTitleOf(GuardPlaceholder g) =>
            !string.IsNullOrEmpty(g?.JobTitle) ? g.JobTitle : "병사";   // 병과 표기 — 기존 소스(JobTitle) 계약(위조 금지)

        private static bool MatchSearch(string name, string search) =>
            string.IsNullOrEmpty(search) || (!string.IsNullOrEmpty(name) && name.Contains(search));

        private static void AddSectionTitle(VisualElement parent, string title, string count)
        {
            var row = new VisualElement { name = "SectionTitle" };
            row.style.height = 14f;
            row.style.marginTop = 12f; row.style.marginBottom = 10f;
            row.style.marginLeft = 12f; row.style.marginRight = 12f;
            var t = new Label(title);
            t.style.fontSize = 12f; t.style.color = new StyleColor(TextMain);   // [144차 P6-TRN #14] fs12 700 (Figma 271:346)
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.position = Position.Absolute;
            t.style.left = 0f; t.style.top = 0f;
            row.Add(t);
            var c = new Label(count);
            c.style.fontSize = 11f; c.style.color = new StyleColor(TextSub);   // [144차 P6-TRN #14] fs11 (Figma 271:347)
            c.style.position = Position.Absolute;
            c.style.right = 0f; c.style.top = 1f;
            row.Add(c);
            parent.Add(row);
        }

        private void AddSoldierRow(VisualElement parent, string name, int level, string job,
            string stateText, int tier, string stableId, SoldierRowView.RowState state, GuardPlaceholder guard)
        {
            var row = new SoldierRowView(stableId, name, level, job, stateText, tier, state,
                onSelect: () => { _selectedStableId = stableId; RefreshSelectionHighlights(); },
                onPrimary: () => OnRowPrimary(stableId, state, guard),
                onCancel: () => OnRowCancel(stableId, state));
            parent.Add(row);
            _soldierRows.Add(row);
            if (stableId == _selectedStableId)
                row.SetSelected(true);
        }

        private void RefreshSelectionHighlights()
        {
            for (int i = 0; i < _soldierRows.Count; i++)
                _soldierRows[i].SetSelected(_soldierRows[i].StableId == _selectedStableId);
            for (int i = 0; i < _trainingRows.Count; i++)
                _trainingRows[i].SetSelected(_trainingRows[i].StableId == _selectedStableId);
        }

        private void OnRowPrimary(string stableId, SoldierRowView.RowState state, GuardPlaceholder guard)
        {
            if (state == SoldierRowView.RowState.Waiting)
            {
                var err = TrainingHallSystem.StartTraining(guard, _territoryKey, out _);
                UTKToastService.Show(err ?? $"훈련 시작 — {guard?.GuardName} (비용 {TrainingHallSystem.CostForLevel(guard?.Level ?? 0)}G)");
                RebuildSoldierList();
                RebuildTrainingRows();
                RefreshCompletedSection();
            }
            else if (state == SoldierRowView.RowState.Training)
            {
                bool ok = TrainingHallSystem.CompleteNow(stableId);
                UTKToastService.Show(ok ? "훈련 완료 — 레벨과 능력치가 상승했습니다" : "훈련 중인 병사가 아닙니다");
                RebuildSoldierList();
                RebuildTrainingRows();
                RefreshCompletedSection();
            }
            else if (state == SoldierRowView.RowState.Completed)
            {
                bool ok = TrainingHallSystem.MarkDeployed(stableId);
                UTKToastService.Show(ok ? "완료 병력 배치 — 주둔 병력에 반영되었습니다" : "배치할 병력이 없습니다");
                RefreshCompletedSection();
                RebuildSoldierList();
            }
            else
            {
                UTKToastService.Show("이미 배치된 병력입니다");
            }
        }

        private void OnRowCancel(string stableId, SoldierRowView.RowState state)
        {
            if (state != SoldierRowView.RowState.Training)
            {
                UTKToastService.Show("취소할 훈련이 없습니다");
                return;
            }
            bool ok = TrainingHallSystem.CancelTraining(stableId);
            UTKToastService.Show(ok ? "훈련 취소 — 비용은 환불되지 않습니다" : "훈련 중인 병사가 아닙니다");
            RebuildSoldierList();
            RebuildTrainingRows();
        }

        private void OnDeployCompleted()
        {
            int deployed = 0;
            var done = TrainingHallSystem.Completed;
            for (int i = 0; i < done.Count; i++)
                if (!done[i].Deployed && TrainingHallSystem.MarkDeployed(done[i].StableId))
                    deployed++;
            UTKToastService.Show(deployed > 0 ? $"완료 병력 {deployed}명 배치 — 주둔 병력에 반영되었습니다" : "배치할 완료 병력이 없습니다");
            RefreshCompletedSection();
            RebuildSoldierList();
        }

        private void RefreshSoldierRowsInPlace()
        {
            for (int i = 0; i < _soldierRows.Count; i++)
                _soldierRows[i].RefreshInPlace();
        }

        // =====================================================================
        //  우측 훈련 행 — 진행률/남은 시간/완료 예정 실시간
        // =====================================================================
        private void RebuildTrainingRows()
        {
            _lastRightSig = ComputeRightSignature();
            if (_trainingScrollRoot == null) return;
            _trainingScrollRoot.contentContainer.Clear();
            _trainingRows.Clear();
            var sessions = TrainingHallSystem.ActiveSessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                var row = new TrainingRowView(i, s.StableId, s.GuardName,
                    onSelect: () => { _selectedStableId = s.StableId; RefreshSelectionHighlights(); });
                _trainingScrollRoot.contentContainer.Add(row);
                _trainingRows.Add(row);
                if (s.StableId == _selectedStableId)
                    row.SetSelected(true);
            }
            if (sessions.Count == 0)
            {
                var empty = new Label("훈련 중인 병사가 없습니다 — 좌측 목록에서 병사를 선택해 훈련을 시작하세요");
                empty.style.fontSize = 14f; empty.style.color = new StyleColor(TextSub);
                empty.style.marginLeft = 14f; empty.style.marginTop = 10f;
                _trainingScrollRoot.contentContainer.Add(empty);
            }
            RefreshTrainingRowsInPlace();
        }

        private void RefreshTrainingRowsInPlace()
        {
            var tm = TimeManager.Instance;
            float now = tm != null ? tm.GameTime : 0f;
            var sessions = TrainingHallSystem.ActiveSessions;
            for (int i = 0; i < _trainingRows.Count; i++)
            {
                var row = _trainingRows[i];
                if (i >= sessions.Count) break;
                row.Refresh(sessions[i], now);
            }
        }

        private void RefreshCompletedSection()
        {
            if (_completedTitle == null) return;
            var done = TrainingHallSystem.Completed;
            int pending = 0;
            for (int i = 0; i < done.Count; i++)
                if (!done[i].Deployed) pending++;
            _completedTitle.text = pending > 0 ? $"훈련 완료 병사 {pending}명" : "훈련 완료 병사 없음";
            int garrison = ResolveGarrisonCount();
            _completedDetail.text = $"배치 후 주둔 병력 {garrison} → {garrison + pending} / {GarrisonCap}명";
            _deployBtn.SetEnabled(pending > 0);
            if (TrainingHallSystem.Completed.Count > 0 && done.Count > 0)
            {
                var latest = done[done.Count - 1];
                _completedTitle.text = pending > 0
                    ? $"훈련 완료 {latest.GuardName} · 대기 {pending}명"
                    : $"훈련 완료 {latest.GuardName} (배치 완료)";
            }
        }

        private int ResolveGarrisonCount()
        {
            var tm = TerritoryDatabase.Instance;
            if (_territoryKey == null || tm == null) return 0;
            if (!TrainingHallSystemTryParse(_territoryKey, out var tid)) return 0;
            var guards = GuardManager.Instance != null ? GuardManager.Instance.GetGuardsInTerritory(tid) : null;
            return guards != null ? guards.Count : 0;
        }

        private static bool TrainingHallSystemTryParse(string key, out TerritoryId id)
        {
            id = default;
            if (string.IsNullOrEmpty(key)) return false;
            string[] parts = key.Split('_');
            if (parts.Length != 2) return false;
            if (!System.Enum.TryParse(parts[0], out NationType nation)) return false;
            if (!int.TryParse(parts[1], out int index) || index < 1) return false;
            id = new TerritoryId(nation, index);
            return true;
        }

        // =====================================================================
        //  단축키 — Enter 완료 확인(선택 행 1차 액션) (Esc는 UTKWindowBase 스택 규약)
        // =====================================================================
        private void RegisterKeyTick()
        {
            schedule.Execute(KeyTick).Every(100);
        }

        private void KeyTick()
        {
            if (!IsOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame)
            {
                if (string.IsNullOrEmpty(_selectedStableId))
                {
                    UTKToastService.Show("선택된 병사가 없습니다 — 목록에서 병사를 클릭하세요");
                    return;
                }
                var state = ResolveRowState(_selectedStableId);
                var guard = TrainingHallSystemResolveGuard(_selectedStableId);
                OnRowPrimary(_selectedStableId, state, guard);
            }
        }

        private SoldierRowView.RowState ResolveRowState(string stableId)
        {
            if (TrainingHallSystem.FindSession(stableId) != null) return SoldierRowView.RowState.Training;
            var done = TrainingHallSystem.Completed;
            for (int i = 0; i < done.Count; i++)
                if (done[i].StableId == stableId)
                    return done[i].Deployed ? SoldierRowView.RowState.Deployed : SoldierRowView.RowState.Completed;
            return SoldierRowView.RowState.Waiting;
        }

        // =====================================================================
        //  좌측 병사 항목 행 — 408×86 카드 (Figma: 등급 5px/아바타 53/이름 20px/병과 17px/pill/버튼)
        // =====================================================================
        private sealed class SoldierRowView : VisualElement
        {
            public enum RowState { Waiting, Training, Completed, Deployed }

            public readonly string StableId;
            private readonly Label _levelLabel;
            private readonly Label _stateLabel;
            private readonly VisualElement _strip;
            private bool _selected;
            private readonly System.Func<string> _liveLevel;

            public SoldierRowView(string stableId, string name, int level, string job, string stateText, int tier,
                RowState state, System.Action onSelect, System.Action onPrimary, System.Action onCancel)
            {
                StableId = stableId;
                style.height = 86f;
                style.marginTop = 10f;
                style.marginLeft = 12f; style.marginRight = 12f;
                style.backgroundColor = new StyleColor(CardBg);
                Round(this, 9.6f);
                style.overflow = Overflow.Hidden;
                pickingMode = PickingMode.Position;
                RegisterCallback<ClickEvent>(_ => onSelect?.Invoke());

                // 등급 표시 5×86
                _strip = new VisualElement { name = "TierStrip" };
                _strip.style.width = 5f; _strip.style.height = 86f;
                _strip.style.backgroundColor = new StyleColor(Accent);
                _strip.pickingMode = PickingMode.Ignore;
                Add(_strip);

                // 아바타 53×53 (24,17)
                var avatar = new VisualElement { name = "Avatar" };
                avatar.style.position = Position.Absolute;
                avatar.style.left = 14f; avatar.style.top = 17f;
                avatar.style.width = 53f; avatar.style.height = 53f;
                avatar.style.backgroundColor = new StyleColor(AvatarBg);
                Chrome(avatar, 26.4f, ChromeBorder, 1.2f);   // [144차 P6-TRN #15] 원형 r26.4 + 테두리 1.2 (Figma 271:350)
                avatar.pickingMode = PickingMode.Ignore;
                var sIcon = IconEl("UI/TrainingHall/Icons/soldier", 14f, 14f, 24f, 24f);
                sIcon.style.position = Position.Absolute;
                avatar.Add(sIcon);
                Add(avatar);

                // 병사 정보 — 이름+레벨 / 병과+상태
                var nameL = new Label(name);
                nameL.style.fontSize = 16.8f; nameL.style.color = new StyleColor(TextMain);   // [144차 P6-TRN #15] fs16.8 700 (Figma 271:355)
                nameL.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameL.style.position = Position.Absolute;
                nameL.style.left = 82f; nameL.style.top = 12f;
                nameL.pickingMode = PickingMode.Ignore;
                Add(nameL);
                _levelLabel = new Label($"Lv.{level}");
                _levelLabel.style.fontSize = 14.4f; _levelLabel.style.color = new StyleColor(Accent);   // [144차 P6-TRN #15] fs14.4 700 (Figma 271:356)
                _levelLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _levelLabel.style.position = Position.Absolute;
                _levelLabel.style.left = 206f; _levelLabel.style.top = 13f;
                _levelLabel.pickingMode = PickingMode.Ignore;
                Add(_levelLabel);
                var jobL = new Label(job);
                jobL.style.fontSize = 14.4f; jobL.style.color = new StyleColor(TextSub);   // [144차 P6-TRN #15] fs14.4 (Figma 271:358)
                jobL.style.position = Position.Absolute;
                jobL.style.left = 82f; jobL.style.top = 47f;
                jobL.pickingMode = PickingMode.Ignore;
                Add(jobL);

                // 상태 pill 50×19 + 훈련 티어
                var pill = new VisualElement { name = "StatePill" };
                pill.style.position = Position.Absolute;
                pill.style.left = 199f; pill.style.top = 37f;
                pill.style.width = 50.4f; pill.style.height = 18.8f;   // [144차 P6-TRN #15] 50.4×18.8 (Figma 271:360)
                pill.style.backgroundColor = new StyleColor(PillBg);
                Chrome(pill, 4.8f, ChromeBorder, 1.2f);
                pill.pickingMode = PickingMode.Ignore;
                _stateLabel = new Label(stateText);
                _stateLabel.style.fontSize = 12f; _stateLabel.style.color = new StyleColor(Accent);
                _stateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;   // [144차 P6-TRN #15] 700 (Figma 271:361)
                _stateLabel.style.position = Position.Absolute;
                _stateLabel.style.left = 7f; _stateLabel.style.top = 2f;
                _stateLabel.pickingMode = PickingMode.Ignore;
                pill.Add(_stateLabel);
                Add(pill);
                var tierL = new Label($"Lv.{tier}");
                tierL.style.fontSize = 12f; tierL.style.color = new StyleColor(Accent);   // [144차 P6-TRN #15] fs12 700 (Figma 271:362)
                tierL.style.unityFontStyleAndWeight = FontStyle.Bold;
                tierL.style.position = Position.Absolute;
                tierL.style.left = 221f; tierL.style.top = 58f;
                tierL.pickingMode = PickingMode.Ignore;
                Add(tierL);

                // 버튼 130×36 (264,25) — 상태별 [144차 P6-TRN #7/#8] r7.2+테두리, 대기중 취소 버튼 추가(P0, Figma 271:366)
                if (state == RowState.Waiting)
                {
                    AddRowButton(this, 264f, "훈련", Violet, TextMain, onPrimary, Violet);
                    AddRowButton(this, 334f, "취소", GhostBtnBg, TextMain, onCancel, ChromeBorder);   // [144차 P6-TRN #8] P0 대기중 취소 — 기존 OnRowCancel 경로 연결
                }
                else if (state == RowState.Training)
                {
                    AddRowButton(this, 264f, "완료", Accent, DarkText, onPrimary, Accent);
                    AddRowButton(this, 334f, "취소", GhostBtnBg, TextMain, onCancel, ChromeBorder);
                }
                else if (state == RowState.Completed)
                    AddRowButton(this, 264f, "배치", Green, DarkText, onPrimary);   // 게임 확장 요소(Figma 좌측 완료행 미샘플링) — 테두리 스펙 부재, 기록
                else
                    AddRowButton(this, 264f, "배치됨", SubBg, TextSub, null);
            }

            public void SetSelected(bool selected)
            {
                _selected = selected;
                style.borderTopWidth = selected ? 1.8f : 0f;
                style.borderBottomWidth = selected ? 1.8f : 0f;
                style.borderLeftWidth = selected ? 1.8f : 0f;
                style.borderRightWidth = selected ? 1.8f : 0f;
                var c = new StyleColor(Accent);
                style.borderTopColor = c; style.borderBottomColor = c;
                style.borderLeftColor = c; style.borderRightColor = c;
            }

            public void RefreshInPlace()
            {
                // 폴링 시 실시간 레벨 반영(훈련 완료 직후) — TierStrip/텍스트는 구조 변화 시 rebuild
            }

            private static void AddRowButton(VisualElement parent, float x, string text, Color bg, Color fg, System.Action onClick, Color? borderColor = null)
            {
                var b = new Button(onClick) { text = text };
                b.style.position = Position.Absolute;
                b.style.left = x; b.style.top = 25f;
                b.style.width = 60f; b.style.height = 36f;
                b.style.fontSize = 14.4f;                                 // [144차 P6-TRN #7] fs14.4 700 (Figma 271:365)
                b.style.unityFontStyleAndWeight = FontStyle.Bold;
                b.style.color = new StyleColor(fg);
                b.style.backgroundColor = new StyleColor(bg);
                if (borderColor.HasValue)
                    Chrome(b, 7.2f, borderColor.Value, 1.2f);             // [144차 P6-TRN #7] r7.2 + 테두리 1.2 (Figma 271:364·366)
                else
                    Round(b, 7.2f);
                parent.Add(b);
            }
        }

        // =====================================================================
        //  우측 훈련 행 — 920×62 (#58A6FF/0.08·0.04 교대)
        // =====================================================================
        private sealed class TrainingRowView : VisualElement
        {
            public readonly string StableId;
            private readonly Color _defaultBorder;
            private readonly Label _name, _tier, _job, _level, _progress, _remaining, _eta, _state;
            private readonly VisualElement _barFill;
            private bool _selected;

            public TrainingRowView(int index, string stableId, string guardName,
                System.Action onSelect)
            {
                StableId = stableId;
                style.height = 62f;
                // [144차 P6-TRN #16] 행 배경/테두리 체계 (Figma 271:284·399): 행 1-2 = #58A6FF/0.078 + 테두리 #58A6FF/0.251, 행 3+ = #58A6FF/0.039 + 테두리 #30363D/0.251, r 없음
                _defaultBorder = index < 2 ? RowBorderActive : RowBorderDim;
                style.backgroundColor = new StyleColor(index < 2 ? AccentSoft : AccentSofter);
                Chrome(this, 0f, _defaultBorder, 1f);
                pickingMode = PickingMode.Position;
                RegisterCallback<ClickEvent>(_ => onSelect?.Invoke());

                var icon = new VisualElement { name = "SoldierIcon" };
                icon.style.position = Position.Absolute;
                icon.style.left = 14f; icon.style.top = 13f;
                icon.style.width = 36f; icon.style.height = 36f;
                icon.style.backgroundColor = new StyleColor(IconBg);
                Round(icon, 6f);   // [144차 P6-TRN #16] r6 (Figma 271:286)
                icon.pickingMode = PickingMode.Ignore;
                var ic = IconEl("UI/TrainingHall/Icons/soldier", 6f, 6f, 24f, 24f);
                ic.style.position = Position.Absolute;
                icon.Add(ic);
                Add(icon);

                _name = new Label(guardName);
                _name.style.fontSize = 13f; _name.style.color = new StyleColor(TextMain);   // [144차 P6-TRN #16] fs13 700 (Figma 271:290)
                _name.style.unityFontStyleAndWeight = FontStyle.Bold;
                _name.style.position = Position.Absolute;
                _name.style.left = 60f; _name.style.top = 15f;
                _name.pickingMode = PickingMode.Ignore;
                Add(_name);
                _tier = new Label("Lv.1");
                _tier.style.fontSize = 10f; _tier.style.color = new StyleColor(Accent);   // [144차 P6-TRN #16] fs10 (Figma 271:291)
                _tier.style.position = Position.Absolute;
                _tier.style.left = 60f; _tier.style.top = 35f;
                _tier.pickingMode = PickingMode.Ignore;
                Add(_tier);

                _job = new Label("병사");
                _job.style.fontSize = 13f; _job.style.color = new StyleColor(TextSub);   // [144차 P6-TRN #16] fs13 (Figma 271:292)
                _job.style.position = Position.Absolute;
                _job.style.left = 266f; _job.style.top = 23f;
                _job.pickingMode = PickingMode.Ignore;
                Add(_job);

                _level = new Label("Lv.—");
                _level.style.fontSize = 13f; _level.style.color = new StyleColor(TextMain);   // [144차 P6-TRN #16] fs13 우정렬 (Figma 271:293)
                _level.style.position = Position.Absolute;
                _level.style.left = 400f; _level.style.top = 23f;
                _level.style.width = 60f; _level.style.unityTextAlign = TextAnchor.MiddleRight;
                _level.pickingMode = PickingMode.Ignore;
                Add(_level);

                _progress = new Label("0%");
                _progress.style.fontSize = 12f; _progress.style.color = new StyleColor(Accent);   // [144차 P6-TRN #16] fs12 (Figma 271:295)
                _progress.style.position = Position.Absolute;
                _progress.style.left = 474f; _progress.style.top = 17f;
                _progress.pickingMode = PickingMode.Ignore;
                Add(_progress);
                var bar = new VisualElement { name = "ProgressBar" };
                bar.style.position = Position.Absolute;
                bar.style.left = 474f; bar.style.top = 41f;   // [144차 P6-TRN #16] y41 (Figma 271:296)
                bar.style.width = 110f; bar.style.height = 4f;
                bar.style.backgroundColor = new StyleColor(IconBg);
                Round(bar, 2f);   // [144차 P6-TRN #16] r2 트랙 (Figma 271:296)
                bar.pickingMode = PickingMode.Ignore;
                _barFill = new VisualElement { name = "ProgressFill" };
                _barFill.style.width = 0f; _barFill.style.height = 4f;
                _barFill.style.backgroundColor = new StyleColor(Accent);
                Round(_barFill, 2f);   // [144차 P6-TRN #16] r2 진행량
                _barFill.pickingMode = PickingMode.Ignore;
                bar.Add(_barFill);
                Add(bar);

                _remaining = new Label("00:00:00");
                _remaining.style.fontSize = 13f; _remaining.style.color = new StyleColor(TextMain);   // [144차 P6-TRN #16] fs13 우정렬 (Figma 271:298)
                _remaining.style.position = Position.Absolute;
                _remaining.style.left = 598f; _remaining.style.top = 23f;
                _remaining.style.width = 112f; _remaining.style.unityTextAlign = TextAnchor.MiddleRight;
                _remaining.pickingMode = PickingMode.Ignore;
                Add(_remaining);

                _eta = new Label("—");
                _eta.style.fontSize = 13f; _eta.style.color = new StyleColor(TextSub);   // [144차 P6-TRN #16] fs13 우정렬 (Figma 271:299)
                _eta.style.position = Position.Absolute;
                _eta.style.left = 724f; _eta.style.top = 23f;
                _eta.style.width = 84f; _eta.style.unityTextAlign = TextAnchor.MiddleRight;
                _eta.pickingMode = PickingMode.Ignore;
                Add(_eta);

                var pill = new VisualElement { name = "StatePill" };
                pill.style.position = Position.Absolute;
                pill.style.left = 856f; pill.style.top = 22f;
                pill.style.width = 50.4f; pill.style.height = 18.8f;   // [144차 P6-TRN #16] 50.4×18.8 r4.8+테두리 (Figma 271:301)
                pill.style.backgroundColor = new StyleColor(PillBg);
                Chrome(pill, 4.8f, ChromeBorder, 1.2f);
                pill.pickingMode = PickingMode.Ignore;
                _state = new Label("훈련 중");
                _state.style.fontSize = 12f; _state.style.color = new StyleColor(Accent);
                _state.style.unityFontStyleAndWeight = FontStyle.Bold;   // [144차 P6-TRN #16] 700 (Figma 271:302)
                _state.style.position = Position.Absolute;
                _state.style.left = 7f; _state.style.top = 2f;
                _state.pickingMode = PickingMode.Ignore;
                pill.Add(_state);
                Add(pill);
            }

            public void SetSelected(bool selected)
            {
                _selected = selected;
                if (selected)
                {
                    style.borderTopWidth = 1.8f; style.borderBottomWidth = 1.8f;
                    style.borderLeftWidth = 1.8f; style.borderRightWidth = 1.8f;
                    var c = new StyleColor(Accent);
                    style.borderTopColor = c; style.borderBottomColor = c;
                    style.borderLeftColor = c; style.borderRightColor = c;
                }
                else
                {
                    // [144차 P6-TRN #16] 비선택 시 기본 행 테두리 복원(1px — Figma 271:284·399)
                    style.borderTopWidth = 1f; style.borderBottomWidth = 1f;
                    style.borderLeftWidth = 1f; style.borderRightWidth = 1f;
                    var c = new StyleColor(_defaultBorder);
                    style.borderTopColor = c; style.borderBottomColor = c;
                    style.borderLeftColor = c; style.borderRightColor = c;
                }
            }

            /// <summary>진행률/남은 시간/완료 예정 실시간 갱신 (300ms 폴링).</summary>
            public void Refresh(TrainingHallSystem.TrainingSession s, float nowGameTime)
            {
                float p = TrainingHallSystem.GetProgress01(s, nowGameTime);
                _progress.text = $"{Mathf.RoundToInt(p * 100f)}%";
                _barFill.style.width = 110f * p;
                _remaining.text = TrainingHallSystem.FormatRemaining(TrainingHallSystem.GetRemainingSeconds(s, nowGameTime));
                float eta = s.EndGameTime % 86400f;
                int eh = (int)(eta / 3600f) % 24;
                int em = (int)(eta / 60f) % 60;
                _eta.text = $"{eh:D2}:{em:D2}";
                _tier.text = $"Lv.{TrainingHallSystem.GetTier(s.StableId)}";
                var g = TrainingHallSystemResolveGuardPublic(s.StableId);
                _level.text = $"Lv.{(g != null ? g.Level : s.LevelAtStart)}";
                _job.text = !string.IsNullOrEmpty(g?.JobTitle) ? g.JobTitle : "병사";
            }

            private static GuardPlaceholder TrainingHallSystemResolveGuardPublic(string stableId)
            {
                if (GuardManager.Instance == null) return null;
                foreach (var kvp in GuardManager.Instance.GetAllRegisteredGuards())
                    if (kvp.Value != null && kvp.Value.StableId == stableId) return kvp.Value;
                return null;
            }
        }

        // =====================================================================
        //  공용 헬퍼
        // =====================================================================
        private static void Abs(VisualElement el, float x, float y, float h)
        {
            el.style.position = Position.Absolute;
            el.style.left = x; el.style.top = y; el.style.height = h;
        }

        private static void Abs(VisualElement el, float x, float y, float h, float fontSize)
        {
            Abs(el, x, y, h);
            el.style.fontSize = fontSize;
        }

        private static void Round(VisualElement el, float r)
        {
            el.style.borderTopLeftRadius = r; el.style.borderTopRightRadius = r;
            el.style.borderBottomLeftRadius = r; el.style.borderBottomRightRadius = r;
        }

        /// <summary>[144차 P6-TRN #4] 크롬 스타일 — 코너 r + 테두리(Figma stroke 체계 일관: 글래스 알파 0.502 체계).
        /// 그림자(DROP_SHADOW 38px #000/0.251)는 UI Toolkit box-shadow 미지원으로 미구현 — 기록(구현 강요 여부 후속 판단).</summary>
        private static void Chrome(VisualElement el, float r, Color borderColor, float borderWidth)
        {
            Round(el, r);
            el.style.borderTopWidth = borderWidth; el.style.borderBottomWidth = borderWidth;
            el.style.borderLeftWidth = borderWidth; el.style.borderRightWidth = borderWidth;
            var c = new StyleColor(borderColor);
            el.style.borderTopColor = c; el.style.borderBottomColor = c;
            el.style.borderLeftColor = c; el.style.borderRightColor = c;
        }

        private static VisualElement IconEl(string resource, float x, float y, float w, float h)
        {
            var el = new VisualElement { name = "Icon_" + System.IO.Path.GetFileNameWithoutExtension(resource) };
            el.style.position = Position.Absolute;
            el.style.left = x; el.style.top = y;
            el.style.width = w; el.style.height = h;
            var tex = Resources.Load<Texture2D>(resource);
            if (tex != null)
            {
                el.style.backgroundImage = new StyleBackground(tex);
                el.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
            }
            el.pickingMode = PickingMode.Ignore;
            return el;
        }

        private static VisualElement IconDot(float x, float y)
        {
            var d = new VisualElement { name = "StatusDot" };
            d.style.position = Position.Absolute;
            d.style.left = x; d.style.top = y;
            d.style.width = 5f; d.style.height = 5f;
            d.style.backgroundColor = new StyleColor(Accent);
            Round(d, 2.5f);
            d.pickingMode = PickingMode.Ignore;
            return d;
        }
    }
}