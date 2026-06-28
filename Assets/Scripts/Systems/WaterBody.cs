using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core.Utils;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 3.6.6: Procedural water body with wave animation and slow-movement collision.
    /// Creates a flat semi-transparent Plane at Start with a URP Lit material,
    /// animates its Y position with a sine wave, and applies a slow factor (0.5x)
    /// to objects tagged "Player" that enter its trigger collider.
    /// </summary>
    public class WaterBody : MonoBehaviour
    {
        [Header("Dimensions")]
        [SerializeField] private float _radius = 3f;
        [SerializeField] private float _surfaceY = 0f;

        [Header("Wave Animation")]
        [SerializeField] private float _waveSpeed = 1.5f;
        [SerializeField] private float _waveAmplitude = 0.05f;

        [Header("Slow Collision")]
        [SerializeField] private float _slowFactor = 0.5f;
        [SerializeField] private string _playerTag = "Player";

        [Header("Visuals")]
        [SerializeField] private Color _waterColor = new Color(0.2f, 0.5f, 0.8f, 0.6f);

        private GameObject _waterSurface;
        private MeshRenderer _surfaceRenderer;
        private Material _surfaceMaterial;
        private GameObject _collisionVolume;

        // FIX: Cache Rigidbody and its entry speed on trigger enter to avoid:
        //    1) Per-frame GetComponent<Rigidbody> calls (performance)
        //    2) Per-frame velocity *= _slowFactor causing exponential decay to zero (BUG)
        private readonly Dictionary<Rigidbody, float> _entrySpeeds = new Dictionary<Rigidbody, float>();

        /// <summary>Public accessor for the water surface GameObject (for testing).</summary>
        public GameObject WaterSurface => _waterSurface;

        /// <summary>Public accessor for the collision volume GameObject (for testing).</summary>
        public GameObject CollisionVolume => _collisionVolume;

        /// <summary>Public accessor for the current surface Material (for testing).</summary>
        public Material SurfaceMaterial => _surfaceMaterial;

        /// <summary>Public accessor for the MeshRenderer on the water surface (for testing).</summary>
        public MeshRenderer SurfaceRenderer => _surfaceRenderer;

        /// <summary>Radius of this water body.</summary>
        public float Radius => _radius;

        /// <summary>Wave speed in Hz.</summary>
        public float WaveSpeed => _waveSpeed;

        /// <summary>Wave amplitude in Unity units.</summary>
        public float WaveAmplitude => _waveAmplitude;

        /// <summary>Slow factor applied on collision (0.5 = half speed).</summary>
        public float SlowFactor => _slowFactor;

        /// <summary>Color of the water material.</summary>
        public Color WaterColor => _waterColor;

        /// <summary>
        /// Upgrades the water surface material with reflection probe keywords,
        /// metallic=0.0, and smoothness=0.8 for high-quality reflections.
        /// Called by Phase G1-03 editor tooling.
        /// </summary>
        public void UpgradeReflectionMaterial()
        {
            if (_surfaceMaterial == null) return;
            _surfaceMaterial.EnableKeyword("_REFLECTION_PROBE_BLENDING");
            _surfaceMaterial.EnableKeyword("_REFLECTION_PROBE_BOX_PROJECTION");
            _surfaceMaterial.SetFloat("_Metallic", 0.0f);
            _surfaceMaterial.SetFloat("_Smoothness", 0.8f);
            _surfaceMaterial.SetFloat("_Surface", 1f);
            _surfaceMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private void Awake()
        {
            ConstructWaterBody();
        }

        private void ConstructWaterBody()
        {
            // --- Create water surface (flat plane, rotated to lie flat) ---
            _waterSurface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _waterSurface.name = $"{gameObject.name}_WaterSurface";
            _waterSurface.transform.SetParent(transform, false);

            // Rotate 90 degrees on X so the plane lies flat (default Plane faces Z+)
            _waterSurface.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _waterSurface.transform.localPosition = Vector3.zero;

            // Scale: Plane is 10x10 units by default; scale uniformly
            float scale = _radius * 2f / 10f;
            _waterSurface.transform.localScale = new Vector3(scale, scale, scale);

            // Remove the default collider from the visual plane — we'll add our own
            DestroyImmediate(_waterSurface.GetComponent<MeshCollider>());

            // --- Create semi-transparent URP Lit material ---
            _surfaceRenderer = _waterSurface.GetComponent<MeshRenderer>();
            _surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _surfaceRenderer.receiveShadows = false;

            _surfaceMaterial = MaterialHelper.CreateLitMaterial(_waterColor, $"{gameObject.name}_WaterMat");
            if (_surfaceMaterial != null)
            {
                // Configure for URP transparent rendering
                _surfaceMaterial.SetFloat("_Surface", 1f);        // Transparent
                _surfaceMaterial.SetFloat("_BlendMode", 0f);      // Alpha
                _surfaceMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _surfaceMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _surfaceMaterial.SetFloat("_ZWrite", 0f);
                _surfaceMaterial.SetFloat("_AlphaClip", 0f);
                _surfaceMaterial.renderQueue = 3000;
                _surfaceMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _surfaceMaterial.EnableKeyword("_BLENDMODE_ALPHA");

                // Ensure alpha is set
                Color c = _surfaceMaterial.color;
                c.a = _waterColor.a;
                _surfaceMaterial.color = c;

                _surfaceRenderer.material = _surfaceMaterial;
            }

            // --- Create collision volume (invisible box collider for trigger detection) ---
            _collisionVolume = new GameObject($"{gameObject.name}_WaterVolume");
            _collisionVolume.transform.SetParent(transform, false);
            _collisionVolume.transform.localPosition = Vector3.zero;

            BoxCollider collider = _collisionVolume.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            float volumeSize = _radius * 2f;
            collider.size = new Vector3(volumeSize, 1f, volumeSize);

            // Set tag for detection
            _collisionVolume.tag = "Water";

            // --- Position at surface Y ---
            transform.position = new Vector3(transform.position.x, _surfaceY, transform.position.z);
        }

        private void Update()
        {
            if (_waterSurface == null) return;

            // Sine wave animation on Y position
            float waveOffset = Mathf.Sin(Time.time * _waveSpeed) * _waveAmplitude;
            Vector3 pos = _waterSurface.transform.localPosition;
            pos.y = waveOffset;
            _waterSurface.transform.localPosition = pos;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && !_entrySpeeds.ContainsKey(rb))
            {
                // Cache the entry speed so we can clamp velocity to _slowFactor * entry speed
                _entrySpeeds[rb] = rb.linearVelocity.magnitude;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && _entrySpeeds.TryGetValue(rb, out float entrySpeed))
            {
                // FIX: Clamp velocity magnitude to _slowFactor * entry speed instead of
                // per-frame multiplication.  This keeps speed at a consistent 50% of the
                // entry speed rather than decaying exponentially toward zero.
                Vector3 velocity = rb.linearVelocity;
                float maxSpeed = entrySpeed * _slowFactor;
                if (velocity.magnitude > maxSpeed)
                {
                    rb.linearVelocity = velocity.normalized * maxSpeed;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;

            // Remove cached speed — player has left the water
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                _entrySpeeds.Remove(rb);
            }
        }

        private void OnDestroy()
        {
            if (_surfaceMaterial != null)
            {
                Destroy(_surfaceMaterial);
                _surfaceMaterial = null;
            }

            _entrySpeeds.Clear();
        }
    }
}