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
        private const float WinW = 760f;
        private const float WinH = 620f;
        private const float SlotSize = 64f;
        private const long RefreshMs = 300L;

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
        private VisualElement _recipeListHost;
        private VisualElement _ingGridHost;
        private Label _messageLabel;
        private IVisualElementScheduledItem _refreshTask;

        private CookingWindowUTK() : base("🍳 요리 테이블", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            BuildSlotRow();
            BuildRateLabel();
            BuildMainSplit();
            BuildFooter();

            ApplyUIToolkitFont(this);

            RefreshAll();
            style.display = DisplayStyle.None;
            style.left = 200f;
            style.top = 80f;
            Debug.Log("[CookingUTK] 요리 창 생성됨 (3슬롯 카테고리 조합)");
        }

        // =====================================================================
        //  UI 빌드 — Figma crafting-panel 요리 변형
        // =====================================================================

        /// <summary>상단: 재료 3슬롯 + → + 결과 슬롯.</summary>
        private void BuildSlotRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;
            _content.Add(row);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var holder = new VisualElement();
                holder.style.flexDirection = FlexDirection.Column;
                holder.style.alignItems = Align.Center;
                holder.style.marginRight = 8f;
                row.Add(holder);

                var title = new Label("재료 " + (i + 1));
                title.style.fontSize = 12f;
                title.style.color = new StyleColor(UTKColor.TextSecondary);
                holder.Add(title);

                var cell = new UTKSlot();
                cell.style.width = SlotSize;
                cell.style.height = SlotSize;
                holder.Add(cell);
                _slotCells[i] = cell;

                var lbl = new Label("[비어있음]");
                lbl.style.width = SlotSize + 8f;
                lbl.style.fontSize = 11f;
                lbl.style.color = new StyleColor(UTKColor.TextSecondary);
                lbl.style.whiteSpace = WhiteSpace.Normal;
                lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                holder.Add(lbl);
                _slotLabels[i] = lbl;

                // 우클릭 → 재료 해제
                cell.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 1) return;
                    _slots[idx] = null;
                    RefreshAll();
                });
            }

            // 흐름 화살표 (FlowArrow)
            var arrow = new Label("→");
            arrow.style.fontSize = 26f;
            arrow.style.color = new StyleColor(UTKColor.AccentRare);
            arrow.style.marginLeft = 6f;
            arrow.style.marginRight = 6f;
            row.Add(arrow);

            // 결과 미리보기 슬롯
            var resultHolder = new VisualElement();
            resultHolder.style.flexDirection = FlexDirection.Column;
            resultHolder.style.alignItems = Align.Center;
            row.Add(resultHolder);

            var resultTitle = new Label("요리 결과");
            resultTitle.style.fontSize = 12f;
            resultTitle.style.color = new StyleColor(UTKColor.TextSecondary);
            resultHolder.Add(resultTitle);

            _resultCell = new UTKSlot();
            _resultCell.style.width = SlotSize;
            _resultCell.style.height = SlotSize;
            resultHolder.Add(_resultCell);

            _resultNameLabel = new Label("—");
            _resultNameLabel.style.fontSize = 11f;
            _resultNameLabel.style.color = new StyleColor(UTKColor.AccentRare);
            _resultNameLabel.style.whiteSpace = WhiteSpace.Normal;
            _resultNameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _resultNameLabel.style.width = 120f;
            resultHolder.Add(_resultNameLabel);
        }

        /// <summary>성공률 라벨 (슬롯 아래).</summary>
        private void BuildRateLabel()
        {
            _rateLabel = new Label("조리 성공 확률: 0%");
            _rateLabel.style.fontSize = 14f;
            _rateLabel.style.color = new StyleColor(UTKColor.GuildGreen);
            _rateLabel.style.marginTop = 2f;
            _rateLabel.style.marginBottom = 4f;
            _content.Add(_rateLabel);
        }

        /// <summary>중단: 좌측 레시피 목록(탭) / 우측 상세 + 재료 인벤토리 그리드.</summary>
        private void BuildMainSplit()
        {
            var split = new VisualElement();
            split.style.flexGrow = 1f;
            split.style.flexDirection = FlexDirection.Row;
            _content.Add(split);

            // ── 좌: 레시피 목록 ──
            var left = new VisualElement();
            left.style.width = 300f;
            left.style.flexShrink = 0;
            left.style.flexDirection = FlexDirection.Column;
            left.style.paddingRight = 8f;
            split.Add(left);

            var listTitle = new Label("요리 레시피");
            listTitle.style.fontSize = 15f;
            listTitle.style.color = new StyleColor(UTKColor.TextPrimary);
            left.Add(listTitle);

            // 카테고리 필터 탭
            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.marginBottom = 4f;
            left.Add(tabs);
            AddTab(tabs, "전체", RecipeTab.All);
            AddTab(tabs, "작물", RecipeTab.Crop);
            AddTab(tabs, "생선", RecipeTab.Fish);
            AddTab(tabs, "몬스터", RecipeTab.Monster);

            var recipeScroll = new ScrollView(ScrollViewMode.Vertical);
            recipeScroll.style.flexGrow = 1f;
            left.Add(recipeScroll);
            _recipeListHost = recipeScroll.contentContainer;

            // ── 우: 상세 + 재료 인벤토리 ──
            var right = new VisualElement();
            right.style.flexGrow = 1f;
            right.style.flexDirection = FlexDirection.Column;
            right.style.paddingLeft = 8f;
            split.Add(right);

            var detailTitle = new Label("요리 상세");
            detailTitle.style.fontSize = 15f;
            detailTitle.style.color = new StyleColor(UTKColor.TextPrimary);
            right.Add(detailTitle);

            _detailLabel = new Label("레시피를 선택하세요.");
            _detailLabel.style.fontSize = 13f;
            _detailLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _detailLabel.style.whiteSpace = WhiteSpace.Normal;
            right.Add(_detailLabel);

            var ingTitle = new Label("재료 (클릭 → 슬롯 배치 / 슬롯 우클릭 해제)");
            ingTitle.style.fontSize = 14f;
            ingTitle.style.color = new StyleColor(UTKColor.TextPrimary);
            ingTitle.style.marginTop = 6f;
            right.Add(ingTitle);

            var ingScroll = new ScrollView(ScrollViewMode.Vertical);
            ingScroll.style.flexGrow = 1f;
            right.Add(ingScroll);
            _ingGridHost = ingScroll.contentContainer;
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            _ingGridHost.Add(grid);
            _ingGridHost = grid;
        }

        private void AddTab(VisualElement parent, string label, RecipeTab tab)
        {
            var btn = UTKButton.Create(label, () =>
            {
                _tab = tab;
                RefreshRecipeList();
            }, UTKButton.Variant.Secondary);
            btn.style.height = 24f;
            btn.style.marginRight = 4f;
            parent.Add(btn);
        }

        /// <summary>하단: COOK 버튼 + 결과 메시지.</summary>
        private void BuildFooter()
        {
            var cookBtn = UTKButton.Create("🔥 요리 제작하기 (COOK)", OnCookClicked, UTKButton.Variant.Primary);
            cookBtn.style.height = 40f;
            cookBtn.style.marginTop = 6f;
            _content.Add(cookBtn);

            _messageLabel = new Label("");
            _messageLabel.style.fontSize = 14f;
            _messageLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _messageLabel.style.whiteSpace = WhiteSpace.Normal;
            _messageLabel.style.marginTop = 4f;
            _content.Add(_messageLabel);
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

            if (recipe.HasValue)
            {
                var r = recipe.Value;
                _rateLabel.text = "조리 성공 확률: 100%";
                _rateLabel.style.color = new StyleColor(UTKColor.GuildGreen);
                _resultCell.SetIcon(null);   // 요리 아이콘은 별도 베이크 전 — 이름 표시로 대체
                _resultNameLabel.text = r.name;
                SetMessage("", UTKColor.TextSecondary);
            }
            else
            {
                int placed = PlacedCategories().Count;
                if (placed >= 2)
                {
                    _rateLabel.text = "조리 성공 확률: 0% (조합 없음)";
                    _rateLabel.style.color = new StyleColor(UTKColor.HealthRed);
                    SetMessage("해당 조합의 요리가 없습니다.", UTKColor.TextSecondary);
                }
                else
                {
                    _rateLabel.text = "조리 성공 확률: 0%";
                    _rateLabel.style.color = new StyleColor(UTKColor.TextSecondary);
                    SetMessage("재료 2~3개를 배치하세요.", UTKColor.TextSecondary);
                }
                _resultNameLabel.text = "—";
            }

            if (_selectedRecipe.HasValue)
            {
                var r = _selectedRecipe.Value;
                _detailLabel.text = $"요리명: {r.name}\n효과: 체력 회복\n재료 카테고리: {CategoryKo(r.cats)}";
            }
            else
            {
                _detailLabel.text = "레시피를 선택하세요.";
            }
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

            // 요리 ItemData — 인라인 팩토리 (RecipeDef 기반, dish_ 접두 id)
            var dish = new PlayerInventory.ItemData
            {
                id = "dish_" + r.id,
                displayName = r.name,
                description = "재료를 우리러 낸 풍미 요리.",
                category = PlayerInventory.ItemCategory.Food,
                maxStack = 99,
                effects = "체력 회복",
            };
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
            style.left = 200f;
            style.top = 80f;
            for (int i = 0; i < 3; i++) _slots[i] = null;
            SetMessage("", UTKColor.TextSecondary);
            RefreshAll();
            StartRefreshLoop();
            Debug.Log("[CookingUTK] 요리 창 열림");
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
                if (IsOpen) { RefreshSlotRow(); RefreshIngredientGrid(); RefreshRateAndDetail(); }
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
