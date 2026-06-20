using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// G2-07: 간소화된 공격 시스템.
    ///
    /// 좌클릭 입력 → Physics.Raycast → IDamageable 감지 → 거리 체크 → 공격 → 사망 시 LootBasket 생성.
    ///
    /// 동작 방식:
    /// 1. 매 프레임 좌클릭(Input.GetMouseButtonDown(0)) 확인
    /// 2. 메인 카메라 → 마우스 커서 방향으로 Raycast
    /// 3. IDamageable 인터페이스를 가진 첫 번째 타겟 탐색
    /// 4. 근접(2m) / 원거리(10m) 사거리 판단
    /// 5. PlayerCombat이 있으면 PlayerCombat의 Attack 호출, 없으면 직접 IDamageable.TakeDamage()
    /// 6. 타겟 사망 시 LootBasket.Create() + DropTable 기반 아이템 채우기
    /// 7. LootBasket은 자체 30초 소멸 타이머 사용
    /// </summary>
    public class AttackSystem : MonoBehaviour
    {
        [Header("Combat Settings")]
        [SerializeField] private LayerMask _targetLayers = -1;
        [SerializeField] private float _meleeRange = 2f;
        [SerializeField] private float _rangedRange = 10f;
        [SerializeField] private float _baseDamage = 10f;
        [SerializeField] private float _basketLifetime = 30f;

        [Header("Input")]
        [SerializeField] private KeyCode _attackKey = KeyCode.Mouse0;

        [Header("Debug")]
        [SerializeField] private bool _debugLogs = true;

        // Cache
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
            // 플레이어 사망 시 행동 불가
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)
                return;

            // 좌클릭 확인
            if (!Input.GetMouseButtonDown(0))
                return;

            TryPerformAttack();
        }

        // ================================================================
        // Core: 공격 수행
        // ================================================================

        /// <summary>
        /// 좌클릭 입력 처리: Raycast → IDamageable 탐색 → 거리 체크 → 공격
        /// </summary>
        private void TryPerformAttack()
        {
            if (_mainCamera == null)
            {
                Debug.LogWarning("[AttackSystem] 메인 카메라가 없습니다.");
                return;
            }

            // 1) 마우스 커서 위치 → Ray 생성
            Vector3 mousePos = Input.mousePosition;
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            // 2) Raycast로 IDamageable 탐색
            IDamageable target = FindTargetByRaycast(ray);

            if (target == null)
            {
                // SphereCast fallback — 정밀 Raycast 실패 시 넓은 범위 탐색
                target = FindTargetBySphereCast(ray);
            }

            if (target == null)
            {
                if (_debugLogs)
                    Debug.Log("[AttackSystem] ⚔️ 공격 실패 — 대상 없음");
                return;
            }

            // 3) 거리 체크 (근접/원거리 판단)
            MonoBehaviour targetBehaviour = target as MonoBehaviour;
            if (targetBehaviour == null) return;

            float distance = Vector3.Distance(transform.position, targetBehaviour.transform.position);

            if (distance > _rangedRange)
            {
                if (_debugLogs)
                    Debug.Log($"[AttackSystem] ⚔️ 공격 실패 — 대상이 원거리 사거리 밖 ({distance:F1}m > {_rangedRange}m)");
                return;
            }

            // 4) 공격 타입 결정 및 HIT
            string weaponType = distance <= _meleeRange ? "melee" : "ranged";
            ApplyDamage(target, targetBehaviour, weaponType);
        }

        /// <summary>
        /// 타겟에 데미지를 적용합니다.
        /// PlayerCombat이 있으면 PlayerCombat 경유, 없으면 직접 IDamageable.TakeDamage() 호출.
        /// 사망 시 LootBasket을 생성합니다.
        /// </summary>
        private void ApplyDamage(IDamageable target, MonoBehaviour targetBehaviour, string weaponType)
        {
            if (!target.IsAlive) return;

            // 데미지 계산
            float damage = CalculateDamage(weaponType);
            Vector3 hitDirection = (targetBehaviour.transform.position - transform.position).normalized;

            // PlayerCombat이 있으면 사운드/이펙트를 재사용하고자 하지만
            // AttackTarget은 private이므로 직접 IDamageable.TakeDamage() 호출
            // (CombatSystem과 동일 패턴)
            target.TakeDamage(damage, hitDirection, weaponType);

            if (_playerCombat != null)
            {
                // PlayerCombat의 공격 이펙트/사운드를 재활용하는 확장 지점
                if (_debugLogs)
                    Debug.Log($"[AttackSystem] PlayerCombat 연동됨 — 데미지: {damage}");
            }

            if (_debugLogs)
                Debug.Log($"[AttackSystem] 🎯 {weaponType} 공격! 데미지: {damage} (거리: {Vector3.Distance(transform.position, targetBehaviour.transform.position):F1}m)");

            // 사망 처리
            if (!target.IsAlive)
            {
                HandleTargetDeath(targetBehaviour);
            }
        }

        /// <summary>
        /// 타겟 사망 시 LootBasket을 생성하고 DropTable 기반 아이템을 채웁니다.
        /// </summary>
        private void HandleTargetDeath(MonoBehaviour targetBehaviour)
        {
            // 이미 LootBasket이 있는지 확인 (중복 생성 방지)
            if (IsLootBasketNearby(targetBehaviour.transform.position))
            {
                if (_debugLogs)
                    Debug.Log("[AttackSystem] ✅ LootBasket 이미 존재, 중복 생성 방지");
                return;
            }

            // LootBasket 생성 (30초 자동 소멸)
            LootBasket basket = LootBasket.Create(targetBehaviour.transform.position, _basketLifetime);

            if (basket == null)
            {
                Debug.LogError("[AttackSystem] LootBasket 생성 실패!");
                return;
            }

            // 드랍 테이블에서 아이템 생성
            PopulateBasket(targetBehaviour.gameObject, basket);

            if (_debugLogs)
                Debug.Log($"[AttackSystem] 🧺 LootBasket 생성 완료 (30초 후 소멸)");
        }

        /// <summary>
        /// 적 타입(몬스터/병사)에 따라 드랍 테이블에서 아이템을 생성하여 바구니에 채웁니다.
        /// </summary>
        private void PopulateBasket(GameObject deadObject, LootBasket basket)
        {
            // AnimalAI(몬스터)인지 확인
            AnimalAI animal = deadObject.GetComponent<AnimalAI>();
            if (animal != null)
            {
                // 몬스터 드랍
                List<KeyValuePair<string, int>> drops = DropTableUtility.GetMonsterDrops(animal.Tier);
                foreach (var drop in drops)
                {
                    // ItemData를 조회하여 LootBasket에 추가
                    PlayerInventory.ItemData itemData = FindItemById(drop.Key);
                    if (itemData != null)
                    {
                        basket.AddItem(itemData, drop.Value);
                    }
                }

                if (_debugLogs)
                    Debug.Log($"[AttackSystem] 📦 몬스터 드랍: {drops.Count}종");
                return;
            }

            // Guard(병사)인지 확인 — 태그 기반 체크
            if (deadObject.CompareTag("Guard"))
            {
                int guardLevel = ExtractGuardLevel(deadObject);
                List<KeyValuePair<string, int>> drops = DropTableUtility.GetGuardDrops(guardLevel);
                foreach (var drop in drops)
                {
                    PlayerInventory.ItemData itemData = FindItemById(drop.Key);
                    if (itemData != null)
                    {
                        basket.AddItem(itemData, drop.Value);
                    }
                }

                if (_debugLogs)
                    Debug.Log($"[AttackSystem] 📦 병사 드랍: {drops.Count}종 (레벨 {guardLevel})");
                return;
            }

            // 알 수 없는 타입 — 기본 드랍
            if (_debugLogs)
                Debug.LogWarning($"[AttackSystem] ⚠️ 알 수 없는 적 타입: {deadObject.name}, 기본 드랍 없음");
        }

        // ================================================================
        // Raycast 탐색
        // ================================================================

        /// <summary>정밀 Raycast — 마우스 커서 직선상의 IDamageable 탐색</summary>
        private IDamageable FindTargetByRaycast(Ray ray)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, _rangedRange, _targetLayers);

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

        /// <summary>SphereCast fallback — 넓은 범위 탐색</summary>
        private IDamageable FindTargetBySphereCast(Ray ray)
        {
            float radius = 0.5f;
            RaycastHit[] hits = Physics.SphereCastAll(ray.origin, radius, ray.direction, _rangedRange, _targetLayers);

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
        // Helpers
        // ================================================================

        /// <summary>데미지 계산 (무기 타입에 따라 보정)</summary>
        private float CalculateDamage(string weaponType)
        {
            float damage = _baseDamage;

            // PlayerStats 레벨 보정
            if (PlayerStats.Instance != null)
                damage += PlayerStats.Instance.Level * 0.5f;

            // 원거리 약간 보정
            if (weaponType == "ranged")
                damage *= 0.8f;

            return Mathf.Max(1f, damage);
        }

        /// <summary>주변에 LootBasket이 있는지 확인 (중복 생성 방지)</summary>
        private bool IsLootBasketNearby(Vector3 position)
        {
            LootBasket[] existing = FindObjectsByType<LootBasket>(FindObjectsSortMode.None);
            foreach (var basket in existing)
            {
                if (Vector3.Distance(basket.transform.position, position) < 0.5f)
                    return true;
            }
            return false;
        }

        /// <summary>아이템 ID로 PlayerInventory.ItemData 조회</summary>
        private PlayerInventory.ItemData FindItemById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            // Resources/Items/ 경로에서 로드 시도
            PlayerInventory.ItemData loaded = null; // placeholder - replaced from Resources.Load
            if (loaded != null) return loaded;

            // 폴백: 기본 ItemData 생성 (테스트/디버그용)
            return new PlayerInventory.ItemData
            {
                id = itemId,
                displayName = itemId,
                description = $"드랍 아이템: {itemId}",
                category = PlayerInventory.ItemCategory.Material,
                maxStack = 99,
                rarity = ItemRarity.Common
            };
        }

        /// <summary>병사 오브젝트에서 레벨 추출 (기본값 1)</summary>
        private int ExtractGuardLevel(GameObject guardObject)
        {
            // GuardManager 또는 기타 컴포넌트에서 레벨 읽기
            // 없으면 기본값 1
            return 1;
        }

        // ================================================================
        // Editor Gizmos
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            // 근접 사거리 (초록)
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _meleeRange);

            // 원거리 사거리 (빨강)
            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, _rangedRange);
        }
    }
}