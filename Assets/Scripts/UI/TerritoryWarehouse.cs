using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 5.6.2: 영지 창고 — 20슬롯.
    /// Scene 상의 창고 오브젝트 근접 상호작용 처리.
    /// 실제 데이터 저장/관리는 WarehouseSystem이 담당 (SaveData.warehouse 연동).
    /// </summary>
    public class TerritoryWarehouse : MonoBehaviour
    {
        [Header("영지 창고 설정")]
        [SerializeField] private string _territoryId = "East_01";
        [SerializeField] private int _maxSlots = 20;
        [SerializeField] private float _interactRange = 3f;

        // 슬롯 데이터 (표시용 캐시 — 실제 저장은 WarehouseSystem)
        [System.Serializable]
        public class WarehouseSlot
        {
            public string itemId;
            public int quantity;
        }

        [NonSerialized] private List<WarehouseSlot> _slots = new List<WarehouseSlot>();

        private Transform _player;
        private bool _isPlayerNearby;

        // OnGUI GC 방지: 캐시
        private bool _guiDirty = true;
        private string _cachedGuiLabel = "";
        private Rect _guiLabelRect;
        private System.Collections.ObjectModel.ReadOnlyCollection<WarehouseSlot> _cachedReadOnly;

        public string TerritoryId => _territoryId;
        public int SlotCount => _slots.Count;
        public int MaxSlots => _maxSlots;
        public IReadOnlyList<WarehouseSlot> Slots => _cachedReadOnly ??= _slots.AsReadOnly();

        private void Awake()
        {
            // 슬롯 배열 초기화 (Inspector 직렬화 무시)
            _slots.Clear();
            for (int i = 0; i < _maxSlots; i++)
                _slots.Add(new WarehouseSlot());
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            SyncFromWarehouseSystem();
        }

        private void Update()
        {
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (_player == null) return;
            }

            // Vector3.Distance → sqrMagnitude (Sqrt 제거 성능 최적화)
            float sqrDist = (transform.position - _player.position).sqrMagnitude;
            float rangeSqr = _interactRange * _interactRange;
            bool wasNearby = _isPlayerNearby;
            _isPlayerNearby = sqrDist <= rangeSqr;

            if (_isPlayerNearby != wasNearby)
                _guiDirty = true;

            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            {
                OpenWarehouseUI();
            }
        }

        /// <summary>
        /// WarehouseSystem의 데이터를 _slots 캐시에 동기화.
        /// </summary>
        private void SyncFromWarehouseSystem()
        {
            if (WarehouseSystem.Instance == null) return;

            var items = WarehouseSystem.Instance.GetItems(_territoryId);
            _slots.Clear();
            for (int i = 0; i < _maxSlots; i++)
            {
                if (i < items.Count && items[i] != null && items[i].item != null)
                {
                    _slots.Add(new WarehouseSlot
                    {
                        itemId = items[i].item.id,
                        quantity = items[i].count
                    });
                }
                else
                {
                    _slots.Add(new WarehouseSlot());
                }
            }
            _guiDirty = true;
        }

        /// <summary>
        /// 아이템 ID → ItemData 변환 (WarehouseSystem API 호환).
        /// 실제 프로젝트에서는 Resources.Load / ItemDatabase 조회로 대체 필요.
        /// </summary>
        private static PlayerInventory.ItemData ResolveItemData(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            // TODO: Resources.Load("Items/" + itemId) 또는 ItemDatabase.Instance.Get(itemId)
            return new PlayerInventory.ItemData
            {
                id = itemId,
                displayName = itemId,
                maxStack = 99,
                category = PlayerInventory.ItemCategory.Material,
                rarity = ItemRarity.Common
            };
        }

        /// <summary>
        /// 창고에 아이템을 보관합니다. (WarehouseSystem 위임)
        /// </summary>
        public bool DepositItem(string itemId, int count = 1)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (WarehouseSystem.Instance == null) return false;

            var itemData = ResolveItemData(itemId);
            if (itemData == null) return false;

            bool result = WarehouseSystem.Instance.AddItem(_territoryId, itemData, count);
            if (result)
            {
                SyncFromWarehouseSystem();
                Debug.Log($"[TerritoryWarehouse] {itemId} x{count} 보관 완료");
            }
            else
            {
                Debug.LogWarning("[TerritoryWarehouse] 창고가 가득 찼습니다!");
            }
            return result;
        }

        /// <summary>
        /// 창고에서 아이템을 회수합니다. (WarehouseSystem 위임)
        /// </summary>
        public bool WithdrawItem(string itemId, int count = 1)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (WarehouseSystem.Instance == null) return false;

            var slots = WarehouseSystem.Instance.GetItems(_territoryId);
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].item != null && slots[i].item.id == itemId && slots[i].count >= count)
                {
                    bool result = WarehouseSystem.Instance.TransferToInventory(_territoryId, i, count);
                    if (result)
                    {
                        SyncFromWarehouseSystem();
                        Debug.Log($"[TerritoryWarehouse] {itemId} x{count} 회수");
                        return true;
                    }
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
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].itemId == itemId)
                    total += _slots[i].quantity;
            }
            return total;
        }

        private void OpenWarehouseUI()
        {
            if (UIManager.Instance != null && UIManager.Instance.warehouseWindow != null)
            {
                var wui = UIManager.Instance.warehouseWindow;
                // UI에 현재 영지 ID 설정 (기본값 "default" 대신)
                if (wui is WarehouseUI warehouseUI)
                {
                    warehouseUI.SetTerritory(_territoryId);
                    wui.Open();
                    Debug.Log($"[TerritoryWarehouse] 창고 UI 열림 (영지: {_territoryId})");
                }
                else
                {
                    Debug.LogWarning("[TerritoryWarehouse] warehouseWindow가 WarehouseUI 타입이 아닙니다.");
                }
            }
            else
            {
                Debug.LogWarning("[TerritoryWarehouse] WarehouseWindow가 UIManager에 없습니다.");
            }
        }

        private void OnGUI()
        {
            if (!_isPlayerNearby) return;

            if (_guiDirty)
            {
                int used = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_slots[i].itemId))
                        used++;
                }
                _cachedGuiLabel = $"[E] 영지 창고 ({used}/{_maxSlots})";
                _guiLabelRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30);
                _guiDirty = false;
            }

            GUI.Label(_guiLabelRect, _cachedGuiLabel);
        }
    }
}
