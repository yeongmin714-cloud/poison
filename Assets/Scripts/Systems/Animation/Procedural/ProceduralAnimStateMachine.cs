using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.IK;
using ProjectName.Systems.Animation.Procedural.Locomotion.Biped;
using ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped;
using ProjectName.Systems.Animation.Procedural.Actions;
using ProjectName.Systems;

namespace ProjectName.Systems.Animation.Procedural
{
    /// <summary>
    /// Procedural animation state machine - manages state transitions.
    /// States: Locomotion, Jump, Airborne, Landing, Attack, Gather, Roll, Climb, Stagger, Death
    /// </summary>
    [Obsolete("Use HybridAnimationController with NeuralAnimationController instead. See MIGRATION_GUIDE_PHASE46.md", false)]
    public class ProceduralAnimStateMachine : MonoBehaviour
    {
        public enum State
        {
            Locomotion,
            Jump,
            Airborne,
            Landing,
            Attack,
            Gather,
            Roll,
            Climb,
            Stagger,
            Death,
            Charge,   // [Phase 1-1] 차지(강공 충전) 절차 상태
            Parry     // [Phase 1-2] 패링(근접 방어) 절차 상태
        }

        [Header("Transition Timing")]
        [SerializeField] float _jumpCooldown = 0.5f;
        [SerializeField] float _attackCooldown = 1f;
        [SerializeField] float _gatherDuration = 1.5f;
        [SerializeField] float _rollDuration = 0.6f;
        [SerializeField] float _landingDuration = 0.3f;
        [SerializeField] float _chargeMaxDuration = 0.8f;   // [Phase 1-1] 차지 최대 충전 시간 (초) — 오버차지 시 자동 강공
        [SerializeField] float _parryDuration = 0.28f;      // [Phase 1-2] 패링 방어 판정 창 (초)

        State _currentState = State.Locomotion;
        State _previousState = State.Locomotion;
        float _stateTimer;
        float _lastJumpTime;
        float _lastAttackTime;

        // [Phase A] 넉백/히트리액션 데이터
        Vector3 _hitDirection;        // 피격 방향 (정규화)
        float _hitDamage;             // 피격 데미지량
        bool _isHeavyHit;             // 강한 피격 여부 (Knockback/Launch 여부)
        float _staggerDuration;       // Stagger 지속시간 (리액션 타입별 가변)

        // Component refs
        ProceduralAnimationController _animController;
        Rigidbody _rigidbody;

        // Events
        public System.Action<State, State> OnStateChanged;

        // ──────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            _animController = GetComponent<ProceduralAnimationController>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        void Start()
        {
            EnterState(State.Locomotion);
        }

        void Update()
        {
            // 플레이어는 HumanoidClipDriver(클립 애니메이션)가 구동 → 프로시저럴 상태머신이
            // 상태 전환을 지시하면 같은 골격을 두 시스템이 덮어써 끊긴다. 클립 드라이버가 있으면 스킵.
            // HumanoidClipDriver는 플레이어 루트가 아닌 자식 PlayerBody에 붙으므로(Assets/GameSetup.cs),
            // InParent(자기+조상)만으론 자기 자신/자식의 드라이버를 못 찾는다. InChildren 병행으로 커버.
            if (GetComponentInParent<HumanoidClipDriver>() != null ||
                GetComponentInChildren<HumanoidClipDriver>() != null) return;

            _stateTimer += Time.deltaTime;
            UpdateTransitions();
        }

        // ──────────────────────────────────────────────
        // State Transitions
        // ──────────────────────────────────────────────

