using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Rendering
{
    /// <summary>
    /// Runtime data source for the project's isolated custom local gas renderer.
    /// It is not consumed by Mirza AERO's fog material and does not alter vendor materials or global fog state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AeroLocalizedGasVolume : MonoBehaviour
    {
        private static readonly List<AeroLocalizedGasVolume> ActiveVolumes = new List<AeroLocalizedGasVolume>();

        [SerializeField] private Transform _followTarget;
        [SerializeField, Min(0.1f)] private float _radius = 4f;
        [SerializeField, Min(0.1f)] private float _height = 2f;
        [SerializeField, Min(0f)] private float _density = 1.2f;
        [SerializeField] private Color _color = new Color(0.55f, 0.82f, 0.42f, 1f);
        [SerializeField, Min(0.01f)] private float _fadeOutSeconds = 0.25f;
        [SerializeField, Min(0f)] private float _plumeLength = 2.5f;
        [SerializeField, Min(0f)] private float _noiseScale = 0.7f;

        private float _alpha;
        private bool _emitting;

        public Transform FollowTarget { get { return _followTarget; } }
        public float Radius { get { return _radius; } }
        public float Height { get { return _height; } }
        public float Density { get { return _density; } }
        public Color Color { get { return _color; } }
        public float Alpha { get { return _alpha; } }
        public bool IsEmitting { get { return _emitting; } }
        public float PlumeLength { get { return _plumeLength; } }
        public float NoiseScale { get { return _noiseScale; } }

        public Vector3 WorldPosition
        {
            get { return _followTarget != null ? _followTarget.position : transform.position; }
        }

        public Vector3 WorldDirection
        {
            get
            {
                Transform directionSource = _followTarget != null ? _followTarget : transform;
                Vector3 direction = directionSource.forward;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            }
        }

        private void OnEnable()
        {
            if (!ActiveVolumes.Contains(this)) ActiveVolumes.Add(this);
        }

        private void OnDisable()
        {
            ActiveVolumes.Remove(this);
            _emitting = false;
            _alpha = 0f;
        }

        private void Update()
        {
            float rate = Mathf.Max(0.01f, _fadeOutSeconds);
            _alpha = Mathf.MoveTowards(_alpha, _emitting ? 1f : 0f, Time.deltaTime / rate);
        }

        /// <summary>Configures a localized field; the material and renderer assets remain untouched.</summary>
        public void Configure(Transform followTarget, float radius, float height, float density,
            Color color, float fadeOutSeconds = 0.25f, float plumeLength = 2.5f, float noiseScale = 0.7f)
        {
            _followTarget = followTarget;
            _radius = Mathf.Max(0.1f, radius);
            _height = Mathf.Max(0.1f, height);
            _density = Mathf.Max(0f, density);
            _color = color;
            _fadeOutSeconds = Mathf.Max(0.01f, fadeOutSeconds);
            _plumeLength = Mathf.Max(0f, plumeLength);
            _noiseScale = Mathf.Max(0f, noiseScale);
        }

        /// <summary>Starts or fades out emission without destroying wisps immediately.</summary>
        public void SetEmitting(bool emitting)
        {
            bool wasEmitting = _emitting;
            _emitting = emitting;
            if (emitting && !wasEmitting && _alpha <= 0.001f)
                _alpha = 1f;
            if (emitting && !isActiveAndEnabled && gameObject.activeInHierarchy)
                enabled = true;
        }

        /// <summary>
        /// CPU reference for the shader's bounded world-space shape, useful for tests and debug tools.
        /// Outside the radial/height mask the returned density is exactly zero.
        /// </summary>
        public float EvaluateWorldDensity(Vector3 worldPosition, float noiseMultiplier = 1f)
        {
            if (_alpha <= 0f || _density <= 0f) return 0f;
            Vector3 relative = worldPosition - WorldPosition;
            float radialDistance = relative.magnitude;
            float local = 1f - SmoothStepEdges(_radius * 0.72f, _radius, radialDistance);
            Vector3 axis = WorldDirection;
            float forward = Vector3.Dot(relative, axis);
            Vector3 radialVector = relative - axis * forward;
            float plumeRadius = Mathf.Max(0.25f, _radius * 0.48f);
            float plumeAxis = SmoothStepEdges(-_radius * 0.45f, 0f, forward) *
                (1f - SmoothStepEdges(_plumeLength, _plumeLength + plumeRadius, forward));
            float plume = plumeAxis * (1f - SmoothStepEdges(plumeRadius * 0.42f, plumeRadius, radialVector.magnitude));
            float vertical = 1f - SmoothStepEdges(_height * 0.55f, _height, Mathf.Abs(relative.y));
            float shaped = Mathf.Clamp01(Mathf.Max(local * 0.55f, plume) * vertical);
            return shaped * Mathf.Clamp01(_density * Mathf.Clamp(noiseMultiplier, 0f, 2f)) * _alpha;
        }

        private static float SmoothStepEdges(float edge0, float edge1, float value)
        {
            if (edge0 == edge1) return value < edge0 ? 0f : 1f;

            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Finds the nearest visible, non-zero local gas field for a camera.</summary>
        public static bool TryGetNearest(Vector3 cameraPosition, out AeroLocalizedGasVolume volume)
        {
            volume = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = ActiveVolumes.Count - 1; i >= 0; --i)
            {
                AeroLocalizedGasVolume candidate = ActiveVolumes[i];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate._alpha <= 0.001f ||
                    candidate._density <= 0f)
                    continue;

                float distance = (candidate.WorldPosition - cameraPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    volume = candidate;
                }
            }
            return volume != null;
        }

        /// <summary>Creates/configures a bridge component on an existing gas emitter/cloud object.</summary>
        public static AeroLocalizedGasVolume Attach(GameObject host, Transform followTarget, float radius,
            float height, float density, Color color, bool startEmitting = true)
        {
            if (host == null) return null;
            AeroLocalizedGasVolume volume = host.GetComponent<AeroLocalizedGasVolume>();
            if (volume == null) volume = host.AddComponent<AeroLocalizedGasVolume>();
            volume.Configure(followTarget, radius, height, density, color);
            volume.SetEmitting(startEmitting);
            return volume;
        }

        public static void ResetRegistryForTests()
        {
            ActiveVolumes.Clear();
        }
    }
}

