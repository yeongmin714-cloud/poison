using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Rendering
{
    /// <summary>
    /// Optional player-side subscriber for the Core gameplay event bridge. Add only to the test
    /// player/scene that uses AeroIsolatedRendererSession; it owns a single local gas volume.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AeroGasSprayRuntimeHost : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _density = 1f;
        [SerializeField, Min(0.01f)] private float _fadeOutSeconds = 0.35f;

        private AeroLocalizedGasVolume _volume;
        private bool _rendererEnabled;
        private bool _destroyWhenFaded;

        public AeroLocalizedGasVolume Volume { get { return _volume; } }

        private void OnEnable()
        {
            AeroGasSprayBridge.SprayUpdated += HandleSprayUpdated;
            AeroGasSprayBridge.SprayStopped += HandleSprayStopped;
            AeroGasSprayBridge.RendererSessionChanged += HandleRendererSessionChanged;
            _rendererEnabled = AeroGasSprayBridge.IsolatedRendererEnabled;
            if (_rendererEnabled && AeroGasSprayBridge.IsSpraying)
                HandleSprayUpdated(AeroGasSprayBridge.CurrentPayload);
        }

        private void OnDisable()
        {
            AeroGasSprayBridge.SprayUpdated -= HandleSprayUpdated;
            AeroGasSprayBridge.SprayStopped -= HandleSprayStopped;
            AeroGasSprayBridge.RendererSessionChanged -= HandleRendererSessionChanged;
            RemoveVolumeImmediately();
        }

        private void Update()
        {
            if (_destroyWhenFaded && (_volume == null || _volume.Alpha <= 0.001f))
                RemoveVolumeImmediately();
        }

        private void HandleRendererSessionChanged(bool enabled)
        {
            _rendererEnabled = enabled;
            if (!enabled) RemoveVolumeImmediately();
            else if (AeroGasSprayBridge.IsSpraying)
                HandleSprayUpdated(AeroGasSprayBridge.CurrentPayload);
        }

        private void HandleSprayUpdated(AeroGasSprayPayload payload)
        {
            if (!_rendererEnabled || payload.FollowTarget == null || payload.Density <= 0f || payload.Tint.a <= 0f)
            {
                FadeAndRemove();
                return;
            }

            if (_volume == null)
            {
                var emitter = new GameObject("AERO_Localized_Gas_Emitter");
                emitter.transform.SetParent(transform, false);
                _volume = emitter.AddComponent<AeroLocalizedGasVolume>();
            }

            _destroyWhenFaded = false;
            _volume.Configure(payload.FollowTarget, payload.Radius, payload.Height,
                _density * payload.Density, payload.Tint, _fadeOutSeconds);
            _volume.SetEmitting(true);
        }

        private void HandleSprayStopped()
        {
            FadeAndRemove();
        }

        private void FadeAndRemove()
        {
            if (_volume == null) return;
            _volume.SetEmitting(false);
            _destroyWhenFaded = true;
        }

        private void RemoveVolumeImmediately()
        {
            _destroyWhenFaded = false;
            if (_volume == null) return;
            GameObject emitter = _volume.gameObject;
            _volume = null;
            if (Application.isPlaying) Destroy(emitter);
            else DestroyImmediate(emitter);
        }
    }
}
