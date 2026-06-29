using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 34: 암살 시스템.
    /// 은신 상태 + NPC 뒤에서 좌클릭 → 즉사 판정.
    /// 영주/보스: 3배 데미지 + BuffManager "Bleeding" 버프.
    /// 암살 모션 0.5초 (Time.timeScale 고정).
    /// 암살 발각 시 주변 NPC/몬스터 적대.
    /// </summary>
    public class StealthAssassination : MonoBehaviour
    {
        public static StealthAssassination Instance { get; private set; }

        /// <summary>암살 수행 중인지 여부 (전역 읽기 전용)</summary>
        public static bool IsPerformingAssassination { get; private set; }

        [Header("Assassination Settings")]
        [SerializeField] private float _assassinationRange = 2.5f;
        [SerializeField] private float _behindAngleThreshold = 30f; // 뒤에서 ±30도 이내
        [SerializeField] private float _assassinationDuration = 0.5f;
        [SerializeField] private float _bossDamageMultiplier = 3f;
        [SerializeField] private float _bleedDamagePerSecond = 5f;
        [SerializeField] private float _bleedDuration = 5f;
        [SerializeField] private float _alertRadius = 10f;

        [Header("Layer Settings")]
        [SerializeField] private LayerMask _targetLayers = -1;

        // 상태
        private bool _isAssassinating = false;
        private Camera _mainCamera;
        private float _timeScaleBeforeAssassination = 1f;

        // 캐시
        private StealthSystem _stealthSystem;
        private PlayerMovement _playerMovement;

        // ===== Public Properties =====
        [System.Obsolete("Use IsPerformingAssassination instead")]
        public bool IsAssassinating => _isAssassinating;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            _stealthSystem = GetComponent<StealthSystem>();
            if (_stealthSystem == null)
                _stealthSystem = FindFirstObjectByType<StealthSystem>();

            _playerMovement = GetComponent<PlayerMovement>();
            if (_playerMovement == null)
                _playerMovement = FindFirstObjectByType<PlayerMovement>();
        }

        private void Update()
        {
            // 은신 상태 + 암살 중이 아닐 때만 좌클릭 감지
            if (_stealthSystem == null || !_stealthSystem.IsStealthed || _isAssassinating)
                return;

            // 플레이어 사망 시 행동 불가
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)
                return;

            // 좌클릭 감지 (Input System)
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryAssassinationFromMouse();
            }

            // E 키로도 암살 가능 (편의)
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                TryAssassinationFromMouse();
            }
        }

        /// <summary>
        /// 퍼블릭 암살 시도 메서드. 외부에서 직접 호출 가능 (GuardPlaceholder 등).
        /// 은신 상태 + target 뒤에서만 실행됩니다.
        /// </summary>
        /// <param name="target">암살 대상 GameObject</param>
        /// <returns>암살이 실행되었으면 true</returns>
        public bool TryAssassinate(GameObject target)
        {
            if (_stealthSystem == null || !_stealthSystem.IsStealthed || _isAssassinating)
                return false;

            if (target == null) return false;

            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)
                return false;

            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
                return false;

            // 거리 체크
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > _assassinationRange) return false;

            // 뒤에서 접근 확인
            if (!IsBehindTarget(target.transform))
            {
                Debug.Log("[StealthAssassination] ⚠️ 타겟 뒤에서만 암살 가능!");
                return false;
            }

            // 암살 실행
            StartCoroutine(ExecuteAssassination(damageable, target));
            return true;
        }

        /// <summary>
        /// 마우스 커서 방향으로 암살 시도.
        /// </summary>
        private void TryAssassinationFromMouse()
        {
            if (_mainCamera == null) return;

            // 마우스 커서 방향 Raycast
            Vector2 mousePos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Ray ray = _mainCamera.ScreenPointToRay(mousePos);
            IDamageable target = FindTargetByRaycast(ray);

            if (target == null)
            {
                // SphereCast fallback
                target = FindTargetBySphereCast(ray);
            }

            if (target == null) return;

            MonoBehaviour targetBehaviour = target as MonoBehaviour;
            if (targetBehaviour == null) return;

            TryAssassinate(targetBehaviour.gameObject);
        }

        /// <summary>
        /// 플레이어가 타겟 뒤에 있는지 확인.
        /// </summary>
        private bool IsBehindTarget(Transform targetTransform)
        {
            Vector3 dirToPlayer = (transform.position - targetTransform.position).normalized;
            float dot = Vector3.Dot(targetTransform.forward, dirToPlayer);
            // dot > 0 = 타겟 정면, dot < 0 = 타겟 뒤
            float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
            return angle > (180f - _behindAngleThreshold);
        }

        /// <summary>
        /// 암살 실행 코루틴. 0.5초 Time.timeScale 고정 + VFX.
        /// </summary>
        private IEnumerator ExecuteAssassination(IDamageable target, GameObject targetObject)
        {
            _isAssassinating = true;
            IsPerformingAssassination = true;

            // Time.timeScale 고정 (0.5초 일시정지 효과)
            _timeScaleBeforeAssassination = Time.timeScale;
            Time.timeScale = 0f;

            // 플레이어를 타겟 방향으로 회전
            Vector3 dirToTarget = (targetObject.transform.position - transform.position).normalized;
            dirToTarget.y = 0f;
            if (dirToTarget.magnitude > 0.1f)
                transform.rotation = Quaternion.LookRotation(dirToTarget);

            // 0.5초 대기 (realtimeSinceStartup 사용 — Time.timeScale=0 영향 없음)
            float timer = 0f;
            float startTime = Time.realtimeSinceStartup;
            while (timer < _assassinationDuration)
            {
                timer = Time.realtimeSinceStartup - startTime;
                yield return null;
            }

            // Time.timeScale 복원
            Time.timeScale = _timeScaleBeforeAssassination;

            // 타겟이 죽었거나 파괴되었는지 다시 확인
            if (target == null || !target.IsAlive || targetObject == null)
            {
                _isAssassinating = false;
                IsPerformingAssassination = false;
                yield break;
            }

            Vector3 targetPos = targetObject.transform.position;

            // 암살 VFX
            CombatVFXController.PlayAssassinationVFX(targetPos);
            CombatVFXController.SpawnBloodSplatter(targetPos, (targetPos - transform.position).normalized);
            CombatVFXController.PlayHitFlash(targetObject);

            // 암살 판정
            bool isBoss = targetObject.CompareTag("Boss") || targetObject.CompareTag("Lord");
            bool isInstantKill = !isBoss;

            if (isInstantKill)
            {
                // 즉사: 최대 HP만큼 데미지
                float maxHP = GetMaxHP(targetObject);
                target.TakeDamage(maxHP + 999f, (targetObject.transform.position - transform.position).normalized, "assassination");

                // 데미지 폰트
                CombatVFXController.ShowDamageNumber(targetPos, Mathf.RoundToInt(maxHP), Color.red);

                // 사운드
                SoundManager.Instance?.PlaySFX("assassination_kill");

                Debug.Log($"[StealthAssassination] 🗡️ 암살 성공! {targetObject.name} 즉사!");
            }
            else
            {
                // 보스/영주: 3배 데미지 + BuffManager 출혈
                float damage = CalculateBaseDamage() * _bossDamageMultiplier;
                target.TakeDamage(damage, (targetObject.transform.position - transform.position).normalized, "assassination");

                // 데미지 폰트
                CombatVFXController.ShowDamageNumber(targetPos, Mathf.RoundToInt(damage), Color.red);

                // BuffManager "Bleeding" 버프 (5초, 초당 5 데미지)
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.AddBuff("Bleeding", _bleedDamagePerSecond, _bleedDuration);
                }

                // 사운드
                SoundManager.Instance?.PlaySFX("assassination_boss_hit");

                Debug.Log($"[StealthAssassination] 🗡️ 보스 암살! {targetObject.name} 데미지 x3 + 출혈!");
            }

            // 암살 발각 → 주변 NPC/몬스터 Alerted 상태로 전환
            AlertNearbyNPCs(targetPos);
            MonsterAggroSystem.SetAlertAllNearby(targetPos, _alertRadius);

            // 은신 해제
            if (_stealthSystem != null)
                _stealthSystem.ForceExitStealth();

            // 암살 종료
            yield return new WaitForSecondsRealtime(0.2f);
            _isAssassinating = false;
            IsPerformingAssassination = false;
        }

        /// <summary>
        /// 암살 발각 시 주변 NPC를 적대 상태로 전환.
        /// </summary>
        private void AlertNearbyNPCs(Vector3 position)
        {
            NPCAwarenessSystem[] npcs = FindObjectsByType<NPCAwarenessSystem>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                if (!npc.IsActive) continue;
                float dist = Vector3.Distance(npc.transform.position, position);
                if (dist <= _alertRadius)
                {
                    npc.SetDetected(gameObject);
                }
            }
        }

        /// <summary>
        /// Raycast로 IDamageable 타겟 탐색.
        /// </summary>
        private IDamageable FindTargetByRaycast(Ray ray)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, _assassinationRange, _targetLayers))
            {
                IDamageable dmg = hit.collider.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                    return dmg;
            }
            return null;
        }

        /// <summary>
        /// SphereCast fallback 타겟 탐색.
        /// </summary>
        private IDamageable FindTargetBySphereCast(Ray ray)
        {
            RaycastHit[] hits = Physics.SphereCastAll(ray.origin, 0.5f, ray.direction, _assassinationRange, _targetLayers);
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

        /// <summary>
        /// 기본 데미지 계산 (PlayerStats 연동).
        /// </summary>
        private float CalculateBaseDamage()
        {
            float damage = 20f;
            if (PlayerStats.Instance != null)
                damage += PlayerStats.Instance.Level * 2f;
            return damage;
        }

        /// <summary>
        /// 타겟의 최대 HP 추정 (IDamageable은 MaxHP가 없으므로 리플렉션).
        /// </summary>
        private float GetMaxHP(GameObject targetObject)
        {
            // GuardPlaceholder
            GuardPlaceholder guard = targetObject.GetComponent<GuardPlaceholder>();
            if (guard != null) return guard.MaxHP;

            // AnimalAI / 일반 MonoBehaviour — 리플렉션
            MonoBehaviour targetBehaviour = targetObject.GetComponent<MonoBehaviour>();
            if (targetBehaviour != null)
            {
                var animalType = targetBehaviour.GetType();
                var hpField = animalType.GetField("_maxHP",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hpField != null)
                {
                    object val = hpField.GetValue(targetBehaviour);
                    if (val is float f) return f;
                }

                var hpProp = animalType.GetProperty("MaxHP",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (hpProp != null)
                {
                    object val = hpProp.GetValue(targetBehaviour);
                    if (val is float f) return f;
                }
            }

            return 100f; // Fallback
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _assassinationRange);

            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, _alertRadius);
        }
    }
}
