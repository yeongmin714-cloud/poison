using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace TimeWarp.Gameplay
{
    [RequireComponent(typeof(TimeWarpIdentity))]
    [RequireComponent(typeof(Rigidbody))]
    public class TimeBullet : MonoBehaviour
    {
        [Header("Bullet Settings")]
        public float Speed = 30f;
        public float Lifetime = 5f;

        [Header("Ballistics")]
        [Tooltip("Check if this is a grenade/arrow and should fall. Uncheck for lasers/rifle bullets.")]
        public bool UseGravity = false;
        public float GravityMultiplier = 1f;

        private TimeWarpIdentity _identity;
        private Rigidbody _rb;
        private Collider[] _colliders;
        private Renderer[] _renderers;
        private TrailRenderer[] _trails;

        private Vector3 _initialForward;
        private float _ageInSeconds = 0f;
        
        private float _lastFractionalOffset = 0f;
        private bool _isPreBirthHidden = false;

        private void Start()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            _rb = GetComponent<Rigidbody>();
            
            // 1. We force Unity to give us full control over the bullet's physics
            // This allows us to avoid any gravity glitches after returning from Slow-Mo
            _rb.isKinematic = true; 
            _rb.useGravity = false;

            _colliders = GetComponentsInChildren<Collider>();
            _renderers = GetComponentsInChildren<Renderer>();
            _trails = GetComponentsInChildren<TrailRenderer>();

            _initialForward = transform.forward;
        }

        private void Update()
        {
            if (_identity == null || TimeWarpManager.Instance == null) return;

            HandleTimelineDivergence();

            // If time is rewinding or the bullet is "pre-birth" - we pause its movement
            // (Then TimeWarpManager takes control and rewinds position from the buffer)
            if (_identity.IsRewinding || _isPreBirthHidden) return;

            float timeScale = _identity.EffectiveTimeScale;
            float scaledDelta = Time.deltaTime * timeScale;

            if (timeScale > 0f)
            {
                _ageInSeconds += scaledDelta;

                // 2. PERFECT BALLISTICS: Velocity is strictly dependent on the bullet's age.
                // It will always fly perfectly, even after returning from Slow-Mo 50 times.
                Vector3 currentVelocity = _initialForward * Speed;
                
                if (UseGravity)
                {
                    currentVelocity += Physics.gravity * GravityMultiplier * _ageInSeconds;
                    if (currentVelocity != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(currentVelocity);
                    }
                }

                // Move the bullet
                transform.position += currentVelocity * scaledDelta;

                if (_ageInSeconds >= Lifetime)
                {
                    Destroy(gameObject);
                }
            }
        }

        private void HandleTimelineDivergence()
        {
            float currentOffset = _identity.FractionalOffset;
            
            // 3. TIMELINE DIVERGENCE DETECTION (Stopping the rewind)
            if (currentOffset == 0f && _lastFractionalOffset > 0f)
            {
                if (_isPreBirthHidden)
                {
                    // "GHOST BULLETS" SOLUTION: 
                    // The player rewound time before the moment of the shot and stopped rewinding (created a new timeline).
                    // This bullet will NEVER happen. We destroy it permanently from RAM!
                    Destroy(gameObject);
                    return;
                }
                else
                {
                    // We rewound time, but didn't erase the bullet from history. 
                    // We rejuvenate the bullet so its gravity and momentum reset to the correct state!
                    _ageInSeconds -= (_lastFractionalOffset / TimeWarpManager.Instance.CaptureFramerate);
                    if (_ageInSeconds < 0f) _ageInSeconds = 0f;
                }
            }

            _lastFractionalOffset = currentOffset;

            // 4. Check if the bullet should be invisible now (we rewound to the moment before its birth)
            float rewindSeconds = currentOffset / TimeWarpManager.Instance.CaptureFramerate;
            bool shouldBeHidden = rewindSeconds >= _ageInSeconds;

            if (shouldBeHidden != _isPreBirthHidden)
            {
                _isPreBirthHidden = shouldBeHidden;
                SetVisibility(!_isPreBirthHidden);
            }
        }

        private void SetVisibility(bool visible)
        {
            foreach (var r in _renderers) if (r != null) r.enabled = visible;
            foreach (var c in _colliders) if (c != null) c.enabled = visible;
            foreach (var t in _trails) 
            {
                if (t != null) 
                {
                    t.emitting = visible;
                    if (!visible) t.Clear();
                }
            }
        }
    }
}