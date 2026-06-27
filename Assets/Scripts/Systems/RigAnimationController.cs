using System;
using System.Collections;
using UnityEngine;

#if UNITY_ANIMATION_RIGGING
using UnityEngine.Animations.Rigging;
#endif

namespace ProjectName.Systems
{
    /// <summary>
    /// All supported animation states for rigged characters.
    /// Each state maps to a specific animation clip or procedural pose.
    /// </summary>
    public enum AnimationState
    {
        /// <summary>Character is standing still, breathing/idle animation.</summary>
        Idle,
        /// <summary>Character is walking at normal pace.</summary>
        Walk,
        /// <summary>Character is sprinting.</summary>
        Run,
        /// <summary>Character is jumping or in mid-air.</summary>
        Jump,
        /// <summary>Character is gathering resources (bending, reaching).</summary>
        Gather,
        /// <summary>Character is crafting at a station.</summary>
        Craft,
        /// <summary>Character is performing a melee attack.</summary>
        Attack,
        /// <summary>Character is throwing a projectile.</summary>
        Throw,
        /// <summary>Character is kneeling or bowing.</summary>
        Kneel
    }

    /// <summary>
    /// Central animation controller for all rigged characters.
    /// Manages state transitions, works with <see cref="Animator"/> for clip-based
    /// animation and <see cref="RigBuilder"/> for procedural animation layering.
    ///
    /// Supports smooth coroutine-driven transitions between states and provides
    /// visual debug information in the Scene view.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Rig Animation Controller")]
    [RequireComponent(typeof(Animator))]
    public class RigAnimationController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        //  Serialized fields
        // ──────────────────────────────────────────────

        [Header("Animation Settings")]
        [SerializeField] private AnimationState _currentState = AnimationState.Idle;
        [SerializeField] private float _transitionDuration = 0.25f;

        [Header("Animator Parameters")]
        [SerializeField] private string _speedParam = "Speed";
        [SerializeField] private string _stateParam = "State";
        [SerializeField] private string _jumpTrigger = "Jump";
        [SerializeField] private string _attackTrigger = "Attack";
        [SerializeField] private string _gatherTrigger = "Gather";
        [SerializeField] private string _craftTrigger = "Craft";
        [SerializeField] private string _throwTrigger = "Throw";
        [SerializeField] private string _kneelTrigger = "Kneel";

        [Header("Procedural Settings")]
#pragma warning disable 0414
        [SerializeField, Range(0f, 1f)] private float _rigWeight = 1f;
#pragma warning restore 0414

        // ──────────────────────────────────────────────
        //  Private state
        // ──────────────────────────────────────────────

        private Animator _animator;
        private Coroutine _transitionCoroutine;
        private float _currentSpeed;

#if UNITY_ANIMATION_RIGGING
        private RigBuilder _rigBuilder;
        private Rig[] _rigLayers;
#endif

        // ──────────────────────────────────────────────
        //  Public properties
        // ──────────────────────────────────────────────

        /// <summary>
        /// The current animation state of this character.
        /// Setting this value triggers a smooth transition via <see cref="SetState"/>.
        /// </summary>
        public AnimationState CurrentState => _currentState;

        /// <summary>
        /// Duration in seconds for smooth state transitions.
        /// </summary>
        public float TransitionDuration
        {
            get => _transitionDuration;
            set => _transitionDuration = Mathf.Max(0.01f, value);
        }

        /// <summary>
        /// Current movement speed (blend tree parameter value).
        /// </summary>
        public float CurrentSpeed
        {
            get => _currentSpeed;
            set => _currentSpeed = Mathf.Max(0f, value);
        }

        /// <summary>
        /// The Animator component on this character.
        /// </summary>
        public Animator AnimatorComponent => _animator;

        /// <summary>
        /// True if a state transition is currently in progress.
        /// </summary>
        public bool IsTransitioning { get; private set; }

