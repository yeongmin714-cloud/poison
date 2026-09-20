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
            Ensure();
            _instance.SetTerritory(territoryId);
            _instance.Show();
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle(string territoryId)
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
            _instance.SetTerritory(territoryId);
        }

        // ===== 설정 =====
        private const float WinW = 420f;   // [U8] 창고 전용 창 (인벤은 독립 창)
        private const float WinH = 680f;
        private const float SlotSize = 52f;
        private const int Columns = 5;     // 행당 N칸 (창고 원본 4열, UTK 그리드 5열)
        private const int MaxSlots = 20;   // 창고 최대 슬롯 (원본 WarehouseUI.MaxSlots / TerritoryWarehouse._maxSlots 기본)
        private const long RefreshMs = 250L;

        // ===== 상태 =====
        private string _territoryId = "default";
        private bool _territoryMenuOpen;

        // ===== 레퍼런스 =====
        private readonly VisualElement _invColumn;
        private readonly VisualElement _whColumn;
        private string _categoryFilter = "전체";   // [U8 요구] 카테고리 탭 필터

        // [우클릭 출고 수리] 우클릭 이중 경로(PointerDown button1 + ContextClickEvent) 중복 발화 방지 가드.
        //   Environment.TickCount(ms) 기준 200ms 내 같은 슬롯 중복 요청은 무시 → 어느 경로로 와도 1회만 실행.
        //   [QA 수정] 슬롯별 키로 분리 — 전역 단일 타이머로는 연속 우클릭(다른 슬롯)을 200ms간 씹는다.
        private static readonly int WithdrawDedupeMs = 200;
        private readonly System.Collections.Generic.Dictionary<int, int> _lastWithdrawMsPerSlot
            = new System.Collections.Generic.Dictionary<int, int>();
        private readonly VisualElement _invGrid;
        private readonly VisualElement _whGrid;
        private readonly Label _territoryButton;
        private readonly Label _capacityLabel;
        private Label _expandButton;   // [C-O2-03b] 확장 버튼
        private readonly VisualElement _territoryMenu;
        private Label _statusLabel;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private WarehouseWindowUTK() : base("영지 창고", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            // ── 헤더 행: 제목 + 영지 선택 + 용량 ──
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginTop = 4f;
            headerRow.style.marginBottom = 6f;

            var title = new Label("영지 창고");
            title.AddToClassList("utk-title-label");
            title.style.fontSize = 18f;
            headerRow.Add(title);

            _territoryButton = new Label("영지: default");
            _territoryButton.AddToClassList("utk-close-btn");
            _territoryButton.style.fontSize = 13f;
            _territoryButton.style.marginRight = 8f;
            _territoryButton.RegisterCallback<PointerDownEvent>(evt => ToggleTerritoryMenu(evt));
            headerRow.Add(_territoryButton);

            _capacityLabel = new Label("");
            _capacityLabel.style.fontSize = 13f;
            _capacityLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            headerRow.Add(_capacityLabel);

            // [C-O2-03b] 슬롯 확장 버튼 — 골드 유출구 (비용 표시, 최대 도달 시 비활성)
            _expandButton = new Label("");
            _expandButton.AddToClassList("utk-btn");
            _expandButton.style.fontSize = 13f;
            _expandButton.style.marginLeft = 8f;
            _expandButton.RegisterCallback<PointerDownEvent>(_ => OnExpandClicked());
            headerRow.Add(_expandButton);

            _content.Add(headerRow);

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
            _invColumn.style.borderTopWidth = 1f;
            _invColumn.style.borderBottomWidth = 1f;
            _invColumn.style.borderLeftWidth = 1f;
            _invColumn.style.borderRightWidth = 1f;
            _invColumn.style.borderTopColor = new StyleColor(UTKColor.IronLine);
            _invColumn.style.borderBottomColor = new StyleColor(UTKColor.IronLine);
            _invColumn.style.borderLeftColor = new StyleColor(UTKColor.IronLine);
            _invColumn.style.borderRightColor = new StyleColor(UTKColor.IronLine);

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
            _whColumn.style.borderTopWidth = 1f;
            _whColumn.style.borderBottomWidth = 1f;
            _whColumn.style.borderLeftWidth = 1f;
            _whColumn.style.borderRightWidth = 1f;
            _whColumn.style.borderTopColor = new StyleColor(UTKColor.IronLine);
            _whColumn.style.borderBottomColor = new StyleColor(UTKColor.IronLine);
            _whColumn.style.borderLeftColor = new StyleColor(UTKColor.IronLine);
            _whColumn.style.borderRightColor = new StyleColor(UTKColor.IronLine);

            var whHeading = new Label("🏰 창고 (드래그 → 좌측 인벤 = 출고 / 우클릭 = 출고)");
            whHeading.style.fontSize = 14f;
            whHeading.style.color = new StyleColor(UTKColor.TextSecondary);
            _whColumn.Add(whHeading);

            // [U8 요구] 카테고리 탭 — 무기/방어구/재료/소모품/전체 필터
            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.marginBottom = 4f;
            _whColumn.Add(tabs);
            string[] tabNames = { "전체", "무기", "방어구", "재료", "소모품" };
            foreach (var tn in tabNames)
            {
                string captured = tn;
                var tabBtn = UTKButton.Create(tn, () => { _categoryFilter = captured; RefreshGrid(); }, 
                    captured == "전체" ? UTKButton.Variant.Primary : UTKButton.Variant.Secondary);
                tabBtn.style.height = 26f;
                tabs.Add(tabBtn);
            }

            _whGrid = new VisualElement();
            _whGrid.name = "WarehouseGrid";
            _whGrid.style.flexDirection = FlexDirection.Row;
            _whGrid.style.flexWrap = Wrap.Wrap;
            _whGrid.style.justifyContent = Justify.Center;   // 행당 5칸 중앙 배치 — 좌우 여백 동일
            _whGrid.style.marginTop = 4f;
            _whGrid.style.marginBottom = 4f;
            _whColumn.Add(_whGrid);

            columnsRow.Add(_whColumn);

            // ── 상태 라벨 ──
            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 13f;
            _statusLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_statusLabel);

            ApplyUIToolkitFont(this);

            // 기본 숨김 + 우측 배치 (좌:인벤/우:창고 — 인벤 16px 좌측에 대응)
            style.display = DisplayStyle.None;
            style.left = 1044f;
            style.top = 96f;
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
            style.left = 1044f;
            style.top = 96f;
            StartRefreshLoop();
            RefreshGrid();
            Debug.Log($"[WarehouseUTK] 창고 창 열림 (영지: {_territoryId})");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            CloseTerritoryMenu();
            Debug.Log($"[WarehouseUTK] 창고 창 닫힘 (영지: {_territoryId})");
        }

        /// <summary>창이 UIRoot에 부착된 후 드롭 타겟 등록 (좌우 컬럼 = 양방향 입출고 타겟).</summary>
        protected override void OnWindowOpen()
        {
            UTKDragDrop.RegisterDropTarget(_invColumn, this);   // 창고→인벤 출고 타겟
            UTKDragDrop.RegisterDropTarget(_whColumn, this);    // 인벤→창고 입고 타겟
        }

        /// <summary>창 닫힘 — 타겟 해제(재열림 시 재등록).</summary>
        protected override void OnWindowClosed()
        {
            UTKDragDrop.UnregisterDropTarget(_invColumn);
            UTKDragDrop.UnregisterDropTarget(_whColumn);
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
        private void OnExpandClicked()
        {
            var ws = WarehouseSystem.Instance;
            if (ws == null)
            {
                _statusLabel.text = "창고 시스템 없음";
                return;
            }
            string result = ws.TryExpandSlots(_territoryId);
            _statusLabel.text = result;
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
            _capacityLabel.text = $"용량: {used}/{capacity}";

            // 확장 버튼 상태
            if (_expandButton != null)
            {
                if (WarehouseSystem.Instance == null || !WarehouseSystem.Instance.CanExpand(_territoryId))
                {
                    _expandButton.text = "확장 최대";
                    _expandButton.style.opacity = 0.45f;
                    _expandButton.pickingMode = PickingMode.Ignore;
                }
                else
                {
                    int cost = WarehouseSystem.Instance.GetNextExpansionCost(_territoryId);
                    _expandButton.text = $"+{WarehouseSystem.SlotsPerExpansion}슬롯 ({cost}G)";
                    _expandButton.style.opacity = 1f;
                    _expandButton.pickingMode = PickingMode.Position;
                }
            }
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
                    var s = allItems[i];
                    if (s != null && s.item != null && _categoryFilter != "전체" && !CategoryMatches(s.item.category, _categoryFilter))
                        continue;
                    items.Add(s);
                    actualIdx.Add(i);
                }
            }
            int total = items != null ? items.Count : 0;

            // [C-O2-03b] 확장 반영 동적 행 수
            int dynamicCapacity = WarehouseSystem.Instance != null
                ? WarehouseSystem.Instance.GetSlotCapacity(_territoryId)
                : MaxSlots;
            int rows = (dynamicCapacity + Columns - 1) / Columns;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    int idx = r * Columns + c;
                    int actual = idx < actualIdx.Count ? actualIdx[idx] : idx;
                    _whGrid.Add(BuildWarehouseCell(items, idx, total, actual));
                }
            }
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

            return cell;
        }

        private void StyleSlot(UTKSlot cell)
        {
            cell.style.width = SlotSize;
            cell.style.height = SlotSize;
            cell.style.marginTop = 2f;
            cell.style.marginBottom = 2f;
            cell.style.marginLeft = 3f;
            cell.style.marginRight = 3f;
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
            if (payload == null || payload.Item == null) return false;
            // 좌측(인벤 컬럼) = 출고: 창고 소스만. 우측(창고 컬럼) = 입고: 인벤 소스만.
            return payload.Source == UTKDragSourceKind.Inventory
                || payload.Source == UTKDragSourceKind.Warehouse;
        }

        public bool Drop(UTKDragPayload payload)
        {
            if (payload == null || payload.Item == null) return false;

            if (payload.Source == UTKDragSourceKind.Inventory)
            {
                // ① 인벤 → 창고 입고 (드롭 대상: 우측 창고 컬럼) — 원본 TryDepositFromDrag 데이터 경로
                bool ok = DepositItem(payload.Item);
                RefreshGrid();
                Debug.Log($"[WarehouseUTK] 입고(드래그): {payload.Item.displayName} → {_territoryId} (성공={ok})");
                return ok;
            }

            if (payload.Source == UTKDragSourceKind.Warehouse)
            {
                // ② 창고 → 인벤 출고 (드롭 대상: 좌측 인벤 컬럼) — 원본 TransferDraggedToInventory 데이터 경로
                if (string.IsNullOrEmpty(payload.TerritoryId) || payload.SourceIndex < 0) return false;
                bool ok = WithdrawSlot(payload.SourceIndex);
                RefreshGrid();
                Debug.Log($"[WarehouseUTK] 출고(드래그): {payload.Item.displayName} ← {payload.TerritoryId} (성공={ok})");
                return ok;
            }

            return false;
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
            var inv = PlayerInventory.Instance;
            if (inv == null || item == null) return false;
            if (!inv.RemoveItem(item.id, 1))
            {
                Debug.LogWarning($"[WarehouseUTK] 입고 실패 — 인벤에 아이템 없음: {item.displayName}");
                return false;
            }
            bool ok = DepositItem(item);
            if (!ok)
            {
                inv.AddItem(item, 1);   // 창고 가득 → 인벤 롤백
                Debug.Log("[WarehouseUTK] 창고 가득 — 인벤 롤백");
                return false;
            }
            RefreshGrid();
            Debug.Log($"[WarehouseUTK] 입고(우클릭): {item.displayName}");
            return true;
        }

        private bool DepositItem(PlayerInventory.ItemData item)
        {
            if (item == null) return false;
            if (WarehouseSystem.Instance == null || PlayerInventory.Instance == null) return false;
            string tid = _territoryId;
            if (string.IsNullOrEmpty(tid)) return false;

            // [입고 실패 진단] RemoveItem 실패 사유 보강 — 개수 부족 vs ID 불일치(재고 없음) 판별.
            int invCount = PlayerInventory.Instance.GetItemCount(item.id);
            bool removed = PlayerInventory.Instance.RemoveItem(item.id, 1);
            if (!removed)
            {
                if (invCount <= 0)
                    Debug.Log($"[WarehouseUTK] 입고 취소(사유: 인벤 ID 불일치 — 재고 0) — {item.displayName}(id={item.id}) 유지");
                else
                    Debug.LogWarning($"[WarehouseUTK] 입고 취소(사유: RemoveItem 실패, 잔여 {invCount}개) — {item.displayName}(id={item.id}) 유지");
                return false;
            }
            if (!WarehouseSystem.Instance.AddItem(tid, item, 1))
            {
                PlayerInventory.Instance.AddItem(item, 1);   // 창고 가득 → 롤백
                Debug.LogWarning($"[WarehouseUTK] 창고 가득 — 입고 취소(인벤 롤백): {item.displayName}");
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
            int now = System.Environment.TickCount;
            int last = _lastWithdrawMsPerSlot.TryGetValue(slotIndex, out int prev) ? prev : int.MinValue;
            if (now - last < WithdrawDedupeMs)
            {
                Debug.Log($"[WarehouseUTK] 출고 중복 요청 무시 slot={slotIndex} ({now - last}ms 내)");
                return false;
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

            if (!WarehouseSystem.Instance.TransferToInventory(_territoryId, slotIndex, 1))
            {
                Debug.LogWarning($"[WarehouseUTK] 출고 실패(인벤 가득 or 슬롯 변경) slot={slotIndex} tid={_territoryId}");
                return false;
            }
            RefreshGrid();   // [우클릭 출고 수리] 출고 직후 즉시 갱신 — 250ms 폴링에만 의존하지 않음
            Debug.Log($"[WarehouseUTK] 출고(우클릭): slot={slotIndex} ← {_territoryId}");
            return true;
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