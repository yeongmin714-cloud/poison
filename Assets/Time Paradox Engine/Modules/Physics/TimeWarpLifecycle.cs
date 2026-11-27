using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace TimeWarp.Modules.Gameplay
{
    [RequireComponent(typeof(TimeWarpIdentity))]
    [AddComponentMenu("TimeWarp/Gameplay/Time Warp Lifecycle")]
    public class TimeWarpLifecycle : MonoBehaviour, ITimeWarpModule
    {
        [Header("Death Settings")]
        [Tooltip("If true, the object is disabled instead of destroyed so it can be rewound.")]
        public bool KeepInMemoryForRewind = true;
        
        [Header("Birth Settings")]
        [Tooltip("If true, rewinding past the moment of birth will hide the object.")]
        public bool DestroyBeforeBirth = true;

        private TimeWarpIdentity _identity;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Collider2D[] _colliders2D; // FIX: 2D Support

        private bool _isDead = false;
        private int _framesAlive = 0; 
        private int _maxBufferFrames;

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _colliders2D = GetComponentsInChildren<Collider2D>(true); // FIX: 2D Support
        }

        private void Start()
        {
            if (TimeWarpManager.Instance != null)
                _maxBufferFrames = Mathf.CeilToInt(TimeWarpManager.Instance.CaptureFramerate * TimeWarpManager.Instance.MaxRecordTime);
        }

        public void OnWarpFixedUpdate()
        {
            if (_identity == null || !_identity.IsRegistered || TimeWarpManager.Instance == null) return;

            // --- CULLING OPTIMIZATION ---
            // Do not age the object or process its death lifecycle if we are not recording its history
            if (!_identity.CanRecordHistory) return;

            bool isRewinding = _identity.IsRewinding || _identity.FractionalOffset > 0.001f;

            if (!isRewinding && _identity.EffectiveTimeScale >= 0f)
            {
                var state = TimeWarpManager.Instance.GlobalTimeState;
                if (state == TimeState.Normal || state == TimeState.SlowMotion || state == TimeState.PreAction || state == TimeState.PostAction)
                {
                    if (_framesAlive < _maxBufferFrames) _framesAlive++;
                    if (_isDead && _framesAlive >= _maxBufferFrames) HardDestroy();
                }
            }
        }

        public void OnWarpUpdate()
        {
            if (_identity == null || !_identity.IsRegistered || TimeWarpManager.Instance == null) return;

            bool isRewinding = _identity.IsRewinding || _identity.FractionalOffset > 0.001f;

            if (isRewinding)
            {
                if (_isDead) Revive(); 
                if (DestroyBeforeBirth)
                {
                    int framesRewound = Mathf.FloorToInt(_identity.FractionalOffset);
                    if (framesRewound >= _framesAlive) ToggleVisuals(false); 
                    else ToggleVisuals(true);  
                }
            }
        }

        /// <summary>
        /// Call this method instead of Destroy(gameObject) to safely kill an object.
        /// </summary>
        public void Die()
        {
            if (_isDead) return;
            
            // --- CULLING FIX ---
            // If the object is killed while outside its required recording zone, 
            // we don't care about its history. Hard destroy it to save memory.
            if (!KeepInMemoryForRewind || !_identity.CanRecordHistory) 
            { 
                HardDestroy(); 
                return; 
            }

            _isDead = true;
            ToggleVisuals(false);
            if (_framesAlive == 0) HardDestroy();
        }

        private void Revive() 
        { 
            _isDead = false; 
            ToggleVisuals(true); 
        }
        
        private void HardDestroy() 
        { 
            if (gameObject != null) Destroy(gameObject); 
        }

        private void ToggleVisuals(bool state)
        {
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].enabled = state;
            for (int i = 0; i < _colliders.Length; i++) if (_colliders[i] != null) _colliders[i].enabled = state;
            for (int i = 0; i < _colliders2D.Length; i++) if (_colliders2D[i] != null) _colliders2D[i].enabled = state; // FIX: 2D Collisions

            if (_identity.CachedRb != null)
            {
                if (!state) { _identity.CachedRb.isKinematic = true; _identity.CachedRb.detectCollisions = false; }
                else { _identity.CachedRb.detectCollisions = true; }
            }
            
            // FIX: 2D Physics Support
            if (_identity.CachedRb2D != null)
            {
                if (!state) { _identity.CachedRb2D.simulated = false; }
                else { _identity.CachedRb2D.simulated = true; }
            }
        }
    }
}