        // ──────────────────────────────────────────────
        //  Unity lifecycle
        // ──────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponent<Animator>();

#if UNITY_ANIMATION_RIGGING
            _rigBuilder = GetComponent<RigBuilder>();
            if (_rigBuilder != null)
                _rigLayers = GetComponentsInChildren<Rig>(true);
#endif
        }

        private void Start()
        {
            // Apply initial state
            ApplyStateImmediate(_currentState);
        }

        private void OnDestroy()
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }
        }

        private void Update()
        {
            // Update animator parameters each frame
            if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
            {
                _animator.SetFloat(_speedParam, _currentSpeed);
                _animator.SetInteger(_stateParam, (int)_currentState);
            }
        }

        // ──────────────────────────────────────────────
        //  State management
        // ──────────────────────────────────────────────

        /// <summary>
        /// Sets the character's animation state with a smooth coroutine-driven transition.
        /// If a transition is already in progress, it is cancelled and replaced.
        /// </summary>
        /// <param name="newState">The target animation state.</param>
        public void SetState(AnimationState newState)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return;

            // Allow re-triggering for trigger-based animation states (Jump, Attack, etc.)
            bool isTriggerState = newState == AnimationState.Jump
                || newState == AnimationState.Gather
                || newState == AnimationState.Craft
                || newState == AnimationState.Attack
                || newState == AnimationState.Throw
                || newState == AnimationState.Kneel;

            if (!isTriggerState && newState == _currentState && !IsTransitioning)
                return;

            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);

            _transitionCoroutine = StartCoroutine(TransitionToState(newState));
        }

        /// <summary>
        /// Immediately applies a state without any blending.
        /// </summary>
        /// <param name="state">The animation state to apply.</param>
        public void SetStateImmediate(AnimationState state)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return;

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            IsTransitioning = false;
            ApplyStateImmediate(state);
        }

        /// <summary>
        /// Coroutine that smoothly transitions from the current state to a new state
        /// over <see cref="_transitionDuration"/> seconds.
        /// </summary>
        private IEnumerator TransitionToState(AnimationState newState)
        {
            IsTransitioning = true;
            AnimationState previousState = _currentState;

            // Apply the new state immediately to the animator
            ApplyAnimatorState(newState);

            // Smooth rig weight transition if available
#if UNITY_ANIMATION_RIGGING
            if (_rigLayers != null && _rigLayers.Length > 0)
            {
                float elapsed = 0f;
                float startWeight = _rigWeight;
                float targetWeight = (newState == AnimationState.Idle) ? 1f : 0.85f;

                while (elapsed < _transitionDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / _transitionDuration;
                    t = t * t * (3f - 2f * t); // smoothstep

                    _rigWeight = Mathf.Lerp(startWeight, targetWeight, t);
                    foreach (var rig in _rigLayers)
                    {
                        if (rig != null)
                            rig.weight = _rigWeight;
                    }

                    yield return null;
                }

                _rigWeight = targetWeight;
            }
            else
#endif
            {
                yield return new WaitForSeconds(_transitionDuration);
            }

            _currentState = newState;
            IsTransitioning = false;
            _transitionCoroutine = null;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[RigAnimationController] State transition: {previousState} → {newState} " +
                $"on '{gameObject.name}'",
                this);
            #endif
        }

        /// <summary>
        /// Applies the given state to the animator immediately (no blending).
        /// </summary>
        private void ApplyStateImmediate(AnimationState state)
        {
            _currentState = state;
            ApplyAnimatorState(state);

#if UNITY_ANIMATION_RIGGING
            if (_rigLayers != null)
            {
                _rigWeight = (state == AnimationState.Idle) ? 1f : 0.85f;
                foreach (var rig in _rigLayers)
                {
                    if (rig != null)
                        rig.weight = _rigWeight;
                }
            }
#endif
        }

        /// <summary>
        /// Sets animator parameters/triggers for the given state.
        /// </summary>
        private void ApplyAnimatorState(AnimationState state)
        {
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
                return;

            // Reset all triggers first
            ResetAllTriggers();

            // Set integer parameter
            _animator.SetInteger(_stateParam, (int)state);

            // Fire the appropriate trigger
            switch (state)
            {
                case AnimationState.Idle:
                    // Idle is the default blend tree state
                    _animator.SetFloat(_speedParam, 0f);
                    _currentSpeed = 0f;
                    break;
                case AnimationState.Walk:
                    _currentSpeed = 0.5f;
                    break;
                case AnimationState.Run:
                    _currentSpeed = 1.0f;
                    break;
                case AnimationState.Jump:
                    _animator.SetTrigger(_jumpTrigger);
                    break;
                case AnimationState.Gather:
                    _animator.SetTrigger(_gatherTrigger);
                    break;
                case AnimationState.Craft:
                    _animator.SetTrigger(_craftTrigger);
                    break;
                case AnimationState.Attack:
                    _animator.SetTrigger(_attackTrigger);
                    break;
                case AnimationState.Throw:
                    _animator.SetTrigger(_throwTrigger);
                    break;
                case AnimationState.Kneel:
                    _animator.SetTrigger(_kneelTrigger);
                    break;
            }
        }

        /// <summary>
        /// Resets all animator triggers to prevent stale trigger states.
        /// </summary>
        private void ResetAllTriggers()
        {
            if (_animator == null) return;

            _animator.ResetTrigger(_jumpTrigger);
            _animator.ResetTrigger(_attackTrigger);
            _animator.ResetTrigger(_gatherTrigger);
            _animator.ResetTrigger(_craftTrigger);
            _animator.ResetTrigger(_throwTrigger);
            _animator.ResetTrigger(_kneelTrigger);
        }

        // ──────────────────────────────────────────────
        //  Convenience methods
        // ──────────────────────────────────────────────

        /// <summary>Transitions to the Idle state.</summary>
        public void Idle() => SetState(AnimationState.Idle);
        /// <summary>Transitions to the Walk state.</summary>
        public void Walk() => SetState(AnimationState.Walk);
        /// <summary>Transitions to the Run state.</summary>
        public void Run() => SetState(AnimationState.Run);
        /// <summary>Triggers a Jump.</summary>
        public void Jump() => SetState(AnimationState.Jump);
        /// <summary>Triggers a Gather animation.</summary>
        public void Gather() => SetState(AnimationState.Gather);
        /// <summary>Triggers a Craft animation.</summary>
        public void Craft() => SetState(AnimationState.Craft);
        /// <summary>Triggers an Attack animation.</summary>
        public void Attack() => SetState(AnimationState.Attack);
        /// <summary>Triggers a Throw animation.</summary>
        public void Throw() => SetState(AnimationState.Throw);
        /// <summary>Transitions to the Kneel state.</summary>
        public void Kneel() => SetState(AnimationState.Kneel);

        // ──────────────────────────────────────────────
        //  Debug visualization
        // ──────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            // Draw state indicator above the character
            Vector3 position = transform.position + Vector3.up * 2.5f;

