using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Data;

namespace TimeWarp.Modules.Physics.Dynamics
{
    /// <summary>
    /// Handles the physics and time scaling for 2D rigidbodies.
    /// Automatically interacts with KineticAccumulator2D to release energy after time freeze.
    /// </summary>
    [RequireComponent(typeof(TimeWarpIdentity))]
    [RequireComponent(typeof(Rigidbody2D))]
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("TimeWarp/Physics 2D/Time Body 2D")]
    public class TimeBody2D : MonoBehaviour, ITimeWarpModule
    {
        private Rigidbody2D _rb2d;
        private TimeWarpIdentity _identity;
        private KineticAccumulator2D _accumulator; 
        
        // State tracking
        private RigidbodyType2D _originalBodyType;
        private RigidbodyInterpolation2D _originalInterpolation;
        private bool _wasFrozenLastFrame = false;

        // Slow motion mathematics
        private float _lastTimeScale = 1.0f;
        private bool _isSlowMoHandling = false;
        private float _originalGravityScale;

        private void Awake()
        {
            _rb2d = GetComponent<Rigidbody2D>();
            _identity = GetComponent<TimeWarpIdentity>();
            _accumulator = GetComponent<KineticAccumulator2D>(); 
            
            // Cache original settings to restore them correctly
            _originalBodyType = _rb2d.bodyType;
            _originalInterpolation = _rb2d.interpolation;
            _originalGravityScale = _rb2d.gravityScale;
        }

        public void OnWarpFixedUpdate() 
        {
            // Do nothing if not registered or velocity tracking is disabled
            if (!_identity.IsRegistered || (_identity.Channels & TimeChannels.Velocity2D) == 0) return;

            bool isRewinding = _identity.IsRewinding || _identity.FractionalOffset > 0.001f;
            bool isTimeStopped = _identity.EffectiveTimeScale <= 0.001f;
            bool isCurrentlyFrozen = isRewinding || isTimeStopped;

            if (isCurrentlyFrozen && !_wasFrozenLastFrame)
            {
                // ==========================================
                // ENTER FREEZE STATE
                // ==========================================
                _originalBodyType = _rb2d.bodyType;
                _originalInterpolation = _rb2d.interpolation;

                _rb2d.bodyType = RigidbodyType2D.Kinematic;
                _rb2d.interpolation = RigidbodyInterpolation2D.None;
                
                _rb2d.linearVelocity = Vector2.zero;
                _rb2d.angularVelocity = 0f;
            }
            else if (!isCurrentlyFrozen && _wasFrozenLastFrame)
            {
                // ==========================================
                // EXIT FREEZE STATE
                // ==========================================
                _rb2d.bodyType = _originalBodyType;
                _rb2d.interpolation = _originalInterpolation;

                // Restore velocity from history buffer ONLY if returning from rewind
                if (_identity.IsRewinding || _identity.FractionalOffset > 0.001f)
                {
                    if (_identity.Buffer != null && _identity.Buffer.Velocity2DHistory != null && _identity.Buffer.Velocity2DHistory.Length > 0)
                    {
                        int head = _identity.Buffer.GetIndex(0);
                        Velocity2DSnapshot snap = _identity.Buffer.Velocity2DHistory[head];
                        
                        _rb2d.linearVelocity = snap.linearVelocity;
                        _rb2d.angularVelocity = snap.angularVelocity;
                    }
                }
                
                // Release accumulated kinetic energy from bullets/hits received during the freeze
                if (_accumulator != null) 
                {
                    _accumulator.ReleaseEnergy();
                }
                
                _lastTimeScale = Mathf.Max(_identity.EffectiveTimeScale, 0.001f); 
            }

            _wasFrozenLastFrame = isCurrentlyFrozen;
        }

        public void OnWarpUpdate() 
        {
            if (!_identity.IsRegistered || (_identity.Channels & TimeChannels.Velocity2D) == 0) return;
            
            HandleLocalSlowMotion();
        }

        /// <summary>
        /// Dynamically scales velocity and gravity based on the current time scale.
        /// </summary>
        private void HandleLocalSlowMotion()
        {
            // Do not apply slow motion math if the object is kinematic or completely frozen
            if (_rb2d.bodyType == RigidbodyType2D.Kinematic || _wasFrozenLastFrame) return;
            
            float currentScale = _identity.EffectiveTimeScale;

            // 1. VELOCITY SCALING
            // Only update if the time scale has noticeably changed
            if (Mathf.Abs(currentScale - _lastTimeScale) > 0.001f)
            {
                float safeLast = Mathf.Max(_lastTimeScale, 0.01f);
                float safeCurrent = Mathf.Max(currentScale, 0.01f);
                
                float ratio = safeCurrent / safeLast;
                if (ratio > 10f) ratio = 10f; // Safety clamp to prevent physics explosions
                
                _rb2d.linearVelocity *= ratio;
                _rb2d.angularVelocity *= ratio;

                _lastTimeScale = currentScale;
            }

            // 2. GRAVITY SCALING
            // Gravity is scaled by the square of the time scale for realistic parabolic trajectories
            if (currentScale < 0.99f && currentScale > 0.001f)
            {
                if (!_isSlowMoHandling) _isSlowMoHandling = true;
                
                _rb2d.gravityScale = _originalGravityScale * (currentScale * currentScale);
            }
            else if (_isSlowMoHandling && currentScale >= 0.99f)
            {
                // Restore original gravity when exiting slow motion
                _rb2d.gravityScale = _originalGravityScale;
                _isSlowMoHandling = false;
            }
        }
    }
}