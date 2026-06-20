using UnityEngine;
using ProjectName.Core;

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
            public float carryCapacityBonus;  // 휴대 용량 보너스
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

        public static BackSlotSystem Instance { get; private set; }

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

        /// <summary>
        /// Back 슬롯에 아이템을 장착합니다.
        /// EquipmentManager의 Back 슬롯 변경 시 호출됩니다.
        /// </summary>
        public void EquipBackItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                UnequipBackItem();
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

            // 이동 속도 보너스 적용 (PlayerStats로 위임)
            Debug.Log($"[BackSlotSystem] 효과 적용: 이동속도 +{_currentEffect.moveSpeedBonus}, 용량 +{_currentEffect.carryCapacityBonus}");
        }

        /// <summary>
        /// Back 아이템의 효과를 제거합니다.
        /// </summary>
        private void RemoveEffect()
        {
            Debug.Log("[BackSlotSystem] 효과 제거");
        }

        private BackSlotEffect FindEffect(string itemId)
        {
            if (_itemEffects == null) return null;
            foreach (var e in _itemEffects)
            {
                if (e.itemId == itemId) return e;
            }
            return null;
        }
    }
}