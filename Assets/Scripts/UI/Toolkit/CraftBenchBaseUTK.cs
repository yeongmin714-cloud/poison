using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.UI;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Shared presentation/composition for legacy weapon, cooking and alchemy benches.
    /// The recipe providers and TryCraft implementations remain owned by each concrete bench.
    /// One frameless UTK window owns three sibling Figma panel roots and its normal ESC lifecycle.
    /// </summary>
    public abstract class CraftBenchBaseUTK : UTKWindowBase
    {
        public struct BenchRecipe
        {
            public string ResultId;
            public string ResultName;
            public string[] MatIds;
            public string Note;
            public ItemRarity rarity;
        }

        public static readonly Rect CanvasBounds = new Rect(144f, 84f, 1632f, 912f);
        public static readonly Rect CraftingPanelBounds = new Rect(144f, 84f, 504f, 912f);
        public static readonly Rect DetailPanelBounds = new Rect(672f, 84f, 576f, 912f);
        public static readonly Rect StoragePanelBounds = new Rect(1272f, 84f, 504f, 912f);
        public static readonly Rect CraftHeaderBounds = new Rect(168f, 108f, 456f, 52.8f);
        public static readonly Rect CraftCombinationBounds = new Rect(168f, 236.4f, 456f, 194.4f);
        public static readonly Rect CraftRecipeListBounds = new Rect(168f, 470.4f, 456f, 419.2f);
        public static readonly Rect CraftFooterBounds = new Rect(168f, 908.8f, 456f, 63.2f);
        public static readonly Rect DetailHeaderBounds = new Rect(696f, 108f, 528f, 52.8f);
        public static readonly Rect DetailNameBounds = new Rect(696f, 180f, 528f, 75.6f);
        public static readonly Rect DetailImageBounds = new Rect(696f, 274.8f, 528f, 384f);
        public static readonly Rect DetailDescriptionBounds = new Rect(696f, 678f, 528f, 294f);
        public static readonly Rect StorageStatsBounds = new Rect(1296f, 180f, 456f, 24.6f);
        public static readonly Rect StorageGridBounds = new Rect(1296f, 223.8f, 456f, 537.6f);
        // [144차 P2-CRAFT W25/W58] 공통 진실치 — Divider(63:64 · 패널-로컬 (24,366,456,1.2)) · StorageFooter(63:297 · (24,697,456,56)).
        public static readonly Rect CraftDividerBounds = new Rect(168f, 450f, 456f, 1.2f);
        public static readonly Rect StorageFooterBounds = new Rect(1296f, 781f, 456f, 56f);
        public const int StorageCellCount = 25;
        public const int StorageColumnCount = 5;
        public const float StorageCellSize = 81.6f;
        public const float StorageCellGap = 9.6f;

        // ── [제작완료 연출] 3페이즈 상수 — A 빛 차오름(왼→오 물결) → B 번쩍 → C 조합품 등장 ──
        // 골드 #D4AF37(프로젝트 골드) · 플래시 순백 · 벡터메시 금지(84차 계약) — 일반 VisualElement+스타일만.
        private const float CraftFxGlowStagger = 0.12f;    // 페이즈 A — 슬롯당 스태거 (s)
        private const float CraftFxGlowInOut = 0.3f;       // 페이즈 A — 슬롯당 글로우 인/아웃 (s)
        private const float CraftFxFlashAt = 0.72f;        // 페이즈 B — 번쩍 시작 시점 (s)
        private const float CraftFxFlashFade = 0.35f;      // 페이즈 B — 플래시 페이드아웃 (s)
        private const float CraftFxFlashPeak = 0.85f;      // 페이즈 B — 플래시 알파 스파이크
        private const float CraftFxResultAt = 0.84f;       // 페이즈 C — 결과 등장 시점 (s)
        private const float CraftFxResultPop = 0.25f;      // 페이즈 C — 결과 스케일업 시간 (s)
        private const float CraftFxGlowPeak = 0.8f;        // 재료 슬롯 글로우 피크 알파
        private const float CraftFxResultGlowPeak = 0.9f;  // 결과 슬롯 글로우 피크 알파
        private long _craftFxGeneration;                   // [제작완료 연출] 세대 토큰 — 연속 재조합 시 이전 세션 정리가 신규 세션 요소를 조기 제거하는 레이스 방지
        private static readonly Color CraftFxGold = new Color(0xD4 / 255f, 0xAF / 255f, 0x37 / 255f, 1f); // 골드 #D4AF37

        protected virtual bool IsDiscovered(BenchRecipe recipe) => true;
        protected virtual string RateHint(BenchRecipe recipe) => "";
        protected virtual string BenchSubtitle => "CRAFTING";
        protected virtual string CombinationHeading => "조합 레시피 슬롯";
        protected virtual string RecipeHeading => "제작 가능한 레시피 목록";
        protected virtual string DetailHeading => "선택한 결과 상세";
        protected virtual string DetailDescriptionHeading => "레시피 및 아이템 정보";
        protected virtual string StorageHeading => "재료 보관함";
        protected virtual string StorageSource => "플레이어 인벤토리";
        protected virtual string CraftActionText => "아이템 제작하기 (CRAFT)";
        // [144차 P2-CRAFT W23] 성공확률 좌측 라벨 슬롯 — 파생별 문구 주입용(v1 "제작 성공 확률"/v2 "조리 성공 확률"/v3 "제조 성공 확률" — 파생 태스크에서 override).
        protected virtual string SuccessRateLabel => "제작 성공 확률";
        // [144차 P2-CRAFT W58] StorageFooter 라벨 슬롯 — PlayerInventory 무게 데이터 부재 → D7 슬롯 표기 계약(CookingWindowUTK C39 선례).
        // kg 위조 금지 — 실사용 슬롯수 표기. 파생 판본이 실무게 데이터를 확보하면 override로 문구 주입.
        protected virtual string StorageFooterWeightLabel => "사용 슬롯";
        protected virtual bool ShowThirdIngredientPlaceholder => false;
        protected virtual IReadOnlyList<string> RecipeFilters => new[] { "전체" };
        protected virtual bool MatchesFilter(BenchRecipe recipe, string filter) => filter == "전체";
        protected virtual PlayerInventory.ItemData GetResultItemData(BenchRecipe recipe)
            => PlayerInventory.GetItemById(recipe.ResultId);
        protected virtual string IngredientDisplayName(string itemId)
        {
            var item = PlayerInventory.GetItemById(itemId);
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : itemId;
        }

        protected readonly int SlotCount;
        private readonly UTKSlot[] _slots;
        private readonly string[] _placed;
        private readonly Label _status;
        private readonly Label _rate;
        private Label _detailName;
        private Label _detailRarity;
        private Label _detailDescription;
        private readonly Label _storageStats;
        private readonly Label _storageSourceLabel;
        private Label _storageFooterValue;              // [144차 P2-CRAFT W58] StorageFooter 값 라벨 — 갱신은 RefreshStorage
        private VisualElement _storageProgressFill;     // [144차 P2-CRAFT W58] StorageFooter 진행바 fill — 갱신은 RefreshStorage
        private VisualElement _detailImage;
        private readonly ScrollView _book;
        private readonly VisualElement _filterBar;
        private readonly Button _craftButton;
        private readonly UTKSlot _resultSlot;
        private readonly Label _resultLabel;
        private VisualElement _combinationContainer;   // [제작완료 연출] 조합 영역 컨테이너 — 화이트 플래시 오버레이 호스트
        private readonly VisualElement[] _storageCells = new VisualElement[StorageCellCount];
        private BenchRecipe _matched;
        private BenchRecipe _selected;
        private bool _hasSelection;
        private bool _craftable;
        private string _activeFilter = "전체";
        private bool _refreshing;
        private VisualElement _canvasLayoutRoot;   // [Figma 정합 v3] UIRoot 훅 — 등배수 디자인공간 스케일 재적용용

        public VisualElement CraftPanelRoot { get; private set; }
        public VisualElement DetailPanelRoot { get; private set; }
        public VisualElement StoragePanelRoot { get; private set; }

        protected CraftBenchBaseUTK(string title, int slotCount, Vector2 ignoredLegacySize)
            : base(title, CanvasBounds.size, UTKWindowChrome.Frameless)
        {
            SlotCount = slotCount;
            _slots = new UTKSlot[slotCount];
            _placed = new string[slotCount];
            pickingMode = PickingMode.Ignore;
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            style.backgroundImage = StyleKeyword.Null;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
            style.left = CanvasBounds.x;
            style.top = CanvasBounds.y;
            _content.style.position = Position.Relative;
            _content.style.width = CanvasBounds.width;
            _content.style.height = CanvasBounds.height;
            _content.style.paddingLeft = _content.style.paddingRight = 0f;
            _content.style.paddingTop = _content.style.paddingBottom = 0f;
            _content.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            _content.Clear();

            CraftPanelRoot = CreatePanel("craft-bench-crafting-panel", CraftingPanelBounds);
            DetailPanelRoot = CreatePanel("craft-bench-detail-panel", DetailPanelBounds);
            StoragePanelRoot = CreatePanel("craft-bench-storage-panel", StoragePanelBounds);
            _content.Add(CraftPanelRoot);
            _content.Add(DetailPanelRoot);
            _content.Add(StoragePanelRoot);

            AddPanelHeader(CraftPanelRoot, "craft-bench-crafting-header", title, BenchSubtitle, true);
            AddPanelHeader(DetailPanelRoot, "craft-bench-detail-header", DetailHeading, "SPECIFICATIONS", false);
            AddPanelHeader(StoragePanelRoot, "craft-bench-storage-header", StorageHeading, "INVENTORY", false);

            var filters = new VisualElement { name = "craft-bench-recipe-filters" };
            filters.style.position = Position.Absolute;
            filters.style.left = 24f;
            filters.style.top = 96f;
            filters.style.width = 456f;
            filters.style.height = 37.2f;
            filters.style.flexDirection = FlexDirection.Row;
            filters.style.alignItems = Align.Center;
            CraftPanelRoot.Add(filters);
            _filterBar = filters;

            var combination = CreateRegion("craft-bench-combination-section", new Rect(24f, 152.4f, 456f, 194.4f));
            CraftPanelRoot.Add(combination);
            _combinationContainer = combination;   // [제작완료 연출] 조합 영역 보관 — 플래시 오버레이 호스트
            var comboTitle = MakeLabel(CombinationHeading, 14.4f, UTKColor.TextSecondary);
            comboTitle.name = "craft-bench-combination-heading";
            comboTitle.style.position = Position.Absolute;
            comboTitle.style.left = 19.2f;
            comboTitle.style.top = 19.2f;
            combination.Add(comboTitle);

            var flow = new VisualElement { name = "craft-bench-combination-flow" };
            flow.style.position = Position.Absolute;
            flow.style.left = 19.2f;
            flow.style.top = 52f;
            flow.style.width = 417.6f;
            flow.style.height = 91.2f;
            flow.style.flexDirection = FlexDirection.Row;
            flow.style.alignItems = Align.Center;
            combination.Add(flow);
            bool showThirdSlot = ShowThirdIngredientPlaceholder && slotCount == 2;
            float ingredientSlotSize = showThirdSlot ? 60f : 76.8f;
            for (int i = 0; i < slotCount; i++)
            {
                int index = i;
                var slot = new UTKSlot { name = "BenchSlot_" + index };
                slot.style.width = ingredientSlotSize;
                slot.style.height = ingredientSlotSize;
                slot.style.marginRight = showThirdSlot ? 4f : 7.2f;
                slot.SetRank("common");
                // [144차 P2-CRAFT W18] TierStrip — 배치 전 숨김(데이터 위조 금지), 배치 시 실등급색 표시(SetSlotIcon).
                slot.SetTierStrip(false, UTKColor.TextSecondary);
                slot.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0) CyclePlace(index);
                    else if (evt.button == 1) ClearSlot(index);
                });
                _slots[index] = slot;
                flow.Add(slot);
            }
            if (showThirdSlot)
            {
                var placeholder = new UTKSlot { name = "BenchSlotPlaceholder_2", pickingMode = PickingMode.Ignore };
                placeholder.style.width = ingredientSlotSize;
                placeholder.style.height = ingredientSlotSize;
                placeholder.style.marginRight = 4f;
                placeholder.SetRank("common");
                placeholder.SetTierStrip(false, UTKColor.TextSecondary);   // [144차 P2-CRAFT W18] 플레이스홀더 — 실아이콘 부재 → 스트립 숨김 유지
                placeholder.tooltip = "추가 재료 슬롯 (시각 전용)";
                flow.Add(placeholder);
            }
            var arrow = MakeLabel("→", 21.6f, UTKColor.BorderBronze);
            arrow.name = "craft-bench-flow-arrow";
            arrow.style.marginLeft = 4f;
            arrow.style.marginRight = 8f;
            flow.Add(arrow);
            _resultSlot = new UTKSlot { name = "BenchResult" };
            _resultSlot.style.width = showThirdSlot ? 72f : 91.2f;
            _resultSlot.style.height = showThirdSlot ? 72f : 91.2f;
            _resultSlot.SetRank("common");
            _resultSlot.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) ExecuteCraft(); });
            flow.Add(_resultSlot);
            _resultLabel = MakeLabel("", 12f, UTKColor.TextPrimary);
            _resultLabel.style.whiteSpace = WhiteSpace.Normal;
            _resultLabel.style.maxWidth = 90f;
            _resultLabel.style.marginLeft = 8f;
            flow.Add(_resultLabel);
            _status = MakeLabel("재료를 배치하거나 레시피를 선택하세요.", 12f, UTKColor.TextSecondary);
            _status.name = "craft-bench-status";
            _status.style.position = Position.Absolute;
            _status.style.left = 19.2f;
            _status.style.top = 149f;
            _status.style.width = 280f;
            _status.style.whiteSpace = WhiteSpace.Normal;
            combination.Add(_status);
            _rate = MakeLabel("", 13.2f, UTKColor.GuildGreen);
            _rate.name = "craft-bench-success-rate";
            _rate.style.position = Position.Absolute;
            _rate.style.right = 19.2f;
            _rate.style.top = 158f;
            combination.Add(_rate);

            var recipeRegion = CreateRegion("craft-bench-recipe-list-section", new Rect(24f, 386.4f, 456f, 419.2f));
            CraftPanelRoot.Add(recipeRegion);
            var recipeHeading = MakeLabel(RecipeHeading, 14.4f, UTKColor.TextPrimary);
            recipeHeading.name = "craft-bench-recipe-heading";
            recipeRegion.Add(recipeHeading);
            _book = new ScrollView(ScrollViewMode.Vertical) { name = "craft-bench-recipe-list" };
            _book.style.position = Position.Absolute;
            _book.style.left = 0f;
            _book.style.top = 27f;
            _book.style.width = 456f;
            _book.style.height = 392.2f;
            recipeRegion.Add(_book);

            var footer = CreateRegion("craft-bench-craft-footer", new Rect(24f, 824.8f, 456f, 63.2f));
            CraftPanelRoot.Add(footer);
            _craftButton = MakeButton(CraftActionText, ExecuteCraft, "craft-bench-craft-button");
            _craftButton.style.position = Position.Absolute;
            _craftButton.style.left = 0f;
            _craftButton.style.top = 14.4f;
            _craftButton.style.width = 456f;
            _craftButton.style.height = 48.8f;
            footer.Add(_craftButton);

            BuildDetailPanel();
            _storageStats = MakeLabel("사용 슬롯: 0 / 0", 13.2f, UTKColor.TextSecondary);
            _storageStats.name = "craft-bench-storage-capacity";
            _storageStats.style.position = Position.Absolute;
            _storageStats.style.left = 24f;
            _storageStats.style.top = 96f;
            StoragePanelRoot.Add(_storageStats);
            _storageSourceLabel = MakeLabel(StorageSource, 12f, UTKColor.TextSecondary);
            _storageSourceLabel.name = "craft-bench-storage-source";
            _storageSourceLabel.style.position = Position.Absolute;
            _storageSourceLabel.style.left = 24f;
            _storageSourceLabel.style.top = 121f;
            StoragePanelRoot.Add(_storageSourceLabel);
            BuildStorageGrid();
            RebuildFilters();
            ApplyUIToolkitFont(this);
        }

        protected abstract IReadOnlyList<BenchRecipe> Recipes { get; }
        protected abstract bool TryCraft(BenchRecipe recipe, List<string> placedIds, out string message);
        protected virtual void OnCraftSuccess() => ClearAllSlots();

        protected override void OnWindowOpen()
        {
            RebuildFilters();
            RebuildBook();
            RefreshMatch();
            RefreshStorage();
        }

        // =====================================================================
        //  [Figma 정합 v3] 등배수 디자인공간 스케일 — 창 박스=CanvasBounds×k, 내부 raw px+scale=k
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
            ApplyBenchLayout();
        }

        private void OnCanvasRootGeometryChanged(GeometryChangedEvent evt) => ApplyBenchLayout();

        private void ApplyBenchLayout()
        {
            if (_canvasLayoutRoot == null) return;
            FigmaCanvasLayout.ApplyDesignSpace(this, _content, CanvasBounds, _canvasLayoutRoot);
        }

        private static VisualElement CreatePanel(string elementName, Rect canvasBounds)
        {
            var panel = new VisualElement { name = elementName };
            panel.AddToClassList("craft-bench-panel");
            panel.style.position = Position.Absolute;
            panel.style.left = canvasBounds.x - CanvasBounds.x;
            panel.style.top = canvasBounds.y - CanvasBounds.y;
            panel.style.width = canvasBounds.width;
            panel.style.height = canvasBounds.height;
            panel.style.overflow = Overflow.Hidden;
            UTKTheme.ApplyFigmaGlass(panel);   // [Figma 63:5/64:337 글래스] @0.85 + #30363D@0.5 1.2px + r14.4
            return panel;
        }

        private static VisualElement CreateRegion(string elementName, Rect bounds)
        {
            var region = new VisualElement { name = elementName };
            region.style.position = Position.Absolute;
            region.style.left = bounds.x;
            region.style.top = bounds.y;
            region.style.width = bounds.width;
            region.style.height = bounds.height;
            region.style.flexDirection = FlexDirection.Column;
            return region;
        }

        private void AddPanelHeader(VisualElement panel, string elementName, string title, string subtitle, bool close)
        {
            var header = CreateRegion(elementName, new Rect(24f, 24f, panel == DetailPanelRoot ? 528f : 456f, 52.8f));
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            var label = MakeLabel(title, 21.6f, UTKColor.TextPrimary);
            label.name = elementName + "-title";
            group.Add(label);
            var sub = MakeLabel(subtitle, 12f, UTKColor.TextSecondary);
            sub.style.marginLeft = 10f;
            group.Add(sub);
            header.Add(group);
            if (close)
            {
                var closeButton = MakeButton("✕", Close, "craft-bench-close-button");
                closeButton.style.width = 32f;
                closeButton.style.height = 32f;
                header.Add(closeButton);
            }
            panel.Add(header);
        }

        private void BuildDetailPanel()
        {
            var nameSection = CreateRegion("craft-bench-detail-name-section", new Rect(24f, 96f, 528f, 75.6f));
            nameSection.style.flexDirection = FlexDirection.Row;
            nameSection.style.alignItems = Align.Center;
            nameSection.style.justifyContent = Justify.SpaceBetween;
            DetailPanelRoot.Add(nameSection);
            _detailName = MakeLabel("레시피를 선택하세요.", 21.6f, UTKColor.TextPrimary);
            _detailName.name = "craft-bench-detail-name";
            _detailName.style.whiteSpace = WhiteSpace.Normal;
            nameSection.Add(_detailName);
            _detailRarity = MakeLabel("", 12f, UTKColor.AccentRare);
            _detailRarity.name = "craft-bench-detail-rarity";
            nameSection.Add(_detailRarity);

            var imageSection = CreateRegion("craft-bench-detail-image-section", new Rect(24f, 190.8f, 528f, 384f));
            imageSection.style.alignItems = Align.Center;
            imageSection.style.justifyContent = Justify.Center;
            imageSection.style.backgroundColor = new StyleColor(UTKTheme.PanelSub);
            DetailPanelRoot.Add(imageSection);
            _detailImage = new VisualElement { name = "craft-bench-detail-actual-item-icon" };
            _detailImage.style.width = 160f;
            _detailImage.style.height = 160f;
            _detailImage.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _detailImage.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _detailImage.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            imageSection.Add(_detailImage);

            var description = CreateRegion("craft-bench-detail-description-section", new Rect(24f, 594f, 528f, 294f));
            description.style.paddingLeft = description.style.paddingRight = 19.2f;
            description.style.paddingTop = 19.2f;
            DetailPanelRoot.Add(description);
            var heading = MakeLabel(DetailDescriptionHeading, 15.6f, UTKColor.TextPrimary);
            heading.name = "craft-bench-detail-description-heading";
            description.Add(heading);
            _detailDescription = MakeLabel("", 13.2f, UTKColor.TextSecondary);
            _detailDescription.name = "craft-bench-detail-description";
            _detailDescription.style.marginTop = 12f;
            _detailDescription.style.whiteSpace = WhiteSpace.Normal;
            _detailDescription.style.flexShrink = 1f;
            description.Add(_detailDescription);
        }

        private void BuildStorageGrid()
        {
            var grid = CreateRegion("craft-bench-storage-grid", new Rect(24f, 139.8f, 456f, 537.6f));
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            StoragePanelRoot.Add(grid);
            for (int i = 0; i < StorageCellCount; i++)
            {
                var cell = new VisualElement { name = "craft-bench-storage-cell-" + i };
                cell.style.width = StorageCellSize;
                cell.style.height = StorageCellSize;
                cell.style.marginRight = i % StorageColumnCount == StorageColumnCount - 1 ? 0f : StorageCellGap;
                cell.style.marginBottom = StorageCellGap;
                cell.style.backgroundColor = new StyleColor(UTKTheme.PanelSub);
                cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = 1f;
                cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = UTKTheme.Stroke;
                grid.Add(cell);
                _storageCells[i] = cell;
            }
        }

        private static Label MakeLabel(string text, float size, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = new StyleColor(color);
            return label;
        }

        private static Button MakeButton(string text, System.Action action, string elementName)
        {
            var button = new Button(action) { text = text, name = elementName };
            button.style.color = new StyleColor(UTKColor.TextPrimary);
            button.style.backgroundColor = new StyleColor(UTKTheme.PanelSub);
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 1f;
            button.style.borderTopColor = button.style.borderBottomColor = button.style.borderLeftColor = button.style.borderRightColor = UTKTheme.Stroke;
            return button;
        }

        private void RebuildFilters()
        {
            _filterBar.Clear();
            foreach (var filter in RecipeFilters)
            {
                string captured = filter;
                var button = MakeButton(filter, () =>
                {
                    _activeFilter = captured;
                    RebuildFilters();
                    RebuildBook();
                }, "craft-bench-filter-" + filter);
                button.style.height = 37.2f;
                button.style.marginRight = 4f;
                button.SetEnabled(captured != _activeFilter);
                _filterBar.Add(button);
            }
        }

        private void RebuildBook()
        {
            _book.Clear();
            foreach (var recipe in Recipes)
            {
                if (!MatchesFilter(recipe, _activeFilter)) continue;
                var row = new VisualElement { name = "craft-bench-recipe-row-" + recipe.ResultId };
                row.style.height = 72f;
                row.style.minHeight = 72f;
                row.style.marginBottom = 7.2f;
                row.style.paddingLeft = 9.6f;
                row.style.paddingRight = 9.6f;
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.035f));
                var item = GetResultItemData(recipe);
                var icon = new VisualElement { name = "recipe-result-icon" };
                icon.style.width = icon.style.height = 52.8f;
                if (item != null) icon.style.backgroundImage = UTKTextureSafe.ToBackground(ItemIconDatabase.GetOrCreateIcon(item));
                row.Add(icon);
                var text = new VisualElement();
                text.style.flexGrow = 1f;
                text.style.marginLeft = 12f;
                text.style.flexDirection = FlexDirection.Column;
                text.style.justifyContent = Justify.Center;
                var discovered = IsDiscovered(recipe);
                var name = MakeLabel(discovered ? recipe.ResultName : "?", 14.4f, UTKColor.TextPrimary);
                text.Add(name);
                var mats = MakeLabel(DescribeMats(recipe), 12f, UTKColor.TextSecondary);
                text.Add(mats);
                row.Add(text);
                var captured = recipe;
                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    _selected = captured;
                    _hasSelection = true;
                    AutoFill(captured);
                    RefreshDetail(captured);
                });
                _book.Add(row);
            }
        }

        private string DescribeMats(BenchRecipe recipe)
        {
            var pieces = new List<string>();
            var seen = new List<string>();
            if (recipe.MatIds == null) return "재료 정보 없음";
            foreach (var id in recipe.MatIds)
            {
                if (string.IsNullOrEmpty(id) || seen.Contains(id)) continue;
                seen.Add(id);
                int count = 0;
                foreach (var candidate in recipe.MatIds) if (candidate == id) count++;
                pieces.Add(IngredientDisplayName(id) + " x" + count);
            }
            return pieces.Count == 0 ? "재료 정보 없음" : string.Join(" · ", pieces);
        }

        private void CyclePlace(int index)
        {
            var candidates = IngredientCandidates();
            if (candidates.Count == 0) { _status.text = "배치할 레시피 재료가 플레이어 인벤토리에 없습니다."; return; }
            int start = _placed[index] != null ? candidates.FindIndex(c => c == _placed[index]) + 1 : 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                var id = candidates[(start + i) % candidates.Count];
                if (!CanPlace(id, index)) continue;
                _placed[index] = id;
                SetSlotIcon(index, id);
                RefreshMatch();
                return;
            }
        }

        private void PlaceFromStorage(string itemId)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (!string.IsNullOrEmpty(_placed[i])) continue;
                if (!CanPlace(itemId, i)) break;
                _placed[i] = itemId;
                SetSlotIcon(i, itemId);
                RefreshMatch();
                return;
            }
            _status.text = "빈 재료 슬롯이 없거나 이 아이템은 현재 레시피 재료가 아닙니다.";
        }

        private bool CanPlace(string itemId, int targetIndex)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null || inv.GetItemCount(itemId) <= 0) return false;
            int alreadyPlaced = 0;
            for (int i = 0; i < _placed.Length; i++) if (_placed[i] == itemId) alreadyPlaced++;
            return alreadyPlaced < inv.GetItemCount(itemId);
        }

        private void SetSlotIcon(int index, string itemId)
        {
            var item = PlayerInventory.GetItemById(itemId);
            _slots[index].SetIcon(item != null ? ItemIconDatabase.GetOrCreateIcon(item) : null);
        }

        private void ClearSlot(int index)
        {
            _placed[index] = null;
            _slots[index].SetIcon(null);
            RefreshMatch();
        }

        private void ClearAllSlots()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _placed[i] = null;
                _slots[i].SetIcon(null);
            }
            RefreshMatch();
        }

        private List<string> IngredientCandidates()
        {
            var result = new List<string>();
            var inventory = PlayerInventory.Instance;
            if (inventory == null) return result;
            foreach (var recipe in Recipes)
            {
                if (recipe.MatIds == null) continue;
                foreach (var id in recipe.MatIds)
                    if (!string.IsNullOrEmpty(id) && !result.Contains(id) && inventory.GetItemCount(id) > 0) result.Add(id);
            }
            return result;
        }

        private void AutoFill(BenchRecipe recipe)
        {
            ClearAllSlots();
            var inventory = PlayerInventory.Instance;
            if (inventory == null) return;
            var consumed = new Dictionary<string, int>();
            for (int i = 0; i < recipe.MatIds.Length && i < SlotCount; i++)
            {
                string id = recipe.MatIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                consumed.TryGetValue(id, out int used);
                if (inventory.GetItemCount(id) <= used)
                {
                    _status.text = "재료 부족 — " + DescribeMats(recipe);
                    continue;
                }
                _placed[i] = id;
                consumed[id] = used + 1;
                SetSlotIcon(i, id);
            }
            RefreshMatch();
        }

        private void RefreshMatch()
        {
            var placed = new List<string>();
            foreach (var id in _placed) if (!string.IsNullOrEmpty(id)) placed.Add(id);
            _craftable = false;
            _matched = default;
            _resultSlot.SetIcon(null);
            _resultSlot.SetRank("common");
            _resultLabel.text = "";
            _rate.text = "";
            if (placed.Count == 0)
            {
                _status.text = "재료를 배치하거나 레시피를 선택하세요.";
                _craftButton.SetEnabled(false);
                return;
            }
            foreach (var recipe in Recipes)
            {
                var need = new List<string>();
                if (recipe.MatIds != null) foreach (var id in recipe.MatIds) if (!string.IsNullOrEmpty(id)) need.Add(id);
                if (!SameMultiset(need, placed)) continue;
                _matched = recipe;
                _craftable = true;
                _selected = recipe;
                _hasSelection = true;
                var item = GetResultItemData(recipe);
                bool discovered = IsDiscovered(recipe);
                if (discovered)
                {
                    _resultSlot.SetIcon(item != null ? ItemIconDatabase.GetOrCreateIcon(item) : null);
                    _resultSlot.SetRank(item != null ? UTKRarity.ClassForIndex((int)item.rarity) : "common");
                    _resultLabel.text = recipe.ResultName;
                    _rate.text = RateHint(recipe);
                    _status.text = "제작 가능 — " + recipe.ResultName;
                }
                else
                {
                    _resultLabel.text = "?";
                    _status.text = "제작 가능 — 성공 시 레시피를 획득합니다.";
                }
                _craftButton.SetEnabled(true);
                RefreshDetail(recipe);
                return;
            }
            _status.text = "일치하는 제작법이 없습니다.";
            _craftButton.SetEnabled(false);
            if (_hasSelection) RefreshDetail(_selected);
        }

        private static bool SameMultiset(List<string> first, List<string> second)
        {
            if (first.Count != second.Count) return false;
            var pool = new List<string>(second);
            foreach (var item in first)
            {
                int index = pool.FindIndex(candidate => candidate == item);
                if (index < 0) return false;
                pool.RemoveAt(index);
            }
            return pool.Count == 0;
        }

        private void RefreshDetail(BenchRecipe recipe)
        {
            var item = GetResultItemData(recipe);
            string name = IsDiscovered(recipe) ? recipe.ResultName : "미확인 레시피";
            _detailName.text = name;
            _detailRarity.text = item != null ? item.rarity.ToString().ToUpperInvariant() : recipe.rarity.ToString().ToUpperInvariant();
            _detailImage.style.backgroundImage = item != null
                ? UTKTextureSafe.ToBackground(ItemIconDatabase.GetOrCreateIcon(item))
                : null;
            var lines = new List<string>();
            if (item != null && !string.IsNullOrEmpty(item.description)) lines.Add(item.description);
            if (!string.IsNullOrEmpty(recipe.Note)) lines.Add(recipe.Note);
            lines.Add("필요 재료: " + DescribeMats(recipe));
            string rate = RateHint(recipe);
            if (!string.IsNullOrEmpty(rate)) lines.Add(rate);
            if (item == null) lines.Add("결과 아이템 상세 데이터는 등록되어 있지 않습니다.");
            _detailDescription.text = string.Join("\n\n", lines);
        }

        private void RefreshStorage()
        {
            var inventory = PlayerInventory.Instance;
            var all = inventory != null ? inventory.GetAllSlots() : null;
            int occupied = 0;
            if (all != null)
                foreach (var entry in all) if (entry != null && entry.item != null && entry.count > 0) occupied++;
            _storageStats.text = "사용 슬롯: " + occupied + " / " + (all != null ? all.Length : 0);
            _storageSourceLabel.text = StorageSource;
            for (int i = 0; i < _storageCells.Length; i++) _storageCells[i].Clear();
            if (all == null) return;
            int visible = Mathf.Min(StorageCellCount, all.Length);
            for (int i = 0; i < visible; i++)
            {
                var entry = all[i];
                if (entry == null || entry.item == null || entry.count <= 0) continue;
                int capturedIndex = i;
                var slot = new UTKSlot { name = "craft-bench-storage-item-" + i };
                slot.style.position = Position.Absolute;
                slot.style.left = slot.style.top = 0f;
                slot.style.width = Length.Percent(100f);
                slot.style.height = Length.Percent(100f);
                slot.style.marginLeft = slot.style.marginRight = slot.style.marginTop = slot.style.marginBottom = 0f;
                slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(entry.item));
                slot.SetCount(entry.count);
                slot.SetRank(UTKRarity.ClassForIndex((int)entry.item.rarity));
                slot.tooltip = entry.item.displayName;
                string itemId = entry.item.id;
                slot.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) PlaceFromStorage(itemId); });
                _storageCells[capturedIndex].Add(slot);
            }
        }

        // =====================================================================
        //  [제작완료 연출] 3페이즈 완료 연출 — 순수 장식(로직 결합 금지)
        //  A 빛 차오름(0~0.72s): 재료 슬롯 왼→오 골드 글로우 물결(스태거 0.12s · 인/아웃 0.3s)
        //  B 번쩍(0.72s): 조합 영역 화이트 플래시 — 알파 0.85 스파이크 후 0.35s 페이드아웃
        //  C 조합품 등장(0.84s): 결과 슬롯 scale 0.6→1.0 0.25s EaseOutQuad + 골드 글로우 페이드
        //  드라이버: schedule.Execute().Every(16ms) — 진행도 기반 순수함수 업데이트.
        //  파괴/탈착 시 조기 종료(EditMode/배치 안전 — 스케줄러 미구동 시에도 로직은 이미 반영 완료라 무해).
        //  벡터메시 금지(84차 계약) — 일반 VisualElement+스타일만.
        // =====================================================================
        private void StartCraftCompletionFx()
        {
            if (SlotCount <= 0 || _combinationContainer == null) return;
            long generation = ++_craftFxGeneration;   // 신규 세션 선점 — 이전 세션의 종료 정리가 본 세션 요소를 건드리지 않는다
            RemoveCraftCompletionFxElements();   // 재연출 방어 — 잔여 연출 요소 제거 후 시작

            // 페이즈 A — 재료 슬롯 글로우 오버레이(연출 시작 시에만 생성 → 기존 창 렌더 무변화)
            var glows = new VisualElement[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                glows[i] = CreateCraftGlowOverlay(_slots[i]);

            // 페이즈 B — 조합(combination) 영역 절대좌표 풀사이즈 화이트 플래시 오버레이
            var flash = new VisualElement { name = "CraftFlashOverlay", pickingMode = PickingMode.Ignore };
            flash.style.position = Position.Absolute;
            flash.style.left = flash.style.top = 0f;
            flash.style.width = flash.style.height = new Length(100f, LengthUnit.Percent);
            flash.style.backgroundColor = new StyleColor(Color.white);   // 플래시 순백
            flash.style.opacity = 0f;
            _combinationContainer.Add(flash);

            // 페이즈 C — 결과 슬롯 골드 글로우(팝과 함께 페이드)
            var resultGlow = CreateCraftGlowOverlay(_resultSlot);

            // 드라이버 — 16ms 틱, 진행도 기반 순수함수 업데이트
            long tick = 0L;
            IVisualElementScheduledItem task = null;
            task = schedule.Execute(() =>
            {
                // 조기 종료 가드 — 창 닫힘/파괴(탈착) 시 연출 중단(아이템 수령은 이미 완료)
                if (panel == null || _combinationContainer.panel == null)
                {
                    task.Pause();
                    return;
                }
                tick += 16L;
                float t = tick / 1000f;
                bool done = true;

                // 페이즈 A — 왼쪽→오른쪽 글로우 물결(스태거 0.12s · 인/아웃 0.3s 삼각파)
                float glowHalf = CraftFxGlowInOut * 0.5f;
                for (int i = 0; i < glows.Length; i++)
                {
                    var glow = glows[i];
                    if (glow == null) continue;
                    float local = t - i * CraftFxGlowStagger;
                    float a = local <= 0f || local >= CraftFxGlowInOut ? 0f
                        : local < glowHalf ? local / glowHalf
                        : 1f - (local - glowHalf) / glowHalf;
                    if (glow.panel != null) glow.style.opacity = a * CraftFxGlowPeak;
                    if (local < CraftFxGlowInOut) done = false;
                }

                // 페이즈 B — 화이트 플래시(0.72s 스파이크 → 0.35s 페이드아웃)
                float flashLocal = t - CraftFxFlashAt;
                float flashAlpha = flashLocal < 0f || flashLocal >= CraftFxFlashFade ? 0f
                    : CraftFxFlashPeak * (1f - flashLocal / CraftFxFlashFade);
                if (flash.panel != null) flash.style.opacity = flashAlpha;
                if (flashLocal < CraftFxFlashFade) done = false;

                // 페이즈 C — 결과 팝(0.84s → 0.25s EaseOutQuad 0.6→1.0) + 결과 글로우 페이드
                float popLocal = t - CraftFxResultAt;
                if (popLocal < 0f)
                {
                    done = false;
                }
                else if (popLocal <= CraftFxResultPop)
                {
                    float u = popLocal / CraftFxResultPop;
                    float ease = 1f - (1f - u) * (1f - u);   // EaseOutQuad
                    float s = 0.6f + 0.4f * ease;
                    _resultSlot.style.scale = new Scale(new Vector2(s, s));
                    if (resultGlow != null && resultGlow.panel != null)
                        resultGlow.style.opacity = CraftFxResultGlowPeak * (1f - u);
                    done = false;
                }
                else if (resultGlow != null && resultGlow.panel != null)
                {
                    resultGlow.style.opacity = 0f;
                }

                if (done)
                {
                    if (generation == _craftFxGeneration)
                    {
                        RemoveCraftCompletionFxElements();   // 페이드 후 제거(신규 세션 없을 때만)
                        _resultSlot.style.scale = new Scale(new Vector2(1f, 1f));   // 원복(1,1)
                    }
                    task.Pause();
                }
            }).Every(16L);
        }

        /// <summary>[제작완료 연출] 슬롯에 절대좌표 풀사이즈 골드 글로우 오버레이 부착 — 호출 시에만 생성(파라미터 없는 기본 렌더 무변화).</summary>
        private VisualElement CreateCraftGlowOverlay(VisualElement slot)
        {
            if (slot == null) return null;
            var glow = new VisualElement { name = "CraftGlowOverlay", pickingMode = PickingMode.Ignore };
            glow.style.position = Position.Absolute;
            glow.style.left = glow.style.top = 0f;
            glow.style.width = glow.style.height = new Length(100f, LengthUnit.Percent);
            glow.style.backgroundColor = new StyleColor(CraftFxGold);   // 골드 #D4AF37
            glow.style.opacity = 0f;   // 개별 opacity 제어
            slot.Add(glow);
            return glow;
        }

        /// <summary>[제작완료 연출] 잔여 연출 요소 제거 — 재연출/재창 멱등(연출 요소는 연출 시작 시에만 존재).</summary>
        private void RemoveCraftCompletionFxElements()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] != null) RemoveCraftFxChildren(_slots[i]);
            }
            if (_resultSlot != null) RemoveCraftFxChildren(_resultSlot);
            if (_combinationContainer != null) RemoveCraftFxChildren(_combinationContainer);
        }

        private static void RemoveCraftFxChildren(VisualElement host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var child = host[i];
                if (child != null && (child.name == "CraftGlowOverlay" || child.name == "CraftFlashOverlay"))
                    child.RemoveFromHierarchy();
            }
        }

        private void ExecuteCraft()
        {
            if (!_craftable) return;
            var placed = new List<string>();
            foreach (var id in _placed) if (!string.IsNullOrEmpty(id)) placed.Add(id);
            bool craftSucceeded = TryCraft(_matched, placed, out string message);   // [제작완료 연출] 성공 판정 지역화 — 기존 분기 흐름 동일
            if (craftSucceeded)
            {
                OnCraftSuccess();
                _status.text = message;
            }
            else
            {
                RefreshMatch();
                _status.text = message;
            }
            RebuildBook();
            RefreshStorage();
            if (craftSucceeded) StartCraftCompletionFx();   // [제작완료 연출] 로직(수령/책갈피/보관함) 전부 반영 후 순수 장식 시작 — 실패 분기는 연출 없음
        }

        protected override void OnWindowClosed()
        {
            base.OnWindowClosed();
            RemoveCraftCompletionFxElements();              // [제작완료 연출] 연출 중 창 닫힘 — 잔여 오버레이 제거(재창 시 기존 렌더 무변화)
            _resultSlot.style.scale = new Scale(new Vector2(1f, 1f));
        }
    }
}
