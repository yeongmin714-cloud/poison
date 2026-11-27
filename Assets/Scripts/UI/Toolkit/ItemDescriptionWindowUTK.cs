// F4 (2026-09-24): 아이템 상세창 — Figma DetailPanel 표준(480×608) 재생 + GitHub-dark.
// 구조: PanelHeader('상세 정보'/'SPECIFICATIONS') + ItemNameSection(이름+Tier배지) +
//       ItemImageSection(등급 스트립 + 320px 아이콘) + StatsGrid(수량/카테고리/내구도/세트) + DescriptionSection.
// 기능(ShowItem/Clear/Open/Hide/SyncWithInventory/CenterOnScreen) 기존 동작 유지.
#pragma warning disable 108,114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;   // PlayerInventory.ItemData, EquipmentRarityData

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit — 아이템 상세창 (F4 Figma DetailPanel 표준 재생).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 요구사항(2026-09-18): I키 → 인벤창(좌)+설명창(중앙) 쌍 표시.
    /// 인벤 슬롯 클릭 → ShowItem(item, count)로 갱신.
    ///
    /// [F4 DetailPanel 표준] Figma DetailPanel(480×608):
    ///   PanelHeader('상세 정보'/'SPECIFICATIONS') + ItemNameSection(ItemName/TierBadge) +
    ///   ItemImageSection(TierStrip 4px + 클래스 텍스트 + ItemImage 320px) +
    ///   StatsGrid(등급/수량/카테고리/내구도/세트 — SpecBox 2~3칸) + DescriptionSection.
    /// GitHub-dark 팔레트(이 창 한정 인라인). 기능 경로 무수정.
    /// </summary>
    public class ItemDescriptionWindowUTK : UTKWindowBase
    {
        private const float WinW = 480f;
        private const float WinH = 608f;

        private static ItemDescriptionWindowUTK _instance;
        public static ItemDescriptionWindowUTK Instance => _instance;

        // [134차 F] StatRow 값 폰트 — Geist Mono(Figma 패밀리 계약). 에셋 미보유 실측(2026-10-24) → UIFont 폴백,
        // 없는 폰트를 흉내로 채우지 않는 위조 금지 계약(WorldMapWindowUTK 선례). Resources/Fonts/GeistMono 추가 시 자동 적용.
        private static Font _geistMono;

        private static Font GetGeistMonoFont()
        {
            if (_geistMono == null)
                _geistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _geistMono != null ? _geistMono : UIFont.Load();
        }

        // ===== GitHub-dark 팔레트 (이 창 한정) =====
        private static class Dark
        {
            public static readonly Color Bg       = Hex(0x0B0E14);
            public static readonly Color Panel    = Hex(0x161B22);
            public static readonly Color Sub      = Hex(0x21262D);
            public static readonly Color Inset    = Hex(0x1C2128);
            public static readonly Color Accent   = Hex(0x58A6FF);
            public static readonly Color Gold     = Hex(0xE3B341);
            public static readonly Color TextMain = Hex(0xF0F6FC);
            public static readonly Color TextSub  = Hex(0x8B949E);
            public static readonly Color Stroke   = Hex(0x2E343D);
            // [134차 F] crafting-panel 63:101 템플릿 토큰 — docs/134_FIGMA_4FRAMES_TRUTH.md ④
            public static readonly Color BadgeBg    = new Color32(0xFF, 0xFF, 0xFF, 0x1F);   // #FFFFFF@0.122 (bg 기준색 미측정 — 알파만 확정)
            public static readonly Color StrokeHalf  = new Color32(0x30, 0x36, 0x3D, 0x80);  // #30363D@0.502 (Divider/배지 스트로크 동일 토큰)
            public static readonly Color StatRed     = Hex(0xFF7B72);
            public static readonly Color StatGreen   = Hex(0x56D364);
            public static readonly Color StatPurple  = Hex(0xBC8CFF);

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        private static Color Hex(uint rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

        // ===== 노드 =====
        private readonly Label _itemName;        // ItemNameSection — 아이템명
        private readonly Label _itemEngName;     // [136차 B2-D] ItemNameSection — 영문명(14.4/400 #8B949E)
        private readonly Label _tierClass;       // TierBadge — "전설 CLASS"
        private readonly VisualElement _tierStrip;   // ItemImageSection 상단 4px 등급 스트립
        private readonly VisualElement _iconPreview; // ItemImage 320px
        private readonly Label _spec1Label; private readonly Label _spec1Val;
        private readonly Label _spec2Label; private readonly Label _spec2Val;
        private readonly Label _spec3Label; private readonly Label _spec3Val;
        private readonly Label _itemDesc;        // DescriptionSection 본문
        private readonly VisualElement _headerSection;
        private readonly VisualElement _nameSection;
        private readonly VisualElement _imageSection;
        private readonly VisualElement _specsSection;
        private readonly VisualElement _descriptionSection;

        private bool _isApplyingDetailLayout;
        private bool _isHandlingRootGeometry;
        private VisualElement _geometryRoot;

        private ItemDescriptionWindowUTK() : base("상세 정보", new Vector2(WinW, WinH))
        {

            // ── PanelHeader: subtitle 'SPECIFICATIONS' — 타이틀(창 크롬)은 이미 '상세 정보'  ──
            _headerSection = new VisualElement { name = "DetailHeaderSection" };
            _headerSection.style.flexDirection = FlexDirection.Row;
            _headerSection.style.alignItems = Align.Center;
            Content.Add(_headerSection);
            var subtitle = new Label("SPECIFICATIONS");
            // [Figma 15:4 헤더 진실치] 영문 부제 14.4/400, #8B949E(=Dark.TextSub), 제목 우측 +12px.
            //   fontSize 13.2 → 14.4 교정. 색(#8B949E)/웨이트(400 기본)는 기존값이 진실치와 동일.
            //   위치: 제목은 크롬 TitleBar('상세 정보'), 부제는 DetailHeader 리전(패널-로컬 (24,24) 기점) 소속이라
            //   서로 다른 트리 가지 — 절대좌표 +12px 배치 산출 불가 → 리전 기준 배치 유지(수치 교정만 원칙).
            subtitle.style.fontSize = 14.4f;
            subtitle.style.color = Dark.TextSub;
            subtitle.style.flexGrow = 1f;
            _headerSection.Add(subtitle);

            // ── ItemNameSection: 이름 + 영문 + Tier 배지 ──
            // [136차 B2-D] 내부 (14.4,14.4) 진실치 — 이름 21.6/700 #F0F6FC + 영문 14.4/400 #8B949E.
            _nameSection = new VisualElement { name = "DetailNameSection" };
            _nameSection.style.flexDirection = FlexDirection.Row;
            _nameSection.style.alignItems = Align.Center;
            _nameSection.style.paddingLeft = 14.4f; _nameSection.style.paddingRight = 14.4f;
            _nameSection.style.paddingTop = 14.4f; _nameSection.style.paddingBottom = 14.4f;
            // [144차 P1-INV][C7] 3섹션 카드 공통 크롬 — Figma 22:3~22:5 r9.6 + stroke #30363D@0.502 1.2
            //   + fill #161B22@0.702(알파 재검증 확정). name섹션 무스타일 해소 — [144차 P0 V2/V8] 어휘.
            _nameSection.style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);
            UTKTheme.ApplyElementBorder(_nameSection);
            UTKTheme.ApplyRadius(_nameSection, 9.6f);
            Content.Add(_nameSection);

            var nameStack = new VisualElement { name = "DetailNameStack" };
            nameStack.style.flexDirection = FlexDirection.Column;
            nameStack.style.flexGrow = 1f;
            _nameSection.Add(nameStack);

            _itemName = new Label("아이템을 선택하세요");
            _itemName.style.fontSize = 21.6f;   // [134차 F] 진실치 이름 21.6/700(구 24)
            _itemName.style.color = Dark.TextMain;
            _itemName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _itemName.style.whiteSpace = WhiteSpace.Normal;
            nameStack.Add(_itemName);

            _itemEngName = new Label("");
            _itemEngName.style.fontSize = 14.4f;   // [136차 B2-D] 진실치 영문 14.4/400 #8B949E
            _itemEngName.style.color = Dark.TextSub;
            _itemEngName.style.unityFontStyleAndWeight = FontStyle.Normal;
            _itemEngName.style.whiteSpace = WhiteSpace.Normal;
            nameStack.Add(_itemEngName);

            _tierClass = new Label("");
            _tierClass.style.fontSize = 13.2f;
            _tierClass.style.color = Dark.Gold;
            _tierClass.style.backgroundColor = Dark.BadgeBg;   // [134차 F] TierBadge bg@0.122 — 진실치 ④(기준색 미측정, #FFFFFF@0.122 적용)
            _tierClass.style.borderTopLeftRadius = 4.8f; _tierClass.style.borderTopRightRadius = 4.8f;   // [134차 F] 진실치 r4.8
            _tierClass.style.borderBottomLeftRadius = 4.8f; _tierClass.style.borderBottomRightRadius = 4.8f;
            _tierClass.style.borderTopWidth = 1.2f; _tierClass.style.borderBottomWidth = 1.2f;   // [134차 F] 진실치 stroke w1.2
            _tierClass.style.borderLeftWidth = 1.2f; _tierClass.style.borderRightWidth = 1.2f;
            _tierClass.style.borderTopColor = _tierClass.style.borderBottomColor =
            _tierClass.style.borderLeftColor = _tierClass.style.borderRightColor = new StyleColor(Dark.StrokeHalf);   // #30363D@0.502
            _tierClass.style.paddingTop = 3f; _tierClass.style.paddingBottom = 3f;
            _tierClass.style.paddingLeft = 10f; _tierClass.style.paddingRight = 10f;
            _tierClass.style.unityFontStyleAndWeight = FontStyle.Bold;   // [134차 F] 진실치 13.2/700 — Bold 명시
            _tierClass.style.marginLeft = 8f;
            // [136차 B2-D] TierBadge (451,25.5) 48.2×24.6 — 우측 여백 총 28.8(=528−499.2, 내부 pad 14.4 + 배지 우측 14.4),
            //   높이 24.6 고정(텍스트 세로중앙). 폭은 텍스트 의존 auto(48.2는 Figma 샘플 문자열 기준).
            _tierClass.style.height = 24.6f;
            _tierClass.style.unityTextAlign = TextAnchor.MiddleCenter;
            _tierClass.style.marginRight = 14.4f;
            _nameSection.Add(_tierClass);

            // ── ItemImageSection: TierStrip + 이미지 320px (세로) ──
            _imageSection = new VisualElement { name = "DetailImageSection" };
            // [136차 B2-D 판단 주석] ItemImageSection 채움 — 63:101 템플릿 #FFF@0.012 vs 15:141 인스턴스(22:4)
            //   #161B22@0.702. "15:141 인스턴스 값 우선" 원칙에 따라 인스턴스 진실치 #161B22@0.702를 명시 적용.
            //   (기존 구현은 무채움→창 배경(#161B22 불투명) 계승이었다 — 렌더 변화 최소, 진실치 명시화 목적.)
            // [144차 P1-INV][C7] — 기존 bg 값(0x16,0x1B,0x22,0xB3)은 UTKTheme.GlassPanelFill과 동일 → 단일소스화.
            //   r미설정+무테두리 해소 — r9.6 + 크롬 스트로크 1.2 적용(흐름자식 TierStrip/아이콘은 보더 1.2만큼
            //   안쪽으로 인셋 — Figma 자동레이아웃 보더 포함 의미와 동일, ≤1.2px).
            _imageSection.style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);   // 0.702
            UTKTheme.ApplyElementBorder(_imageSection);
            UTKTheme.ApplyRadius(_imageSection, 9.6f);
            _imageSection.style.overflow = Overflow.Hidden;
            Content.Add(_imageSection);

            _tierStrip = new VisualElement();
            _tierStrip.style.height = 4f;
            _tierStrip.style.marginBottom = 4f;
            _imageSection.Add(_tierStrip);

            _iconPreview = new VisualElement();
            _iconPreview.style.height = 376f;
            _iconPreview.style.backgroundColor = Dark.Inset;
            _iconPreview.style.borderTopWidth = 1; _iconPreview.style.borderBottomWidth = 1;
            _iconPreview.style.borderLeftWidth = 1; _iconPreview.style.borderRightWidth = 1;
            _iconPreview.style.borderTopColor = _iconPreview.style.borderBottomColor =
            _iconPreview.style.borderLeftColor = _iconPreview.style.borderRightColor = new StyleColor(Dark.Stroke);
            _iconPreview.style.borderTopLeftRadius = 6f; _iconPreview.style.borderTopRightRadius = 6f;
            _iconPreview.style.borderBottomLeftRadius = 6f; _iconPreview.style.borderBottomRightRadius = 6f;
            _iconPreview.style.alignSelf = Align.Center;
            _iconPreview.style.width = 528f;
            _iconPreview.style.alignSelf = Align.FlexStart;
            _imageSection.Add(_iconPreview);

            // ── StatsGrid: SpecBox 3칸 (등급/수량/기타) ──
            _specsSection = new VisualElement { name = "DetailSpecsSection" };
            _specsSection.style.flexDirection = FlexDirection.Column;   // [134차 F] StatRow 세로 스택 — SPACE_BETWEEN 행 3개(구 SpecBox 가로 3칸)
            _specsSection.style.width = Length.Percent(100f);
            _specsSection.style.marginBottom = 14.4f;   // [134차 F] 블록 gap 14.4(진실치 ④)

            // [136차 B2-D] StatRow 행 pitch 29.6 = h20 + gap 9.6(마지막 행 여백 없음 — 진실치).
            BuildSpecBox(_specsSection, "등급", out _spec1Label, out _spec1Val, 9.6f);
            BuildSpecBox(_specsSection, "수량", out _spec2Label, out _spec2Val, 9.6f);
            BuildSpecBox(_specsSection, "카테고리", out _spec3Label, out _spec3Val);

            // ── DescriptionSection ──
            // [134차 F] StatRow 값 색 — 템플릿 고정(#FF7B72/#56D364/#BC8CFF, 진실치 문서 ④)
            _spec1Val.style.color = new StyleColor(Dark.StatRed);
            _spec2Val.style.color = new StyleColor(Dark.StatGreen);
            _spec3Val.style.color = new StyleColor(Dark.StatPurple);
            _descriptionSection = new VisualElement { name = "DetailDescriptionSection" };
            // [144차 P1-INV][C7] desc섹션 — r6→진실치 9.6 + 무테두리→크롬 1.2 + bg #1C2128(Dark.Inset)→
            //   22:5 진실치 #161B22@0.702(GlassPanelFill — 22:3~22:5 공통 fill 재검증 확정).
            _descriptionSection.style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);
            UTKTheme.ApplyElementBorder(_descriptionSection);
            UTKTheme.ApplyRadius(_descriptionSection, 9.6f);
            _descriptionSection.style.paddingTop = 19.2f; _descriptionSection.style.paddingBottom = 19.2f;   // [134차 F] 진실치 pad19.2
            _descriptionSection.style.paddingLeft = 19.2f; _descriptionSection.style.paddingRight = 19.2f;
            Content.Add(_descriptionSection);
            // [134차 F] 자식 순서 정합 — Heading → StatRow → Divider → BioText(_specsSection는 descHead 뒤에 부착)

            var descHead = new Label("아이템 설명");
            descHead.style.fontSize = 15.6f;   // [134차 F] 진실치 Heading #58A6FF 15.6/700
            descHead.style.color = Dark.Accent;
            descHead.style.unityFontStyleAndWeight = FontStyle.Bold;
            descHead.style.marginBottom = 14.4f;   // [134차 F] 블록 gap 14.4
            _descriptionSection.Add(descHead);

            // [134차 F] 템플릿 순서 정합 — Heading 아래 StatRow 3행 + Divider 1.2 #30363D@0.502
            _descriptionSection.Add(_specsSection);
            var divider = new VisualElement();
            divider.style.height = 1.2f;
            divider.style.backgroundColor = new StyleColor(Dark.StrokeHalf);
            divider.style.marginBottom = 14.4f;
            _descriptionSection.Add(divider);

            _itemDesc = new Label("");
            _itemDesc.style.fontSize = 15.6f;   // [134차 F] 진실치 BioText 15.6/400 #8B949E
            _itemDesc.style.color = Dark.TextSub;
            _itemDesc.style.whiteSpace = WhiteSpace.Normal;
            _descriptionSection.Add(_itemDesc);

            ApplyF4GitHubDarkStyle();   // (창 크롬 GitHub-dark)
            // [144차 P1-INV C10] Figma 15:142~15:153 코너 데칼 4개 — overflow:hidden으로 안쪽 모드(박스 (0,0) 기점 L12 대비 inset 근사 기록)
            UTKTheme.AddCornerDecals(this, 14.4f, 1.8f, "#58A6FF", outsideCorner: false);
            ApplyUIToolkitFont(this);
            RegisterCallback<AttachToPanelEvent>(OnDetailAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetailDetached);
            RegisterCallback<GeometryChangedEvent>(OnDetailGeometryChanged);

            style.display = DisplayStyle.None;
        }

        private void ApplyDetailBodyLayout()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null || panel == null || Content.panel == null || panel != Content.panel || panel != root.panel || _isApplyingDetailLayout)
                return;

            Vector2 rootSize = new Vector2(root.resolvedStyle.width, root.resolvedStyle.height);
            if (rootSize.x <= 0f || rootSize.y <= 0f || float.IsNaN(rootSize.x) || float.IsNaN(rootSize.y))
                return;

            _isApplyingDetailLayout = true;
            try
            {
                // [136차 B2-D 이중 스케일 수리] Content(designSpace)는 ApplyDesignSpace에서 이미 scale=k를 지닌다
                //   (Figma 정합 v3 등배수 계약 — 디자인공간은 raw Figma px 유지 + scale=k). 그러므로 섹션 rect는
                //   raw 패널-로컬(ToPanelLocal)을 그대로 Content-로컬에 대입해야 ×k가 한 번만 적용된다.
                //   구 경로(ScaleDetailRegion = ScaleRect ×k)가 디자인공간 스케일과 곱해져 528×0.75²=297 렌더의
                //   원인이었다(134차 F 설명창 기하 로그 — nameSection 화면폭 297).
                Rect header = InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.DetailHeader, InventoryClusterFigmaLayout.DetailBounds);
                Rect name = InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.DetailItemName, InventoryClusterFigmaLayout.DetailBounds);
                Rect image = InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.DetailItemImage, InventoryClusterFigmaLayout.DetailBounds);
                Rect description = InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.DetailDescription, InventoryClusterFigmaLayout.DetailBounds);

                // Figma region coordinates are window-local. Translate to Content-local only after attachment,
                // using the actual direct child's layout offset instead of ChangeCoordinatesTo.
                // [136차 B2-D] Content.layout은 창(박스 432×547.2) 유닛 — raw 디자인공간 오프셋으로 변환하려면
                //   등배수 k로 나눈다(섹션 좌표는 Content-로컬 raw px).
                float k = FigmaCanvasLayout.DesignScale(rootSize);
                Vector2 contentOrigin = new Vector2(Content.layout.x, Content.layout.y) / Mathf.Max(k, 0.0001f);
                SetSectionRect(_headerSection, header, contentOrigin);
                SetSectionRect(_nameSection, name, contentOrigin);
                SetSectionRect(_imageSection, image, contentOrigin);
                SetSectionRect(_descriptionSection, description, contentOrigin);
                _iconPreview.style.width = image.width;
                _iconPreview.style.height = Mathf.Max(0f, image.height - _tierStrip.resolvedStyle.height - _tierStrip.resolvedStyle.marginBottom);
            }
            finally
            {
                _isApplyingDetailLayout = false;
            }
        }

        /// <summary>[134차 F] 설명창 기하 실측 — 셸(진실치 576×729.6 상당)·이름/설명 섹션·이름·TierBadge 바운스.
        /// 대조 문서: docs/134_FIGMA_4FRAMES_TRUTH.md ④(crafting-panel 63:101 템플릿).</summary>
        private void LogDescriptionGeometry(string phase)
        {
            var nameSec = Content != null ? Content.Q("DetailNameSection") : null;
            var descSec = Content != null ? Content.Q("DetailDescriptionSection") : null;
            Debug.Log($"[134차 F] 설명창 기하({phase}) — winBound={worldBound} display={style.display.value} " +
                      $"winStyle=({style.width.value.value:F1}x{style.height.value.value:F1}) " +
                      $"nameSection={(nameSec != null ? nameSec.worldBound.ToString() : "미발견")} " +
                      $"descSection={(descSec != null ? descSec.worldBound.ToString() : "미발견")} " +
                      $"itemName={(_itemName != null ? _itemName.worldBound.ToString() : "null")} " +
                      $"tierBadge={(_tierClass != null ? _tierClass.worldBound.ToString() : "null")}");
        }

        private static void SetSectionRect(VisualElement section, Rect rect, Vector2 contentOrigin)
        {
            section.style.position = Position.Absolute;
            section.style.left = rect.x - contentOrigin.x;
            section.style.top = rect.y - contentOrigin.y;
            section.style.width = rect.width;
            section.style.height = rect.height;
        }

        private void OnDetailAttached(AttachToPanelEvent evt)
        {
            RegisterRootGeometryCallback(UIToolkitBootstrap.UIRoot);
            if (IsOpen)
            {
                InventoryClusterFigmaLayout.ApplyDetail(this, UIToolkitBootstrap.UIRoot);
                ApplyDetailBodyLayout();
            }
        }

        private void OnDetailDetached(DetachFromPanelEvent evt)
        {
            UnregisterRootGeometryCallback();
        }

        private void RegisterRootGeometryCallback(VisualElement root)
        {
            if (root == null || panel == null || root.panel != panel || _geometryRoot == root)
                return;
            UnregisterRootGeometryCallback();
            _geometryRoot = root;
            _geometryRoot.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        private void UnregisterRootGeometryCallback()
        {
            if (_geometryRoot == null)
                return;
            _geometryRoot.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            _geometryRoot = null;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            var root = _geometryRoot;
            if (root == null || evt.target != root || !IsOpen || _isApplyingDetailLayout || _isHandlingRootGeometry)
                return;
            if (panel == null || panel != root.panel || Content.panel != panel)
                return;

            _isHandlingRootGeometry = true;
            try
            {
                InventoryClusterFigmaLayout.ApplyDetail(this, Content, root);   // [Figma 정합 v3]
                ApplyDetailBodyLayout();
            }
            finally
            {
                _isHandlingRootGeometry = false;
            }
        }

        private void OnDetailGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyDetailBodyLayout();
        }

        private void BuildSpecBox(VisualElement parent, string label, out Label lbl, out Label val, float rowBottomMargin = 0f)
        {
            var box = new VisualElement();
            box.style.width = Length.Percent(100f);   // [134차 F] StatRow — 라벨 좌/값 우 SPACE_BETWEEN
            box.style.flexDirection = FlexDirection.Row;
            box.style.justifyContent = Justify.SpaceBetween;
            box.style.height = 20f;                   // [136차 B2-D] 진실치 StatRow h20
            box.style.marginBottom = rowBottomMargin; // [136차 B2-D] 행 pitch 29.6(h20+gap 9.6)
            parent.Add(box);

            lbl = new Label(label);
            lbl.style.fontSize = 15.6f;   // [134차 F] 진실치 StatRow 라벨 15.6/400 #8B949E
            lbl.style.color = Dark.TextSub;
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(lbl);

            val = new Label("—");
            val.style.fontSize = 15.6f;   // [134차 F] 진실치 StatRow 값 Geist Mono 700/15.6
            val.style.unityFontStyleAndWeight = FontStyle.Bold;
            val.style.unityFont = GetGeistMonoFont();
            val.style.color = Dark.TextMain;
            val.style.unityTextAlign = TextAnchor.MiddleRight;   // [136차 B2-D] 진실치 값 우측정렬
            box.Add(val);
        }

        // ===== F4 GitHub-dark 창 크롬 =====
        private void ApplyF4GitHubDarkStyle()
        {
            style.backgroundColor = Dark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            // [144차 P1-INV][C8] 창 박스 크롬 — Figma 15:141 r14.4 + stroke #30363D@0.502 1.2.
            //   구 134차 r8 / stroke 1(#2E343D 불투명) 폐기 — [144차 P0 V2] ApplyElementBorder/ApplyRadius 어휘.
            UTKTheme.ApplyElementBorder(this);
            UTKTheme.ApplyRadius(this, UTKTheme.GlassRadius);
            // [Figma 정합 v3] min 가드 제거 — 구 고정 크기(480×608)가 디자인 박스(432×547.2)를
            // 밟아 상세창이 11% 과대 렌더+창고 겹침의 원인. ApplyDesignSpace가 박스=min을 보장.
            style.color = Dark.TextMain;

            var titleBar = this.Q("TitleBar");
            if (titleBar != null)
            {
                titleBar.style.backgroundColor = Dark.Sub;
                titleBar.style.borderTopLeftRadius = 8f; titleBar.style.borderTopRightRadius = 8f;
                titleBar.style.borderBottomWidth = 1f;
                titleBar.style.borderBottomColor = Dark.Stroke;
            }
            if (_titleLabel != null)
            {
                // [Figma 15:4 헤더 진실치] 제목 24/700, #F0F6FC(=Dark.TextMain), 패널-로컬 (24, 29.2).
                //   before: fontSize = USS var(--fs-lg) 38 → 진실치 24 교정. Bold(700)는 USS 유지(이미 진실치).
                //   크롬 TitleBar flex 구조상 (24, 29.2) 절대배치 불가(공유 크롬 불변 제약) → fontSize/색만 교정.
                _titleLabel.style.fontSize = 24f;
                _titleLabel.style.color = Dark.TextMain;
            }

            var closeBtn = this.Q<Button>("CloseButton");
            if (closeBtn != null)
            {
                // [144차 P1-INV C9] Figma 21:6 CloseBtn — 33.6×33.6, #161B22@0.702, 테두리 #30363D@0.502 1.2, r7.2
                closeBtn.style.width = 33.6f; closeBtn.style.height = 33.6f;
                closeBtn.style.marginTop = -2.8f; closeBtn.style.marginBottom = -2.8f; // 크롬 바 높이 불변 가드
                closeBtn.style.backgroundColor = UTKTheme.GlassPanelFill;
                UTKTheme.ApplyElementBorder(closeBtn);
                UTKTheme.ApplyRadius(closeBtn, 7.2f);
                closeBtn.style.color = Dark.TextMain;
            }
        }

        // ===== 공개 API (기존 동작 유지) =====

        public static ItemDescriptionWindowUTK Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new ItemDescriptionWindowUTK();
            root.Add(_instance);
            Debug.Log("[DescUTK] 설명창 인스턴스 생성 (F4 DetailPanel)");
            return _instance;
        }

        public void PlaceCenterColumn()
        {
            UTKThreeColumnLayout.Place(this, 1);
        }

        protected override void OnWindowOpen()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && panel != null && panel == root.panel)
            {
                InventoryClusterFigmaLayout.ApplyDetail(this, Content, root);   // [Figma 정합 v3]
                ApplyDetailBodyLayout();
            }
        }

        public static void Open()
        {
            var i = Ensure();
            if (i == null) return;
            if (!i.IsOpen) i.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && i.panel != null && i.panel == root.panel)
            {
                // [Figma 정합 v3] I키/창고 진입 경로도 디자인공간 계약으로 — 구 2인자 계약(독립 X/Y 스케일)이
                // v3 기하를 덮어써 상세창 과대+창고 겹침을 유발했다.
                InventoryClusterFigmaLayout.ApplyDetail(i, i.Content, root);
                i.ApplyDetailBodyLayout();
                // [134차 F] 개방즉시+250ms 기하 로그(RestaurantWindowUTK [118차 R1] 패턴 준용)
                i.LogDescriptionGeometry("개방즉시");
                i.schedule.Execute(() => i.LogDescriptionGeometry("250ms")).StartingIn(250);
            }
            i.BringToFront();
        }

        public void CenterOnScreen()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float pw = root.resolvedStyle.width, ph = root.resolvedStyle.height;
            if (pw <= 0f || ph <= 0f) { pw = 1920f; ph = 1080f; }
            float w = style.width.value.value, h = style.height.value.value;
            style.left = Mathf.Max(0f, (pw - w) * 0.5f);
            style.top = Mathf.Max(0f, (ph - h) * 0.5f);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            if (_instance.IsOpen) _instance.Close();
        }

        public static void SyncWithInventory(bool inventoryOpen)
        {
            if (inventoryOpen) Open();
            else Hide();
        }

        /// <summary>아이템 표시 — 인벤 슬롯 클릭 시 호출. F4: 등급/수량/카테고리/내구도/세트를 StatsGrid·TierBadge·설명으로.</summary>
        public static void ShowItem(PlayerInventory.ItemData item, int count)
        {
            var i = Ensure();
            if (i == null || item == null) return;

            var icon = ItemIconDatabase.GetOrCreateIcon(item);
            i._iconPreview.style.backgroundImage = UTKTextureSafe.ToBackground(icon);
            i._itemName.text = item.displayName ?? item.id;
            // [136차 B2-D] 영문명 라인 — ItemData.id(영문 식별자) 표기(진실치 14.4/400 #8B949E).
            i._itemEngName.text = string.IsNullOrEmpty(item.id) ? "" : item.id;

            // 등급 색 + Tier 배지 + 스트립
            Color rc = EquipmentRarityData.GetRarityColor(item.rarity);
            string rarityName = EquipmentRarityData.GetRarityDisplayName(item.rarity);
            i._itemName.style.color = rc == Dark.TextMain ? Dark.TextMain : rc;
            i._tierClass.text = string.IsNullOrEmpty(rarityName) ? "" : $"{rarityName} CLASS";
            i._tierClass.style.color = rc;
            // [136차 B2-D] TierBadge bg=티어색@0.122 / stroke=티어색(63:101 진실치 — 구 #FFFFFF@0.122·#30363D@0.502 교체).
            i._tierClass.style.backgroundColor = new Color(rc.r, rc.g, rc.b, 31f / 255f);
            i._tierClass.style.borderTopColor = i._tierClass.style.borderBottomColor =
            i._tierClass.style.borderLeftColor = i._tierClass.style.borderRightColor = new StyleColor(rc);
            i._tierStrip.style.backgroundColor = rc;

            // StatsGrid — 3칸: 등급 / 수량 / 카테고리
            i._spec1Val.text = string.IsNullOrEmpty(rarityName) ? item.rarity.ToString() : rarityName;
            // [134차 F] 등급 값 색 — 63:101 템플릿 고정(#FF7B72). 희귀도색(rc)은 이름/배지/스트립이 담당.
            i._spec1Val.style.color = new StyleColor(Dark.StatRed);
            i._spec2Val.text = count.ToString();
            i._spec3Val.text = item.category.ToString();

            // 세트/내구도는 설명 하단에 병기 (C-O1-04 세트 보너스 보존)
            var sb = new System.Text.StringBuilder();
            if (item.maxDurability > 0) sb.AppendLine($"내구도: {item.maxDurability}");
            var setKind = EquipmentTierSet.GetSetForItem(item.id);
            if (setKind.HasValue)
            {
                var completion = EquipmentTierSet.GetActiveSetBonus(setKind.Value, 4);
                sb.AppendLine($"세트: {EquipmentTierSet.GetSetDisplayName(setKind.Value)} — {completion.description}");
            }
            string extra = sb.ToString();
            i._itemDesc.text = (item.description ?? "") + (extra.Length > 0 ? "\n" + extra : "");

            if (!i.IsOpen) Open();
            Debug.Log($"[DescUTK] 설명 갱신 — {item.displayName ?? item.id} x{count}");
        }

        public static void Clear()
        {
            if (_instance == null) return;
            _instance._itemName.text = "아이템을 선택하세요";
            _instance._itemName.style.color = Dark.TextMain;
            _instance._itemEngName.text = "";
            _instance._tierClass.text = "";
            // [136차 B2-D] 배지 기본 스킨 복원(ShowItem에서 티어색 bg/stroke로 교체됨).
            _instance._tierClass.style.backgroundColor = Dark.BadgeBg;
            _instance._tierClass.style.borderTopColor = _instance._tierClass.style.borderBottomColor =
            _instance._tierClass.style.borderLeftColor = _instance._tierClass.style.borderRightColor = new StyleColor(Dark.StrokeHalf);
            _instance._spec1Val.text = "—";
            _instance._spec2Val.text = "—";
            _instance._spec3Val.text = "—";
            _instance._itemDesc.text = "";
        }
    }
}
