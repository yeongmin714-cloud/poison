using ProjectName.Core;
using UnityEngine;
#pragma warning disable 0414

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
        [SerializeField] private float _arrowSpeed = 70f;   // [화살-사거리2] 60→70 — 축소중력(0.22g)과 결합, 실사거리 ~80m
        [SerializeField] private Transform _arrowSpawnPoint; // 플레이어 손/활 위치

        private PlayerInventory _inventory;

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

        /// <summary>화살 1개 소모하고 발사(스폰 포인트/기본 위치에서). 실패 시 false 반환.</summary>
        public bool TryShootArrow(Vector3 direction, float baseDamage)
        {
            Vector3 origin = _arrowSpawnPoint != null
                ? _arrowSpawnPoint.position
                : transform.position + transform.forward * 0.5f + Vector3.up * 1.2f;
            return TryShootArrow(origin, direction, baseDamage);
        }

        /// <summary>화살 1개 소모하고 지정 위치(origin)에서 발사. 실패 시 false 반환.
        /// 파워 미지정(3-arg) → 파워 풀(1f) 위임 — 기존 호출 하위 호환 보장.</summary>
        public bool TryShootArrow(Vector3 origin, Vector3 direction, float baseDamage)
        {
            return TryShootArrow(origin, direction, baseDamage, 1f);
        }

        /// <summary>화살 1개 소모하고 지정 위치(origin)에서 파워 기반 발사(드로→릴리즈). 실패 시 false 반환.</summary>
        /// 파워(0~1)로 발사 속도/데미지 보정: 파워 0→속도 70%, 파워 1→속도 120%; 데미지 +power*8.
        public bool TryShootArrow(Vector3 origin, Vector3 direction, float baseDamage, float power)
        {
            if (!HasArrows())
            {
                ShowNoArrowMessage();
                return false;
            }

            // 가장 좋은 화살부터 소모 (마법 > 강화 > 일반)
            ArrowData.ArrowType consumedType = ConsumeBestArrow();
            if (consumedType == ArrowData.ArrowType.Regular && !HasArrows())
            {
                // 소모 실패 (없음)
                ShowNoArrowMessage();
                return false;
            }

            // 화살 데이터 조회
            var arrowData = new ArrowData(consumedType);

            // 발사체 생성 — origin 우선(호출부 지정 활 위치), 미지정 시 스폰 포인트/기본 위치
            Vector3 spawnPos = origin;

            // 파워 반영 — 발사 속도(0~1 파워: 70%~120%)와 데미지(+power*8) 보정
            float speed = _arrowSpeed * (0.7f + 0.5f * power);
            int totalDamage = Mathf.RoundToInt(baseDamage + arrowData.damageBonus + power * 8f);

            // [70차 후속19/A3] 발사 직후 플레이어 콜라이더 충돌 무시 — 스폰 겹침으로 화살이 튕기는 것 방지
            var proj = ArrowProjectile.Spawn(spawnPos, direction, speed, totalDamage, arrowData.trailColor);
            proj._power = power;   // [C6] 명중 시 파워 풀 크리틱 판정용
            var playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null)
            {
                var myCol = proj.GetComponent<Collider>();
                foreach (var pc in playerGo.GetComponentsInChildren<Collider>())
                    if (myCol != null && pc != null) Physics.IgnoreCollision(myCol, pc, true);
            }

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
            return ArrowData.ArrowType.Regular; // 없음 — 호출부에서 HasArrows()로 걸러짐
        }

        private bool TryConsumeArrow(string itemId)
        {
            if (_inventory == null) return false;
            return _inventory.RemoveItem(itemId, 1);
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
            if (_inventory == null || count <= 0) return;
            var data = new ArrowData(type);
            string itemId = data.GetItemId();

            var item = new PlayerInventory.ItemData
            {
                id = itemId,
                displayName = data.displayName,
                description = data.description,
                category = PlayerInventory.ItemCategory.Arrow,
                rarity = data.rarity,
                maxStack = 99,
                maxDurability = 0
            };

            _inventory.AddItem(item, count);
        }

        public void SetSpawnPoint(Transform point) => _arrowSpawnPoint = point;
    }
}
