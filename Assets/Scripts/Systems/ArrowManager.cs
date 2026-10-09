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
            BowTrajectoryPreview.Kill();
            _inventory = PlayerInventory.Instance;
        }

        /// <summary>Late-created player inventories are resolved on demand (Test_10 creates its player after this manager).</summary>
        private PlayerInventory ResolveInventory()
        {
            if (_inventory == null) _inventory = PlayerInventory.Instance;
            return _inventory;
        }

        /// <summary>활 공격 가능 여부 (화살 1개 이상 소지)</summary>
        public bool HasArrows()
        {
            return GetTotalArrowCount() > 0;
        }

        /// <summary>Raw screen-space point shared by the bow reticle and shot solver.</summary>
        public static Vector2 GetAimScreenPoint()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        /// <summary>Configured bow muzzle when present, otherwise the caller's existing fallback origin.</summary>
        public Vector3 GetArrowSpawnOrigin(Vector3 fallbackOrigin)
        {
            return _arrowSpawnPoint != null ? _arrowSpawnPoint.position : fallbackOrigin;
        }

        /// <summary>
        /// Resolve the reticle's screen ray into a diagnostic world aim point. The normalized camera
        /// ray itself defines launch direction; collider hits and muzzle parallax never bend the shot.
        /// The shooter hierarchy is skipped when selecting the diagnostic hit point.
        /// </summary>
        public static bool TrySolveAim(Camera camera, Vector2 aimScreenPoint, Vector3 muzzleOrigin,
            Transform shooter, out Vector3 aimPoint, out Vector3 muzzleDirection)
        {
            aimPoint = muzzleOrigin;
            muzzleDirection = Vector3.zero;
            if (camera == null || !camera.isActiveAndEnabled || !camera.gameObject.activeInHierarchy)
                return false;

            // Never clamp: a reticle sample outside the active screen/camera viewport denotes
            // a coordinate-space mismatch and must cancel rather than silently choose another ray.
            if (aimScreenPoint.x < 0f || aimScreenPoint.x > Screen.width
                || aimScreenPoint.y < 0f || aimScreenPoint.y > Screen.height
                || !camera.pixelRect.Contains(aimScreenPoint))
                return false;

            Ray ray = camera.ScreenPointToRay(aimScreenPoint);
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool foundWorldPoint = false;
            RaycastHit acceptedHit = default;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].collider != null ? hits[i].collider.transform : null;
                if (shooter != null && hitTransform != null && hitTransform.IsChildOf(shooter))
                    continue;
                aimPoint = hits[i].point;
                acceptedHit = hits[i];
                foundWorldPoint = true;
                break;
            }

            // An unobstructed reticle still has a stable world-space target on its camera ray.
            if (!foundWorldPoint) aimPoint = ray.GetPoint(100f);
            muzzleDirection = ray.direction.normalized;
            Debug.LogWarning($"[BowAim] screen={aimScreenPoint} camera={camera.name} rayO={ray.origin} rayD={ray.direction} "
                + $"hit={(foundWorldPoint ? acceptedHit.collider.name : "<none; ray+100m>")} "
                + $"point={aimPoint} muzzle={muzzleOrigin} dir={muzzleDirection}");
            return true;
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
            return TryShootArrow(origin, direction, baseDamage, 1f, out _);
        }

        /// <summary>기존 bool API — 발사체 참조가 필요 없는 호출부의 호환 경로.</summary>
        public bool TryShootArrow(Vector3 origin, Vector3 direction, float baseDamage, float power)
        {
            return TryShootArrow(origin, direction, baseDamage, power, out _);
        }

        /// <summary>화살 1개 소모하고 지정 위치(origin)에서 파워 기반 발사(드로→릴리즈). 실패 시 false 반환.</summary>
        /// 파워(0~1)로 발사 속도/데미지 보정: 파워 0→속도 70%, 파워 1→속도 120%; 데미지 +power*8.
        public bool TryShootArrow(Vector3 origin, Vector3 direction, float baseDamage, float power,
            out ArrowProjectile projectile)
        {
            projectile = null;
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

            // 파워 반영 — 기존 최소/최대 배율(70%~120%) 유지. 비행 튜닝은 Projectile이 단일 제공.
            float speed = ArrowProjectile.GetSpeedForPower(power);
            int totalDamage = Mathf.RoundToInt(baseDamage + arrowData.damageBonus + power * 8f);

            // [70차 후속19/A3] 발사 직후 플레이어 콜라이더 충돌 무시 — 스폰 겹침으로 화살이 튕기는 것 방지
            projectile = ArrowProjectile.Spawn(spawnPos, direction, speed, totalDamage, arrowData.trailColor);
            projectile._power = power;   // [C6] 명중 시 파워 풀 크리틱 판정용
            projectile.SetArrowData(arrowData);   // [C 고품질] 3티어 파라미터(관통/발광/스파크) 주입
            var playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null)
            {
                var myCol = projectile.GetComponent<Collider>();
                foreach (var pc in playerGo.GetComponentsInChildren<Collider>())
                    if (myCol != null && pc != null) Physics.IgnoreCollision(myCol, pc, true);
            }

            return true;
        }

        /// <summary>전체 화살 개수</summary>
        public int GetTotalArrowCount()
        {
            var inventory = ResolveInventory();
            if (inventory == null) return 0;
            int count = 0;
            foreach (var slot in inventory.GetAllSlots())
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

        /// <summary>[F 고품질] 다음 발사 시 소모될 화살 타입(우선순위: 마법>강화>일반) — 리티클 표시용.</summary>
        public ArrowData.ArrowType GetNextArrowType()
        {
            if (CountOf(ARROW_MAGIC_ID) > 0) return ArrowData.ArrowType.Magic;
            if (CountOf(ARROW_REINFORCED_ID) > 0) return ArrowData.ArrowType.Reinforced;
            return ArrowData.ArrowType.Regular;
        }

        /// <summary>[F 고품질] 특정 화살 종류 보유 개수.</summary>
        public int CountOf(string itemId)
        {
            var inventory = ResolveInventory();
            if (inventory == null) return 0;
            int n = 0;
            foreach (var slot in inventory.GetAllSlots())
            {
                if (slot == null || slot.item == null || slot.item.id != itemId) continue;
                n += slot.count;
            }
            return n;
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
            var inventory = ResolveInventory();
            if (inventory == null) return false;
            return inventory.RemoveItem(itemId, 1);
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
            var inventory = ResolveInventory();
            if (inventory == null || count <= 0) return;
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

            inventory.AddItem(item, count);
        }

        public void SetSpawnPoint(Transform point) => _arrowSpawnPoint = point;
    }
}
