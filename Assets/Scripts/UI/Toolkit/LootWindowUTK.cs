using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // ILootBasket, LootEntry, PlayerInventory
using ProjectName.UI;              // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit — 전리품 윈도우 (Figma loot-panel 정합 개편).
    /// Figma 규격: 5열×2행 그리드 + 서브헤더(습득가능 배지 + 획득 n/10) + 풋터(전부 습득/닫기).
    /// 기존 UTK Phase U2 Round 2A 로직(빈바구니 자동Hide·드래그→인벤·우클릭 획득) 100% 보존.
    ///  ① 항목 그리드  — ILootBasket.Items의 각 LootEntry를 5열 그리드 슬롯(아이콘+카운트)으로 표시. 매 갱신 재조회.
    ///  ② 슬롯 드래그 → 인벤 — IUTKDragSource 구현: 좌클릭 드래그가 UTKDragPayload(SourceKind.Loot, SourceIndex=항목 인덱스) 생성.
    ///                         InventoryWindowUTK가 수신해 PlayerInventory.AddItem 수행.
    ///  ③ 슬롯 우클릭 획득 — TakeSelectedItem(→ ILootBasket.TakeItem → PlayerInventory.AddItem) 동일 데이터 경로.
    ///  ⑤ 변화 폴링 갱신    — 바구니 항목 배열을 주기 재조회해 즉시 갱신.
    ///  ⑥ 빈바구니 처리     — 바구니가 비어있거나 회수 불가하면 자동 Hide + 참조 해제.
    /// </summary>
    public class LootWindowUTK : UTKWindowBase, IUTKDragSource, IUTKDropTarget
    {
        // ===== 싱글턴 / 팩토리 =====
        private static LootWindowUTK _instance;
        public static LootWindowUTK Instance => _instance;

        /// <summary>팩토리 — 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new LootWindowUTK();
        }

        /// <summary>특정 바스켓 열기 (원본 OpenForBasket과 동일 역할 + 열기 보장).</summary>
        public static void Open(ILootBasket basket)
        {
            if (basket == null) return;
            Ensure();
            _instance.OpenForBasket(basket, null);
        }

        /// <summary>
        /// [Figma 정합 v3 / 조합 배치(사용자 확정)] 바구니 라우트 조합(인벤 좌측 + 전리품 우상단)용 위치 오버라이드.
        /// Figma 70:4 단독 캔버스는 전리품 중앙(708,324)이지만, 인벤(144,84,504,912)과 함께 열릴 때는
        /// 15:4 우측 컬럼(스토리지 위치)을 차용해 오른쪽 위에 배치한다.
        /// </summary>
        public static readonly Rect CompositionBounds = new Rect(1272f, 84f, 504f, 432f);

        public static void Open(ILootBasket basket, Rect canvasRectOverride)
        {
            if (basket == null) return;
            Ensure();
            _instance.OpenForBasket(basket, canvasRectOverride);
        }

        // ===== 설정 (Figma node 70:5; centered in 1920x1080 canvas) =====
        private const long RefreshMs = 250L;
        public const int GridColumns = 5;
        public const int VisibleSlotCount = 10;
        public const float SlotSize = 81.6f;
        public const float SlotGap = 9.6f;
        public const float GridViewportHeight = 172.8f;
        public static readonly Rect FigmaBounds = new Rect(708f, 324f, 504f, 432f);
        public static int GetRenderedSlotCount(int itemCount) => Mathf.Max(VisibleSlotCount, itemCount);
        public static int GetOverflowSlotCount(int itemCount) => Mathf.Max(0, itemCount - VisibleSlotCount);
        public static float GetGridContentHeight(int renderedSlotCount)
        {
            int rows = (Mathf.Max(0, renderedSlotCount) + GridColumns - 1) / GridColumns;
            return rows * SlotSize + Mathf.Max(0, rows - 1) * SlotGap;
        }
        public static float GetGridSlotBottomMargin(int slotOrdinal, int renderedSlotCount)
        {
            bool hasFollowingRow = slotOrdinal / GridColumns < (renderedSlotCount - 1) / GridColumns;
            return hasFollowingRow ? SlotGap : 0f;
        }

        /// <summary>등급 테두리 색 — Figma/GitHub-dark 팔레트(4~5=금/3=퍼플/1~2=액센트/0=보조).</summary>
        private static Color RankColor(int rarityIndex)
        {
            switch (rarityIndex)
            {
                case 0: return UTKTheme.TextSub;
                case 1:
                case 2: return UTKTheme.Accent;
                case 3: return new Color32(0xA3, 0x71, 0xF7, 0xFF);
                default: return UTKTheme.Gold;
            }
        }

        /// <summary>GitHub-dark 버튼 인라인 오버라이드(이 창 한정) — IStyle 쇼트핸드 없음 → 4면 개별 대입.</summary>
        private static void StyleButton(Button btn, UTKButton.Variant variant)
        {
            if (btn == null) return;
            Color baseBg, hoverBg, textColor;
            switch (variant)
            {
                case UTKButton.Variant.Primary:
                    baseBg = UTKTheme.Accent; hoverBg = UTKTheme.AccentHover; textColor = UTKTheme.BgBase; break;
                case UTKButton.Variant.Danger:
                    baseBg = UTKTheme.Danger; hoverBg = UTKTheme.DangerHover; textColor = UTKTheme.TextMain; break;
                default:
                    baseBg = UTKTheme.PanelSub; hoverBg = UTKTheme.Stroke; textColor = UTKTheme.TextMain; break;
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


        /// <summary>GitHub-dark 그리드 슬롯 — 다크 인셋 + 1px 스트로크 + r6 + 등급 상단 테두리 (이 창 한정).</summary>
        private static void ApplyDarkSlotStyle(VisualElement slot, int rarityIndex)
        {
            if (slot == null) return;
            slot.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            slot.style.backgroundColor = UTKTheme.BgBase;
            slot.style.borderTopWidth = 2f;   // 등급색 상단 강조선 (피그마 슬롯 상단 유색 선)
            slot.style.borderBottomWidth = 1f;
            slot.style.borderLeftWidth = 1f;
            slot.style.borderRightWidth = 1f;
            var rank = RankColor(rarityIndex);
            slot.style.borderTopColor = new StyleColor(rank);
            slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor = new StyleColor(UTKTheme.Stroke);
            slot.style.borderTopLeftRadius = 6f;
            slot.style.borderTopRightRadius = 6f;
            slot.style.borderBottomLeftRadius = 6f;
            slot.style.borderBottomRightRadius = 6f;   // 서브 반경 r6
        }

        // ===== 레퍼런스 =====
        private ILootBasket _basket;
        private Rect _activeBounds = FigmaBounds;   // [Figma 정합 v3] 기본=70:4 단독 좌표, 조합 개방 시 오버라이드
        private VisualElement _canvasLayoutRoot;         // [Figma 정합 v3] 등배수 스케일 소스(UIRoot)
        private readonly VisualElement _grid;            // 5열 그리드
        private readonly ScrollView _gridScroll;
        private readonly Label _countLabel;              // "획득 아이템: n / 10"
        private readonly Label _badgeLabel;              // "습득 가능" 배지
        private readonly Label _emptyLabel;
        private readonly Button _acquireAllBtn;          // 풋터 "전부 습득하기"
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private LootWindowUTK() : base("🎁 전리품", new Vector2(504f, 432f), UTKWindowChrome.Frameless)
        {
            // [Figma 70:4 loot-panel] 프레임리스 단일 패널 — pad 24 / 세로 gap 14.4 / 모서리 L 데칼 /
            // PanelHeader(전리품+LOOT SECURED+닫기 31.2) / 드래그 핸들=헤더.
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = _content.style.paddingRight = 24f;
            _content.style.paddingTop = _content.style.paddingBottom = 24f;
            // [Figma 정합 v3] 모서리 데칼은 디자인공간(_content, raw 504×432)에 부착 — raw 좌표 유지 + k 등비 스케일.
            AddCornerDecals(_content);

            // ── PanelHeader 456×48 ──
            var panelHeader = new VisualElement { name = "LootPanelHeader" };
            panelHeader.style.flexDirection = FlexDirection.Row;
            panelHeader.style.alignItems = Align.Center;
            panelHeader.style.height = 48f;
            panelHeader.style.minHeight = 48f;
            panelHeader.style.flexShrink = 0f;
            var lootTitle = MkTextLabel("전리품", UTKTheme.FontTitle, UTKTheme.TextMain);   // [Figma] 21.6/700
            lootTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            panelHeader.Add(lootTitle);
            var lootSub = MkTextLabel("LOOT SECURED", 11f, UTKTheme.TextSub);
            lootSub.style.marginLeft = 12f;
            lootSub.style.marginTop = 5f;
            lootSub.style.flexGrow = 1f;
            panelHeader.Add(lootSub);
            var headerClose = new Button(Hide) { text = "×", name = "LootHeaderCloseButton" };
            headerClose.style.width = 31.2f;
            headerClose.style.height = 31.2f;
            headerClose.style.flexShrink = 0f;
            StyleButton(headerClose, UTKButton.Variant.Secondary);
            panelHeader.Add(headerClose);
            _content.Add(panelHeader);
            SetDragHandle(panelHeader);

            // ── LootStats 456×26.6: '습득 가능' 배지(pad 9.6/4.8) + 우측 "획득 아이템: n / 10" ──
            var stats = new VisualElement { name = "LootStats" };
            stats.style.flexDirection = FlexDirection.Row;
            stats.style.alignItems = Align.Center;
            stats.style.height = 26.6f;
            stats.style.minHeight = 26.6f;
            stats.style.flexShrink = 0f;
            stats.style.marginBottom = 14.4f;
            _content.Add(stats);

            _badgeLabel = new Label("습득 가능") { name = "LootBadge" };
            _badgeLabel.style.backgroundColor = UTKTheme.Accent;
            _badgeLabel.style.color = UTKTheme.BgBase;
            _badgeLabel.style.fontSize = UTKTheme.FontBadge;   // [Figma] 12/700
            _badgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _badgeLabel.style.paddingTop = 4.8f;
            _badgeLabel.style.paddingBottom = 4.8f;
            _badgeLabel.style.paddingLeft = 9.6f;
            _badgeLabel.style.paddingRight = 9.6f;
            _badgeLabel.style.borderTopLeftRadius = 4f;
            _badgeLabel.style.borderTopRightRadius = 4f;
            _badgeLabel.style.borderBottomLeftRadius = 4f;
            _badgeLabel.style.borderBottomRightRadius = 4f;
            stats.Add(_badgeLabel);

            _countLabel = new Label("");
            _countLabel.style.fontSize = UTKTheme.FontBody;   // [Figma] 14.4/400
            _countLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            _countLabel.style.color = new StyleColor(UTKTheme.TextSub);
            _countLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _countLabel.style.flexGrow = 1f;
            stats.Add(_countLabel);

            // ── 그리드 본문 (5열 wrap, 로직 무수정) ──
            _gridScroll = new ScrollView(ScrollViewMode.Vertical);
            _gridScroll.name = "LootGridScroll";
            _gridScroll.style.height = GridViewportHeight;
            _gridScroll.style.minHeight = GridViewportHeight;
            _gridScroll.style.maxHeight = GridViewportHeight;
            _gridScroll.style.flexShrink = 0f;
            _grid = _gridScroll.contentContainer;
            _grid.name = "LootGrid";
            _grid.style.flexDirection = FlexDirection.Row;
            _grid.style.flexWrap = Wrap.Wrap;
            _grid.style.alignContent = Align.FlexStart;
            _grid.style.width = 456f;
            _content.Add(_gridScroll);

            _emptyLabel = new Label("(전리품이 없습니다)");
            _emptyLabel.style.fontSize = 14f;
            _emptyLabel.style.color = new StyleColor(UTKTheme.TextSub);
            _emptyLabel.style.marginTop = 12f;
            _content.Add(_emptyLabel);

            // ── LootFooter [Figma 456×61.2, gap 9.6]: 전부 습득 222×46.8 + 닫기 224.4×46.8 ──
            var footer = new VisualElement { name = "LootFooter" };
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.marginTop = 14.4f;
            footer.style.flexShrink = 0f;
            _content.Add(footer);

            _acquireAllBtn = new Button(AcquireAll);
            _acquireAllBtn.text = "전부 습득하기";
            _acquireAllBtn.style.width = 222f;
            _acquireAllBtn.style.height = 46.8f;
            _acquireAllBtn.style.marginRight = 9.6f;
            StyleButton(_acquireAllBtn, UTKButton.Variant.Primary);
            footer.Add(_acquireAllBtn);

            var closeBtnFooter = new Button(Hide);
            closeBtnFooter.text = "닫기";
            closeBtnFooter.style.width = 224.4f;
            closeBtnFooter.style.height = 46.8f;
            StyleButton(closeBtnFooter, UTKButton.Variant.Secondary);
            footer.Add(closeBtnFooter);

            ApplyUIToolkitFont(this);
            // [Figma 70:4 글래스] #161B22@0.85(사용자 조정) + #30363D@0.5 1.2px + r14.4
            style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = UTKTheme.GlassStrokeWidth;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new StyleColor(UTKTheme.GlassPanelStroke);
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = UTKTheme.GlassRadius;

            style.display = DisplayStyle.None;
            style.left = FigmaBounds.x;
            style.top = FigmaBounds.y;
        }

        /// <summary>[Figma 70:4] 패널 4모서리 L자 데칼(12선, 14.4 영역) — 시각 전용.</summary>
        private static void AddCornerDecals(VisualElement host)
        {
            float w = FigmaBounds.width, h = FigmaBounds.height;
            void Line(float left, float top, float lw, float lh)
            {
                var l = new VisualElement();
                l.style.position = Position.Absolute;
                l.style.left = left;
                l.style.top = top;
                l.style.width = lw;
                l.style.height = lh;
                l.style.backgroundColor = new StyleColor(UTKTheme.Stroke);
                l.pickingMode = PickingMode.Ignore;
                host.Add(l);
            }
            Line(0f, 0f, 12f, 1f); Line(0f, 0f, 1f, 12f);                       // TL
            Line(w - 12f, 0f, 12f, 1f); Line(w - 1f, 0f, 1f, 12f);              // TR
            Line(0f, h - 1f, 12f, 1f); Line(0f, h - 12f, 1f, 12f);              // BL
            Line(w - 12f, h - 1f, 12f, 1f); Line(w - 1f, h - 12f, 1f, 12f);     // BR
        }

        private static Label MkTextLabel(string text, float size, Color color)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            return l;
        }

        // =====================================================================
        //  공개 진입점 — 원본 OpenForBasket 바스켓 설정 경로
        // =====================================================================

        /// <summary>특정 바스켓 열기. 빈/회수 불가 바구니는 무시(원본 OpenForBasket 규약).</summary>
        public void OpenForBasket(ILootBasket basket) => OpenForBasket(basket, null);

        /// <summary>[Figma 정합 v3] 캔버스 rect 오버라이드 열기 — 조합 배치(우측 상단 컬럼)용.</summary>
        public void OpenForBasket(ILootBasket basket, Rect? canvasRectOverride)
        {
            if (basket == null || basket.IsEmpty || !basket.IsAvailable) return;
            _activeBounds = canvasRectOverride ?? FigmaBounds;
            _basket = basket;
            Show();
        }

        /// <summary>현재 바스켓 (외부/테스트용 읽기 전용).</summary>
        public ILootBasket Basket => _basket;

        // =====================================================================
        //  생명주기 (UTKWindowBase 훅)
        // =====================================================================

        public override void Show()
        {
            if (_basket == null) return;
            if (_basket.IsEmpty || !_basket.IsAvailable)
            {
                Hide();
                return;
            }
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
            ApplyFigmaPlacement(root);
            StartRefreshLoop();
            RefreshGrid();
            Debug.Log("[LootUTK] 전리품 창 열림 (" + (_basket != null ? _basket.BasketName : "?") + ")");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[LootUTK] 전리품 창 닫힘");
        }

        /// <summary>창이 UIRoot에 부착된 후 이 창을 드롭 타겟으로 등록 (소스 재드롭=취소 소비).</summary>
        protected override void OnWindowOpen()
        {
            UTKDragDrop.RegisterDropTarget(this, this);
        }

        /// <summary>창 닫힘 — 드롭 타겟 해제(재열림 시 재등록).</summary>
        protected override void OnWindowClosed()
        {
            UTKDragDrop.UnregisterDropTarget(this);
            _basket = null;
        }

        private void OnCanvasRootGeometryChanged(GeometryChangedEvent evt) => ApplyFigmaPlacement(_canvasLayoutRoot);

        /// <summary>
        /// [Figma 정합 v3] 등배수 디자인공간 — 창 박스=FigmaBounds×k, _content는 raw 504×432 유지 + scale=k.
        /// 기존 X/Y 독립 스케일(FigmaCanvasLayout.Apply → ScaleRect)은 계약에서 제거됐다.
        /// </summary>
        private void ApplyFigmaPlacement(VisualElement root)
        {
            if (root != null)
                FigmaCanvasLayout.ApplyDesignSpace(this, _content, _activeBounds, root);
        }

        // =====================================================================
        //  ⑤ 변화 폴링 갱신
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (UTKDragDrop.Active) return;   // [U8 수리] 드래그 중 재생성 금지 — 캡처 상실 차단
                if (IsOpen) RefreshGrid();
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
        //  ① 바구니 항목 그리드 — 매 갱신 재조회 (⑥ 빈바구니 자동 Hide)
        // =====================================================================

        /// <summary>[U8] 우클릭 즉시 획득 — 기존 TakeSelectedItem 데이터 경로.</summary>
        private void TakeLootRow(int index)
        {
            TakeSelectedItem(index);
        }

        private void RefreshGrid()
        {
            // ⑥ 원본 RefreshLoot 규약 — 바구니 없음/빈/회수 불가 → 참조 해제 + 닫힘
            if (_basket == null || _basket.IsEmpty || !_basket.IsAvailable)
            {
                _basket = null;
                _grid.Clear();
                if (IsOpen)
                    Hide();
                return;
            }

            var items = _basket.Items;
            int total = items != null ? items.Count : 0;
            int validOverflowCount = 0;
            for (int i = VisibleSlotCount; i < total; i++)
            {
                var entry = items[i];
                if (entry != null && entry.Item != null && entry.Count > 0)
                    validOverflowCount++;
            }
            int renderedSlotCount = VisibleSlotCount + validOverflowCount;

            _grid.Clear();

            for (int i = 0; i < VisibleSlotCount; i++)
            {
                if (items != null && i < total && items[i] != null && items[i].Item != null && items[i].Count > 0)
                    _grid.Add(BuildSlot(items[i], i, i, renderedSlotCount));
                else
                    _grid.Add(BuildEmptySlot(i, renderedSlotCount));
            }
            for (int i = VisibleSlotCount; i < total; i++)
            {
                var entry = items[i];
                if (entry == null || entry.Item == null || entry.Count <= 0) continue;
                _grid.Add(BuildSlot(entry, i, _grid.childCount, renderedSlotCount));
            }

            _countLabel.text = "획득 아이템: " + total + " / 10";
            Debug.Log("[LootUTK] 바구니 그리드 갱신: " + total + "개 항목");
        }

        private VisualElement BuildEmptySlot(int index, int renderedSlotCount)
        {
            var slot = new VisualElement { name = "LootSlotEmpty_" + index };
            slot.style.width = SlotSize;
            slot.style.height = SlotSize;
            slot.style.marginRight = SlotGap;
            slot.style.marginBottom = GetGridSlotBottomMargin(index, renderedSlotCount);
            slot.style.backgroundColor = UTKTheme.BgBase;
            slot.style.borderTopWidth = slot.style.borderBottomWidth = slot.style.borderLeftWidth = slot.style.borderRightWidth = 1f;
            slot.style.borderTopColor = slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor = new StyleColor(UTKTheme.Stroke);
            slot.style.borderTopLeftRadius = slot.style.borderTopRightRadius = 6f;
            slot.style.borderBottomLeftRadius = slot.style.borderBottomRightRadius = 6f;
            slot.pickingMode = PickingMode.Ignore;
            return slot;
        }

        private VisualElement BuildSlot(LootEntry entry, int itemIndex, int slotOrdinal, int renderedSlotCount)
        {
            var slot = new VisualElement();
            slot.name = "LootSlot_" + itemIndex;
            slot.style.width = SlotSize;
            slot.style.height = SlotSize;
            slot.style.marginRight = SlotGap;
            slot.style.marginBottom = GetGridSlotBottomMargin(slotOrdinal, renderedSlotCount);
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;
            ApplyDarkSlotStyle(slot, (int)entry.Item.rarity);   // [GitHub-dark] 슬롯 + 등급 상단 테두리

            // 아이콘 (중앙 32×32 — Figma 아이콘 규격)
            var icon = ItemIconDatabase.GetOrCreateIcon(entry.Item);
            var slotIcon = new VisualElement();
            slotIcon.style.width = 32f;
            slotIcon.style.height = 32f;
            slotIcon.style.alignSelf = Align.Center;
            slotIcon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            slotIcon.style.backgroundImage = UTKTextureSafe.ToBackground(icon);
            slot.Add(slotIcon);

            // 카운트 (우하단)
            var countLabel = new Label("x" + entry.Count);
            countLabel.style.position = Position.Absolute;
            countLabel.style.right = 4f;
            countLabel.style.bottom = 2f;
            countLabel.style.fontSize = 14f;   // Figma 카운트 14px
            countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            countLabel.style.color = new StyleColor(UTKTheme.Gold);   // 카운트 — 골드 의미색
            slot.Add(countLabel);

            // ② 슬롯 드래그 소스 (좌클릭) + 좌클릭 획득 없음/우클릭 획득
            // Overflow cells retain their basket index for drag and acquisition actions.
            UTKDragDrop.MakeDraggable(slot, () => MakePayload(itemIndex, entry.Item), null, () => TakeLootRow(itemIndex));

            return slot;
        }

        /// <summary>행 드래그 페이로드 — SourceKind.Loot, SourceIndex=바구니 항목 인덱스.</summary>
        private UTKDragPayload MakePayload(int index, PlayerInventory.ItemData item)
        {
            var p = new UTKDragPayload();
            p.Source = UTKDragSourceKind.Loot;
            p.SourceIndex = index;
            p.Item = item;
            p.Icon = item != null ? ItemIconDatabase.GetOrCreateIcon(item) : null;
            return p;
        }

        // =====================================================================
        //  IUTKDragSource — ② 드래그 시작 통지
        // =====================================================================

        public void BeginDrag(UTKDragPayload payload)
        {
            Debug.Log("[LootUTK] 슬롯 드래그 시작: " + (payload != null && payload.Item != null ? payload.Item.displayName : "?"));
        }

        // =====================================================================
        //  IUTKDropTarget — 이 창 위 드롭 = 소스 재드롭 소비(취소, 아이템 유지)
        // =====================================================================

        public bool CanDrop(UTKDragPayload payload)
        {
            return payload != null && payload.Item != null;
        }

        public bool Drop(UTKDragPayload payload)
        {
            if (payload == null || payload.Item == null) return false;
            // Loot 소스를 자기 창 위에 드롭 = 재드롭 취소(소비) — 아이템 유지. 인벤 이동은 InventoryWindowUTK가 담당.
            if (payload.Source == UTKDragSourceKind.Loot)
            {
                Debug.Log("[LootUTK] 전리품 소스 재드롭(창 위) — 취소 소비, 아이템 유지");
                return true;
            }
            return false;
        }

        // =====================================================================
        //  ③ 우클릭 즉시 획득 — 원본 TakeSelectedItem(→ ILootBasket.TakeItem → PlayerInventory.AddItem)
        // =====================================================================

        private void TakeSelectedItem(int index)
        {
            if (_basket == null) return;
            if (_basket.TakeItem(index))
            {
                Debug.Log("[LootUTK] 아이템 획득 완료 (우클릭)");
            }
            else
            {
                Debug.Log("[LootUTK] 아이템 획득 실패 (우클릭) — 인벤 가득 참 또는 바구니 소멸");
            }
            RefreshGrid();
        }

        /// <summary>풋터 "전부 습득하기" — 모든 항목 역순 TakeItem(Count 감소 대비).</summary>
        private void AcquireAll()
        {
            if (_basket == null) return;
            var items = _basket.Items;
            if (items == null) return;
            int n = items.Count;
            for (int i = n - 1; i >= 0; i--)
            {
                if (i < _basket.Items.Count)
                    TakeSelectedItem(i);
            }
            RefreshGrid();
            Debug.Log("[LootUTK] 전부 습득 요청 — " + n + "개 항목 처리");
        }
    }
}
