using UnityEngine;
using ProjectName.UI.Themes;
using System;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Systems;
using Game.UI.Core;
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
        private bool _warehouseOpen;   // 2026-09-11(2): 창고 UI 열림 상태 (E토글/ESC/이탈 닫기 판정)

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

        /// <summary>
        /// 런타임 AddComponent 후 외부 초기화용 (예: PlayerCastleInteriorBuilder).
        /// private [SerializeField] 필드만 설정하며, 기존 Inspector 직렬화 기본 경로는 그대로 유지됩니다.
        /// AddComponent 시점에 Awake가 이미 실행되어 슬롯 캐시가 만들어진 경우 새 _maxSlots에 맞춰 재조정합니다.
        /// </summary>
        /// <param name="territoryId">영지 고유 키 (예: "East_01")</param>
        /// <param name="maxSlots">최대 슬롯 수 (기본 20)</param>
        /// <param name="interactRange">상호작용 반경 (null이면 기존 값 유지)</param>
        public void Configure(string territoryId, int maxSlots = 20, float? interactRange = null)
        {
            if (!string.IsNullOrEmpty(territoryId))
                _territoryId = territoryId;
            if (interactRange.HasValue && interactRange.Value > 0f)
                _interactRange = interactRange.Value;

            if (maxSlots > 0 && maxSlots != _maxSlots)
            {
                _maxSlots = maxSlots;
                if (_slots.Count > 0 && _slots.Count != _maxSlots)
                {
                    _slots.Clear();
                    for (int i = 0; i < _maxSlots; i++)
                        _slots.Add(new WarehouseSlot());
                    _guiDirty = true;
                }
            }
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
            {
                _guiDirty = true;

                // 2026-09-11(2): 창고 열림 상태에서 상호작용 반경(3m) 이탈 → 자동 닫기
                if (_warehouseOpen && !_isPlayerNearby)
                {
                    CloseWarehouseUI();
                }
            }

            // 2026-09-11(2): ESC 닫기 — 열려 있으면 근접 여부 무관하게 닫는다
            if (_warehouseOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseWarehouseUI();
                return;
            }

            // 2026-09-11(2): E키 토글 — 열기/닫기 동일 키
            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            {
                if (_warehouseOpen)
                    CloseWarehouseUI();
                else
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

            // TODO: Resources.Load("Items/" + itemId) or ItemDatabase.Instance.Get(itemId)
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
            // 2026-09-09: [인벤][설명][창고] 3패널 — 인벤토리를 함께 연다
            InventoryWindow.SetContextMode(InventoryWindow.ContextMode.Warehouse);
            _warehouseOpen = true;
            _guiDirty = true;   // "[E] 닫기" 안내 전환

            if (UIManager.Instance != null && UIManager.Instance.warehouseWindow != null)
            {
                var warehouseUI = UIManager.Instance.warehouseWindow;
                if (warehouseUI != null)
                {
                    warehouseUI.gameObject.SetActive(true);
                    Debug.Log($"[TerritoryWarehouse] 창고 UI 열림 (영지: {_territoryId})");
                }
                else
                {
                    Debug.LogWarning("[TerritoryWarehouse] warehouseWindow가 null입니다.");
                }
            }
            else
            {
                // 2026-09-11(2): warehouseWindow 미연결 폴백 — SetContextMode(Warehouse)로 열린
                // 인벤 창(3패널 레이아웃)이 실제 창고 창 역할을 한다 (Test_09 경로). 정상 동작.
                Debug.Log("[TerritoryWarehouse] warehouseWindow 미연결 — 인벤 창 Warehouse 컨텍스트로 개방");
            }
        }

        /// <summary>
        /// 2026-09-11(2): 창고 UI 닫기 (E토글/ESC/반경 이탈 공용) —
        /// 인벤 창 닫기 + Warehouse 컨텍스트 해제 + 진행 중 드래그 정리.
        /// </summary>
        private void CloseWarehouseUI()
        {
            if (!_warehouseOpen) return;
            _warehouseOpen = false;
            _guiDirty = true;   // "[E] 영지 창고" 안내 복원

            // 드래그 중이던 아이템 컨텍스트 정리 (고스트 잔상 방지)
            ItemDragContext.Cancel();

            // 실제로 열리는 창은 SetContextMode(Warehouse)로 열린 인벤 창 — 컨텍스트 해제 + 창 닫기
            InventoryWindow.CloseContext();

            // warehouseWindow가 있으면 함께 닫기 (Test_09에선 null 가능)
            if (UIManager.Instance != null && UIManager.Instance.warehouseWindow != null)
                UIManager.Instance.warehouseWindow.Hide();

            Debug.Log($"[TerritoryWarehouse] 창고 UI 닫힘 (영지: {_territoryId})");
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
                _cachedGuiLabel = _warehouseOpen
                    ? $"[E] 닫기 / ESC — 영지 창고 ({used}/{_maxSlots})"
                    : $"[E] 영지 창고 ({used}/{_maxSlots})";
                _guiLabelRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30);
                _guiDirty = false;
            }

            GUI.Label(_guiLabelRect, _cachedGuiLabel);
        }
    }
}