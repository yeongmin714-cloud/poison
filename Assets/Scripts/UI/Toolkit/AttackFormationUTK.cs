using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;
using ProjectName.Core;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// 공격대 배치 창 — 피그마 "공격조 전투 배치"(204:1963, 1920×1080) 구현.
    /// 좌: 공격조 명부(432×984 — 필터/대원 카드/요약·진행률)
    /// 우: 전투 배치 계획(1080×984 — 적 방향 스트립 / 5열A~E × 4행(1=전열,2=중열,3=지원,4=후열) 20칸 배치도 /
    ///     선택 대원 배치 정보 / 계획 저장(취소 + 전투 배치 적용))
    /// 조작: 명부에서 대원 선택 → 빈 칸 클릭 = 배치(점유 칸 클릭 = 그 대원 선택) / 배치 해제 = 선택 대원 칸 해제 /
    ///       배치 초기화 = 전체 해제 / 전투 배치 적용 = 편성 확정 + 칸 위치 보행 명령.
    /// squad(공격대) = Attack 역할 배정 병사 + (없으면) 포섭 아군 전원 — 실측 데이터만 표시.
    /// </summary>
    public class AttackFormationUTK : UTKWindowBase
    {
        private static AttackFormationUTK _instance;

        // [Figma 204:1963] 컴포지션 — 좌 명부 (168,48,480,984) + 우 계획 (672,48,1080,984)
        public static readonly Rect CompositionBounds = new Rect(168f, 48f, 1584f, 984f);
        public static readonly Rect RosterBounds = new Rect(0f, 0f, 480f, 984f);
        public static readonly Rect PlanBounds = new Rect(504f, 0f, 1080f, 984f);

        private const int GridCols = AttackFormationSystem.Columns;   // 5
        private const int GridRows = AttackFormationSystem.Rows;      // 4

        private static class Dark
        {
            public static readonly Color BgBase = Hex(0x0B0E14);
            public static readonly Color Panel = Hex(0x161B22);
            public static readonly Color PanelSub = Hex(0x21262D);
            public static readonly Color Accent = Hex(0x58A6FF);
            public static readonly Color Gold = Hex(0xE3B341);
            public static readonly Color TextMain = Hex(0xF0F6FC);
            public static readonly Color TextSub = Hex(0x8B949E);
            public static readonly Color Stroke = Hex(0x2E343D);
            public static readonly Color StatusGreen = Hex(0x39D353);
            public static readonly Color StatusOrange = Hex(0xFF9E2C);
            public static readonly Color CardFill = new Color(1f, 1f, 1f, 0.02f);
            public static readonly Color CardFillSelected = new Color(1f, 1f, 1f, 0.08f);
            public static readonly Color SelectedFill = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.12f);
            public static readonly Color GlassStroke = new Color32(0x30, 0x36, 0x3D, 0x80);   // #30363D@0.5

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)((rgb) & 0xFF), 0xFF);
        }

        private enum FilterCat { All, Assigned, Unassigned }

        private readonly List<GuardPlaceholder> _squad = new();
        private readonly List<VisualElement> _rosterCards = new();
        private readonly List<Button> _filterTabs = new();
        private readonly Dictionary<string, Label> _cellNameLabels = new();
        private readonly Dictionary<string, Label> _cellRoleLabels = new();
        private readonly Dictionary<string, VisualElement> _cellTags = new();
        private readonly Dictionary<string, VisualElement> _cellRoots = new();

        private Label _summaryLabel;
        private Label _progressLabel;
        private VisualElement _progressFill;
        private Label _deployStatLabel;
        private Label _reserveStatLabel;
        private Label _selectedInfoLabel;    // "01 · 베테랑 돌격병 · Lv.45"
        private Label _selectedCellLabel;    // "C1 · 전열 중앙" / "미배치"
        private Label _selectedHintLabel;
        private Label _applyStatusLabel;
        private FilterCat _filter = FilterCat.All;
        private GuardPlaceholder _selected;
        private IVisualElementScheduledItem _refreshTask;

        private const long RefreshMs = 250;

        private AttackFormationUTK() : base("⚔️ 공격대 배치", new Vector2(CompositionBounds.width, CompositionBounds.height))
        {
            SetChrome(UTKWindowChrome.Frameless);
            ApplyGitHubDarkStyle();

            _content.style.flexDirection = FlexDirection.Row;

            var roster = CreatePanelRoot("AttackRosterPanel", RosterBounds);
            BuildRosterPanel(roster);
            var plan = CreatePanelRoot("AttackPlanPanel", PlanBounds);
            BuildPlanPanel(plan);

            CreateCloseButton(plan);

            ApplyCanvasBounds();
        }

        // ===== 공개 API =====
        public static AttackFormationUTK Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // SoldierManagementUTK와 동일 — Open 시 Ensure.
        }

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new AttackFormationUTK();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && _instance.parent == null)
                root.Add(_instance);
            _instance.style.display = DisplayStyle.None;
            Debug.Log("[AttackFormationUTK] 인스턴스 생성");
        }

        public static void Open()
        {
            Ensure();
            _instance.Show();
            _instance.RefreshAll();
            _instance.StartRefresh();
        }

        public static void Toggle()
        {
            Ensure();
            if (_instance.IsOpen) _instance.Close();
            else Open();
        }

        public override void Hide()
        {
            if (_geometryRoot != null)
                _geometryRoot.UnregisterCallback<GeometryChangedEvent>(OnCanvasGeometryChanged);
            _geometryRoot = null;
            StopRefresh();
            base.Hide();
        }

        // ===== 좌: 공격조 명부 =====
        private void BuildRosterPanel(VisualElement parent)
        {
            var header = MkPanelHeader("공격조 명부", "ATTACK ROSTER");
            parent.Add(header);

            // [Figma 명부 필터 432×38] 전체 N / 배치 N / 미배치 N — 140×38 ×3
            var filterRow = new VisualElement { name = "RosterFilterRow" };
            filterRow.style.flexDirection = FlexDirection.Row;
            filterRow.style.marginBottom = 14.4f;
            filterRow.style.flexShrink = 0f;
            parent.Add(filterRow);
            string[] tabNames = { "전체", "배치", "미배치" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var tab = new Button(() => { _filter = (FilterCat)idx; RefreshAll(); })
                {
                    name = "RosterFilter_" + idx,
                    text = tabNames[i]
                };
                tab.style.height = 38f;
                tab.style.flexGrow = 1f;
                tab.style.flexShrink = 0f;
                if (i < 2) tab.style.marginRight = 6f;
                tab.style.fontSize = UTKTheme.FontBody;
                tab.style.unityFontStyleAndWeight = FontStyle.Bold;
                StyleFilterTab(tab, i == (int)_filter);
                filterRow.Add(tab);
                _filterTabs.Add(tab);
            }

            // 대원 목록 [Figma 대원 목록 432×634]
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "AttackRosterList" };
            scroll.style.flexGrow = 1f;
            scroll.style.minHeight = 0f;
            scroll.style.marginBottom = 9.6f;
            parent.Add(scroll);
            _rosterScroll = scroll;

            // 명부 사용 안내 [Figma 432×64]
            var guide = MkLabel("대원을 선택한 뒤 오른쪽 빈 칸을 눌러 배치합니다.\n행 1=전열(선봉) · 2=중열 · 3=지원 · 4=후열(후방 공격).", UTKTheme.FontBadge, Dark.TextSub);
            guide.style.whiteSpace = WhiteSpace.Normal;
            guide.style.flexShrink = 0f;
            guide.style.backgroundColor = new StyleColor(Dark.CardFill);
            guide.style.borderTopWidth = guide.style.borderBottomWidth = guide.style.borderLeftWidth = guide.style.borderRightWidth = 1.2f;
            guide.style.borderTopColor = guide.style.borderBottomColor = guide.style.borderLeftColor = guide.style.borderRightColor = new StyleColor(Dark.GlassStroke);
            guide.style.borderTopLeftRadius = guide.style.borderTopRightRadius = guide.style.borderBottomLeftRadius = guide.style.borderBottomRightRadius = 9.6f;
            guide.style.paddingTop = guide.style.paddingBottom = 9.6f;
            guide.style.paddingLeft = guide.style.paddingRight = 12f;
            parent.Add(guide);

            // 명부 요약 + 진행률 [Figma 명부 요약 432×54 + 진행률 309×5]
            var summary = new VisualElement { name = "RosterSummary" };
            summary.style.flexDirection = FlexDirection.Row;
            summary.style.alignItems = Align.Center;
            summary.style.marginTop = 12f;
            summary.style.flexShrink = 0f;
            parent.Add(summary);
            var sumLabel = MkLabel("배치 완료 / 공격조 총원", UTKTheme.FontBody, Dark.TextSub);
            sumLabel.style.flexGrow = 1f;
            summary.Add(sumLabel);
            _summaryLabel = MkLabel("0 / 0 명", UTKTheme.FontRowValue, Dark.TextMain);
            _summaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            summary.Add(_summaryLabel);

            var progress = new VisualElement { name = "RosterProgress" };
            progress.style.height = 5f;
            progress.style.marginTop = 9.6f;
            progress.style.flexShrink = 0f;
            progress.style.backgroundColor = new StyleColor(Dark.PanelSub);
            progress.style.borderTopLeftRadius = progress.style.borderTopRightRadius = progress.style.borderBottomLeftRadius = progress.style.borderBottomRightRadius = 2.5f;
            parent.Add(progress);
            _progressFill = new VisualElement { name = "RosterProgressFill" };
            _progressFill.style.height = 5f;
            _progressFill.style.width = 0f;
            _progressFill.style.backgroundColor = new StyleColor(Dark.Accent);
            _progressFill.style.borderTopLeftRadius = _progressFill.style.borderTopRightRadius = _progressFill.style.borderBottomLeftRadius = _progressFill.style.borderBottomRightRadius = 2.5f;
            progress.Add(_progressFill);
        }

        private void StyleFilterTab(Button tab, bool active)
        {
            tab.style.backgroundColor = new StyleColor(active ? Dark.Accent : Dark.PanelSub);
            tab.style.color = new StyleColor(active ? Dark.BgBase : Dark.TextSub);
            tab.style.borderTopWidth = tab.style.borderBottomWidth = tab.style.borderLeftWidth = tab.style.borderRightWidth = 0f;
            tab.style.borderTopLeftRadius = tab.style.borderTopRightRadius = tab.style.borderBottomLeftRadius = tab.style.borderBottomRightRadius = 6f;
        }

        private VisualElement BuildRosterCard(GuardPlaceholder g, int index)
        {
            string cell = AttackFormationSystem.GetCell(g);
            bool assigned = !string.IsNullOrEmpty(cell);
            bool selected = g == _selected;

            var card = new VisualElement { name = "AttackRosterCard_" + index };
            card.style.height = 82f;
            card.style.minHeight = 82f;
            card.style.flexShrink = 0f;
            card.style.marginBottom = 9.6f;
            card.style.paddingTop = card.style.paddingBottom = 14.4f;
            card.style.paddingLeft = card.style.paddingRight = 14.4f;
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            float strokeW = selected ? 1.8f : 1.2f;
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = strokeW;
            Color stroke = selected ? Dark.Accent : Dark.GlassStroke;
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(stroke);
            card.style.backgroundColor = new StyleColor(selected ? Dark.CardFillSelected : Dark.CardFill);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 9.6f;
            card.RegisterCallback<PointerDownEvent>(_ => { _selected = g; RefreshAll(); });

            // [Figma] TierStrip 4×82
            var tier = new VisualElement { name = "AttackTierStrip" };
            tier.style.position = Position.Absolute;
            tier.style.left = 0f; tier.style.top = 0f;
            tier.style.width = 4f; tier.style.height = 82f;
            tier.style.backgroundColor = new StyleColor(Dark.Stroke);
            tier.pickingMode = PickingMode.Ignore;
            card.Add(tier);

            // [Figma] 번호 "01" fs17 fw700
            var num = MkLabel((index + 1).ToString("00"), 17f, Dark.TextSub);
            num.style.unityFontStyleAndWeight = FontStyle.Bold;
            num.style.width = 30f;
            num.style.flexShrink = 0f;
            card.Add(num);

            // [Figma] 아바타 48×48 — 사람 아이콘
            var avatar = new VisualElement { name = "AttackAvatar" };
            avatar.style.width = 48f; avatar.style.height = 48f;
            avatar.style.backgroundColor = new StyleColor(Dark.PanelSub);
            avatar.style.borderTopWidth = avatar.style.borderBottomWidth = avatar.style.borderLeftWidth = avatar.style.borderRightWidth = 1.2f;
            avatar.style.borderTopColor = avatar.style.borderBottomColor = avatar.style.borderLeftColor = avatar.style.borderRightColor = new StyleColor(Dark.GlassStroke);
            avatar.style.borderTopLeftRadius = avatar.style.borderTopRightRadius = avatar.style.borderBottomLeftRadius = avatar.style.borderBottomRightRadius = 24f;
            avatar.style.marginRight = 12f;
            avatar.style.flexShrink = 0f;
            avatar.style.justifyContent = Justify.Center;
            avatar.style.alignItems = Align.Center;
            var icon = new VisualElement();
            var avatarTex = Resources.Load<Texture2D>("UI/SoldierAvatarIcon");
            if (avatarTex != null) icon.style.backgroundImage = UTKTextureSafe.ToBackground(avatarTex);
            icon.style.width = 28f; icon.style.height = 28f;
            avatar.Add(icon);
            card.Add(avatar);

            // 정보 — 이름 fs16 fw700 + 직책 fs13 fw400 (+침대/칸 태그)
            var info = new VisualElement();
            info.style.flexGrow = 1f;
            var name = MkLabel(g.GuardName, 16f, Dark.TextMain);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            info.Add(name);
            string cellTag = assigned ? $" · {cell} 배치" : "";
            var role = MkLabel((g.JobTitle ?? "병사") + cellTag, UTKTheme.FontBody, Dark.TextSub);
            role.style.marginTop = 4.8f;
            info.Add(role);
            card.Add(info);

            // 우측 배치 상태 — Lv + 칸 배지(배치=그린/미배치=보조)
            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Column;
            right.style.alignItems = Align.FlexEnd;
            var lv = MkLabel($"Lv.{g.Level}", UTKTheme.FontBody, Dark.Accent);
            lv.style.unityFontStyleAndWeight = FontStyle.Bold;
            right.Add(lv);
            var badge = MkLabel(assigned ? $"{cell} 배치" : "미배치", UTKTheme.FontBadge,
                assigned ? Dark.StatusGreen : Dark.TextSub);
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.marginTop = 4.8f;
            badge.style.paddingLeft = badge.style.paddingRight = 6f;
            badge.style.paddingTop = badge.style.paddingBottom = 2f;
            badge.style.backgroundColor = new StyleColor(Dark.BgBase);
            badge.style.borderTopWidth = badge.style.borderBottomWidth = badge.style.borderLeftWidth = badge.style.borderRightWidth = 1f;
            badge.style.borderTopColor = badge.style.borderBottomColor = badge.style.borderLeftColor = badge.style.borderRightColor = new StyleColor(Dark.Stroke);
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 4f;
            right.Add(badge);
            card.Add(right);

            return card;
        }

        // ===== 우: 전투 배치 계획 =====
        private void BuildPlanPanel(VisualElement parent)
        {
            var header = MkPanelHeader("공격조 전투 배치", "BATTLE DEPLOYMENT PLAN");
            parent.Add(header);

            // 계획 도구 [Figma 계획 도구 1032×42] — 통계 3종 + 우측 초기화
            var tools = new VisualElement { name = "PlanToolsRow" };
            tools.style.flexDirection = FlexDirection.Row;
            tools.style.alignItems = Align.Center;
            tools.style.marginBottom = 14.4f;
            tools.style.flexShrink = 0f;
            parent.Add(tools);

            _deployStatLabel = MkLabel("배치 0명", UTKTheme.FontBody, Dark.TextMain);
            _deployStatLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _deployStatLabel.style.marginRight = 14.4f;
            tools.Add(_deployStatLabel);
            _reserveStatLabel = MkLabel("미배치 0명", UTKTheme.FontBody, Dark.TextSub);
            _reserveStatLabel.style.marginRight = 14.4f;
            tools.Add(_reserveStatLabel);
            var cellStat = MkLabel($"사용 0 / {GridCols * GridRows}칸", UTKTheme.FontBody, Dark.TextSub);
            cellStat.style.flexGrow = 1f;
            tools.Add(cellStat);

            var resetChip = MkLabel("↺ 배치 초기화", UTKTheme.FontBody, Dark.TextSub);
            resetChip.name = "FormationResetChip";
            resetChip.style.unityFontStyleAndWeight = FontStyle.Bold;
            resetChip.style.paddingLeft = resetChip.style.paddingRight = 9.6f;
            resetChip.style.paddingTop = resetChip.style.paddingBottom = 4.8f;
            resetChip.style.backgroundColor = new StyleColor(Dark.PanelSub);
            resetChip.style.borderTopWidth = resetChip.style.borderBottomWidth = resetChip.style.borderLeftWidth = resetChip.style.borderRightWidth = 1.2f;
            resetChip.style.borderTopColor = resetChip.style.borderBottomColor = resetChip.style.borderLeftColor = resetChip.style.borderRightColor = new StyleColor(Dark.GlassStroke);
            resetChip.style.borderTopLeftRadius = resetChip.style.borderTopRightRadius = resetChip.style.borderBottomLeftRadius = resetChip.style.borderBottomRightRadius = 6f;
            resetChip.RegisterCallback<PointerDownEvent>(_ =>
            {
                AttackFormationSystem.ClearAll();
                RefreshAll();
            });
            tools.Add(resetChip);

            // 적 방향 [Figma 적 방향 1032×32] — 배치도 상단 = 적 진영
            var enemyStrip = new VisualElement { name = "EnemyDirectionStrip" };
            enemyStrip.style.height = 32f;
            enemyStrip.style.flexShrink = 0f;
            enemyStrip.style.alignItems = Align.Center;
            enemyStrip.style.justifyContent = Justify.Center;
            enemyStrip.style.flexDirection = FlexDirection.Row;
            enemyStrip.style.backgroundColor = new StyleColor(Dark.CardFill);
            enemyStrip.style.borderTopWidth = enemyStrip.style.borderBottomWidth = enemyStrip.style.borderLeftWidth = enemyStrip.style.borderRightWidth = 1.2f;
            enemyStrip.style.borderTopColor = enemyStrip.style.borderBottomColor = enemyStrip.style.borderLeftColor = enemyStrip.style.borderRightColor = new StyleColor(Dark.GlassStroke);
            enemyStrip.style.borderTopLeftRadius = enemyStrip.style.borderTopRightRadius = enemyStrip.style.borderBottomLeftRadius = enemyStrip.style.borderBottomRightRadius = 6f;
            enemyStrip.style.marginBottom = 9.6f;
            var enemyText = MkLabel("▲ 적 진영 · 공격 방향 (전열이 이쪽)", UTKTheme.FontBadge, Dark.StatusOrange);
            enemyText.style.unityFontStyleAndWeight = FontStyle.Bold;
            enemyStrip.Add(enemyText);
            parent.Add(enemyStrip);

            // 전투 배치도 — 열 헤더(A~E) + 4행 × 5열 그리드 [Figma 전투 배치도 1032×534]
            var gridWrap = new VisualElement { name = "FormationGrid" };
            gridWrap.style.flexGrow = 1f;
            gridWrap.style.minHeight = 0f;
            gridWrap.style.marginBottom = 9.6f;
            parent.Add(gridWrap);

            var colHeader = new VisualElement { name = "FormationColHeader" };
            colHeader.style.flexDirection = FlexDirection.Row;
            colHeader.style.height = 24f;
            colHeader.style.flexShrink = 0f;
            colHeader.style.marginBottom = 4.8f;
            gridWrap.Add(colHeader);
            var rowGutter = new VisualElement();   // 행 라벨 여백(46)
            rowGutter.style.width = 40f;
            rowGutter.style.flexShrink = 0f;
            colHeader.Add(rowGutter);
            for (int c = 0; c < GridCols; c++)
            {
                var colLabel = MkLabel(AttackFormationSystem.ColumnNames[c], UTKTheme.FontBody, Dark.TextSub);
                colLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                colLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                colLabel.style.flexGrow = 1f;
                colLabel.style.flexShrink = 0f;
                if (c < GridCols - 1) colLabel.style.marginRight = 6f;
                colHeader.Add(colLabel);
            }

            var grid = new VisualElement { name = "FormationGridBody" };
            grid.style.flexGrow = 1f;
            gridWrap.Add(grid);

            for (int r = 1; r <= GridRows; r++)
            {
                var rowEl = new VisualElement { name = "FormationRow_" + r };
                rowEl.style.flexDirection = FlexDirection.Row;
                rowEl.style.flexGrow = 1f;
                rowEl.style.flexShrink = 0f;
                rowEl.style.marginBottom = 6f;
                grid.Add(rowEl);

                // 행 라벨 — "1" + 전열(fs10)
                var rowHead = new VisualElement();
                rowHead.style.width = 40f;
                rowHead.style.flexShrink = 0f;
                rowHead.style.justifyContent = Justify.Center;
                rowEl.Add(rowHead);
                var rowNum = MkLabel(r.ToString(), 16f, Dark.TextMain);
                rowNum.style.unityFontStyleAndWeight = FontStyle.Bold;
                rowHead.Add(rowNum);
                var rowName = MkLabel(AttackFormationSystem.RowName(r), 10f, Dark.TextSub);
                rowHead.Add(rowName);

                for (int c = 0; c < GridCols; c++)
                {
                    string cell = $"{AttackFormationSystem.ColumnNames[c]}{r}";
                    rowEl.Add(BuildCell(cell, r, c < GridCols - 1));
                }
            }

            // 선택 대원 배치 정보 [Figma 1032×90]
            var selInfo = new VisualElement { name = "SelectedPlacementInfo" };
            selInfo.style.height = 72f;
            selInfo.style.flexShrink = 0f;
            selInfo.style.flexDirection = FlexDirection.Row;
            selInfo.style.alignItems = Align.Center;
            selInfo.style.paddingLeft = selInfo.style.paddingRight = 14.4f;
            selInfo.style.backgroundColor = new StyleColor(Dark.CardFill);
            selInfo.style.borderTopWidth = selInfo.style.borderBottomWidth = selInfo.style.borderLeftWidth = selInfo.style.borderRightWidth = 1.2f;
            selInfo.style.borderTopColor = selInfo.style.borderBottomColor = selInfo.style.borderLeftColor = selInfo.style.borderRightColor = new StyleColor(Dark.GlassStroke);
            selInfo.style.borderTopLeftRadius = selInfo.style.borderTopRightRadius = selInfo.style.borderBottomLeftRadius = selInfo.style.borderBottomRightRadius = 9.6f;
            selInfo.style.marginBottom = 9.6f;
            parent.Add(selInfo);

            var selNumBadge = MkLabel("--", 18f, Dark.Gold);
            selNumBadge.name = "SelectedNumberBadge";
            selNumBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            selNumBadge.style.width = 42f;
            selNumBadge.style.height = 42f;
            selNumBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            selNumBadge.style.backgroundColor = new StyleColor(Dark.PanelSub);
            selNumBadge.style.borderTopWidth = selNumBadge.style.borderBottomWidth = selNumBadge.style.borderLeftWidth = selNumBadge.style.borderRightWidth = 1.2f;
            selNumBadge.style.borderTopColor = selNumBadge.style.borderBottomColor = selNumBadge.style.borderLeftColor = selNumBadge.style.borderRightColor = new StyleColor(Dark.GlassStroke);
            selNumBadge.style.borderTopLeftRadius = selNumBadge.style.borderTopRightRadius = selNumBadge.style.borderBottomLeftRadius = selNumBadge.style.borderBottomRightRadius = 6f;
            selNumBadge.style.marginRight = 14.4f;
            selInfo.Add(selNumBadge);
            _selectedNumberBadge = selNumBadge;

            var selMid = new VisualElement();
            selMid.style.flexGrow = 1f;
            _selectedInfoLabel = MkLabel("대원을 선택하세요", UTKTheme.FontBody, Dark.TextMain);
            _selectedInfoLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            selMid.Add(_selectedInfoLabel);
            _selectedCellLabel = MkLabel("현재 배치: -", UTKTheme.FontRowValue, Dark.Gold);
            _selectedCellLabel.style.marginTop = 4.8f;
            selMid.Add(_selectedCellLabel);
            selInfo.Add(selMid);

            // 배치 해제 칩 [Figma Action 배치 해제]
            var unassignChip = MkLabel("배치 해제", UTKTheme.FontBody, Dark.TextMain);
            unassignChip.name = "UnassignChip";
            unassignChip.style.unityFontStyleAndWeight = FontStyle.Bold;
            unassignChip.style.paddingLeft = unassignChip.style.paddingRight = 14.4f;
            unassignChip.style.height = 42f;
            unassignChip.style.unityTextAlign = TextAnchor.MiddleCenter;
            unassignChip.style.backgroundColor = new StyleColor(Dark.PanelSub);
            unassignChip.style.borderTopWidth = unassignChip.style.borderBottomWidth = unassignChip.style.borderLeftWidth = unassignChip.style.borderRightWidth = 1.2f;
            unassignChip.style.borderTopColor = unassignChip.style.borderBottomColor = unassignChip.style.borderLeftColor = unassignChip.style.borderRightColor = new StyleColor(Dark.GlassStroke);
            unassignChip.style.borderTopLeftRadius = unassignChip.style.borderTopRightRadius = unassignChip.style.borderBottomLeftRadius = unassignChip.style.borderBottomRightRadius = 7.2f;
            unassignChip.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (_selected == null) return;
                AttackFormationSystem.ClearCell(_selected);
                RefreshAll();
            });
            selInfo.Add(unassignChip);

            // 계획 저장 [Figma 계획 저장 1032×63] — 확인 메시지 + 취소/전투 배치 적용
            var saveRow = new VisualElement { name = "FormationSaveRow" };
            saveRow.style.flexDirection = FlexDirection.Row;
            saveRow.style.alignItems = Align.Center;
            saveRow.style.flexShrink = 0f;
            parent.Add(saveRow);

            _applyStatusLabel = MkLabel("미배치 대원은 예비 인원으로 남습니다.", UTKTheme.FontBadge, Dark.TextSub);
            _applyStatusLabel.style.flexGrow = 1f;
            _applyStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            saveRow.Add(_applyStatusLabel);

            var cancelChip = MkLabel("취소", UTKTheme.FontBody, Dark.TextSub);
            cancelChip.style.height = 46f;
            cancelChip.style.width = 84f;
            cancelChip.style.unityTextAlign = TextAnchor.MiddleCenter;
            cancelChip.style.flexShrink = 0f;
            cancelChip.style.marginRight = 9.6f;
            cancelChip.style.backgroundColor = new StyleColor(Dark.PanelSub);
            cancelChip.style.borderTopWidth = cancelChip.style.borderBottomWidth = cancelChip.style.borderLeftWidth = cancelChip.style.borderRightWidth = 1.2f;
            cancelChip.style.borderTopColor = cancelChip.style.borderBottomColor = cancelChip.style.borderLeftColor = cancelChip.style.borderRightColor = new StyleColor(Dark.GlassStroke);
            cancelChip.style.borderTopLeftRadius = cancelChip.style.borderTopRightRadius = cancelChip.style.borderBottomLeftRadius = cancelChip.style.borderBottomRightRadius = 7.2f;
            cancelChip.RegisterCallback<PointerDownEvent>(_ => Close());
            saveRow.Add(cancelChip);

            var applyChip = MkLabel("전투 배치 적용", UTKTheme.FontBody, Dark.BgBase);
            applyChip.name = "ApplyFormationChip";
            applyChip.style.height = 46f;
            applyChip.style.width = 190f;
            applyChip.style.unityTextAlign = TextAnchor.MiddleCenter;
            applyChip.style.unityFontStyleAndWeight = FontStyle.Bold;
            applyChip.style.flexShrink = 0f;
            applyChip.style.backgroundColor = new StyleColor(Dark.Accent);
            applyChip.style.borderTopLeftRadius = applyChip.style.borderTopRightRadius = applyChip.style.borderBottomLeftRadius = applyChip.style.borderBottomRightRadius = 7.2f;
            applyChip.RegisterCallback<PointerDownEvent>(_ => ApplyFormationNow());
            saveRow.Add(applyChip);
        }

        private Label _selectedNumberBadge;

        private VisualElement BuildCell(string cell, int row, bool notLast)
        {
            var cellEl = new VisualElement { name = "FormationCell_" + cell };
            cellEl.style.flexGrow = 1f;
            cellEl.style.flexShrink = 0f;
            cellEl.style.paddingTop = cellEl.style.paddingBottom = 6f;
            cellEl.style.paddingLeft = cellEl.style.paddingRight = 8f;
            cellEl.style.backgroundColor = new StyleColor(Dark.CardFill);
            cellEl.style.borderTopWidth = cellEl.style.borderBottomWidth = cellEl.style.borderLeftWidth = cellEl.style.borderRightWidth = 1.2f;
            cellEl.style.borderTopColor = cellEl.style.borderBottomColor = cellEl.style.borderLeftColor = cellEl.style.borderRightColor = new StyleColor(Dark.GlassStroke);
            cellEl.style.borderTopLeftRadius = cellEl.style.borderTopRightRadius = cellEl.style.borderBottomLeftRadius = cellEl.style.borderBottomRightRadius = 7.2f;
            if (notLast) cellEl.style.marginRight = 6f;
            cellEl.RegisterCallback<PointerDownEvent>(_ => OnCellClicked(cell));
            _cellRoots[cell] = cellEl;

            // 셀 헤더 — 좌표 fs12 fw600
            var head = MkLabel(cell, UTKTheme.FontBadge, Dark.TextSub);
            head.style.unityFontStyleAndWeight = FontStyle.Bold;
            head.style.marginBottom = 6f;
            cellEl.Add(head);

            // 대원 라벨 (점유 시 이름) / 빈 칸 안내
            var nameLabel = MkLabel("+ 배치 가능", UTKTheme.FontBadge, Dark.TextSub);
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            cellEl.Add(nameLabel);
            _cellNameLabels[cell] = nameLabel;

            // 역할 행 (점유 시 직책)
            var roleLabel = MkLabel("", 11f, Dark.TextSub);
            roleLabel.style.marginTop = 4.8f;
            roleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            cellEl.Add(roleLabel);
            _cellRoleLabels[cell] = roleLabel;

            return cellEl;
        }

        private void OnCellClicked(string cell)
        {
            if (_selected == null)
            {
                _applyStatusLabel.text = "먼저 왼쪽 명부에서 대원을 선택하세요.";
                _applyStatusLabel.style.color = new StyleColor(Dark.StatusOrange);
                return;
            }
            // 점유 칸 클릭 = 그 대원 선택(스왑 없음 — 안전)
            foreach (var kvp in AttackFormationSystem.EnumerateCells())
            {
                if (kvp.Value == cell && kvp.Key != _selected)
                {
                    _selected = kvp.Key;
                    RefreshAll();
                    return;
                }
            }
            bool ok = AttackFormationSystem.AssignCell(_selected, cell);
            _applyStatusLabel.text = ok
                ? $"{_selected.GuardName} → {cell} · {AttackFormationSystem.RowName(int.Parse(cell.Substring(1)))} 배치"
                : $"{cell} 칸 배치 실패";
            _applyStatusLabel.style.color = new StyleColor(ok ? Dark.StatusGreen : GitHubDanger());
            RefreshAll();
        }

        private static Color GitHubDanger() => new Color32(0xF8, 0x51, 0x49, 0xFF);

        private void ApplyFormationNow()
        {
            // 전투 기준점 — 실내 = 로비 중앙(문 앞), 월드 = (배치 시스템이 공격 목표로 대체)
            Vector3 anchor = Vector3.zero;
            var room = GameObject.Find("Room");
            if (room != null) anchor = room.transform.position;
            int n = AttackFormationSystem.ApplyFormation(anchor);
            _applyStatusLabel.text = $"전투 배치 적용 — {n}명 편성 위치로 이동 개시. 미배치 대원은 예비 인원.";
            _applyStatusLabel.style.color = new StyleColor(Dark.StatusGreen);
        }

        // ===== 갱신 =====
        private void RefreshAll()
        {
            _squad.Clear();
            var gm = GuardManager.Instance;
            var gts = GuardTaskSystem.Instance;
            if (gm != null)
            {
                foreach (var g in gm.GetAllPlayerGuards())
                {
                    if (g == null || !g.IsRecruited) continue;
                    // 공격대 = Attack 역할 배정 병사 우선 — 없으면 포섭 아군 전원(편성 대상)
                    _squad.Add(g);
                }
            }

            // 필터
            var shown = new List<GuardPlaceholder>();
            foreach (var g in _squad)
            {
                bool assigned = !string.IsNullOrEmpty(AttackFormationSystem.GetCell(g));
                if (_filter == FilterCat.Assigned && !assigned) continue;
                if (_filter == FilterCat.Unassigned && assigned) continue;
                shown.Add(g);
            }

            // 명부 카드
            _rosterScroll.Clear();
            _rosterCards.Clear();
            for (int i = 0; i < shown.Count; i++)
            {
                var card = BuildRosterCard(shown[i], i);
                _rosterScroll.Add(card);
                _rosterCards.Add(card);
            }

            // 필터 탭 스타일 + 요약
            for (int i = 0; i < _filterTabs.Count; i++)
                StyleFilterTab(_filterTabs[i], i == (int)_filter);
            var (assignedCount, totalCount) = AttackFormationSystem.Summarize(_squad);
            _summaryLabel.text = $"{assignedCount} / {totalCount} 명";
            _deployStatLabel.text = $"배치 {assignedCount}명";
            _reserveStatLabel.text = $"미배치 {totalCount - assignedCount}명";
            float ratio = totalCount > 0 ? (float)assignedCount / totalCount : 0f;
            _progressFill.style.width = Length.Percent(ratio * 100f);

            // 셀 20개 갱신
            foreach (var kvp in _cellNameLabels)
            {
                string cell = kvp.Key;
                GuardPlaceholder owner = null;
                foreach (var pair in AttackFormationSystem.EnumerateCells())
                    if (pair.Value == cell) { owner = pair.Key; break; }

                bool isSelectedCell = _selected != null && AttackFormationSystem.GetCell(_selected) == cell;
                var root = _cellRoots[cell];
                Color border = owner != null ? Dark.StatusGreen
                    : isSelectedCell ? Dark.Accent : Dark.GlassStroke;
                root.style.borderTopColor = root.style.borderBottomColor = root.style.borderLeftColor = root.style.borderRightColor = new StyleColor(border);
                root.style.backgroundColor = new StyleColor(owner != null ? Dark.CardFillSelected
                    : isSelectedCell ? Dark.SelectedFill : Dark.CardFill);

                if (owner != null)
                {
                    kvp.Value.text = $"{owner.GuardName}";
                    kvp.Value.style.color = new StyleColor(Dark.TextMain);
                    _cellRoleLabels[cell].text = $"{owner.JobTitle ?? "병사"} · Lv.{owner.Level}";
                }
                else
                {
                    kvp.Value.text = "+ 배치 가능";
                    kvp.Value.style.color = new StyleColor(Dark.TextSub);
                    _cellRoleLabels[cell].text = "";
                }
            }

            // 선택 정보
            if (_selected != null)
            {
                int idx = shown.IndexOf(_selected);
                _selectedNumberBadge.text = idx >= 0 ? (idx + 1).ToString("00") : "--";
                string cell = AttackFormationSystem.GetCell(_selected);
                _selectedInfoLabel.text = _selected.GuardName;
                _selectedCellLabel.text = string.IsNullOrEmpty(cell)
                    ? "현재 배치: 미배치 (예비)"
                    : $"현재 배치: {cell} · {AttackFormationSystem.RowName(int.Parse(cell.Substring(1)))}";
            }
            else
            {
                _selectedNumberBadge.text = "--";
                _selectedInfoLabel.text = "대원을 선택하세요";
                _selectedCellLabel.text = "현재 배치: -";
            }
        }

        private ScrollView _rosterScroll;

        private void StartRefresh()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() => { if (IsOpen) RefreshAll(); }).Every(400);
        }
        private void StopRefresh()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        // ===== 공용 유틸 =====
        private VisualElement CreatePanelRoot(string name, Rect bounds)
        {
            var panel = new VisualElement { name = name };
            panel.style.position = Position.Absolute;
            panel.style.left = bounds.x; panel.style.top = bounds.y;
            panel.style.width = bounds.width; panel.style.height = bounds.height;
            panel.style.paddingTop = 14.4f; panel.style.paddingBottom = 14.4f;
            panel.style.paddingLeft = 14.4f; panel.style.paddingRight = 14.4f;
            UTKTheme.ApplyFigmaGlass(panel);
            Content.Add(panel);
            return panel;
        }

        private VisualElement MkPanelHeader(string ko, string en)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 12f;
            header.style.flexShrink = 0f;
            var title = MkLabel(ko, 21.6f, Dark.TextMain);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            header.Add(spacer);
            var sub = MkLabel(en, UTKTheme.FontBadge, Dark.TextSub);
            header.Add(sub);
            return header;
        }

        private static Label MkLabel(string text, float fontSize, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = fontSize;
            l.style.color = new StyleColor(color);
            return l;
        }

        private void CreateCloseButton(VisualElement parent)
        {
            var close = new Button(Close) { text = "✕", name = "FormationCloseButton" };
            close.style.position = Position.Absolute;
            close.style.right = 14.4f; close.style.top = 14.4f;
            close.style.width = 31.2f; close.style.height = 31.2f;
            close.style.fontSize = 18f;
            StyleChipButton(close);
            parent.Add(close);
        }

        private void StyleChipButton(Button b)
        {
            b.style.backgroundColor = new StyleColor(Dark.PanelSub);
            b.style.color = new StyleColor(Dark.TextMain);
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 1.2f;
            b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(Dark.GlassStroke);
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6f;
        }

        private void ApplyGitHubDarkStyle()
        {
            style.backgroundColor = new StyleColor(Color.clear);
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
            style.color = Dark.TextMain;
            pickingMode = PickingMode.Ignore;   // 빈 영역 클릭 통과 — 패널이 수신
            style.minWidth = CompositionBounds.width;
            style.minHeight = CompositionBounds.height;
        }

        private VisualElement _geometryRoot;

        private void ApplyCanvasBounds()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            FigmaCanvasLayout.ApplyDesignSpace(this, _content, CompositionBounds, root);
            _geometryRoot = root;
            root.RegisterCallback<GeometryChangedEvent>(OnCanvasGeometryChanged);
        }

        private void OnCanvasGeometryChanged(GeometryChangedEvent evt)
        {
            if (evt.target == _geometryRoot && IsOpen)
                ApplyCanvasBounds();
        }
    }
}
