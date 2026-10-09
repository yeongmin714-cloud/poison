// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;          // QuestManager, PlayerStats
using ProjectName.Core.Data;     // QuestData, QuestState, QuestObjective
using ProjectName.UI;            // QuestRewardPreview

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round A-3 — 퀘스트 저널(일지) 포팅.
    /// 원본: Assets/Scripts/UI/QuestJournalUI.cs (627줄, IMGUI) — 본 파일은 그 탭 구조(진행/완료)와
    /// 보상 연동을 차용하되, 데이터 소스는 원본의 자체 관리 리스트가 아닌 살아있는 QuestManager
    /// 실데이터 경로로 연결한다. 원본은 절대 수정하지 않는다.
    ///
    /// [탭 구조 — 원본 실측]
    ///  원본 _activeTab: 0=Active(진행 중), 1=Completed(완료). DrawTabButton으로 전환.
    ///  - 진행 탭: QuestManager.GetActiveQuests()      → 목표 진행도(current/required) + 보상 요약
    ///  - 완료 탭: QuestManager.GetCompletedQuests()   → "✅ 완료" 표시 + 보상 요약
    ///  - 보상 요약: QuestRewardPreview.GetRewardSummary(QuestData)  (ProjectName.UI)
    ///
    /// 폴링 400ms(schedule.Execute().Every) + J 키 토글 / ESC 닫기.
    /// </summary>
    public class QuestJournalUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static QuestJournalUTK _instance;
        public static QuestJournalUTK Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            _instance = new QuestJournalUTK();
            var go = new GameObject("QuestJournalUTK");
            Object.DontDestroyOnLoad(go);
            var updater = go.AddComponent<Updater>();
            updater.window = _instance;
            Debug.Log("[JournalUTK] 초기화 완료 — J키로 토글");
        }

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance == null)
            {
                _instance = new QuestJournalUTK();
                var go = new GameObject("QuestJournalUTK_UPD");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Updater>().window = _instance;
            }
        }

        /// <summary>열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 [Figma 93:230 QuestListPanel] 480×984 @(144,48) — 저널은 Figma 퀘스트 목록 패널 단독 창 =====
        private const float WinW = 480f;
        private const float WinH = 984f;
        /// <summary>[Figma 정합 v3] 저널 디자인공간 바운드(테스트 참조용) — 창 박스=JournalBounds×k(등배수).</summary>
        public static readonly Rect JournalBounds = new Rect(144f, 48f, WinW, WinH);
        private const long RefreshMs = 400L;
        private const int TabActive = 0;
        private const int TabCompleted = 1;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 퀘스트 저널 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKButton·UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color BgBase   = Hex(0x0B0E14);   // 최배경 — 슬롯/게이지 인셋
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(행/버튼)
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트) — 활성 탭/진행
            public static readonly Color Gold     = Hex(0xE3B341);   // 희귀/보상/골드
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/구분선
            public static readonly Color Success  = Hex(0x3FB950);   // success 그린 — 완료

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        /// <summary>GitHub-dark 버튼 인라인 오버라이드(이 창 한정) — IStyle 쇼트핸드 없음 → 4면 개별 대입.</summary>
        private static void StyleButton(Button btn, UTKButton.Variant variant)
        {
            if (btn == null) return;
            Color baseBg, hoverBg, textColor;
            switch (variant)
            {
                case UTKButton.Variant.Primary:
                    baseBg = GitHubDark.Accent; hoverBg = new Color32(0x79, 0xC0, 0xFF, 0xFF); textColor = GitHubDark.Panel; break;
                case UTKButton.Variant.Danger:
                    baseBg = new Color32(0xF8, 0x51, 0x49, 0xFF); hoverBg = new Color32(0xDA, 0x36, 0x33, 0xFF); textColor = GitHubDark.TextMain; break;
                default:
                    baseBg = GitHubDark.PanelSub; hoverBg = GitHubDark.Stroke; textColor = GitHubDark.TextMain; break;
            }

            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = baseBg;
            btn.style.color = textColor;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(baseBg);
            btn.style.borderTopLeftRadius = 6f;
            btn.style.borderTopRightRadius = 6f;
            btn.style.borderBottomLeftRadius = 6f;
            btn.style.borderBottomRightRadius = 6f;

            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverBg);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = baseBg);
        }

        /// <summary>창 크롬(본체/타이틀바/닫기버튼) GitHub-dark 리스타일 — 생성 시 1회.</summary>
        private void ApplyGitHubDarkStyle()
        {
            // [Figma 93:230 글래스] #161B22@0.85(사용자 조정) + #30363D@0.5 1.2px + r14.4
            style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = UTKTheme.GlassStrokeWidth;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new StyleColor(UTKTheme.GlassPanelStroke);
            style.borderTopLeftRadius = UTKTheme.GlassRadius;
            style.borderTopRightRadius = UTKTheme.GlassRadius;
            style.borderBottomLeftRadius = UTKTheme.GlassRadius;
            style.borderBottomRightRadius = UTKTheme.GlassRadius;
            style.color = GitHubDark.TextMain;   // 명시색 없는 라벨 상속색 — 기본 텍스트

            var titleBar = this.Q("TitleBar");
            if (titleBar != null)
            {
                titleBar.style.backgroundColor = GitHubDark.PanelSub;
                titleBar.style.borderTopLeftRadius = 8f;
                titleBar.style.borderTopRightRadius = 8f;
                titleBar.style.borderBottomWidth = 1f;
                titleBar.style.borderBottomColor = GitHubDark.Stroke;
            }

            if (_titleLabel != null)
                _titleLabel.style.color = GitHubDark.TextMain;

            var closeBtn = this.Q<Button>("CloseButton");
            if (closeBtn != null)
            {
                closeBtn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                closeBtn.style.backgroundColor = GitHubDark.PanelSub;
                closeBtn.style.borderTopWidth = closeBtn.style.borderBottomWidth = closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth = 0f;
                closeBtn.style.borderTopColor = closeBtn.style.borderBottomColor = closeBtn.style.borderLeftColor = closeBtn.style.borderRightColor = new StyleColor(GitHubDark.PanelSub);
                closeBtn.style.borderTopLeftRadius = 4f;
                closeBtn.style.borderTopRightRadius = 4f;
                closeBtn.style.borderBottomLeftRadius = 4f;
                closeBtn.style.borderBottomRightRadius = 4f;            // 작은배지 r4
                closeBtn.style.color = GitHubDark.TextMain;
            }
        }

        /// <summary>GitHub-dark 리스트 행(퀘스트 항목 박스) — 보조 패널 #21262D + 1px 스트로크 + r6 (이 창 한정).</summary>
        private static void ApplyDarkRowStyle(VisualElement row)
        {
            if (row == null) return;
            row.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            row.style.backgroundColor = GitHubDark.PanelSub;
            row.style.borderTopWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
            row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            row.style.borderTopLeftRadius = 6f;
            row.style.borderTopRightRadius = 6f;
            row.style.borderBottomLeftRadius = 6f;
            row.style.borderBottomRightRadius = 6f;   // 서브 반경 r6
        }

        // ===== 레퍼런스 =====
        private readonly VisualElement _tabBar;
        private Button _btnActive, _btnCompleted;
        private Label _statLabel;
        private readonly ScrollView _list;
        private int _activeTab = TabActive;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;
        private VisualElement _canvasLayoutRoot;   // [Figma 정합 v3] UIRoot 훅 — 등배수 디자인공간 스케일 재적용용

        private QuestJournalUTK() : base("📜 퀘스트 저널", new Vector2(WinW, WinH), UTKWindowChrome.Frameless)
        {
            // [Figma 93:230 QuestListPanel] 프레임리스 단일 패널 — pad 24 / 세로 gap 19.2 / 모서리 L 데칼 /
            // PanelHeader(퀘스트 창+QUEST DECK+닫기 31.2) / FilterTabs(진행 중·완료 36.2) / 카드 432×135.2 / 요약 푸터.
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = _content.style.paddingRight = 24f;
            _content.style.paddingTop = _content.style.paddingBottom = 24f;
            AddCornerDecals(_content);   // [Figma 정합 v3] 데칼도 디자인공간(_content) 내부에 두어 창 스케일 k와 함께 등비 축소

            // ── PanelHeader 432×52.8 ──
            var panelHeader = new VisualElement { name = "PanelHeader" };
            panelHeader.style.flexDirection = FlexDirection.Row;
            panelHeader.style.alignItems = Align.Center;
            panelHeader.style.height = 52.8f;
            panelHeader.style.minHeight = 52.8f;
            panelHeader.style.flexShrink = 0f;
            var jTitle = MkLabel("퀘스트 창", UTKTheme.FontTitle, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [Figma] 21.6/700
            jTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            panelHeader.Add(jTitle);
            var headerSub = MkLabel("QUEST DECK", 12, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            headerSub.style.marginLeft = 12f;
            headerSub.style.marginTop = 5f;
            headerSub.style.flexGrow = 1f;
            panelHeader.Add(headerSub);
            var headerClose = new Button(Hide) { text = "×", name = "JournalHeaderCloseButton" };
            headerClose.style.width = 31.2f;
            headerClose.style.height = 31.2f;
            headerClose.style.flexShrink = 0f;
            StyleButton(headerClose, UTKButton.Variant.Secondary);
            panelHeader.Add(headerClose);
            _content.Add(panelHeader);
            SetDragHandle(panelHeader);

            // ── FilterTabs 432×36.2, gap 4.8 ──
            _tabBar = new VisualElement { name = "FilterTabs" };
            _tabBar.style.flexDirection = FlexDirection.Row;
            _tabBar.style.height = 36.2f;
            _tabBar.style.minHeight = 36.2f;
            _tabBar.style.marginTop = 19.2f;
            _tabBar.style.marginBottom = 19.2f;
            _tabBar.style.flexShrink = 0f;
            _content.Add(_tabBar);

            _btnActive = UTKButton.Create("진행 중", () => SwitchTab(TabActive), UTKButton.Variant.Secondary);
            _btnCompleted = UTKButton.Create("완료", () => SwitchTab(TabCompleted), UTKButton.Variant.Secondary);
            _btnActive.style.height = 36.2f;
            _btnActive.style.flexGrow = 1f;
            _btnActive.style.marginRight = 4.8f;
            _btnCompleted.style.height = 36.2f;
            _btnCompleted.style.flexGrow = 1f;
            _btnActive.style.fontSize = UTKTheme.FontTab;       // [Figma] 14.4/700
            _btnActive.style.unityFontStyleAndWeight = FontStyle.Bold;
            _btnCompleted.style.fontSize = UTKTheme.FontTab;
            _btnCompleted.style.unityFontStyleAndWeight = FontStyle.Bold;
            _tabBar.Add(_btnActive);
            _tabBar.Add(_btnCompleted);

            // ── QuestSlotsContainer (카드 목록) ──
            _list = new ScrollView { name = "QuestSlotsContainer" };
            _list.style.flexGrow = 1f;
            _list.style.flexShrink = 1f;
            _content.Add(_list);

            // ── SummaryFooter 432×34.4: 좌 라벨 + 우 실측값 ──
            var footer = new VisualElement { name = "SummaryFooter" };
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.height = 34.4f;
            footer.style.minHeight = 34.4f;
            footer.style.flexShrink = 0f;
            var footerLeft = MkLabel("퀘스트 현황", 12, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            footerLeft.style.flexGrow = 1f;
            footer.Add(footerLeft);
            _statLabel = MkLabel("진행 0개 · 완료 0개", 13.2f, GitHubDark.TextMain, TextAnchor.MiddleRight);
            footer.Add(_statLabel);
            _content.Add(footer);

            ApplyUIToolkitFont(this);
            ApplyGitHubDarkStyle();   // [GitHub-dark] 창 크롬(패널 배경+1px 스트로크+r8) — 이 창 한정 인라인
            style.display = DisplayStyle.None;
            style.left = 144f;   // [Figma 93:230] QuestListPanel @(144,48)
            style.top = 48f;
        }

        /// <summary>[Figma 93:230] 패널 4모서리 L자 데칼(12선) — 시각 전용.</summary>
        private static void AddCornerDecals(VisualElement host)
        {
            void Line(float left, float top, float lw, float lh)
            {
                var l = new VisualElement();
                l.style.position = Position.Absolute;
                l.style.left = left;
                l.style.top = top;
                l.style.width = lw;
                l.style.height = lh;
                l.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
                l.pickingMode = PickingMode.Ignore;
                host.Add(l);
            }
            Line(0f, 0f, 12f, 1f); Line(0f, 0f, 1f, 12f);
            Line(WinW - 12f, 0f, 12f, 1f); Line(WinW - 1f, 0f, 1f, 12f);
            Line(0f, WinH - 1f, 12f, 1f); Line(0f, WinH - 12f, 1f, 12f);
            Line(WinW - 12f, WinH - 1f, 12f, 1f); Line(WinW - 1f, WinH - 12f, 1f, 12f);
        }

        private void SwitchTab(int tab)
        {
            _activeTab = tab;
            StyleTab(_btnActive, _activeTab == TabActive);
            StyleTab(_btnCompleted, _activeTab == TabCompleted);
            RefreshDisplay();
            Debug.Log($"[JournalUTK] 탭 전환 → {(tab == TabActive ? "진행 중" : "완료")}");
        }

        private static void StyleTab(Button btn, bool active)
        {
            if (btn == null) return;
            // [Figma TabActive] 활성=액센트 배경 + 최배경 텍스트 / 비활성=보조 패널 + 기본 텍스트
            btn.style.backgroundColor = new StyleColor(active ? GitHubDark.Accent : GitHubDark.PanelSub);
            btn.style.color = new StyleColor(active ? GitHubDark.Panel : GitHubDark.TextMain);
        }

        // =====================================================================
        //  생명주기
        // =====================================================================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            if (_canvasLayoutRoot != root)
            {
                if (_canvasLayoutRoot != null)
                    _canvasLayoutRoot.UnregisterCallback<GeometryChangedEvent>(OnCanvasRootGeometryChanged);
                _canvasLayoutRoot = root;
                if (_canvasLayoutRoot != null)
                    _canvasLayoutRoot.RegisterCallback<GeometryChangedEvent>(OnCanvasRootGeometryChanged);
            }
            ApplyJournalLayout();
            StyleTab(_btnActive, _activeTab == TabActive);
            StyleTab(_btnCompleted, _activeTab == TabCompleted);
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[JournalUTK] 저널 열림 (키: J)");
        }

        // =====================================================================
        //  [Figma 정합 v3] 등배수 디자인공간 스케일 — 창 박스=JournalBounds×k, 내부 raw px+scale=k
        // =====================================================================

        private void OnCanvasRootGeometryChanged(GeometryChangedEvent evt) => ApplyJournalLayout();

        private void ApplyJournalLayout()
        {
            if (_canvasLayoutRoot == null) return;
            FigmaCanvasLayout.ApplyDesignSpace(this, _content, JournalBounds, _canvasLayoutRoot);
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[JournalUTK] 저널 닫힘");
        }

        // =====================================================================
        //  폴링 루프
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshDisplay();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        // =====================================================================
        //  데이터 갱신 — QuestManager 실데이터 경로
        // =====================================================================

        private void RefreshDisplay()
        {
            List<QuestData> active = QuestManager.GetActiveQuests();
            List<QuestData> completed = QuestManager.GetCompletedQuests();

            if (_statLabel != null)
                _statLabel.text = $"진행 {active.Count}개 · 완료 {completed.Count}개";
            Debug.Log($"[JournalUTK] 목록 갱신(탭={(_activeTab == TabActive ? "진행" : "완료")}): 진행 {active.Count} / 완료 {completed.Count}");

            List<QuestData> filtered = _activeTab == TabActive ? active : completed;
            _list.Clear();

            if (filtered.Count == 0)
            {
                string emptyMsg = _activeTab == TabActive
                    ? "진행 중인 퀘스트가 없습니다.\nNPC를 찾아 퀘스트를 수락하세요."
                    : "완료된 퀘스트가 없습니다.\n퀘스트를 완료하면 여기에 표시됩니다.";
                var empty = MkLabel(emptyMsg, 14.4f, GitHubDark.TextSub, TextAnchor.UpperLeft);   // [GitHub-dark] 보조 텍스트
                empty.style.flexGrow = 1f;
                empty.style.whiteSpace = WhiteSpace.Normal;
                _list.Add(empty);
                return;
            }

            for (int i = 0; i < filtered.Count; i++)
                _list.Add(BuildQuestEntry(filtered[i]));
        }

        private VisualElement BuildQuestEntry(QuestData quest)
        {
            // [Figma 93:230 QuestSlot 432×135.2] pad 14.4 / 내부 gap 14.4 — TierStrip 좌측 세로(메인=Gold/서브=Accent — 기존 색 계약),
            // CardHeader(CategoryPill+Lv) / CardBody(제목+목표 요약 1줄) / ProgressArea(GaugeTrack 4.8 + %).
            // 목표 상세는 Q키 퀘스트 창(QuestWindowUTK) 상세 패널이 소유 — 저널 카드는 요약만.
            var card = new VisualElement { name = "QuestSlot" };
            card.style.height = 135.2f;
            card.style.minHeight = 135.2f;
            card.style.flexShrink = 0f;
            card.style.marginBottom = 9.6f;
            card.style.paddingLeft = 19.2f;   // TierStrip 4.8 + 카드 pad 14.4
            card.style.paddingRight = 14.4f;
            card.style.paddingTop = 14.4f;
            card.style.paddingBottom = 14.4f;
            ApplyDarkRowStyle(card);
            card.style.borderTopLeftRadius = 8f;
            card.style.borderTopRightRadius = 8f;
            card.style.borderBottomLeftRadius = 8f;
            card.style.borderBottomRightRadius = 8f;

            var strip = new VisualElement { name = "TierStrip" };
            strip.style.position = Position.Absolute;
            strip.style.left = 0f;
            strip.style.top = 0f;
            strip.style.width = 4.8f;
            strip.style.height = 135.2f;
            strip.style.backgroundColor = new StyleColor(quest.isMain ? GitHubDark.Gold : GitHubDark.Accent);
            strip.pickingMode = PickingMode.Ignore;
            card.Add(strip);

            var header = new VisualElement { name = "CardHeader" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = 18.8f;
            header.style.minHeight = 18.8f;
            header.style.marginBottom = 14.4f;
            var pill = new Label(quest.isMain ? "주 임무" : "부 임무") { name = "CategoryPill" };
            pill.style.fontSize = UTKTheme.FontBadge;   // [Figma] 12/700
            pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.style.color = new StyleColor(quest.isMain ? GitHubDark.Gold : GitHubDark.Accent);
            pill.style.backgroundColor = new StyleColor(GitHubDark.BgBase);
            pill.style.paddingLeft = pill.style.paddingRight = 7.2f;
            pill.style.paddingTop = pill.style.paddingBottom = 2.4f;
            pill.style.borderTopWidth = pill.style.borderBottomWidth = pill.style.borderLeftWidth = pill.style.borderRightWidth = 1f;
            pill.style.borderTopColor = pill.style.borderBottomColor = pill.style.borderLeftColor = pill.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius = pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 4f;
            header.Add(pill);
            var headerSpacer = new VisualElement();
            headerSpacer.style.flexGrow = 1f;
            header.Add(headerSpacer);
            var jLv = MkLabel(quest.requiredLevel > 0 ? $"Lv.{quest.requiredLevel}" : "", UTKTheme.FontTab, GitHubDark.Accent, TextAnchor.MiddleRight);   // [Figma] 14.4/700
            jLv.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(jLv);
            card.Add(header);

            var body = new VisualElement { name = "CardBody" };
            body.style.flexDirection = FlexDirection.Column;
            var nameL = MkLabel(quest.questName, UTKTheme.FontCardName, quest.isMain ? GitHubDark.Gold : GitHubDark.Accent, TextAnchor.MiddleLeft);   // [Figma] 16.8/700 + 메인/서브 색 계약
            nameL.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameL.style.height = 20f;
            nameL.style.minHeight = 20f;
            body.Add(nameL);
            var objL = MkLabel(BuildObjectiveSummary(quest), UTKTheme.FontBody, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [Figma] 14.4/400
            objL.style.marginTop = 4.8f;
            objL.style.whiteSpace = WhiteSpace.Normal;
            body.Add(objL);
            card.Add(body);

            float pct;
            Color fillColor;
            if (_activeTab == TabCompleted)
            {
                pct = 100f;
                fillColor = GitHubDark.Success;
            }
            else
            {
                int cur = 0, req = 0;
                if (quest.objectives != null)
                    for (int i = 0; i < quest.objectives.Count; i++)
                    {
                        cur += Mathf.Max(0, quest.objectives[i].currentCount);
                        req += Mathf.Max(0, quest.objectives[i].requiredCount);
                    }
                pct = req > 0 ? Mathf.Clamp01((float)cur / req) * 100f : 0f;
                fillColor = GitHubDark.Accent;
            }

            var progress = new VisualElement { name = "ProgressArea" };
            progress.style.flexDirection = FlexDirection.Row;
            progress.style.alignItems = Align.Center;
            progress.style.height = 17f;
            progress.style.marginTop = 14.4f;
            var track = new VisualElement { name = "GaugeTrack" };
            track.style.flexGrow = 1f;
            track.style.height = 4.8f;
            track.style.backgroundColor = new StyleColor(GitHubDark.BgBase);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 2.4f;
            var fill = new VisualElement { name = "GaugeFill" };
            fill.style.height = new Length(100f, LengthUnit.Percent);
            fill.style.width = new Length(pct, LengthUnit.Percent);
            fill.style.backgroundColor = new StyleColor(fillColor);
            fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius = fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius = 2.4f;
            track.Add(fill);
            progress.Add(track);
            var pctLabel = MkLabel($"{pct:F0}%", 13.2f, GitHubDark.TextMain, TextAnchor.MiddleRight);
            pctLabel.style.marginLeft = 9.6f;
            pctLabel.style.width = 40f;
            progress.Add(pctLabel);
            card.Add(progress);

            Debug.Log($"[JournalUTK] 항목 렌더: {quest.questName} ({quest.questId})");
            return card;
        }

        /// <summary>목표 요약 1줄 — 첫 목표 cur/req + 외 N개 (Figma 카드 1줄 구조, 데이터 손실 없이 집계).</summary>
        private static string BuildObjectiveSummary(QuestData quest)
        {
            if (quest.objectives == null || quest.objectives.Count == 0)
                return !string.IsNullOrEmpty(quest.description) ? quest.description : "";
            var first = quest.objectives[0];
            string d = !string.IsNullOrEmpty(first.description) ? first.description : first.type.ToString();
            string s = $"{d} {first.currentCount}/{first.requiredCount}";
            if (quest.objectives.Count > 1) s += $" 외 {quest.objectives.Count - 1}개";
            return s;
        }

        // =====================================================================
        //  키 토글 / ESC용 Updater
        // =====================================================================

        private class Updater : MonoBehaviour
        {
            public QuestJournalUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.jKey.wasPressedThisFrame && window != null) if (window.IsOpen) window.Close(); else Toggle();;
                    if (kb.escapeKey.wasPressedThisFrame && window != null && window.IsOpen) window.Close();
                }
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.StopRefreshLoop();
                    window.RemoveFromHierarchy();
                }
            }
        }

        // =====================================================================
        //  내부 헬퍼
        // =====================================================================

        private static Label MkLabel(string text, float size, Color color, TextAnchor align)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            l.style.unityTextAlign = align;
            return l;
        }
    }
}