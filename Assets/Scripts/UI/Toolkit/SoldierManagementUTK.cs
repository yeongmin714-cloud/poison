using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // GuardManager, GuardPlaceholder, GuardTaskSystem
using ProjectName.Core;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// 병사 관리 창 (Figma soldier-management-ui 정합 — 로직을 피그마 구조에 맞춤).
    /// 피그마: 좌 SoldierListPanel(3필터탭 + 병사 카드 목록) + 중앙 상세(능력치 프로그레스) + 우 배치(역할 7 + 적용).
    /// 데이터: GuardManager.GetAllPlayerGuards() 목록, 선택 병사 상세, GuardTaskSystem 역할 배정.
    /// GitHub-dark 팔레트. 이 창 한정 인라인 오버라이드 — 공용 파일 무수정.
    /// </summary>
    public class SoldierManagementUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static SoldierManagementUTK _instance;
        public static SoldierManagementUTK Instance => _instance;

        public static readonly Rect CompositionBounds = new Rect(168f, 48f, 1584f, 984f);
        public static readonly Rect SoldierListBounds = new Rect(0f, 0f, 480f, 984f);
        public static readonly Rect SoldierDetailBounds = new Rect(504f, 0f, 576f, 984f);
        public static readonly Rect DeploymentBounds = new Rect(1104f, 0f, 480f, 984f);

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new SoldierManagementUTK();
        }

        public static void Open() { Ensure(); _instance.Show(); }
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Hide(); return; }
            Open();
        }

        // ===== GitHub-dark 팔레트 (이 창 한정) =====
        private static class GitHubDark
        {
            public static readonly Color BgBase   = Hex(0x0B0E14);
            public static readonly Color Panel    = Hex(0x161B22);
            public static readonly Color PanelSub = Hex(0x21262D);
            public static readonly Color Accent   = Hex(0x58A6FF);
            public static readonly Color Gold     = Hex(0xE3B341);
            public static readonly Color TextMain = Hex(0xF0F6FC);
            public static readonly Color TextSub  = Hex(0x8B949E);
            public static readonly Color Stroke   = Hex(0x2E343D);
            public static readonly Color Health   = Hex(0x3FB950);
            public static readonly Color Danger   = Hex(0xF85149);
            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        private static void StyleButton(Button btn, bool primary)
        {
            if (btn == null) return;
            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = primary ? GitHubDark.Accent : GitHubDark.PanelSub;
            btn.style.color = primary ? GitHubDark.BgBase : GitHubDark.TextMain;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(primary ? GitHubDark.Accent : GitHubDark.Stroke);
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 6f;
            var accent = primary ? GitHubDark.Accent : GitHubDark.PanelSub;
            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = accent);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = primary ? GitHubDark.Accent : GitHubDark.PanelSub);
        }

        private static Label MkLabel(string text, float size, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            return l;
        }

        // ===== 필터 (피그마 3탭: 전체/전투/용병) =====
        private enum FilterCat { All, Combat, Mercenary }

        // ===== 상태 =====
        private readonly List<GuardPlaceholder> _guards = new List<GuardPlaceholder>();
        private readonly ScrollView _listScroll;
        private readonly Label _summaryLabel;
        private readonly VisualElement _listPanel;
        private readonly VisualElement _detailPanel;
        private readonly VisualElement _deploymentPanel;
        private readonly VisualElement _detailRoot;   // [69:118] 상세는 부모 패널에 직접 구성(빌더 null 반환) — 레거시 참조 유지용
        private readonly List<Button> _filterTabs = new List<Button>(3);
        private readonly List<VisualElement> _deploymentCards = new List<VisualElement>(8);
        private readonly List<Label> _deploymentCounts = new List<Label>(8);
        private Button _applyDeploymentButton;
        private GuardTaskSystem.GuardTask _pendingTask;
        private FilterCat _filter = FilterCat.All;
        private GuardPlaceholder _selected;
        private IVisualElementScheduledItem _refreshTask;

        private const long RefreshMs = 250L;
        private VisualElement _geometryRoot;

        private SoldierManagementUTK() : base("⚔️ 병사 관리", new Vector2(1584f, 984f))
        {
            SetChrome(UTKWindowChrome.Frameless);
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Row;
            _content.style.paddingLeft = _content.style.paddingRight = 0f;
            _content.style.paddingTop = _content.style.paddingBottom = 0f;
            _content.style.position = Position.Relative;
            _content.name = "SoldierManagementCompositionContent";

            // ── 좌: 목록 ──
            _listPanel = CreatePanelRoot("SoldierListPanel", SoldierListBounds);
            var left = _listPanel;

            left.Add(MkPanelHeader("부대원 명부", "SOLDIERS LIST"));

            // [Figma] FilterTabs 432×36.2, gap 4.8, Tab pad 12/9.6
            var filterRow = new VisualElement();
            filterRow.style.flexDirection = FlexDirection.Row;
            filterRow.style.height = 36.2f;
            filterRow.style.minHeight = 36.2f;
            filterRow.style.marginBottom = 19.2f;
            string[] flt = { "전체", "전투", "용병" };
            for (int i = 0; i < flt.Length; i++)
            {
                var f = (FilterCat)i;
                var b = new Button(() => SelectFilter(f));
                b.text = flt[i];
                b.style.flexGrow = 1f;
                b.style.height = 36.2f;
                b.style.marginRight = i < flt.Length - 1 ? 4.8f : 0f;
                StyleButton(b, false);
                _filterTabs.Add(b);
                filterRow.Add(b);
            }
            left.Add(filterRow);
            RefreshFilterTabs();

            _listScroll = new ScrollView();
            _listScroll.style.flexGrow = 1f;
            left.Add(_listScroll);

            // [Figma SummaryFooter] 좌 라벨 + 우 값 (병사 N/N명 · 임무 배치 N명 — 실존 데이터)
            var summaryRow = new VisualElement { name = "SummaryFooter" };
            summaryRow.style.flexDirection = FlexDirection.Row;
            summaryRow.style.alignItems = Align.Center;
            summaryRow.style.height = 34.4f;
            summaryRow.style.minHeight = 34.4f;
            var summaryLeft = MkLabel("총원 · 임무 배치", 12f, GitHubDark.TextSub);
            summaryLeft.style.flexGrow = 1f;
            summaryRow.Add(summaryLeft);
            _summaryLabel = MkLabel("", 13f, GitHubDark.TextMain);
            _summaryLabel.name = "DeploySummary";
            _summaryLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            summaryRow.Add(_summaryLabel);
            left.Add(summaryRow);

            // ── 중앙: 상세 ──
            _detailPanel = CreatePanelRoot("SoldierDetailPanel", SoldierDetailBounds);
            _detailRoot = BuildDetailRoot(_detailPanel);

            // ── 우: 배치 ──
            _deploymentPanel = CreatePanelRoot("SoldierDeploymentPanel", DeploymentBounds);
            BuildDeployPanel(_deploymentPanel);

            CreateCloseButton(_deploymentPanel);

            ApplyUIToolkitFont(this);
            ApplyGitHubDarkWindowStyle();
            if (IsFrameless)
            {
                // This composition is a transparent full-canvas host; style only the child panels.
                style.backgroundColor = new StyleColor(Color.clear);
                style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
                style.borderTopLeftRadius = style.borderTopRightRadius = 0f;
                style.borderBottomLeftRadius = style.borderBottomRightRadius = 0f;
            }
            style.display = DisplayStyle.None;
        }

        private VisualElement CreatePanelRoot(string panelName, Rect bounds)
        {
            var panelRoot = new VisualElement { name = panelName };
            panelRoot.style.position = Position.Absolute;
            panelRoot.style.left = Length.Percent(bounds.x / CompositionBounds.width * 100f);
            panelRoot.style.top = Length.Percent(bounds.y / CompositionBounds.height * 100f);
            panelRoot.style.width = Length.Percent(bounds.width / CompositionBounds.width * 100f);
            panelRoot.style.height = Length.Percent(bounds.height / CompositionBounds.height * 100f);
            panelRoot.style.flexDirection = FlexDirection.Column;
            panelRoot.style.flexShrink = 0f;
            panelRoot.style.backgroundColor = GitHubDark.Panel;
            panelRoot.style.borderTopWidth = panelRoot.style.borderBottomWidth = panelRoot.style.borderLeftWidth = panelRoot.style.borderRightWidth = 1f;
            panelRoot.style.borderTopColor = panelRoot.style.borderBottomColor = panelRoot.style.borderLeftColor = panelRoot.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            // [Figma 69:4] 패널 pad 24 + 세로 gap 19.2 + 모서리 L 데칼(14.4, 선 12)
            panelRoot.style.paddingLeft = panelRoot.style.paddingRight = 24f;
            panelRoot.style.paddingTop = panelRoot.style.paddingBottom = 24f;
            panelRoot.style.justifyContent = Justify.FlexStart;
            AddCornerDecals(panelRoot);
            _content.Add(panelRoot);
            return panelRoot;
        }

        /// <summary>[Figma] 패널 4모서리 L자 데칼 — 시각 전용(pickingMode Ignore).</summary>
        private static void AddCornerDecals(VisualElement panel)
        {
            void AddL(float left, float top, float hLine, float vLine, bool verticalFirst)
            {
                var l1 = new VisualElement();
                l1.style.position = Position.Absolute;
                l1.style.left = left; l1.style.top = top;
                l1.style.width = hLine; l1.style.height = 1f;
                l1.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
                l1.pickingMode = PickingMode.Ignore;
                var l2 = new VisualElement();
                l2.style.position = Position.Absolute;
                l2.style.left = left; l2.style.top = top;
                l2.style.width = 1f; l2.style.height = vLine;
                l2.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
                l2.pickingMode = PickingMode.Ignore;
                panel.Add(verticalFirst ? l2 : l1);
                panel.Add(verticalFirst ? l1 : l2);
            }
            AddL(0f, 0f, 12f, 12f, false);                 // TL
            AddL(12f, 0f, 12f, 12f, false);                // TR (두 번째 선이 right에 붙도록 위치 보정)
            AddL(0f, 11f, 12f, 12f, false);                // BL
            AddL(12f, 11f, 12f, 12f, false);               // BR
        }

        private void CreateCloseButton(VisualElement parent)
        {
            var close = new Button(Close) { text = "✕", name = "DeploymentCloseButton" };
            close.style.position = Position.Absolute;
            close.style.top = 8f;
            close.style.right = 8f;
            close.style.width = 32f;
            close.style.height = 32f;
            StyleButton(close, false);
            parent.Add(close);
        }

        private void ApplyCompositionBounds()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float rootWidth = root.resolvedStyle.width;
            float rootHeight = root.resolvedStyle.height;
            if (rootWidth <= 0f || rootHeight <= 0f) return;
            style.position = Position.Absolute;
            style.right = StyleKeyword.Auto;
            style.bottom = StyleKeyword.Auto;
            style.left = Length.Percent(CompositionBounds.x / FigmaCanvasLayout.CanvasWidth * 100f);
            style.top = Length.Percent(CompositionBounds.y / FigmaCanvasLayout.CanvasHeight * 100f);
            style.width = Length.Percent(CompositionBounds.width / FigmaCanvasLayout.CanvasWidth * 100f);
            style.height = Length.Percent(CompositionBounds.height / FigmaCanvasLayout.CanvasHeight * 100f);
        }

        // ===== 중앙 상세 [Figma 69:118 SoldierDetailPanel] =====
        private Label _dName, _dLevel, _dRole, _dNationBadge, _dTraitText, _dImageHint;
        private readonly VisualElement[] _statBars = new VisualElement[4];
        private readonly Label[] _statVals = new Label[4];

        private VisualElement BuildDetailRoot(VisualElement parent)
        {
            parent.Add(MkPanelHeader("상세 정보", "SOLDIER SPECIFICATIONS"));

            // [Figma SoldierHeroHeader 528×84.6] pad 14.4 — 이름(24) + 국가 배지 / 서브 "Lv. n · 직책"
            var hero = new VisualElement { name = "SoldierHeroHeader" };
            hero.style.height = 84.6f;
            hero.style.minHeight = 84.6f;
            hero.style.flexShrink = 0f;
            hero.style.backgroundColor = GitHubDark.PanelSub;
            hero.style.borderTopWidth = hero.style.borderBottomWidth = hero.style.borderLeftWidth = hero.style.borderRightWidth = 1f;
            hero.style.borderTopColor = hero.style.borderBottomColor = hero.style.borderLeftColor = hero.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            hero.style.borderTopLeftRadius = hero.style.borderTopRightRadius = hero.style.borderBottomLeftRadius = hero.style.borderBottomRightRadius = 8f;
            hero.style.paddingLeft = hero.style.paddingRight = hero.style.paddingTop = hero.style.paddingBottom = 14.4f;
            parent.Add(hero);
            var heroRow = new VisualElement();
            heroRow.style.flexDirection = FlexDirection.Row;
            heroRow.style.alignItems = Align.Center;
            heroRow.style.height = 31f;
            _dName = MkLabel("병사를 선택하세요", 24f, GitHubDark.TextMain);
            heroRow.Add(_dName);
            var heroSpacer = new VisualElement();
            heroSpacer.style.flexGrow = 1f;
            heroRow.Add(heroSpacer);
            _dNationBadge = MkLabel("-", 11f, GitHubDark.TextSub);
            _dNationBadge.style.backgroundColor = new StyleColor(new Color(0.08f, 0.10f, 0.13f, 1f));
            _dNationBadge.style.borderTopWidth = _dNationBadge.style.borderBottomWidth = _dNationBadge.style.borderLeftWidth = _dNationBadge.style.borderRightWidth = 1f;
            _dNationBadge.style.borderTopColor = _dNationBadge.style.borderBottomColor = _dNationBadge.style.borderLeftColor = _dNationBadge.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            _dNationBadge.style.borderTopLeftRadius = _dNationBadge.style.borderTopRightRadius = _dNationBadge.style.borderBottomLeftRadius = _dNationBadge.style.borderBottomRightRadius = 4f;
            _dNationBadge.style.paddingLeft = _dNationBadge.style.paddingRight = 9.6f;
            _dNationBadge.style.paddingTop = _dNationBadge.style.paddingBottom = 2.4f;
            heroRow.Add(_dNationBadge);
            hero.Add(heroRow);
            _dLevel = MkLabel("", 13f, GitHubDark.TextSub);
            _dLevel.style.marginTop = 4.8f;
            hero.Add(_dLevel);

            // [Figma SoldierImageFrame 528×384] 3D 미리보기 placeholder (데이터 아닌 시각 자리)
            var image = new VisualElement { name = "SoldierImageFrame" };
            image.style.height = 384f;
            image.style.minHeight = 384f;
            image.style.flexShrink = 0f;
            image.style.backgroundColor = GitHubDark.BgBase;
            image.style.borderTopWidth = image.style.borderBottomWidth = image.style.borderLeftWidth = image.style.borderRightWidth = 1f;
            image.style.borderTopColor = image.style.borderBottomColor = image.style.borderLeftColor = image.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            image.style.borderTopLeftRadius = image.style.borderTopRightRadius = image.style.borderBottomLeftRadius = image.style.borderBottomRightRadius = 8f;
            image.style.justifyContent = Justify.Center;
            image.style.alignItems = Align.Center;
            _dImageHint = MkLabel("3D 미리보기", 15f, GitHubDark.TextSub);
            image.Add(_dImageHint);
            parent.Add(image);

            // [Figma StatsContainer 528×236.8] pad 19.2, gap 14.4 — StatRow(라벨+우측 "cur / max" + GaugeTrack 489.6×9.6)
            var stats = new VisualElement { name = "StatsContainer" };
            stats.style.height = 236.8f;
            stats.style.minHeight = 236.8f;
            stats.style.flexShrink = 0f;
            stats.style.backgroundColor = GitHubDark.PanelSub;
            stats.style.borderTopWidth = stats.style.borderBottomWidth = stats.style.borderLeftWidth = stats.style.borderRightWidth = 1f;
            stats.style.borderTopColor = stats.style.borderBottomColor = stats.style.borderLeftColor = stats.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            stats.style.borderTopLeftRadius = stats.style.borderTopRightRadius = stats.style.borderBottomLeftRadius = stats.style.borderBottomRightRadius = 8f;
            stats.style.paddingLeft = stats.style.paddingRight = stats.style.paddingTop = stats.style.paddingBottom = 19.2f;
            parent.Add(stats);
            string[] statNames = { "공격력", "방어력", "체력", "민첩" };
            for (int i = 0; i < 4; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Column;
                row.style.marginBottom = i < 3 ? 14.4f : 0f;

                var labelRow = new VisualElement();
                labelRow.style.flexDirection = FlexDirection.Row;
                labelRow.style.alignItems = Align.Center;
                labelRow.style.height = 22f;
                var name = MkLabel(statNames[i], 14f, GitHubDark.TextSub);
                name.style.flexGrow = 1f;
                labelRow.Add(name);
                var val = MkLabel("-", 13f, GitHubDark.TextMain);
                val.style.unityTextAlign = TextAnchor.MiddleRight;
                labelRow.Add(val);
                _statVals[i] = val;
                row.Add(labelRow);

                // [Figma GaugeTrack 489.6×9.6]
                var track = new VisualElement { name = "GaugeTrack" };
                track.style.height = 9.6f;
                track.style.marginTop = 7.2f;
                track.style.backgroundColor = GitHubDark.BgBase;
                track.style.borderTopLeftRadius = track.style.borderBottomLeftRadius = track.style.borderTopRightRadius = track.style.borderBottomRightRadius = 2f;
                var fill = new VisualElement { name = "GaugeFill" };
                fill.style.height = new Length(100f, LengthUnit.Percent);
                fill.style.width = new Length(0f, LengthUnit.Percent);
                fill.style.backgroundColor = GitHubDark.Accent;
                fill.style.borderTopLeftRadius = fill.style.borderBottomLeftRadius = fill.style.borderTopRightRadius = fill.style.borderBottomRightRadius = 2f;
                track.Add(fill);
                _statBars[i] = fill;
                row.Add(track);
                stats.Add(row);
            }
            parent.Add(stats);

            // [Figma TraitSection 528×98] pad 14.4, gap 7.2 — 실존 데이터(직책/현재 임무)만, 모킹 특성 위조 금지
            var trait = new VisualElement { name = "TraitSection" };
            trait.style.height = 98f;
            trait.style.minHeight = 98f;
            trait.style.flexShrink = 0f;
            trait.style.backgroundColor = GitHubDark.PanelSub;
            trait.style.borderTopWidth = trait.style.borderBottomWidth = trait.style.borderLeftWidth = trait.style.borderRightWidth = 1f;
            trait.style.borderTopColor = trait.style.borderBottomColor = trait.style.borderLeftColor = trait.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            trait.style.borderTopLeftRadius = trait.style.borderTopRightRadius = trait.style.borderBottomLeftRadius = trait.style.borderBottomRightRadius = 8f;
            trait.style.paddingLeft = trait.style.paddingRight = trait.style.paddingTop = trait.style.paddingBottom = 14.4f;
            trait.Add(MkLabel("고유 전술 특성", 14f, GitHubDark.Gold));
            _dTraitText = MkLabel("-", 13f, GitHubDark.TextSub);
            _dTraitText.style.marginTop = 7.2f;
            _dTraitText.style.whiteSpace = WhiteSpace.Normal;
            trait.Add(_dTraitText);
            parent.Add(trait);

            return null;
        }

        // ===== 우: Figma deployment cards + one apply action =====
        private readonly List<(string, GuardTaskSystem.GuardTask)> _deployDefs = new List<(string, GuardTaskSystem.GuardTask)>
        {
            ("군사 공격 조", GuardTaskSystem.GuardTask.Attack),
            ("요새 수비대",  GuardTaskSystem.GuardTask.Defend),
            ("농사 투입",    GuardTaskSystem.GuardTask.Farm),
            ("채집 작전대",  GuardTaskSystem.GuardTask.Gather),
            ("외교 특사",    GuardTaskSystem.GuardTask.Envoy),
            ("광물 채굴대",  GuardTaskSystem.GuardTask.Mine),
            ("수색 정찰단",  GuardTaskSystem.GuardTask.Hunt),
            ("대기(해제)",   GuardTaskSystem.GuardTask.None),
        };

        private void BuildDeployPanel(VisualElement parent)
        {
            parent.Add(MkPanelHeader("임무 배치 및 제어", "DEPLOYMENT OPTIONS"));

            // [Figma SectionDescription 432×48.8]
            var tip = MkLabel("병사를 목록에서 선택하고 우측 역할로 배치합니다.", 13f, GitHubDark.TextSub);
            tip.style.whiteSpace = WhiteSpace.Normal;
            tip.style.height = 48.8f;
            tip.style.flexShrink = 0f;
            parent.Add(tip);

            var list = new ScrollView(ScrollViewMode.Vertical) { name = "DeploymentOptionsList" };
            list.style.flexGrow = 1f;
            list.style.minHeight = 0f;
            for (int i = 0; i < _deployDefs.Count; i++)
            {
                int idx = i;
                // [Figma MissionOption 432×67.2] pad 14.4, gap 9.6 — 좌 라디오 21.6 + 임무명, 우 CountBadge 77.2×28.6
                var row = new VisualElement { name = "DeploymentOption_" + idx };
                row.style.height = 67.2f;
                row.style.minHeight = 67.2f;
                row.style.flexShrink = 0f;
                row.style.marginBottom = 9.6f;
                row.style.paddingLeft = row.style.paddingRight = 14.4f;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = GitHubDark.PanelSub;
                row.style.borderTopWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
                row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
                row.style.borderTopLeftRadius = row.style.borderTopRightRadius = row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 8f;
                row.RegisterCallback<PointerDownEvent>(_ => SelectDeploymentTask(_deployDefs[idx].Item2));

                var radio = new VisualElement { name = "DeploymentRadio_" + idx };
                radio.style.width = 21.6f;
                radio.style.height = 21.6f;
                radio.style.flexShrink = 0f;
                radio.style.borderTopWidth = radio.style.borderBottomWidth = radio.style.borderLeftWidth = radio.style.borderRightWidth = 1.6f;
                radio.style.borderTopColor = radio.style.borderBottomColor = radio.style.borderLeftColor = radio.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
                radio.style.borderTopLeftRadius = radio.style.borderTopRightRadius = radio.style.borderBottomLeftRadius = radio.style.borderBottomRightRadius = 10.8f;
                radio.style.marginRight = 14.4f;
                row.Add(radio);

                var name = MkLabel(_deployDefs[idx].Item1, 13f, GitHubDark.TextMain);
                name.style.flexGrow = 1f;
                row.Add(name);

                var count = MkLabel("0명 활성", 12f, GitHubDark.TextSub);
                count.name = "DeploymentCount_" + idx;
                count.style.minWidth = 77.2f;
                count.style.height = 28.6f;
                count.style.backgroundColor = new StyleColor(GitHubDark.BgBase);
                count.style.borderTopWidth = count.style.borderBottomWidth = count.style.borderLeftWidth = count.style.borderRightWidth = 1f;
                count.style.borderTopColor = count.style.borderBottomColor = count.style.borderLeftColor = count.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
                count.style.borderTopLeftRadius = count.style.borderTopRightRadius = count.style.borderBottomLeftRadius = count.style.borderBottomRightRadius = 4f;
                count.style.unityTextAlign = TextAnchor.MiddleCenter;
                row.Add(count);

                _deploymentCards.Add(row);
                _deploymentCounts.Add(count);
                list.Add(row);
            }
            parent.Add(list);

            _applyDeploymentButton = new Button(ApplyPendingDeployment)
            {
                name = "ApplyDeploymentButton",
                text = "선택 부대 배치 적용"
            };
            // [Figma DeploymentFooter] ApplyButton 432×53.6, 상단 gap 14.4
            _applyDeploymentButton.style.height = 53.6f;
            _applyDeploymentButton.style.marginTop = 14.4f;
            _applyDeploymentButton.style.flexShrink = 0f;
            StyleButton(_applyDeploymentButton, true);
            parent.Add(_applyDeploymentButton);
            RefreshDeploymentOptions();
        }

        private void SelectDeploymentTask(GuardTaskSystem.GuardTask task)
        {
            _pendingTask = task;
            RefreshDeploymentOptions();
        }

        private void ApplyPendingDeployment()
        {
            if (_selected == null) return;
            var gts = GuardTaskSystem.Instance;
            if (gts == null) { Debug.LogWarning("[SoldierMgmt] GuardTaskSystem 없음"); return; }
            gts.AssignTask(_selected, _pendingTask);
            Debug.Log($"[SoldierMgmt] {_selected.GuardName} → {_pendingTask}");
            RefreshAll();
        }

        private void RefreshDeploymentOptions()
        {
            var guards = GuardManager.Instance != null ? GuardManager.Instance.GetAllPlayerGuards() : null;
            var taskSystem = GuardTaskSystem.Instance;
            for (int i = 0; i < _deployDefs.Count && i < _deploymentCards.Count; i++)
            {
                GuardTaskSystem.GuardTask task = _deployDefs[i].Item2;
                int assigned = 0;
                if (guards != null && taskSystem != null)
                {
                    foreach (var guard in guards)
                        if (guard != null && guard.IsRecruited && taskSystem.GetTask(guard) == task)
                            assigned++;
                }
                _deploymentCounts[i].text = $"{assigned}명 활성";
                bool selected = task == _pendingTask;
                VisualElement card = _deploymentCards[i];
                Color border = selected ? GitHubDark.Accent : GitHubDark.Stroke;
                card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(border);
                card.style.backgroundColor = selected ? new Color(0x1C / 255f, 0x2B / 255f, 0x3A / 255f, 1f) : GitHubDark.PanelSub;
                var radio = card.Q("DeploymentRadio_" + i);
                if (radio != null)
                {
                    radio.style.borderTopColor = radio.style.borderBottomColor = radio.style.borderLeftColor = radio.style.borderRightColor =
                        new StyleColor(selected ? GitHubDark.Accent : GitHubDark.Stroke);
                    radio.style.backgroundColor = new StyleColor(selected ? GitHubDark.Accent : new Color(0f, 0f, 0f, 0f));
                }
            }
            if (_applyDeploymentButton != null)
                _applyDeploymentButton.SetEnabled(_selected != null && taskSystem != null);
        }

        // ===== 생명주기 =====
        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            CenterSelf();
            StartRefresh();
            RefreshAll();
            if (root != null)
            {
                _geometryRoot = root;
                root.RegisterCallback<GeometryChangedEvent>(OnCanvasGeometryChanged);
                ApplyCompositionBounds();
            }
        }

        private void OnCanvasGeometryChanged(GeometryChangedEvent evt)
        {
            if (evt.target == _geometryRoot && IsOpen)
                ApplyCompositionBounds();
        }

        public override void Hide()
        {
            if (_geometryRoot != null)
                _geometryRoot.UnregisterCallback<GeometryChangedEvent>(OnCanvasGeometryChanged);
            _geometryRoot = null;
            base.Hide();
            StopRefresh();
        }

        private void CenterSelf()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float pw = root.resolvedStyle.width;
            float ph = root.resolvedStyle.height;
            if (pw > 0f) style.left = (pw - resolvedStyle.width) * 0.5f;
            if (ph > 0f) style.top = (ph - resolvedStyle.height) * 0.5f;
        }

        private void StartRefresh()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() => { if (IsOpen) RefreshAll(); }).Every(RefreshMs);
        }

        private void StopRefresh()
        {
            if (_refreshTask != null) { _refreshTask.Pause(); _refreshTask = null; }
        }

        // ===== 갱신 =====
        private void SelectFilter(FilterCat cat)
        {
            _filter = cat;
            RefreshFilterTabs();
            RefreshAll();
        }

        private void RefreshFilterTabs()
        {
            for (int i = 0; i < _filterTabs.Count; i++)
            {
                bool active = (FilterCat)i == _filter;
                var b = _filterTabs[i];
                b.style.backgroundColor = active ? GitHubDark.Accent : GitHubDark.PanelSub;
                b.style.color = active ? GitHubDark.BgBase : GitHubDark.TextMain;
            }
        }

        private void SelectGuard(GuardPlaceholder g)
        {
            _selected = g;
            _pendingTask = GuardTaskSystem.Instance != null && g != null
                ? GuardTaskSystem.Instance.GetTask(g)
                : GuardTaskSystem.GuardTask.None;
            RefreshDetail();
            RefreshDeploymentOptions();
        }

        private void RefreshAll()
        {
            var gm = GuardManager.Instance;
            if (gm == null)
            {
                _guards.Clear();
                _listScroll.Clear();
                _listScroll.Add(MkLabel("병사 시스템 없음", 14f, GitHubDark.TextSub));
                _selected = null;
                RefreshDetail();
                RefreshSummary();
                RefreshDeploymentOptions();
                return;
            }
            _guards.Clear();
            _guards.AddRange(gm.GetAllPlayerGuards());

            // 필터
            var shown = new List<GuardPlaceholder>();
            foreach (var g in _guards)
            {
                if (g == null || !g.IsRecruited) continue;
                if (_filter == FilterCat.Combat && g.Role != GuardRole.Soldier) continue;
                if (_filter == FilterCat.Mercenary && g.Role == GuardRole.Soldier) continue;
                shown.Add(g);
            }

            _listScroll.Clear();
            foreach (var g in shown)
                _listScroll.Add(BuildGuardCard(g));

            // 선택 유지
            if (_selected != null && _selected.IsRecruited) RefreshDetail();
            else { _selected = null; RefreshDetail(); }

            RefreshSummary();
            RefreshDeploymentOptions();
        }

        private void RefreshSummary()
        {
            var gm = GuardManager.Instance;
            var guards = gm != null ? gm.GetAllPlayerGuards() : null;
            // Contract: recruited guards / all non-null registered guards; assigned counts recruited guards with a non-None task.
            int totalRegistered = 0;
            int recruited = 0;
            int assigned = 0;
            var taskSystem = GuardTaskSystem.Instance;

            if (guards != null)
            {
                foreach (var guard in guards)
                {
                    if (guard == null) continue;
                    totalRegistered++;
                    if (!guard.IsRecruited) continue;
                    recruited++;
                    if (taskSystem != null && taskSystem.GetTask(guard) != GuardTaskSystem.GuardTask.None)
                        assigned++;
                }
            }

            _summaryLabel.text = $"병사 {recruited}/{totalRegistered}명 · 임무 배치 {assigned}명";
        }

        private VisualElement BuildGuardCard(GuardPlaceholder g)
        {
            // [Figma SoldierSlot] 432×86.4 고정, pad 14.4, gap 9.6 — TierStrip 좌측 세로 4.8×86.4(중립 스트로크, 등급 데이터 미존재),
            // AvatarFrame 52.8 원형, InfoGroup gap 4.8(이름 15/직책 12), 우측 Lv/임무.
            var card = new VisualElement { name = "SoldierSlot" };
            card.style.height = 86.4f;
            card.style.minHeight = 86.4f;
            card.style.flexShrink = 0f;
            card.style.marginBottom = 9.6f;
            card.style.paddingTop = card.style.paddingBottom = 14.4f;
            card.style.paddingLeft = card.style.paddingRight = 14.4f;
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 1f;
            Color cardStroke = g == _selected ? GitHubDark.Accent : GitHubDark.Stroke;
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(cardStroke);
            card.style.backgroundColor = g == _selected ? new Color(0x1C / 255f, 0x2B / 255f, 0x3A / 255f, 1f) : GitHubDark.PanelSub;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8f;
            card.RegisterCallback<PointerDownEvent>(_ => SelectGuard(g));

            var tierStrip = new VisualElement { name = "SoldierTierStrip" };
            tierStrip.style.position = Position.Absolute;
            tierStrip.style.left = 0f;
            tierStrip.style.top = 0f;
            tierStrip.style.width = 4.8f;
            tierStrip.style.height = 86.4f;
            tierStrip.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
            tierStrip.pickingMode = PickingMode.Ignore;
            card.Add(tierStrip);

            // 아바타 (원형 — 이름 첫 글자) [Figma AvatarFrame 52.8×52.8]
            var avatar = new Label((g.GuardName.Length > 0 ? g.GuardName[0].ToString() : "?"));
            avatar.style.width = 52.8f; avatar.style.height = 52.8f;
            avatar.style.backgroundColor = GitHubDark.Accent;
            avatar.style.color = GitHubDark.BgBase;
            avatar.style.unityFontStyleAndWeight = FontStyle.Bold;
            avatar.style.unityTextAlign = TextAnchor.MiddleCenter;
            avatar.style.fontSize = 22f;
            avatar.style.borderTopLeftRadius = avatar.style.borderTopRightRadius = avatar.style.borderBottomLeftRadius = avatar.style.borderBottomRightRadius = 26.4f;
            avatar.style.marginRight = 14.4f;
            avatar.style.flexShrink = 0f;
            card.Add(avatar);

            var info = new VisualElement();
            info.style.flexDirection = FlexDirection.Column;
            info.style.flexGrow = 1f;
            var name = MkLabel(g.GuardName, 15f, GitHubDark.TextMain);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            info.Add(name);
            var role = MkLabel(g.JobTitle ?? "병사", 12f, GitHubDark.TextSub);
            role.style.marginTop = 4.8f;
            info.Add(role);
            card.Add(info);

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Column;
            right.style.alignItems = Align.FlexEnd;
            var lv = MkLabel($"Lv.{g.Level}", 13f, GitHubDark.Accent);
            right.Add(lv);
            var task = GetTaskName(g);
            var st = MkLabel(task, 11f, GitHubDark.TextSub);
            st.style.unityTextAlign = TextAnchor.MiddleRight;
            right.Add(st);
            card.Add(right);

            return card;
        }

        private static string GetTaskName(GuardPlaceholder g)
        {
            var gts = GuardTaskSystem.Instance;
            if (gts == null) return "대기";
            var t = gts.GetTask(g);
            switch (t)
            {
                case GuardTaskSystem.GuardTask.Attack: return "전투";
                case GuardTaskSystem.GuardTask.Defend: return "수비";
                case GuardTaskSystem.GuardTask.Hunt: return "사냥";
                case GuardTaskSystem.GuardTask.Gather: return "채집";
                case GuardTaskSystem.GuardTask.Farm: return "농사";
                case GuardTaskSystem.GuardTask.Envoy: return "특사";
                default: return "대기";
            }
        }

        private void RefreshDetail()
        {
            if (_selected == null)
            {
                _dName.text = "병사를 선택하세요";
                _dLevel.text = "";
                _dNationBadge.text = "-";
                _dTraitText.text = "-";
                _dImageHint.text = "병사를 선택하세요";
                for (int i = 0; i < 4; i++) { _statBars[i].style.width = new Length(0f, LengthUnit.Percent); _statVals[i].text = "-"; }
                return;
            }
            var g = _selected;
            // [Figma HeroHeader] 이름 + 국가 배지(국가색 유지) + 서브 "Lv. n · 직책" — 실존 데이터만
            _dName.text = g.GuardName;
            _dLevel.text = $"Lv.{g.Level}  ·  {g.JobTitle ?? "병사"}";
            _dNationBadge.text = string.IsNullOrEmpty(g.Nation) ? "-" : g.Nation;
            _dTraitText.text = $"{g.JobTitle ?? "병사"}는 임무 배치 시 영지 생산과 국경 전투력에 기여합니다. 현재 임무: {GetTaskName(g)} · 현재체력 {Mathf.Round(g.CurrentHP)}/{Mathf.Round(g.MaxHP)}.";
            _dImageHint.text = "3D 미리보기";

            int atk = g.GetAttack();
            int def = g.GetDefense();
            float hp = g.MaxHP;
            int agi = g.GetAgility();

            SetStat(0, atk, 300);
            SetStat(1, def, 300);
            SetStat(2, hp, 500);
            SetStat(3, agi, 100);
        }

        private void SetStat(int idx, float val, float max)
        {
            float pct = Mathf.Clamp01(max > 0 ? val / max : 0f);
            _statBars[idx].style.width = new Length(pct * 100f, LengthUnit.Percent);
            _statVals[idx].text = $"{Mathf.RoundToInt(val)} / {Mathf.RoundToInt(max)}";   // [Figma] "284 / 400" 형식
        }

        /// <summary>[Figma PanelHeader] 제목(20) + 영문 서브타이틀(11, gap12) — 3패널 공통.</summary>
        private static VisualElement MkPanelHeader(string title, string subtitle)
        {
            var h = new VisualElement { name = "PanelHeader" };
            h.style.flexDirection = FlexDirection.Row;
            h.style.alignItems = Align.Center;
            h.style.paddingBottom = 14.4f;
            h.style.height = 52.8f;
            h.style.minHeight = 52.8f;
            h.Add(MkLabel(title, 20f, GitHubDark.TextMain));
            var s = MkLabel(subtitle, 11f, GitHubDark.TextSub);
            s.style.marginLeft = 12f;
            s.style.marginTop = 5f;
            h.Add(s);
            return h;
        }

        private static VisualElement AddSep()
        {
            var s = new VisualElement();
            s.style.height = 1f;
            s.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
            s.style.marginTop = 8f;
            s.style.marginBottom = 8f;
            return s;
        }

        // ===== 창 크롬 =====
        private void ApplyGitHubDarkWindowStyle()
        {
            style.backgroundColor = GitHubDark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 8f;
            style.color = GitHubDark.TextMain;

            var tb = this.Q("TitleBar");
            if (tb != null)
            {
                tb.style.backgroundColor = GitHubDark.PanelSub;
                tb.style.borderTopLeftRadius = 8f;
                tb.style.borderTopRightRadius = 8f;
                tb.style.borderBottomWidth = 1f;
                tb.style.borderBottomColor = GitHubDark.Stroke;
            }
            if (_titleLabel != null) _titleLabel.style.color = GitHubDark.TextMain;
            var close = this.Q<Button>("CloseButton");
            if (close != null)
            {
                close.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                close.style.backgroundColor = GitHubDark.PanelSub;
                close.style.borderTopWidth = close.style.borderBottomWidth = close.style.borderLeftWidth = close.style.borderRightWidth = 0f;
                close.style.color = GitHubDark.TextMain;
            }
        }
    }
}
