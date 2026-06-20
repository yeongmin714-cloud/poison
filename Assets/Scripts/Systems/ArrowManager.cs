using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// AB-02/03: 화살 소모 관리자.
    /// 활 공격 시 인벤토리에서 화살을 소모하고,
    /// 부족 시 공격을 막고 메시지를 표시합니다.
    /// </summary>
    public class ArrowManager : MonoBehaviour
    {
        public static ArrowManager Instance { get; private set; }

        [Header("화살 발사 설정")]
        [SerializeField] private float _arrowSpeed = 30f;
        [SerializeField] private Transform _arrowSpawnPoint; // 플레이어 손/활 위치

        private PlayerInventory _inventory;
        private int _lastArrowCount = -1;

        // 화살 아이템 ID
        private const string ARROW_REGULAR_ID = "arrow_regular";
        private const string ARROW_REINFORCED_ID = "arrow_reinforced";
        private const string ARROW_MAGIC_ID = "arrow_magic";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _inventory = PlayerInventory.Instance;
        }

        /// <summary>활 공격 가능 여부 (화살 1개 이상 소지)</summary>
        public bool HasArrows()
        {
            if (_inventory == null) return false;
            return GetTotalArrowCount() > 0;
        }

        /// <summary>화살 1개 소모하고 발사. 실패 시 false 반환.</summary>
        public bool TryShootArrow(Vector3 direction, float baseDamage)
        {
            if (!HasArrows())
            {
                ShowNoArrowMessage();
                return false;
            }

            // 가장 좋은 화살부터 소모 (마법 > 강화 > 일반)
            ArrowData.ArrowType consumedType = ConsumeBestArrow();
            if (consumedType == ArrowData.ArrowType.Regular && GetTotalArrowCount() == 0)
            {
                // 소모 실패 (없음)
                ShowNoArrowMessage();
                return false;
            }

            // 화살 데이터 조회
            var arrowData = new ArrowData(consumedType);

            // 발사체 생성
            Vector3 spawnPos = _arrowSpawnPoint != null
                ? _arrowSpawnPoint.position
                : transform.position + transform.forward * 0.5f + Vector3.up * 1.2f;

            int totalDamage = Mathf.RoundToInt(baseDamage + arrowData.damageBonus);

            ArrowProjectile.Spawn(spawnPos, direction, _arrowSpeed, totalDamage, arrowData.trailColor);

            // UI 업데이트
            _lastArrowCount = GetTotalArrowCount();
            return true;
        }

        /// <summary>전체 화살 개수</summary>
        public int GetTotalArrowCount()
        {
            if (_inventory == null) return 0;
            int count = 0;
            foreach (var slot in _inventory.GetAllSlots())
            {
                if (slot == null || slot.item == null) continue;
                if (slot.item.id == ARROW_REGULAR_ID ||
                    slot.item.id == ARROW_REINFORCED_ID ||
                    slot.item.id == ARROW_MAGIC_ID)
                {
                    count += slot.count;
                }
            }
            return count;
        }

        /// <summary>가장 좋은 화살 1개 소모</summary>
        private ArrowData.ArrowType ConsumeBestArrow()
        {
            // 우선순위: 마법 > 강화 > 일반
            if (TryConsumeArrow(ARROW_MAGIC_ID))
                return ArrowData.ArrowType.Magic;
            if (TryConsumeArrow(ARROW_REINFORCED_ID))
                return ArrowData.ArrowType.Reinforced;
            if (TryConsumeArrow(ARROW_REGULAR_ID))
                return ArrowData.ArrowType.Regular;
            return ArrowData.ArrowType.Regular; // 없음
        }

        private bool TryConsumeArrow(string itemId)
        {
            if (_inventory == null) return false;
            var slots = _inventory.GetAllSlots();
            foreach (var slot in slots)
            {
                if (slot == null || slot.item == null) continue;
                if (slot.item.id == itemId && slot.count > 0)
                {
                    slot.count--;
                    if (slot.count <= 0)
                    {
                        _inventory.RemoveItem(itemId, 1);
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>화살 부족 메시지</summary>
        private void ShowNoArrowMessage()
        {
            Debug.Log("[ArrowManager] 화살이 부족합니다!");
        }

        /// <summary>
        /// 플레이어 인벤토리에 화살 추가 (AB-07 연동용)
        /// </summary>
        public void AddArrows(ArrowData.ArrowType type, int count)
        {
            if (_inventory == null) return;
            var data = new ArrowData(type);
            string itemId = data.GetItemId();

            // 기존 아이템이 있으면 개수 추가
            var slots = _inventory.GetAllSlots();
            foreach (var slot in slots)
            {
                if (slot == null || slot.item == null) continue;
                if (slot.item.id == itemId)
                {
                    slot.count += count;
                    return;
                }
            }

            // 새 아이템 생성
            var item = new PlayerInventory.ItemData
            {
                id = itemId,
                displayName = data.displayName,
                description = data.description,
                category = PlayerInventory.ItemCategory.Weapon,
                rarity = data.rarity,
                maxStack = 99,
                maxDurability = 0
            };

            _inventory.AddItem(item, count);
        }

        public void SetSpawnPoint(Transform point) => _arrowSpawnPoint = point;
    }
}
