using UnityEngine;
using TimeWarp.Core; 
using TimeWarp.Modules.Gameplay; 

namespace TimeWarp.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public class BulletFlight : MonoBehaviour
    {
        [Header("Flight Settings")]
        public float speed = 30f;
        public float lifetime = 3f;

        private float _age = 0f;
        private TimeWarpIdentity _identity;
        private TimeWarpLifecycle _lifecycle;

        void Start()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            _lifecycle = GetComponent<TimeWarpLifecycle>();

            // Instead of moving the bullet every frame, we apply physical velocity ONCE at startup
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = transform.forward * speed;
            }
        }

        void Update()
        {
            // If time is rewinding, our script must stay silent
            if (_identity != null && _identity.IsRewinding) return;

            // We calculate the bullet's lifetime taking the time scale into account
            float timeScale = _identity != null ? _identity.EffectiveTimeScale : 1f;
            _age += Time.deltaTime * timeScale;
            
            if (_age >= lifetime)
            {
                if (_lifecycle != null) _lifecycle.Die();
                else Destroy(gameObject);
            }
        }
    }
}