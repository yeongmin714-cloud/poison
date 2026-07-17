using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 프로시저럴 애니메이션 상태 머신.
    /// Locomotion, Jump, Attack, Gather, Roll, Climb 등 모든 상태 전이 관리.
    /// </summary>
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
            Death
        }

        [Header("Transitions")]
        [SerializeField] private float _jumpCooldown = 0.5f;
        [SerializeField] private float _attackCooldown = 1f;
        [SerializeField] private float _gatherDuration = 1.5f;
        [SerializeField] private float _rollDuration = 0.6f;

        private State _currentState = State.Locomotion;
        private State _previousState = State.Locomotion;
        private float _stateTimer;
        private float _lastJumpTime;
        private float _lastAttackTime;

        // External refs
        private ProceduralAnimationController _animController;
        private Rigidbody _rigidbody;

        // Events
        public System.Action<State, State> OnStateChanged;

        // ──────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────

        private void Awake()
        {
            _animController = GetComponent<ProceduralAnimationController>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            EnterState(State.Locomotion);
        }

        private void Update()
        {
            _stateTimer += Time.deltaTime;
            UpdateTransitions();
        }

        // ──────────────────────────────────────────────
        // 상태 전이
        // ──────────────────────────────────────────────

        private void UpdateTransitions()
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
                    if (_stateTimer > 0.3f)
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

                case State.Climb:
                    if (!IsClimbing())
                        SetState(State.Locomotion);
                    break;
            }
        }

        private void CheckLocomotionTransitions()
        {
            // Jump
            if (Input.GetKeyDown(KeyCode.Space) && Time.time - _lastJumpTime > _jumpCooldown && IsGrounded())
                RequestJump();

            // Attack
            if (Input.GetMouseButtonDown(0) && Time.time - _lastAttackTime > _attackCooldown)
                RequestAttack();

            // Gather
            if (Input.GetKeyDown(KeyCode.E))
                RequestGather();

            // Roll
            if (Input.GetKeyDown(KeyCode.Q) && IsGrounded())
                RequestRoll();
        }

        // ──────────────────────────────────────────────
        // 공개 요청 메서드 (외부에서 호출)
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

        public void RequestClimb()
        {
            if (_currentState == State.Locomotion)
                SetState(State.Climb);
        }

        public void TakeDamage(float damage)
        {
            if (_currentState != State.Death)
            {
                if (damage > 30f)
                    SetState(State.Stagger);
            }
        }

        public void Die()
        {
            SetState(State.Death);
        }

        // ──────────────────────────────────────────────
        // 상태 진입/종료
        // ──────────────────────────────────────────────

        private void SetState(State newState)
        {
            if (_currentState == newState) return;

            ExitState(_currentState);
            _previousState = _currentState;
            _currentState = newState;
            _stateTimer = 0f;
            EnterState(newState);

            OnStateChanged?.Invoke(_previousState, _currentState);
        }

        private void EnterState(State state)
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

                case State.Climb:
                    break;

                case State.Stagger:
                    break;

                case State.Death:
                    break;
            }
        }

        private void ExitState(State state)
        {
            // Cleanup if needed
        }

        // ──────────────────────────────────────────────
        // 물리 헬퍼
        // ──────────────────────────────────────────────

        private void ApplyJumpImpulse()
        {
            if (_rigidbody != null)
            {
                _rigidbody.AddForce(Vector3.up * 8f, ForceMode.VelocityChange);
            }
        }

        private void ApplyRollImpulse()
        {
            if (_rigidbody != null)
            {
                Vector3 rollDir = transform.forward;
                if (_animController.CurrentVelocity.magnitude > 0.1f)
                    rollDir = _animController.CurrentVelocity.normalized;
                _rigidbody.AddForce(rollDir * 15f, ForceMode.VelocityChange);
            }
        }

        private bool IsGrounded()
        {
            return _animController != null && _animController.IsGrounded;
        }

        private bool IsFalling()
        {
            return _rigidbody != null && _rigidbody.velocity.y < -0.1f;
        }

        private bool IsClimbing()
        {
            // Check for climbable surface ahead
            return Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, 1f, LayerMask.GetMask("Climbable"));
        }

        // ──────────────────────────────────────────────
        // 공개 속성
        // ──────────────────────────────────────────────

        public State CurrentState => _currentState;
        public State PreviousState => _previousState;
        public float StateTime => _stateTimer;
        public bool IsInState(State state) => _currentState == state;
    }
}