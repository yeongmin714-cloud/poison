// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;          // QuestManager (static), PlayerStats
using ProjectName.Core.Data;     // QuestData, QuestState, QuestObjective, QuestChainData
using ProjectName.Systems;       // QuestChainManager, QuestChainManager.ChainProgress

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round A-3 → [P4 Figma quest-window-ui 3열 재구성] — 퀘스트 윈도우.
    /// 원본: Assets/Scripts/UI/QuestWindow.cs (304줄, IMGUI) — 본 파일은 그것의 데이터 경로만
    /// UTKWindowBase 파생으로 이식한 별도 파일. 원본은 절대 수정하지 않는다.
    ///
    /// [P4 Figma 3열 구조 — 게임 로직 보존, 표시 구조만 Figma 재배열]
    ///  - 좌: QuestListPanel — PanelHeader + FilterTabs(진행 중/완료) + 카드 스크롤
    ///       (TierStrip 상태색 + CategoryPill + Lv.N + 제목 + 설명 + 진행게이지%) + SummaryFooter
    ///  - 중앙: QuestDetailPanel — 선택 퀘스트 HeroHeader(명+등급태그+추가) + StorySection(브리핑) + ObjectivesSection(체크리스트)
    ///  - 우: RewardPanel — 보상 목록(골드/경험/아이템 카드/호감도) + 없으면 "보상 없음"
    ///
    /// [데이터 경로 — 원본 실측, 보존]
    ///  - 진행/완료 목록: QuestManager.GetActiveQuests() / GetCompletedQuests()
    ///  - 수락 목록:      QuestManager.GetAvailableQuests(PlayerStats.Instance.Level)
    ///  - 수락 버튼:       QuestManager.AcceptQuest(questId)
    ///  - 완료 버튼:       QuestManager.TryCompleteQuest(questId) — quest.AllObjectivesMet 일 때만 활성
    ///  - 활성 체인:       QuestChainManager.Instance.GetAllChainIds() → GetChainProgress(isActive) → GetChainData
    ///  - 체인 진행 버튼:  QuestChainManager.Instance.CompleteCurrentNode(chainId)
    ///  - 보상 요약:       QuestRewardPreview.GetRewardSummary(QuestData)
    ///
    /// 데이터 갱신 폴링 400ms(schedule.Execute().Every) + ESC 닫기. 열기는 public Open/Toggle API 사용.
    /// </summary>
    public class QuestWindowUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static QuestWindowUTK _instance;
        public static QuestWindowUTK Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            _instance = new QuestWindowUTK();
            var go = new GameObject("QuestWindowUTK");
            Object.DontDestroyOnLoad(go);
            var updater = go.AddComponent<Updater>();
            updater.window = _instance;
            Debug.Log("[QuestWindowUTK] 초기화 완료");
        }

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance == null)
            {
                _instance = new QuestWindowUTK();
                var go = new GameObject("QuestWindowUTK_UPD");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Updater>().window = _instance;
            }
        }

        /// <summary>열기 (팩토리 겸용).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 =====
        // Saved Figma frame 93:230 is the 1920x1080 canvas. Panel coordinates below are
        // local to its origin (32082,3); this remains a full-screen transparent overlay host.
        public static readonly Rect QuestListBounds = new Rect(144f, 48f, 480f, 984f);
        public static readonly Rect QuestDetailBounds = new Rect(648f, 252f, 624f, 576f);
        public static readonly Rect RewardBounds = new Rect(1296f, 48f, 480f, 984f);
        /// <summary>[Figma 정합 v3] 풀캔버스 호스트 디자인공간 — 창 박스=CanvasBounds×k(등배수), 내부 raw px.</summary>
        public static readonly Rect CanvasBounds = new Rect(0f, 0f, 1920f, 1080f);
        private const float WinW = 1920f;
        private const float WinH = 1080f;
        private const long RefreshMs = 400L;

        // ===== [P4] 필터 =====
        // Archived frame 93:230 has exactly two tabs. Available quests and active chains remain
        // accessible under the in-progress tab so their existing accept/progress actions stay reachable.
        private enum QuestFilter { Active, Completed }
        private QuestFilter _filter = QuestFilter.Active;

        // ===== [P4] 선택된 퀘스트 =====
        private string _selectedQuestId;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 퀘스트 창 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKButton·UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color BgBase   = Hex(0x0B0E14);   // 최배경 — 게이지 트랙
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(행/버튼)
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트) — 진행/목표
            public static readonly Color Gold     = Hex(0xE3B341);   // 희귀/보상/수락가능/골드
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
            style.backgroundColor = GitHubDark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;   // 메인 반경 r8
            style.color = GitHubDark.TextMain;

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

        /// <summary>GitHub-dark 리스트 행(퀘스트/체인 박스) — 보조 패널 #21262D + 1px 스트로크 + r6 (이 창 한정).</summary>
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

        // ===== [P4] 3열 슬롯 래퍼 (보조패널 인셋) =====
        private static void ApplyDarkSlotStyle(VisualElement slot)
        {
            if (slot == null) return;
            // [Figma 93:230 패널 글래스] #161B22@0.85(사용자 조정) + #30363D@0.5 1.2px + r14.4
            slot.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            slot.style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);
            slot.style.borderTopWidth = slot.style.borderBottomWidth = slot.style.borderLeftWidth = slot.style.borderRightWidth = UTKTheme.GlassStrokeWidth;
            slot.style.borderTopColor = slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor = new StyleColor(UTKTheme.GlassPanelStroke);
            slot.style.borderTopLeftRadius = UTKTheme.GlassRadius;
            slot.style.borderTopRightRadius = UTKTheme.GlassRadius;
            slot.style.borderBottomLeftRadius = UTKTheme.GlassRadius;
            slot.style.borderBottomRightRadius = UTKTheme.GlassRadius;
        }

        // ===== 레퍼런스 =====
        private ScrollView _list;   // [Phase2a] readonly 제거 — BuildListZone()에서 초기화(독립 창 분리 기반)
        private Label _statActive, _statCompleted, _statAvailable, _statChain, _summaryFooter;
        private VisualElement _listPanel, _detailPanel, _rewardPanel;
        private VisualElement _canvasLayoutRoot;
        private Label _detailHeroTitle, _detailHeroTag, _detailStory;
        private VisualElement _objectivesList;
        private VisualElement _rewardsList;
        private readonly List<Button> _filterTabs = new List<Button>();
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private QuestWindowUTK() : base("📋 퀘스트", new Vector2(WinW, WinH), UTKWindowChrome.Frameless)
        {
            // [Frameless] 배경 투명 — 게임 화면이 비치고, 아래 3개 판넬(목록/상세/보상)만 떠 보임.
            _content.style.flexGrow = 1f;
            // _content hosts only the three positioned panel roots; its parent window stays
            // full-screen and transparent so the game remains visible around the overlays.
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            _content.style.position = Position.Relative;
            _content.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            // [Phase2a] 3영역 빌더를 메서드로 추출 (동작 0 변화 — 독립 창 승격의 기반)
            BuildListZone();
            BuildDetailZone();
            BuildRewardZone();

            ApplyUIToolkitFont(this);
            if (!IsFrameless)
                ApplyGitHubDarkStyle();   // [Frameless] 배경 투명 유지 — 타이틀바 없음, 크롬 리스타일 스킵
            style.display = DisplayStyle.None;
            style.left = 0f;
            style.top = 0f;
            // [Figma 정합 v3] 패널은 Build*Zone의 raw Figma 좌표 그대로 — 등배수 스케일은 Attach 시
            // ApplyDesignSpace가 _content에 일괄 적용(패널별 X/Y 독립 재스케일 경로는 제거됨).
            RegisterCallback<AttachToPanelEvent>(OnCanvasAttached);
            RegisterCallback<DetachFromPanelEvent>(OnCanvasDetached);
        }

        private void OnCanvasAttached(AttachToPanelEvent evt)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            if (_canvasLayoutRoot != root)
            {
                if (_canvasLayoutRoot != null)
                    _canvasLayoutRoot.UnregisterCallback<GeometryChangedEvent>(OnCanvasGeometryChanged);
                _canvasLayoutRoot = root;
                _canvasLayoutRoot.RegisterCallback<GeometryChangedEvent>(OnCanvasGeometryChanged);
            }
            ApplyCanvasScale();
        }

        private void OnCanvasDetached(DetachFromPanelEvent evt)
        {
            if (_canvasLayoutRoot == null) return;
            _canvasLayoutRoot.UnregisterCallback<GeometryChangedEvent>(OnCanvasGeometryChanged);
            _canvasLayoutRoot = null;
        }

        private void OnCanvasGeometryChanged(GeometryChangedEvent evt)
        {
            if (evt.target == _canvasLayoutRoot) ApplyCanvasScale();
        }

        private void ApplyCanvasScale()
        {
            if (_canvasLayoutRoot == null) return;
            // [Figma 정합 v3] 등배수 디자인공간 — 창 박스=CanvasBounds×k, _content raw 1920×1080 + scale=k.
            // 기존 패널별 X/Y 독립 scale(비-16:9 세로 신장 원인)은 제거하고 조상 스케일에 일임한다.
            FigmaCanvasLayout.ApplyDesignSpace(this, _content, CanvasBounds, _canvasLayoutRoot);
        }

        // [Phase2a] 좌: QuestListPanel (필터탭 + 카드 목록 + 요약풋터)
        private void BuildListZone()
        {
            var listCol = new VisualElement();
            _listPanel = listCol;
            listCol.name = "QuestListPanel";
            listCol.style.flexDirection = FlexDirection.Column;
            listCol.style.width = QuestListBounds.width;
            listCol.style.height = QuestListBounds.height;
            listCol.style.position = Position.Absolute;
            listCol.style.left = QuestListBounds.x;
            listCol.style.top = QuestListBounds.y;
            listCol.style.flexShrink = 0;
            listCol.style.paddingLeft = 24f;
            listCol.style.paddingRight = 24f;
            listCol.style.paddingTop = 24f;
            listCol.style.paddingBottom = 24f;
            ApplyDarkSlotStyle(listCol);     // 판넬 배경/테두리(독립 창처럼)
            _content.Add(listCol);

            listCol.Add(BuildPanelBar("퀘스트 목록", "QUEST DECK", GitHubDark.Gold));

            // 필터 탭 [진행 중][완료]
            var tabRow = new VisualElement();
            tabRow.name = "FilterTabs";
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.height = 36.2f;
            tabRow.style.flexShrink = 0f;
            tabRow.style.marginTop = 19.2f;
            listCol.Add(tabRow);
            AddFilterTab(tabRow, QuestFilter.Active, "진행 중");
            AddFilterTab(tabRow, QuestFilter.Completed, "완료");

            _list = new ScrollView { name = "QuestList" };
            _list.style.flexGrow = 1f;
            _list.style.minHeight = 0f;
            _list.style.marginTop = 19.2f;
            listCol.Add(_list);

            // 요약 풋터
            var footerRow = new VisualElement { name = "SummaryFooter" };
            footerRow.style.flexDirection = FlexDirection.Row;
            footerRow.style.alignItems = Align.Center;
            footerRow.style.height = 34.4f;
            footerRow.style.flexShrink = 0f;
            footerRow.style.marginTop = 18f;
            _summaryFooter = MkLabel("동시 추적 제한 2 / 5 개 등록", UTKTheme.FontRowLabel, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [Figma] 15.6
            _summaryFooter.style.flexGrow = 1f;
            footerRow.Add(_summaryFooter);
            listCol.Add(footerRow);
        }

        // [Phase2a] 중앙: QuestDetailPanel — 선택 퀘스트 상세(브리핑+목표)
        private void BuildDetailZone()
        {
            _detailPanel = new VisualElement();
            _detailPanel.name = "QuestDetailPanel";
            _detailPanel.style.flexDirection = FlexDirection.Column;
            _detailPanel.style.width = QuestDetailBounds.width;
            _detailPanel.style.height = QuestDetailBounds.height;
            _detailPanel.style.position = Position.Absolute;
            _detailPanel.style.left = QuestDetailBounds.x;
            _detailPanel.style.top = QuestDetailBounds.y;
            _detailPanel.style.paddingLeft = 24f;
            _detailPanel.style.paddingRight = 24f;
            _detailPanel.style.paddingTop = 24f;
            _detailPanel.style.paddingBottom = 24f;
            ApplyDarkSlotStyle(_detailPanel);
            _content.Add(_detailPanel);

            _detailPanel.Add(BuildPanelBar("임무 상세", "OBJECTIVE", GitHubDark.Accent));

            var hero = new VisualElement { name = "QuestHeroHeader" };
            hero.style.flexDirection = FlexDirection.Column;
            hero.style.height = 82.6f;
            hero.style.flexShrink = 0f;
            hero.style.marginTop = 19.2f;
            hero.style.paddingLeft = 14.4f;
            hero.style.paddingRight = 14.4f;
            hero.style.paddingTop = 14.4f;
            _detailHeroTitle = MkLabel("퀘스트를 선택하세요", UTKTheme.FontDisplayName, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [Figma] 26.4/700
            _detailHeroTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailHeroTitle.style.height = 31f;
            hero.Add(_detailHeroTitle);
            _detailHeroTag = MkLabel("", 12, GitHubDark.Accent, TextAnchor.MiddleLeft);
            _detailHeroTag.style.marginTop = 4.8f;
            _detailHeroTag.style.height = 18f;
            hero.Add(_detailHeroTag);
            _detailPanel.Add(hero);

            var storySection = new VisualElement { name = "StorySection" };
            storySection.style.flexDirection = FlexDirection.Column;
            storySection.style.height = 141f;
            storySection.style.flexShrink = 0f;
            storySection.style.marginTop = 19.2f;
            storySection.style.paddingLeft = 14.4f;
            storySection.style.paddingRight = 14.4f;
            storySection.style.paddingTop = 14.4f;
            var storyH = MkLabel("작전 브리핑", 14, GitHubDark.Gold, TextAnchor.MiddleLeft);
            storyH.style.height = 18f;
            storySection.Add(storyH);
            _detailStory = MkLabel("—", 13, GitHubDark.TextSub, TextAnchor.UpperLeft);
            _detailStory.style.whiteSpace = WhiteSpace.Normal;
            _detailStory.style.height = 87f;
            _detailStory.style.marginTop = 7.2f;
            storySection.Add(_detailStory);
            _detailPanel.Add(storySection);

            var objectivesSection = new VisualElement { name = "ObjectivesPanel" };
            objectivesSection.style.flexDirection = FlexDirection.Column;
            objectivesSection.style.flexGrow = 1f;
            objectivesSection.style.minHeight = 0f;
            objectivesSection.style.marginTop = 19.2f;
            var objH = MkLabel("달성 조건", 14, GitHubDark.Gold, TextAnchor.MiddleLeft);
            objH.style.height = 18f;
            objectivesSection.Add(objH);

            _objectivesList = new VisualElement();
            _objectivesList.name = "ObjectivesSection";
            _objectivesList.style.flexDirection = FlexDirection.Column;
            _objectivesList.style.flexGrow = 1f;
            _objectivesList.style.minHeight = 0f;
            _objectivesList.style.marginTop = 12f;
            objectivesSection.Add(_objectivesList);
            _detailPanel.Add(objectivesSection);
        }

        // [Phase2a] 우: RewardPanel — 보상 목록
        private void BuildRewardZone()
        {
            _rewardPanel = new VisualElement();
            _rewardPanel.name = "RewardPanel";
            _rewardPanel.style.flexDirection = FlexDirection.Column;
            _rewardPanel.style.width = RewardBounds.width;
            _rewardPanel.style.height = RewardBounds.height;
            _rewardPanel.style.position = Position.Absolute;
            _rewardPanel.style.left = RewardBounds.x;
            _rewardPanel.style.top = RewardBounds.y;
            _rewardPanel.style.flexShrink = 0;
            _rewardPanel.style.paddingLeft = 24f;
            _rewardPanel.style.paddingRight = 24f;
            _rewardPanel.style.paddingTop = 24f;
            _rewardPanel.style.paddingBottom = 24f;
            ApplyDarkSlotStyle(_rewardPanel);
            _content.Add(_rewardPanel);

            _rewardPanel.Add(BuildPanelBar("보상", "REWARD", GitHubDark.Gold));

            _rewardsList = new VisualElement();
            _rewardsList.name = "RewardsList";
            _rewardsList.style.flexDirection = FlexDirection.Column;
            _rewardsList.style.flexGrow = 1f;
            _rewardsList.style.minHeight = 0f;
            // The archive reserves a section-description block here, but no corresponding
            // runtime data exists; preserve the spacing without inserting placeholder copy.
            _rewardsList.style.marginTop = 87.2f;
            _rewardPanel.Add(_rewardsList);
        }

        private void AddFilterTab(VisualElement parent, QuestFilter filter, string label)
        {
            var tab = UTKButton.Create(label, () =>
            {
                _filter = filter;
                RefreshDisplay();
            }, UTKButton.Variant.Secondary);
            tab.style.flexGrow = 1f;
            tab.style.height = 36.2f;
            tab.style.minWidth = 0f;
            tab.style.marginRight = filter == QuestFilter.Completed ? 0f : 8.4f;
            StyleButton(tab, UTKButton.Variant.Secondary);
            _filterTabs.Add(tab);
            parent.Add(tab);
        }

        // [Phase2b] 각 판넬을 독립 창처럼 — 상단 타이틀바(제목 + ✕닫기). 닫기는 관리자 Close(3창 함께).
        private VisualElement BuildPanelBar(string title, string enTitle, Color accent)
        {
            var bar = new VisualElement();
            bar.name = "PanelTitleBar";
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.height = 52.8f;
            bar.style.flexShrink = 0f;
            bar.style.paddingLeft = 0f;
            bar.style.paddingRight = 0f;
            // [Figma 93:230 PanelHeader] 배경/테두리 없음 — 제목 21.6/700(accent) + 영문 13.2/500 + 닫기 31.2
            bar.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            bar.style.borderTopWidth = bar.style.borderBottomWidth = bar.style.borderLeftWidth = bar.style.borderRightWidth = 0f;
            var titleLbl = MkLabel(title, UTKTheme.FontTitle, accent, TextAnchor.MiddleLeft);
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.flexGrow = 1f;
            titleLbl.style.minWidth = 0f;
            bar.Add(titleLbl);
            var enLbl = MkLabel(enTitle, UTKTheme.FontSubtitle, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            enLbl.style.unityFontStyleAndWeight = FontStyle.Normal;
            enLbl.style.marginLeft = 12f;
            enLbl.style.marginTop = 5f;
            enLbl.style.flexShrink = 0f;
            bar.Add(enLbl);
            var closeBtn = UTKButton.Create("✕", () => Close(), UTKButton.Variant.Secondary);
            closeBtn.style.flexShrink = 0f;
            closeBtn.style.width = 31.2f;
            closeBtn.style.height = 31.2f;
            StyleButton(closeBtn, UTKButton.Variant.Secondary);
            bar.Add(closeBtn);
            return bar;
        }

        private static Label MkStatLabel(string text)
        {
            var l = new Label(text ?? "");
            l.style.width = 185f;
            l.style.fontSize = 14f;
            l.style.color = new StyleColor(GitHubDark.TextMain);
            return l;
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
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[QuestWindowUTK] 퀘스트 창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[QuestWindowUTK] 퀘스트 창 닫힘");
        }

        // =====================================================================
        //  폴링 루프 (schedule.Execute().Every, Pause 정지)
        //  =====================================================================

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
        //  데이터 갱신 — (원본 실측 경로, 보존)
        //  =====================================================================

        private void RefreshDisplay()
        {
            RecolorTabs();

            List<QuestData> active = QuestManager.GetActiveQuests();
            List<QuestData> completed = QuestManager.GetCompletedQuests();
            List<QuestData> available = new List<QuestData>();
            if (PlayerStats.Instance != null)
                available = QuestManager.GetAvailableQuests(PlayerStats.Instance.Level);

            // ── 활성 체인 ──
            List<KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>> chains =
                new List<KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>>();
            var mgr = QuestChainManager.Instance;
            if (mgr != null)
            {
                List<string> ids = new List<string>(mgr.GetAllChainIds());
                for (int i = 0; i < ids.Count; i++)
                {
                    var progress = mgr.GetChainProgress(ids[i]);
                    if (progress != null && progress.isActive)
                    {
                        var data = mgr.GetChainData(ids[i]);
                        if (data != null)
                            chains.Add(new KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>(data, progress));
                    }
                }
            }

            // 통계
            if (_statAvailable != null) _statAvailable.text = $"🆕 수락 가능: {available.Count}";
            if (_statActive != null) _statActive.text = $"🔄 진행 중: {active.Count}";
            if (_statCompleted != null) _statCompleted.text = $"✅ 완료: {completed.Count}";
            if (_statChain != null) _statChain.text = $"🔗 활성 체인: {chains.Count}";

            int tracked = active.Count + chains.Count + (int)Mathf.Min(5, available.Count);
            if (_summaryFooter != null)
                _summaryFooter.text = $"등록: 수락가능 {available.Count} / 진행 {active.Count} / 완료 {completed.Count} / 체인 {chains.Count}  (추적 제한 2/5)";

            // 목록 재조립 (필터 적용)
            _list.Clear();
            bool any = false;

            if (_filter == QuestFilter.Active && chains.Count > 0)
            {
                _list.Add(BuildChainHeader());
                for (int i = 0; i < chains.Count; i++)
                {
                    _list.Add(BuildChainBox(chains[i]));
                    any = true;
                }
            }

            if (_filter == QuestFilter.Active)
            {
                for (int i = 0; i < active.Count; i++)
                {
                    _list.Add(BuildQuestBox(active[i], QuestState.Active));
                    any = true;
                }
            }

            if (_filter == QuestFilter.Completed)
            {
                for (int i = 0; i < completed.Count; i++)
                {
                    _list.Add(BuildQuestBox(completed[i], QuestState.Completed));
                    any = true;
                }
            }

            // Keep acceptance reachable without adding a third tab that the archive does not contain.
            if (_filter == QuestFilter.Active)
            {
                for (int i = 0; i < available.Count; i++)
                {
                    _list.Add(BuildQuestBox(available[i], QuestState.Available));
                    any = true;
                }
            }

            if (!any)
            {
                var empty = MkLabel("표시할 퀘스트가 없습니다.", 15, GitHubDark.TextSub, TextAnchor.UpperLeft);
                empty.style.flexGrow = 1f;
                _list.Add(empty);
            }

            // 선택 퀘스트는 현재 필터에 표시될 수 있는 항목으로만 유지한다.
            string sel = _selectedQuestId;
            if (!IsQuestVisible(sel, active, completed, available, chains))
                sel = null;

            if (string.IsNullOrEmpty(sel))
            {
                if (_filter == QuestFilter.Active && chains.Count > 0)
                    sel = chains[0].Key.chainId;
                else if (_filter == QuestFilter.Active && active.Count > 0)
                    sel = active[0].questId;
                else if (_filter == QuestFilter.Active && available.Count > 0)
                    sel = available[0].questId;
                else if (_filter == QuestFilter.Completed && completed.Count > 0)
                    sel = completed[0].questId;
            }
            _selectedQuestId = sel;

            RefreshDetail(sel, chains);
        }

        private bool IsQuestVisible(string questId, List<QuestData> active, List<QuestData> completed,
                                     List<QuestData> available, List<KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>> chains)
        {
            if (string.IsNullOrEmpty(questId)) return false;

            if (_filter == QuestFilter.Active)
                for (int i = 0; i < active.Count; i++) if (active[i].questId == questId) return true;
            if (_filter == QuestFilter.Completed)
                for (int i = 0; i < completed.Count; i++) if (completed[i].questId == questId) return true;
            if (_filter == QuestFilter.Active)
            {
                for (int i = 0; i < available.Count; i++) if (available[i].questId == questId) return true;
                for (int i = 0; i < chains.Count; i++) if (chains[i].Key.chainId == questId) return true;
            }
            return false;
        }

        private void RecolorTabs()
        {
            for (int i = 0; i < _filterTabs.Count; i++)
            {
                Button b = _filterTabs[i];
                bool active = (i == 0 && _filter == QuestFilter.Active)
                    || (i == 1 && _filter == QuestFilter.Completed);
                if (active)
                {
                    b.style.backgroundColor = GitHubDark.Accent;
                    b.style.color = GitHubDark.Panel;
                }
                else
                {
                    b.style.backgroundColor = GitHubDark.PanelSub;
                    b.style.color = GitHubDark.TextMain;
                }
            }
        }

        private VisualElement BuildChainHeader()
        {
            var h = MkLabel("🔗 활성 퀘스트 체인", 17, GitHubDark.Accent, TextAnchor.MiddleLeft);
            h.style.marginTop = 6f;
            h.style.marginBottom = 2f;
            return h;
        }

        private VisualElement BuildChainBox(KeyValuePair<QuestChainData, QuestChainManager.ChainProgress> kvp)
        {
            var chainData = kvp.Key;
            var progress = kvp.Value;

            var box = new VisualElement();
            box.AddToClassList("utk-slot");
            ApplyDarkRowStyle(box);
            box.style.flexDirection = FlexDirection.Column;
            box.style.marginTop = 3f;
            box.style.marginBottom = 3f;
            box.style.paddingTop = 5f;
            box.style.paddingBottom = 5f;
            box.RegisterCallback<PointerDownEvent>(_ => { _selectedQuestId = chainData.chainId; RefreshDisplay(); });

            string progressStr = chainData.nodes != null
                ? $"{progress.completedNodeIds.Count}/{chainData.nodes.Length}"
                : "0/0";
            var title = MkLabel($"{chainData.chainTitle}  [{progressStr}]", 15, GitHubDark.Accent, TextAnchor.MiddleLeft);
            box.Add(title);

            string nodeTitle = "알 수 없음";
            string nodeDesc = "";
            bool hasChoices = false;
            var node = chainData.GetNode(progress.currentNodeId);
            if (!string.IsNullOrEmpty(node.id))
            {
                nodeTitle = string.IsNullOrEmpty(node.title) ? node.id : node.title;
                nodeDesc = string.IsNullOrEmpty(node.description) ? "" : node.description;
                hasChoices = node.choices != null && node.choices.Length > 0;
            }

            var desc = MkLabel($"▸ 현재: {nodeTitle}  {nodeDesc}", 13, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            desc.style.whiteSpace = WhiteSpace.Normal;
            box.Add(desc);

            string btnText = hasChoices ? "선택" : "진행";
            string chainId = progress.chainId;
            var btn = UTKButton.Create(btnText, () =>
            {
                if (hasChoices)
                {
                    var uiRoot = UIToolkitBootstrap.UIRoot;
                    if (uiRoot != null)
                    {
                        QuestChoiceUTK.Show(chainId, node);
                        var choicePopup = QuestChoiceUTK.Instance;
                        if (choicePopup != null && choicePopup.parent != uiRoot)
                        {
                            choicePopup.RemoveFromHierarchy();
                            uiRoot.Add(choicePopup);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[QuestWindowUTK] 선택지 팝업을 표시할 수 없습니다: UIToolkitBootstrap.UIRoot가 없습니다. 씬의 UI Toolkit 부트스트랩/UI Document를 초기화한 뒤 다시 시도하세요. 체인은 변경되지 않았습니다.");
                    }
                    return;
                }

                bool ok = QuestChainManager.Instance != null
                    && QuestChainManager.Instance.CompleteCurrentNode(chainId, -1);
                Debug.Log($"[QuestWindowUTK] 체인 노드 진행({chainId}, 자동): 성공={ok}");
                RefreshDisplay();
            }, UTKButton.Variant.Primary);
            StyleButton(btn, UTKButton.Variant.Primary);
            btn.style.alignSelf = Align.FlexEnd;
            box.Add(btn);

            return box;
        }

        private VisualElement BuildQuestBox(QuestData quest, QuestState state)
        {
            var box = new VisualElement();
            box.AddToClassList("utk-slot");
            ApplyDarkRowStyle(box);
            box.name = "QuestSlot";
            box.style.flexDirection = FlexDirection.Column;
            box.style.position = Position.Relative;
            box.style.minHeight = 135.2f;
            box.style.height = 135.2f;
            box.style.flexShrink = 0f;
            box.style.marginBottom = 9.6f;
            box.style.paddingTop = 14.4f;
            box.style.paddingBottom = 14.4f;
            box.style.paddingLeft = 14.4f;
            box.style.paddingRight = 14.4f;
            string qid = quest.questId;
            box.RegisterCallback<PointerDownEvent>(_ => { _selectedQuestId = qid; RefreshDisplay(); });

            // 상태 배지 (TierStrip 개수색)
            string stateStr = state == QuestState.Active ? "진행 중"
                : state == QuestState.Completed ? "완료" : "수락 가능";
            Color stateColor = state == QuestState.Active ? GitHubDark.Accent
                : state == QuestState.Completed ? GitHubDark.Success : GitHubDark.Gold;

            // [Figma 93:230] full-height TierStrip + data-backed category pill + level.
            var cardRow = new VisualElement();
            cardRow.name = "CardHeader";
            cardRow.style.flexDirection = FlexDirection.Row;
            cardRow.style.alignItems = Align.Center;
            cardRow.style.height = 18.8f;
            cardRow.style.flexShrink = 0f;

            var tierStrip = new VisualElement();
            tierStrip.name = "TierStrip";
            tierStrip.style.width = 4.8f;
            tierStrip.style.position = Position.Absolute;
            tierStrip.style.left = -14.4f;
            tierStrip.style.top = -14.4f;
            tierStrip.style.bottom = -14.4f;
            tierStrip.style.backgroundColor = new StyleColor(stateColor);
            box.Add(tierStrip);

            string categoryStr = quest.isMain ? "주 임무" : "부 임무";
            var pill = MkLabel(categoryStr, UTKTheme.FontBadge, GitHubDark.TextSub, TextAnchor.MiddleCenter);   // [Figma] 12/700
            pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.name = "CategoryPill";
            pill.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);
            pill.style.borderTopLeftRadius = 4f;
            pill.style.borderTopRightRadius = 4f;
            pill.style.borderBottomLeftRadius = 4f;
            pill.style.borderBottomRightRadius = 4f;
            pill.style.paddingLeft = 6f;
            pill.style.paddingRight = 6f;
            pill.style.height = 18.8f;
            pill.style.flexShrink = 0f;
            cardRow.Add(pill);

            var levelLbl = MkLabel(quest.requiredLevel > 0 ? $"Lv.{quest.requiredLevel}" : "", UTKTheme.FontTab, GitHubDark.Accent, TextAnchor.MiddleLeft);   // [Figma] 14.4/700
            levelLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            levelLbl.style.marginLeft = 6f;
            cardRow.Add(levelLbl);

            box.Add(cardRow);

            // 제목 + 설명 — Figma CardBody region with live quest data.
            var cardBody = new VisualElement { name = "CardBody" };
            cardBody.style.flexDirection = FlexDirection.Column;
            cardBody.style.marginTop = 4.8f;
            cardBody.style.flexShrink = 0f;
            Color questNameColor = quest.isMain ? GitHubDark.Gold : GitHubDark.Accent;
            var nameLabel = MkLabel(quest.questName, UTKTheme.FontCardName, questNameColor, TextAnchor.MiddleLeft);   // [Figma] 16.8/700
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.minHeight = 20f;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            cardBody.Add(nameLabel);

            if (!string.IsNullOrEmpty(quest.description))
            {
                var desc = MkLabel(quest.description, UTKTheme.FontBody, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [Figma] 14.4/400
                desc.style.minHeight = 17f;
                desc.style.marginTop = 4.8f;
                desc.style.whiteSpace = WhiteSpace.Normal;
                cardBody.Add(desc);
            }
            box.Add(cardBody);

            // [P4 Figma] 진행 게이지 + %
            int objCount = quest.objectives != null ? quest.objectives.Count : 0;
            int metCount = 0;
            if (quest.objectives != null)
            {
                for (int i = 0; i < quest.objectives.Count; i++)
                    if (quest.objectives[i].IsMet) metCount++;
            }
            float prog = objCount > 0 ? (float)metCount / objCount : (state == QuestState.Completed ? 1f : 0f);

            var progRow = new VisualElement();
            progRow.name = "ProgressArea";
            progRow.style.flexDirection = FlexDirection.Row;
            progRow.style.alignItems = Align.Center;
            progRow.style.height = state == QuestState.Completed ? 17f : 26f;
            progRow.style.flexShrink = 0f;
            progRow.style.marginTop = 7.2f;

            var track = new VisualElement();
            track.name = "GaugeTrack";
            track.style.flexGrow = 1f;
            track.style.minWidth = 0f;
            track.style.height = 4.8f;
            track.style.backgroundColor = new StyleColor(GitHubDark.BgBase);
            track.style.borderTopLeftRadius = 2f;
            track.style.borderTopRightRadius = 2f;
            track.style.borderBottomLeftRadius = 2f;
            track.style.borderBottomRightRadius = 2f;

            var fill = new VisualElement();
            fill.name = "GaugeFill";
            fill.style.height = new Length(100f, LengthUnit.Percent);
            fill.style.width = new Length(Mathf.Clamp01(prog) * 100f, LengthUnit.Percent);
            fill.style.backgroundColor = new StyleColor(stateColor);
            track.Add(fill);
            progRow.Add(track);

            var pct = MkLabel($"{Mathf.RoundToInt(Mathf.Clamp01(prog) * 100f)}%", 12, GitHubDark.TextMain, TextAnchor.MiddleRight);
            pct.style.width = 32f;
            pct.style.marginLeft = 6f;
            progRow.Add(pct);

            // Keep existing accept/complete behavior inside the archived card bounds.
            if (state == QuestState.Available || state == QuestState.Active)
            {
                Button actionButton;
                if (state == QuestState.Available)
                {
                    actionButton = UTKButton.Create("수락", () =>
                    {
                        bool ok = QuestManager.AcceptQuest(qid);
                        Debug.Log($"[QuestWindowUTK] 퀘스트 수락 시도({qid}): 성공={ok}");
                        _selectedQuestId = null;
                        RefreshDisplay();
                    }, UTKButton.Variant.Primary);
                    StyleButton(actionButton, UTKButton.Variant.Primary);
                }
                else
                {
                    bool completeable = quest.AllObjectivesMet;
                    actionButton = UTKButton.Create("완료", () =>
                    {
                        bool ok = QuestManager.TryCompleteQuest(qid);
                        Debug.Log($"[QuestWindowUTK] 퀘스트 완료 시도({qid}): 성공={ok}");
                        _selectedQuestId = null;
                        RefreshDisplay();
                    }, UTKButton.Variant.Danger);
                    actionButton.SetEnabled(completeable);
                    StyleButton(actionButton, UTKButton.Variant.Danger);
                }
                actionButton.style.flexShrink = 0f;
                actionButton.style.width = 54f;
                actionButton.style.height = 26f;
                actionButton.style.marginLeft = 7.2f;
                progRow.Add(actionButton);
            }
            box.Add(progRow);

            Debug.Log($"[QuestWindowUTK] 퀘스트 항목: {quest.questName} ({qid}) [{stateStr}] 진행 {metCount}/{objCount}");

            return box;
        }

        // =====================================================================
        //  [P4] 중앙 상세 + 우 보상 갱신
        //  =====================================================================

        private void RefreshDetail(string questId, List<KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>> chains)
        {
            QuestData quest = FindQuestData(questId);
            if (string.IsNullOrEmpty(quest.questId))
            {
                KeyValuePair<QuestChainData, QuestChainManager.ChainProgress> selectedChain = default;
                for (int i = 0; i < chains.Count; i++)
                {
                    if (chains[i].Key.chainId == questId)
                    {
                        selectedChain = chains[i];
                        break;
                    }
                }

                if (selectedChain.Key != null)
                {
                    QuestChainData chain = selectedChain.Key;
                    QuestChainManager.ChainProgress progress = selectedChain.Value;
                    QuestChainNode node = chain.GetNode(progress.currentNodeId);
                    _detailHeroTitle.text = string.IsNullOrEmpty(chain.chainTitle) ? chain.chainId : chain.chainTitle;
                    int completedNodeCount = progress.completedNodeIds != null ? progress.completedNodeIds.Count : 0;
                    _detailHeroTag.text = $"활성 퀘스트 체인  ·  노드 {completedNodeCount}/{(chain.nodes != null ? chain.nodes.Length : 0)}";
                    _detailStory.text = string.IsNullOrEmpty(chain.chainDescription) ? "현재 진행 중인 퀘스트 체인입니다." : chain.chainDescription;
                    _objectivesList.Clear();
                    if (!string.IsNullOrEmpty(node.id))
                    {
                        var nodeTitle = MkLabel("현재 노드: " + (string.IsNullOrEmpty(node.title) ? node.id : node.title), 13, GitHubDark.Accent, TextAnchor.MiddleLeft);
                        nodeTitle.style.whiteSpace = WhiteSpace.Normal;
                        _objectivesList.Add(nodeTitle);
                        if (!string.IsNullOrEmpty(node.description))
                        {
                            var nodeDescription = MkLabel(node.description, 12, GitHubDark.TextSub, TextAnchor.UpperLeft);
                            nodeDescription.style.whiteSpace = WhiteSpace.Normal;
                            _objectivesList.Add(nodeDescription);
                        }
                        if (node.objectives != null)
                        {
                            for (int i = 0; i < node.objectives.Length; i++)
                                _objectivesList.Add(MkLabel("• " + node.objectives[i], 12, GitHubDark.TextMain, TextAnchor.MiddleLeft));
                        }
                    }
                    else
                    {
                        _objectivesList.Add(MkLabel("현재 노드 정보를 찾을 수 없습니다.", 12, GitHubDark.TextSub, TextAnchor.MiddleLeft));
                    }

                    RefreshRewards(new QuestData { questId = "" });
                    return;
                }

                _detailHeroTitle.text = "퀘스트를 선택하세요";
                _detailHeroTag.text = "";
                _detailStory.text = "왼쪽 목록에서 퀘스트를 선택하면 상세 정보가 표시됩니다.";
                _objectivesList.Clear();
                RefreshRewards(new QuestData { questId = "" });
                return;
            }

            _detailHeroTitle.text = quest.questName;
            _detailHeroTitle.style.color = new StyleColor(quest.isMain ? GitHubDark.Gold : GitHubDark.Accent);
            _detailHeroTag.text = $"{(quest.isMain ? "주 임무" : "부 임무")}  ·  Lv.{quest.requiredLevel}  ·  {QuestRewardPreview.GetRewardSummary(quest)}";
            _detailStory.text = string.IsNullOrEmpty(quest.description) ? "—" : quest.description;

            // 목표 체크리스트
            _objectivesList.Clear();
            if (quest.objectives != null)
            {
                for (int i = 0; i < quest.objectives.Count; i++)
                    _objectivesList.Add(BuildObjectiveRow(quest.objectives[i]));
            }

            RefreshRewards(quest);
        }

        private VisualElement BuildObjectiveRow(QuestObjective obj)
        {
            var row = new VisualElement();
            row.name = "GoalRow";
            row.style.flexDirection = FlexDirection.Column;
            row.style.height = 32f;
            row.style.flexShrink = 0f;

            float ratio = obj.requiredCount > 0 ? Mathf.Clamp01((float)obj.currentCount / obj.requiredCount) : (obj.IsMet ? 1f : 0f);
            Color c = obj.IsMet ? GitHubDark.Success : GitHubDark.TextSub;

            var contentRow = new VisualElement();
            contentRow.style.flexDirection = FlexDirection.Row;
            contentRow.style.alignItems = Align.Center;
            contentRow.style.height = 20f;
            row.Add(contentRow);

            var mark = MkLabel(obj.IsMet ? "✓" : "○", 15, c, TextAnchor.MiddleCenter);
            mark.style.width = 26.4f;
            contentRow.Add(mark);

            var desc = MkLabel(obj.description ?? "목표", 13, GitHubDark.TextMain, TextAnchor.MiddleLeft);
            desc.style.flexGrow = 1f;
            desc.style.minWidth = 0f;
            desc.style.whiteSpace = WhiteSpace.Normal;
            contentRow.Add(desc);

            var cnt = MkLabel(obj.requiredCount > 0 ? $"{obj.currentCount}/{obj.requiredCount}" : (obj.IsMet ? "완료" : "진행"), 12, c, TextAnchor.MiddleRight);
            cnt.style.width = 47f;
            contentRow.Add(cnt);

            // Objective gauge follows the archived full-width track geometry.
            var track = new VisualElement();
            track.name = "GaugeTrack";
            track.style.width = new Length(100f, LengthUnit.Percent);
            track.style.height = 7.2f;
            track.style.marginTop = 4.8f;
            track.style.backgroundColor = new StyleColor(GitHubDark.BgBase);
            track.style.borderTopLeftRadius = 3f;
            track.style.borderTopRightRadius = 3f;
            track.style.borderBottomLeftRadius = 3f;
            track.style.borderBottomRightRadius = 3f;
            var fill = new VisualElement();
            fill.style.width = new Length(Mathf.Clamp01(ratio) * 100f, LengthUnit.Percent);
            fill.style.height = new Length(100f, LengthUnit.Percent);
            fill.style.backgroundColor = new StyleColor(c);
            track.Add(fill);
            row.Add(track);

            return row;
        }

        private void RefreshRewards(QuestData quest)
        {
            _rewardsList.Clear();
            if (string.IsNullOrEmpty(quest.questId))
            {
                _rewardsList.Add(MkLabel("—", 13, GitHubDark.TextSub, TextAnchor.MiddleLeft));
                return;
            }

            QuestReward reward = quest.reward;   // struct — 항상 존재, IsEmpty로 빈 판정
            bool empty = reward.IsEmpty;

            // 골드
            if (reward.gold > 0)
                _rewardsList.Add(BuildRewardRow($"💰 골드", $"{reward.gold:N0}", GitHubDark.Gold));
            // 경험치
            if (reward.exp > 0)
                _rewardsList.Add(BuildRewardRow($"✨ 경험치", $"+{reward.exp}", GitHubDark.Accent));
            // 호감도
            if (reward.affinity > 0)
                _rewardsList.Add(BuildRewardRow($"💛 호감도", $"+{reward.affinity}", GitHubDark.Success));

            // 아이템 카드 — 원본은 각 아이템을 1개씩 AddItem(ItemData list), 수량은 "1x"
            if (reward.items != null)
            {
                for (int i = 0; i < reward.items.Count; i++)
                {
                    var it = reward.items[i];
                    _rewardsList.Add(BuildRewardRow(
                        string.IsNullOrEmpty(it.displayName) ? it.id : it.displayName,
                        "1x",
                        GitHubDark.Gold));
                }
            }

            if (empty)
                _rewardsList.Add(MkLabel("보상 없음", 13, GitHubDark.TextSub, TextAnchor.MiddleLeft));
        }

        private VisualElement BuildRewardRow(string name, string value, Color accent)
        {
            var row = new VisualElement();
            row.name = "RewardRow";
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 72f;
            row.style.flexShrink = 0f;
            row.style.marginTop = 4.8f;
            row.style.marginBottom = 9.6f;
            row.style.paddingTop = 12f;
            row.style.paddingBottom = 12f;
            row.style.paddingLeft = 12f;
            row.style.paddingRight = 12f;
            row.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);
            row.style.borderTopLeftRadius = 6f;
            row.style.borderTopRightRadius = 6f;
            row.style.borderBottomLeftRadius = 6f;
            row.style.borderBottomRightRadius = 6f;

            var icon = new VisualElement();
            icon.style.width = 48f;
            icon.style.height = 48f;
            icon.style.flexShrink = 0f;
            icon.style.backgroundColor = new StyleColor(GitHubDark.BgBase);
            icon.style.borderTopWidth = 1f;
            icon.style.borderBottomWidth = 1f;
            icon.style.borderLeftWidth = 1f;
            icon.style.borderRightWidth = 1f;
            icon.style.borderTopColor = icon.style.borderBottomColor = icon.style.borderLeftColor = icon.style.borderRightColor = new StyleColor(accent);
            icon.style.borderTopLeftRadius = 4f;
            icon.style.borderTopRightRadius = 4f;
            icon.style.borderBottomLeftRadius = 4f;
            icon.style.borderBottomRightRadius = 4f;
            row.Add(icon);

            var nameLbl = MkLabel(name, 14, GitHubDark.TextMain, TextAnchor.MiddleLeft);
            nameLbl.style.flexGrow = 1f;
            nameLbl.style.minWidth = 0f;
            nameLbl.style.marginLeft = 14.4f;
            row.Add(nameLbl);

            var val = MkLabel(value, 13, accent, TextAnchor.MiddleRight);
            val.style.width = 57f;
            row.Add(val);

            return row;
        }

        private QuestData FindQuestData(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return new QuestData { questId = "" };
            List<QuestData> all = QuestManager.GetActiveQuests();
            for (int i = 0; i < all.Count; i++) if (all[i].questId == questId) return all[i];
            all = QuestManager.GetCompletedQuests();
            for (int i = 0; i < all.Count; i++) if (all[i].questId == questId) return all[i];
            if (PlayerStats.Instance != null)
            {
                all = QuestManager.GetAvailableQuests(PlayerStats.Instance.Level);
                for (int i = 0; i < all.Count; i++) if (all[i].questId == questId) return all[i];
            }
            return new QuestData { questId = "" };
        }

        // =====================================================================
        //  키 토글 / ESC용 Updater — StatusWindowUTK 패턴
        //  =====================================================================

        private class Updater : MonoBehaviour
        {
            public QuestWindowUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                // Q키 토글 복구 — 5858bc18(10-01)에서 유실된 유일한 열기 경로(퀘스트창 기능 소실 버그). ESC 닫기와 병행.
                if (kb != null && kb.qKey.wasPressedThisFrame && window != null)
                    if (window.IsOpen) window.Close(); else QuestWindowUTK.Toggle();   // Toggle은 static(싱글턴 토글)
                if (kb != null && kb.escapeKey.wasPressedThisFrame && window != null && window.IsOpen)
                    window.Close();
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
        //  =====================================================================

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