// Mirza AERO 1.8.0's fullscreen fog shader has its own constant-density hook. This project
// compositor does not invoke that hook; this interface keeps gameplay decoupled from the
// project-owned isolated local volume and from vendor package implementation details.
namespace ProjectName.Rendering
{
    public interface IAeroLocalizedGasBridge
    {
        void SetEmitting(bool emitting);
        void Configure(Transform followTarget, float radius, float height, float density, Color color);
    }
}

namespace ProjectName.Rendering
{
    /// <summary>Thin adapter for gameplay callers that should remain decoupled from the renderer implementation.</summary>
    public sealed class AeroLocalizedGasVolumeBridge : MonoBehaviour, IAeroLocalizedGasBridge
    {
        private AeroLocalizedGasVolume _volume;

        private void Awake()
        {
            _volume = GetComponent<AeroLocalizedGasVolume>();
            if (_volume == null) _volume = gameObject.AddComponent<AeroLocalizedGasVolume>();
        }

        public void Configure(Transform followTarget, float radius, float height, float density, Color color)
        {
            if (_volume == null) _volume = GetComponent<AeroLocalizedGasVolume>();
            if (_volume == null) _volume = gameObject.AddComponent<AeroLocalizedGasVolume>();
            _volume.Configure(followTarget, radius, height, density, color);
        }

        public void SetEmitting(bool emitting)
        {
            if (_volume == null) _volume = GetComponent<AeroLocalizedGasVolume>();
            if (_volume != null) _volume.SetEmitting(emitting);
        }
    }
}

// This project-owned bridge is deliberately opt-in. Add AeroLocalizedGasRendererFeature only
// to a cloned, test-only URP renderer data asset; do not add it to the shared renderer.