        void UpdateTransitions()
        {
            switch (_currentState)
            {
                case State.Locomotion:
                    CheckLocomotionTransitions();
                    break;

                case State.Jump:
                    if (_stateTimer > 0.1f && IsFalling())
                        SetState(State.Airborne);
                    break;

                case State.Airborne:
                    if (IsGrounded())
                        SetState(State.Landing);
                    break;

                case State.Landing:
                    if (_stateTimer > _landingDuration)
                        SetState(State.Locomotion);
                    break;

                case State.Attack:
                    if (_stateTimer > 0.8f)
                        SetState(State.Locomotion);
                    break;

                case State.Gather:
                    if (_stateTimer > _gatherDuration)
                        SetState(State.Locomotion);
                    break;

                case State.Roll:
                    if (_stateTimer > _rollDuration)
                        SetState(State.Locomotion);
                    break;

                case State.Charge:
                    // [Phase 1-1] 차지 — 최대 충전 시간 후 자동 강공 발동(해제 시 PlayerCombat이 발동).
                    if (_stateTimer > _chargeMaxDuration)
                        SetState(State.Locomotion);   // 오버차지 — 대기 종료
                    break;

                case State.Parry:
                    // [Phase 1-2] 패링 — 짧은 창 이후 종료.
                    if (_stateTimer > _parryDuration)
                        SetState(State.Locomotion);
                    break;

                case State.Climb:
                    if (!IsClimbing())
                        SetState(State.Locomotion);
                    break;

                case State.Stagger:
                    if (_stateTimer > _staggerDuration)
                        SetState(State.Locomotion);
                    break;
            }
        }

        void CheckLocomotionTransitions()
        {
            // Jump
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && Time.time - _lastJumpTime > _jumpCooldown && IsGrounded())
                RequestJump();

            // Attack
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Time.time - _lastAttackTime > _attackCooldown)
                RequestAttack();

            // Gather
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                RequestGather();

