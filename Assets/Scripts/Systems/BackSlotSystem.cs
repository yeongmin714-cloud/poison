using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 5.6.3: Back 슬롯 시스템.
    /// 플레이어 등에 장착하는 아이템(가방, 망토, 가스 분사기 등)을 관리.
    /// EquipmentManager의 Back 슬롯과 연동하여 효과를 적용합니다.
    /// </summary>
    public class BackSlotSystem : MonoBehaviour
    {
        [System.Serializable]
        public class BackSlotEffect
        {
            public string itemId;
            public string displayName;
            public BackSlotType slotType;
            public float moveSpeedBonus;      // 이동 속도 보너스
            public float carryCapacityBonus;  // 휴대 용량 보너스 (PlayerInventory 슬롯 증가)
            public string description;
        }

        public enum BackSlotType
        {
            None,
            Backpack,       // 가방 — 용량 증가
            Cloak,          // 망토 — 속도/은신
            GasSprayer,     // 가스 분사기 — C8 전용
            Quiver,         // 화살통 — 원거리 무기
            Trophy          // 전리품 — 장식
        }

        [Header("Back 슬롯 설정")]
        [SerializeField] private BackSlotEffect _currentEffect;

        [Header("사전 정의된 Back 아이템 효과")]
        [SerializeField] private BackSlotEffect[] _itemEffects;

        public BackSlotEffect CurrentEffect => _currentEffect;
        public bool HasBackItem => _currentEffect != null && !string.IsNullOrEmpty(_currentEffect.itemId);

        /// <summary>
        /// 현재 적용 중인 휴대 용량 보너스. PlayerInventory 등에서 참조 가능.
        /// PlayerInventory 확장 시 이 값을 활용하여 _maxSlots에 반영하십시오.
        /// </summary>
        public float CurrentCarryCapacityBonus { get; private set; }

        public static BackSlotSystem Instance { get; private set; }

        // ── 이전 상태 추적 (스택 방지) ──
        private float _previousMoveSpeedBase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // EquipmentManager의 Back 슬롯 변경 이벤트 구독
            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.OnEquipmentChanged += HandleEquipmentChanged;
            }
            else
            {
                Debug.LogWarning("[BackSlotSystem] EquipmentManager.Instance is null. 이벤트 구독 실패.");
            }
        }

        private void OnDestroy()
        {
            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.OnEquipmentChanged -= HandleEquipmentChanged;
            }
        }

        /// <summary>
        /// EquipmentManager의 장비 변경 이벤트 핸들러.
        /// Back 슬롯 변경 시에만 반응합니다.
        /// </summary>
        private void HandleEquipmentChanged(EquipmentManager.EquipmentSlot slot, string newItemId)
        {
            if (slot != EquipmentManager.EquipmentSlot.Back) return;
            EquipBackItem(newItemId);
        }

        /// <summary>
        /// Back 슬롯에 아이템을 장착합니다.
        /// EquipmentManager의 Back 슬롯 변경 시 자동 호출됩니다.
        /// 직접 호출도 가능합니다.
        /// </summary>
        public void EquipBackItem(string itemId)
        {
            // 기존 효과가 있다면 먼저 제거 (스택 방지)
            if (_currentEffect != null)
            {
                RemoveEffect();
            }

            if (string.IsNullOrEmpty(itemId))
            {
                _currentEffect = null;
                Debug.Log("[BackSlotSystem] Back 슬롯 비어있음");
                return;
            }

            var effect = FindEffect(itemId);
            if (effect != null)
            {
                _currentEffect = effect;
                ApplyEffect();
                Debug.Log($"[BackSlotSystem] 🎒 {effect.displayName} 장착! (효과: 속도+{effect.moveSpeedBonus}, 용량+{effect.carryCapacityBonus})");
            }
            else
            {
                _currentEffect = new BackSlotEffect
                {
                    itemId = itemId,
                    displayName = "알 수 없는 Back 아이템",
                    slotType = BackSlotType.None
                };
                Debug.LogWarning($"[BackSlotSystem] {itemId}의 효과 정의를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// Back 슬롯을 해제합니다.
        /// </summary>
        public void UnequipBackItem()
        {
            if (_currentEffect == null) return;

            RemoveEffect();

            string oldName = _currentEffect.displayName;
            _currentEffect = null;
            Debug.Log($"[BackSlotSystem] {oldName} 해제됨");
        }

        /// <summary>
        /// Back 아이템의 효과를 플레이어 스탯에 적용합니다.
        /// </summary>
        private void ApplyEffect()
        {
            if (PlayerStats.Instance == null || _currentEffect == null) return;

            // 이동 속도 보너스 저장 및 적용
            _previousMoveSpeedBase = PlayerStats.Instance.MoveSpeedBase;
            PlayerStats.Instance.MoveSpeedBase += _currentEffect.moveSpeedBonus;

            // 휴대 용량 보너스 저장
            CurrentCarryCapacityBonus = _currentEffect.carryCapacityBonus;

            Debug.Log($"[BackSlotSystem] 효과 적용: 이동속도 {_previousMoveSpeedBase:F1} → {PlayerStats.Instance.MoveSpeedBase:F1} (+{_currentEffect.moveSpeedBonus}), 용량 +{_currentEffect.carryCapacityBonus}");
        }

        /// <summary>
        /// Back 아이템의 효과를 제거합니다.
        /// </summary>
        private void RemoveEffect()
        {
            if (PlayerStats.Instance == null || _currentEffect == null) return;

            // 이동 속도 복원
            PlayerStats.Instance.MoveSpeedBase = _previousMoveSpeedBase;

            // 휴대 용량 보너스 초기화
            CurrentCarryCapacityBonus = 0f;
            _previousMoveSpeedBase = PlayerStats.Instance.MoveSpeedBase;

            Debug.Log("[BackSlotSystem] 효과 제거 완료");
        }

        /// <summary>
        /// _itemEffects 배열에서 itemId와 일치하는 BackSlotEffect를 찾습니다.
        /// </summary>
        private BackSlotEffect FindEffect(string itemId)
        {
            if (_itemEffects == null) return null;
            foreach (var e in _itemEffects)
            {
                // null 요소 방어
                if (e == null) continue;
                if (e.itemId == itemId) return e;
            }
            return null;
        }
    }
}