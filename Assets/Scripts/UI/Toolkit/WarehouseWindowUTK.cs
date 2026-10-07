using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // PlayerInventory
using ProjectName.Systems;         // WarehouseSystem
using ProjectName.Core.Data;       // TerritoryDatabase
using ProjectName.UI;              // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U3 Round A — 창고 윈도우 (인벤 ↔ 창고 양방향 DnD).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/WarehouseUI.cs (1102줄) + Assets/Scripts/UI/TerritoryWarehouse.cs (317줄).
    /// 원본은 절대 수정하지 않으며, 데이터 API는 실측 후 그대로 호출한다 (복제 금지).
    ///
    /// [레이아웃 관례 — 좌:인벤 / 우:창고]
    ///   좌측 컬럼  = 인벤토리 요약 그리드 (드래그 소스 + 창고→인벤 출고 드롭 타겟)
    ///   우측 컬럼  = 창고 그리드 (드래그 소스 + 인벤→창고 입고 드롭 타겟)
    ///
    /// [양방향 DnD]
    ///   ① 인벤 슬롯 드래그 → 우측(창고) 드롭 = 입고  — 원본 TryDepositFromDrag와 동일
    ///      (PlayerInventory.RemoveItem → WarehouseSystem.AddItem → 실패 시 인벤 롤백)
    ///   ② 창고 슬롯 드래그 → 좌측(인벤) 드롭 = 출고 — 원본 TransferDraggedToInventory와 동일
    ///      (WarehouseSystem.TransferToInventory)
    ///   ③ 행/슬롯 우클릭 = 반대편 즉시 이동 (입고/출고)
    ///
    /// [데이터 소스] territoryId별 WarehouseSystem.Instance.GetItems(territoryId) 실측 연동.
    /// [갱신] 250ms 폴링 + Open 시 RefreshGrid. 경로별 [WarehouseUTK] 로그 1줄.
    /// [진입점] static Open(territoryId) / Ensure(). 순수 VisualElement 트리 (1회성 폴링 갱신).
    /// </summary>
    public class WarehouseWindowUTK : UTKWindowBase, IUTKDropTarget
    {
        // ===== 싱글턴 / 팩토리 =====
        private static WarehouseWindowUTK _instance;
        public static WarehouseWindowUTK Instance => _instance;

        /// <summary>팩토리 — UIRoot 우측(창고) 배치. 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new WarehouseWindowUTK();
        }

        /// <summary>창고 열기 (territoryId 전달 — 원본 TerritoryWarehouse.OpenWarehouseUI 관례).</summary>
        /// <summary>[U8 배선] 열려있으면 닫고 true, 아니면 false — TerritoryWarehouse 닫기 경로.</summary>
        public static bool CloseIfOpen()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return true; }
            return false;
        }

        public static void Open(string territoryId)
        {
            UTKInventoryClusterComposition.OpenWarehouse(territoryId);
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle(string territoryId)
        {
            UTKInventoryClusterComposition.ToggleWarehouse(territoryId);
        }

        // ===== 설정 =====
        private const float WinW = 504f;
        private const float WinH = 912f;
        private const int Columns = InventoryClusterPanelRegions.StorageVisibleColumns;
        private const int DisplaySlots = Columns * InventoryClusterPanelRegions.StorageVisibleRows;
        private const int MaxSlots = 20;   // system capacity fallback when WarehouseSystem is unavailable
        private const int FigmaVisibleRows = 6;
        private const int FigmaVisibleSlots = Columns * FigmaVisibleRows;
        private const long RefreshMs = 250L;

        private static readonly Color PanelBg = new Color32(0x16, 0x1B, 0x22, 0xFF);
        private static readonly Color PanelText = new Color32(0xF0, 0xF6, 0xFC, 0xFF);
        private static readonly Color PanelMuted = new Color32(0x8B, 0x94, 0x9E, 0xFF);
        private static readonly Color Stroke = new Color32(0x30, 0x36, 0x3D, 0xFF);
        private static readonly Color HeaderDecal = new Color32(0x58, 0xA6, 0xFF, 0xFF);
        private readonly Label _availabilityLabel;
        private readonly VisualElement _headerTitleGroup;
        private readonly VisualElement[] _cornerDecals = new VisualElement[4];
        private Button _closeButton;

        private VisualElement AddCornerDecal(string decalName, float x, float y, bool right, bool bottom)
        {
            var decal = new VisualElement { name = decalName };
            decal.style.position = Position.Absolute;
            decal.style.left = x;
            decal.style.top = y;
            decal.style.width = 14.4f;
            decal.style.height = 14.4f;
            decal.pickingMode = PickingMode.Ignore;
            var horizontal = new VisualElement();
            horizontal.style.position = Position.Absolute;
            horizontal.style.width = 12f;
            horizontal.style.height = 1.8f;
            horizontal.style.backgroundColor = new StyleColor(HeaderDecal);
            horizontal.style.left = right ? 2.4f : 0f;
            horizontal.style.top = bottom ? 12.6f : 0f;
            var vertical = new VisualElement();
            vertical.style.position = Position.Absolute;
            vertical.style.width = 1.8f;
            vertical.style.height = 12f;
            vertical.style.backgroundColor = new StyleColor(HeaderDecal);
            vertical.style.left = right ? 12.6f : 0f;
            vertical.style.top = bottom ? 2.4f : 0f;
            decal.Add(horizontal);
            decal.Add(vertical);
            Add(decal);
            return decal;
        }

        private static void StyleFigmaActionButton(Button button)
        {
            if (button == null) return;
            Color buttonBg = new Color(1f, 1f, 1f, 0.05882353f);
            button.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            button.style.backgroundColor = new StyleColor(buttonBg);
            button.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            button.style.color = new StyleColor(PanelText);
            button.style.fontSize = 14.4f;
            button.style.unityFontStyleAndWeight = FontStyle.Normal;
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 1f;
            button.style.borderTopColor = button.style.borderBottomColor = button.style.borderLeftColor = button.style.borderRightColor = new StyleColor(Stroke);
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 7.2f;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 7.2f;
            button.style.paddingLeft = button.style.paddingRight = 6f;
        }

        private VisualElement CreateStorageFooter()
        {
            var footer = new VisualElement { name = "StorageFooter" };
            footer.style.position = Position.Absolute;
            footer.style.left = 24f;
            footer.style.top = 713f;
            footer.style.width = 456f;
            footer.style.height = 61.2f;
            footer.style.borderTopWidth = 1f;
            footer.style.borderTopColor = new StyleColor(Stroke);
            footer.pickingMode = PickingMode.Ignore;
            return footer;
        }

        // ===== 상태 =====
        private string _territoryId = "default";
        private bool _territoryMenuOpen;

        // ===== 레퍼런스 =====
        private readonly VisualElement _invColumn;
        private readonly VisualElement _whColumn;
        private readonly VisualElement _storageColumnsRow;
        private readonly VisualElement _storageFooter;
        private bool _isApplyingWarehouseCellLayout;

        // [우클릭 출고 수리] 우클릭 이중 경로(PointerDown button1 + ContextClickEvent) 중복 발화 방지 가드.
        //   Environment.TickCount(ms) 기준 200ms 내 같은 슬롯 중복 요청은 무시 → 어느 경로로 와도 1회만 실행.
        //   [QA 수정] 슬롯별 키로 분리 — 전역 단일 타이머로는 연속 우클릭(다른 슬롯)을 200ms간 씹는다.
        private static readonly int WithdrawDedupeMs = 200;
        private readonly System.Collections.Generic.Dictionary<int, int> _lastWithdrawMsPerSlot
            = new System.Collections.Generic.Dictionary<int, int>();
        private readonly VisualElement _invGrid;
        private readonly VisualElement _whGrid;
        private readonly ScrollView _whGridViewport;
        private readonly VisualElement _storageStats;
        private readonly Button _storeAllButton;
        private readonly Button _retrieveAllButton;
        private readonly Label _territoryButton;
        private readonly Label _capacityLabel;
        private readonly VisualElement _territoryMenu;
        private Label _statusLabel;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;
        private VisualElement _geometryRoot;
        private bool _isApplyingStorageLayout;
        private bool _isHandlingRootGeometry;

        private WarehouseWindowUTK() : base("창고", new Vector2(WinW, WinH), UTKWindowChrome.Frameless)
        {
            // [Figma 15:4 글래스] #161B22@0.85(사용자 조정) + #30363D@0.5 1.2px + r14.4
            style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = UTKTheme.GlassStrokeWidth;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new StyleColor(UTKTheme.GlassPanelStroke);
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = UTKTheme.GlassRadius;
            _content.style.flexGrow = 1f;
            _content.style.position = Position.Relative;
            _content.style.paddingTop = 0f;
            _content.style.paddingBottom = 0f;
            _content.style.paddingLeft = 0f;
            _content.style.paddingRight = 0f;

            // Figma StoragePanel header: one custom title/subtitle group and a close control.
            var headerRow = new VisualElement { name = "StorageHeader" };
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            _headerTitleGroup = new VisualElement { name = "StorageTitleGroup" };
            _headerTitleGroup.style.flexDirection = FlexDirection.Row;
            _headerTitleGroup.style.alignItems = Align.Center;
            var title = new Label("창고") { name = "storage-title" };
            title.style.position = Position.Absolute;
            title.style.left = 0f;
            title.style.top = 5.2f;
            title.style.width = 45f;
            title.style.height = 28f;
            title.style.fontSize = 24f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(PanelText);
            _headerTitleGroup.Add(title);
            var subtitle = new Label("BASE DEPOSIT") { name = "storage-subtitle" };
            subtitle.style.position = Position.Absolute;
            subtitle.style.left = 57f;
            subtitle.style.top = 14.2f;
            subtitle.style.width = 97f;
            subtitle.style.height = 17f;
            subtitle.style.fontSize = 14.4f;
            subtitle.style.unityFontStyleAndWeight = FontStyle.Normal;   // [Figma] 14.4/500
            subtitle.style.color = new StyleColor(PanelMuted);
            _headerTitleGroup.Add(subtitle);
            _headerTitleGroup.style.width = 456f;
            _headerTitleGroup.style.height = 52.8f;
            _headerTitleGroup.style.position = Position.Absolute;
            _headerTitleGroup.style.left = 0f;
            _headerTitleGroup.style.top = 0f;
            headerRow.Add(_headerTitleGroup);

            _territoryButton = new Label("영지: default") { name = "TerritoryButton" };
            _territoryButton.style.display = DisplayStyle.None;
            _territoryButton.RegisterCallback<PointerDownEvent>(evt => ToggleTerritoryMenu(evt));
            headerRow.Add(_territoryButton);

            _closeButton = new Button(Close) { name = "storage-close", text = "×" };
            _closeButton.style.position = Position.Absolute;
            _closeButton.style.width = 31.2f;
            _closeButton.style.height = 31.2f;
            _closeButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.05882353f));
            _closeButton.style.color = new StyleColor(PanelMuted);
            _closeButton.style.fontSize = 21f;
            _closeButton.style.borderTopWidth = _closeButton.style.borderBottomWidth = _closeButton.style.borderLeftWidth = _closeButton.style.borderRightWidth = 1f;
            _closeButton.style.borderTopColor = _closeButton.style.borderBottomColor = _closeButton.style.borderLeftColor = _closeButton.style.borderRightColor = new StyleColor(Stroke);
            headerRow.Add(_closeButton);
            _content.Add(headerRow);

            _availabilityLabel = new Label("보관 가능") { name = "storage-availability" };
            _availabilityLabel.style.position = Position.Absolute;
            _availabilityLabel.style.left = 9.6f;
            _availabilityLabel.style.top = 4.8f;
            _availabilityLabel.style.width = 57f;
            _availabilityLabel.style.height = 17f;
            _availabilityLabel.style.fontSize = UTKTheme.FontBadge;
            _availabilityLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _availabilityLabel.style.color = new StyleColor(PanelText);
            _capacityLabel = new Label("") { name = "storage-capacity" };
            _capacityLabel.style.fontSize = UTKTheme.FontBody;   // [Figma] 14.4/400
            _capacityLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            _capacityLabel.style.color = new StyleColor(PanelMuted);
            _storageStats = new VisualElement { name = "StorageStats" };
            _storageStats.style.flexDirection = FlexDirection.Row;
            _storageStats.style.alignItems = Align.Center;
            _storageStats.style.backgroundColor = StyleKeyword.None;
            _storageStats.style.borderTopWidth = _storageStats.style.borderBottomWidth = _storageStats.style.borderLeftWidth = _storageStats.style.borderRightWidth = 0f;
            _storageStats.Add(_availabilityLabel);
            _storageStats.Add(_capacityLabel);
            _content.Add(_storageStats);

            _cornerDecals[0] = AddCornerDecal("StorageCornerTL", 0f, 0f, false, false);
            _cornerDecals[1] = AddCornerDecal("StorageCornerTR", WinW - 14.4f, 0f, true, false);
            _cornerDecals[2] = AddCornerDecal("StorageCornerBL", 0f, WinH - 14.4f, false, true);
            _cornerDecals[3] = AddCornerDecal("StorageCornerBR", WinW - 14.4f, WinH - 14.4f, true, true);
            _storageFooter = CreateStorageFooter();
            _content.Add(_storageFooter);

            // ── 영지 선택 드롭다운 (절대 위치 팝오버) ──
            _territoryMenu = new VisualElement();
            _territoryMenu.name = "TerritoryMenu";
            _territoryMenu.style.position = Position.Absolute;
            _territoryMenu.style.backgroundColor = new StyleColor(UTKColor.BgPanelDark);
            _territoryMenu.style.borderTopWidth = 1f;
            _territoryMenu.style.borderBottomWidth = 1f;
            _territoryMenu.style.borderLeftWidth = 1f;
            _territoryMenu.style.borderRightWidth = 1f;
            _territoryMenu.style.borderTopColor = new StyleColor(UTKColor.BorderBronze);
            _territoryMenu.style.borderBottomColor = new StyleColor(UTKColor.BorderBronze);
            _territoryMenu.style.borderLeftColor = new StyleColor(UTKColor.BorderBronze);
            _territoryMenu.style.borderRightColor = new StyleColor(UTKColor.BorderBronze);
            _territoryMenu.style.display = DisplayStyle.None;
            _content.Add(_territoryMenu);

            // ── 좌:인벤 / 우:창고 2컬럼 ──
            var columnsRow = new VisualElement();
            _storageColumnsRow = columnsRow;
            columnsRow.name = "StorageColumns";
            columnsRow.style.flexGrow = 1f;
            columnsRow.style.flexDirection = FlexDirection.Row;
            columnsRow.style.alignItems = Align.FlexStart;
            _content.Add(columnsRow);

            // 좌측 컬럼: 인벤토리 요약 (드래그 소스 + 출고 드롭 타겟)
            _invColumn = new VisualElement();
            _invColumn.name = "InvColumn";
            _invColumn.style.flexGrow = 1f;
            _invColumn.style.flexDirection = FlexDirection.Column;
            _invColumn.style.marginRight = 12f;
            _invColumn.style.borderTopWidth = UTKTheme.GlassStrokeWidth;
            _invColumn.style.borderBottomWidth = UTKTheme.GlassStrokeWidth;
            _invColumn.style.borderLeftWidth = UTKTheme.GlassStrokeWidth;
            _invColumn.style.borderRightWidth = UTKTheme.GlassStrokeWidth;
            _invColumn.style.borderTopColor = new StyleColor(UTKTheme.GlassPanelStroke);
            _invColumn.style.borderBottomColor = new StyleColor(UTKTheme.GlassPanelStroke);
            _invColumn.style.borderLeftColor = new StyleColor(UTKTheme.GlassPanelStroke);
            _invColumn.style.borderRightColor = new StyleColor(UTKTheme.GlassPanelStroke);

            var invHeading = new Label("🧺 인벤토리 (드래그 → 우측 창고 = 입고)");
            invHeading.style.fontSize = 14f;
            invHeading.style.color = new StyleColor(UTKColor.TextSecondary);
            _invColumn.Add(invHeading);

            _invGrid = new VisualElement();
            _invGrid.name = "InvGrid";
            _invGrid.style.flexDirection = FlexDirection.Row;
            _invGrid.style.flexWrap = Wrap.Wrap;
            _invGrid.style.marginTop = 4f;
            _invGrid.style.marginBottom = 4f;
            _invColumn.Add(_invGrid);

            // [U8 요구] 창고 전용 창 — 인벤 컬럼 은닉(독립 InventoryWindowUTK가 인벤 담당)
            _invColumn.style.display = DisplayStyle.None;

            columnsRow.Add(_invColumn);

            // 우측 컬럼: 창고 그리드 (드래그 소스 + 입고 드롭 타겟)
            _whColumn = new VisualElement();
            _whColumn.name = "WarehouseColumn";
            _whColumn.style.flexGrow = 1f;
            _whColumn.style.flexDirection = FlexDirection.Column;
            _whColumn.style.borderTopWidth = UTKTheme.GlassStrokeWidth;
            _whColumn.style.borderBottomWidth = UTKTheme.GlassStrokeWidth;
            _whColumn.style.borderLeftWidth = UTKTheme.GlassStrokeWidth;
            _whColumn.style.borderRightWidth = UTKTheme.GlassStrokeWidth;
            _whColumn.style.borderTopColor = new StyleColor(UTKTheme.GlassPanelStroke);
            _whColumn.style.borderBottomColor = new StyleColor(UTKTheme.GlassPanelStroke);
            _whColumn.style.borderLeftColor = new StyleColor(UTKTheme.GlassPanelStroke);
            _whColumn.style.borderRightColor = new StyleColor(UTKTheme.GlassPanelStroke);

            var whHeading = new Label("🏰 창고 (드래그 → 좌측 인벤 = 출고 / 우클릭 = 출고)");
            // [Figma 15:4] StoragePanel에 헤딩 없음 — 프롬프트 중복(이름판/인벤 프롬프트 존재)으로 숨김
            whHeading.style.display = DisplayStyle.None;
            _whColumn.Add(whHeading);

            // [U8 요구] 카테고리 탭 — 무기/방어구/재료/소모품/전체 필터
            // The Figma 15:4 StoragePanel has no category tab strip; all stored items remain visible.

            _whGridViewport = new ScrollView(ScrollViewMode.Vertical);
            _whGridViewport.name = "WarehouseGridViewport";
            _whGridViewport.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _whGridViewport.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _whGrid = new VisualElement();
            _whGrid.name = "WarehouseGrid";
            _whGrid.style.flexDirection = FlexDirection.Row;
            _whGrid.style.flexWrap = Wrap.Wrap;
            _whGrid.style.justifyContent = Justify.FlexStart;
            _whGridViewport.contentContainer.Add(_whGrid);
            _whGridViewport.RegisterCallback<GeometryChangedEvent>(_ => ApplyWarehouseCellLayout());
            _whColumn.Add(_whGridViewport);

            columnsRow.Add(_whColumn);

            // ── 상태 라벨 ──
            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 13f;
            _statusLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_statusLabel);

            _storeAllButton = UTKButton.Create("전부 보관하기", OnStoreAllClicked, UTKButton.Variant.Primary);
            _storeAllButton.name = "StoreAll";
            _retrieveAllButton = UTKButton.Create("전부 꺼내기", OnRetrieveAllClicked, UTKButton.Variant.Secondary);
            _retrieveAllButton.name = "RetrieveAll";
            StyleFigmaActionButton(_storeAllButton);
            StyleFigmaActionButton(_retrieveAllButton);
            _content.Add(_storeAllButton);
            _content.Add(_retrieveAllButton);

            ApplyUIToolkitFont(this);

            // 기본 숨김 + [P12] 3분할 우측 배치
            style.display = DisplayStyle.None;
            UTKThreeColumnLayout.Place(this, 2);
            RegisterCallback<AttachToPanelEvent>(OnStorageAttached);
            RegisterCallback<DetachFromPanelEvent>(OnStorageDetached);
            RegisterCallback<GeometryChangedEvent>(OnStorageGeometryChanged);
        }

        // =====================================================================
        //  공개 — 영지 설정
        // =====================================================================

        /// <summary>현재 영지 설정 (Open/토글 시 호출). null이면 그대로 유지.</summary>
        public void SetTerritory(string territoryId)
        {
            if (string.IsNullOrEmpty(territoryId)) return;
            _territoryId = territoryId;
            _territoryButton.text = "영지: " + _territoryId;
            CloseTerritoryMenu();
            RefreshGrid();
        }

        public string CurrentTerritory => _territoryId;

        // =====================================================================
        //  생명주기 (UTKWindowBase 훅)
        // =====================================================================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            UTKThreeColumnLayout.Place(this, 2);   // [P12] 우측 1/3
            InventoryClusterFigmaLayout.ApplyWarehouse(this, root);
            RegisterRootGeometryCallback(root);
            ApplyStorageBodyLayout();
            StartRefreshLoop();
            RefreshGrid();
            Debug.Log($"[WarehouseUTK] 창고 창 열림 (영지: {_territoryId})");
        }

        public override void Hide()
        {
            base.Hide();
            UnregisterRootGeometryCallback();
            StopRefreshLoop();
            CloseTerritoryMenu();
            _lastWithdrawMsPerSlot.Clear();   // [P10 보강] 재오피 시 가드 초기화
            Debug.Log($"[WarehouseUTK] 창고 창 닫힘 (영지: {_territoryId})");
        }


        private void OnStorageAttached(AttachToPanelEvent evt)
        {
            RegisterRootGeometryCallback(UIToolkitBootstrap.UIRoot);
            if (IsOpen)
            {
                InventoryClusterFigmaLayout.ApplyWarehouse(this, UIToolkitBootstrap.UIRoot);
                ApplyStorageBodyLayout();
            }
        }

        private void OnStorageDetached(DetachFromPanelEvent evt)
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
            if (_geometryRoot == null) return;
            _geometryRoot.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            _geometryRoot = null;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            var root = _geometryRoot;
            if (root == null || evt.target != root || !IsOpen || _isApplyingStorageLayout || _isHandlingRootGeometry)
                return;
            if (panel == null || panel != root.panel || _content.panel != panel)
                return;
            _isHandlingRootGeometry = true;
            try
            {
                InventoryClusterFigmaLayout.ApplyWarehouse(this, root);
                ApplyStorageBodyLayout();
            }
            finally
            {
                _isHandlingRootGeometry = false;
            }
        }

        private void OnStorageGeometryChanged(GeometryChangedEvent evt)
        {
            if (evt.target == this || evt.target == _content)
                ApplyStorageBodyLayout();
        }

        protected override void OnWindowOpen()
        {
            // Storage owns one visible grid; the hidden inventory column is not a drop destination.
            UTKDragDrop.RegisterDropTarget(_whColumn, this);
        }

        protected override void OnWindowClosed()
        {
            UTKDragDrop.UnregisterDropTarget(_whColumn);
        }

        private void ApplyStorageBodyLayout()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (_content == null || _isApplyingStorageLayout)
                return;
            if (root != null && panel != null && (_content.panel != panel || root.panel != panel))
                return;
            Vector2 rootSize = root != null
                ? new Vector2(root.resolvedStyle.width, root.resolvedStyle.height)
                : Vector2.zero;
            if (rootSize.x <= 0f || rootSize.y <= 0f || float.IsNaN(rootSize.x) || float.IsNaN(rootSize.y))
                rootSize = FigmaCanvasLayout.CanonicalCanvasSize;

            _isApplyingStorageLayout = true;
            try
            {
                style.backgroundImage = new StyleBackground(StyleKeyword.None);
                style.backgroundColor = new StyleColor(PanelBg);
                float sx = rootSize.x / FigmaCanvasLayout.CanvasWidth;
                float sy = rootSize.y / FigmaCanvasLayout.CanvasHeight;
                Vector2 contentOrigin = panel != null ? new Vector2(_content.layout.x, _content.layout.y) : Vector2.zero;
                SetStorageRect(this.Q("StorageHeader"), InventoryClusterPanelRegions.StorageHeaderBody, rootSize, contentOrigin);
                SetStorageRect(_storageStats, InventoryClusterPanelRegions.StorageStatsBody, rootSize, contentOrigin);
                SetStorageRect(_storageColumnsRow, InventoryClusterPanelRegions.StorageGridBody, rootSize, contentOrigin);
                SetStorageRect(_storageFooter, InventoryClusterPanelRegions.StorageFooterBody, rootSize, contentOrigin);
                _whColumn.style.position = Position.Absolute;
                _whColumn.style.left = 0f;
                _whColumn.style.top = 0f;
                _whColumn.style.width = InventoryClusterPanelRegions.StorageGridBody.width * sx;
                _whColumn.style.height = InventoryClusterPanelRegions.StorageGridBody.height * sy;
                _whGridViewport.style.position = Position.Absolute;
                _whGridViewport.style.left = 0f;
                _whGridViewport.style.top = 0f;
                _whGridViewport.style.width = InventoryClusterPanelRegions.StorageGridBody.width * sx;
                _whGridViewport.style.height = InventoryClusterPanelRegions.StorageGridBody.height * sy;
                var statusBand = new Rect(
                    InventoryClusterPanelRegions.StorageFooterBody.x,
                    InventoryClusterPanelRegions.StorageFooterBody.y,
                    InventoryClusterPanelRegions.StorageFooterBody.width,
                    14.4f);
                SetStorageRect(_statusLabel, statusBand, rootSize, contentOrigin);
                SetStorageRect(_storeAllButton, InventoryClusterPanelRegions.StoreAllButtonBody, rootSize, contentOrigin);
                SetStorageRect(_retrieveAllButton, InventoryClusterPanelRegions.RetrieveAllButtonBody, rootSize, contentOrigin);

                var heading = _whColumn.childCount > 0 ? _whColumn[0] : null;
                if (heading != null) heading.style.display = DisplayStyle.None;
                ApplyCornerDecalLayout(sx, sy);
                ApplyCapacityLabelLayout(_capacityLabel, sx, sy);
                _closeButton.style.left = 448.8f - 24f;
                _closeButton.style.top = 27.6f - 24f;
                _whGridViewport.style.overflow = Overflow.Hidden;
                _whGridViewport.contentContainer.style.width = InventoryClusterPanelRegions.StorageGridBody.width * sx;
                _whGridViewport.contentContainer.style.paddingTop = 0f;
                _whGridViewport.contentContainer.style.paddingLeft = 0f;
                _whGridViewport.contentContainer.style.paddingRight = 0f;
                _whGridViewport.contentContainer.style.flexDirection = FlexDirection.Row;
                _whGridViewport.contentContainer.style.flexWrap = Wrap.Wrap;
                _whGridViewport.contentContainer.style.justifyContent = Justify.FlexStart;
                _whGridViewport.contentContainer.style.alignContent = Align.FlexStart;

                ApplyWarehouseCellLayout();
            }
            finally
            {
                _isApplyingStorageLayout = false;
            }
        }

        private void ApplyWarehouseCellLayout()
        {
            if (_isApplyingWarehouseCellLayout || _whGridViewport == null || _whGrid == null)
                return;
            float width = InventoryClusterPanelRegions.StorageGridBody.width;
            float height = InventoryClusterPanelRegions.StorageGridBody.height;
            if (UIToolkitBootstrap.UIRoot != null)
            {
                Vector2 rootSize = new Vector2(UIToolkitBootstrap.UIRoot.resolvedStyle.width, UIToolkitBootstrap.UIRoot.resolvedStyle.height);
                if (rootSize.x > 0f && rootSize.y > 0f)
                {
                    width *= rootSize.x / FigmaCanvasLayout.CanvasWidth;
                    height *= rootSize.y / FigmaCanvasLayout.CanvasHeight;
                }
            }
            else if (_whGridViewport.resolvedStyle.width > 0f && _whGridViewport.resolvedStyle.height > 0f)
            {
                width = _whGridViewport.resolvedStyle.width;
                height = _whGridViewport.resolvedStyle.height;
            }
            _isApplyingWarehouseCellLayout = true;
            try
            {
                float sx = width / InventoryClusterPanelRegions.StorageGridBody.width;
                float sy = height / InventoryClusterPanelRegions.StorageGridBody.height;
                float cellWidth = InventoryClusterPanelRegions.CellSize * sx;
                float cellHeight = InventoryClusterPanelRegions.CellSize * sy;
                float gapX = InventoryClusterPanelRegions.CellGap * sx;
                float gapY = InventoryClusterPanelRegions.CellGap * sy;
                float iconWidth = 38.4f * sx;
                float iconHeight = 38.4f * sy;
                float iconOffsetX = 21.6f * sx;
                float iconOffsetY = 21.6f * sy;
                int childCount = _whGrid.childCount;
                int lastRow = childCount > 0 ? (childCount - 1) / Columns : -1;
                int index = 0;
                foreach (var slot in _whGrid.Children())
                {
                    slot.style.width = cellWidth;
                    slot.style.height = cellHeight;
                    slot.style.flexShrink = 0f;
                    slot.style.marginLeft = 0f;
                    slot.style.marginRight = index % Columns < Columns - 1 ? gapX : 0f;
                    slot.style.marginTop = 0f;
                    slot.style.marginBottom = index / Columns < lastRow ? gapY : 0f;
                    StyleWarehouseSlot(slot, sx, sy, iconOffsetX, iconOffsetY, iconWidth, iconHeight);
                    index++;
                }
            }
            finally
            {
                _isApplyingWarehouseCellLayout = false;
            }
        }

        private void ApplyCornerDecalLayout(float sx, float sy)
        {
            for (int i = 0; i < _cornerDecals.Length; i++)
            {
                var decal = _cornerDecals[i];
                if (decal == null) continue;
                bool right = i == 1 || i == 3;
                bool bottom = i >= 2;
                decal.style.left = (right ? WinW - 14.4f : 0f) * sx;
                decal.style.top = (bottom ? WinH - 14.4f : 0f) * sy;
                decal.style.width = 14.4f * sx;
                decal.style.height = 14.4f * sy;

                var horizontal = decal[0];
                horizontal.style.width = 12f * sx;
                horizontal.style.height = 1.8f * sy;
                horizontal.style.left = right ? 2.4f * sx : 0f;
                horizontal.style.top = bottom ? 12.6f * sy : 0f;

                var vertical = decal[1];
                vertical.style.width = 1.8f * sx;
                vertical.style.height = 12f * sy;
                vertical.style.left = right ? 12.6f * sx : 0f;
                vertical.style.top = bottom ? 2.4f * sy : 0f;
            }
        }

        private static void ApplyCapacityLabelLayout(Label label, float sx, float sy)
        {
            if (label == null) return;
            label.style.position = Position.Absolute;
            label.style.right = 0f;
            label.style.top = 3.8f * sy;
            label.style.width = 140f * sx;
            label.style.height = 19f * sy;
            label.style.fontSize = 12f * sy;
        }

        private static void SetStorageRect(VisualElement element, Rect panelRect, Vector2 rootSize, Vector2 contentOrigin)
        {
            if (element == null) return;
            Rect scaled = InventoryClusterPanelRegions.ScaleStorageBody(panelRect, rootSize);
            element.style.position = Position.Absolute;
            element.style.left = scaled.x - contentOrigin.x;
            element.style.top = scaled.y - contentOrigin.y;
            element.style.width = scaled.width;
            element.style.height = scaled.height;
        }

        // =====================================================================
        //  ⑤ 갱신 — 250ms 폴링
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
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
        //  그리드 리프레시 (좌:인벤 / 우:창고)
        // =====================================================================

        private void RefreshGrid()
        {
            if (UTKDragDrop.Active) return; // [U8 수리] 드래그 중 재생성 금지
            RefreshInventoryGrid();
            RefreshWarehouseGrid();
            RefreshCapacity();
        }

        /// <summary>[C-O2-03b] 확장 클릭 — WarehouseSystem.TryExpandSlots → 결과 표시 + 갱신.</summary>

        private void OnStoreAllClicked()
        {
            var inventory = PlayerInventory.Instance;
            var slots = inventory != null ? inventory.GetAllSlots() : null;
            if (slots == null) return;

            int moved = 0;
            for (int index = 0; index < slots.Length; index++)
            {
                var source = slots[index];
                if (source == null || source.item == null || source.count <= 0) continue;
                var item = source.item;
                int originalCount = source.count;
                for (int count = 0; count < originalCount; count++)
                {
                    if (!DepositFromInventory(index, item))
                    {
                        _statusLabel.text = $"모두 보관 중단: {item.displayName} (창고 가득 또는 인벤 슬롯 변경)";
                        RefreshGrid();
                        return;
                    }
                    moved++;
                }
            }
            _statusLabel.text = $"모두 보관 완료: {moved}개";
            RefreshGrid();
        }

        private void OnRetrieveAllClicked()
        {
            var warehouse = WarehouseSystem.Instance;
            var slots = warehouse != null ? warehouse.GetItems(_territoryId) : null;
            if (slots == null) return;

            int moved = 0;
            var originalIndices = InventoryClusterPanelRegions.GetDescendingStorageIndices(slots.Count);
            foreach (int sourceIndex in originalIndices)
            {
                var source = slots[sourceIndex];
                if (source == null || source.item == null || source.count <= 0) continue;
                int originalCount = source.count;
                for (int count = 0; count < originalCount; count++)
                {
                    if (!TransferWarehouseSlotToInventory(sourceIndex))
                    {
                        _statusLabel.text = $"모두 꺼내기 중단: 인벤 가득 (이동 {moved}개)";
                        RefreshGrid();
                        return;
                    }
                    moved++;
                }
            }
            _statusLabel.text = $"모두 꺼내기 완료: {moved}개";
            RefreshGrid();
        }

        private void RefreshCapacity()
        {
            int used = 0;
            var items = WarehouseSystem.Instance != null ? WarehouseSystem.Instance.GetItems(_territoryId) : null;
            if (items != null) used = items.Count;
            // [C-O2-03b] 확장 반영 동적 용량
            int capacity = WarehouseSystem.Instance != null
                ? WarehouseSystem.Instance.GetSlotCapacity(_territoryId)
                : MaxSlots;
            _capacityLabel.text = $"사용 슬롯: {used} / {capacity}";

        }

        private void RefreshInventoryGrid()
        {
            _invGrid.Clear();
            var inv = PlayerInventory.Instance;
            var slots = inv != null ? inv.GetAllSlots() : null;
            int total = slots != null ? slots.Length : 0;

            int rows = total > 0 ? (total + Columns - 1) / Columns : 1;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    int idx = r * Columns + c;
                    _invGrid.Add(BuildInventoryCell(slots, idx, total));
                }
            }
        }

        /// <summary>[U8 요구] 카테고리 탭 매칭 — 무기/방어구/재료/소모품.</summary>
        private static bool CategoryMatches(PlayerInventory.ItemCategory cat, string tab)
        {
            switch (tab)
            {
                case "무기": return cat == PlayerInventory.ItemCategory.Weapon;
                case "방어구": return cat == PlayerInventory.ItemCategory.Armor;
                case "재료": return cat == PlayerInventory.ItemCategory.Material || cat == PlayerInventory.ItemCategory.Herb;
                case "소모품": return cat == PlayerInventory.ItemCategory.Potion || cat == PlayerInventory.ItemCategory.Food || cat == PlayerInventory.ItemCategory.Drug;
                default: return true;
            }
        }

        private void RefreshWarehouseGrid()
        {
            _whGrid.Clear();
            var allItems = WarehouseSystem.Instance != null ? WarehouseSystem.Instance.GetItems(_territoryId) : null;
            // [우클릭 출고 수리] 카테고리 탭 필터 — 표시 목록(items)과 전체 리스트 기준 실제 인덱스(actualIdx) 병행 보관.
            //   필터로 걸러진 "표시 idx"를 TransferToInventory(전체 리스트 기준)에 그대로 쓰면
            //   엉뚱한 슬롯 출고/실패가 생긴다 → 반드시 전체 인덱스를 함께 캡처해 클로저에 넘긴다.
            var items = new List<PlayerInventory.ItemSlot>();
            var actualIdx = new List<int>();
            if (allItems != null)
            {
                for (int i = 0; i < allItems.Count; i++)
                {
                    items.Add(allItems[i]);
                    actualIdx.Add(i);
                }
            }

            int total = items.Count;

            // Figma reserves six rows/30 cells regardless of gameplay capacity; additional capacity remains scrollable.
            int dynamicCapacity = WarehouseSystem.Instance != null
                ? WarehouseSystem.Instance.GetSlotCapacity(_territoryId)
                : MaxSlots;
            int displayCapacity = Mathf.Max(FigmaVisibleSlots, dynamicCapacity);
            int rows = (displayCapacity + Columns - 1) / Columns;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    int idx = r * Columns + c;
                    int actual = idx < actualIdx.Count ? actualIdx[idx] : idx;
                    _whGrid.Add(BuildWarehouseCell(items, idx, total, actual));
                }
            }
            ApplyWarehouseCellLayout();
            ApplyStorageBodyLayout();
        }

        // =====================================================================
        //  셀 빌드 — 좌측 인벤토리
        // =====================================================================

        private VisualElement BuildInventoryCell(PlayerInventory.ItemSlot[] slots, int idx, int total)
        {
            var cell = new UTKSlot();
            cell.name = "WhInvSlot_" + idx;
            StyleSlot(cell);

            if (idx < total && slots[idx] != null && slots[idx].item != null && slots[idx].count > 0)
            {
                var slotData = slots[idx];
                var item = slotData.item;
                cell.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                cell.SetCount(slotData.count);
                cell.SetRank(UTKRarity.ClassForIndex((int)item.rarity));

                int slotIndex = idx;
                // ① 드래그 소스 (좌/우 드래그 = 입고) + 좌클릭 설명 + 우클릭(비드래그) = 즉시 입고
                UTKDragDrop.MakeDraggable(cell,
                    () => MakeInventoryPayload(slotIndex, item),
                    () => OnInventorySlotClick(slotIndex),
                    () => OnInventorySlotRightClick(slotIndex, null));
            }
            else
            {
                cell.SetRank("common");
            }

            return cell;
        }

        // =====================================================================
        //  셀 빌드 — 우측 창고
        // =====================================================================

        private VisualElement BuildWarehouseCell(List<PlayerInventory.ItemSlot> items, int idx, int total, int actualSlotIndex)
        {
            var cell = new UTKSlot();
            cell.name = "WhSlot_" + idx;
            StyleSlot(cell);

            if (idx < total && items[idx] != null && items[idx].item != null && items[idx].count > 0)
            {
                var slotData = items[idx];
                var item = slotData.item;
                cell.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                cell.SetCount(slotData.count);
                cell.SetRank(UTKRarity.ClassForIndex((int)item.rarity));
                var tier = cell.Q("WarehouseTierStrip");
                if (tier != null) tier.style.backgroundColor = new StyleColor(RarityColor(item.rarity));

                int slotIndex = actualSlotIndex;   // [우클릭 출고 수리] 반드시 전체 리스트 기준 실제 인덱스 사용
                // ② 드래그 소스 (좌/우 드래그 = 출고 이동) + 좌클릭 설명 + 우클릭(비드래그) = 즉시 출고
                UTKDragDrop.MakeDraggable(cell,
                    () => MakeWarehousePayload(slotIndex, item),
                    () => OnWarehouseSlotClick(slotIndex),
                    () => OnWarehouseSlotRightClick(slotIndex, null));

                // [P9 수리] 우클릭 3중 경로 — 실측(Play): WhSlot 우클릭이 UTKDragDrop 라우팅과
                //   ContextClickEvent 모두 미도착(InvSlot은 도착) → PointerDown(button=1)을 셀에 직접 등록.
                //   WithdrawSlot의 slot별 200ms 가드가 중복을 흡수하므로 3경로여도 1회만 실행.
                cell.RegisterCallback<ContextClickEvent>(evt =>
                {
                    OnWarehouseSlotRightClick(slotIndex, evt);
                });
                cell.RegisterCallback<UnityEngine.UIElements.PointerDownEvent>(evt =>
                {
                    if (evt.button == 1)
                        OnWarehouseSlotRightClick(slotIndex, null);
                }, UnityEngine.UIElements.TrickleDown.TrickleDown); // 트리클다운 — 오버레이 가로챔 우선 우회
            }
            else
            {
                cell.SetRank("common");
            }

            SetWarehouseEmpty(cell, !(idx < total && items[idx] != null && items[idx].item != null && items[idx].count > 0));
            return cell;
        }

        private static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return new Color32(0x8B, 0x94, 0x9E, 0xFF);
                case ItemRarity.Rare: return new Color32(0x1F, 0x6F, 0xEB, 0xFF);
                case ItemRarity.Epic: return new Color32(0xBC, 0x8C, 0xFF, 0xFF);
                case ItemRarity.Legendary: return new Color32(0xE3, 0xB3, 0x41, 0xFF);
                case ItemRarity.Unique: return new Color32(0xF8, 0x51, 0x49, 0xFF);
                default: return new Color32(0x8B, 0x94, 0x9E, 0xFF);
            }
        }

        private void StyleSlot(UTKSlot cell)
        {
            cell.style.width = InventoryClusterPanelRegions.CellSize;
            cell.style.height = InventoryClusterPanelRegions.CellSize;
            cell.style.marginTop = 0f;
            cell.style.marginBottom = InventoryClusterPanelRegions.CellGap;
            cell.style.marginLeft = 0f;
            cell.style.marginRight = 0f;
            StyleWarehouseSlot(cell, 1f, 1f);
        }

        private static void StyleWarehouseSlot(VisualElement cell, float sx, float sy, float iconOffsetX = -1f, float iconOffsetY = -1f, float iconWidth = -1f, float iconHeight = -1f)
        {
            if (cell == null) return;
            if (iconOffsetX < 0f) iconOffsetX = 21.6f * sx;
            if (iconOffsetY < 0f) iconOffsetY = 21.6f * sy;
            if (iconWidth < 0f) iconWidth = 38.4f * sx;
            if (iconHeight < 0f) iconHeight = 38.4f * sy;
            cell.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            cell.style.backgroundColor = new StyleColor(new Color(0.12941177f, 0.14901961f, 0.1764706f, 0.6f));
            cell.style.borderTopWidth = cell.style.borderBottomWidth = 1f * sy;
            cell.style.borderLeftWidth = cell.style.borderRightWidth = 1f * sx;
            cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = new StyleColor(new Color(0.1882353f, 0.2117647f, 0.2392157f, 0.5019608f));
            float radius = 9.6f * Mathf.Min(sx, sy);
            cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius = radius;
            cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = radius;

            var icon = cell.Q<VisualElement>("Icon");
            if (icon != null)
            {
                icon.style.position = Position.Absolute;
                icon.style.left = iconOffsetX;
                icon.style.top = iconOffsetY;
                icon.style.width = iconWidth;
                icon.style.height = iconHeight;
                icon.style.flexGrow = 0f;
            }

            var count = cell.Q<Label>("Count");
            if (count != null)
            {
                count.style.position = Position.Absolute;
                count.style.left = 57.6f * sx;
                count.style.top = 60f * sy;
                count.style.minWidth = 16f * sx;
                count.style.height = 17f * sy;
                count.style.fontSize = 12f * sy;
                count.style.color = new StyleColor(PanelMuted);
                count.style.unityTextAlign = TextAnchor.MiddleRight;
            }

            var tierStrip = cell.Q("WarehouseTierStrip");
            if (tierStrip == null)
            {
                tierStrip = new VisualElement { name = "WarehouseTierStrip" };
                tierStrip.pickingMode = PickingMode.Ignore;
                tierStrip.style.position = Position.Absolute;
                cell.Add(tierStrip);
            }
            tierStrip.style.position = Position.Absolute;
            tierStrip.style.left = 0f;
            tierStrip.style.top = 0f;
            tierStrip.style.width = InventoryClusterPanelRegions.CellSize * sx;
            tierStrip.style.height = 3.6f * sy;
            tierStrip.style.backgroundColor = new StyleColor(PanelMuted);

            var emptyMark = cell.Q("WarehouseEmptyMark");
            if (emptyMark == null)
            {
                emptyMark = new VisualElement { name = "WarehouseEmptyMark" };
                emptyMark.pickingMode = PickingMode.Ignore;
                for (int i = 0; i < 4; i++)
                {
                    var dash = new VisualElement();
                    dash.style.position = Position.Absolute;
                    dash.style.backgroundColor = new StyleColor(PanelMuted);
                    emptyMark.Add(dash);
                }
                cell.Add(emptyMark);
            }
            emptyMark.style.position = Position.Absolute;
            emptyMark.style.left = iconOffsetX;
            emptyMark.style.top = iconOffsetY;
            emptyMark.style.width = iconWidth;
            emptyMark.style.height = iconHeight;
            emptyMark.style.position = Position.Absolute;
            for (int i = 0; i < 4; i++)
            {
                var dash = emptyMark[i];
                if (i < 2)
                {
                    dash.style.width = 8f * sx;
                    dash.style.height = 1f * sy;
                    dash.style.left = i == 0 ? 0f : 30.4f * sx;
                    dash.style.top = i == 0 ? 0f : 37.4f * sy;
                }
                else
                {
                    dash.style.width = 1f * sx;
                    dash.style.height = 8f * sy;
                    dash.style.left = i == 2 ? 0f : 37.4f * sx;
                    dash.style.top = i == 2 ? 0f : 30.4f * sy;
                }
            }
            emptyMark.style.display = DisplayStyle.None;
        }

        private static void SetWarehouseEmpty(UTKSlot cell, bool isEmpty)
        {
            if (cell == null) return;
            var emptyMark = cell.Q("WarehouseEmptyMark");
            var icon = cell.Q<VisualElement>("Icon");
            var count = cell.Q<Label>("Count");
            if (emptyMark != null) emptyMark.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            if (icon != null) icon.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            if (count != null && isEmpty) count.style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  페이로드 / 클릭
        // =====================================================================

        private UTKDragPayload MakeInventoryPayload(int slotIndex, PlayerInventory.ItemData item)
        {
            var p = new UTKDragPayload();
            p.Source = UTKDragSourceKind.Inventory;
            p.SourceIndex = slotIndex;
            p.Item = item;
            p.Icon = ItemIconDatabase.GetOrCreateIcon(item);
            return p;
        }

        private UTKDragPayload MakeWarehousePayload(int slotIndex, PlayerInventory.ItemData item)
        {
            var p = new UTKDragPayload();
            p.Source = UTKDragSourceKind.Warehouse;
            p.SourceIndex = slotIndex;
            p.TerritoryId = _territoryId;
            p.Item = item;
            p.Icon = ItemIconDatabase.GetOrCreateIcon(item);
            return p;
        }

        private void OnInventorySlotClick(int slotIndex)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;
            var slots = inv.GetAllSlots();
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
            var slotData = slots[slotIndex];
            if (slotData == null || slotData.item == null) { _statusLabel.text = ""; return; }
            _statusLabel.text = $"{slotData.item.displayName}  x{slotData.count}  —  {slotData.item.description}";
        }

        private void OnWarehouseSlotClick(int slotIndex)
        {
            var items = WarehouseSystem.Instance != null ? WarehouseSystem.Instance.GetItems(_territoryId) : null;
            if (items == null || slotIndex < 0 || slotIndex >= items.Count) return;
            var slotData = items[slotIndex];
            if (slotData == null || slotData.item == null) { _statusLabel.text = ""; return; }
            _statusLabel.text = $"{slotData.item.displayName}  x{slotData.count}  —  {slotData.item.description}  (우클릭: 출고)";
        }

        // =====================================================================
        //  ③ 우클릭 — 반대편 즉시 이동 (입고/출고)
        // =====================================================================

        private void OnInventorySlotRightClick(int slotIndex, ContextClickEvent evt)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;
            var slots = inv.GetAllSlots();
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
            var slotData = slots[slotIndex];
            if (slotData == null || slotData.item == null) return;
            DepositItem(slotData.item);
            RefreshGrid();
            if (evt != null) evt.StopPropagation();
        }

        private void OnWarehouseSlotRightClick(int slotIndex, ContextClickEvent evt)
        {
            WithdrawSlot(slotIndex);
            RefreshGrid();
            if (evt != null) evt.StopPropagation();
        }

        // =====================================================================
        //  IUTKDropTarget — 좌우 컬럼 양방향
        // =====================================================================

        public bool CanDrop(UTKDragPayload payload)
        {
            // This target is registered only on the visible warehouse column; accept inventory deposits.
            return payload != null
                && payload.Item != null
                && payload.Source == UTKDragSourceKind.Inventory;
        }

        public bool Drop(UTKDragPayload payload)
        {
            if (!CanDrop(payload)) return false;

            bool ok = DepositItem(payload.Item);
            RefreshGrid();
            Debug.Log($"[WarehouseUTK] 입고(드래그): {payload.Item.displayName} → {_territoryId} (성공={ok})");
            return ok;
        }

        // =====================================================================
        //  데이터 경로 (원본 시그니처 실측 맵핑 — 복제 없이 직접 호출)
        // =====================================================================

        /// <summary>
        /// ① 인벤 → 창고 입고 (1개). 원본 WarehouseUI.TryDepositFromDrag와 동일:
        ///   PlayerInventory.RemoveItem → WarehouseSystem.AddItem → 실패 시 인벤 롤백.
        /// </summary>
        /// <summary>[U8 요구] 인벤 우클릭 입고 — 인벤 슬롯에서 창고로 이동 (우클릭 한 번).</summary>
        public bool DepositFromInventory(int inventorySlotIndex, PlayerInventory.ItemData item)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null || item == null) return false;

            // The source index is retained for API compatibility, but the Core API removes by item ID.
            // DepositItem owns the single removal and restores it if WarehouseSystem rejects the add.
            bool moved = DepositItem(item);
            if (!moved) return false;

            RefreshGrid();
            Debug.Log($"[WarehouseUTK] 입고: {item.displayName} (sourceSlot={inventorySlotIndex})");
            return true;
        }

        private bool DepositItem(PlayerInventory.ItemData item)
        {
            if (item == null) return false;
            var inventory = PlayerInventory.Instance;
            var warehouse = WarehouseSystem.Instance;
            if (warehouse == null || inventory == null) return false;
            string tid = _territoryId;
            if (string.IsNullOrEmpty(tid)) return false;

            // [입고 실패 진단] RemoveItem 실패 사유 보강 — 개수 부족 vs ID 불일치(재고 없음) 판별.
            int inventoryCountBefore = inventory.GetItemCount(item.id);
            if (inventoryCountBefore <= 0 || !inventory.RemoveItem(item.id, 1))
            {
                Debug.LogWarning($"[WarehouseUTK] 입고 취소(사유: 인벤 재고 없음) — {item.displayName}(id={item.id}) 유지");
                return false;
            }
            if (!warehouse.AddItem(tid, item, 1))
            {
                bool rolledBack = inventory.AddItem(item, 1);
                Debug.LogWarning($"[WarehouseUTK] 창고 가득 — 입고 취소(롤백={rolledBack}): {item.displayName}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// ② 창고 → 인벤 출고 (1개). 원본 WarehouseUI.TransferDraggedToInventory / TerritoryWarehouse.WithdrawItem와 동일:
        ///   WarehouseSystem.TransferToInventory(territoryId, slotIndex, 1).
        /// </summary>
        private bool WithdrawSlot(int slotIndex)
        {
            // [우클릭 출고 수리] 진입 로그 — 출고 시도 자체 추적 (차기 실측 대비)
            Debug.Log($"[WarehouseUTK] 출고 시도 slot={slotIndex}");

            // [우클릭 출고 수리] 이중 발화 가드 — 우클릭 시 PointerDown(button=1)과 ContextClickEvent가
            //   둘 다 도착. 같은 슬롯에 대해 200ms 내 중복 요청은 무시해 어느 경로로 와도 1회만 실행.
            //   (슬롯별 키 → 서로 다른 슬롯의 연속 우클릭은 씹지 않는다.)
            // [P10 근본 수리] int.MinValue 초기값 비교는 Environment.TickCount가 양수일 때
            //   now - int.MinValue 가 int 오버플로우로 큰 음수가 됨(실측: -2086493883ms) →
            //   첫 우클릭이 항상 "중복"으로 오판되어 출고가 영구 차단됐다.
            //   TickCount 랩어웨이 안전 delta 패턴(unchecked uint 차)으로 교체.
            int now = System.Environment.TickCount;
            bool hasLast = _lastWithdrawMsPerSlot.TryGetValue(slotIndex, out int last);
            if (hasLast)
            {
                int delta = unchecked((int)((uint)now - (uint)last));
                if (delta >= 0 && delta < WithdrawDedupeMs)
                {
                    Debug.Log($"[WarehouseUTK] 출고 중복 요청 무시 slot={slotIndex} ({delta}ms 내)");
                    return false;
                }
            }
            _lastWithdrawMsPerSlot[slotIndex] = now;

            if (WarehouseSystem.Instance == null)
            {
                Debug.LogWarning("[WarehouseUTK] 출고 실패 — WarehouseSystem.Instance 없음");
                return false;
            }
            if (string.IsNullOrEmpty(_territoryId))
            {
                Debug.LogWarning("[WarehouseUTK] 출고 실패 — _territoryId 빈값");
                return false;
            }
            if (slotIndex < 0)
            {
                Debug.LogWarning($"[WarehouseUTK] 출고 실패 — 슬롯범위이상 slot={slotIndex}");
                return false;
            }

            if (!TransferWarehouseSlotToInventory(slotIndex))
            {
                Debug.LogWarning($"[WarehouseUTK] 출고 실패(인벤 가득 or 슬롯 변경) slot={slotIndex} tid={_territoryId}");
                return false;
            }
            RefreshGrid();   // [우클릭 출고 수리] 출고 직후 즉시 갱신 — 250ms 폴링에만 의존하지 않음
            Debug.Log($"[WarehouseUTK] 출고(우클릭): slot={slotIndex} ← {_territoryId}");
            return true;
        }

        private bool TransferWarehouseSlotToInventory(int slotIndex)
        {
            return WarehouseSystem.Instance != null
                && !string.IsNullOrEmpty(_territoryId)
                && slotIndex >= 0
                && WarehouseSystem.Instance.TransferToInventory(_territoryId, slotIndex, 1);
        }

        // =====================================================================
        //  영지 선택 드롭다운
        // =====================================================================

        private void ToggleTerritoryMenu(PointerDownEvent evt)
        {
            if (evt != null && evt.button != 0) return;
            if (_territoryMenuOpen) { CloseTerritoryMenu(); return; }
            OpenTerritoryMenu();
            if (evt != null) evt.StopPropagation();
        }

        private void OpenTerritoryMenu()
        {
            _territoryMenu.Clear();
            var options = BuildTerritoryOptions();
            foreach (var opt in options)
            {
                var item = new Label(opt);
                item.style.fontSize = 13f;
                item.style.color = new StyleColor(UTKColor.TextPrimary);
                item.style.paddingLeft = 6f;
                item.style.paddingRight = 6f;
                item.style.paddingTop = 4f;
                item.style.paddingBottom = 4f;
                if (opt == _territoryId)
                    item.style.color = new StyleColor(UTKColor.AccentRare);
                string sel = opt;
                item.RegisterCallback<PointerDownEvent>(e =>
                {
                    SetTerritory(sel);
                    e.StopPropagation();
                });
                _territoryMenu.Add(item);
            }
            _territoryMenu.style.display = DisplayStyle.Flex;
            _territoryMenu.style.right = 0f;
            _territoryMenu.style.top = 24f;
            _territoryMenu.BringToFront();
            _territoryMenuOpen = true;
        }

        private void CloseTerritoryMenu()
        {
            _territoryMenu.style.display = DisplayStyle.None;
            _territoryMenuOpen = false;
        }

        /// <summary>영지 선택 옵션 목록 — TerritoryDatabase 정의(실측) + 현재 영지 + "default" 폴백.</summary>
        private List<string> BuildTerritoryOptions()
        {
            var options = new List<string>();
            if (TerritoryDatabase.Instance != null)
            {
                var defs = TerritoryDatabase.Instance.GetAllDefinitions();
                if (defs != null)
                {
                    foreach (var def in defs)
                        options.Add(def.id.ToString());
                }
            }
            if (options.Count == 0)
                options.Add("default");
            bool hasCurrent = false;
            foreach (var opt in options)
            {
                if (opt == _territoryId) { hasCurrent = true; break; }
            }
            if (!hasCurrent && !string.IsNullOrEmpty(_territoryId))
                options.Add(_territoryId);
            return options;
        }
    }
}