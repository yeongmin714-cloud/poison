using System;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>Renderer-independent description of the active gameplay gas spray.</summary>
    public struct AeroGasSprayPayload
    {
        public Transform FollowTarget { get; private set; }
        public float Radius { get; private set; }
        public float Height { get; private set; }
        public float Density { get; private set; }
        public Color Tint { get; private set; }

        public AeroGasSprayPayload(Transform followTarget, float radius, float height, float density, Color tint)
        {
            FollowTarget = followTarget;
            Radius = Mathf.Max(0.1f, radius);
            Height = Mathf.Max(0.1f, height);
            Density = Mathf.Max(0f, density);
            Tint = tint;
        }
    }

    /// <summary>
    /// One-way gameplay-to-rendering event bridge. Systems publishes state; the optional isolated
    /// renderer consumes it. Core intentionally has no dependency on Systems or Rendering.
    /// </summary>
    public static class AeroGasSprayBridge
    {
        public static event Action<AeroGasSprayPayload> SprayUpdated;
        public static event Action SprayStopped;
        public static event Action<bool> RendererSessionChanged;

        public static bool IsSpraying { get; private set; }
        public static AeroGasSprayPayload CurrentPayload { get; private set; }
        public static bool IsolatedRendererEnabled { get; private set; }

        public static void Publish(AeroGasSprayPayload payload)
        {
            if (payload.FollowTarget == null)
            {
                Stop();
                return;
            }

            CurrentPayload = payload;
            IsSpraying = payload.Density > 0f && payload.Tint.a > 0f;
            if (IsSpraying) SprayUpdated?.Invoke(payload);
            else SprayStopped?.Invoke();
        }

        public static void Stop()
        {
            bool wasSpraying = IsSpraying;
            IsSpraying = false;
            if (wasSpraying) SprayStopped?.Invoke();
        }

        /// <summary>Called only by the opt-in session after it activates/restores its cloned URP asset.</summary>
        public static void SetRendererSessionEnabled(bool enabled)
        {
            if (IsolatedRendererEnabled == enabled) return;
            IsolatedRendererEnabled = enabled;
            RendererSessionChanged?.Invoke(enabled);
            if (enabled && IsSpraying) SprayUpdated?.Invoke(CurrentPayload);
        }
    }
}

// This bridge carries values and Transform references only; it does not reference an AERO package,
// the Systems assembly, or any renderer asset.