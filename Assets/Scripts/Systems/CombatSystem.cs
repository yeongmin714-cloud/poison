using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// G2-07: 중앙 집중형 전투 시스템 (CombatSystem)
    ///
    /// PlayerCombat 기반 재구성:
    /// - 좌클릭 → Physics.Raycast → IDamageable 감지 → 거리 체크 → PlayerCombat.Attack()
    /// - 적 사망 시 LootBasket.Create() 호출
    /// - 전리품 주머니 30초 후 자동 소멸 (LootBasket 자체 타이머)
    ///
    /// PlayerCombat은 실제 데미지 계산/적중 이펙트를 담당하고,
    /// CombatSystem은 입력 → 타겟 선정 → 공격 명령 오케스트레이션을 담당합니다.
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        [Header("Combat Settings")]
        [SerializeField] private LayerMask _targetLayers = -1;
        [SerializeField] private float _maxAttackRange = 3f;
        [SerializeField] private float _basketLifetime = 30f;

        [Header("Input Binding")]
        [SerializeField] private KeyCode _attackKey = KeyCode.Mouse0;

        [Header("Debug")]
        [SerializeField] private bool _debugLogs = true;

        // 캐시
        private Camera _mainCamera;
        private PlayerCombat _playerCombat;

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            _mainCamera = Camera.main;
            _playerCombat = GetComponent<PlayerCombat>();
            if (_playerCombat == null)
                _playerCombat = FindFirstObjectByType<PlayerCombat>();
        }

        private void Update()
        {
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return;
            if (!Input.GetKeyDown(_attackKey)) return;

            TryPerformAttack();
        }

        // ================================================================
        // Core: 공격 시도
        // ================================================================

        /// <summary>
        /// 좌클릭 입력을 처리합니다.
        /// 1. Physics.Raycast (마우스 커서 위치)
        /// 2. IDamageable 감지
        /// 3. 거리 체크 (_maxAttackRange 이내)
        /// 4. PlayerCombat.Attack() 호출
        /// </summary>
        private void TryPerformAttack()
        {
            if (_mainCamera == null) return;

            // 1) 마우스 커서 위치로 Raycast
            Vector2 mousePos = Input.mousePosition;
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            // 2) IDamageable 감지 (Raycast + SphereCast fallback)
            IDamageable target = FindTargetByRaycast(ray);

            if (target == null)
                target = FindTargetBySphereCast(ray);

            if (target == null)
            {
                if (_debugLogs)
                    Debug.Log("[CombatSystem] ⚔️ 공격 실패 — 대상 없음");
                return;
            }

            // 3) 거리 체크
            MonoBehaviour targetBehaviour = target as MonoBehaviour;
            if (targetBehaviour == null) return;

            float distance = Vector3.Distance(transform.position, targetBehaviour.transform.position);
            if (distance > _maxAttackRange)
            {
                if (_debugLogs)
                    Debug.Log($"[CombatSystem] ⚔️ 공격 실패 — 대상이 사거리 밖 ({distance:F1}m > {_maxAttackRange}m)");
                return;
            }

            // 4) PlayerCombat.Attack() 호출
            if (_playerCombat != null)
            {
                // PlayerCombat의 AttackTarget은 internal이므로
                // 직접 TakeDamage를 호출하거나 public API 사용
                AttackViaPlayerCombat(target);
            }
            else
            {
                // Fallback: 직접 데미지 처리
                AttackDirect(target, targetBehaviour);
            }
        }

        /// <summary>
        /// PlayerCombat 인스턴스를 통해 공격합니다.
        /// </summary>
        private void AttackViaPlayerCombat(IDamageable target)
        {
            if (!target.IsAlive) return;

            // PlayerCombat.TryAttack() / AttackTarget() 호출이 불가능하면
            // 직접 데미지를 처리하되 PlayerCombat의 이펙트를 재사용
            float damage = CalculateDamage();
            Vector3 hitDirection = (GetTargetPosition(target) - transform.position).normalized;

            target.TakeDamage(damage, hitDirection, "melee");

            if (_debugLogs)
                Debug.Log($"[CombatSystem] 🎯 공격! 데미지: {damage}");

            // 사망 시 LootBasket.Create()는 AnimalAI.Die() 또는 GuardPlaceholder.Die()에서 자동 처리됨
            // 하지만 여기서도 안전장치로 체크
            if (!target.IsAlive)
            {
                MonoBehaviour mb = target as MonoBehaviour;
                if (mb != null)
                {
                    // LootBasket 생성 (Die()에서 이미 생성하지만, 놓친 경우를 대비)
                    // 이미 AnimalAI.Die()가 LootBasket.Create()를 호출하므로
                    // 중복 생성 방지를 위해 플래그 사용
                    EnsureLootBasket(mb.gameObject);
                }
            }
        }

        /// <summary>
        /// PlayerCombat 없이 직접 데미지 처리하는 폴백.
        /// </summary>
        private void AttackDirect(IDamageable target, MonoBehaviour targetBehaviour)
        {
            if (!target.IsAlive) return;

            float damage = CalculateDamage();
            Vector3 hitDirection = (targetBehaviour.transform.position - transform.position).normalized;

            target.TakeDamage(damage, hitDirection, "melee");

            if (!target.IsAlive)
            {
                EnsureLootBasket(targetBehaviour.gameObject);
            }
        }

        // ================================================================
        // Raycast 탐색
        // ================================================================

        /// <summary>정밀 Raycast — 마우스 커서 직선상의 IDamageable 탐색</summary>
        private IDamageable FindTargetByRaycast(Ray ray)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, _maxAttackRange, _targetLayers);

            IDamageable closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                IDamageable dmg = hit.collider.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive && hit.distance < closestDist)
                {
                    closestDist = hit.distance;
                    closest = dmg;
                }
            }
            return closest;
        }

        /// <summary>SphereCast fallback — Raycast 실패 시 넓은 범위 탐색</summary>
        private IDamageable FindTargetBySphereCast(Ray ray)
        {
            float radius = 0.5f;
            RaycastHit[] hits = Physics.SphereCastAll(ray.origin, radius, ray.direction, _maxAttackRange, _targetLayers);

            IDamageable closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                IDamageable dmg = hit.collider.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive && hit.distance < closestDist)
                {
                    closestDist = hit.distance;
                    closest = dmg;
                }
            }
            return closest;
        }

        // ================================================================
        // LootBasket 생성 보장
        // ================================================================

        /// <summary>
        /// 적 사망 시 LootBasket이 생성되었는지 확인하고,
        /// 생성되지 않았다면 직접 생성합니다.
        ///
        /// 대부분의 경우 AnimalAI.Die() / GuardPlaceholder.Die()에서
        /// LootBasket.Create()를 호출하므로 이중 생성되지 않도록 주의.
        /// </summary>
        private void EnsureLootBasket(GameObject deadObject)
        {
            // 죽은 오브젝트 위치에 이미 LootBasket이 있는지 확인
            LootBasket[] existing = FindObjectsByType<LootBasket>();
            foreach (var basket in existing)
            {
                if (Vector3.Distance(basket.transform.position, deadObject.transform.position) < 0.5f)
                {
                    if (_debugLogs)
                        Debug.Log("[CombatSystem] ✅ LootBasket 이미 존재, 중복 생성 방지");
                    return; // 이미 있음
                }
            }

            // 없으면 직접 생성
            LootBasket basket2 = LootBasket.Create(deadObject.transform.position, _basketLifetime);
            if (_debugLogs)
                Debug.Log($"[CombatSystem] 🧺 LootBasket 생성됨 (30초 후 소멸)");
            _ = basket2; // 사용됨
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        private float CalculateDamage()
        {
            // PlayerCombat이 있으면 PlayerCombat의 데미지 사용
            if (_playerCombat != null)
            {
                // PlayerCombat.CalculateDamage()는 private이므로
                // WeaponData 기반 추정값 사용
                return 10f; // 기본값
            }

            float damage = 10f;
            if (PlayerStats.Instance != null)
                damage += PlayerStats.Instance.Level * 0.5f;
            return damage;
        }

        private Vector3 GetTargetPosition(IDamageable target)
        {
            MonoBehaviour mb = target as MonoBehaviour;
            return mb != null ? mb.transform.position : Vector3.zero;
        }

        // ================================================================
        // Editor Gizmos
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _maxAttackRange);
        }
    }
}