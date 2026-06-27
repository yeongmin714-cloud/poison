using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 영지 창고 시스템 — 영지별 20슬롯 창고.
    /// SaveData 연동 포함.
    /// </summary>
    public class WarehouseSystem : MonoBehaviour
    {
        public static WarehouseSystem Instance { get; private set; }

        [SerializeField] private int _maxSlotsPerTerritory = 20;

        // territoryId → 슬롯 리스트
        private Dictionary<string, List<PlayerInventory.ItemSlot>> _warehouses = new Dictionary<string, List<PlayerInventory.ItemSlot>>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ================================================================
        //  Core API
        // ================================================================

        /// <summary>해당 영지 창고에 아이템 추가</summary>
        public bool AddItem(string territoryId, PlayerInventory.ItemData item, int count = 1)
        {
            var slots = GetOrCreateWarehouse(territoryId);
            if (item == null || count <= 0) return false;

            // 같은 아이템 스택 먼저 찾기
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].item != null && slots[i].item.id == item.id && slots[i].count < item.maxStack)
                {
                    int space = item.maxStack - slots[i].count;
                    int add = Mathf.Min(space, count);
                    slots[i].count += add;
                    count -= add;
                    if (count <= 0) return true;
                }
            }

            // 빈 슬롯에 추가
            while (count > 0 && slots.Count < _maxSlotsPerTerritory)
            {
                int add = Mathf.Min(count, item.maxStack);
                slots.Add(new PlayerInventory.ItemSlot { item = item, count = add, currentDurability = item.maxDurability });
                count -= add;
            }

            return count <= 0; // false = 창고 가득 참
        }

        /// <summary>해당 영지 창고에서 아이템 제거</summary>
        public bool RemoveItem(string territoryId, int slotIndex, int count = 1)
        {
            var slots = GetOrCreateWarehouse(territoryId);
            if (slotIndex < 0 || slotIndex >= slots.Count) return false;

            var slot = slots[slotIndex];
            if (slot.item == null || slot.count < count) return false;

            slot.count -= count;
            if (slot.count <= 0)
            {
                slots.RemoveAt(slotIndex);
            }
            return true;
        }

        /// <summary>플레이어 인벤토리로 아이템 이동</summary>
        public bool TransferToInventory(string territoryId, int slotIndex, int count = 1)
        {
            var slots = GetOrCreateWarehouse(territoryId);
            if (slotIndex < 0 || slotIndex >= slots.Count) return false;

            var slot = slots[slotIndex];
            if (slot.item == null || slot.count < count) return false;

            if (PlayerInventory.Instance == null) return false;

            bool added = PlayerInventory.Instance.AddItem(slot.item, count);
            if (!added) return false;

            slot.count -= count;
            if (slot.count <= 0)
                slots.RemoveAt(slotIndex);

            return true;
        }

        /// <summary>해당 영지 창고 아이템 목록 조회 (방어적 복사 — 슬롯 단위 딥카피)</summary>
        public List<PlayerInventory.ItemSlot> GetItems(string territoryId)
        {
            var source = GetOrCreateWarehouse(territoryId);
            var copy = new List<PlayerInventory.ItemSlot>(source.Count);
            foreach (var slot in source)
            {
                copy.Add(new PlayerInventory.ItemSlot
                {
                    item = slot.item,
                    count = slot.count,
                    currentDurability = slot.currentDurability
                });
            }
            return copy;
        }

        /// <summary>총 아이템 종류 수</summary>
        public int GetItemCount(string territoryId)
        {
            return GetOrCreateWarehouse(territoryId).Count;
        }

        /// <summary>창고가 가득 찼는지</summary>
        public bool IsFull(string territoryId)
        {
            return GetOrCreateWarehouse(territoryId).Count >= _maxSlotsPerTerritory;
        }

        // ================================================================
        //  Save/Load
        // ================================================================

        /// <summary>SaveData에 저장할 데이터로 변환</summary>
        public WarehouseSaveData GetSaveData()
        {
            var data = new WarehouseSaveData();
            data.warehouseData = new List<WarehouseSaveEntry>();
            foreach (var kvp in _warehouses)
            {
                var entry = new WarehouseSaveEntry
                {
                    territoryId = kvp.Key,
                    slots = new List<PlayerInventory.ItemSlot>(kvp.Value)
                };
                data.warehouseData.Add(entry);
            }
            return data;
        }

        /// <summary>SaveData에서 복원</summary>
        public void LoadFromSaveData(WarehouseSaveData data)
        {
            _warehouses.Clear();
            if (data == null || data.warehouseData == null) return;

            foreach (var entry in data.warehouseData)
            {
                if (!string.IsNullOrEmpty(entry.territoryId))
                {
                    _warehouses[entry.territoryId] = new List<PlayerInventory.ItemSlot>(entry.slots ?? new List<PlayerInventory.ItemSlot>());
                }
            }
        }

        public void Clear()
        {
            _warehouses.Clear();
        }

        // ================================================================
        //  Internal
        // ================================================================

        private List<PlayerInventory.ItemSlot> GetOrCreateWarehouse(string territoryId)
        {
            if (string.IsNullOrEmpty(territoryId))
                territoryId = "default";

            if (!_warehouses.TryGetValue(territoryId, out var slots))
            {
                slots = new List<PlayerInventory.ItemSlot>();
                _warehouses[territoryId] = slots;
            }
            return slots;
        }
    }

    // ================================================================
    //  Save Data
    // ================================================================

    [System.Serializable]
    public class WarehouseSaveData
    {
        public List<WarehouseSaveEntry> warehouseData = new List<WarehouseSaveEntry>();
    }

    [System.Serializable]
    public class WarehouseSaveEntry
    {
        public string territoryId;
        public List<PlayerInventory.ItemSlot> slots = new List<PlayerInventory.ItemSlot>();
    }
}