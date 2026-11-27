// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // PlayerInventory, PlayerStats, RecipeDiscoverySystem
using ProjectName.Systems;         // RecipeCatalog (카테고리 조합 요리 카탈로그)
using ProjectName.UI;              // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit — 요리 창 (Figma 3슬롯 카테고리 조합 요리 UI, 2026-09-24 리빌드).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md + Figma 'crafting-panel' 요리 변형.
    ///
    /// [기능 — Figma 3-slot flow]
    ///   ① 상단: 재료 슬롯 3개(재료 1/2/3) + 흐름 화살표(→) + 결과 미리보기 슬롯.
    ///   ② 성공률 라벨: RecipeCatalog.FindRecipe 매칭 시 100%, 미매칭 '조합 없음'.
    ///   ③ 좌측: 레시피 목록(전체/작물/생선/몬스터 탭) — 클릭 시 보유 재료로 슬롯 자동 채움 + 상세 표시.
    ///   ④ 우측: 재료 인벤토리 그리드(Meat/Food/어류 Material) — 클릭 시 첫 빈 슬롯에 배치, 슬롯 우클릭 해제.
    ///   ⑤ COOK: 재료 확인 → RemoveItem(1씩) → 요리 ItemData(id='dish_'+recipe.id) 지급 + EXP.
    ///   카테고리 해석: RecipeCatalog.CropCategory / FishCategory / MonsterCategory (표시명 기반).
    ///   [CookingUTK] 로그.
    ///
    /// 300ms 폴링 갱신 (재료 수량 반영).
    /// </summary>
    public class CookingWindowUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static CookingWindowUTK _instance;
        public static CookingWindowUTK Instance => _instance;

        /// <summary>팩토리 — 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new CookingWindowUTK();
        }

        /// <summary>요리 창 열기(팩토리 겸용).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>닫혀있으면 열고, 열려있으면 닫음.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
            _instance.Show();
        }

        // ===== 설정 =====
        // Verified saved Figma frame 64:2 (crafting-panel), in 1920x1080 canvas pixels.
        // The three panel rectangles are sibling VisualElements inside this one transaction owner.
        public static readonly Rect FigmaBounds = new Rect(144f, 84f, 1632f, 912f);
        public static readonly Rect CraftingPanelBounds = new Rect(144f, 84f, 504f, 912f);
        public static readonly Rect DetailPanelBounds = new Rect(672f, 84f, 576f, 912f);
        public static readonly Rect StoragePanelBounds = new Rect(1272f, 84f, 504f, 912f);
        private const float SlotSize = 76.8f;
        public const int StoragePlaceholderCount = 25;
        private const int StorageGridColumns = 5;
        private const long RefreshMs = 300L;
        private const float InnerLeft = 24f;
        private const float InnerWidth = 456f;
        private static readonly Rect HeaderLocalBounds = new Rect(24f, 24f, 456f, 52.8f);
        private static readonly Rect TabsLocalBounds = new Rect(24f, 96f, 456f, 37.2f);
        private static readonly Rect CombinationLocalBounds = new Rect(24f, 152.4f, 456f, 194.4f);
        private static readonly Rect RecipeLocalBounds = new Rect(24f, 386.4f, 456f, 419.2f);
        // [144차 P2-COOK C20] 64:103 CraftFooter (24,825,456,63.2) — 구식 824.8 교정.
        private static readonly Rect FooterLocalBounds = new Rect(24f, 825f, 456f, 63.2f);
        private static readonly Rect DetailNameLocalBounds = new Rect(24f, 96f, 528f, 75.6f);
        private static readonly Rect DetailImageLocalBounds = new Rect(24f, 190.8f, 528f, 384f);
        private static readonly Rect DetailDescriptionLocalBounds = new Rect(24f, 594f, 528f, 294f);
        private static readonly Rect StorageStatsLocalBounds = new Rect(24f, 96f, 456f, 24.6f);
        private static readonly Rect StorageGridLocalBounds = new Rect(24f, 139.8f, 456f, 537.6f);
        private static readonly Rect StorageFooterLocalBounds = new Rect(24f, 696.6f, 456f, 56f);

        // ===== 레시피 탭 필터 =====
        private enum RecipeTab { All, Crop, Fish, Monster }

        // ===== 상태 =====
        private readonly PlayerInventory.ItemData[] _slots = new PlayerInventory.ItemData[3];
        private RecipeTab _tab = RecipeTab.All;
        private RecipeCatalog.RecipeDef? _selectedRecipe;

        // ===== 레퍼런스 =====
        private readonly Label[] _slotLabels = new Label[3];
        private readonly UTKSlot[] _slotCells = new UTKSlot[3];
        private UTKSlot _resultCell;
        private Label _resultNameLabel;
        private Label _rateValueLabel;               // [144차 P2-COOK C14] EstimatedRate 우측 값 라벨(GeistMono700 #56D364)
        private VisualElement _tabsHost;             // [144차 P2-COOK C5] 탭바 호스트 — (24,96,456,37.2) 상단
        private string _selectedRecipeId;            // [144차 P2-COOK C19] 목록 선택 행 추적(SELECTED 배지)
        private Label _detailEnNameLabel;            // [144차 P2-COOK C25] 영문 아이템명 슬롯(데이터 부재 → "—")
        private VisualElement _detailTierBadgeHost;  // [144차 P2-COOK C26] 등급 배지 호스트(UTKTheme.CreateTierBadge)
        // [144차 P2-COOK C14/C31] Figma 진실치 값색 — 64:65 #56D364 / 64:164 #FF7B72.
        private static readonly Color CookValueGreen = new Color(0x56 / 255f, 0xD3 / 255f, 0x64 / 255f, 1f);
        private static readonly Color CookStatRed = new Color(0xFF / 255f, 0x7B / 255f, 0x72 / 255f, 1f);
        private static Font _geistMonoFont;          // [144차 P2-COOK C14/C19/C31] GeistMono 폴백 캐시
        private Label _detailLabel;
        private Label _detailStats;
        private PlayerInventory.ItemData _selectedDetailItem;
        private VisualElement _recipeListHost;
        private VisualElement _ingGridHost;
        private VisualElement _craftPanel;
        private VisualElement _detailPanel;
        private VisualElement _storagePanel;
        private VisualElement _detailItemIcon;
        private Label _detailDescriptionLabel;
        private Label _detailEffectsLabel;
        private Label _detailIngredients;   // [Figma 64:2 StatsGrid] 레시피 재료 StatRow
        private Label _messageLabel;
        private IVisualElementScheduledItem _refreshTask;
        private VisualElement _canvasLayoutRoot;

        private CookingWindowUTK() : base("🍳 요리 테이블", FigmaBounds.size, UTKWindowChrome.Frameless)
        {
            // One frameless owner retains the RecipeCatalog transaction and ESC lifecycle; its
            // three positioned panel roots reproduce the sibling composition from Figma 64:2.
            pickingMode = PickingMode.Ignore;
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            _content.pickingMode = PickingMode.Position;
            style.backgroundImage = StyleKeyword.Null;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
            style.left = FigmaBounds.x;
            style.top = FigmaBounds.y;
            _content.style.flexGrow = 1f;
            _content.style.position = Position.Relative;
            _content.style.flexDirection = FlexDirection.Row;

            BuildPanelRoots();
            BuildPanelHeaders();
            BuildTabBar();      // [144차 P2-COOK C5] 탭바 (24,96,456,37.2) 상단 이동(하단 레시피 섹션 내장 폐기)
            BuildSlotRow();
            BuildRateLabel();
            BuildMainSplit();
            BuildFooter();
            BuildStoragePanelContents();

            ApplyUIToolkitFont(this);

            RefreshAll();
            style.display = DisplayStyle.None;
            Debug.Log("[CookingUTK] 요리 창 생성됨 (3슬롯 카테고리 조합)");
        }

        private void BuildPanelRoots()
        {
            _craftPanel = CreatePanel("cooking-craft-panel", "Craft", CraftingPanelBounds);
            _detailPanel = CreatePanel("cooking-detail-panel", "Detail", DetailPanelBounds);
            _storagePanel = CreatePanel("cooking-storage-panel", "Storage", StoragePanelBounds);
            _content.Add(_craftPanel);
            _content.Add(_detailPanel);
            _content.Add(_storagePanel);

            // Reparent the existing widgets' owning containers; event handlers and recipe state remain
            // on this single CookingWindowUTK instance.
            _craftPanel.style.flexDirection = FlexDirection.Column;
            _detailPanel.style.flexDirection = FlexDirection.Column;
            _storagePanel.style.flexDirection = FlexDirection.Column;
        }

        private static VisualElement CreatePanel(string elementName, string title, Rect bounds)
        {
            var panel = new VisualElement { name = elementName };
            panel.AddToClassList("cooking-panel");
            panel.style.position = Position.Absolute;
            panel.style.left = bounds.x - FigmaBounds.x;
            panel.style.top = bounds.y - FigmaBounds.y;
            panel.style.overflow = Overflow.Hidden;
            panel.style.width = bounds.width;
            panel.style.height = bounds.height;
            panel.style.paddingLeft = panel.style.paddingRight = 0f;
            panel.style.paddingTop = panel.style.paddingBottom = 0f;
            UTKTheme.ApplyFigmaGlass(panel);   // [Figma 64:2 글래스] @0.85 + #30363D@0.5 1.2px + r14.4
            return panel;
        }

        private void BuildPanelHeaders()
        {
            AddPanelHeader(_craftPanel, "요리 제작", "COOKING", HeaderLocalBounds, true);
            AddPanelHeader(_detailPanel, "요리 정보", "RECIPE", HeaderLocalBounds, true);   // [144차 P2-COOK C22] 64:121-122 진실 문구
            AddPanelHeader(_storagePanel, "재료 가방", "INGREDIENTS", HeaderLocalBounds, true);
        }

        private void AddPanelHeader(VisualElement panel, string title, string subtitle, Rect bounds, bool close)
        {
            var header = new VisualElement { name = panel.name + "-header" };
            header.style.position = Position.Absolute;
            header.style.left = bounds.x;
            header.style.top = bounds.y;
            header.style.width = bounds.width;
            header.style.height = bounds.height;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            var group = new VisualElement { name = panel.name + "-title-group" };
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            // [Figma 64:2 PanelHeader] 제목 24/700 + 영문 서브 14.4/500
            var label = new Label(title);
            label.style.fontSize = UTKTheme.FontTitleLarge;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new StyleColor(UTKColor.TextPrimary);
            group.Add(label);
            var subtitleLabel = new Label(subtitle);
            subtitleLabel.style.fontSize = UTKTheme.FontTab;
            subtitleLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            subtitleLabel.style.marginLeft = 10f;
            subtitleLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            group.Add(subtitleLabel);
            header.Add(group);
            if (close)
            {
                var closeButton = UTKButton.Create("✕", () => panel.style.display = DisplayStyle.None, UTKButton.Variant.Secondary);
                closeButton.name = "cooking-close-button";
                closeButton.style.width = 31.2f;
                closeButton.style.height = 31.2f;
                header.Add(closeButton);
            }
            panel.Add(header);
        }

        private VisualElement MakeStorageGrid()
        {
            var grid = new VisualElement { name = "cooking-storage-grid" };
            grid.style.position = Position.Absolute;
            grid.style.left = StorageGridLocalBounds.x;
            grid.style.top = StorageGridLocalBounds.y;
            grid.style.width = StorageGridLocalBounds.width;
            grid.style.height = StorageGridLocalBounds.height;
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.FlexStart;
            for (int i = 0; i < StoragePlaceholderCount; i++)
            {
                var placeholder = new VisualElement { name = "cooking-storage-placeholder-" + i };
                placeholder.AddToClassList("cooking-storage-placeholder");
                placeholder.style.width = 81.6f;
                placeholder.style.height = 81.6f;
                placeholder.style.flexShrink = 0f;
                placeholder.style.marginLeft = 0f;
                placeholder.style.marginTop = 0f;
                placeholder.style.marginRight = (i % StorageGridColumns == StorageGridColumns - 1) ? 0f : 9.6f;
                // [Figma 64:2 GridRow pitch 91.2] 모든 셀 세로 gap 9.6 — 마지막 행도 다음 행 예약 간격 유지
                placeholder.style.marginBottom = 9.6f;
                placeholder.style.borderTopWidth = placeholder.style.borderBottomWidth = 1f;
                placeholder.style.borderLeftWidth = placeholder.style.borderRightWidth = 1f;
                placeholder.style.borderTopColor = placeholder.style.borderBottomColor = UTKTheme.Stroke;
                placeholder.style.borderLeftColor = placeholder.style.borderRightColor = UTKTheme.Stroke;
                placeholder.style.backgroundColor = new StyleColor(UTKTheme.PanelSub);
                // [144차 P2-COOK C37] 64:274-276 빈셀 circle-dashed 28.8 @(26,27) 근사 — ◇ 글리프 폐기.
                // (V3 SetEmptyIcon 38.4 진실치는 loot 전용 — 본 창은 64:274 진실 28.8 @(26,27) 직접 근사)
                var emptyMark = new VisualElement { name = "cooking-empty-slot-mark-" + i, pickingMode = PickingMode.Ignore };
                emptyMark.style.position = Position.Absolute;
                emptyMark.style.left = 26f;
                emptyMark.style.top = 27f;
                emptyMark.style.width = 28.8f;
                emptyMark.style.height = 28.8f;
                UTKTheme.ApplyRadius(emptyMark, 14.4f);
                var dashColor = new StyleColor(UTKTheme.BorderChrome);   // 64:276 #30363D@0.502
                emptyMark.style.borderTopColor = dashColor;
                emptyMark.style.borderBottomColor = dashColor;
                emptyMark.style.borderLeftColor = dashColor;
                emptyMark.style.borderRightColor = dashColor;
                emptyMark.style.borderTopWidth = 2.4f;
                emptyMark.style.borderBottomWidth = 2.4f;
                emptyMark.style.borderLeftWidth = 2.4f;
                emptyMark.style.borderRightWidth = 2.4f;
                placeholder.Add(emptyMark);
                grid.Add(placeholder);
                _storageCells[i] = placeholder;
            }
            return grid;
        }

        private void RefreshStoragePanel()
        {
            if (_storageItemsHost == null) return;
            _storageItemsHost.Clear();
            for (int i = 0; i < _storageCells.Length; i++)
                _storageCells[i].Clear();
            var inv = PlayerInventory.Instance;
            var items = new List<PlayerInventory.ItemSlot>();
            var all = inv != null ? inv.GetAllSlots() : null;
            int occupiedSlots = 0;
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    var slot = all[i];
                    if (slot == null || slot.item == null || slot.count <= 0) continue;
                    occupiedSlots++;
                    if (IsCookingIngredient(slot.item)) items.Add(slot);
                }
            }
            // PlayerInventory 기본 슬롯 용량 40 — 인스턴스 부재(EditMode) 시에도 진실 표기. 무게 데이터는 존재하지 않으므로 위조 금지.
            int capacity = all != null ? all.Length : 40;
            _storageCapacityLabel.text = $"사용 슬롯: {occupiedSlots} / {capacity}";
            // [144차 P2-COOK C38/C39] 무게 데이터 부재 → 값 "—" 슬롯(위조 금지 D7) — 슬롯 점유율은 트랙 fill로만 표기.
            _storagePanel.Q<Label>("cooking-storage-weight-value").text = "—";
            _storageCapacityFill.style.width = capacity > 0
                ? Mathf.Min(InnerWidth, InnerWidth * occupiedSlots / (float)capacity)
                : 0f;
            for (int i = 0; i < StoragePlaceholderCount; i++)
            {
                VisualElement cell = _storageCells[i];
                bool hasItem = i < items.Count;
                var emptyMark = cell.Q<VisualElement>("cooking-empty-slot-mark-" + i);
                if (emptyMark != null) emptyMark.style.display = hasItem ? DisplayStyle.None : DisplayStyle.Flex;
            }

            int fixedCount = Mathf.Min(items.Count, StoragePlaceholderCount);
            for (int i = 0; i < fixedCount; i++)
            {
                var ingredient = items[i].item;
                var entry = new UTKSlot();
                entry.name = "cooking-storage-item-" + i;
                entry.style.position = Position.Absolute;
                entry.style.left = 0f;
                entry.style.top = 0f;
                entry.style.width = Length.Percent(100f);
                entry.style.height = Length.Percent(100f);
                // UTKSlot's shared USS margins otherwise extend the overlay outside the fixed cell.
                entry.style.marginLeft = 0f;
                entry.style.marginRight = 0f;
                entry.style.marginTop = 0f;
                entry.style.marginBottom = 0f;
                entry.SetIcon(ItemIconDatabase.GetOrCreateIcon(items[i].item));
                entry.SetCount(items[i].count);
                entry.SetRank(UTKRarity.ClassForIndex((int)items[i].item.rarity));
                entry.tooltip = items[i].item.displayName;
                entry.pickingMode = PickingMode.Position;
                entry.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 1) return;
                    PlaceIngredient(ingredient);
                });
                _storageCells[i].Add(entry);
            }
            for (int i = StoragePlaceholderCount; i < items.Count; i++)
            {
                var entry = new UTKSlot();
                entry.name = "cooking-storage-item-overflow-" + (i - StoragePlaceholderCount);
                entry.style.width = 81.6f;
                entry.style.height = 81.6f;
                entry.style.marginRight = ((i - StoragePlaceholderCount) % StorageGridColumns == StorageGridColumns - 1) ? 0f : 9.6f;
                entry.style.marginBottom = 9.6f;
                entry.SetIcon(ItemIconDatabase.GetOrCreateIcon(items[i].item));
                entry.SetCount(items[i].count);
                entry.SetRank(UTKRarity.ClassForIndex((int)items[i].item.rarity));
                entry.tooltip = items[i].item.displayName;
                entry.pickingMode = PickingMode.Ignore;
                _storageItemsHost.Add(entry);
            }
        }

        private VisualElement _storageItemsHost;
        private VisualElement _storageScroll;
        private Label _storageCapacityLabel;
        private VisualElement _storageCapacityFill;
        private readonly VisualElement[] _storageCells = new VisualElement[StoragePlaceholderCount];

        private void BuildStoragePanelContents()
        {
            // [144차 P2-COOK C35] 64:194 BadgeItem — r4.8 bg #FFFFFF@0.039 "보유 재료 리스트" 13.2/700 #F0F6FC
            // (CreateCountBadge는 r7.2/#000@0.251 체계로 불일치 → 직접 구현).
            var statsBadge = new VisualElement { name = "cooking-storage-stats-badge" };
            statsBadge.style.position = Position.Absolute;
            statsBadge.style.left = StorageStatsLocalBounds.x;
            statsBadge.style.top = StorageStatsLocalBounds.y;
            statsBadge.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.039f));
            UTKTheme.ApplyRadius(statsBadge, 4.8f);
            statsBadge.style.paddingLeft = statsBadge.style.paddingRight = 9.6f;
            statsBadge.style.paddingTop = statsBadge.style.paddingBottom = 4.8f;
            var badgeText = new Label("보유 재료 리스트") { name = "cooking-storage-section-label" };
            badgeText.style.fontSize = 13.2f;   // [C35] 12→13.2
            badgeText.style.unityFontStyleAndWeight = FontStyle.Bold;
            badgeText.style.color = new StyleColor(UTKColor.TextPrimary);   // #F0F6FC
            statsBadge.Add(badgeText);
            _storagePanel.Add(statsBadge);
            _storageCapacityLabel = new Label("사용 슬롯: 0 / 25") { name = "cooking-storage-capacity" };
            _storageCapacityLabel.style.position = Position.Absolute;
            _storageCapacityLabel.style.right = InnerLeft;
            _storageCapacityLabel.style.top = 93f;   // [144차 P2-COOK C35] capacity top 96→93
            _storageCapacityLabel.style.width = 140f;
            _storageCapacityLabel.style.height = StorageStatsLocalBounds.height;
            _storageCapacityLabel.style.fontSize = 14.4f;   // [144차 P2-COOK C35] 12→14.4(GeistMono400 폴백)
            _storageCapacityLabel.style.unityFont = GetGeistMonoFont();
            _storageCapacityLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _storageCapacityLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _storagePanel.Add(_storageCapacityLabel);

            _storageScroll = new ScrollView(ScrollViewMode.Vertical) { name = "cooking-storage-scroll" };
            _storageScroll.style.position = Position.Absolute;
            _storageScroll.style.left = StorageGridLocalBounds.x;
            _storageScroll.style.top = StorageGridLocalBounds.y;
            _storageScroll.style.width = StorageGridLocalBounds.width;
            _storageScroll.style.height = StorageGridLocalBounds.height;
            _storagePanel.Add(_storageScroll);
            var content = _storageScroll.contentContainer;
            content.style.width = StorageGridLocalBounds.width;
            content.Add(MakeStorageGrid());
            _storageItemsHost = new VisualElement { name = "cooking-storage-overflow" };
            _storageItemsHost.style.position = Position.Absolute;
            _storageItemsHost.style.top = StorageGridLocalBounds.height + 12f;
            _storageItemsHost.style.left = 0f;
            _storageItemsHost.style.flexDirection = FlexDirection.Row;
            _storageItemsHost.style.flexWrap = Wrap.Wrap;
            _storageItemsHost.style.width = StorageGridLocalBounds.width;
            content.Add(_storageItemsHost);

            var footer = new VisualElement { name = "cooking-storage-footer" };
            footer.style.position = Position.Absolute;
            footer.style.left = StorageFooterLocalBounds.x;
            footer.style.top = StorageFooterLocalBounds.y;
            footer.style.width = StorageFooterLocalBounds.width;
            footer.style.height = StorageFooterLocalBounds.height;
            footer.style.flexShrink = 0f;
            footer.style.flexDirection = FlexDirection.Column;

            var weightRow = new VisualElement { name = "cooking-storage-capacity-row" };
            weightRow.style.position = Position.Absolute;
            weightRow.style.left = 0f;
            weightRow.style.top = 14f;   // [144차 P2-COOK C38] 64:328 top 14
            weightRow.style.width = InnerWidth;
            weightRow.style.height = 20f;
            weightRow.style.flexDirection = FlexDirection.Row;
            weightRow.style.alignItems = Align.Center;
            weightRow.style.justifyContent = Justify.SpaceBetween;
            // [144차 P2-COOK C38/C39] 64:332 문구 "재료 적재 중량" — 값은 무게 데이터 부재 → "—" 슬롯 유지(D7).
            var weightTitle = new Label("재료 적재 중량") { name = "cooking-storage-weight-title" };
            weightTitle.style.fontSize = 15.6f;   // [C38] 12→15.6
            weightTitle.style.color = new StyleColor(UTKColor.TextSecondary);
            weightRow.Add(weightTitle);
            var weightValue = new Label("—") { name = "cooking-storage-weight-value" };
            weightValue.style.fontSize = 15.6f;   // [C38] 12→15.6
            weightValue.style.unityFont = GetGeistMonoFont();             // GeistMono700(UIFont 폴백)
            weightValue.style.unityFontStyleAndWeight = FontStyle.Bold;
            weightValue.style.color = new StyleColor(UTKTheme.Accent);    // [C38] #58A6FF
            weightRow.Add(weightValue);
            footer.Add(weightRow);

            var track = new VisualElement { name = "cooking-storage-progress-track" };
            track.style.position = Position.Absolute;
            track.style.left = 0f;
            track.style.top = 48f;   // [144차 P2-COOK C38] 34.4→48
            track.style.width = InnerWidth;
            track.style.height = 7.2f;
            track.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.078f));   // [144차 P2-COOK C38] #FFFFFF@0.078
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = 3.6f;
            track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 3.6f;
            _storageCapacityFill = new VisualElement { name = "cooking-storage-progress-fill" };
            _storageCapacityFill.style.height = 7.2f;
            _storageCapacityFill.style.width = 0f;
            _storageCapacityFill.style.backgroundColor = new StyleColor(UTKTheme.Accent);   // [144차 P2-COOK C38] #E3B341→#58A6FF
            _storageCapacityFill.style.borderTopLeftRadius = _storageCapacityFill.style.borderTopRightRadius = 3.6f;
            _storageCapacityFill.style.borderBottomLeftRadius = _storageCapacityFill.style.borderBottomRightRadius = 3.6f;
            track.Add(_storageCapacityFill);
            footer.Add(track);
            _storagePanel.Add(footer);
        }

        // =====================================================================
        //  UI 빌드 — Figma crafting-panel 요리 변형
        // =====================================================================

        /// <summary>상단: 재료 3슬롯 + → + 결과 슬롯.</summary>
        private void BuildSlotRow()
        {
            var section = new VisualElement { name = "cooking-combination-section" };
            section.style.position = Position.Absolute;
            section.style.left = CombinationLocalBounds.x;
            section.style.top = CombinationLocalBounds.y;
            section.style.width = CombinationLocalBounds.width;
            section.style.height = CombinationLocalBounds.height;
            // [144차 P2-COOK C8] 64:32 bg #FFFFFF@0.012 + 크롬 보더 1.2 + r9.6(PanelSub 단색/1px Stroke 폐기).
            section.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.012f));
            UTKTheme.ApplyElementBorder(section);
            UTKTheme.ApplyRadius(section, 9.6f);
            _craftPanel.Add(section);
            var sectionTitle = new Label("요리 레시피 슬롯");
            sectionTitle.style.position = Position.Absolute;
            sectionTitle.style.left = 19.2f;
            sectionTitle.style.top = 19.2f;
            sectionTitle.style.fontSize = UTKTheme.FontTab;                    // [Figma] 14.4/700 #58A6FF
            sectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionTitle.style.color = new StyleColor(UTKTheme.Accent);   // [144차 P2-COOK C9] AccentRare #E3B341→#58A6FF
            section.Add(sectionTitle);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var holder = new VisualElement { name = "cooking-input-holder-" + idx };
                holder.style.position = Position.Absolute;
                holder.style.left = 19.2f + i * 84f;
                holder.style.top = 57.8f;
                holder.style.width = SlotSize;
                holder.style.height = SlotSize;
                holder.style.alignItems = Align.Center;
                section.Add(holder);

                var cell = new UTKSlot { name = "cooking-input-slot-" + idx };
                cell.style.width = SlotSize;
                cell.style.height = SlotSize;
                holder.Add(cell);
                _slotCells[i] = cell;

                var lbl = new Label("—") { name = "cooking-input-count-" + idx };
                lbl.style.position = Position.Absolute;
                lbl.style.left = 0f;
                lbl.style.top = 55f;   // [144차 P2-COOK C11] 64:41 Count top 59.8→55
                lbl.style.width = SlotSize;
                lbl.style.height = 17f;
                lbl.style.fontSize = 13.2f;   // [144차 P2-COOK C11] 12→13.2(GeistMono600 폴백)
                lbl.style.unityFont = GetGeistMonoFont();
                lbl.style.color = new StyleColor(UTKColor.TextSecondary);
                lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                holder.Add(lbl);
                _slotLabels[i] = lbl;

                cell.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 1) return;
                    _slots[idx] = null;
                    RefreshAll();
                });
            }

            var arrow = new Label("→") { name = "cooking-flow-arrow" };
            arrow.style.position = Position.Absolute;
            arrow.style.left = 278.4f;
            arrow.style.top = 86f;   // [144차 P2-COOK C12] 64:54 FlowArrow y 70→86
            arrow.style.fontSize = 21.6f;
            arrow.style.color = new StyleColor(UTKTheme.Accent);   // [144차 P2-COOK C12] #E3B341→#58A6FF
            section.Add(arrow);

            var resultHolder = new VisualElement { name = "cooking-result-holder" };
            resultHolder.style.position = Position.Absolute;
            resultHolder.style.left = 321.6f;
            resultHolder.style.top = 50.6f;
            resultHolder.style.width = 91.2f;
            resultHolder.style.height = 91.2f;
            resultHolder.style.alignItems = Align.Center;
            section.Add(resultHolder);
            _resultCell = new UTKSlot { name = "cooking-result-slot" };
            _resultCell.style.width = 91.2f;
            _resultCell.style.height = 91.2f;
            UTKTheme.ApplyElementBorder(_resultCell, UTKTheme.Accent, 1.8f);   // [144차 P2-COOK C13] 64:58 #58A6FF 1.8(UTKSlot 기본 2px #2E343D 교체)
            resultHolder.Add(_resultCell);
            _resultNameLabel = new Label("—");
            _resultNameLabel.style.position = Position.Absolute;
            _resultNameLabel.style.left = 91.2f;
            _resultNameLabel.style.top = 33f;
            _resultNameLabel.style.width = 96f;
            _resultNameLabel.style.fontSize = 12f;
            _resultNameLabel.style.color = new StyleColor(UTKColor.AccentRare);
            _resultNameLabel.style.whiteSpace = WhiteSpace.Normal;
            resultHolder.Add(_resultNameLabel);
        }

        /// <summary>[144차 P2-COOK C14] 성공률 행 (슬롯 아래) — 64:63 EstimatedRate 좌/우 분리.
        /// 좌 "조리 성공 확률" 14.4 #8B949E + 우측 값 GeistMono700 #56D364 우측정렬(기존 병합 단일 라벨 폐기).</summary>
        private void BuildRateLabel()
        {
            var rateRow = new VisualElement { name = "cooking-rate" };
            rateRow.style.position = Position.Absolute;
            rateRow.style.left = CombinationLocalBounds.x + 19.2f;
            rateRow.style.top = CombinationLocalBounds.y + 156.6f;   // 64:63 ay=396 → 섹션 로컬 156.6
            rateRow.style.width = 417.6f;
            rateRow.style.height = 19f;
            rateRow.style.flexDirection = FlexDirection.Row;
            rateRow.style.alignItems = Align.Center;
            rateRow.style.justifyContent = Justify.SpaceBetween;
            var rateTitle = new Label("조리 성공 확률") { name = "cooking-rate-title", pickingMode = PickingMode.Ignore };
            rateTitle.style.fontSize = UTKTheme.FontTab;   // 14.4/400
            rateTitle.style.color = new StyleColor(UTKColor.TextSecondary);   // #8B949E
            rateRow.Add(rateTitle);
            _rateValueLabel = new Label("0%") { name = "cooking-rate-value", pickingMode = PickingMode.Ignore };
            _rateValueLabel.style.fontSize = UTKTheme.FontTab;
            _rateValueLabel.style.unityFont = GetGeistMonoFont();             // GeistMono700(UIFont 폴백)
            _rateValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _rateValueLabel.style.color = new StyleColor(CookValueGreen);     // [C14] #56D364
            rateRow.Add(_rateValueLabel);
            _craftPanel.Add(rateRow);
        }

        /// <summary>[144차 P2-COOK C5] 탭바 — 64:23 TabContainer (24,96,456,37.2) 상단(하단 레시피 섹션 내장 폐기).</summary>
        private void BuildTabBar()
        {
            _tabsHost = new VisualElement { name = "cooking-recipe-tabs" };
            _tabsHost.style.position = Position.Absolute;
            _tabsHost.style.left = TabsLocalBounds.x;
            _tabsHost.style.top = TabsLocalBounds.y;
            _tabsHost.style.width = TabsLocalBounds.width;
            _tabsHost.style.height = TabsLocalBounds.height;
            _tabsHost.style.flexDirection = FlexDirection.Row;
            _craftPanel.Add(_tabsHost);
            RefreshTabBar();
        }

        /// <summary>탭바 재구축 — 활성/비활성 폭·색 상태 반영(C6/C7).</summary>
        private void RefreshTabBar()
        {
            if (_tabsHost == null) return;
            _tabsHost.Clear();
            AddTab(_tabsHost, "전체", RecipeTab.All, false);
            AddTab(_tabsHost, "주식", RecipeTab.Crop, false);
            AddTab(_tabsHost, "부식", RecipeTab.Fish, false);
            AddTab(_tabsHost, "소스", RecipeTab.Monster, true);
        }

        /// <summary>Craft recipe list and center Detail panel ingredient selection.</summary>
        private void BuildMainSplit()
        {
            // Figma has a left recipe browser and a separate centered detail panel, not a nested split.

            // [144차 P2-COOK C15] 64:66 Divider — (24,366,456,1.2) #30363D@0.502 신설(조합 섹션과 레시피 섹션 사이).
            var sectionDivider = new VisualElement { name = "cooking-section-divider", pickingMode = PickingMode.Ignore };
            sectionDivider.style.position = Position.Absolute;
            sectionDivider.style.left = 24f;
            sectionDivider.style.top = 366f;
            sectionDivider.style.width = 456f;
            sectionDivider.style.height = 1.2f;
            sectionDivider.style.backgroundColor = new StyleColor(UTKTheme.BorderChrome);
            _craftPanel.Add(sectionDivider);

            // ── 좌: Figma recipe list region ──
            var left = new VisualElement { name = "cooking-recipe-list-section" };
            left.style.position = Position.Absolute;
            left.style.left = RecipeLocalBounds.x;
            left.style.top = RecipeLocalBounds.y;
            left.style.width = RecipeLocalBounds.width;
            left.style.height = RecipeLocalBounds.height;
            left.style.flexDirection = FlexDirection.Column;
            left.style.paddingRight = 0f;
            _craftPanel.Add(left);

            var listTitle = new Label("조리 가능한 레시피 목록");
            listTitle.style.position = Position.Absolute;
            listTitle.style.left = 0f;
            listTitle.style.top = 0f;
            listTitle.style.height = 17f;
            listTitle.style.fontSize = UTKTheme.FontTab;        // [144차 P2-COOK C17] 15.6→14.4
            listTitle.style.color = new StyleColor(UTKColor.TextSecondary);   // [C17] TextPrimary→#8B949E
            left.Add(listTitle);

            // 탭바는 [144차 P2-COOK C5] BuildTabBar로 상단 (24,96) 이동 — 하단 내장 폐기.
            var recipeScroll = new ScrollView(ScrollViewMode.Vertical) { name = "cooking-recipe-list" };
            recipeScroll.style.position = Position.Absolute;
            recipeScroll.style.left = 0f;
            recipeScroll.style.top = 27f;          // [144차 P2-COOK C18] 64:69 top → RecipeLocal.y+27 = 413.4
            recipeScroll.style.width = 456f;
            recipeScroll.style.height = 392.6f;    // [C18] 352.2→392.6
            recipeScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            left.Add(recipeScroll);
            _recipeListHost = recipeScroll.contentContainer;

            var detailName = new VisualElement { name = "cooking-detail-name-section" };
            detailName.style.position = Position.Absolute;
            detailName.style.left = DetailNameLocalBounds.x;
            detailName.style.top = DetailNameLocalBounds.y;
            detailName.style.width = DetailNameLocalBounds.width;
            detailName.style.height = DetailNameLocalBounds.height;
            detailName.style.flexDirection = FlexDirection.Row;
            detailName.style.alignItems = Align.Center;
            detailName.style.justifyContent = Justify.SpaceBetween;
            // [144차 P2-COOK C23] 64:126 ItemNameSection — bg #FFFFFF@0.02 + 크롬 보더 + r9.6 + pad 14.4.
            detailName.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
            UTKTheme.ApplyElementBorder(detailName);
            UTKTheme.ApplyRadius(detailName, 9.6f);
            detailName.style.paddingLeft = detailName.style.paddingRight = 14.4f;
            detailName.style.paddingTop = detailName.style.paddingBottom = 14.4f;
            _detailPanel.Add(detailName);
            var nameGroup = new VisualElement { name = "cooking-detail-name-group" };
            nameGroup.style.flexDirection = FlexDirection.Column;
            nameGroup.style.flexGrow = 1f;
            _detailLabel = new Label("레시피를 선택하세요.") { name = "cooking-selected-detail" };
            _detailLabel.style.fontSize = UTKTheme.FontTitle;   // [Figma] 21.6/700
            _detailLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            _detailLabel.style.whiteSpace = WhiteSpace.Normal;
            nameGroup.Add(_detailLabel);
            // [144차 P2-COOK C25] 64:129 영문 아이템명 라벨 — ItemData에 영문명 필드 부재 → "—" 슬롯 표기(위조 금지 D7).
            _detailEnNameLabel = new Label("—") { name = "cooking-detail-en-name", pickingMode = PickingMode.Ignore };
            _detailEnNameLabel.style.fontSize = UTKTheme.FontTab;   // 14.4/400
            _detailEnNameLabel.style.marginTop = 4.8f;              // 64:127 ItemNameGroup gap 4.8
            _detailEnNameLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            nameGroup.Add(_detailEnNameLabel);
            detailName.Add(nameGroup);
            // [144차 P2-COOK C26] 64:130 TierBadge — UTKTheme.CreateTierBadge 사용(플레인 텍스트 폐기).
            // 배지는 선택된 레시피 등급에 맞춰 RefreshRateAndDetail에서 재구축한다.
            _detailTierBadgeHost = new VisualElement { name = "cooking-detail-tier-badge-host" };
            detailName.Add(_detailTierBadgeHost);

            var image = new VisualElement { name = "cooking-detail-image-section" };
            image.style.position = Position.Absolute;
            image.style.left = DetailImageLocalBounds.x;
            image.style.top = DetailImageLocalBounds.y;
            image.style.width = DetailImageLocalBounds.width;
            image.style.height = DetailImageLocalBounds.height;
            // [144차 P2-COOK C27] 64:132 ItemImageSection — bg #FFFFFF@0.012 + 크롬 보더 + r9.6(PanelSub 폐기).
            image.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.012f));
            UTKTheme.ApplyElementBorder(image);
            UTKTheme.ApplyRadius(image, 9.6f);
            image.style.alignItems = Align.Center;
            image.style.justifyContent = Justify.Center;
            _detailPanel.Add(image);
            BuildBlueprintVisual(image);   // [144차 P2-COOK C28] 64:133-157 메시 그리드 + 타원 2(아이콘 하층)
            _detailItemIcon = new VisualElement { name = "cooking-detail-item-icon" };
            _detailItemIcon.style.width = 240f;    // [144차 P2-COOK C27] RenderGraphic 240×240(기존 160 교체)
            _detailItemIcon.style.height = 240f;
            image.Add(_detailItemIcon);

            var description = new VisualElement { name = "cooking-detail-description-section" };
            description.style.position = Position.Absolute;
            description.style.left = DetailDescriptionLocalBounds.x;
            description.style.top = DetailDescriptionLocalBounds.y;
            description.style.width = DetailDescriptionLocalBounds.width;
            description.style.height = DetailDescriptionLocalBounds.height;
            description.style.paddingLeft = description.style.paddingRight = 19.2f;
            description.style.paddingTop = 19.2f;
            // [144차 P2-COOK C29] 64:159 DescriptionSection 카드 — bg #FFFFFF@0.02 + 크롬 보더 + r9.6(pad 19.2 유지).
            description.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
            description.style.paddingBottom = 19.2f;
            UTKTheme.ApplyElementBorder(description);
            UTKTheme.ApplyRadius(description, 9.6f);
            _detailPanel.Add(description);
            var detailTitle = new Label("요리 사양 및 조리 정보");
            detailTitle.style.fontSize = UTKTheme.FontRowLabel;   // [Figma] 15.6/700 #58A6FF
            detailTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            detailTitle.style.color = new StyleColor(UTKTheme.Accent);   // [144차 P2-COOK C30] #E3B341→#58A6FF
            description.Add(detailTitle);
            // [144차 P2-COOK C31] 64:161-170 StatsGrid — 라벨:값 행 구조(라벨 15.6/400 #8B949E 좌측 +
            // 값 GeistMono700 우측정렬 #FF7B72/#56D364/#BC8CFF). 데이터 판정(2026-10-31): 요리 스탯 필드 부재 →
            // 위조 금지(D7): 값 "—" 슬롯 표기(실데이터 확보 시 값 라벨 갱신점만 연결).
            var statsGrid = new VisualElement { name = "cooking-detail-stats-grid" };
            statsGrid.style.marginTop = 14.8f;   // 64:161 desc-로컬 y52(헤딩 하단 기준)
            statsGrid.style.flexDirection = FlexDirection.Column;
            string[] statLabels = { "요리 분류", "레시피 재료", "효과" };
            Color[] statValueColors = { CookStatRed, CookValueGreen, TierColorFor(3) };
            Label[] statValues =
            {
                _detailStats = new Label("—"),
                _detailIngredients = new Label("—") { name = "cooking-detail-ingredients" },
                _detailEffectsLabel = new Label("—") { name = "cooking-detail-effect-label" },
            };
            string[] statValueNames = { "cooking-detail-stats", "cooking-detail-ingredients", "cooking-detail-effect-label" };
            for (int i = 0; i < 3; i++)
            {
                var statRow = new VisualElement { name = "cooking-detail-stat-row-" + i };
                statRow.style.height = 20f;
                statRow.style.marginTop = i == 0 ? 0f : 9.6f;   // StatsGrid gap 9.6(64:161)
                statRow.style.flexDirection = FlexDirection.Row;
                statRow.style.alignItems = Align.Center;
                statRow.style.justifyContent = Justify.SpaceBetween;
                var statLabel = new Label(statLabels[i]) { pickingMode = PickingMode.Ignore };
                statLabel.style.fontSize = UTKTheme.FontRowLabel;   // 15.6/400
                statLabel.style.color = new StyleColor(UTKColor.TextSecondary);
                statRow.Add(statLabel);
                var statValue = statValues[i];
                statValue.name = statValueNames[i];
                statValue.style.fontSize = UTKTheme.FontRowLabel;
                statValue.style.unityFont = GetGeistMonoFont();   // GeistMono700(UIFont 폴백)
                statValue.style.unityFontStyleAndWeight = FontStyle.Bold;
                statValue.style.color = new StyleColor(statValueColors[i]);
                statValue.style.whiteSpace = WhiteSpace.NoWrap;
                statRow.Add(statValue);
                statsGrid.Add(statRow);
            }
            description.Add(statsGrid);
            var descriptionDivider = new VisualElement { name = "cooking-detail-divider" };
            descriptionDivider.style.height = 1.2f;
            descriptionDivider.style.marginTop = 12f;
            descriptionDivider.style.backgroundColor = new StyleColor(UTKTheme.BorderChrome);   // [144차 P2-COOK C32] Stroke→#30363D@0.502
            description.Add(descriptionDivider);
            _detailDescriptionLabel = new Label("") { name = "cooking-detail-description" };
            _detailDescriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            _detailDescriptionLabel.style.fontSize = 15.6f;   // [144차 P2-COOK C33] 13.2→15.6(lineH 23.4 설정 금지 — IStyle.lineHeight CS1061 함정)
            _detailDescriptionLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _detailDescriptionLabel.style.marginTop = 12f;
            description.Add(_detailDescriptionLabel);

            _ingGridHost = null; // Figma detail frame owns specs/description, not a second ingredient inventory.
        }

        /// <summary>[144차 P2-COOK C6/C7] 탭 셀 — 64:24-31 활성(fill #58A6FF + 텍스트 #0B0E14, 폭 108.6) /
        /// 비활성(투명 + 크롬 스트로크 + 텍스트 #8B949E, 폭 111) · r7.2 · 15.6/700. UTKButton Secondary(#21262D r3) 폐기.</summary>
        private void AddTab(VisualElement parent, string label, RecipeTab tab, bool isLast)
        {
            bool active = _tab == tab;
            var tabEl = new VisualElement { name = "cooking-recipe-tab-" + tab.ToString().ToLowerInvariant() };
            tabEl.style.width = active ? 108.6f : 111f;   // [C6] 활성 108.6 유지 / 비활성 108.6→111
            tabEl.style.height = 37.2f;
            tabEl.style.alignItems = Align.Center;
            tabEl.style.justifyContent = Justify.Center;
            UTKTheme.ApplyRadius(tabEl, 7.2f);            // [C7] r7.2
            if (active)
            {
                tabEl.style.backgroundColor = new StyleColor(UTKTheme.Accent);          // #58A6FF
            }
            else
            {
                tabEl.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f)); // 투명
                UTKTheme.ApplyElementBorder(tabEl);                                      // 크롬 스트로크
            }
            var tabLabel = new Label(label) { pickingMode = PickingMode.Ignore };
            tabLabel.style.fontSize = 15.6f;              // [Figma 64:25 TabLabel] 15.6/700
            tabLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            tabLabel.style.color = new StyleColor(active ? UTKTheme.BgBase : UTKColor.TextSecondary);   // [C7] #0B0E14 / #8B949E
            tabEl.Add(tabLabel);
            tabEl.style.marginRight = isLast ? 0f : 4.8f;   // 64:23 TabContainer gap 4.8
            var captured = tab;
            tabEl.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                if (_tab == captured) return;
                _tab = captured;
                RefreshTabBar();
                RefreshRecipeList();
            });
            parent.Add(tabEl);
        }

        /// <summary>하단: COOK 버튼 + 결과 메시지.</summary>
        private void BuildFooter()
        {
            // [144차 P2-COOK C20] 64:103 CraftFooter (24,825,456,63.2) + 64:104 버튼 456×48.8 @y14.4
            // (기존 "버튼이 footer를 통째로 차지" 구조 교체).
            var footer = new VisualElement { name = "cooking-craft-footer" };
            footer.style.position = Position.Absolute;
            footer.style.left = FooterLocalBounds.x;
            footer.style.top = FooterLocalBounds.y;
            footer.style.width = FooterLocalBounds.width;
            footer.style.height = FooterLocalBounds.height;
            UTKTheme.ApplyElementBorder(footer);   // 64:103 stroke #30363D@0.502 1.2
            footer.style.paddingTop = 14.4f;
            _craftPanel.Add(footer);

            var cookBtn = UTKButton.Create("요리 제작하기 (COOK)", OnCookClicked, UTKButton.Variant.Primary);   // [C20] D7 문구 정합 64:105
            cookBtn.style.width = FooterLocalBounds.width;   // 456 풀폭
            cookBtn.style.height = 48.8f;
            cookBtn.style.backgroundColor = new StyleColor(UTKTheme.Accent);   // fill #58A6FF
            UTKTheme.ApplyRadius(cookBtn, 7.2f);
            cookBtn.style.fontSize = 16.8f;                       // [C20] 64:105 16.8/700
            cookBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            cookBtn.style.color = new StyleColor(UTKTheme.BgBase);   // 텍스트 #0B0E14
            cookBtn.name = "cooking-cook-action";
            footer.Add(cookBtn);

            _messageLabel = new Label("") { name = "cooking-result-message" };
            _messageLabel.style.position = Position.Absolute;
            _messageLabel.style.left = FooterLocalBounds.x + 8f;
            _messageLabel.style.top = FooterLocalBounds.y - 20f;
            _messageLabel.style.width = FooterLocalBounds.width - 16f;
            _messageLabel.style.height = 18f;
            _messageLabel.style.fontSize = 12f;
            _messageLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _messageLabel.style.whiteSpace = WhiteSpace.Normal;
            _craftPanel.Add(_messageLabel);
        }

        // =====================================================================
        //  카테고리 해석 — ItemData 표시명 → RecipeCatalog.IngredientCategory
        // =====================================================================

        /// <summary>
        /// ItemData → 요리 재료 카테고리. 몬스터 재료(id 접두) → FishCategory(🐟 접두 허용) →
        /// CropCategory 순 조회. 미등록이면 None.
        /// </summary>
        private static RecipeCatalog.IngredientCategory ResolveCategory(PlayerInventory.ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.displayName)) return RecipeCatalog.None;
            if (!string.IsNullOrEmpty(item.id) && item.id.StartsWith("monster_meat_"))
            {
                var mc = RecipeCatalog.MonsterCategory(item.displayName);
                if (mc != RecipeCatalog.None) return mc;
            }
            var fc = RecipeCatalog.FishCategory(item.displayName);
            if (fc != RecipeCatalog.None) return fc;
            return RecipeCatalog.CropCategory(item.displayName);
        }

        /// <summary>현재 배치된 재료(카테고리 해석 성공) 목록.</summary>
        private List<RecipeCatalog.IngredientCategory> PlacedCategories()
        {
            var list = new List<RecipeCatalog.IngredientCategory>(3);
            for (int i = 0; i < 3; i++)
            {
                if (_slots[i] == null) continue;
                var c = ResolveCategory(_slots[i]);
                if (c != RecipeCatalog.None && !list.Contains(c)) list.Add(c);
            }
            return list;
        }

        /// <summary>배치된 카테고리로 FindRecipe 조회 (2개 → 2인자, 3개 → 3인자).</summary>
        private RecipeCatalog.RecipeDef? FindCurrentRecipe()
        {
            var cats = PlacedCategories();
            if (cats.Count == 2)
                return RecipeCatalog.FindRecipe(cats[0], cats[1]);
            if (cats.Count == 3)
                return RecipeCatalog.FindRecipe(cats[0], cats[1], cats[2]);
            return null;
        }

        // =====================================================================
        //  갱신 — 슬롯 라벨 / 결과 미리보기 / 성공률 / 레시피 목록 / 재료 그리드
        // =====================================================================

        private void RefreshAll()
        {
            RefreshSlotRow();
            RefreshRateAndDetail();
            RefreshRecipeList();
            RefreshIngredientGrid();
            RefreshStoragePanel();
        }

        private void RefreshSlotRow()
        {
            for (int i = 0; i < 3; i++)
            {
                var item = _slots[i];
                // [144차 P2-COOK C11] 64:41 Count 형식 "보유/요구" — 보유=인벤토리 실보유량, 요구=1(제작 시 슬롯당 1개 차감).
                _slotLabels[i].text = item != null
                    ? $"{(PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemCount(item.id) : 0)}/1"
                    : "—";
                if (item != null)
                {
                    _slotCells[i].SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                    _slotCells[i].SetRank(UTKRarity.ClassForIndex((int)item.rarity));
                }
                else
                {
                    _slotCells[i].SetIcon(null);
                }
            }
        }

        private void RefreshRateAndDetail()
        {
            var recipe = FindCurrentRecipe();
            _selectedRecipe = recipe;
            if (_selectedRecipe.HasValue)
            {
                var selected = _selectedRecipe.Value;
                _selectedDetailItem = BuildDishItem(selected);
                _rateValueLabel.text = "100%";                                        // [C14] 우측 값만 갱신
                _rateValueLabel.style.color = new StyleColor(CookValueGreen);         // #56D364
                _resultCell.SetIcon(ItemIconDatabase.GetOrCreateIcon(_selectedDetailItem));
                _resultCell.SetCount(1);
                _resultNameLabel.text = selected.name;
                _detailLabel.text = selected.name;
                // [144차 P2-COOK C26] TierBadge — 선택 dish 등급 실데이터(DishDatabase)로 재구축.
                _detailTierBadgeHost.Clear();
                _detailTierBadgeHost.Add(UTKTheme.CreateTierBadge(
                    _selectedDetailItem.rarity.ToString().ToUpperInvariant(),
                    TierColorFor((int)_selectedDetailItem.rarity),
                    "cooking-detail-tier-badge"));
                var icon = ItemIconDatabase.GetOrCreateIcon(_selectedDetailItem);
                _detailItemIcon.style.backgroundImage = icon != null ? new StyleBackground(icon) : StyleKeyword.None;
                _detailStats.text = CategoryKo(selected.cats);
                _detailIngredients.text = CategoryKo(selected.cats);
                _detailEffectsLabel.text = string.IsNullOrEmpty(_selectedDetailItem.effects) ? "—" : _selectedDetailItem.effects;
                _detailDescriptionLabel.text = _selectedDetailItem.description ?? "";
                SetMessage("", UTKColor.TextSecondary);
            }
            else
            {
                _selectedDetailItem = null;
                int placed = PlacedCategories().Count;
                if (placed >= 2)
                {
                    _rateValueLabel.text = "0%";
                    _rateValueLabel.style.color = new StyleColor(UTKColor.HealthRed);
                    SetMessage("해당 조합의 요리가 없습니다.", UTKColor.TextSecondary);
                }
                else
                {
                    _rateValueLabel.text = "0%";
                    _rateValueLabel.style.color = new StyleColor(UTKColor.TextSecondary);
                    SetMessage("재료 2~3개를 배치하세요.", UTKColor.TextSecondary);
                }
                _resultCell.SetIcon(null);
                _resultNameLabel.text = "—";
                _detailLabel.text = "레시피를 선택하세요.";
                _detailItemIcon.style.backgroundImage = StyleKeyword.None;
                _detailTierBadgeHost.Clear();   // [C26] 미선택 — 배지 숨김
                _detailStats.text = "—";
                _detailIngredients.text = "—";
                _detailEffectsLabel.text = "—";
                _detailDescriptionLabel.text = "";
            }
        }

        private static PlayerInventory.ItemData BuildDishItem(RecipeCatalog.RecipeDef recipe)
        {
            string dishName = recipe.name ?? "";
            var authoredDish = ProjectName.Core.Data.DishDatabase.GetDishInfoByName(dishName);
            if (authoredDish != null) return authoredDish.ToItemData();
            return new PlayerInventory.ItemData
            {
                id = recipe.id ?? "",
                displayName = dishName,
                description = CategoryKo(recipe.cats),
                category = PlayerInventory.ItemCategory.Food,
                rarity = ItemRarity.Common,
                maxStack = 99,
                effects = ""
            };
        }

        private static string CategoryKo(RecipeCatalog.IngredientCategory[] cats)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cats.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(CategoryKo(cats[i]));
            }
            return sb.ToString();
        }

        private static string CategoryKo(RecipeCatalog.IngredientCategory cat)
        {
            foreach (var g in RecipeCatalog.MonsterGroups)
                if (g.category == cat) return g.categoryKo;
            switch (cat)
            {
                case RecipeCatalog.IngredientCategory.Fruit: return "과일류";
                case RecipeCatalog.IngredientCategory.Veg: return "채소류";
                case RecipeCatalog.IngredientCategory.RootVeg: return "뿌리채소류";
                case RecipeCatalog.IngredientCategory.LeafVeg: return "엽채류";
                case RecipeCatalog.IngredientCategory.Grain: return "곡류";
                case RecipeCatalog.IngredientCategory.Legume: return "콩류";
                case RecipeCatalog.IngredientCategory.Nut: return "견과류";
                case RecipeCatalog.IngredientCategory.Herb: return "허브";
                case RecipeCatalog.IngredientCategory.Spice: return "향신료";
                case RecipeCatalog.IngredientCategory.Drink: return "음료류";
                case RecipeCatalog.IngredientCategory.Salmon: return "연어·송어류";
                case RecipeCatalog.IngredientCategory.BlueFish: return "등푸른 해양어";
                case RecipeCatalog.IngredientCategory.FlatFish: return "넙치·가자미류";
                case RecipeCatalog.IngredientCategory.Catfish: return "메기류";
                case RecipeCatalog.IngredientCategory.Carp: return "잉어·붕어류";
                case RecipeCatalog.IngredientCategory.SmallFresh: return "소형 민물어";
                case RecipeCatalog.IngredientCategory.Eel: return "장어류";
                default: return cat.ToString();
            }
        }

        // =====================================================================
        //  레시피 목록 — 탭 필터 + 클릭 시 슬롯 자동 채움
        // =====================================================================

        private static bool InTab(RecipeCatalog.IngredientCategory c, RecipeTab tab)
        {
            const int CropFirst = (int)RecipeCatalog.IngredientCategory.Fruit;
            const int CropLast = (int)RecipeCatalog.IngredientCategory.Drink;
            const int FishFirst = (int)RecipeCatalog.IngredientCategory.Salmon;
            const int FishLast = (int)RecipeCatalog.IngredientCategory.Eel;
            const int MonFirst = (int)RecipeCatalog.IngredientCategory.RegularMeat;
            const int MonLast = (int)RecipeCatalog.IngredientCategory.Mystic;
            int v = (int)c;
            switch (tab)
            {
                case RecipeTab.Crop: return v >= CropFirst && v <= CropLast;
                case RecipeTab.Fish: return v >= FishFirst && v <= FishLast;
                case RecipeTab.Monster: return v >= MonFirst && v <= MonLast;
                default: return true;
            }
        }

        private void RefreshRecipeList()
        {
            _recipeListHost.Clear();

            var all = RecipeCatalog.All();
            int shown = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var r = all[i];
                if (r.cats == null || r.cats.Length == 0) continue;
                if (!InTab(r.cats[0], _tab)) continue;

                var def = r;
                _recipeListHost.Add(BuildRecipeRow(def, def.id == _selectedRecipeId));   // [144차 P2-COOK C19]
                shown++;
            }

            if (shown == 0)
            {
                var empty = new Label("(해당 탭 레시피 없음)");
                empty.style.fontSize = 13.2f;
                empty.style.color = new StyleColor(UTKColor.TextSecondary);
                _recipeListHost.Add(empty);
            }
        }

        /// <summary>[144차 P2-COOK C19] 64:70-101 RecipeRow — 456×72 행(pad 9.6, 아이콘 슬롯 52.8,
        /// 이름 16.8/700 + 부제 13.2, 우측 LV./SELECTED 배지 13.2 GeistMono600, 행간 7.2).
        /// 기존 UTKButton h26 텍스트 전용 행 폐기. 클릭 선택 동작 유지.
        /// 레벨 데이터 판정(2026-10-31): RecipeDef/DishDatabase에 요리 레벨 필드 부재 → 우측 "LV. —" 슬롯(위조 금지 D7),
        /// 부제는 실데이터(재료 카테고리명)로 표기. 티어 스트립은 DishDatabase 등급 실데이터 사용.
        /// 아이콘 텍스처는 행 2024건 GLB 베이크 비용 문제로 미장착(프레임+티어스트립만 — 보완 시 SetIcon 1줄 연결).</summary>
        private VisualElement BuildRecipeRow(RecipeCatalog.RecipeDef def, bool selected)
        {
            var row = new VisualElement { name = "cooking-recipe-row-" + def.id };
            row.style.height = 72f;
            row.style.marginBottom = 7.2f;   // 64:69 RecipesScroll gap 7.2
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = row.style.paddingRight = 9.6f;
            row.style.paddingTop = row.style.paddingBottom = 9.6f;
            UTKTheme.ApplyRadius(row, 7.2f);
            if (selected)
            {
                row.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.039f));   // 64:70 활성 fill
                UTKTheme.ApplyElementBorder(row, UTKTheme.Accent, 1.2f);                     // #58A6FF 1.2
            }
            else
            {
                UTKTheme.ApplyElementBorder(row);   // 64:81 stroke #30363D@0.502 1.2
            }

            var rowLeft = new VisualElement { name = "cooking-recipe-row-left" };
            rowLeft.style.flexDirection = FlexDirection.Row;
            rowLeft.style.alignItems = Align.Center;
            rowLeft.style.flexGrow = 1f;
            row.Add(rowLeft);

            var rowCell = new UTKSlot { name = "cooking-recipe-row-slot" };
            rowCell.style.width = 52.8f;   // 64:72 ItemSlot 52.8×52.8
            rowCell.style.height = 52.8f;
            rowCell.style.marginRight = 12f;   // 64:71 RecipeLeft gap 12
            var dish = BuildDishItem(def);   // 티어 실데이터(DishDatabase) — 위조 금지
            rowCell.SetTierStrip(true, TierColorFor((int)dish.rarity));
            rowLeft.Add(rowCell);

            var rowText = new VisualElement { name = "cooking-recipe-row-text" };
            rowText.style.flexDirection = FlexDirection.Column;
            var rowName = new Label(def.name) { pickingMode = PickingMode.Ignore };
            rowName.style.fontSize = 16.8f;   // 64:78 16.8/700
            rowName.style.unityFontStyleAndWeight = FontStyle.Bold;
            rowName.style.color = new StyleColor(UTKColor.TextPrimary);
            rowText.Add(rowName);
            var rowSub = new Label(CategoryKo(def.cats)) { pickingMode = PickingMode.Ignore };
            rowSub.style.fontSize = 13.2f;    // 64:79 13.2/400
            rowSub.style.marginTop = 2.4f;    // 64:77 RecipeText gap 2.4
            rowSub.style.color = new StyleColor(UTKColor.TextSecondary);
            rowText.Add(rowSub);
            rowLeft.Add(rowText);

            var rowBadge = new Label(selected ? "SELECTED" : "LV. —")
            {
                name = selected ? "cooking-recipe-row-selected" : "cooking-recipe-row-level",
                pickingMode = PickingMode.Ignore,
            };
            rowBadge.style.fontSize = 13.2f;   // 64:80 13.2 GeistMono600
            rowBadge.style.unityFont = GetGeistMonoFont();   // UIFont 폴백 허용
            rowBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            rowBadge.style.color = new StyleColor(selected ? UTKTheme.Accent : UTKColor.TextSecondary);
            row.Add(rowBadge);

            var captured = def;
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                SelectRecipe(captured);
            });
            return row;
        }

        /// <summary>[144차 P2-COOK C14/C19/C31] GeistMono 폰트 폴백 — LootWindowUTK/UTKSlot 선례
        /// (에셋 미보유 시 UIFont 폴백 — 폰트 패밀리 위조 금지 계약(C40)은 기록 보류).</summary>
        private static Font GetGeistMonoFont()
        {
            if (_geistMonoFont == null)
                _geistMonoFont = Resources.Load<Font>("Fonts/GeistMono");
            return _geistMonoFont;
        }

        /// <summary>[144차 P2-COOK C19/C26] 티어 의미색 — 진실치(crafting 64:36/42/48): Common #8B949E ·
        /// Rare #1F6FEB · Epic #BC8CFF · Top(Legendary) #D29922. Uncommon/Unique는 진실치 부재 →
        /// 기존 토큰 근사(Success/Epic — UI 토큰 선택, 데이터 위조 아님).</summary>
        private static Color TierColorFor(int rarityIndex)
        {
            switch (rarityIndex)
            {
                case 1: return UTKTheme.Success;                                       // Uncommon — 근사 토큰
                case 2: return new Color(0x1F / 255f, 0x6F / 255f, 0xEB / 255f, 1f);   // Rare #1F6FEB
                case 3: return new Color(0xBC / 255f, 0x8C / 255f, 0xFF / 255f, 1f);   // Epic #BC8CFF
                case 4: return new Color(0xD2 / 255f, 0x99 / 255f, 0x22 / 255f, 1f);   // Top(Legendary) #D29922
                case 5: return new Color(0xBC / 255f, 0x8C / 255f, 0xFF / 255f, 1f);   // Unique — Epic 토큰 근사
                default: return UTKColor.TextSecondary;                                // Common #8B949E
            }
        }

        /// <summary>[144차 P2-COOK C28] 64:133-157 BlueprintVisual — MeshGrid 가로 8본(y0..384 pitch≈55) +
        /// 세로 12본(x22 pitch44 · h120) + Ellipse 216/96(#58A6FF@0.102/0.122) + RenderGraphic 240×240 중앙.
        /// [144차 P2-WF W46] WeaponForgeUTK 구현 재사용. 라인/타원은 UTK 어휘(border/backgroundColor) 근사 — 벡터메시 금지 계약 준수.</summary>
        private static void BuildBlueprintVisual(VisualElement imageSection)
        {
            var blueprint = new VisualElement { name = "cooking-blueprint-visual", pickingMode = PickingMode.Ignore };
            blueprint.style.position = Position.Absolute;
            blueprint.style.left = 0f;
            blueprint.style.top = 0f;
            blueprint.style.width = 528f;
            blueprint.style.height = 384f;
            float[] horizontalY = { 0f, 55f, 110f, 164f, 219f, 274f, 329f, 382.8f };   // 64:135-142(말단 384→382.8 클램프)
            foreach (float y in horizontalY)
                blueprint.Add(BuildBlueprintBar(0f, y, 528f, 1.2f, "cooking-blueprint-line-h"));
            for (int i = 0; i < 12; i++)                                              // 64:144-155
                blueprint.Add(BuildBlueprintBar(22f + i * 44f, 0f, 1.2f, 120f, "cooking-blueprint-line-v"));
            blueprint.Add(BuildBlueprintEllipse(156f, 84f, 216f, 0.102f, "cooking-blueprint-ellipse-outer"));   // 64:156
            blueprint.Add(BuildBlueprintEllipse(216f, 144f, 96f, 0.122f, "cooking-blueprint-ellipse-inner"));   // 64:157
            imageSection.Add(blueprint);
        }

        /// <summary>BlueprintVisual 라인 근사 바 — #FFFFFF@0.02(UTK 어휘).</summary>
        private static VisualElement BuildBlueprintBar(float x, float y, float w, float h, string elementName)
        {
            var bar = new VisualElement { name = elementName, pickingMode = PickingMode.Ignore };
            bar.style.position = Position.Absolute;
            bar.style.left = x;
            bar.style.top = y;
            bar.style.width = w;
            bar.style.height = h;
            bar.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));   // 64:135-155
            return bar;
        }

        /// <summary>BlueprintVisual 타원 — 보더 라운드 원(UTK 어휘)으로 Ellipse 근사, #58A6FF@alpha 스트로크 1.2.</summary>
        private static VisualElement BuildBlueprintEllipse(float x, float y, float size, float alpha, string elementName)
        {
            var ellipse = new VisualElement { name = elementName, pickingMode = PickingMode.Ignore };
            ellipse.style.position = Position.Absolute;
            ellipse.style.left = x;
            ellipse.style.top = y;
            ellipse.style.width = size;
            ellipse.style.height = size;
            UTKTheme.ApplyRadius(ellipse, size * 0.5f);
            var stroke = new StyleColor(new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, alpha));
            ellipse.style.borderTopColor = stroke;
            ellipse.style.borderBottomColor = stroke;
            ellipse.style.borderLeftColor = stroke;
            ellipse.style.borderRightColor = stroke;
            ellipse.style.borderTopWidth = 1.2f;
            ellipse.style.borderBottomWidth = 1.2f;
            ellipse.style.borderLeftWidth = 1.2f;
            ellipse.style.borderRightWidth = 1.2f;
            return ellipse;
        }

        /// <summary>레시피 선택 — 보유 재료에서 각 카테고리 대표 아이템을 슬롯에 자동 채움.</summary>
        private void SelectRecipe(RecipeCatalog.RecipeDef def)
        {
            _selectedRecipeId = def.id;   // [144차 P2-COOK C19] 목록 SELECTED 배지 동기화
            Debug.Log($"[CookingUTK] 레시피 선택: {def.name} ({def.id})");
            for (int i = 0; i < 3; i++) _slots[i] = null;

            var inv = PlayerInventory.Instance;
            if (inv != null)
            {
                var slots = inv.GetAllSlots();
                int slotIdx = 0;
                for (int c = 0; c < def.cats.Length && slotIdx < 3; c++)
                {
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var s = slots[i];
                        if (s == null || s.item == null || s.count <= 0) continue;
                        if (ResolveCategory(s.item) != def.cats[c]) continue;
                        _slots[slotIdx++] = s.item;
                        break;
                    }
                }
            }

            RefreshAll();
            if (_selectedRecipe.HasValue)
                SetMessage($"'{_selectedRecipe.Value.name}' 재료 배치 완료.", UTKColor.GuildGreen);
            else
                SetMessage($"'{def.name}' 재료가 인벤토리에 부족합니다.", UTKColor.HealthRed);
        }

        // =====================================================================
        //  재료 인벤토리 그리드 — Meat / Food / 어류 Material
        // =====================================================================

        private static bool IsCookingIngredient(PlayerInventory.ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) return false;
            if (item.id.StartsWith("dish_")) return false;   // 완성 요리는 재료 아님
            var cat = item.category;
            if (cat == PlayerInventory.ItemCategory.Meat) return true;
            if (cat == PlayerInventory.ItemCategory.Food) return true;
            if (cat == PlayerInventory.ItemCategory.Herb) return true;
            if (cat == PlayerInventory.ItemCategory.Material
                && (item.id.StartsWith("fish_glb_") || ResolveCategory(item) != RecipeCatalog.None))
                return true;
            return false;
        }

        private void RefreshIngredientGrid()
        {
            if (_ingGridHost == null) return;
            _ingGridHost.Clear();

            var inv = PlayerInventory.Instance;
            var items = new List<PlayerInventory.ItemSlot>();
            if (inv != null)
            {
                var all = inv.GetAllSlots();
                if (all != null)
                    for (int i = 0; i < all.Length; i++)
                    {
                        var s = all[i];
                        if (s == null || s.item == null || s.count <= 0) continue;
                        if (!IsCookingIngredient(s.item)) continue;
                        items.Add(s);
                    }
            }

            if (items.Count == 0)
            {
                var empty = new Label("(요리 재료 없음)");
                empty.style.fontSize = 13.2f;
                empty.style.color = new StyleColor(UTKColor.TextSecondary);
                _ingGridHost.Add(empty);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var invSlot = items[i];
                var cell = new UTKSlot();
                cell.style.width = SlotSize;
                cell.style.height = SlotSize;
                cell.style.marginTop = 2f;
                cell.style.marginBottom = 2f;
                cell.style.marginLeft = 3f;
                cell.style.marginRight = 3f;
                cell.SetIcon(ItemIconDatabase.GetOrCreateIcon(invSlot.item));
                cell.SetCount(invSlot.count);
                cell.SetRank(UTKRarity.ClassForIndex((int)invSlot.item.rarity));
                cell.tooltip = invSlot.item.displayName;
                cell.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 1) return;
                    PlaceIngredient(invSlot.item);
                });
                _ingGridHost.Add(cell);
            }
        }

        /// <summary>재료를 첫 빈 슬롯에 배치 (중복 카테고리는 기존 슬롯 교체).</summary>
        private void PlaceIngredient(PlayerInventory.ItemData item)
        {
            var cat = ResolveCategory(item);
            if (cat == RecipeCatalog.None)
            {
                SetMessage($"'{item.displayName}'은(는) 요리 재료 카테고리가 아닙니다.", UTKColor.HealthRed);
                return;
            }

            // 이미 같은 카테고리가 배치되어 있으면 해당 슬롯 교체
            for (int i = 0; i < 3; i++)
            {
                if (_slots[i] != null && ResolveCategory(_slots[i]) == cat)
                {
                    _slots[i] = item;
                    RefreshAll();
                    return;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = item;
                    RefreshAll();
                    return;
                }
            }
            SetMessage("재료 슬롯이 가득 찼습니다. (슬롯 우클릭으로 해제)", UTKColor.HealthRed);
        }

        // =====================================================================
        //  요리 실행 — RecipeCatalog.FindRecipe 매칭 → 재료 차감 → 요리 지급 + EXP
        // =====================================================================

        private void OnCookClicked()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                SetMessage("인벤토리를 찾을 수 없습니다.", UTKColor.HealthRed);
                return;
            }

            var recipe = FindCurrentRecipe();
            if (!recipe.HasValue)
            {
                SetMessage("해당 조합의 요리가 없습니다.", UTKColor.TextSecondary);
                Debug.Log("[CookingUTK] 알 수 없는 카테고리 조합 — 요리 불가");
                return;
            }
            var r = recipe.Value;

            // 배치된 재료 아이템 확정 (카테고리 매칭분)
            var usedItems = new List<PlayerInventory.ItemData>();
            var cats = PlacedCategories();
            for (int i = 0; i < 3 && usedItems.Count < cats.Count; i++)
            {
                if (_slots[i] == null) continue;
                if (ResolveCategory(_slots[i]) == RecipeCatalog.None) continue;
                usedItems.Add(_slots[i]);
            }

            // 인벤토리 보유 확인
            foreach (var item in usedItems)
            {
                if (inv.GetItemCount(item.id) < 1)
                {
                    SetMessage($"재료 '{item.displayName}'이(가) 인벤토리에 없습니다.", UTKColor.HealthRed);
                    return;
                }
            }

            // 재료 차감
            foreach (var item in usedItems)
                inv.RemoveItem(item.id, 1);

            PlayerInventory.ItemData dish = BuildDishItem(r);
            bool added = inv.AddItem(dish, 1);

            if (PlayerStats.Instance != null)
                PlayerStats.Instance.AddExp(10);

            // MarkDiscovered는 static — 인스턴스 확인 없이 호출
            RecipeDiscoverySystem.MarkDiscovered(r.name);

            SetMessage($"🟢 요리 성공! '{r.name}' 획득!{(added ? "" : " (인벤토리 부족 — 일부 미지급)")}", UTKColor.GuildGreen);
            // [129차 Q1] 요리 성공 — 애니 없이 환호만 발화
            var cheerPlayer = GameObject.FindGameObjectWithTag("Player");
            var cheerDriver = cheerPlayer != null ? (cheerPlayer.GetComponent<HumanoidClipDriver>() ?? cheerPlayer.GetComponentInChildren<HumanoidClipDriver>()) : null;
            cheerDriver?.TriggerCheer();
            Debug.Log($"[CookingUTK] 요리 성공: {string.Join(" + ", cats.ToArray())} → {r.name} ({r.id})");

            for (int i = 0; i < 3; i++) _slots[i] = null;
            RefreshAll();
        }

        private void SetMessage(string message, Color color)
        {
            _messageLabel.text = message;
            _messageLabel.style.color = new StyleColor(color);
        }

        // =====================================================================
        //  생명주기 — UTKWindowBase 훅
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
            ApplyFigmaBounds();
            for (int i = 0; i < 3; i++) _slots[i] = null;
            SetMessage("", UTKColor.TextSecondary);
            RefreshAll();
            StartRefreshLoop();
            Debug.Log("[CookingUTK] 요리 창 열림");
        }

        private void OnCanvasRootGeometryChanged(GeometryChangedEvent evt) => ApplyFigmaBounds();

        private void ApplyFigmaBounds()
        {
            if (_canvasLayoutRoot == null) return;
            // [Figma 정합 v3] 등배수 디자인공간 — 창 박스=FigmaBounds×k, 패널은 CreatePanel의 raw 오프셋 그대로
            // (_content가 designSpace: raw 1632×912 + scale=k). 패널 박스 재스케일 경로는 제거됨.
            FigmaCanvasLayout.ApplyDesignSpace(this, _content, FigmaBounds, _canvasLayoutRoot);
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[CookingUTK] 요리 창 닫힘");
        }

        // =====================================================================
        //  폴링 갱신
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) { RefreshSlotRow(); RefreshIngredientGrid(); RefreshStoragePanel(); RefreshRateAndDetail(); }
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
    }
}
