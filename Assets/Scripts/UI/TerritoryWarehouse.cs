using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.UI;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 5.6.2: 영지 창고 — 20슬롯.
    /// 영지 단위로 아이템을 보관/회수합니다.
    /// SaveData의 TerritoryWarehouseData / WarehouseSlotData와 연동.
    /// </summary>
    public class TerritoryWarehouse : MonoBehaviour
    {
        [Header("영지 창고 설정")]
        [SerializeField] private string _territoryId = "East_01";
        [SerializeField] private int _maxSlots = 20;
        [SerializeField] private float _interactRange = 3f;

        // 슬롯 데이터: itemId, quantity
        [System.Serializable]
        public class WarehouseSlot
        {
            public string itemId;
            public int quantity;
        }

        [SerializeField] private List<WarehouseSlot> _slots = new List<WarehouseSlot>();

        private Transform _player;
        private bool _isPlayerNearby;

        public string TerritoryId => _territoryId;
        public int SlotCount => _slots.Count;
        public int MaxSlots => _maxSlots;
        public List<WarehouseSlot> Slots => _slots;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            // 빈 슬롯 초기화
            while (_slots.Count < _maxSlots)
                _slots.Add(new WarehouseSlot());
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _isPlayerNearby = dist <= _interactRange;

            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            {
                OpenWarehouseUI();
            }
        }

        /// <summary>
        /// 창고에 아이템을 보관합니다.
        /// </summary>
        public bool DepositItem(string itemId, int count = 1)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;

            // 같은 아이템 슬롯 찾기
            foreach (var slot in _slots)
            {
                if (slot.itemId == itemId)
                {
                    slot.quantity += count;
                    Debug.Log($"[TerritoryWarehouse] {itemId} x{count} 보관 완료 (누적: {slot.quantity})");
                    return true;
                }
            }

            // 빈 슬롯 찾기
            foreach (var slot in _slots)
            {
                if (string.IsNullOrEmpty(slot.itemId))
                {
                    slot.itemId = itemId;
                    slot.quantity = count;
                    Debug.Log($"[TerritoryWarehouse] {itemId} x{count} 새 슬롯에 보관");
                    return true;
                }
            }

            Debug.LogWarning("[TerritoryWarehouse] 창고가 가득 찼습니다!");
            return false;
        }

        /// <summary>
        /// 창고에서 아이템을 회수합니다.
        /// </summary>
        public bool WithdrawItem(string itemId, int count = 1)
        {
            foreach (var slot in _slots)
            {
                if (slot.itemId == itemId && slot.quantity >= count)
                {
                    slot.quantity -= count;
                    if (slot.quantity <= 0)
                    {
                        slot.itemId = null;
                        slot.quantity = 0;
                    }
                    Debug.Log($"[TerritoryWarehouse] {itemId} x{count} 회수");
                    return true;
                }
            }

            Debug.LogWarning($"[TerritoryWarehouse] {itemId}이(가) 부족합니다.");
            return false;
        }

        /// <summary>
        /// 특정 아이템의 창고 보관량을 반환합니다.
        /// </summary>
        public int GetItemCount(string itemId)
        {
            int total = 0;
            foreach (var slot in _slots)
            {
                if (slot.itemId == itemId)
                    total += slot.quantity;
            }
            return total;
        }

        private void OpenWarehouseUI()
        {
            if (UIManager.Instance != null && UIManager.Instance.warehouseWindow != null)
            {
                UIManager.Instance.warehouseWindow.Open();
                Debug.Log($"[TerritoryWarehouse] 창고 UI 열림 (영지: {_territoryId}, 슬롯: {_slots.Count}/{_maxSlots})");
            }
            else
            {
                Debug.LogWarning("[TerritoryWarehouse] WarehouseWindow가 UIManager에 없습니다.");
            }
        }

        private void OnGUI()
        {
            if (!_isPlayerNearby) return;

            int used = 0;
            foreach (var s in _slots) { if (!string.IsNullOrEmpty(s.itemId)) used++; }

            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30),
                $"[E] 영지 창고 ({used}/{_maxSlots})");
        }
    }
}