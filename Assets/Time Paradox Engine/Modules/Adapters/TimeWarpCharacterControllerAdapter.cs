using UnityEngine;
using TimeWarp.Core;

namespace TimeWarp.Modules.Adapters
{
    [RequireComponent(typeof(TimeWarpIdentity))]
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("TimeWarp/Adapters/Character Controller Adapter")]
    public class TimeWarpCharacterControllerAdapter : MonoBehaviour, ITimeWarpModule, ITimeWarpCharacterAdapter
    {
        private TimeWarpIdentity _identity;
        private CharacterController _controller;
        
        // Remembers if our system disabled the controller, to avoid accidentally enabling it 
        // if, for example, a player death script killed it.
        private bool _wasControllerEnabledByUs = false;

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            _controller = GetComponent<CharacterController>();
        }

        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate()
        {
            if (!_identity.IsRegistered) return;

            // GUARD: We disable the CharacterController ONLY during rewind, 
            // so the buffer can move the player backwards without fighting the Unity engine for position.
            bool isRewinding = _identity.IsRewinding || _identity.FractionalOffset > 0.001f;

            if (isRewinding)
            {
                if (_controller.enabled)
                {
                    _controller.enabled = false;
                    _wasControllerEnabledByUs = true;
                }
            }
            else
            {
                if (!_controller.enabled && _wasControllerEnabledByUs)
                {
                    _controller.enabled = true;
                    _wasControllerEnabledByUs = false;
                }
            }
        }

        // ==========================================
        // INTERFACE IMPLEMENTATION FOR MOVEMENT SCRIPTS
        // ==========================================
        
        public float GetLocalDeltaTime()
        {
            if (_identity == null || !_identity.IsRegistered) return Time.deltaTime;
            
            // If time is rewinding or completely stopped, the time for the player script is 0.
            // This causes gravity and movement to stop working (perfect freeze).
            if (_identity.IsRewinding || _identity.FractionalOffset > 0.001f || _identity.EffectiveTimeScale <= 0.001f) 
            {
                return 0f; 
            }
            
            // In Slow-Mo, we provide multiplied time so gravity and movement slow down naturally!
            return Time.deltaTime * _identity.EffectiveTimeScale;
        }

        public float GetLocalFixedDeltaTime()
        {
            if (_identity == null || !_identity.IsRegistered) return Time.fixedDeltaTime;
            
            if (_identity.IsRewinding || _identity.FractionalOffset > 0.001f || _identity.EffectiveTimeScale <= 0.001f) 
            {
                return 0f;
            }
            
            return Time.fixedDeltaTime * _identity.EffectiveTimeScale;
        }

        public bool IsInputLocked()
        {
            if (_identity == null || !_identity.IsRegistered) return false;
            
            // The player loses control when time is stopped or rewinding
            return _identity.IsRewinding || _identity.FractionalOffset > 0.001f || _identity.EffectiveTimeScale <= 0.001f;
        }
    }
}