using System.Reflection;
using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 5.6.3: 장비 관리 싱글톤.
    /// 플레이어의 6개 장비 슬롯(헬멧/갑옷/무기/신발/장갑/Back)을 관리합니다.
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        public static EquipmentManager Instance { get; private set; }

        // 장비 슬롯 enum
        public enum EquipmentSlot
        {
            Helmet,
            Armor,
            Weapon,
            Shoes,
            Gloves,
            Back
        }

        // 장비 슬롯 데이터
        [System.Serializable]
        public class EquipmentSlotData
        {
            public string itemId;              // 장착된 아이템 ID (null/empty = 비어있음)
            public int currentDurability;      // 현재 내구도
            public PlayerInventory.ItemData itemData; // 캐시된 ItemData 참조
        }

        [SerializeField] private EquipmentSlotData[] _slots = new EquipmentSlotData[6];

        // 장비 변경 이벤트
        public System.Action<EquipmentSlot, string> OnEquipmentChanged; // (slot, newItemId)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 슬롯 초기화
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                    _slots[i] = new EquipmentSlotData();
            }
        }

        // ===== 장비 슬롯 인덱스 =====

        public static int SlotIndex(EquipmentSlot slot) => (int)slot;

        // ===== 장착 =====

        /// <summary>
        /// 인벤토리에서 아이템을 장비 슬롯에 장착합니다.
        /// </summary>
        public bool EquipItem(PlayerInventory.ItemSlot inventorySlot, EquipmentSlot slot)
        {
            if (inventorySlot == null || inventorySlot.item == null)
            {
                Debug.LogWarning("[EquipmentManager] 장착할 아이템이 없습니다.");
                return false;
            }

            int idx = SlotIndex(slot);

            // 현재 장비가 있으면 먼저 해제
            if (!string.IsNullOrEmpty(_slots[idx].itemId))
            {
                UnequipSlot(slot);
            }

            // 아이템 장착
            _slots[idx].itemId = inventorySlot.item.id;
            _slots[idx].currentDurability = inventorySlot.currentDurability;
            _slots[idx].itemData = inventorySlot.item;

            // 인벤토리에서 아이템 제거
            PlayerInventory.Instance.RemoveItem(inventorySlot.item.id, 1);

            Debug.Log($"[EquipmentManager] {slot}에 {inventorySlot.item.displayName} 장착!");

            OnEquipmentChanged?.Invoke(slot, _slots[idx].itemId);
            return true;
        }

        // ===== 해제 =====

        /// <summary>
        /// 지정된 슬롯의 장비를 해제하고 인벤토리로 반환합니다.
        /// </summary>
        public bool UnequipSlot(EquipmentSlot slot)
        {
            int idx = SlotIndex(slot);

            if (string.IsNullOrEmpty(_slots[idx].itemId))
            {
                Debug.Log($"[EquipmentManager] {slot} 슬롯이 이미 비어있습니다.");
                return false;
            }

            var itemData = _slots[idx].itemData;
            if (itemData == null)
                itemData = FindItemDataById(_slots[idx].itemId);

            if (itemData == null)
            {
                Debug.LogWarning($"[EquipmentManager] 아이템 데이터를 찾을 수 없음: {_slots[idx].itemId}");
                _slots[idx].itemId = null;
                _slots[idx].currentDurability = 0;
                return false;
            }

            // 인벤토리에 추가
            bool added = PlayerInventory.Instance.AddItem(itemData, 1);
            if (!added)
            {
                Debug.LogWarning("[EquipmentManager] 인벤토리가 가득 차서 장비를 해제할 수 없습니다.");
                return false;
            }

            // 마지막으로 추가된 슬롯의 내구도 설정
            var allSlots = PlayerInventory.Instance.GetAllSlots();
            for (int i = 0; i < allSlots.Length; i++)
            {
                if (allSlots[i] != null && allSlots[i].item.id == _slots[idx].itemId)
                {
                    allSlots[i].currentDurability = _slots[idx].currentDurability;
                    break;
                }
            }

            string oldItemId = _slots[idx].itemId;
            _slots[idx].itemId = null;
            _slots[idx].currentDurability = 0;
            _slots[idx].itemData = null;

            Debug.Log($"[EquipmentManager] {slot} 해제! 인벤토리로 반환됨.");

            OnEquipmentChanged?.Invoke(slot, null);
            return true;
        }

        // ===== 조회 =====

        /// <summary>
        /// 특정 슬롯에 장착된 아이템 정보 반환.
        /// </summary>
        public EquipmentSlotData GetSlotData(EquipmentSlot slot)
        {
            return _slots[SlotIndex(slot)];
        }

        /// <summary>
        /// 특정 슬롯에 장착된 아이템 ID 반환. 비어있으면 null.
        /// </summary>
        public string GetItemId(EquipmentSlot slot)
        {
            return _slots[SlotIndex(slot)].itemId;
        }

        /// <summary>
        /// 특정 슬롯이 비어있는지 확인.
        /// </summary>
        public bool IsSlotEmpty(EquipmentSlot slot)
        {
            return string.IsNullOrEmpty(_slots[SlotIndex(slot)].itemId);
        }

        /// <summary>
        /// 모든 슬롯 데이터 반환.
        /// </summary>
        public EquipmentSlotData[] GetAllSlots() => _slots;

        // ===== 내구도 =====

        /// <summary>
        /// 장착된 장비의 내구도를 감소시킵니다.
        /// 내구도 0이 되면 아이템이 파괴됩니다.
        /// </summary>
        public void ReduceDurability(EquipmentSlot slot, int amount = 1)
        {
            int idx = SlotIndex(slot);
            if (string.IsNullOrEmpty(_slots[idx].itemId)) return;
            if (_slots[idx].itemData == null || _slots[idx].itemData.maxDurability <= 0) return;

            _slots[idx].currentDurability -= amount;
            if (_slots[idx].currentDurability <= 0)
            {
                _slots[idx].currentDurability = 0;
                Debug.Log($"[EquipmentManager] {slot} 장비가 파괴되었습니다!");
                OnEquipmentChanged?.Invoke(slot, _slots[idx].itemId);
            }
        }

        /// <summary>
        /// 장착된 장비의 내구도 비율 반환 (0~1).
        /// </summary>
        public float GetDurabilityRatio(EquipmentSlot slot)
        {
            int idx = SlotIndex(slot);
            if (string.IsNullOrEmpty(_slots[idx].itemId)) return 0f;
            if (_slots[idx].itemData == null || _slots[idx].itemData.maxDurability <= 0) return 1f;
            return (float)_slots[idx].currentDurability / _slots[idx].itemData.maxDurability;
        }

        /// <summary>
        /// 내구도 색상 태그 반환.
        /// </summary>
        public string GetDurabilityColorTag(EquipmentSlot slot)
        {
            float ratio = GetDurabilityRatio(slot);
            if (ratio >= 0.6f) return "🟢";
            if (ratio >= 0.3f) return "🟡";
            return "🔴";
        }

        // ===== 저장/로드 =====

        /// <summary>
        /// 현재 장비 상태를 EquipmentSaveData로 변환.
        /// </summary>
        public EquipmentSaveData SaveState()
        {
            var data = new EquipmentSaveData
            {
                helmetItemId = _slots[0].itemId,
                armorItemId = _slots[1].itemId,
                weaponItemId = _slots[2].itemId,
                shoesItemId = _slots[3].itemId,
                glovesItemId = _slots[4].itemId,
                backItemId = _slots[5].itemId,
                helmetDurability = _slots[0].currentDurability,
                armorDurability = _slots[1].currentDurability,
                weaponDurability = _slots[2].currentDurability,
                shoesDurability = _slots[3].currentDurability,
                glovesDurability = _slots[4].currentDurability,
                backDurability = _slots[5].currentDurability
            };
            return data;
        }

        /// <summary>
        /// EquipmentSaveData에서 장비 상태를 복원.
        /// </summary>
        public void LoadState(EquipmentSaveData data)
        {
            if (data == null)
            {
                // 기본값으로 초기화
                for (int i = 0; i < _slots.Length; i++)
                {
                    _slots[i].itemId = null;
                    _slots[i].currentDurability = 0;
                    _slots[i].itemData = null;
                }
                return;
            }

            LoadSlot(0, data.helmetItemId, data.helmetDurability);
            LoadSlot(1, data.armorItemId, data.armorDurability);
            LoadSlot(2, data.weaponItemId, data.weaponDurability);
            LoadSlot(3, data.shoesItemId, data.shoesDurability);
            LoadSlot(4, data.glovesItemId, data.glovesDurability);
            LoadSlot(5, data.backItemId, data.backDurability);
        }

        private void LoadSlot(int idx, string itemId, int durability)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                var itemData = FindItemDataById(itemId);
                _slots[idx].itemId = itemId;
                _slots[idx].currentDurability = durability;
                _slots[idx].itemData = itemData;
            }
            else
            {
                _slots[idx].itemId = null;
                _slots[idx].currentDurability = 0;
                _slots[idx].itemData = null;
            }
        }

        // ===== 헬퍼 =====

        /// <summary>
        /// PlayerInventory의 정적 ItemData 필드에서 itemId로 ItemData를 찾습니다.
        /// </summary>
        private static PlayerInventory.ItemData FindItemDataById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            var fields = typeof(PlayerInventory).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(PlayerInventory.ItemData))
                {
                    var item = field.GetValue(null) as PlayerInventory.ItemData;
                    if (item != null && item.id == itemId)
                        return item;
                }
            }
            return null;
        }
    }
}