            // Roll
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame && IsGrounded())
                RequestRoll();
        }

        // ──────────────────────────────────────────────
        // Public Requests
        // ──────────────────────────────────────────────

        public void RequestJump()
        {
            if (_currentState == State.Locomotion || _currentState == State.Airborne)
            {
                _lastJumpTime = Time.time;
                SetState(State.Jump);
            }
        }

        public void RequestAttack()
        {
            if (_currentState == State.Locomotion || _currentState == State.Airborne)
            {
                _lastAttackTime = Time.time;
                SetState(State.Attack);
            }
        }

        public void RequestGather()
        {
            if (_currentState == State.Locomotion)
                SetState(State.Gather);
        }

        public void RequestRoll()
        {
            if (_currentState == State.Locomotion)
                SetState(State.Roll);
        }

        /// <summary>[Phase 1-1] 차지(강공 충전) 시작 — 전투 상태에서만 진입.</summary>
        public void RequestCharge()
        {
            if (_currentState == State.Locomotion)
                SetState(State.Charge);
        }

        /// <summary>[Phase 1-2] 패링(근접 방어) 시작 — 공격 직후 창이 짧아 전투 진입과 별도.</summary>
        public void RequestParry()
        {
            if (_currentState == State.Locomotion || _currentState == State.Roll)
                SetState(State.Parry);
        }

        public void RequestClimb()
        {
            if (_currentState == State.Locomotion)
                SetState(State.Climb);
        }

        public void TakeDamage(float damage, Vector3 hitDirection = default)
        {
            if (_currentState != State.Death)
            {
                // [Phase A] 히트 방향/데미지 저장 (리액션 타입 결정용)
                _hitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : transform.forward;
                _hitDamage = damage;
                _isHeavyHit = damage > 30f;

                if (damage > 30f)
                    SetState(State.Stagger);

                // T-D3+: 클립 히트 반응(경직/스턴/다운) — HumanoidClipDriver 경유(등급: 소/중/대)
                var clipDriver = GetComponent<HumanoidClipDriver>()
                    ?? GetComponentInParent<HumanoidClipDriver>()
                    ?? GetComponentInChildren<HumanoidClipDriver>();
                if (clipDriver != null)
                {
                    if (damage >= 40f) clipDriver.TriggerKnockdown();
                    else if (damage >= 25f) clipDriver.TriggerStun();
                    else clipDriver.TriggerHitLight();
                }
            }
        }

        public void Die()
        {
            SetState(State.Death);
        }

        // ──────────────────────────────────────────────
        // State Enter/Exit
        // ──────────────────────────────────────────────

        void SetState(State newState)
        {
            if (_currentState == newState) return;

            ExitState(_currentState);
            _previousState = _currentState;
            _currentState = newState;
            _stateTimer = 0f;
            EnterState(newState);

            OnStateChanged?.Invoke(_previousState, _currentState);
        }

        void EnterState(State state)
        {
            switch (state)
            {
                case State.Locomotion:
                    break;

                case State.Jump:
                    ApplyJumpImpulse();
                    break;

                case State.Airborne:
                    break;

                case State.Landing:
                    break;

                case State.Attack:
                    _animController?.TriggerAction("attack");
                    break;

                case State.Gather:
                    _animController?.TriggerAction("gather");
                    break;

                case State.Roll:
                    _animController?.TriggerAction("roll");
                    ApplyRollImpulse();
                    break;

                case State.Charge:
                    // [Phase 1-1] 차지 절차 동작 트리거 — FBX 클립 확보 시 TriggerAction("charge")를 클립 재생으로 대체 가능.
                    _animController?.TriggerAction("charge");
                    break;

                case State.Parry:
                    // [Phase 1-2] 패링 절차 동작 트리거 — FBX 클립 확보 시 TriggerAction("parry") 클립 재생.
                    _animController?.TriggerAction("parry");
                    break;

                case State.Climb:
                    break;

                case State.Stagger:
                    // [Phase A] 넉백/히트리액션 다양화
                    // 데미지/방향에 따른 리액션 타입:
                    // - Light (damage <= 20): 경직만 (0.3s)
                    // - Heavy (damage 20-40): 넉백 (0.5s + 후방 밀림)
                    // - Launch (damage >= 40): 에어본 (0.8s + 상향 튕김)
                    if (_hitDamage <= 20f)
                    {
                        _staggerDuration = 0.3f; // Light: 짧은 경직
                        _animController?.TriggerAction("stagger_light");
                    }
                    else if (_hitDamage < 40f)
                    {
                        _staggerDuration = 0.5f; // Heavy: 넉백
                        _animController?.TriggerAction("stagger_heavy");
                        // 넉백 물리: 피격 반대 방향으로 밀림
                        if (_rigidbody != null && !_rigidbody.isKinematic)
                        {
                            Vector3 knockbackDir = -_hitDirection;
                            knockbackDir.y = 0.2f; // 약간 상향
                            _rigidbody.AddForce(knockbackDir * _hitDamage * 0.5f, ForceMode.Impulse);
                        }
                    }
                    else
                    {
                        _staggerDuration = 0.8f; // Launch: 에어본
                        _animController?.TriggerAction("stagger_launch");
                        // 런치 물리: 상향 튕김
                        if (_rigidbody != null && !_rigidbody.isKinematic)
                        {
                            _rigidbody.linearVelocity = new Vector3(_rigidbody.linearVelocity.x, 10f, _rigidbody.linearVelocity.z);
                        }
                    }
                    _stateTimer = 0f;
                    break;

                case State.Death:
                    _animController?.TriggerAction("death");
                    break;
            }
        }

        void ExitState(State state)
        {
            // Cleanup if needed
        }

        // ──────────────────────────────────────────────
        // Physics Helpers
        // ──────────────────────────────────────────────

        void ApplyJumpImpulse()
        {
            if (_rigidbody != null && _animController != null && !_rigidbody.isKinematic)
            {
                float jumpVel = Mathf.Sqrt(-2f * _animController.GetJumpGravity() * _animController.GetJumpHeight());
                _rigidbody.linearVelocity = new Vector3(_rigidbody.linearVelocity.x, jumpVel, _rigidbody.linearVelocity.z);
            }
        }

        void ApplyRollImpulse()
        {
            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                Vector3 rollDir = _currentVelocity.magnitude > 0.1f ? _currentVelocity.normalized : transform.forward;
                _rigidbody.AddForce(rollDir * 15f, ForceMode.VelocityChange);
            }
        }

        bool IsGrounded() => _animController != null && _animController.IsGrounded;
        bool IsFalling() => _rigidbody != null && _rigidbody.linearVelocity.y < -0.1f;
        bool IsClimbing() => Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, 1f, LayerMask.GetMask("Climbable"));

        // ──────────────────────────────────────────────
        // Public Properties
        // ──────────────────────────────────────────────

        public State CurrentState => _currentState;
        public State PreviousState => _previousState;
        public float StateTime => _stateTimer;
        public bool IsInState(State state) => _currentState == state;

        // Need to expose current velocity from controller
        Vector3 _currentVelocity => _animController?.CurrentVelocity ?? Vector3.zero;
    }
}