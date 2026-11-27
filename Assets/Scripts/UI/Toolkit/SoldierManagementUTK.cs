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
            // [Figma 69:4 진실치] 글래스 요소 색 — 저장 트리 실측(화이트 틴트/글래스 스트로크/상태색)
            public static readonly Color GlassStroke = new Color32(0x30, 0x36, 0x3D, 0x80);   // #30363D@0.5
            public static readonly Color CardFill = new Color(1f, 1f, 1f, 0.02f);             // 슬롯 미선택
            public static readonly Color CardFillSelected = new Color(1f, 1f, 1f, 0.08f);     // 슬롯 선택
            public static readonly Color StatsFill = new Color(1f, 1f, 1f, 0.01f);            // StatsContainer
            public static readonly Color GaugeTrackFill = new Color(1f, 1f, 1f, 0.08f);       // GaugeTrack
            public static readonly Color MissionSelectedFill = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.12f);
            public static readonly Color StatusGreen = Hex(0x39D353);   // 배치 활성(피그마 상태 그린)
            public static readonly Color StatusOrange = Hex(0xFF9E2C);  // 외교 특사
            // [144차 P6-SOL #47] [Figma 69:138] 등급 배지 fill #BC8CFF 솔리드 — CSP Rarity 선례(144차 배지 어휘 체계)
            public static readonly Color RarityPurple = new Color32(0xBC, 0x8C, 0xFF, 0xFF);
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
        private Label _deployTip;        // [122차 R2] 배치 패널 상태 라벨 — 거부 사유 표시용(기존 SetStatus 계약 부재 → tip 라벨 준용)
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
                b.style.fontSize = UTKTheme.FontTab;                 // [Figma] 14.4/700
                b.style.unityFontStyleAndWeight = FontStyle.Bold;
                b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 7.2f;
                // [144차 P6-SOL #21] [Figma 69:25/27] 비활성 탭 — fill 없음(투명) + stroke #30363D@0.5 1.2.
                //   StyleButton 비사용(공용 스타일 — 닫기/적용 버튼 계약 무변경) · hover 콜백 미등록(피그마 상태계 준수).
                b.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                b.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
                b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 1.2f;
                b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(GitHubDark.GlassStroke);
                _filterTabs.Add(b);
                filterRow.Add(b);
            }
            left.Add(filterRow);
            RefreshFilterTabs();

            _listScroll = new ScrollView();
            _listScroll.style.flexGrow = 1f;
            left.Add(_listScroll);

            // [Figma SummaryFooter 69:114] 좌 라벨 + 우 값
            // [144차 P6-SOL #41] 상단 테두리 #30363D@0.5 1.2 + paddingTop 14.4
            var summaryRow = new VisualElement { name = "SummaryFooter" };
            summaryRow.style.flexDirection = FlexDirection.Row;
            summaryRow.style.alignItems = Align.Center;
            summaryRow.style.height = 34.4f;
            summaryRow.style.minHeight = 34.4f;
            summaryRow.style.paddingTop = 14.4f;
            summaryRow.style.borderTopWidth = 1.2f;
            summaryRow.style.borderTopColor = new StyleColor(GitHubDark.GlassStroke);
            var summaryLeft = MkLabel("총원 / 최대 수용력", UTKTheme.FontRowLabel, GitHubDark.TextSub);   // [Figma 69:116] 15.6/400 #8B949E
            summaryLeft.style.flexGrow = 1f;
            summaryRow.Add(summaryLeft);
            _summaryLabel = MkLabel("", UTKTheme.FontRowLabel, GitHubDark.Accent);   // [Figma] 15.6/700 #58A6FF
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
            // [Figma 정합 v3] 컴포지션 상대 raw Figma px — 등배수 스케일은 _content 디자인공간이 담당.
            panelRoot.style.left = bounds.x;
            panelRoot.style.top = bounds.y;
            panelRoot.style.width = bounds.width;
            panelRoot.style.height = bounds.height;
            panelRoot.style.flexDirection = FlexDirection.Column;
            panelRoot.style.flexShrink = 0f;
            UTKTheme.ApplyFigmaGlass(panelRoot);   // [Figma 69:4 글래스] #161B22@0.7 + #30363D@0.5 1.2px + r14.4
            // [144차 P6-SOL #8] [Figma 69:5/118/171 fills] 패널 글래스 fill #161B22@0.702 — 이 창 개별 재적용.
            //   UTKTheme.GlassPanelFill 전역 상수 무수정 계약(D1 확정 0.702 전 창 통일 — 동일값이나 계약 명시).
            panelRoot.style.backgroundColor = new StyleColor(new Color32(0x16, 0x1B, 0x22, 0xB3));   // 0xB3=179 → α0.702
            // [Figma 69:4] 패널 pad 24 + 세로 gap 19.2
            panelRoot.style.paddingLeft = panelRoot.style.paddingRight = 24f;
            panelRoot.style.paddingTop = panelRoot.style.paddingBottom = 24f;
            panelRoot.style.justifyContent = Justify.FlexStart;
            // [144차 P6-SOL #75 P0 / #76 P1] [Figma 69:6~17] 모서리 데칼 — 구 AddL(0,0)/(12,0)/(0,11)/(12,11) 4개가
            //   좌상단 12px 박스에 몰리던 오배치(진실치: TR/BL/BR 각 모서리, ax13506/ay1021 실측)를
            //   144차 P0 V1 공용 헬퍼로 전환 + 색 Stroke(#2E343D)→#58A6FF · 두께 1→1.8 동시 수리.
            //   코너 밖 기본 모드(InteractionPanelUTK #42 승계 선례). Δ 기록: 진실치 L 12 코너 내부 기점 vs
            //   헬퍼 9.6 코너 중심(밖 걸침 −4.8) — .utk-window overflow:hidden으로 좌패널 TL/BL·우패널
            //   TR/BR의 바깥 절반은 클립(중앙 패널 4코너·좌 TR·우 BL은 온전). 시각 근사는 어휘 계약 우선.
            UTKTheme.AddCornerDecals(panelRoot, 9.6f, 1.8f, "#58A6FF");
            _content.Add(panelRoot);
            return panelRoot;
        }

        // [144차 P6-SOL #75] 구 로컬 AddCornerDecals(좌상단 몰림 오배치 원인) 삭제 —
        //   UTKTheme.AddCornerDecals 공용 어휘(144차 P0 V1)로 대체. CreatePanelRoot 참조.

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
            // [Figma 정합 v3] 등배수 디자인공간 — 창 박스=CompositionBounds×k, _content raw 1584×984 + scale=k.
            // 기존 percent(X/Y 독립) 배치는 비-16:9 게임창에서 찌그러지므로 등배수 계약으로 교체.
            FigmaCanvasLayout.ApplyDesignSpace(this, _content, CompositionBounds, root);
        }

        // ===== 중앙 상세 [Figma 69:118 SoldierDetailPanel] =====
        private Label _dName, _dLevel, _dRole, _dNationBadge, _dTraitText, _dImageHint;
        private VisualElement _previewImage;             // [39차] 3D 미리보기 RT 표시 영역
        private RenderTexture _previewRT;                // [39차] 오프스크린 렌더 타깃
        private GameObject _previewCamGo;                // [39차] 미리보기 카메라
        private GameObject _previewModel;                // [39차] 현재 마운트된 병사 모델 클론
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
            // [Figma SoldierHeroHeader] #FFFFFF@0.02 + #30363D@0.5 1.2px + r9.6
            hero.style.backgroundColor = new StyleColor(GitHubDark.CardFill);
            hero.style.borderTopWidth = hero.style.borderBottomWidth = hero.style.borderLeftWidth = hero.style.borderRightWidth = 1.2f;
            hero.style.borderTopColor = hero.style.borderBottomColor = hero.style.borderLeftColor = hero.style.borderRightColor = new StyleColor(GitHubDark.GlassStroke);
            hero.style.borderTopLeftRadius = hero.style.borderTopRightRadius = hero.style.borderBottomLeftRadius = hero.style.borderBottomRightRadius = 9.6f;
            hero.style.paddingLeft = hero.style.paddingRight = hero.style.paddingTop = hero.style.paddingBottom = 14.4f;
            parent.Add(hero);
            var heroRow = new VisualElement();
            heroRow.style.flexDirection = FlexDirection.Row;
            heroRow.style.alignItems = Align.Center;
            heroRow.style.height = 31f;
            _dName = MkLabel("병사를 선택하세요", UTKTheme.FontDisplayName, GitHubDark.TextMain);   // [Figma] 26.4/700
            _dName.style.unityFontStyleAndWeight = FontStyle.Bold;
            // [43차 수리 — 부대 명부 ui3.PNG "이름/Lv 줄 겹침"] 폰트 자연 행높이(~36px)가 heroRow 31을
            //   넘어 아래 Lv 줄과 충돌 — [Figma 69:137 실측] 라벨 행박스 31 고정 + 수직 중앙 정렬로
            //   레이아웃 박스를 피그마와 동일하게 고정(글자 초과분은 시각만 ±2.5px 중앙 수용).
            _dName.style.height = 31f;
            _dName.style.unityTextAlign = TextAnchor.MiddleLeft;
            heroRow.Add(_dName);
            var heroSpacer = new VisualElement();
            heroSpacer.style.flexGrow = 1f;
            heroRow.Add(heroSpacer);
            // [Figma 등급 배지 규격] 12/700 + pad 9.6/2.4 + r4 — 국가색 데이터 없어 액센트 틴트로 자리 유지(위조 금지)
            _dNationBadge = MkLabel("-", UTKTheme.FontBadge, GitHubDark.Accent);
            _dNationBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            _dNationBadge.style.backgroundColor = new StyleColor(GitHubDark.MissionSelectedFill);   // #58A6FF@0.12
            _dNationBadge.style.borderTopLeftRadius = _dNationBadge.style.borderTopRightRadius = _dNationBadge.style.borderBottomLeftRadius = _dNationBadge.style.borderBottomRightRadius = 4f;
            _dNationBadge.style.paddingLeft = _dNationBadge.style.paddingRight = 9.6f;
            _dNationBadge.style.paddingTop = _dNationBadge.style.paddingBottom = 2.4f;
            heroRow.Add(_dNationBadge);
            hero.Add(heroRow);
            _dLevel = MkLabel("", UTKTheme.FontRowValue, GitHubDark.TextSub);   // [Figma] 16.8/400
            _dLevel.style.unityFontStyleAndWeight = FontStyle.Normal;
            // [43차] [Figma 69:140 실측] Lv 줄 행박스 20 고정(이름 줄과 충돌 방지)
            _dLevel.style.height = 20f;
            _dLevel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _dLevel.style.marginTop = 4.8f;
            hero.Add(_dLevel);

            // [Figma SoldierImageFrame 528×384] 3D 미리보기 — [39차] 실제 병사 모델 RT 렌더 표시
            var image = new VisualElement { name = "SoldierImageFrame" };
            image.style.height = 384f;
            image.style.minHeight = 384f;
            image.style.flexShrink = 0f;
            image.style.backgroundColor = new StyleColor(GitHubDark.StatsFill);   // [Figma] #FFFFFF@0.01
            image.style.borderTopWidth = image.style.borderBottomWidth = image.style.borderLeftWidth = image.style.borderRightWidth = 1.2f;
            image.style.borderTopColor = image.style.borderBottomColor = image.style.borderLeftColor = image.style.borderRightColor = new StyleColor(GitHubDark.GlassStroke);
            image.style.borderTopLeftRadius = image.style.borderTopRightRadius = image.style.borderBottomLeftRadius = image.style.borderBottomRightRadius = 9.6f;
            image.style.justifyContent = Justify.Center;
            image.style.alignItems = Align.Center;
            _previewImage = new VisualElement { name = "SoldierPreviewImage" };
            _previewImage.style.width = 460f;
            _previewImage.style.height = 340f;
            _previewImage.style.borderTopLeftRadius = _previewImage.style.borderTopRightRadius = _previewImage.style.borderBottomLeftRadius = _previewImage.style.borderBottomRightRadius = 6f;
            image.Add(_previewImage);
            // [42차] "3D 미리보기" 안내 텍스트 제거(피그마 = 순수 그래픽 영역) — 박스 밖으로
            //   삐져나가던 원인. 실제 모델 RT가 영역을 채운다.
            _dImageHint = MkLabel("", 15.6f, GitHubDark.TextSub);
            _dImageHint.style.display = DisplayStyle.None;
            image.Add(_dImageHint);
            parent.Add(image);

            // [Figma StatsContainer 528×236.8] pad 19.2, gap 14.4 — StatRow(라벨+우측 "cur / max" + GaugeTrack 489.6×9.6)
            var stats = new VisualElement { name = "StatsContainer" };
            stats.style.height = 236.8f;
            stats.style.minHeight = 236.8f;
            stats.style.flexShrink = 0f;
            // [Figma StatsContainer] #FFFFFF@0.01 + #30363D@0.5 1.2px + r9.6
            stats.style.backgroundColor = new StyleColor(GitHubDark.StatsFill);
            stats.style.borderTopWidth = stats.style.borderBottomWidth = stats.style.borderLeftWidth = stats.style.borderRightWidth = 1.2f;
            stats.style.borderTopColor = stats.style.borderBottomColor = stats.style.borderLeftColor = stats.style.borderRightColor = new StyleColor(GitHubDark.GlassStroke);
            stats.style.borderTopLeftRadius = stats.style.borderTopRightRadius = stats.style.borderBottomLeftRadius = stats.style.borderBottomRightRadius = 9.6f;
            stats.style.paddingLeft = stats.style.paddingRight = stats.style.paddingTop = stats.style.paddingBottom = 19.2f;
            parent.Add(stats);
            // [144차 P6-SOL #53 Figma 69:146/152/158/164] Stat 라벨 영문 접미 원문
            string[] statNames = { "공격력 (Attack Power)", "방어력 (Defense)", "체력 (Vitality)", "민첩 (Agility)" };
            for (int i = 0; i < 4; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Column;
                row.style.marginBottom = i < 3 ? 14.4f : 0f;

                var labelRow = new VisualElement();
                labelRow.style.flexDirection = FlexDirection.Row;
                labelRow.style.alignItems = Align.Center;
                labelRow.style.height = 22f;
                // [Figma StatRow] 라벨 16.8/700 #F0F6FC · 값 16.8/700 #58A6FF
                var name = MkLabel(statNames[i], UTKTheme.FontCardName, GitHubDark.TextMain);
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                name.style.flexGrow = 1f;
                labelRow.Add(name);
                var val = MkLabel("-", UTKTheme.FontCardName, GitHubDark.Accent);
                val.style.unityFontStyleAndWeight = FontStyle.Bold;
                val.style.unityTextAlign = TextAnchor.MiddleRight;
                labelRow.Add(val);
                _statVals[i] = val;
                row.Add(labelRow);

                // [Figma GaugeTrack 489.6×9.6]
                var track = new VisualElement { name = "GaugeTrack" };
                track.style.height = 9.6f;
                track.style.marginTop = 7.2f;
                track.style.backgroundColor = new StyleColor(GitHubDark.GaugeTrackFill);   // [Figma] #FFFFFF@0.08
                track.style.borderTopLeftRadius = track.style.borderBottomLeftRadius = track.style.borderTopRightRadius = track.style.borderBottomRightRadius = 4.8f;
                var fill = new VisualElement { name = "GaugeFill" };
                fill.style.height = new Length(100f, LengthUnit.Percent);
                fill.style.width = new Length(0f, LengthUnit.Percent);
                fill.style.backgroundColor = GitHubDark.Accent;
                fill.style.borderTopLeftRadius = fill.style.borderBottomLeftRadius = fill.style.borderTopRightRadius = fill.style.borderBottomRightRadius = 4.8f;
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
            // [Figma TraitSection] 배경/테두리 없음(투명) — 텍스트만
            trait.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            trait.style.paddingLeft = trait.style.paddingRight = 14.4f;
            trait.style.paddingTop = trait.style.paddingBottom = 14.4f;
            var traitTitle = MkLabel("고유 전술 특성", UTKTheme.FontRowLabel, GitHubDark.Accent);   // [Figma] 15.6/700 #58A6FF
            traitTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            trait.Add(traitTitle);
            _dTraitText = MkLabel("-", UTKTheme.FontBody, GitHubDark.TextSub);   // [Figma] 14.4/400
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
            ("경작지 농사단", GuardTaskSystem.GuardTask.Farm),
            ("자원 야외 채집", GuardTaskSystem.GuardTask.Gather),
            ("외교 특사",    GuardTaskSystem.GuardTask.Envoy),
            ("광산 광부",     GuardTaskSystem.GuardTask.Mine),
            ("야생 사냥 헌터", GuardTaskSystem.GuardTask.Hunt),
            ("대기(해제)",   GuardTaskSystem.GuardTask.None),
        };

        /// <summary>[144차 P6-SOL #66 Figma MissionText 2행] 영문 부제 원문(JSON 69:196/205/213/221/229/237/245).
        ///   8번째 "대기(해제)"(None)는 게임 전용 계약 카드 — Figma 부제 없음(표기 생략).</summary>
        private static readonly string[] _deploySubtitles =
        {
            "Assault Squad",
            "Fortress Guard",
            "Farming Core",
            "Resource Gathering",
            "Diplomatic Envoy",
            "Ore Mining Team",
            "Wild Hunting Group",
        };

        private void BuildDeployPanel(VisualElement parent)
        {
            parent.Add(MkPanelHeader("임무 배치 및 제어", "DEPLOYMENT OPTIONS"));

            // [Figma SectionDescription 432×48.8] — [122차 R2] 상태 라벨로 격상: 배치 거부 사유 표시용 필드 보관
            _deployTip = MkLabel("병사를 목록에서 선택하고 우측 역할로 배치합니다.", UTKTheme.FontRowLabel, GitHubDark.TextSub);   // [Figma] 15.6/400
            _deployTip.style.whiteSpace = WhiteSpace.Normal;
            _deployTip.style.height = 48.8f;
            _deployTip.style.paddingBottom = 4.8f;   // [144차 P6-SOL #61 Figma SectionDescription pb 4.8]
            _deployTip.style.flexShrink = 0f;
            parent.Add(_deployTip);

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
                row.style.paddingLeft = row.style.paddingRight = row.style.paddingTop = row.style.paddingBottom = 14.4f;   // [144차 P6-SOL #63 Figma pad 14.4 전측]
                row.style.alignItems = Align.Center;
                // [Figma MissionOption] 기본 #FFFFFF@0.02 + #30363D@0.5 1.2 / 선택 #58A6FF@0.12 + #58A6FF 1.2 / r9.6
                row.style.backgroundColor = new StyleColor(GitHubDark.CardFill);
                row.style.borderTopWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 1.2f;
                row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(GitHubDark.GlassStroke);
                row.style.borderTopLeftRadius = row.style.borderTopRightRadius = row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 9.6f;
                row.RegisterCallback<PointerDownEvent>(_ => SelectDeploymentTask(_deployDefs[idx].Item2));

                // [39차 사용자 요구] 라디오 파란 점 제거 — 선택 상태는 카드 테두리 색만으로 표시.
                // [144차 P6-SOL #66 Figma MissionText 2행] 한국명 16.8/700 + 영문 부제 12/400 #8B949E, 세로 gap 2.4
                var missionText = new VisualElement { name = "MissionText_" + idx };
                missionText.style.flexDirection = FlexDirection.Column;
                missionText.style.flexGrow = 1f;
                var name = MkLabel(_deployDefs[idx].Item1, UTKTheme.FontCardName, GitHubDark.TextMain);   // [Figma] 16.8/700
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                missionText.Add(name);
                string subtitle = idx < _deploySubtitles.Length ? _deploySubtitles[idx] : null;
                if (!string.IsNullOrEmpty(subtitle))
                {
                    var sub = MkLabel(subtitle, 12f, GitHubDark.TextSub);   // [Figma] 12/400 #8B949E
                    sub.style.marginTop = 2.4f;                             // [Figma 69:194 MissionText 세로 gap 2.4]
                    missionText.Add(sub);
                }
                row.Add(missionText);

                // [122차 R2] 채집/채광/사냥 행 — 저장고 선행 조건 표시(작은 접미 라벨, 피그마 신규 없음).
                //   missionText가 flexGrow=1이므로 힌트는 카운트 배지 좌측에 유연 부착 — 레이아웃 파손 없음.
                var hintTask = _deployDefs[idx].Item2;
                if (hintTask == GuardTaskSystem.GuardTask.Gather ||
                    hintTask == GuardTaskSystem.GuardTask.Mine ||
                    hintTask == GuardTaskSystem.GuardTask.Hunt)
                {
                    var whHint = MkLabel("※저장고", UTKTheme.FontBadge, GitHubDark.StatusOrange);
                    whHint.style.flexShrink = 0f;
                    whHint.style.marginRight = 6f;
                    row.Add(whHint);
                }

                var count = MkLabel("0명 활성", UTKTheme.FontTab, GitHubDark.TextSub);   // [Figma] 14.4/700 (활성 시 Accent — Refresh에서 갱신)
                count.style.unityFontStyleAndWeight = FontStyle.Bold;
                count.name = "DeploymentCount_" + idx;
                count.style.minWidth = 77.2f;
                count.style.height = 28.6f;
                // [144차 P6-SOL #69 Figma CountBadge] fill #000000@0.251 + stroke #30363D@0.5 1.2 + r 7.2
                count.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.251f));
                count.style.borderTopWidth = count.style.borderBottomWidth = count.style.borderLeftWidth = count.style.borderRightWidth = 1.2f;
                count.style.borderTopColor = count.style.borderBottomColor = count.style.borderLeftColor = count.style.borderRightColor = new StyleColor(GitHubDark.GlassStroke);
                count.style.borderTopLeftRadius = count.style.borderTopRightRadius = count.style.borderBottomLeftRadius = count.style.borderBottomRightRadius = 7.2f;
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
            // [Figma DeploymentFooter] ApplyButton 432×53.6, 상단 gap 14.4 — 라벨 16.8/700, r7.2
            _applyDeploymentButton.style.height = 53.6f;
            _applyDeploymentButton.style.marginTop = 14.4f;
            _applyDeploymentButton.style.flexShrink = 0f;
            StyleButton(_applyDeploymentButton, true);
            _applyDeploymentButton.style.fontSize = UTKTheme.FontCardName;
            _applyDeploymentButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _applyDeploymentButton.style.borderTopLeftRadius = _applyDeploymentButton.style.borderTopRightRadius = _applyDeploymentButton.style.borderBottomLeftRadius = _applyDeploymentButton.style.borderBottomRightRadius = 7.2f;
            parent.Add(_applyDeploymentButton);
            // [38차] 실내 침대 수동 배치 UI 제거(사용자 확정) — 침대 누움은 자동(모든 영지 소속 병사),
            //   이 UI는 역할 배치(공격대/수비대/특사 등)에 집중.

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

            // [122차 R2] 채집/채광/사냥 배치 선행 게이트 — 해당 영지에 저장고(건설 완료)가 있어야 배치 가능.
            if (_pendingTask == GuardTaskSystem.GuardTask.Gather ||
                _pendingTask == GuardTaskSystem.GuardTask.Mine ||
                _pendingTask == GuardTaskSystem.GuardTask.Hunt)
            {
                string territoryKey = ResolveSelectedTerritoryKey();
                if (!WarehouseSystem.HasWarehouse(territoryKey))
                {
                    // 배치 거부 — 토스트 + 상태 라벨 사유 표시, AssignTask 호출 스킵.
                    UTKToastService.Show("저장고를 먼저 건설하세요", 2.5f);
                    if (_deployTip != null)
                    {
                        _deployTip.text = $"저장고를 먼저 건설하세요 — [{(string.IsNullOrEmpty(territoryKey) ? "영지 미식별" : territoryKey)}]";
                        _deployTip.style.color = new StyleColor(GitHubDark.StatusOrange);
                    }
                    Debug.Log($"[SoldierMgmt] 배치 거부 — {_selected.GuardName} → {_pendingTask} (저장고 부재: {territoryKey ?? "영지 미식별"})");
                    return;
                }
            }
            RestoreDeployTip();   // 게이트 통과 — 상태 라벨 기본 안내 복원
            gts.AssignTask(_selected, _pendingTask);
            Debug.Log($"[SoldierMgmt] {_selected.GuardName} → {_pendingTask}");
            RefreshAll();
        }

        /// <summary>[122차 R2] 선택 병사의 소속 영지 키(TerritoryId.ToString() — "Nation_XX") 판정.
        ///   SoldierManagementUTK에 영지 컨텍스트 필드가 없으므로 GuardTaskSystem.ResolveGuardTerritory와
        ///   동일 경로(플레이어 소유 영지 순회 → GuardManager.GetGuardsInTerritory 멤버십)를 UI측에서 재현.
        ///   미식별 시 null 반환 — WarehouseSystem.HasWarehouse(null)=false → 게이트 fail-safe(거부).</summary>
        private string ResolveSelectedTerritoryKey()
        {
            var guard = _selected;
            if (guard == null) return null;
            var db = ProjectName.Core.Data.TerritoryDatabase.Instance;
            var gm = GuardManager.Instance;
            if (db == null || gm == null) return null;
            foreach (var def in db.GetAllDefinitions())
            {
                var st = db.GetState(def.id);
                if (st == null || st.ownership != ProjectName.Core.Data.TerritoryOwnership.PlayerOwned) continue;
                foreach (var cand in gm.GetGuardsInTerritory(def.id))
                    if (cand == guard) return def.id.ToString();
            }
            return null;
        }

        /// <summary>[122차 R2] 배치 패널 상태 라벨(_deployTip) 기본 안내 문구 복원 — null 가드 포함.</summary>
        private void RestoreDeployTip()
        {
            if (_deployTip == null) return;
            _deployTip.text = "병사를 목록에서 선택하고 우측 역할로 배치합니다.";
            _deployTip.style.color = new StyleColor(GitHubDark.TextSub);
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
                _deploymentCounts[i].style.color = new StyleColor(assigned > 0 ? GitHubDark.Accent : GitHubDark.TextSub);   // [Figma] 활성=Accent
                bool selected = task == _pendingTask;
                VisualElement card = _deploymentCards[i];
                Color border = selected ? GitHubDark.Accent : GitHubDark.GlassStroke;
                card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(border);
                card.style.backgroundColor = new StyleColor(selected ? GitHubDark.MissionSelectedFill : GitHubDark.CardFill);   // [Figma] #58A6FF@0.12 / #FFFFFF@0.02
                var radio = card.Q("DeploymentRadio_" + i);
                if (radio != null)
                {
                    radio.style.borderTopColor = radio.style.borderBottomColor = radio.style.borderLeftColor = radio.style.borderRightColor =
                        new StyleColor(selected ? GitHubDark.Accent : GitHubDark.GlassStroke);
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
            // [Figma 정합 v3] 위치/크기는 CompositionBounds×k(등배수) — 중앙 정렬(CenterSelf) 대신 Figma 절대좌표 준수.
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
            CleanupPreviewAll();   // [39차] 미리보기 리그 정리(모델/카메라/RT)
            if (_geometryRoot != null)
                _geometryRoot.UnregisterCallback<GeometryChangedEvent>(OnCanvasGeometryChanged);
            _geometryRoot = null;
            base.Hide();
            StopRefresh();
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
                // [144차 P6-SOL #21] [Figma 69:23/25] 활성 fill #58A6FF / 비활성 fill 없음(투명)+스트로크 유지
                b.style.backgroundColor = new StyleColor(active ? GitHubDark.Accent : new Color(0f, 0f, 0f, 0f));
                b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor =
                    new StyleColor(active ? GitHubDark.Accent : GitHubDark.GlassStroke);   // 활성=fill 동색 근사(피그마 활성 스트로크 없음)
                b.style.color = active ? GitHubDark.BgBase : GitHubDark.TextSub;   // [Figma] 활성 #0B0E14 / 비활성 #8B949E
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
                var emptyList = MkLabel("병사 시스템 없음", UTKTheme.FontBody, GitHubDark.TextSub);   // [Figma] 14.4/400
                _listScroll.Add(emptyList);
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

            // [144차 P6-SOL #43 Figma 69:117] 우 값 "N / M 명" — 총원=모집 병사 수(실데이터).
            //   기록: 최대 수용력(침대 수용력) 데이터 소스가 코드베이스에 존재하지 않음(grep 실측) →
            //   위조 수치 대신 totalRegistered를 임시 상한으로 사용(37차 위조임금 계약 준용 — 실데이터 부재 시 기록).
            _summaryLabel.text = $"{recruited} / {totalRegistered} 명";
        }

        private VisualElement BuildGuardCard(GuardPlaceholder g)
        {
            // [43차 피그마 69:4 SoldierSlot 전수 실측 재정합 — 부대 명부 ui3.PNG 깨짐 근원 수리]
            //   근원: 카드에 flexDirection 미설정 → 기본 Column(세로 쌓임) → 아바타/이름/직책/Lv가
            //   카드 프레임을 뚫고 세로로 배치(ui3 실측). AttackFormationUTK 카드(정합 완성본)와
            //   동일하게 Row 방향으로 재작성.
            //   [Figma 69:30 SoldierSlot] dir=HORIZONTAL · pad 14.4(전측) · gap 14.4 · r9.6
            //   ├─ TierStrip [ABS 좌측] 4.8×86.4 — 등급 컬러(피그마 7슬롯 실측 팔레트)
            //   ├─ AvatarFrame 52.8×52.8 r26.4 (#21262D) — 수직 중앙(86.4−52.8)/2=16.8
            //   ├─ InfoGroup [세로 gap 4.8, flexGrow]
            //   │   ├─ NameRow [가로 space-between] 이름 16.8/700 #F0F6FC · Lv 14.4/700 #58A6FF
            //   │   └─ RoleRow [가로 space-between] 직책 14.4/400 #8B949E · StatusAndSalary(세로 gap2.4)
            //   │       └─ StatusPill pad(7.2/2.4) r4.8 #000000 — 상태 12/700
            var card = new VisualElement { name = "SoldierSlot" };
            card.style.height = 86.4f;
            card.style.minHeight = 86.4f;
            card.style.flexShrink = 0f;
            card.style.marginBottom = 9.6f;                       // [Figma SoldierSlotsContainer gap 9.6]
            card.style.flexDirection = FlexDirection.Row;         // [43차 근원 수리] Column→Row
            card.style.alignItems = Align.Center;                 // 아바타 수직 중앙(피그마 y16.8 실측)
            card.style.paddingTop = card.style.paddingBottom = 14.4f;
            card.style.paddingLeft = card.style.paddingRight = 14.4f;
            // [Figma SoldierSlot] 선택 #FFFFFF@0.08+#58A6FF 1.8 / 미선택 #FFFFFF@0.02+#30363D@0.5 1.2 / r9.6
            float cardStrokeWidth = g == _selected ? 1.8f : 1.2f;
            card.style.borderTopWidth = card.style.borderBottomWidth = cardStrokeWidth;
            card.style.borderLeftWidth = card.style.borderRightWidth = cardStrokeWidth;
            Color cardStroke = g == _selected ? GitHubDark.Accent : GitHubDark.GlassStroke;
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(cardStroke);
            card.style.backgroundColor = new StyleColor(g == _selected ? GitHubDark.CardFillSelected : GitHubDark.CardFill);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 9.6f;
            card.RegisterCallback<PointerDownEvent>(_ => SelectGuard(g));

            var tierStrip = new VisualElement { name = "SoldierTierStrip" };
            tierStrip.style.position = Position.Absolute;
            tierStrip.style.left = 0f;
            tierStrip.style.top = 0f;
            tierStrip.style.width = 4.8f;
            tierStrip.style.height = 86.4f;
            // [43차 Figma 69:29 7슬롯 TierStrip 실측 팔레트] Lv45=#BC8CFF(퍼플) / Lv 중간=#1F6FEB(딥블루)
            //   / Lv 낮음=#58A6FF(액센트) / 최하=#8B949E(그레이) — 레벨 구간 매핑(실데이터 기반).
            tierStrip.style.backgroundColor = new StyleColor(TierColorForLevel(g.Level));
            tierStrip.pickingMode = PickingMode.Ignore;
            card.Add(tierStrip);

            // [43차 Figma 69:32 AvatarFrame] 52.8 원형 r26.4 — 사람 아이콘(39차 요구 유지)
            var avatar = new VisualElement { name = "SoldierAvatar" };
            avatar.style.width = 52.8f; avatar.style.height = 52.8f;
            avatar.style.backgroundColor = new StyleColor(new Color(GitHubDark.PanelSub.r, GitHubDark.PanelSub.g, GitHubDark.PanelSub.b, 0.6f));   // [144차 P6-SOL #30 Figma #21262D@0.6]
            avatar.style.borderTopWidth = avatar.style.borderBottomWidth = avatar.style.borderLeftWidth = avatar.style.borderRightWidth = 1.2f;
            avatar.style.borderTopColor = avatar.style.borderBottomColor = avatar.style.borderLeftColor = avatar.style.borderRightColor = new StyleColor(GitHubDark.GlassStroke);
            avatar.style.borderTopLeftRadius = avatar.style.borderTopRightRadius = avatar.style.borderBottomLeftRadius = avatar.style.borderBottomRightRadius = 26.4f;
            avatar.style.marginRight = 14.4f;                     // [Figma SoldierSlot gap 14.4]
            avatar.style.flexShrink = 0f;
            avatar.style.justifyContent = Justify.Center;
            avatar.style.alignItems = Align.Center;
            var personIcon = new VisualElement { name = "SoldierAvatarIcon" };
            var avatarTex = Resources.Load<Texture2D>("UI/SoldierAvatarIcon");
            if (avatarTex != null)
                personIcon.style.backgroundImage = UTKTextureSafe.ToBackground(avatarTex);
            personIcon.style.width = 24f; personIcon.style.height = 24f;   // [144차 P6-SOL #31 Figma user-cog 24×24] (42차 32→24 복원)
            avatar.Add(personIcon);
            card.Add(avatar);

            // [43차 Figma 69:34 InfoGroup] 세로 gap 4.8 · flexGrow 1(우측 끝까지, 실측 336w)
            var info = new VisualElement();
            info.style.flexGrow = 1f;
            info.style.flexDirection = FlexDirection.Column;
            info.style.alignItems = Align.Stretch;

            // [Figma NameRow] 이름 16.8/700 #F0F6FC(좌) · Lv 14.4/700 #58A6FF(우측 끝 — 실측 x373.6 우정렬)
            var nameRow = new VisualElement { name = "NameRow" };
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            var name = MkLabel(g.GuardName, UTKTheme.FontCardName, GitHubDark.TextMain);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.flexGrow = 1f;
            nameRow.Add(name);
            var lv = MkLabel($"Lv.{g.Level}", UTKTheme.FontTab, GitHubDark.Accent);   // [Figma] 14.4/700 #58A6FF
            lv.style.unityFontStyleAndWeight = FontStyle.Bold;
            lv.style.unityTextAlign = TextAnchor.MiddleRight;
            nameRow.Add(lv);
            info.Add(nameRow);

            // [Figma RoleRow] 직책 14.4/400 #8B949E(좌·행 중앙) · 우측 상태필(세로 스택)
            var roleRow = new VisualElement { name = "RoleRow" };
            roleRow.style.flexDirection = FlexDirection.Row;
            roleRow.style.alignItems = Align.Center;
            roleRow.style.marginTop = 4.8f;                       // [Figma InfoGroup gap 4.8]
            var role = MkLabel(g.JobTitle ?? "병사", UTKTheme.FontBody, GitHubDark.TextSub);
            role.style.unityFontStyleAndWeight = FontStyle.Normal;
            role.style.flexGrow = 1f;
            roleRow.Add(role);
            // [Figma StatusAndSalary] 세로 gap 2.4 — StatusPill(상태) + 침대 현황(실데이터 — 임금 데이터 미존재)
            var statusStack = new VisualElement { name = "StatusAndSalary" };
            statusStack.style.flexDirection = FlexDirection.Column;
            statusStack.style.alignItems = Align.FlexEnd;
            statusStack.style.flexShrink = 0f;
            var task = GetTaskName(g);
            var pill = new VisualElement { name = "StatusPill" };
            pill.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.251f));   // [144차 P6-SOL #37 Figma #000000@0.251]
            pill.style.paddingLeft = pill.style.paddingRight = 7.2f;
            pill.style.paddingTop = pill.style.paddingBottom = 2.4f;
            pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius = pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 4.8f;
            var st = MkLabel(task, UTKTheme.FontBadge, TaskStatusColor(task));          // [Figma] 12/700
            st.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.Add(st);
            statusStack.Add(pill);
            // [37차 침대 현황 유지 — Figma Salary 슬롯을 실데이터(침대 n)로 채움 — 위조 임금 금지]
            string bedTag = g.IndoorBedIndex > 0 ? $"🛏️ 침대 {g.IndoorBedIndex}" : "";
            if (!string.IsNullOrEmpty(bedTag))
            {
                var bed = MkLabel(bedTag, UTKTheme.FontBadge, GitHubDark.Accent);
                bed.style.unityFontStyleAndWeight = FontStyle.Bold;
                bed.style.marginTop = 2.4f;
                statusStack.Add(bed);
            }
            roleRow.Add(statusStack);
            info.Add(roleRow);
            card.Add(info);

            return card;
        }

        /// <summary>[43차 Figma 69:29 TierStrip 실측 팔레트] 등급 컬러 — 레벨 구간 매핑.
        ///   피그마 7슬롯: #BC8CFF(퍼플·최상) / #1F6FEB(딥블루) / #58A6FF(액센트) / #8B949E(그레이·최하).</summary>
        private static Color TierColorForLevel(int level)
        {
            if (level >= 40) return new Color32(0xBC, 0x8C, 0xFF, 0xFF);   // 퍼플 — 정예
            if (level >= 25) return new Color32(0x1F, 0x6F, 0xEB, 0xFF);   // 딥블루
            if (level >= 10) return GitHubDark.Accent;                     // #58A6FF
            return GitHubDark.TextSub;                                     // #8B949E
        }

        /// <summary>[Figma StatusPill 상태색] 활성 배치=#39D353, 외교 특사=#FF9E2C, 대기=#8B949E.</summary>
        private static Color TaskStatusColor(string taskName)
        {
            if (taskName == "특사") return GitHubDark.StatusOrange;
            if (taskName != "대기") return GitHubDark.StatusGreen;
            return GitHubDark.TextSub;
        }

        // ===== [39차] 3D 미리보기 — 선택 병사 모델을 오프스크린 RT에 동기 렌더 =====
        //  GblItemIconRenderer 동기 베이크(34차)와 동일 패턴: 마운트→cam.Render→ReadPixels→표시.

        private void RenderSoldierPreview(GuardPlaceholder g)
        {
            if (_previewImage == null) return;
            if (g == null || !Application.isPlaying)
            {
                _previewImage.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                return;
            }

            // 모델 프리팹 — TerritoryBuilder 병사 FBX 키(레벨 구간) 재사용
            string fbxKey = TerritoryBuilder.GetSoldierFbxKeyForLevel(g.Level);
            var modelPrefab = Resources.Load<GameObject>(fbxKey);
            if (modelPrefab == null)
            {
                _previewImage.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                return;
            }

            try
            {
                CleanupPreviewRig();

                // [47차 고품질] RT 256 → 512 + MSAA 8x — 피그마 528×384 프레임에 원본 이상 해상도.
                const int size = 512;
                if (_previewRT == null)
                {
                    _previewRT = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                    _previewRT.Create();
                }

                _previewModel = Object.Instantiate(modelPrefab);
                _previewModel.name = "SoldierPreview_" + fbxKey;
                Vector3 remote = new Vector3(20000f, 2000f, 20000f);
                _previewModel.transform.position = remote;

                // [43차 수리 — 뒷모습/무텍스처/T포즈 3건 근원]
                // ① 게임 CreateGuard와 동일 체인 적용: HumanoidClipDriver(Soldier 모드·이동 게이트로
                //    T포즈 해소 — Start에서 Speed=0 → Idle 클립 상태) + GLB 텍스처 복사.
                //    드라이버 Update는 실내에서도 Speed=0 Idle만 돌므로 미리보기에서 안전(이동 없음).
                var anim = _previewModel.GetComponent<Animator>();
                if (anim == null) anim = _previewModel.AddComponent<Animator>();
                anim.runtimeAnimatorController =
                    Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
                anim.applyRootMotion = false;
                var driver = _previewModel.AddComponent<HumanoidClipDriver>();
                driver.mode = HumanoidClipDriver.DriveMode.Soldier;
                driver.enabled = false;          // Update 정지 — A-pose 정지 포즈만 확보(호흡 없음 = 프레임 안정)
                var glbKey = fbxKey.Replace("/fbx/", "/");
                HumanoidClipDriver.CopyMaterialsFromGlb(_previewModel, glbKey);

                // [51차] 미리보기 병사도 게임과 동일 1.8m(SoldierTargetHeight) — 게임 병사와 동일 크기.
                TerritoryBuilder.NormalizeSoldierHeight(_previewModel, TerritoryBuilder.SoldierTargetHeight);

                // 프레이밍 — bounds 상면 기준 살짝 사선
                var rends = _previewModel.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) return;
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);

                if (_previewCamGo == null)
                {
                    _previewCamGo = new GameObject("SoldierPreviewCam");
                    var cam0 = _previewCamGo.AddComponent<Camera>();
                    // [47차 고품질] MSAA 8x + HDR — 경계선/텍스처 디테일 선명화.
                    cam0.allowMSAA = true;
                    cam0.allowHDR = true;
                }
                var cam = _previewCamGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.043f, 0.047f, 0.078f, 1f);   // #0B0E14
                cam.cullingMask = ~0;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 20f;
                cam.fieldOfView = 28f;
                cam.targetTexture = _previewRT;

                // [43차 ② 정면 카메라 — 뒷모습 근원 수리] [Figma 69:142 Soldier3DGraphic 정면 위주 뷰]
                //   병사 FBX 피벗은 +Z(앞)를 보고 서 있으므로 카메라를 +Z 쪽에 두고 LookAt로 조준.
                Vector3 center = b.center;
                // [43차 ③ 발 잘림 수리] 수직 프레이밍 여유 — 절반 높이 1.30배+0.08 + 조준점=몸 수직 중앙.
                float halfH = b.extents.y * 1.30f + 0.08f;
                float dist = halfH / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                Vector3 aim = new Vector3(center.x, b.min.y + b.size.y * 0.5f, center.z);
                cam.transform.position = aim + new Vector3(dist * 0.22f, dist * 0.10f, dist);   // +Z 정면·살짝 우측·상단 사선
                cam.transform.LookAt(aim);

                // [47차 고품질] 미리보기 전용 라이팅 — 키 라이트(우상단 주광)+필 라이트(좌측 보광)+림 라이트
                //   (후방 윤곽)+앰비언트 게이트. 44차 실측처럼 무광 실루엣을 없애고 옷 텍스처/금속 광택이 읽힌다.
                EnsurePreviewLighting(_previewModel.transform.position);

                cam.Render();

                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = _previewRT;
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.hideFlags = HideFlags.HideAndDontSave;

                _previewImage.style.backgroundImage = UTKTextureSafe.ToBackground(tex);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SoldierMgmt] 3D 미리보기 렌더 실패(무시): " + e.Message);
            }
        }

        /// <summary>[47차 고품질 → 48차 근원 수리] 미리보기 전용 3점 라이팅.
        ///   ⚠ Directional은 **위치가 무의미한 씬 전역 방향광** — 원격(20000)에 둬도 실내 전체가
        ///   밝아지는 사고(사용자 실측). 수리: **Point 라이트로 전환**(모델 bounds 기준 상대 배치 —
        ///   카메라 targetTexture에만 기여, 실내 씬 조명 무영향). Range를 모델 1.0m 스케일에 맞춤.</summary>
        private Transform _previewLights;

        private void EnsurePreviewLighting(Vector3 at)
        {
            if (_previewLights != null) DestroyPreviewLightsGameObject(_previewLights.gameObject);
            var lights = new GameObject("SoldierPreviewLights");
            _previewLights = lights.transform;
            lights.transform.position = at;
            void Add(string name, Vector3 offset, Color color, float intensity, float range)
            {
                var go = new GameObject(name);
                go.transform.SetParent(lights.transform, false);
                go.transform.localPosition = offset;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;                       // [48차] Directional 폐지 — 위치 유효 타입
                l.color = color;
                l.intensity = intensity;
                l.range = range;
            }
            // 모델 1.0m — 카메라(전방 dist≈2.2m)에서 모델을 비추는 광원 배치(월드 오프셋 = 모델 중심 기준)
            Add("KeyLight", new Vector3(0.8f, 1.1f, 1.2f), new Color(1f, 0.97f, 0.9f), 2.6f, 4f);     // 우상단 전방 주광
            Add("FillLight", new Vector3(-1.1f, 0.4f, 0.6f), new Color(0.75f, 0.82f, 1f), 1.4f, 4f);  // 좌측 쿨 보광
            Add("RimLight", new Vector3(0.2f, 0.9f, -1.4f), new Color(0.9f, 0.95f, 1f), 2.0f, 4f);    // 후방 윤곽
        }

        private void CleanupPreviewRig()
        {
            if (_previewModel != null) { Object.Destroy(_previewModel); _previewModel = null; }
            if (_previewLights != null) { DestroyPreviewLightsGameObject(_previewLights.gameObject); _previewLights = null; }
        }

        /// <summary>[EditMode 호환] EditMode 테스트에서 Object.Destroy는 에러 로그를 내므로 재생/편집 모드 분기 정리.</summary>
        private static void DestroyPreviewLightsGameObject(GameObject go)
        {
#if UNITY_EDITOR
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
#else
            Object.Destroy(go);
#endif
        }
        private void CleanupPreviewAll()
        {
            CleanupPreviewRig();
            if (_previewCamGo != null) { Object.Destroy(_previewCamGo); _previewCamGo = null; }
            if (_previewRT != null)
            {
                _previewRT.Release();
                Object.Destroy(_previewRT);
                _previewRT = null;
            }
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
            RenderSoldierPreview(g);

            int atk = g.GetAttack();
            int def = g.GetDefense();
            float hp = g.MaxHP;
            int agi = g.GetAgility();

            // [144차 P6-SOL #57 Figma 69:147/153/159/165] Stat max 스펙 400/400/500/300 — 하드코딩 300/300/500/100 교정
            SetStat(0, atk, 400);
            SetStat(1, def, 400);
            SetStat(2, hp, 500);
            SetStat(3, agi, 300);
        }

        private void SetStat(int idx, float val, float max)
        {
            float pct = Mathf.Clamp01(max > 0 ? val / max : 0f);
            _statBars[idx].style.width = new Length(pct * 100f, LengthUnit.Percent);
            _statVals[idx].text = $"{Mathf.RoundToInt(val)} / {Mathf.RoundToInt(max)}";   // [Figma] "284 / 400" 형식
        }

        /// <summary>[Figma PanelHeader 69:18] 제목 21.6/700 + 영문 부제 13.2/500(gap 12/세로 8) — 3패널 공통.</summary>
        private static VisualElement MkPanelHeader(string title, string subtitle)
        {
            var h = new VisualElement { name = "PanelHeader" };
            h.style.flexDirection = FlexDirection.Row;
            h.style.alignItems = Align.Center;
            h.style.paddingBottom = 14.4f;
            h.style.height = 52.8f;
            h.style.minHeight = 52.8f;
            // [144차 P6-SOL #13] [Figma 69:18 strokes] PanelHeader 테두리 #30363D@0.502 1.2
            h.style.borderTopWidth = h.style.borderBottomWidth = h.style.borderLeftWidth = h.style.borderRightWidth = 1.2f;
            h.style.borderTopColor = h.style.borderBottomColor = h.style.borderLeftColor = h.style.borderRightColor = new StyleColor(GitHubDark.GlassStroke);
            var t = MkLabel(title, UTKTheme.FontTitle, GitHubDark.TextMain);   // [Figma] 21.6/700
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            h.Add(t);
            var s = MkLabel(subtitle, UTKTheme.FontSubtitle, GitHubDark.TextSub);   // [Figma] 13.2/500
            s.style.unityFontStyleAndWeight = FontStyle.Normal;
            s.style.marginLeft = 12f;
            s.style.marginTop = 8f;   // [144차 P6-SOL #16] [Figma 69:19 ay82/69:21 ay90] Title↔Subtitle 세로 8 (구 5)
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
