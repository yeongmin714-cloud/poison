using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // PlayerInventory
using ProjectName.Systems;         // EquipmentManager, LootBasket
using ProjectName.UI;              // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U2 Round 1 — 인벤토리 통합 그리드 (MVP 6경로).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/InventoryWindow.cs (3891줄) — 그 전체 기능이 아닌 후속 6경로만 이식.
    /// 원본은 절대 수정하지 않는다.
    ///
    /// [MVP 6경로]
    ///  ① 통합 그리드   — PlayerInventory.GetAllSlots() 전 카테고리를 단일 그리드(행당 N칸)로 표시.
    ///                     UTKSlot(아이콘+카운트+등급 테두리), 매 갱신 재조회(즉시 반영).
    ///  ② Loot→인벤     — IUTKDropTarget 구현, Drop에서 PlayerInventory.AddItem 호출(원본 TryTakeDraggedToInventory 데이터 경로).
    ///  ③ 인벤 내 스왑  — 드래그 소스=인벤 슬롯, 드롭 타겟=인벤 슬롯 → 원본 슬롯 교체 경로 호출.
    ///  ④ 인벤 밖 드롭  — 땅에 바구니(LootBasket.Create + AddItem) — 원본 TryDropDraggedToTerrain와 동일 API.
    ///  ⑤ 장비 해제 수신— EquipmentManager.OnEquipmentChanged 구독해 갱신만(해제 자체는 Equipment 창 라운드).
    ///  ⑥ 즉시 갱신     — 폴링(refresh 스케줄) + 장비 이벤트 + 드롭 직후 재조회.
    ///  각 경로에 [InventoryUTK] Debug.Log 실측 로그.
    /// </summary>
    public class InventoryWindowUTK : UTKWindowBase, IUTKDropTarget
    {
        // ===== 싱글턴 / 팩토리 =====
        private static InventoryWindowUTK _instance;
        public static InventoryWindowUTK Instance => _instance;

        /// <summary>팩토리 — UIRoot 좌측 배치(좌:인벤 관례). 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new InventoryWindowUTK();
        }

        /// <summary>인벤 열기(팩토리 겸용).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 =====
        private const float WinW = 560f;    // [독립 창] 좌측 인벤창(장비 2x5 + 가방 그리드 행당 7칸) — 설명/창고/전리품은 별개 창
        private const float WinH = 700f;
        private const int BagColumns = 5;   // [미참조] 실제 그리드 행당 칸은 Columns=7
        private const int BagRows = 6;
        private const int EquipColumns = 5; // 장비 2줄×5칸
        private const int EquipRows = 2;
        private const float SlotSize = 64f;
        private const int Columns = 7;   // 행당 N칸
        private const long RefreshMs = 250L;

        // ===== 레퍼런스 =====
        private readonly VisualElement _grid;
        private readonly VisualElement _equipPanel;        // [3분할] 좌측 상단 장비 2x5
        private readonly Dictionary<string, Label> _equipSlotLabels = new Dictionary<string, Label>();
        private readonly VisualElement _warehousePanel;    // [3분할] 우측 창고 그리드 (상호작용시만)
        private readonly Label _warehouseEmptyHint;
        private Label _descName;                            // [3분할] 중앙 설명창
        private Label _descText;
        private Label _selectedLabel;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;
        private EquipmentManager _subscribedEquip;

        /// <summary>현재 선택된 인벤 아이템 (QuickSlot 등록용 노출).</summary>
        private PlayerInventory.ItemData _selectedItemData;

        // =====================================================================
        //  선택 접근자 — QuickSlotUTK 등록용 (원본 InventoryWindow.HasSelectedItem/GetSelectedItemData 대응)
        // =====================================================================

        /// <summary>인벤토리에서 선택된 아이템이 있는지.</summary>
        public bool HasSelectedInInventory() => _selectedItemData != null;

        /// <summary>현재 선택된 인벤토리 아이템 (없으면 null).</summary>
        public PlayerInventory.ItemData GetSelectedInventoryItem() => _selectedItemData;

        // 드롭 타겟 재생성 위생: 각 리프레시에서 이전 슬롯 바인딩 해제 후 재등록.
        private readonly List<VisualElement> _slotTargets = new List<VisualElement>();

        private InventoryWindowUTK() : base("인벤토리", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            // ④ 월드 드롭 백드롭: UIRoot(전체 화면) — 인벤 드롭이 UI 밖이면 땅에 바구니.
            RegisterWorldDrop();

            // [3분할 레이아웃] 좌: 장비 2x5+가방 6x5 / 중: 설명창 / 우: 창고(상호작용시)
            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexGrow = 1f;
            _content.Add(columns);

            // ── 좌측 패널: 장비 2줄×5칸 + 가방 그리드(행당 7칸) ──
            var leftCol = new VisualElement();
            leftCol.style.flexGrow = 1f;   // 고정 380px 제거 — 창 폭을 채워 우측 빈 여백 제거(좌우 대칭)
            leftCol.style.marginRight = 10f;
            columns.Add(leftCol);

            var equipTitle = new Label("장비");
            equipTitle.style.fontSize = 17f;
            equipTitle.style.color = new StyleColor(UTKColor.AccentRare);
            equipTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            leftCol.Add(equipTitle);

            _equipPanel = new VisualElement();
            _equipPanel.style.flexDirection = FlexDirection.Row;
            _equipPanel.style.flexWrap = Wrap.Wrap;
            _equipPanel.style.justifyContent = Justify.Center;   // 장비 행 좌우 대칭(우측 쏠림 제거)
            _equipPanel.style.marginBottom = 10f;
            leftCol.Add(_equipPanel);
            BuildEquipPanel();

            var bagTitle = new Label("가방");
            bagTitle.style.fontSize = 17f;
            bagTitle.style.color = new StyleColor(UTKColor.TextPrimary);
            bagTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            leftCol.Add(bagTitle);

            _grid = new VisualElement();
            _grid.name = "InvGrid";
            _grid.style.flexDirection = FlexDirection.Row;
            _grid.style.flexWrap = Wrap.Wrap;
            _grid.style.justifyContent = Justify.Center;   // 행당 7칸 중앙 배치 — 행 좌우 여백 동일
            _grid.style.marginTop = 6f;
            leftCol.Add(_grid);

            _selectedLabel = new Label("");
            _selectedLabel.style.fontSize = 13f;
            _selectedLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _selectedLabel.style.whiteSpace = WhiteSpace.Normal;
            _selectedLabel.style.marginTop = 4f;
            leftCol.Add(_selectedLabel);

            // [독립 창] 설명창은 ItemDescriptionWindowUTK(별개 창), 창고/전리품은 각각 독립 창.

            ApplyUIToolkitFont(this);

            // 윈도우 자체 = ②Loot 수령 / ③패널 위 인벤 취소 타겟 (슬롯보다 하위 우선).
            UTKDragDrop.RegisterDropTarget(this, this);

            // 기본 숨김 + 좌측 배치 관례
            style.display = DisplayStyle.None;
            style.left = 16f;
            style.top = 96f;
        }

        // =====================================================================
        //  생명주기 (UTKWindowBase 훅)
        // =====================================================================

        public override void Show()
        {
            base.Show();   // Register + 표시 + OnWindowOpen
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 16f;   // 좌측 고정
            style.top = 96f;
            EnsureEquipSubscription();
            StartRefreshLoop();
            RefreshGrid();
            RefreshEquipPanel();   // [U8 확장] 장비 임베드 갱신
            Debug.Log("[InventoryUTK] 인벤토리 창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[InventoryUTK] 인벤토리 창 닫힘");
        }

        /// <summary>[U8 확장] 우측 장비 임베드 패널 구성 — EquipmentManager 실측 데이터, 우클릭=해제(UnequipSlot).</summary>
        private void BuildEquipPanel()
        {
            var em = EquipmentManager.Instance ?? EquipmentManager.Get();
            if (em == null)
            {
                _equipPanel.Add(new Label("(장비 시스템 없음)"));
                return;
            }

            var slots = new System.Collections.Generic.List<(string, EquipmentManager.EquipmentSlot)>
            {
                ("무기", EquipmentManager.EquipmentSlot.Weapon), ("방패", EquipmentManager.EquipmentSlot.Back),
                ("투구", EquipmentManager.EquipmentSlot.Helmet), ("갑옷", EquipmentManager.EquipmentSlot.Armor),
                ("신발", EquipmentManager.EquipmentSlot.Shoes), ("장갑", EquipmentManager.EquipmentSlot.Gloves),
                ("가면", EquipmentManager.EquipmentSlot.Mask), ("가방", EquipmentManager.EquipmentSlot.Bag)
            };

            // 2줄×5칸 격자 — 8슬롯+빈 2칸 (예시 레이아웃 정합)
            while (slots.Count < EquipColumns * EquipRows)
                slots.Add(("", (EquipmentManager.EquipmentSlot)999));

            foreach (var pair in slots)
            {
                string label = pair.Item1;
                var slot = pair.Item2;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4f;
                _equipPanel.Add(row);

                var name = new Label(label);
                name.style.width = 40f;
                name.style.fontSize = 13f;
                name.style.color = new StyleColor(UTKColor.TextSecondary);
                row.Add(name);

                var value = new Label("—");
                value.style.fontSize = 13f;
                value.style.color = new StyleColor(UTKColor.TextPrimary);
                value.style.flexGrow = 1f;
                value.style.whiteSpace = WhiteSpace.Normal;
                row.Add(value);
                _equipSlotLabels[slot.ToString()] = value;

                // 우클릭 = 해제 (원본 TryRenderEmbedded 관례 — EquipmentManager.UnequipSlot)
                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 1 && (int)slot <= 8)
                    {
                        bool ok = em.UnequipSlot(slot);
                        Debug.Log($"[InventoryUTK] 장비 해제(우클릭) slot={slot} → {ok}");
                    }
                });
            }
        }

        /// <summary>[U8 확장] 장비 임베드 라벨 갱신 — 창 열림/장비 변경 시.</summary>
        private void RefreshEquipPanel()
        {
            var em = EquipmentManager.Instance ?? EquipmentManager.Get();
            if (em == null || _equipPanel == null) return;
            foreach (var kv in _equipSlotLabels)
            {
                var data = em.GetSlotData((EquipmentManager.EquipmentSlot)System.Enum.Parse(typeof(EquipmentManager.EquipmentSlot), kv.Key));
                kv.Value.text = (data != null && !string.IsNullOrEmpty(data.itemId)) ? data.itemId : "—";
            }
        }

        /// <summary>[3분할] 중앙 설명창 갱신 — 슬롯 클릭 시 호출. PlayerInventory.ItemData 직접 표시.</summary>
        public void ShowItemDescription(PlayerInventory.ItemData item, int count)
        {
            if (_descName == null) return;
            if (item == null) { _descName.text = "아이템을 선택하세요"; _descText.text = ""; return; }
            _descName.text = item.displayName ?? item.id;
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(item.description)) sb.AppendLine(item.description);
            sb.AppendLine($"카테고리: {item.category}");
            sb.AppendLine($"등급: {item.rarity}");
            if (item.maxDurability > 0) sb.AppendLine($"내구도: {item.maxDurability}");
            _descText.text = sb.ToString();
            Debug.Log($"[InventoryUTK] 설명창 갱신 — {item.displayName ?? item.id}");
        }

        /// <summary>[3분할] 우측 창고 패널 표시/숨김 — 창고 상호작용 시 Show(territoryId) 경유.</summary>
        public void SetWarehouseMode(string territoryId, bool visible)
        {
            if (_warehousePanel == null || _warehouseEmptyHint == null) return;
            _warehousePanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _warehouseEmptyHint.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            Debug.Log($"[InventoryUTK] 창고 패널 {(visible ? "표시" : "숨김")} (territory={territoryId})");
        }

        /// <summary>창이 UIRoot에 부착(등록)된 후 ④ 백드롭과 ②③ 윈도우 타겟을 등록한다.</summary>
        protected override void OnWindowOpen()
        {
            RegisterWorldDrop();
            UTKDragDrop.RegisterDropTarget(this, this);
        }

        /// <summary>창 닫힘 — 창 타겟 해제(재열림 시 재등록).</summary>
        protected override void OnWindowClosed()
        {
            UTKDragDrop.UnregisterDropTarget(this);
            UnsubscribeEquip();
        }

        // =====================================================================
        //  ⑥ 즉시 갱신 — 폴링 + 장비 이벤트
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

        private void EnsureEquipSubscription()
        {
            var em = EquipmentManager.Get();
            if (!ReferenceEquals(_subscribedEquip, em))
            {
                if (_subscribedEquip != null) _subscribedEquip.OnEquipmentChanged -= OnEquipmentChanged;
                _subscribedEquip = em;
                if (_subscribedEquip != null) _subscribedEquip.OnEquipmentChanged += OnEquipmentChanged;
            }
        }

        private void UnsubscribeEquip()
        {
            if (_subscribedEquip != null)
            {
                _subscribedEquip.OnEquipmentChanged -= OnEquipmentChanged;
                _subscribedEquip = null;
            }
        }

        /// <summary>⑤ 장비 해제 수신 — 외부에서 해제가 일어나면 즉시 갱신만(해제 자체는 Equipment 창 라운드).</summary>
        private void OnEquipmentChanged(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            if (!IsOpen) return;
            Debug.Log($"[InventoryUTK] 장비 변경 수신(slot={slot}, item={itemId ?? "none"}) — 그리드 갱신");
            RefreshEquipPanel();   // [U8 확장] 장비 임베드 즉시 갱신
            RefreshGrid();
        }

        // =====================================================================
        //  ① 통합 그리드 — 매 갱신 재조회
        // =====================================================================

        private void RefreshGrid()
        {
            // [U8 수리] 드래그 중 셀 재생성 금지 — 캡처 상실로 드래그 도중 취소되는 뿌리 차단
            if (UTKDragDrop.Active) return;

            var inv = PlayerInventory.Instance;
            var slots = inv != null ? inv.GetAllSlots() : null;
            int total = slots != null ? slots.Length : 0;

            // 이전 슬롯 드롭 타겟 해제
            for (int i = 0; i < _slotTargets.Count; i++)
            {
                var cell = _slotTargets[i];
                UTKDragDrop.UnregisterDropTarget(cell);
                if (cell != null && cell.parent == _grid)
                    cell.RemoveFromHierarchy();
            }
            _slotTargets.Clear();
            _grid.Clear();

            int rows = total > 0 ? (total + Columns - 1) / Columns : 1;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    int idx = r * Columns + c;
                    _grid.Add(BuildSlotCell(slots, idx, total));
                }
            }
        }

        private VisualElement BuildSlotCell(PlayerInventory.ItemSlot[] slots, int idx, int total)
        {
            var cell = new UTKSlot();
            cell.name = "InvSlot_" + idx;
            cell.style.width = SlotSize;
            cell.style.height = SlotSize;
            cell.style.marginTop = 2f;
            cell.style.marginBottom = 2f;
            cell.style.marginLeft = 3f;
            cell.style.marginRight = 3f;

            if (idx < total && slots[idx] != null && slots[idx].item != null && slots[idx].count > 0)
            {
                var slotData = slots[idx];
                cell.SetIcon(ItemIconDatabase.GetOrCreateIcon(slotData.item));
                cell.SetCount(slotData.count);
                cell.SetRank(UTKRarity.ClassForIndex((int)slotData.item.rarity));

                // ③ 인벤 소스 드래그(좌/우) + 좌클릭 설명 + 우클릭 비드래그=사용/장착
                int globalIdx = idx;
                var item = slotData.item;
                var slotRef = slotData;
                UTKDragDrop.MakeDraggable(cell, () => MakeSlotPayload(globalIdx, item),
                    () => OnSlotClick(globalIdx),
                    () => OnSlotRightClick(globalIdx, slotRef, item));

                // ③ 인벤 타겟 — 슬롯 위 드롭 = 교체(스왑) (슬롯이 윈도우 타겟보다 상위 우선)
                IUTKDropTarget slotTarget = new SlotDropTarget(this, globalIdx);
                UTKDragDrop.RegisterDropTarget(cell, slotTarget);
                _slotTargets.Add(cell);
            }
            else
            {
                cell.SetRank("common");   // 빈 셀 가이드
            }

            return cell;
        }

        private UTKDragPayload MakeSlotPayload(int slotIndex, PlayerInventory.ItemData item)
        {
            var p = new UTKDragPayload();
            p.Source = UTKDragSourceKind.Inventory;
            p.SourceIndex = slotIndex;
            p.Item = item;
            p.Icon = item != null ? ItemIconDatabase.GetOrCreateIcon(item) : null;
            return p;
        }

        private void OnSlotClick(int slotIndex)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;
            var slots = inv.GetAllSlots();
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
            var slotData = slots[slotIndex];
            if (slotData == null || slotData.item == null)
            {
                _selectedLabel.text = "";
                _selectedItemData = null;
                ItemDescriptionWindowUTK.Clear();
                return;
            }
            _selectedLabel.text = $"{slotData.item.displayName}  x{slotData.count}  —  {slotData.item.description}";
            _selectedItemData = slotData.item;
            ItemDescriptionWindowUTK.ShowItem(slotData.item, slotData.count);   // [독립 창] 중앙 설명창 갱신
            Debug.Log($"[InventoryUTK] 슬롯 선택(클릭): {slotData.item.displayName} (슬롯 {slotIndex})");
        }

        /// <summary>[U8 요구] 가방 우클릭(비드래그) — 창고 열림 중=입고 / 소모품=사용 / 무기·방어구=장착.</summary>
        private void OnSlotRightClick(int slotIndex, PlayerInventory.ItemSlot slotRef, PlayerInventory.ItemData item)
        {
            // [U8 요구] 창고가 열려 있으면 우클릭 = 입고 (창고↔인벤 이동)
            var wh = WarehouseWindowUTK.Instance;
            if (wh != null && wh.IsOpen)
            {
                bool deposited = wh.DepositFromInventory(slotIndex, item);
                if (deposited) RefreshGrid();
                return;
            }

            var cat = item.category;
            if (cat == PlayerInventory.ItemCategory.Weapon || cat == PlayerInventory.ItemCategory.Armor)
            {
                var origin = ProjectName.UI.InventoryWindow.Instance;
                if (origin != null)
                {
                    origin.TryEquipItemPublic(slotRef);
                    RefreshGrid();
                    Debug.Log($"[InventoryUTK] 장착(우클릭): {item.displayName}");
                }
                else Debug.LogWarning("[InventoryUTK] InventoryWindow.Instance 없음 — 장착 불가");
                return;
            }
            if (cat == PlayerInventory.ItemCategory.Potion
                || cat == PlayerInventory.ItemCategory.Food
                || cat == PlayerInventory.ItemCategory.Herb
                || cat == PlayerInventory.ItemCategory.Drug)
            {
                var pi = PlayerInventory.Instance;
                if (pi != null)
                {
                    pi.UseItem(slotIndex);
                    RefreshGrid();
                    ItemDescriptionWindowUTK.Clear();
                    Debug.Log($"[InventoryUTK] 소모품 사용(우클릭): {item.displayName}");
                }
                return;
            }
            Debug.Log($"[InventoryUTK] 우클릭 — {item.displayName} (사용/장착 대상 아님)");
        }

        // =====================================================================
        //  IUTKDropTarget — ② Loot 수령 / ③ 패널 위 인벤 취소
        // =====================================================================

        public bool CanDrop(UTKDragPayload payload)
        {
            if (payload == null || payload.Item == null) return false;
            // Loot → 인벤 수령 / 창고 → 인벤 출고 / 인벤 → 패널 위(빈 영역)는 소비(취소 — 땅 드롭 방지)
            return payload.Source == UTKDragSourceKind.Loot
                || payload.Source == UTKDragSourceKind.Warehouse
                || payload.Source == UTKDragSourceKind.Inventory;
        }

        public bool Drop(UTKDragPayload payload)
        {
            if (payload == null || payload.Item == null) return false;

            if (payload.Source == UTKDragSourceKind.Loot)
            {
                TakeLootToInventory(payload);
                return true;
            }
            if (payload.Source == UTKDragSourceKind.Inventory)
            {
                // 인벤 그리드 위(빈 셀/패널) 드롭 = 소비(취소) — 아이템 유지. 슬롯 교체는 슬롯 타겟이 담당.
                return true;
            }
            if (payload.Source == UTKDragSourceKind.Warehouse)
            {
                // [U8 수리] 창고 → 인벤 출고 (드래그) — WarehouseSystem.TransferToInventory 단일 경로
                var ws = WarehouseSystem.Instance;
                if (ws == null) return false;
                bool ok = ws.TransferToInventory(payload.TerritoryId, payload.SourceIndex, 1);
                if (ok) RefreshGrid();
                Debug.Log($"[InventoryUTK] 창고→인벤 출고(드래그): {payload.Item.displayName} (성공={ok})");
                return ok;
            }
            return false;
        }

        // =====================================================================
        //  데이터 경로 (원본 시그니처 실측 맵핑)
        // =====================================================================

        /// <summary>② Loot→인벤: PlayerInventory.AddItem (원본 TryTakeDraggedToInventory 데이터 경로).</summary>
        private void TakeLootToInventory(UTKDragPayload payload)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;
            bool ok = inv.AddItem(payload.Item, 1);
            if (ok) RefreshGrid();
            Debug.Log($"[InventoryUTK] 전리품→인벤 이동(드래그): {payload.Item.displayName} (성공={ok})");
        }

        /// <summary>③ 인벤 내 슬롯 교체(스왑) — 원본 슬롯 교체 경로와 동일한 배열 스왑.</summary>
        private bool SwapSlots(int from, int to)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return false;
            var all = inv.GetAllSlots();
            if (all == null || from < 0 || from >= all.Length || to < 0 || to >= all.Length || from == to)
                return false;
            var tmp = all[from];
            all[from] = all[to];
            all[to] = tmp;
            RefreshGrid();
            Debug.Log($"[InventoryUTK] 슬롯 교체(드래그): {from} ↔ {to}");
            return true;
        }

        /// <summary>④ 인벤 밖 드롭 = 땅에 바구니 — 원본 TryDropDraggedToTerrain(false)와 동일 API.</summary>
        private bool DropToTerrain(UTKDragPayload payload)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null || payload == null || payload.Item == null) return false;

            // 1) 커서 레이캐스트 → 지형 지점 (미스 시 플레이어 전방 폴백)
            Vector3 spawnPos;
            Camera cam = Camera.main;
            Vector2 panelPos = UTKDragDrop.LastDropPos;
            RaycastHit hit;
            if (cam != null
                && Physics.Raycast(cam.ScreenPointToRay(new Vector3(panelPos.x, cam.scaledPixelHeight - panelPos.y, 0f)),
                    out hit, 500f))
            {
                spawnPos = hit.point;
            }
            else
            {
                var playerT = GameObject.FindWithTag("Player")?.transform;
                if (playerT == null)
                {
                    Debug.Log($"[InventoryUTK] 지형 버림 취소(사유: 레이캐스트 미스 + Player 없음) — {payload.Item.displayName} 유지");
                    return false;
                }
                spawnPos = playerT.position + playerT.forward * 1.5f;
            }

            // 2) 소모: 인벤에서 1개 제거 (실패 시 롤백 — 취소)
            if (!inv.RemoveItem(payload.Item.id, 1))
            {
                Debug.Log($"[InventoryUTK] 지형 버림 취소(사유: 인벤에서 제거 실패) — {payload.Item.displayName} 유지");
                return false;
            }

            // 3) 바구니 스폰 + 1개 담기
            var basket = LootBasket.Create(spawnPos);
            if (basket == null)
            {
                Debug.LogError($"[InventoryUTK] 지형 버림 — LootBasket 생성 실패({spawnPos}), {payload.Item.displayName} 소모됨");
                return false;
            }
            basket.AddItem(payload.Item, 1);
            RefreshGrid();
            Debug.Log($"[InventoryUTK] 지형에 버림: {payload.Item.displayName} → 바구니 ({spawnPos}) — E키로 회수");
            return true;
        }

        // ─────────────────────────────── ④ 월드 백드롭 ───────────────────────────────

        private static IUTKDropTarget _worldDrop;
        private static bool _worldDropAttached;

        /// <summary>UIRoot 전체를 인벤-세계 드롭 백드롭으로 등록 (멱등).</summary>
        private void RegisterWorldDrop()
        {
            if (_worldDropAttached) return;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;

            _worldDrop = new WorldDropTarget();

            UTKDragDrop.RegisterDropTarget(root, _worldDrop);
            _worldDropAttached = true;
        }

        /// <summary>③ 슬롯 드롭 타겟 — 인벤 소스가 다른 슬롯 위 드롭 → 슬롯 교체(스왑).</summary>
        private sealed class SlotDropTarget : IUTKDropTarget
        {
            private readonly InventoryWindowUTK _owner;
            private readonly int _slotIndex;

            public SlotDropTarget(InventoryWindowUTK owner, int slotIndex)
            {
                _owner = owner;
                _slotIndex = slotIndex;
            }

            public bool CanDrop(UTKDragPayload payload)
            {
                return payload != null && payload.Item != null
                    && payload.Source == UTKDragSourceKind.Inventory
                    && payload.SourceIndex >= 0 && payload.SourceIndex != _slotIndex;
            }

            public bool Drop(UTKDragPayload payload)
                => _owner.SwapSlots(payload.SourceIndex, _slotIndex);
        }

        /// <summary>④ 월드 드롭 백드롭 — 인벤 소스가 UI 밖(월드)에 드롭 → 땅에 바구니.</summary>
        private sealed class WorldDropTarget : IUTKDropTarget
        {
            public bool CanDrop(UTKDragPayload payload)
            {
                return payload != null && payload.Item != null
                    && payload.Source == UTKDragSourceKind.Inventory;
            }

            public bool Drop(UTKDragPayload payload)
                => _instance != null && _instance.DropToTerrain(payload);
        }
    }
}