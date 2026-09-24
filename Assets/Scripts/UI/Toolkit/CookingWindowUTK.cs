// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // PlayerInventory, PlayerStats
using ProjectName.Core.Data;       // CookingDatabase, CookingResult, DishDatabase
using ProjectName.Systems;         // CraftSuccessSystem, CraftResult
using ProjectName.UI;              // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U3 Round C — 요리 창 (CookingUI 581줄 uGUI → UTK 포팅, 핵심 경로만).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/CookingUI.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 고기/약초 인벤토리 그리드 (원본 카테고리 필터: Meat / Herb).
    ///   ② 선택: 고기 1 + 약초 1 → 요리 버튼.
    ///   ③ CookingDatabase.GetCooking(고기명, 약초명) → CraftSuccessSystem.ExecuteCraft(false, grade1, grade2).
    ///   ④ 성공 시 재료 차감 + DishDatabase.GetItemData(음식명) 지급 + EXP(Random 5~15, 원본 패리티).
    ///   [CookingUTK] 로그.
    ///
    /// 300ms 폴링 갱신 (재료 수량 / 진행 반영).
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
        private const float WinW = 560f;
        private const float WinH = 460f;
        private const float SlotSize = 52f;
        private const int Columns = 8;
        private const long RefreshMs = 300L;

        // ===== 상태 =====
        private PlayerInventory.ItemData _selectedMeat;
        private PlayerInventory.ItemData _selectedHerb;

        // ===== 레퍼런스 =====
        private readonly VisualElement _meatGrid;
        private readonly VisualElement _herbGrid;
        private readonly Label _meatSlotLabel;
        private readonly Label _herbSlotLabel;
        private readonly Label _resultLabel;
        private IVisualElementScheduledItem _refreshTask;

        private CookingWindowUTK() : base("요리 테이블", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            var hint = new Label("고기 1 + 약초 1 선택 후 요리.");
            hint.style.fontSize = 13f;
            hint.style.color = new StyleColor(UTKColor.TextSecondary);
            _content.Add(hint);

            // ── 재료 선택 슬롯 ──
            var selectedRow = new VisualElement();
            selectedRow.style.flexDirection = FlexDirection.Row;
            selectedRow.style.marginTop = 6f;
            selectedRow.style.marginBottom = 6f;
            _content.Add(selectedRow);

            _meatSlotLabel = BuildIngredientSlot(selectedRow, "고기", true, true);
            _herbSlotLabel = BuildIngredientSlot(selectedRow, "약초", false, false);

            // ── 고기 그리드 ──
            var meatTitle = new Label("─── 고기 (선택) ───");
            meatTitle.style.fontSize = 14f;
            meatTitle.style.color = new StyleColor(UTKColor.TextPrimary);
            _content.Add(meatTitle);

            var meatScroll = new ScrollView(ScrollViewMode.Vertical);
            meatScroll.style.height = 110f;
            meatScroll.style.marginBottom = 4f;
            _content.Add(meatScroll);
            _meatGrid = new VisualElement();
            _meatGrid.style.flexDirection = FlexDirection.Row;
            _meatGrid.style.flexWrap = Wrap.Wrap;
            meatScroll.Add(_meatGrid);

            // ── 약초 그리드 ──
            var herbTitle = new Label("─── 약초 (선택) ───");
            herbTitle.style.fontSize = 14f;
            herbTitle.style.color = new StyleColor(UTKColor.TextPrimary);
            herbTitle.style.marginTop = 2f;
            _content.Add(herbTitle);

            var herbScroll = new ScrollView(ScrollViewMode.Vertical);
            herbScroll.style.height = 110f;
            herbScroll.style.marginBottom = 4f;
            _content.Add(herbScroll);
            _herbGrid = new VisualElement();
            _herbGrid.style.flexDirection = FlexDirection.Row;
            _herbGrid.style.flexWrap = Wrap.Wrap;
            herbScroll.Add(_herbGrid);

            // ── 요리 버튼 + 결과 ──
            var cookBtn = UTKButton.Create("요리하기", OnCookClicked, UTKButton.Variant.Primary);
            cookBtn.style.marginTop = 6f;
            _content.Add(cookBtn);

            _resultLabel = new Label("");
            _resultLabel.style.fontSize = 15f;
            _resultLabel.style.color = new StyleColor(UTKColor.AccentRare);
            _resultLabel.style.whiteSpace = WhiteSpace.Normal;
            _resultLabel.style.marginTop = 6f;
            _content.Add(_resultLabel);

            ApplyUIToolkitFont(this);
        }

        // ≡≡≡ UI 벌드 헬퍼 ≡≡≡

        /// <summary>재료 선택 슬롯 (커서 호버 + 우클릭 해제). isMeat=true면 고기 슬롯.</summary>
        private Label BuildIngredientSlot(VisualElement parent, string label, bool isMeat_slot, bool isMeat)
        {
            var holder = new VisualElement();
            holder.style.flexDirection = FlexDirection.Column;
            holder.style.alignItems = Align.Center;
            holder.style.marginRight = 16f;
            parent.Add(holder);

            var title = new Label(label);
            title.style.fontSize = 13f;
            title.style.color = new StyleColor(UTKColor.TextSecondary);
            holder.Add(title);

            var slotLabel = new Label("[비어있음]");
            slotLabel.style.width = 150f;
            slotLabel.style.height = 46f;
            slotLabel.style.fontSize = 13f;
            slotLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            slotLabel.style.backgroundColor = new StyleColor(UTKColor.BgPanelDark);
            slotLabel.style.borderLeftWidth = 1f;
            slotLabel.style.borderRightWidth = 1f;
            slotLabel.style.borderTopWidth = 1f;
            slotLabel.style.borderBottomWidth = 1f;
            slotLabel.style.borderLeftColor = new StyleColor(UTKColor.IronLine);
            slotLabel.style.borderRightColor = new StyleColor(UTKColor.IronLine);
            slotLabel.style.borderTopColor = new StyleColor(UTKColor.IronLine);
            slotLabel.style.borderBottomColor = new StyleColor(UTKColor.IronLine);
            slotLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            slotLabel.style.whiteSpace = WhiteSpace.Normal;
            holder.Add(slotLabel);

            // 우클릭 → 재료 해제 (원본 DrawIngredientSlot 우클릭 패리티)
            slotLabel.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                if (isMeat) _selectedMeat = null; else _selectedHerb = null;
                RefreshSelectionLabels();
                RefreshGrids();
            });

            return slotLabel;
        }

        // =====================================================================
        //  데이터 소스 — 인벤토리 카테고리 필터 (원본 CookingUI 패리티)
        // =====================================================================

        private void RefreshSelectionLabels()
        {
            _meatSlotLabel.text = _selectedMeat != null ? _selectedMeat.displayName : "[비어있음]";
            _herbSlotLabel.text = _selectedHerb != null ? _selectedHerb.displayName : "[비어있음]";
        }

        private void RefreshGrids()
        {
            // [Crops/Fish Dishes] 메인 재료 = 고기(Meat) + 작물/과일(Food) + 물고기(Material, id 접두사 fish_glb_).
            List<PlayerInventory.ItemSlot> meatSlots = CollectMainIngredients();
            List<PlayerInventory.ItemSlot> herbSlots = CollectCategory(PlayerInventory.ItemCategory.Herb);

            _meatGrid.Clear();
            _herbGrid.Clear();

            BuildCategoryGrid(meatSlots, _meatGrid, true);
            BuildCategoryGrid(herbSlots, _herbGrid, false);
        }

        private static List<PlayerInventory.ItemSlot> CollectCategory(PlayerInventory.ItemCategory category)
        {
            var result = new List<PlayerInventory.ItemSlot>();
            var inv = PlayerInventory.Instance;
            if (inv == null) return result;
            var slots = inv.GetAllSlots();
            if (slots == null) return result;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null || s.item == null || s.count <= 0) continue;
                if (s.item.category != category) continue;
                result.Add(s);
            }
            return result;
        }

        /// <summary>
        /// 메인 재료(좌측 와이드 그리드) 수집: Meat + Food(작물/과일) + Material 중 id가 "fish_glb_"로
        /// 시작하는 물고기. 신규 작물/어류 요리(CookingDatabase)의 주재료로 선택 가능하게 확장.
        /// </summary>
        private static List<PlayerInventory.ItemSlot> CollectMainIngredients()
        {
            var result = new List<PlayerInventory.ItemSlot>();
            var inv = PlayerInventory.Instance;
            if (inv == null) return result;
            var slots = inv.GetAllSlots();
            if (slots == null) return result;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null || s.item == null || s.count <= 0) continue;
                var cat = s.item.category;
                bool isMain = cat == PlayerInventory.ItemCategory.Meat
                           || cat == PlayerInventory.ItemCategory.Food
                           || (cat == PlayerInventory.ItemCategory.Material
                               && !string.IsNullOrEmpty(s.item.id)
                               && s.item.id.StartsWith("fish_glb_"));
                if (!isMain) continue;
                result.Add(s);
            }
            return result;
        }

        private void BuildCategoryGrid(List<PlayerInventory.ItemSlot> slots, VisualElement grid, bool isMeat)
        {
            if (slots.Count == 0)
            {
                var empty = new Label("(없음)");
                empty.style.fontSize = 13f;
                empty.style.color = new StyleColor(UTKColor.TextSecondary);
                grid.Add(empty);
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var invSlot = slots[i];
                int slotIndex = i;
                var cell = new UTKSlot();
                cell.name = (isMeat ? "Meat_":"Herb_") + slotIndex;
                cell.style.width = SlotSize;
                cell.style.height = SlotSize;
                cell.style.marginTop = 2f;
                cell.style.marginBottom = 2f;
                cell.style.marginLeft = 3f;
                cell.style.marginRight = 3f;
                cell.SetIcon(ItemIconDatabase.GetOrCreateIcon(invSlot.item));
                cell.SetCount(invSlot.count);
                cell.SetRank(UTKRarity.ClassForIndex((int)invSlot.item.rarity));
                cell.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 1) return;
                    if (isMeat) _selectedMeat = invSlot.item; else _selectedHerb = invSlot.item;
                    RefreshSelectionLabels();
                    RefreshGrids();
                });
                grid.Add(cell);
            }
        }

        // =====================================================================
        //  요리 실행 — 원본 CookingUI 요리 경로(472~568) 패리티
        // =====================================================================

        private void OnCookClicked()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                SetResult("인벤토리를 찾을 수 없습니다.", UTKColor.HealthRed);
                return;
            }
            if (_selectedMeat == null || _selectedHerb == null)
            {
                SetResult("고기와 약초를 선택해주세요.", UTKColor.HealthRed);
                return;
            }
            if (inv.GetItemCount(_selectedMeat.id) < 1 || inv.GetItemCount(_selectedHerb.id) < 1)
            {
                SetResult("재료가 인벤토리에 없습니다.", UTKColor.HealthRed);
                return;
            }

            var cookingResult = CookingDatabase.GetCooking(_selectedMeat.displayName, _selectedHerb.displayName);
            if (!cookingResult.HasValue)
            {
                SetResult($"'{_selectedMeat.displayName}'와(과) '{_selectedHerb.displayName}'의 조합으로는 요리를 만들 수 없습니다.", UTKColor.TextSecondary);
                Debug.Log($"[CookingUTK] 알 수 없는 레시피: {_selectedMeat.displayName} + {_selectedHerb.displayName}");
                return;
            }
            var result = cookingResult.Value;

            string grade1 = CraftSuccessSystem.GetGradeFromItemId(_selectedMeat.id);
            string grade2 = CraftSuccessSystem.GetGradeFromItemId(_selectedHerb.id);
            CraftResult craft = CraftSuccessSystem.ExecuteCraft(false, grade1, grade2);

            switch (craft)
            {
                case CraftResult.Success:
                    inv.RemoveItem(_selectedMeat.id, 1);
                    inv.RemoveItem(_selectedHerb.id, 1);
                    inv.AddItem(DishDatabase.GetItemData(result.DishName), 1);
                    RecipeDiscoverySystem.MarkDiscovered(result.DishName);
                    if (PlayerStats.Instance != null)
                        PlayerStats.Instance.AddExp(Random.Range(5, 16));
                    SetResult($"🟢 요리 성공! '{result.DishName}' 획득!", UTKColor.GuildGreen);
                    Debug.Log($"[CookingUTK] 요리 성공: {_selectedMeat.displayName} + {_selectedHerb.displayName} → {result.DishName}");
                    _selectedMeat = null;
                    _selectedHerb = null;
                    break;

                case CraftResult.Fail_MaterialPreserved:
                    SetResult("🟡 요리 실패... 재료가 보존되었다.", UTKColor.TextSecondary);
                    Debug.Log($"[CookingUTK] 요리 실패(재료보존): {_selectedMeat.displayName} + {_selectedHerb.displayName}");
                    break;

                case CraftResult.Fail_MaterialDestroyed:
                    bool destroyMeat = Random.value < 0.5f;
                    string destroyedName = destroyMeat ? _selectedMeat.displayName : _selectedHerb.displayName;
                    string destroyedId = destroyMeat ? _selectedMeat.id : _selectedHerb.id;
                    inv.RemoveItem(destroyedId, 1);
                    SetResult($"🔴 요리 실패! '{destroyedName}'이(가) 소멸했다!", UTKColor.HealthRed);
                    Debug.Log($"[CookingUTK] 요리 실패(재료소멸): → '{destroyedName}' 소멸");
                    break;

                case CraftResult.Fail_Burned:
                    inv.RemoveItem(_selectedMeat.id, 1);
                    inv.RemoveItem(_selectedHerb.id, 1);
                    SetResult("🔥 요리 대실패! 모든 재료가 타서 사라졌다!", UTKColor.HealthRed);
                    Debug.Log($"[CookingUTK] 요리 대실패(전소): {_selectedMeat.displayName} + {_selectedHerb.displayName}");
                    break;
            }

            RefreshSelectionLabels();
            RefreshGrids();
        }

        private void SetResult(string message, Color color)
        {
            _resultLabel.text = message;
            _resultLabel.style.color = new StyleColor(color);
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
            style.left = 240f;
            style.top = 96f;
            _selectedMeat = null;
            _selectedHerb = null;
            _resultLabel.text = "";
            RefreshGrids();
            RefreshSelectionLabels();
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
                if (IsOpen) RefreshGrids();
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