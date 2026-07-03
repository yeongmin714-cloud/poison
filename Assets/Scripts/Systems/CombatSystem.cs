using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

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
    ///
    /// ⚠ PlayerCombat이 존재하는 경우 PlayerCombat이 자체적으로 입력을 처리합니다.
    ///   CombatSystem은 PlayerCombat이 없을 때의 Fallback 전투 시스템으로 동작합니다.
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
        [SerializeField] private bool _debugLogs = false;

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
                _playerCombat = FindAnyObjectByType<PlayerCombat>();
        }

        private void Update()
        {
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return;

            // PlayerCombat이 존재하면 PlayerCombat이 자체적으로 입력을 처리하므로
            // CombatSystem은 중복 입력 처리를 피하기 위해 건너뜁니다.
            if (_playerCombat != null) return;

            if (!Input.GetKeyDown(_attackKey)) return;

            TryPerformAttack();
        }

        // ================================================================
        // Core: 공격 시도
        // ================================================================

        /// <summary>
        /// 좌클릭 입력을 처리합니다.
        /// 1. Physics.Raycast (마우스 커서 위치, 무한 거리)
        /// 2. IDamageable 감지
        /// 3. 거리 체크 (_maxAttackRange 이내, 플레이어 기준)
        /// 4. 직접 데미지 처리 (AttackDirect)
        ///
        /// ※ PlayerCombat이 없는 Fallback 시나리오에서만 호출됩니다.
        /// </summary>
        private void TryPerformAttack()
        {
            if (_mainCamera == null) return;

            // 1) 마우스 커서 위치로 Raycast (무한 거리 — 카메라 기준 거리가 아닌 플레이어 기준 거리로 필터링)
            Vector2 mousePos = Input.mousePosition;
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            // 2) IDamageable 감지 (Raycast + SphereCast fallback, 무한 거리)
            IDamageable target = FindTargetByRaycast(ray);

            if (target == null)
                target = FindTargetBySphereCast(ray);

            if (target == null)
            {
                if (_debugLogs)
                    Debug.Log("[CombatSystem] ⚔️ 공격 실패 — 대상 없음");
                return;
            }

            // 3) 거리 체크 (플레이어 기준)
            MonoBehaviour targetBehaviour = target as MonoBehaviour;
            if (targetBehaviour == null) return;

            float distance = Vector3.Distance(transform.position, targetBehaviour.transform.position);
            if (distance > _maxAttackRange)
            {
                if (_debugLogs)
                    Debug.Log($"[CombatSystem] ⚔️ 공격 실패 — 대상이 사거리 밖 ({distance:F1}m > {_maxAttackRange}m)");
                return;
            }

            // 4) 직접 데미지 처리 (PlayerCombat이 없으므로)
            AttackDirect(target, targetBehaviour);
        }

        /// <summary>
        /// 직접 데미지 처리 (PlayerCombat이 없는 Fallback 시나리오).
        /// </summary>
        private void AttackDirect(IDamageable target, MonoBehaviour targetBehaviour)
        {
            if (!target.IsAlive) return;

            float damage = CalculateDamage();
            Vector3 hitDirection = (targetBehaviour.transform.position - transform.position).normalized;

            target.TakeDamage(damage, hitDirection, "melee");

            if (_debugLogs)
                Debug.Log($"[CombatSystem] 🎯 공격! 데미지: {damage}");

            // 사망 시 LootBasket 생성 (AnimalAI/GuardPlaceholder.Die()에서 이미 처리,
            // CombatSystem이 직접 처리한 경우를 위한 안전장치)
            if (!target.IsAlive)
            {
                EnsureLootBasket(targetBehaviour.gameObject);
            }
        }

        // ================================================================
        // Raycast 탐색
        // ================================================================

        /// <summary>정밀 Raycast — 마우스 커서 직선상의 IDamageable 탐색 (무한 거리, 플레이어 기준 필터링)</summary>
        private IDamageable FindTargetByRaycast(Ray ray)
        {
            // 무한 거리로 Raycast (카메라 기준 거리가 아닌 플레이어 기준 거리로 필터링)
            RaycastHit[] hits = Physics.RaycastAll(ray, float.PositiveInfinity, _targetLayers);

            IDamageable closest = null;
            float closestPlayerDist = float.MaxValue;

            foreach (var hit in hits)
            {
                IDamageable dmg = hit.collider.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                {
                    MonoBehaviour mb = dmg as MonoBehaviour;
                    if (mb == null) continue;
                    float playerDist = Vector3.Distance(transform.position, mb.transform.position);
                    if (playerDist < closestPlayerDist)
                    {
                        closestPlayerDist = playerDist;
                        closest = dmg;
                    }
                }
            }
            return closest;
        }

        /// <summary>SphereCast fallback — Raycast 실패 시 넓은 범위 탐색 (무한 거리)</summary>
        private IDamageable FindTargetBySphereCast(Ray ray)
        {
            float radius = 0.5f;
            RaycastHit[] hits = Physics.SphereCastAll(ray.origin, radius, ray.direction, float.PositiveInfinity, _targetLayers);

            IDamageable closest = null;
            float closestPlayerDist = float.MaxValue;

            foreach (var hit in hits)
            {
                IDamageable dmg = hit.collider.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                {
                    MonoBehaviour mb = dmg as MonoBehaviour;
                    if (mb == null) continue;
                    float playerDist = Vector3.Distance(transform.position, mb.transform.position);
                    if (playerDist < closestPlayerDist)
                    {
                        closestPlayerDist = playerDist;
                        closest = dmg;
                    }
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
            // 죽은 오브젝트 위치에 이미 LootBasket이 있는지 확인 (OverlapSphere, FindObjectsByType보다 효율적)
            Collider[] nearby = Physics.OverlapSphere(deadObject.transform.position, 0.5f);
            foreach (var col in nearby)
            {
                if (col.GetComponent<LootBasket>() != null)
                {
                    if (_debugLogs)
                        Debug.Log("[CombatSystem] ✅ LootBasket 이미 존재, 중복 생성 방지");
                    return;
                }
            }

            // 없으면 직접 생성
            LootBasket.Create(deadObject.transform.position, _basketLifetime);
            if (_debugLogs)
                Debug.Log($"[CombatSystem] 🧺 LootBasket 생성됨 ({_basketLifetime:F0}초 후 소멸)");
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        private float CalculateDamage()
        {
            // PlayerCombat이 없으므로 WeaponData를 참조할 수 없음.
            // PlayerStats 기반 기본 데미지 사용 (PlayerCombat.CalculateDamage()와 일관된 로직)
            float damage = 10f;
            if (PlayerStats.Instance != null)
                damage += PlayerStats.Instance.Level * 0.5f;
            return damage;
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