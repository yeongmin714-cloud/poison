using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Managers;
using TimeWarp.Core.Data;

namespace TimeWarp.Modules.Physics.Dynamics
{
    [RequireComponent(typeof(TimeWarpIdentity))]
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-200)]
    public class TimeBody : MonoBehaviour, ITimeWarpModule
    {
        private Rigidbody _rb;
        private TimeWarpIdentity _identity;
        private KineticAccumulator _accumulator; 
        
        private bool _wasKinematicOriginally;
        private RigidbodyInterpolation _originalInterpolation;
        private bool _wasFrozenLastFrame = false;

        private float _lastTimeScale = 1.0f;
        private bool _isSlowMoHandling = false;
        private bool _wasUsingGravity;

        private Vector3 _baseVelocityBeforeFreeze;
        private Vector3 _baseAngularVelocityBeforeFreeze;
        private bool _didRewindDuringFreeze = false;
        
        // Flag to synchronize the initial time scale when the object is created
        private bool _isTimeScaleInitialized = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _identity = GetComponent<TimeWarpIdentity>();
            _accumulator = GetComponent<KineticAccumulator>(); 
            
            // Remember original state so we can restore it later
            _wasKinematicOriginally = _rb.isKinematic;
            _originalInterpolation = _rb.interpolation;
        }

        public void OnWarpFixedUpdate() 
        {
            if (!_identity.IsRegistered || _rb == null) return;
            
            if (_identity.IsRewinding) _didRewindDuringFreeze = true; 
            
            SyncFreezeState();
            HandleLocalSlowMotion(); 
        }

        public void OnWarpUpdate() { }

        private void SyncFreezeState()
        {
            bool isCurrentlyFrozen = (_identity.EffectiveTimeScale <= 0f || _identity.FractionalOffset > 0f);

            if (isCurrentlyFrozen == _wasFrozenLastFrame) return;

            // ==========================================
            // ENTERING FREEZE STATE
            // ==========================================
            if (isCurrentlyFrozen && !_wasFrozenLastFrame)
            {
                _wasKinematicOriginally = _rb.isKinematic;
                _originalInterpolation = _rb.interpolation;

                float currentSafeScale = Mathf.Max(_lastTimeScale, 0.01f);
                _baseVelocityBeforeFreeze = _rb.linearVelocity / currentSafeScale;
                _baseAngularVelocityBeforeFreeze = _rb.angularVelocity / currentSafeScale;

                _rb.isKinematic = true; 
                _rb.interpolation = RigidbodyInterpolation.None; 
            }
            // ==========================================
            // EXITING FREEZE STATE
            // ==========================================
            else if (!isCurrentlyFrozen && _wasFrozenLastFrame)
            {
                _rb.position = transform.position;
                _rb.rotation = transform.rotation;
                
                _rb.isKinematic = _wasKinematicOriginally; 
                _rb.interpolation = _originalInterpolation; 

                if (!_wasKinematicOriginally)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;

                    if ((_identity.Channels & TimeChannels.Velocity) != 0)
                    {
                        _rb.WakeUp();
                        
                        if (_didRewindDuringFreeze)
                        {
                            if (_identity.VelocitySettings.RestoreRecordedVelocity && _identity.Buffer.Count > 0)
                            {
                                int currentIndex = _identity.Buffer.GetIndex(1); 
                                VelocitySnapshot snap = _identity.Buffer.VelocityHistory[currentIndex];
                                
                                _rb.linearVelocity = snap.linearVelocity;
                                _rb.angularVelocity = snap.angularVelocity;
                            }
                            _lastTimeScale = Mathf.Max(_identity.EffectiveTimeScale, 0.001f);
                        }
                        else
                        {
                            _rb.linearVelocity = _baseVelocityBeforeFreeze;
                            _rb.angularVelocity = _baseAngularVelocityBeforeFreeze;
                            _lastTimeScale = 1.0f; 
                        }
                    }
                }
                else
                {
                    _lastTimeScale = Mathf.Max(_identity.EffectiveTimeScale, 0.001f);
                }

                if (_accumulator != null) _accumulator.ReleaseEnergy();
                
                _didRewindDuringFreeze = false;
            }

            _wasFrozenLastFrame = isCurrentlyFrozen;
        }

        private void HandleLocalSlowMotion()
        {
            if (_rb.isKinematic || _wasFrozenLastFrame) return;
            
            float currentScale = _identity.EffectiveTimeScale;

            // ==========================================
            // TACTICAL 1-FRAME DELAY
            // ==========================================
            if (!_isTimeScaleInitialized)
            {
                // We force the system to think it just came from normal time.
                _lastTimeScale = 1.0f;
                _isTimeScaleInitialized = true;
                
                // Exit immediately! The object flies with its raw, normal parameters 
                // for exactly one physics tick. It will catch the Slow-Mo in the next frame.
                return; 
            }

            // ==========================================
            // NORMAL SCALING FOR EXISTING OBJECTS
            // ==========================================
            if (Mathf.Abs(currentScale - _lastTimeScale) > 0.001f)
            {
                float safeLast = Mathf.Max(_lastTimeScale, 0.01f);
                float safeCurrent = Mathf.Max(currentScale, 0.01f);
                
                float ratio = safeCurrent / safeLast;
                if (ratio > 10f) ratio = 10f; // Safety clamp
                
                _rb.linearVelocity *= ratio;
                _rb.angularVelocity *= ratio;

                _lastTimeScale = currentScale;
            }

            // Custom Gravity handling for Slow Motion
            if (currentScale < 0.99f && currentScale > 0.001f)
            {
                if (!_isSlowMoHandling)
                {
                    _wasUsingGravity = _rb.useGravity;
                    if (_wasUsingGravity) _rb.useGravity = false; // Turn off default gravity
                    _isSlowMoHandling = true;
                }
                
                if (_wasUsingGravity) 
                    _rb.AddForce(UnityEngine.Physics.gravity * (currentScale * currentScale), ForceMode.Acceleration);
            }
            else if (_isSlowMoHandling && currentScale >= 0.99f)
            {
                if (_wasUsingGravity) _rb.useGravity = true;
                _isSlowMoHandling = false;
            }
        }
    }
}