using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace TimeWarp.Modules.UI
{
    /// <summary>
    /// Drop-in "rewind meter" - the mana-bar style readout that is usually the first
    /// thing a project needs on top of the engine.
    ///
    /// NO TextMeshPro DEPENDENCY BY DESIGN: instead of referencing TMP (and forcing that
    /// package on everyone), the formatted string is pushed through the OnLabelChanged
    /// UnityEvent. Hook a TMP_Text.text setter to it in the inspector and it just works,
    /// with a plain UI.Text, or with nothing at all.
    /// </summary>
    /// <summary>
    /// Concrete UnityEvent subclass. A bare UnityEvent&lt;string&gt; does not show up in the
    /// inspector - Unity only serialises a named, [Serializable] subclass.
    /// </summary>
    [System.Serializable]
    public class TimeWarpStringEvent : UnityEvent<string> { }

    [AddComponentMenu("TimeWarp/UI/Time Warp Rewind Bar")]
    public class TimeWarpRewindBar : MonoBehaviour
    {
        public enum BarSource
        {
            /// <summary>How much of the duration-limited global action is left (needs Use Max Duration).</summary>
            ActionTimeRemaining,

            /// <summary>How far into recorded history a tracked object currently is.</summary>
            RewindDepth,

            /// <summary>How much recorded history the tracked object has available.</summary>
            AvailableHistory
        }

        [Header("What to show")]
        public BarSource Source = BarSource.ActionTimeRemaining;

        [Tooltip("Object whose history feeds the bar. Only used by the Rewind Depth and " +
                 "Available History modes. Leave empty to use the first registered object.")]
        public TimeWarpIdentity Target;

        [Header("Output (all optional)")]
        [Tooltip("Any Image set to Filled - its fillAmount is driven 0..1.")]
        public Image FillImage;

        [Tooltip("Slider driven 0..1. Interactable is forced off - this is a readout.")]
        public Slider FillSlider;

        [Tooltip("Optional group faded out while the bar is full and idle.")]
        public CanvasGroup FadeGroup;

        [Header("Behaviour")]
        [Tooltip("Smoothing speed. 0 snaps instantly.")]
        [Range(0f, 30f)] public float SmoothSpeed = 12f;

        [Tooltip("Hide the bar while it is full and no action is running.")]
        public bool HideWhenIdle = false;

        [Range(0f, 1f)] public float IdleAlpha = 0.25f;
        [Range(0f, 30f)] public float FadeSpeed = 8f;

        [Header("Colour")]
        public bool TintByState = true;
        public Color NormalColor = new Color(0.35f, 0.80f, 1.00f);
        public Color RewindColor = new Color(0.85f, 0.40f, 1.00f);
        public Color SlowMoColor = new Color(0.40f, 0.95f, 0.60f);
        public Color TimeStopColor = new Color(1.00f, 0.85f, 0.30f);

        [Tooltip("Below this fill the bar turns to Low Color, so the player sees it running out.")]
        [Range(0f, 1f)] public float LowThreshold = 0.25f;
        public Color LowColor = new Color(1.00f, 0.35f, 0.30f);

        [Header("Label")]
        [Tooltip("{0} = current seconds, {1} = total seconds, {2} = percent (0-100).")]
        public string LabelFormat = "{0:0.0}s / {1:0.0}s";

        [Tooltip("Receives the formatted label. Bind a TMP_Text or UI.Text setter here.")]
        public TimeWarpStringEvent OnLabelChanged = new TimeWarpStringEvent();

        /// <summary>Current normalised value shown by the bar, after smoothing.</summary>
        public float DisplayedValue01 { get; private set; } = 1f;

        private float _targetValue01 = 1f;
        private string _lastLabel;

        private void Reset()
        {
            // Convenience when the component is added straight onto an Image or Slider.
            FillImage = GetComponent<Image>();
            FillSlider = GetComponent<Slider>();
            FadeGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (FillSlider != null)
            {
                FillSlider.interactable = false;
                FillSlider.minValue = 0f;
                FillSlider.maxValue = 1f;
            }

            // Start already settled so the bar does not visibly sweep in on the first frame.
            Evaluate(out _targetValue01, out _, out _);
            DisplayedValue01 = _targetValue01;
        }

        private void Update()
        {
            var manager = TimeWarpManager.Instance;
            if (manager == null) return;

            Evaluate(out _targetValue01, out float currentSeconds, out float totalSeconds);

            // Unscaled: the bar must keep animating during a time stop.
            DisplayedValue01 = SmoothSpeed <= 0f
                ? _targetValue01
                : Mathf.Lerp(DisplayedValue01, _targetValue01, 1f - Mathf.Exp(-SmoothSpeed * Time.unscaledDeltaTime));

            ApplyFill();
            ApplyColor(manager);
            ApplyFade(manager);
            ApplyLabel(currentSeconds, totalSeconds);
        }

        // ==========================================
        // VALUE
        // ==========================================
        private void Evaluate(out float value01, out float currentSeconds, out float totalSeconds)
        {
            value01 = 1f;
            currentSeconds = 0f;
            totalSeconds = 0f;

            var manager = TimeWarpManager.Instance;
            if (manager == null) return;

            switch (Source)
            {
                case BarSource.ActionTimeRemaining:
                {
                    float remaining = manager.ActionTimeRemaining;

                    // -1 means "no duration limit configured" - show a full, static bar.
                    if (remaining < 0f)
                    {
                        totalSeconds = manager.MaxDuration;
                        currentSeconds = manager.MaxDuration;
                        value01 = 1f;
                        return;
                    }

                    totalSeconds = manager.MaxDuration;
                    currentSeconds = remaining;
                    value01 = totalSeconds > 0f ? Mathf.Clamp01(remaining / totalSeconds) : 0f;
                    return;
                }

                case BarSource.RewindDepth:
                {
                    TimeWarpIdentity id = ResolveTarget();
                    totalSeconds = manager.MaxRecordSeconds;

                    if (id == null || !id.IsRegistered || manager.CaptureFramerate <= 0)
                    {
                        currentSeconds = 0f;
                        value01 = 0f;
                        return;
                    }

                    currentSeconds = id.FractionalOffset / manager.CaptureFramerate;
                    value01 = totalSeconds > 0f ? Mathf.Clamp01(currentSeconds / totalSeconds) : 0f;
                    return;
                }

                case BarSource.AvailableHistory:
                {
                    TimeWarpIdentity id = ResolveTarget();
                    totalSeconds = manager.MaxRecordSeconds;

                    if (id == null || !id.IsRegistered || manager.CaptureFramerate <= 0)
                    {
                        currentSeconds = 0f;
                        value01 = 0f;
                        return;
                    }

                    float usedSeconds = id.FractionalOffset / manager.CaptureFramerate;
                    currentSeconds = Mathf.Max(0f, totalSeconds - usedSeconds);
                    value01 = totalSeconds > 0f ? Mathf.Clamp01(currentSeconds / totalSeconds) : 0f;
                    return;
                }
            }
        }

        private TimeWarpIdentity ResolveTarget()
        {
            if (Target != null) return Target;

            // Cheap fallback so the component is useful with zero setup in a test scene.
            // Cached after the first successful lookup.
            //
            // FindAnyObjectByType, not FindFirst: Unity 6.5 deprecated the "first" variants
            // because they depended on instance ID ordering, which is going away. For a
            // fallback that is fine - but it does mean that in a scene with several
            // identities you cannot predict WHICH one the bar latches onto. Set Target
            // explicitly whenever it matters.
            Target = FindAnyObjectByType<TimeWarpIdentity>(FindObjectsInactive.Exclude);
            return Target;
        }

        // ==========================================
        // OUTPUT
        // ==========================================
        private void ApplyFill()
        {
            if (FillImage != null) FillImage.fillAmount = DisplayedValue01;
            if (FillSlider != null) FillSlider.value = DisplayedValue01;
        }

        private void ApplyColor(TimeWarpManager manager)
        {
            if (!TintByState || FillImage == null) return;

            Color target;

            if (DisplayedValue01 <= LowThreshold)
            {
                target = LowColor;
            }
            else
            {
                switch (manager.GlobalTimeState)
                {
                    case TimeState.Rewinding:  target = RewindColor; break;
                    case TimeState.SlowMotion: target = SlowMoColor; break;
                    case TimeState.TimeStop:   target = TimeStopColor; break;
                    default:                   target = NormalColor; break;
                }
            }

            FillImage.color = FadeSpeed <= 0f
                ? target
                : Color.Lerp(FillImage.color, target, 1f - Mathf.Exp(-FadeSpeed * Time.unscaledDeltaTime));
        }

        private void ApplyFade(TimeWarpManager manager)
        {
            if (FadeGroup == null) return;

            bool idle = !manager.IsActionActive && DisplayedValue01 >= 0.999f;
            float targetAlpha = (HideWhenIdle && idle) ? IdleAlpha : 1f;

            FadeGroup.alpha = FadeSpeed <= 0f
                ? targetAlpha
                : Mathf.Lerp(FadeGroup.alpha, targetAlpha, 1f - Mathf.Exp(-FadeSpeed * Time.unscaledDeltaTime));
        }

        private void ApplyLabel(float currentSeconds, float totalSeconds)
        {
            if (OnLabelChanged == null || string.IsNullOrEmpty(LabelFormat)) return;

            string label = string.Format(LabelFormat, currentSeconds, totalSeconds, DisplayedValue01 * 100f);

            // Only fire when the visible text actually changes - avoids allocating a
            // string and dirtying the canvas every single frame.
            if (label == _lastLabel) return;

            _lastLabel = label;
            OnLabelChanged.Invoke(label);
        }
    }
}
