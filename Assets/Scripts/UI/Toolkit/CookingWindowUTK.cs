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
        private static readonly Rect FooterLocalBounds = new Rect(24f, 824.8f, 456f, 63.2f);
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
        private Label _rateLabel;
        private Label _detailLabel;
        private Label _detailStats;
        private PlayerInventory.ItemData _selectedDetailItem;
        private VisualElement _recipeListHost;
        private VisualElement _ingGridHost;
        private VisualElement _craftPanel;
        private VisualElement _detailPanel;
        private VisualElement _storagePanel;
        private VisualElement _detailItemIcon;
        private Label _detailRarityLabel;
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
            AddPanelHeader(_detailPanel, "상세 정보", "SPECIFICATIONS", HeaderLocalBounds, true);
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
                var emptyMark = new Label("◇") { name = "cooking-empty-slot-mark-" + i };
                emptyMark.pickingMode = PickingMode.Ignore;
                emptyMark.style.position = Position.Absolute;
                emptyMark.style.left = 21.6f;
                emptyMark.style.top = 21.6f;
                emptyMark.style.width = 38.4f;
                emptyMark.style.height = 38.4f;
                emptyMark.style.color = new StyleColor(UTKColor.TextSecondary);
                emptyMark.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyMark.style.fontSize = 20f;
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
            _storagePanel.Q<Label>("cooking-storage-weight-value").text = $"사용 슬롯 기반 {occupiedSlots} / {capacity}";
            _storageCapacityFill.style.width = capacity > 0
                ? Mathf.Min(InnerWidth, InnerWidth * occupiedSlots / (float)capacity)
                : 0f;
            for (int i = 0; i < StoragePlaceholderCount; i++)
            {
                VisualElement cell = _storageCells[i];
                bool hasItem = i < items.Count;
                var emptyMark = cell.Q<Label>("cooking-empty-slot-mark-" + i);
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
            var sectionLabel = new Label("보유 재료 리스트") { name = "cooking-storage-section-label" };
            sectionLabel.style.position = Position.Absolute;
            sectionLabel.style.left = StorageStatsLocalBounds.x;
            sectionLabel.style.top = StorageStatsLocalBounds.y;
            sectionLabel.style.fontSize = 12f;
            sectionLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _storagePanel.Add(sectionLabel);
            _storageCapacityLabel = new Label("사용 슬롯: 0 / 25") { name = "cooking-storage-capacity" };
            _storageCapacityLabel.style.position = Position.Absolute;
            _storageCapacityLabel.style.right = InnerLeft;
            _storageCapacityLabel.style.top = StorageStatsLocalBounds.y;
            _storageCapacityLabel.style.width = 140f;
            _storageCapacityLabel.style.height = StorageStatsLocalBounds.height;
            _storageCapacityLabel.style.fontSize = 12f;
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
            weightRow.style.top = 0f;
            weightRow.style.width = InnerWidth;
            weightRow.style.height = 20f;
            weightRow.style.flexDirection = FlexDirection.Row;
            weightRow.style.alignItems = Align.Center;
            weightRow.style.justifyContent = Justify.SpaceBetween;
            var weightTitle = new Label("사용 슬롯") { name = "cooking-storage-weight-title" };
            weightTitle.style.fontSize = 12f;
            weightTitle.style.color = new StyleColor(UTKColor.TextSecondary);
            weightRow.Add(weightTitle);
            var weightValue = new Label("0 / 40") { name = "cooking-storage-weight-value" };
            weightValue.style.fontSize = 12f;
            weightValue.style.color = new StyleColor(UTKColor.TextSecondary);
            weightRow.Add(weightValue);
            footer.Add(weightRow);

            var track = new VisualElement { name = "cooking-storage-progress-track" };
            track.style.position = Position.Absolute;
            track.style.left = 0f;
            track.style.top = 34.4f;
            track.style.width = InnerWidth;
            track.style.height = 7.2f;
            track.style.backgroundColor = new StyleColor(UTKTheme.PanelSub);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = 3.6f;
            track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 3.6f;
            _storageCapacityFill = new VisualElement { name = "cooking-storage-progress-fill" };
            _storageCapacityFill.style.height = 7.2f;
            _storageCapacityFill.style.width = 0f;
            _storageCapacityFill.style.backgroundColor = new StyleColor(UTKColor.AccentRare);
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
            section.style.borderTopWidth = section.style.borderBottomWidth = 1f;
            section.style.borderLeftWidth = section.style.borderRightWidth = 1f;
            section.style.borderTopColor = section.style.borderBottomColor = UTKTheme.Stroke;
            section.style.borderLeftColor = section.style.borderRightColor = UTKTheme.Stroke;
            section.style.backgroundColor = new StyleColor(UTKTheme.PanelSub);
            _craftPanel.Add(section);
            var sectionTitle = new Label("요리 레시피 슬롯");
            sectionTitle.style.position = Position.Absolute;
            sectionTitle.style.left = 19.2f;
            sectionTitle.style.top = 19.2f;
            sectionTitle.style.fontSize = UTKTheme.FontTab;                    // [Figma] 14.4/700 #58A6FF
            sectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionTitle.style.color = new StyleColor(UTKColor.AccentRare);
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
                lbl.style.top = SlotSize - 17f;
                lbl.style.width = SlotSize;
                lbl.style.height = 17f;
                lbl.style.fontSize = 11f;
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
            arrow.style.top = 70f;
            arrow.style.fontSize = 22f;
            arrow.style.color = new StyleColor(UTKColor.AccentRare);
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
            resultHolder.Add(_resultCell);
            _resultNameLabel = new Label("—");
            _resultNameLabel.style.position = Position.Absolute;
            _resultNameLabel.style.left = 91.2f;
            _resultNameLabel.style.top = 33f;
            _resultNameLabel.style.width = 96f;
            _resultNameLabel.style.fontSize = 11f;
            _resultNameLabel.style.color = new StyleColor(UTKColor.AccentRare);
            _resultNameLabel.style.whiteSpace = WhiteSpace.Normal;
            resultHolder.Add(_resultNameLabel);
        }

        /// <summary>성공률 라벨 (슬롯 아래).</summary>
        private void BuildRateLabel()
        {
            _rateLabel = new Label("조리 성공 확률: 0%") { name = "cooking-rate" };
            _rateLabel.style.position = Position.Absolute;
            _rateLabel.style.left = CombinationLocalBounds.x + 19.2f;
            _rateLabel.style.top = CombinationLocalBounds.y + 156.2f;
            _rateLabel.style.width = 417.6f;
            _rateLabel.style.height = 19f;
            _rateLabel.style.fontSize = UTKTheme.FontTab;   // [Figma] 14.4/400
            _rateLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            _rateLabel.style.color = new StyleColor(UTKColor.GuildGreen);
            _craftPanel.Add(_rateLabel);
        }

        /// <summary>Craft recipe list and center Detail panel ingredient selection.</summary>
        private void BuildMainSplit()
        {
            // Figma has a left recipe browser and a separate centered detail panel, not a nested split.


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
            listTitle.style.fontSize = UTKTheme.FontRowLabel;   // [Figma] 15.6
            listTitle.style.color = new StyleColor(UTKColor.TextPrimary);
            left.Add(listTitle);

            var tabs = new VisualElement { name = "cooking-recipe-tabs" };
            tabs.style.position = Position.Absolute;
            tabs.style.left = 0f;
            tabs.style.top = 27f;
            tabs.style.width = 456f;
            tabs.style.height = 37.2f;
            tabs.style.flexDirection = FlexDirection.Row;
            left.Add(tabs);
            AddTab(tabs, "전체", RecipeTab.All);
            AddTab(tabs, "주식", RecipeTab.Crop);
            AddTab(tabs, "부식", RecipeTab.Fish);
            AddTab(tabs, "소스", RecipeTab.Monster);

            var recipeScroll = new ScrollView(ScrollViewMode.Vertical) { name = "cooking-recipe-list" };
            recipeScroll.style.position = Position.Absolute;
            recipeScroll.style.left = 0f;
            recipeScroll.style.top = 67f;
            recipeScroll.style.width = 456f;
            recipeScroll.style.height = 352.2f;
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
            _detailPanel.Add(detailName);
            _detailLabel = new Label("레시피를 선택하세요.") { name = "cooking-selected-detail" };
            _detailLabel.style.fontSize = UTKTheme.FontTitle;   // [Figma] 21.6/700
            _detailLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            _detailLabel.style.whiteSpace = WhiteSpace.Normal;
            detailName.Add(_detailLabel);
            _detailRarityLabel = new Label("") { name = "cooking-selected-rarity" };
            _detailRarityLabel.style.fontSize = UTKTheme.FontSubtitle;   // [Figma EPIC] 13.2/700 #BC8CFF
            _detailRarityLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailRarityLabel.style.color = new StyleColor(new Color32(0xBC, 0x8C, 0xFF, 0xFF));
            detailName.Add(_detailRarityLabel);

            var image = new VisualElement { name = "cooking-detail-image-section" };
            image.style.position = Position.Absolute;
            image.style.left = DetailImageLocalBounds.x;
            image.style.top = DetailImageLocalBounds.y;
            image.style.width = DetailImageLocalBounds.width;
            image.style.height = DetailImageLocalBounds.height;
            image.style.backgroundColor = new StyleColor(UTKTheme.PanelSub);
            image.style.alignItems = Align.Center;
            image.style.justifyContent = Justify.Center;
            _detailPanel.Add(image);
            _detailItemIcon = new VisualElement { name = "cooking-detail-item-icon" };
            _detailItemIcon.style.width = 160f;
            _detailItemIcon.style.height = 160f;
            image.Add(_detailItemIcon);

            var description = new VisualElement { name = "cooking-detail-description-section" };
            description.style.position = Position.Absolute;
            description.style.left = DetailDescriptionLocalBounds.x;
            description.style.top = DetailDescriptionLocalBounds.y;
            description.style.width = DetailDescriptionLocalBounds.width;
            description.style.height = DetailDescriptionLocalBounds.height;
            description.style.paddingLeft = description.style.paddingRight = 19.2f;
            description.style.paddingTop = 19.2f;
            _detailPanel.Add(description);
            var detailTitle = new Label("요리 사양 및 조리 정보");
            detailTitle.style.fontSize = UTKTheme.FontRowLabel;   // [Figma] 15.6/700 #58A6FF
            detailTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            detailTitle.style.color = new StyleColor(UTKColor.AccentRare);
            description.Add(detailTitle);
            _detailStats = new Label("효과                         —\n재료 분류                      —");
            _detailStats.name = "cooking-detail-stats";
            _detailStats.style.whiteSpace = WhiteSpace.Normal;
            _detailStats.style.marginTop = 12f;
            _detailStats.style.fontSize = UTKTheme.FontRowLabel;   // [Figma StatRow] 15.6/400
            _detailStats.style.unityFontStyleAndWeight = FontStyle.Normal;
            _detailStats.style.color = new StyleColor(UTKColor.TextSecondary);
            description.Add(_detailStats);
            _detailIngredients = new Label("레시피 재료                      —") { name = "cooking-detail-ingredients" };
            _detailIngredients.style.whiteSpace = WhiteSpace.Normal;
            _detailIngredients.style.marginTop = 10f;
            _detailIngredients.style.fontSize = UTKTheme.FontRowLabel;
            _detailIngredients.style.color = new StyleColor(UTKColor.TextSecondary);
            description.Add(_detailIngredients);
            _detailEffectsLabel = new Label("") { name = "cooking-detail-effect-label" };
            _detailEffectsLabel.style.fontSize = 13f;
            _detailEffectsLabel.style.color = new StyleColor(UTKColor.AccentRare);
            _detailEffectsLabel.style.marginTop = 10f;
            description.Add(_detailEffectsLabel);
            var descriptionDivider = new VisualElement { name = "cooking-detail-divider" };
            descriptionDivider.style.height = 1.2f;
            descriptionDivider.style.marginTop = 12f;
            descriptionDivider.style.backgroundColor = new StyleColor(UTKTheme.Stroke);
            description.Add(descriptionDivider);
            _detailDescriptionLabel = new Label("") { name = "cooking-detail-description" };
            _detailDescriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            _detailDescriptionLabel.style.fontSize = 13f;
            _detailDescriptionLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _detailDescriptionLabel.style.marginTop = 12f;
            description.Add(_detailDescriptionLabel);

            _ingGridHost = null; // Figma detail frame owns specs/description, not a second ingredient inventory.
        }

        private void AddTab(VisualElement parent, string label, RecipeTab tab)
        {
            var btn = UTKButton.Create(label, () =>
            {
                _tab = tab;
                RefreshRecipeList();
            }, UTKButton.Variant.Secondary);
            btn.style.height = 37.2f;
            btn.style.width = 108.6f;
            btn.style.fontSize = 15.6f;                        // [Figma 64:2 TabLabel] 15.6/700
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.flexGrow = 0f;
            btn.style.flexShrink = 0f;
            btn.style.marginLeft = 0f;
            btn.style.marginRight = 6f;
            parent.Add(btn);
        }

        /// <summary>하단: COOK 버튼 + 결과 메시지.</summary>
        private void BuildFooter()
        {
            var cookBtn = UTKButton.Create("요리 제작하기   COOK", OnCookClicked, UTKButton.Variant.Primary);
            cookBtn.style.position = Position.Absolute;
            cookBtn.style.left = FooterLocalBounds.x;
            cookBtn.style.top = FooterLocalBounds.y;
            cookBtn.style.width = FooterLocalBounds.width;
            cookBtn.style.height = FooterLocalBounds.height;
            cookBtn.name = "cooking-cook-action";
            _craftPanel.Add(cookBtn);

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
                _slotLabels[i].text = item != null ? item.displayName : "[비어있음]";
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
                _rateLabel.text = "조리 성공 확률                                 100%";
                _rateLabel.style.color = new StyleColor(UTKColor.GuildGreen);
                _resultCell.SetIcon(ItemIconDatabase.GetOrCreateIcon(_selectedDetailItem));
                _resultCell.SetCount(1);
                _resultNameLabel.text = selected.name;
                _detailLabel.text = selected.name;
                var icon = ItemIconDatabase.GetOrCreateIcon(_selectedDetailItem);
                _detailItemIcon.style.backgroundImage = icon != null ? new StyleBackground(icon) : StyleKeyword.None;
                _detailRarityLabel.text = "";
                _detailStats.text = $"요리 분류                         {CategoryKo(selected.cats)}";
                _detailIngredients.text = $"레시피 재료                      {CategoryKo(selected.cats)}";
                _detailEffectsLabel.text = string.IsNullOrEmpty(_selectedDetailItem.effects) ? "" : $"효과   {_selectedDetailItem.effects}";
                _detailDescriptionLabel.text = _selectedDetailItem.description ?? "";
                SetMessage("", UTKColor.TextSecondary);
            }
            else
            {
                _selectedDetailItem = null;
                int placed = PlacedCategories().Count;
                if (placed >= 2)
                {
                    _rateLabel.text = "조리 성공 확률                                 0% (조합 없음)";
                    _rateLabel.style.color = new StyleColor(UTKColor.HealthRed);
                    SetMessage("해당 조합의 요리가 없습니다.", UTKColor.TextSecondary);
                }
                else
                {
                    _rateLabel.text = "조리 성공 확률                                 0%";
                    _rateLabel.style.color = new StyleColor(UTKColor.TextSecondary);
                    SetMessage("재료 2~3개를 배치하세요.", UTKColor.TextSecondary);
                }
                _resultCell.SetIcon(null);
                _resultNameLabel.text = "—";
                _detailLabel.text = "레시피를 선택하세요.";
                _detailItemIcon.style.backgroundImage = StyleKeyword.None;
                _detailRarityLabel.text = "";
                _detailStats.text = "요리 분류                         —";
                _detailIngredients.text = "레시피 재료                      —";
                _detailEffectsLabel.text = "";
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
                var btn = UTKButton.Create(def.name, () => SelectRecipe(def), UTKButton.Variant.Secondary);
                btn.style.height = 26f;
                btn.style.marginBottom = 2f;
                _recipeListHost.Add(btn);
                shown++;
            }

            if (shown == 0)
            {
                var empty = new Label("(해당 탭 레시피 없음)");
                empty.style.fontSize = 13f;
                empty.style.color = new StyleColor(UTKColor.TextSecondary);
                _recipeListHost.Add(empty);
            }
        }

        /// <summary>레시피 선택 — 보유 재료에서 각 카테고리 대표 아이템을 슬롯에 자동 채움.</summary>
        private void SelectRecipe(RecipeCatalog.RecipeDef def)
        {
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
                empty.style.fontSize = 13f;
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
            Vector2 rootSize = new Vector2(_canvasLayoutRoot.resolvedStyle.width, _canvasLayoutRoot.resolvedStyle.height);
            if (!FigmaCanvasLayout.Apply(this, FigmaBounds, _canvasLayoutRoot)) return;
            ApplyPanelScale(rootSize);
        }

        private void ApplyPanelScale(Vector2 rootSize)
        {
            float scaleX = rootSize.x / FigmaCanvasLayout.CanvasWidth;
            float scaleY = rootSize.y / FigmaCanvasLayout.CanvasHeight;
            ApplyPanelBounds(_craftPanel, CraftingPanelBounds, scaleX, scaleY);
            ApplyPanelBounds(_detailPanel, DetailPanelBounds, scaleX, scaleY);
            ApplyPanelBounds(_storagePanel, StoragePanelBounds, scaleX, scaleY);
        }

        private static void ApplyPanelBounds(VisualElement panel, Rect figmaBounds, float scaleX, float scaleY)
        {
            panel.style.left = (figmaBounds.x - FigmaBounds.x) * scaleX;
            panel.style.top = (figmaBounds.y - FigmaBounds.y) * scaleY;
            // Preserve authored local dimensions for all controls. The root transform scales the
            // complete fixed panel geometry, including padding, gaps, and child hit targets.
            panel.style.width = figmaBounds.width;
            panel.style.height = figmaBounds.height;
            panel.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            panel.style.scale = new StyleScale(new Scale(new Vector2(scaleX, scaleY)));
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
