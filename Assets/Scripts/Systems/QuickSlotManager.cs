using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 🔢 퀵슬롯 데이터 매니저 — 6개 슬롯의 아이템 데이터를 저장/관리.
    /// MonoBehaviour 싱글톤. PlayerPrefs로 게임 재시작 시 복원.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class QuickSlotManager : MonoBehaviour
    {
        public static QuickSlotManager Instance { get; private set; }

        private const int SLOT_COUNT = 6;
        private const string PREFS_KEY = "QuickSlot_";

        /// <summary>
        /// 퀵슬롯 하나의 데이터
        /// </summary>
        public class SlotData
        {
            public string itemId;
            public PlayerInventory.ItemData itemData;
        }

        private SlotData[] _slots;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _slots = new SlotData[SLOT_COUNT];
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                _slots[i] = new SlotData();
            }

            LoadFromPlayerPrefs();
        }

        /// <summary>
        /// 특정 슬롯에 아이템 등록 (이미 있으면 덮어쓰기)
        /// </summary>
        public void SetSlot(int index, PlayerInventory.ItemData itemData)
        {
            if (index < 0 || index >= SLOT_COUNT)
            {
                Debug.LogWarning($"[QuickSlotManager] SetSlot: index {index} out of range.");
                return;
            }

            if (itemData == null)
            {
                Debug.LogWarning("[QuickSlotManager] SetSlot: itemData is null.");
                return;
            }

            _slots[index].itemId = itemData.id;
            _slots[index].itemData = itemData;
            SaveSlotToPrefs(index);
            Debug.Log($"[QuickSlotManager] 슬롯 {index + 1}에 '{itemData.displayName}' 등록됨");
        }

        /// <summary>
        /// 특정 슬롯 비우기
        /// </summary>
        public void ClearSlot(int index)
        {
            if (index < 0 || index >= SLOT_COUNT)
            {
                Debug.LogWarning($"[QuickSlotManager] ClearSlot: index {index} out of range.");
                return;
            }

            _slots[index].itemId = null;
            _slots[index].itemData = null;
            SaveSlotToPrefs(index);
        }

        /// <summary>
        /// 모든 슬롯 비우기
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                _slots[i].itemId = null;
                _slots[i].itemData = null;
                PlayerPrefs.DeleteKey(PREFS_KEY + i);
            }
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 해당 슬롯에 아이템이 있는지 확인
        /// </summary>
        public bool HasItemInSlot(int index)
        {
            if (index < 0 || index >= SLOT_COUNT) return false;
            var slot = _slots[index];
            return slot != null && !string.IsNullOrEmpty(slot.itemId) && slot.itemData != null;
        }

        /// <summary>
        /// 해당 슬롯의 ItemData 반환 (없으면 null)
        /// </summary>
        public PlayerInventory.ItemData GetItemInSlot(int index)
        {
            if (index < 0 || index >= SLOT_COUNT) return null;
            var slot = _slots[index];
            if (slot == null || string.IsNullOrEmpty(slot.itemId)) return null;
            return slot.itemData;
        }

        /// <summary>
        /// 해당 슬롯의 itemId 반환 (없으면 null)
        /// </summary>
        public string GetItemIdInSlot(int index)
        {
            if (index < 0 || index >= SLOT_COUNT) return null;
            var slot = _slots[index];
            if (slot == null) return null;
            return slot.itemId;
        }

        /// <summary>
        /// 전체 슬롯 배열 반환 (UI용)
        /// </summary>
        public SlotData[] GetAllSlots() => _slots;

        /// <summary>
        /// 슬롯 총 개수
        /// </summary>
        public int SlotCount => SLOT_COUNT;

        // ===== PlayerPrefs 저장/로드 =====

        private void SaveSlotToPrefs(int index)
        {
            if (index < 0 || index >= SLOT_COUNT) return;
            string key = PREFS_KEY + index;
            string itemId = _slots[index]?.itemId;
            if (!string.IsNullOrEmpty(itemId))
            {
                PlayerPrefs.SetString(key, itemId);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
        }

        private void LoadFromPlayerPrefs()
        {
            if (_slots == null) return;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                string savedId = PlayerPrefs.GetString(PREFS_KEY + i, "");
                if (!string.IsNullOrEmpty(savedId))
                {
                    _slots[i].itemId = savedId;
                    // 인벤토리에서 해당 itemId로 ItemData 찾기
                    _slots[i].itemData = FindItemDataById(savedId);
                }
            }
        }

        /// <summary>
        /// 인벤토리 전체에서 itemId로 ItemData 찾기
        /// </summary>
        private PlayerInventory.ItemData FindItemDataById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (PlayerInventory.Instance == null) return null;

            var allSlots = PlayerInventory.Instance.GetAllSlots();
            if (allSlots == null) return null;

            foreach (var slot in allSlots)
            {
                if (slot != null && slot.item != null && slot.item.id == itemId)
                {
                    return slot.item;
                }
            }

            // 인벤토리에 없어도 정적 아이템 데이터에서 찾기
            // PlayerInventory의 정적 ItemData 필드들 확인
            return FindStaticItemData(itemId);
        }

        /// <summary>
        /// PlayerInventory 정적 필드에서 itemId로 ItemData 찾기
        /// </summary>
        private static PlayerInventory.ItemData FindStaticItemData(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            // 모든 정적 아이템 데이터 검사
            var staticFields = typeof(PlayerInventory).GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.GetField);

            foreach (var field in staticFields)
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
