using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;
using ProjectName.Systems.Animation.Procedural;
using Unity.Cinemachine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 공격 시스템 — 마우스 좌클릭 → 커서 방향 자동 조준 → 데미지
    /// C4-08: 마우스 커서 방향으로 가장 가까운 적 자동 탐지 및 타겟팅
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        public static PlayerCombat Instance { get; private set; }

        [Header("Combat Settings")]
        [SerializeField] private LayerMask _targetLayers = -1; // 모든 레이어
        [SerializeField] private float _maxRange = 3f;
        [SerializeField] private float _baseDamage = 10f;
        [SerializeField] private float _attackRadius = 0.5f; // 공격 반지름

        [Header("Auto-Aim (C4-08)")]
        [SerializeField] private float _autoAimRange = 15f;           // 자동 조준 최대 거리
        [SerializeField] private float _autoAimAngle = 10f;           // 조준 원뿔 각도 (도)

        private WeaponData _currentWeapon;
        private float _lastAttackTime = -10f;
        private Camera _mainCamera;
        private CinemachineImpulseSource _impulseSource;

        // ===== 애니메이션 =====
        private RigAnimationController _rigAnim;
        private ProceduralAnimationController _proceduralAnim;

        // ===== C4-08: 자동 조준 상태 =====
        private IDamageable _currentTarget;

        /// <summary>현재 타겟 (자동 조준으로 선택된 적)</summary>
        public IDamageable CurrentTarget => _currentTarget;

        /// <summary>타겟이 있고 살아있는가?</summary>
        public bool HasTarget => _currentTarget != null && _currentTarget.IsAlive;

        /// <summary>공격이 가능한지 여부 (쿨다운 기준)</summary>
        public bool CanAttack => _currentWeapon != null && Time.time - _lastAttackTime >= _currentWeapon.attackSpeed;

        /// <summary>남은 쿨다운 시간 (0 이하이면 공격 가능)</summary>
        public float RemainingCooldown => Mathf.Max(0f, (_lastAttackTime + _currentWeapon?.attackSpeed ?? 1f) - Time.time);

        /// <summary>무기와 플레이어 레벨을 기반으로 데미지를 계산합니다.</summary>
        private float CalculateDamage()
        {
            if (_currentWeapon == null) return _baseDamage;

            float damage = _currentWeapon.damage;
            if (PlayerStats.Instance != null)
                damage += PlayerStats.Instance.Level * 0.5f;
            return damage;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _currentWeapon = WeaponData.Fist;

            // ProceduralAnimationController 획득 (PlayerModel 자식)
            _proceduralAnim = GetComponentInChildren<ProceduralAnimationController>();
            if (_proceduralAnim == null)
            {
                Transform model = transform.Find("PlayerModel");
                if (model != null)
                    _proceduralAnim = model.GetComponent<ProceduralAnimationController>();
            }
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                // Initialize Cinemachine Impulse Source
                _impulseSource = _mainCamera.GetComponent<CinemachineImpulseSource>();
                if (_impulseSource == null)
                    _impulseSource = _mainCamera.gameObject.AddComponent<CinemachineImpulseSource>();
            }

            // RigAnimationController 획득
            _rigAnim = GetComponent<RigAnimationController>();
            if (_rigAnim == null)
            {
                Animator anim = GetComponent<Animator>();
                if (anim != null)
                    _rigAnim = gameObject.AddComponent<RigAnimationController>();
            }
        }

        private void Update()
        {
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return;

            // 타겟 상태 업데이트 (사망 또는 범위 이탈 체크)
            UpdateTargetState();

            // 좌클릭 감지 (InputSystem)
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryAttack();
            }
        }

        /// <summary>
        /// C4-08: 타겟 상태를 업데이트합니다.
        /// 타겟이 죽었거나 너무 멀어지면 해제합니다.
        /// </summary>
        private void UpdateTargetState()
        {
            if (_currentTarget == null) return;

            // 타겟이 죽었는지 확인
            if (!_currentTarget.IsAlive)
            {
                ClearTarget();
                return;
            }

            // 타겟 오브젝트가 파괴되었는지 확인 (MonoBehaviour null 체크)
            MonoBehaviour targetBehaviour = _currentTarget as MonoBehaviour;
            if (targetBehaviour == null) // 오브젝트 파괴됨
            {
                ClearTarget();
                return;
            }

            // 타겟 오브젝트가 범위를 벗어났는지 확인
            float dist = Vector3.Distance(transform.position, targetBehaviour.transform.position);
            if (dist > _autoAimRange * 1.5f)
            {
                ClearTarget();
            }
        }

        /// <summary>
        /// C4-08: 타겟 해제
        /// </summary>
        private void ClearTarget()
        {
            _currentTarget = null;
        }

        private void TryAttack()
        {
            if (!CanAttack) return;
            _lastAttackTime = Time.time;

            // Phase 8.3: 공격 스윙 사운드
            SoundManager.Instance?.PlaySFX("attack_swing");

            // 공격 애니메이션 트리거
            _rigAnim?.Attack();
            _proceduralAnim?.TriggerAction("attack");

            // C4-08: 커서 방향으로 자동 조준 먼저 시도
            IDamageable autoAimTarget = FindTargetInCursorDirection();
            if (autoAimTarget != null)
            {
                // 자동 조준 성공 → 타겟 공격
                _currentTarget = autoAimTarget;
                AttackTarget(_currentTarget);
            }
            else
            {
                // 자동 조준 실패 → 기존 SphereCast 방식 (화면 중앙)
                AttackCenterScreen();
            }

            // 카메라 이펙트 (Cinemachine Impulse)
            TriggerCameraEffects();

            // 공격 전진 (attack lunge)
            StartCoroutine(AttackLungeCoroutine());
        }

        /// <summary>
        /// C4-08: 마우스 커서 방향으로 가장 가까운 적을 찾습니다.
        /// 카메라 → 마우스 커서 위치로 Ray를 쏘아 IDamageable 구현체를 탐색합니다.
        /// </summary>
        private IDamageable FindTargetInCursorDirection()
        {
            if (_mainCamera == null || Mouse.current == null) return null;

            // 마우스 화면 좌표 → 월드 Ray
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            // RaycastAll로 모든 충돌체 검색
            RaycastHit[] hits = Physics.RaycastAll(ray, _autoAimRange, _targetLayers);
            IDamageable closestTarget = null;
            float closestDistance = Mathf.Infinity;

            foreach (RaycastHit hit in hits)
            {
                IDamageable target = hit.collider.GetComponent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        closestTarget = target;
                    }
                }
            }

            // Direct Raycast hit이 없으면 → 원뿔(SphereCast)로 재탐색
            if (closestTarget == null)
            {
                // 카메라 → 마우스 커서 방향, 넓은 범위 SphereCast
                float coneRadius = Mathf.Tan(_autoAimAngle * Mathf.Deg2Rad) * _autoAimRange;
                RaycastHit[] sphereHits = Physics.SphereCastAll(ray.origin, coneRadius, ray.direction, _autoAimRange, _targetLayers);

                foreach (RaycastHit hit in sphereHits)
                {
                    IDamageable target = hit.collider.GetComponent<IDamageable>();
                    if (target != null && target.IsAlive)
                    {
                        // 원뿔 각도 내에 있는지 추가 확인 (실제 각도 계산)
                        Vector3 directionToTarget = (hit.point - ray.origin).normalized;
                        float angle = Vector3.Angle(ray.direction, directionToTarget);
                        if (angle <= _autoAimAngle && hit.distance < closestDistance)
                        {
                            closestDistance = hit.distance;
                            closestTarget = target;
                        }
                    }
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// C4-08: 특정 타겟을 공격합니다.
        /// </summary>
        private void AttackTarget(IDamageable target)
        {
            if (target == null || !target.IsAlive) return;

            float damage = CalculateDamage();
            Vector3 hitDirection = Vector3.zero;

            // 타겟 방향 계산
            MonoBehaviour targetBehaviour = target as MonoBehaviour;
            if (targetBehaviour != null)
            {
                hitDirection = (targetBehaviour.transform.position - transform.position).normalized;
            }

            target.TakeDamage(damage, hitDirection, _currentWeapon?.weaponType.ToString() ?? "melee");

            // ⏱️ 전투 로그: 데미지 기록
            string targetName = targetBehaviour != null ? targetBehaviour.gameObject.name : "Unknown";
            CombatLog.AddEntry($"{targetName}에게 {damage} 데미지", LogType.Damage);

            // 🔊 컨트롤러 진동: 기본 공격 hit (Light)
            HapticFeedback.PlayPreset(HapticFeedback.RumblePreset.Light);

            // G2-04: 치명타/백어택 감지 → Shake 2배 + HitStop
            if (targetBehaviour != null)
            {
                Vector3 dirToAttacker = (transform.position - targetBehaviour.transform.position).normalized;
                float dot = Vector3.Dot(targetBehaviour.transform.forward, dirToAttacker);
                if (dot > 0.5f) // 뒤에서 공격 (back attack)
                {
                    CombatCameraEffects.PlayCrit();
                    Debug.Log("[PlayerCombat] ★ 백어택! 치명타 카메라 이펙트");
                }
                else
                {
                    CombatCameraEffects.PlayHit();
                }
            }

            // 킬 시 슬로우모션
            if (target is IDamageable damageable && damageable.IsDead)
            {
                CombatCameraEffects.PlayKill();
            }

            // Phase 8.3: 적중 사운드
            SoundManager.Instance?.PlaySFX("attack_hit");

            // Hit VFX: Sparks and Flash
            if (targetBehaviour != null)
            {
                Renderer targetRenderer = targetBehaviour.GetComponent<Renderer>();
                if (targetRenderer != null)
                {
                    HitVFX.PlayHitEffect(targetBehaviour.transform.position, hitDirection);
                    HitVFX.PlayHitFlash(targetRenderer);
                }
            }

            Debug.Log($"[PlayerCombat] 🎯 자동조준! {_currentWeapon?.weaponName ?? "Unknown"} → {target} 데미지: {damage}");

            // 카메라 효과
            TriggerCameraEffects();
        }

        /// <summary>
        /// 자동 조준 실패 시 화면 중앙 방향으로 SphereCast 공격
        /// </summary>
        private void AttackCenterScreen()
        {
            if (_mainCamera == null) return;

            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Ray ray = _mainCamera.ScreenPointToRay(screenCenter);

            if (Physics.SphereCast(ray, _attackRadius, out RaycastHit hit, _maxRange, _targetLayers))
            {
                IDamageable target = hit.collider.GetComponent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    AttackTarget(target);
                    return;
                }
            }

            Debug.Log("[PlayerCombat] ⚔️ 공격 실패 — 대상 없음");
        }

        /// <summary>
        /// 공격 시 카메라 이펙트 — Cinemachine Impulse Source로 반동 처리.
        /// CombatCameraEffects.PlayCrit()가 추가 Shake/HitStop을 처리합니다.
        /// </summary>
        private void TriggerCameraEffects()
        {
            if (_impulseSource != null)
            {
                _impulseSource.GenerateImpulse(Vector3.forward * 0.5f);
            }
        }
    }
}
