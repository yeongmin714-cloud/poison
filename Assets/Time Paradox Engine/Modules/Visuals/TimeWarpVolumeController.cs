using UnityEngine;
using UnityEngine.Rendering;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace TimeWarp.Modules.Visuals
{
    /// <summary>
    /// Drives the WEIGHT of a post-processing Volume from the engine's state.
    ///
    /// The engine deliberately authors no effects of its own - you build the Volume in your
    /// own art style and this simply fades it in and out. That keeps the look yours and
    /// works identically in URP and HDRP.
    ///
    /// "THE WEIGHT CHANGES BUT I SEE NOTHING": that is almost never this script. It is one
    /// of five setup conditions, every one of which fails in complete silence. The
    /// diagnostics at the bottom of this file check the four we can see from here, and the
    /// fifth (the camera) is listed in the warning text because we cannot test it without
    /// taking a hard dependency on URP.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    [AddComponentMenu("TimeWarp/Visuals/Time Warp Volume Controller")]
    public class TimeWarpVolumeController : MonoBehaviour
    {
        [Header("State Targets (Weight)")]
        [Range(0f, 1f)] public float targetWeightRewind = 1.0f;
        [Range(0f, 1f)] public float targetWeightTimeStop = 0.8f;

        [Range(0f, 1f)]
        [Tooltip("Weight reached at FULL slow motion - that is, when time has slowed all the way " +
                 "to the manager's Target Slow Mo Scale.")]
        public float maxWeightSlowMo = 0.4f;

        [Header("Blending")]
        [Tooltip("How fast the weight moves toward its target, in weight units per second.\n\n" +
                 "0 snaps instantly. A value around 4-8 smooths the transition so the effect does " +
                 "not pop when an anomaly starts or ends.")]
        [Range(0f, 30f)] public float blendSpeed = 6f;

        [Header("Diagnostics")]
        [Tooltip("Warn once at runtime if this volume cannot possibly be visible.")]
        public bool warnOnBrokenSetup = true;

        private Volume _anomalyVolume;
        private bool _warningIssued;

        private void Awake()
        {
            _anomalyVolume = GetComponent<Volume>();
            _anomalyVolume.weight = 0f;
        }

        private void Start()
        {
            if (warnOnBrokenSetup) CheckSetup();
        }

        private void Update()
        {
            if (TimeWarpManager.Instance == null) return;

            float target = EvaluateTargetWeight(TimeWarpManager.Instance);

            // Unscaled, because during a time stop scaled delta time is zero and the effect
            // would never finish fading in.
            _anomalyVolume.weight = blendSpeed <= 0f
                ? target
                : Mathf.MoveTowards(_anomalyVolume.weight, target, blendSpeed * Time.unscaledDeltaTime);
        }

        private float EvaluateTargetWeight(TimeWarpManager manager)
        {
            float currentScale = manager.CurrentGlobalTimeScale;

            // 1. REWIND - fixed weight, the strongest state.
            if (manager.GlobalTimeState == TimeState.Rewinding) return targetWeightRewind;

            // 2. TIME STOP - fixed weight.
            if (currentScale <= 0.01f) return targetWeightTimeStop;

            // 3. SLOW MOTION - scaled by HOW FAR INTO the slow motion we are.
            if (currentScale < 0.99f)
            {
                // This used to be a raw (1 - currentScale), which quietly meant you could
                // never reach the weight you configured. With Target Slow Mo Scale at 0.5,
                // currentScale bottoms out at 0.5, so the old formula peaked at half of
                // maxWeightSlowMo and the effect always looked weaker than the inspector
                // promised. Normalising against the manager's own target fixes that:
                // full slow motion now means exactly maxWeightSlowMo.
                float slowest = Mathf.Clamp01(manager.TargetSlowMoScale);
                float range = 1f - slowest;

                float t = range > 0.001f
                    ? Mathf.Clamp01((1f - currentScale) / range)
                    : 1f;

                return t * maxWeightSlowMo;
            }

            // 4. NORMAL TIME
            return 0f;
        }

        // ==========================================
        // DIAGNOSTICS
        // ==========================================
        /// <summary>
        /// Every one of these makes the volume invisible while the weight animates happily
        /// in the inspector - which is exactly why they are so confusing to debug.
        /// </summary>
        private void CheckSetup()
        {
            if (_warningIssued) return;

            var volume = GetComponent<Volume>();
            string problem = null;

            if (volume.profile == null && volume.sharedProfile == null)
            {
                problem = "the Volume has NO PROFILE assigned, so there are no effects to fade in";
            }
            else if (!volume.isGlobal && GetComponent<Collider>() == null)
            {
                problem = "the Volume is set to Local but has no Collider, so it covers no space at all";
            }
            else if (HasNoOverrides(volume))
            {
                problem = "the Volume profile contains no overrides - add Chromatic Aberration, " +
                          "Vignette, Color Adjustments or similar to it";
            }

            if (problem == null) return;

            _warningIssued = true;

            Debug.LogWarning(
                $"[TimeWarp] \"{name}\": the anomaly volume will not be visible because {problem}.\n\n" +
                $"If that is not it, check the CAMERA: in URP the camera has a Post Processing " +
                $"checkbox that must be ON, and a Volume Mask that must include this object's layer " +
                $"(currently \"{LayerMask.LayerToName(gameObject.layer)}\"). Also make sure post-processing " +
                $"is enabled on the Renderer asset itself.", this);
        }

        private static bool HasNoOverrides(Volume volume)
        {
            VolumeProfile profile = volume.profile != null ? volume.profile : volume.sharedProfile;
            if (profile == null) return true;

            // An empty profile is a very common half-finished setup.
            return profile.components == null || profile.components.Count == 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var volume = GetComponent<Volume>();
            if (volume == null) return;

            if (volume.profile == null && volume.sharedProfile == null)
            {
                Debug.LogWarning(
                    $"[TimeWarp] \"{name}\": no Volume Profile assigned. Create one on the Volume " +
                    $"component and add the effects you want the anomaly to show.", this);
            }
            else if (!volume.isGlobal && GetComponent<Collider>() == null)
            {
                Debug.LogWarning(
                    $"[TimeWarp] \"{name}\": the Volume is Local but has no Collider, so it affects " +
                    $"nothing. Tick Is Global, or add a trigger Collider.", this);
            }
        }
#endif
    }
}
