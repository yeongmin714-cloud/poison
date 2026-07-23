using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.IK;
using ProjectName.Systems.Animation.Procedural.Locomotion.Biped;
using ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped;
using ProjectName.Systems.Animation.Procedural.Actions;

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
            Death
        }

        [Header("Transition Timing")]
        [SerializeField] float _jumpCooldown = 0.5f;
        [SerializeField] float _attackCooldown = 1f;
        [SerializeField] float _gatherDuration = 1.5f;
        [SerializeField] float _rollDuration = 0.6f;
        [SerializeField] float _landingDuration = 0.3f;

        State _currentState = State.Locomotion;
        State _previousState = State.Locomotion;
        float _stateTimer;
        float _lastJumpTime;
        float _lastAttackTime;

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

                case State.Climb:
                    if (!IsClimbing())
                        SetState(State.Locomotion);
                    break;

                case State.Stagger:
                    if (_stateTimer > 0.5f)
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

                case State.Climb:
                    break;

                case State.Stagger:
                    _animController?.TriggerAction("stagger");
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
            if (_rigidbody != null && _animController != null)
            {
                float jumpVel = Mathf.Sqrt(-2f * _animController.GetJumpGravity() * _animController.GetJumpHeight());
                _rigidbody.linearVelocity = new Vector3(_rigidbody.linearVelocity.x, jumpVel, _rigidbody.linearVelocity.z);
            }
        }

        void ApplyRollImpulse()
        {
            if (_rigidbody != null)
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