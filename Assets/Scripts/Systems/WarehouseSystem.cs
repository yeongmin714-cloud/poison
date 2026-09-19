using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 영지 창고 시스템 — 영지별 20슬롯 창고 + 슬롯 확장 체계 [Phase O2 C-O2-03b].
    /// 확장: 영지별 단계 0~MaxExpansions(3), 단계당 +SlotsPerExpansion(5)슬롯,
    /// 비용 BaseExpansionCost(100) × (현재단계+1) → 100/200/300G (골드 유출구).
    /// SaveData 연동 포함 (확장 단계 직렬화 — 구형 세이브는 미기록=0 호환).
    /// </summary>
    public class WarehouseSystem : MonoBehaviour
    {
        public static WarehouseSystem Instance { get; private set; }

        // 기본 용량 (확장 단계 0). 사용처는 GetSlotCapacity(territoryId)로 교체 — 하드코드 직접 참조 금지.
        [SerializeField] private int _maxSlotsPerTerritory = 20;

        // [C-O2-03b] 확장 상수 — 단계당 +5슬롯, 비용표 100→200→300G.
        public const int MaxExpansions = 3;
        public const int SlotsPerExpansion = 5;
        public const int BaseExpansionCost = 100;
        /// <summary>확장 지출 원장 태그 (PlayerStats.SpendGold source — EconomyAuditSystem 원장)</summary>
        public const string ExpansionGoldSource = "warehouse_expansion";

        // [C-O2-03b] territoryId → 확장 단계 (0=기본, 최대 MaxExpansions). SaveData 직렬화 대상.
        private Dictionary<string, int> _expansionLevels = new Dictionary<string, int>();

        // 영지별 폭탄 시딩 1회 마킹 (세션당 영지마다 최초 1회만 — 중복 추가/무한 생성 방지)
        private static readonly System.Collections.Generic.HashSet<string> _seededTerritories =
            new System.Collections.Generic.HashSet<string>();

        // territoryId → 슬롯 리스트
        private Dictionary<string, List<PlayerInventory.ItemSlot>> _warehouses = new Dictionary<string, List<PlayerInventory.ItemSlot>>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SeedDefaultBombs("East_01"); // 기본 영지 창고에 폭탄 1개 시딩 (멱등)
        }

        /// <summary>
        /// 영지 창고에 폭탄 1개를 시딩한다. 멱등 보장:
        ///  - 이미 해당 영지가 이 세션에서 시딩된 적 있으면 건너뜀 (무한 재추가 방지)
        ///  - 시딩 시점 창고에 Bomb이 이미 있으면 건너뜀 (저장 데이터 유지)
        /// </summary>
        public void SeedDefaultBombs(string territoryId)
        {
            if (string.IsNullOrEmpty(territoryId)) territoryId = "default";
            if (_seededTerritories.Contains(territoryId)) return;

            _seededTerritories.Add(territoryId);
            var slots = GetOrCreateWarehouse(territoryId);

            // 이미 폭탄이 있으면 건너뜀 (복원된 저장 데이터 존중)
            foreach (var slot in slots)
            {
                if (slot != null && slot.item != null && slot.item.isBomb)
                    return;
            }

            AddItem(territoryId, PlayerInventory.Bomb_Explosive, 1);
            Debug.Log($"[WarehouseSystem] 🎁 {territoryId} 창고에 폭탄 1개 시딩 완료");
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

            // 빈 슬롯에 추가 [C-O2-03b] 하드코드(_maxSlotsPerTerritory) → 확장 반영 용량
            while (count > 0 && slots.Count < GetSlotCapacity(territoryId))
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
            return GetOrCreateWarehouse(territoryId).Count >= GetSlotCapacity(territoryId);
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
                    slots = new List<PlayerInventory.ItemSlot>(kvp.Value),
                    expansionLevel = GetExpansionLevel(kvp.Key)
                };
                data.warehouseData.Add(entry);
            }
            return data;
        }

        /// <summary>SaveData에서 복원</summary>
        public void LoadFromSaveData(WarehouseSaveData data)
        {
            _warehouses.Clear();
            _expansionLevels.Clear();
            if (data == null || data.warehouseData == null) return;

            foreach (var entry in data.warehouseData)
            {
                if (!string.IsNullOrEmpty(entry.territoryId))
                {
                    _warehouses[entry.territoryId] = new List<PlayerInventory.ItemSlot>(entry.slots ?? new List<PlayerInventory.ItemSlot>());
                    // [C-O2-03b] 확장 단계 복원 — 구형 세이브는 0(기본값) 호환
                    if (entry.expansionLevel > 0)
                        _expansionLevels[entry.territoryId] = Mathf.Clamp(entry.expansionLevel, 0, MaxExpansions);
                }
            }
        }

        public void Clear()
        {
            _warehouses.Clear();
            _expansionLevels.Clear();
        }

        // ================================================================
        //  슬롯 확장 [Phase O2 C-O2-03b] — 골드 유출구
        // ================================================================

        /// <summary>영지의 현재 슬롯 용량 (기본 20 + 확장 단계당 +5).</summary>
        public int GetSlotCapacity(string territoryId)
        {
            return _maxSlotsPerTerritory + SlotsPerExpansion * GetExpansionLevel(territoryId);
        }

        /// <summary>영지의 확장 단계 (0~MaxExpansions, 미기록 영지=0).</summary>
        public int GetExpansionLevel(string territoryId)
        {
            if (string.IsNullOrEmpty(territoryId)) return 0;
            return _expansionLevels.TryGetValue(territoryId, out int level) ? level : 0;
        }

        /// <summary>다음 확장 비용 (현재단계+1 배 — 100/200/300G).</summary>
        public int GetNextExpansionCost(string territoryId)
        {
            return BaseExpansionCost * (GetExpansionLevel(territoryId) + 1);
        }

        /// <summary>더 확장 가능한가?</summary>
        public bool CanExpand(string territoryId)
        {
            return GetExpansionLevel(territoryId) < MaxExpansions;
        }

        /// <summary>
        /// 슬롯 확장 실행 — PlayerStats 지갑에서 SpendGold(원장 태그 warehouse_expansion).
        /// 반환: 결과 메시지 (UI SetStatus용).
        /// </summary>
        public string TryExpandSlots(string territoryId)
        {
            if (!CanExpand(territoryId))
                return "이미 최대 확장";

            // [O3 C-O3-03] 확장 단계별 레벨 게이트 — 0→1: 게이트 없음 / 1→2: Lv12 / 2→3: Lv20.
            int nextLevel = GetExpansionLevel(territoryId) + 1;
            string gateId = null;
            if (nextLevel >= 3)
                gateId = "warehouse_expansion_3";
            else if (nextLevel >= 2)
                gateId = "warehouse_expansion_2";

            if (gateId != null)
            {
                int level = PlayerStats.Instance?.Level ?? 1;
                if (!ContentGate.IsUnlocked(gateId, level))
                {
                    var gate = ContentGate.GetGateInfo(gateId);
                    int requiredLevel = gate.HasValue ? gate.Value.minLevel : 0;
                    return $"레벨 부족 (필요 Lv.{requiredLevel})";
                }
            }

            int cost = GetNextExpansionCost(territoryId);
            if (PlayerStats.Instance == null)
                return "골드 지갑 접근 실패";

            if (!PlayerStats.Instance.SpendGold(cost, ExpansionGoldSource))
                return $"골드 부족 (필요 {cost}G)";

            _expansionLevels[territoryId] = GetExpansionLevel(territoryId) + 1;
            return $"창고 확장! 슬롯 +{SlotsPerExpansion} (현재 {GetSlotCapacity(territoryId)})";
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
        /// <summary>[C-O2-03b] 슬롯 확장 단계 (구형 세이브 미기록=0 호환)</summary>
        public int expansionLevel = 0;
    }
}