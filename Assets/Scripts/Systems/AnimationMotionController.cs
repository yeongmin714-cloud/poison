using System.Collections;
using ProjectName.Systems.Motions;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Main animation motion driver that coordinates all procedural motion types
    /// (Idle, Walk, Run, Jump, Gather, Craft, Attack, Throw) via coroutine-based
    /// animation loops.
    ///
    /// Listens to state changes from <see cref="RigAnimationController"/>,
    /// activates the corresponding motion component, and
    /// handles smooth transitions between states.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Animation Motion Controller")]
    [RequireComponent(typeof(RigAnimationController))]
    public class AnimationMotionController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Motion Components")]
        [SerializeField] private IdleMotion _idleMotion;
        [SerializeField] private WalkMotion _walkMotion;
        [SerializeField] private RunMotion _runMotion;
        [SerializeField] private JumpMotion _jumpMotion;
        [SerializeField] private GatherMotion _gatherMotion;
        [SerializeField] private CraftMotion _craftMotion;
        [SerializeField] private AttackMotion _attackMotion;
        [SerializeField] private ThrowMotion _throwMotion;

        [Header("Transition Settings")]
        [SerializeField] private float _crossFadeDuration = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool _logStateChanges;

        #endregion

        #region Private State

        private RigAnimationController _rigController;
        private AnimationState _previousState = AnimationState.Idle;
        private Coroutine _transitionRoutine;
        private bool _isInitialized;

        #endregion

        #region Public Properties

        /// <summary>The RigAnimationController this driver monitors.</summary>
        public RigAnimationController RigController => _rigController;

        /// <summary>Cross-fade duration between motion states.</summary>
        public float CrossFadeDuration
        {
            get => _crossFadeDuration;
            set => _crossFadeDuration = Mathf.Max(0f, value);
        }

        /// <summary>True if the controller has been initialized.</summary>
        public bool IsInitialized => _isInitialized;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rigController = GetComponent<RigAnimationController>();
            if (_rigController == null)
            {
                Debug.LogError(
                    "[AnimationMotionController] RigAnimationController not found. " +
                    "Add one to this GameObject.", this);
                return;
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (_isInitialized)
                SubscribeToStateChanges();
        }

        private void OnDisable()
        {
            UnsubscribeFromStateChanges();
            StopAllMotion();
        }

        private void OnDestroy()
        {
            UnsubscribeFromStateChanges();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the controller by subscribing to the
        /// <see cref="RigAnimationController"/> state change events via update polling.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            if (_rigController == null)
            {
                _rigController = GetComponent<RigAnimationController>();
                if (_rigController == null)
                {
                    Debug.LogError(
                        "[AnimationMotionController] Cannot initialize — " +
                        "RigAnimationController missing.", this);
                    return;
                }
            }

            _isInitialized = true;
            SubscribeToStateChanges();
        }

        /// <summary>
        /// Subscribes to RigAnimationController state changes via polling in Update.
        /// </summary>
        private void SubscribeToStateChanges()
        {
            // We use an Update-based polling approach rather than events
            // to keep things simple and avoid modifying RigAnimationController.
        }

        /// <summary>
        /// Unsubscribes from state changes.
        /// </summary>
        private void UnsubscribeFromStateChanges()
        {
            // Cleanup placeholder
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (!_isInitialized || _rigController == null)
                return;

            // Poll for state changes
            AnimationState currentState = _rigController.CurrentState;
            if (currentState != _previousState && !_rigController.IsTransitioning)
            {
                HandleStateChange(currentState);
            }
        }

        #endregion

        #region State Handling

        /// <summary>
        /// Handles a detected state change from the RigAnimationController.
        /// Stops the previous motion, starts the new one with a cross-fade.
        /// </summary>
        /// <param name="newState">The new animation state.</param>
        private void HandleStateChange(AnimationState newState)
        {
            if (_logStateChanges)
            {
                Debug.Log(
                    $"[AnimationMotionController] State change: {_previousState} → {newState}",
                    this);
            }

            // Stop previous motion
            StopMotionForState(_previousState);

            // Start new motion with possible delay for cross-fade
            if (_crossFadeDuration > 0.01f)
            {
                if (_transitionRoutine != null)
                    StopCoroutine(_transitionRoutine);
                _transitionRoutine = StartCoroutine(CrossFadeAndStart(newState));
            }
            else
            {
                StartMotionForState(newState);
            }

            _previousState = newState;
        }

        /// <summary>
        /// Coroutine that waits for the cross-fade duration before starting
        /// the new motion, allowing the previous motion to blend out smoothly.
        /// </summary>
        private IEnumerator CrossFadeAndStart(AnimationState newState)
        {
            // Allow a brief overlap for visual smoothing
            yield return new WaitForSeconds(_crossFadeDuration * 0.5f);

            // If state changed again during the fade, abort
            if (_rigController == null || _rigController.CurrentState != newState)
                yield break;

            StartMotionForState(newState);
            _transitionRoutine = null;
        }

        /// <summary>
        /// Stops all currently playing motion components.
        /// </summary>
        private void StopAllMotion()
        {
            if (_idleMotion != null && _idleMotion.IsPlaying)
                _idleMotion.StopMotion();
            if (_walkMotion != null && _walkMotion.IsPlaying)
                _walkMotion.StopMotion();
            if (_runMotion != null && _runMotion.IsPlaying)
                _runMotion.StopMotion();
            if (_jumpMotion != null && _jumpMotion.IsPlaying)
                _jumpMotion.StopMotion();
            if (_gatherMotion != null && _gatherMotion.IsPlaying)
                _gatherMotion.StopMotion();
            if (_craftMotion != null && _craftMotion.IsPlaying)
                _craftMotion.StopMotion();
            if (_attackMotion != null && _attackMotion.IsPlaying)
                _attackMotion.StopMotion();
            if (_throwMotion != null && _throwMotion.IsPlaying)
                _throwMotion.StopMotion();
        }

        /// <summary>
        /// Stops the motion component associated with the given state.
        /// </summary>
        /// <param name="state">The animation state whose motion to stop.</param>
        private void StopMotionForState(AnimationState state)
        {
            switch (state)
            {
                case AnimationState.Idle:
                    if (_idleMotion != null) _idleMotion.StopMotion();
                    break;
                case AnimationState.Walk:
                    if (_walkMotion != null) _walkMotion.StopMotion();
                    break;
                case AnimationState.Run:
                    if (_runMotion != null) _runMotion.StopMotion();
                    break;
                case AnimationState.Jump:
                    if (_jumpMotion != null) _jumpMotion.StopMotion();
                    break;
                case AnimationState.Gather:
                    if (_gatherMotion != null) _gatherMotion.StopMotion();
                    break;
                case AnimationState.Craft:
                    if (_craftMotion != null) _craftMotion.StopMotion();
                    break;
                case AnimationState.Attack:
                    if (_attackMotion != null) _attackMotion.StopMotion();
                    break;
                case AnimationState.Throw:
                    if (_throwMotion != null) _throwMotion.StopMotion();
                    break;
                case AnimationState.Kneel:
                    // Kneel is handled separately by the rig animation controller
                    break;
            }
        }

        /// <summary>
        /// Starts the motion component associated with the given state.
        /// </summary>
        /// <param name="state">The animation state whose motion to start.</param>
        private void StartMotionForState(AnimationState state)
        {
            switch (state)
            {
                case AnimationState.Idle:
                    if (_idleMotion != null) _idleMotion.StartMotion();
                    break;
                case AnimationState.Walk:
                    if (_walkMotion != null) _walkMotion.StartMotion();
                    break;
                case AnimationState.Run:
                    if (_runMotion != null) _runMotion.StartMotion();
                    break;
                case AnimationState.Jump:
                    if (_jumpMotion != null) _jumpMotion.StartMotion();
                    break;
                case AnimationState.Gather:
                    if (_gatherMotion != null) _gatherMotion.StartMotion();
                    break;
                case AnimationState.Craft:
                    if (_craftMotion != null) _craftMotion.StartMotion();
                    break;
                case AnimationState.Attack:
                    if (_attackMotion != null) _attackMotion.StartMotion();
                    break;
                case AnimationState.Throw:
                    if (_throwMotion != null) _throwMotion.StartMotion();
                    break;
                case AnimationState.Kneel:
                    // Kneel is handled separately
                    break;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Directly sets the given motion component's state without relying on
        /// RigAnimationController state changes. Useful for testing or scripting.
        /// </summary>
        /// <param name="state">The animation state to activate.</param>
        public void ActivateMotion(AnimationState state)
        {
            // Stop any pending transition coroutine to prevent it from
            // overriding this activation after a delay
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            // Sync _previousState with the RigController's actual current state
            // so the Update polling loop does NOT detect a spurious change and
            // override the motion we are about to start.
            _previousState = _rigController != null
                ? _rigController.CurrentState
                : state;

            StopAllMotion();
            StartMotionForState(state);
        }

        /// <summary>
        /// Returns true if the given state's motion component is currently playing.
        /// </summary>
        /// <param name="state">The animation state to check.</param>
        /// <returns>True if the motion is active.</returns>
        public bool IsMotionActive(AnimationState state)
        {
            switch (state)
            {
                case AnimationState.Idle:   return _idleMotion   != null && _idleMotion.IsPlaying;
                case AnimationState.Walk:   return _walkMotion   != null && _walkMotion.IsPlaying;
                case AnimationState.Run:    return _runMotion    != null && _runMotion.IsPlaying;
                case AnimationState.Jump:   return _jumpMotion   != null && _jumpMotion.IsPlaying;
                case AnimationState.Gather: return _gatherMotion != null && _gatherMotion.IsPlaying;
                case AnimationState.Craft:  return _craftMotion  != null && _craftMotion.IsPlaying;
                case AnimationState.Attack: return _attackMotion != null && _attackMotion.IsPlaying;
                case AnimationState.Throw:  return _throwMotion  != null && _throwMotion.IsPlaying;
                default:                    return false;
            }
        }

        #endregion
    }
}