#if UNITY_EDITOR
            UnityEditor.Handles.color = GetStateColor(_currentState);
            UnityEditor.Handles.DrawSolidDisc(position, Vector3.up, 0.3f);

            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                position + Vector3.up * 0.4f,
                $"[{_currentState}]{(IsTransitioning ? " ~" : "")}");

            // Draw speed indicator
            if (_currentSpeed > 0.01f)
            {
                Vector3 speedPos = position + Vector3.right * 0.5f;
                UnityEditor.Handles.color = Color.Lerp(Color.green, Color.red, _currentSpeed);
                UnityEditor.Handles.DrawLine(speedPos, speedPos + Vector3.forward * _currentSpeed);
            }
#endif
        }

        /// <summary>
        /// Returns a colour associated with the given animation state for debug viz.
        /// </summary>
        private Color GetStateColor(AnimationState state)
        {
            switch (state)
            {
                case AnimationState.Idle:   return new Color(0.5f, 0.5f, 0.5f); // grey
                case AnimationState.Walk:   return Color.green;
                case AnimationState.Run:    return Color.blue;
                case AnimationState.Jump:   return Color.cyan;
                case AnimationState.Gather: return new Color(0.8f, 0.6f, 0.2f); // gold
                case AnimationState.Craft:  return new Color(0.6f, 0.3f, 0.8f); // purple
                case AnimationState.Attack: return Color.red;
                case AnimationState.Throw:  return new Color(1f, 0.5f, 0f);     // orange
                case AnimationState.Kneel:  return new Color(0.3f, 0.3f, 0.6f); // indigo
                default:                    return Color.white;
            }
        }
    }
}