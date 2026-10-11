using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Inherit your MonoBehaviours from this class to have them ready to go with IHaveLocalTime
 */

namespace MagicPigGames.MagicTime
{
    public class MagicTimeUser : MonoBehaviour, IHaveLocalTime
    {
        [Header("TimeScale Subscriptions")]
        public List<LocalTimeScale> initialTimeScales = new List<LocalTimeScale>();
        
        [Header("Transition Settings")]
        public float transitionDuration = 0.2f;
        public AnimationCurve transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        
        [Header("Options")]
        [Min(0f)]
        [Tooltip("This is the default LocalTimeScale for this object. This is the value that will be set when the object is enabled.")]
        public float defaultTimeScale = 1f;
        
        // These are the various LocalTimeScale objects that this object is subscribed to, and affected by.
        public List<LocalTimeScale> SubscribedTimeScales { get; } = new List<LocalTimeScale>();
        protected LocalTimeScale _localTimeScale; // The LocalTimeScale attached to THIS object.
        public LocalTimeScale ObjectTimeScale => _localTimeScale;
        public GameObject GameObject => gameObject;

        protected float _desiredCombinedTimeScale;
        protected float _currentTimeScale;
        protected float _transitionStartTime;
        protected float _transitionStartValue;
        protected bool _isTransitioning = false;

        // --- Per-object time accumulators ---
        protected float _timeSinceStart = 0f;            // Affected by this object's TimeScale
        protected float _unscaledTimeSinceStart = 0f;    // Real time, unaffected by TimeScale

        public float LocalTimeScaleValue
        {
            get => _localTimeScale.Value;
            set => _localTimeScale.SetValue(value);
        }

        // ---- THE IMPORTANT PROPERTIES ----
        // TimeScale is the combined timeScale of all subscribed timeScales, handling transitions
        public virtual float TimeScale => _currentTimeScale;
        // InverseTimeScale is the inverse of the TimeScale. Multiply TimeScale by this to get Time.timeScale.
        public virtual float InverseTimeScale => UnityEngine.Time.timeScale / TimeScale; // Example: 1 / 0.5 = 2; 1 / 2 = 0.5;
        
        // These are the standard Unity Time properties, but they are multiplied by the TimeScale. This is often
        // what you'll call when doing timers and other time-based calculations.
        public virtual float DeltaTime => UnityEngine.Time.deltaTime * TimeScale;
        public virtual float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;
        public virtual float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime * TimeScale;

        // Time since start, per-object
        public virtual float Time => _timeSinceStart;                 // Accumulates using DeltaTime (scaled)
        public virtual float UnscaledTime => _unscaledTimeSinceStart;  // Accumulates using UnityEngine.Time.unscaledDeltaTime (unscaled)
        // ------------------------------
        
        public LocalTimeScale LocalTimeScale => _localTimeScale;

        protected virtual void Awake()
        {
            // Initialize the local timeScale -- Note we do NOT register these with LocalTimeScale!
            _localTimeScale = ScriptableObject.CreateInstance<LocalTimeScale>();
            _localTimeScale.Initialize($"{name}_LocalTimeScale", 1f);
        }
        
        protected virtual void SetDesiredCombinedTimeScale(float value) => _desiredCombinedTimeScale = value;
        
        protected virtual void SetCurrentTimeScale(float value) => _currentTimeScale = value;

        protected bool _subscriptionsComplete = false;
        protected virtual void OnEnable()
        {
            _subscriptionsComplete = false;
            SetDesiredCombinedTimeScale(defaultTimeScale);
            SetCurrentTimeScale(defaultTimeScale);
            StartCoroutine(SubscribeToInitialTimeScales());
            SubscribeToLocalTimeScale(_localTimeScale);
        }
        
        protected virtual IEnumerator SetDefaultTimeScale()
        {
            yield return new WaitUntil(() => _subscriptionsComplete);
            
            EndTransition(); // Force it to jump directly to the desired timeScale.
            yield return null;
        }

        // NOTE: If this isn't firing, make sure you are not overriding Update in a subclass!!!
        protected virtual void Update()
        {
            // Advance per-object clocks
            _timeSinceStart += DeltaTime;                                      // affected by TimeScale
            _unscaledTimeSinceStart += UnityEngine.Time.unscaledDeltaTime;     // real time

            HandleTransition();
        }

        /// <summary>
        /// This will immediately end the transition and set the current timeScale to the desired timeScale.
        /// </summary>
        public virtual void EndTransition()
        {
            _isTransitioning = false;
            SetCurrentTimeScale(_desiredCombinedTimeScale);
        }

        protected virtual void HandleTransition()
        {
            if (!_isTransitioning) return;
            
            var elapsedTime = UnityEngine.Time.time - _transitionStartTime;
            var t = transitionDuration > 0 ? Mathf.Clamp01(elapsedTime / transitionDuration) : 1f;
            var curveValue = transitionCurve.Evaluate(t);
            var newValue = Mathf.Lerp(_transitionStartValue, _desiredCombinedTimeScale, curveValue);
            
            SetCurrentTimeScale(newValue);
            
            // End the transition if we are at the end.
            if (t >= 1.0f || Mathf.Approximately(newValue, _desiredCombinedTimeScale))
                EndTransition();
        }

        protected virtual void OnDisable() => UnsubscribeFromAll();

        /// <summary>
        /// Subscribes this object to a LocalTimeScale. This will cause the object to be affected by the timeScale.
        /// </summary>
        /// <param name="myTimeScale"></param>
        public virtual void SubscribeToLocalTimeScale(LocalTimeScale myTimeScale)
        {
            if (myTimeScale == null)
            {
                Debug.LogError("Cannot subscribe to a null LocalTimeScale.");
                return;
            }

            // Can't subscribe to the same timeScale twice.
            if (SubscribedTimeScales.Contains(myTimeScale)) return;

            SubscribedTimeScales.Add(myTimeScale);
            myTimeScale.Subscribe(this);
            myTimeScale.OnTimeScaleChanged += OnTimeScaleChanged;
            
            // After subscribing, the desired combined timeScale may change.
            ResetDesiredTimeScale();
        }

        /// <summary>
        /// Unsubscribes this object from a LocalTimeScale. This will cause the object to no longer be affected by the timeScale.
        /// </summary>
        /// <param name="timeScale"></param>
        public virtual void UnsubscribeFromLocalTimeScale(LocalTimeScale timeScale)
        {
            if (timeScale == null)
                return;

            // If it is in our list, then unsubscribe and remove it.
            if (SubscribedTimeScales.Contains(timeScale))
            {
                // Detach the change handler FIRST -- Subscribers and OnTimeScaleChanged are two separate
                // registers, and leaving this one attached leaks the user, stacks a duplicate on every
                // re-subscribe, and lets a destroyed user throw out of the middle of the scale's invoke.
                timeScale.OnTimeScaleChanged -= OnTimeScaleChanged;
                timeScale.Unsubscribe(this);
                SubscribedTimeScales.Remove(timeScale);
            }
            
            // This can be called from the LocalTimeScale, so we may need to reset the desired timeScale, 
            // even if the timeScale is not in our list.
            ResetDesiredTimeScale();
        }

        // This combines the timeScales of all subscribed timeScales.
        protected virtual float CombinedTimeScale()
        {
            RemoveNullTimeScales();
            // Start at identity, NOT at _localTimeScale.Value. OnEnable subscribes _localTimeScale, so it
            // is itself one of SubscribedTimeScales -- the inspector knows this and skips it when listing
            // the others -- and seeding with it applied this object's own scale twice: 0.5 ran at 0.25.
            // It also means a destroyed user, whose list is empty, gets 1f here instead of an NRE.
            var combinedTimeScale = 1f;
            foreach (var timeScale in SubscribedTimeScales)
                combinedTimeScale *= timeScale.Value;

            return combinedTimeScale;
        }

        /// <summary>
        /// Called when the object is enabled. This will subscribe to the initial timeScales set up in the
        /// Inspector.
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerator SubscribeToInitialTimeScales()
        {
            // Wait until LocalTimeScaleManager.Instance is available
            yield return new WaitUntil(() => MagicTimeManager.Instance != null);

            foreach(var timeScale in initialTimeScales)
            {
                var localTimeScale = MagicTimeManager.Instance.TimeScale(timeScale.name);
                if (localTimeScale == null)
                {
                    Debug.LogError($"TimeScale '{timeScale.name}' not found in LocalTimeScaleManager.");
                    continue;
                }
                SubscribeToLocalTimeScale(localTimeScale);
            }

            _subscriptionsComplete = true;
            yield return null;
        }

        protected virtual void OnDestroy()
        {
            UnsubscribeFromAll();
            if (_localTimeScale == null) return;
            
            _localTimeScale.Dispose();
            _localTimeScale = null;
        }

        /// <summary>
        /// Unsubscribes from all LocalTimeScales. This will cause the object to no longer be affected by any timeScales,
        /// including its own!
        /// </summary>
        public virtual void UnsubscribeFromAll()
        {
            // Reverse for loop
            for (var i = SubscribedTimeScales.Count - 1; i >= 0; i--)
            {
                var timeScale = SubscribedTimeScales[i];
                UnsubscribeFromLocalTimeScale(timeScale);
            }
            
            SubscribedTimeScales.Clear();
            ResetDesiredTimeScale();
        }
        
        /// <summary>
        /// Called when an object is unsubscribed from a LocalTimeScale.
        /// </summary>
        /// <param name="obj"></param>
       /* public virtual void OnUnsubscribed(object obj)
        {
            Debug.Log($"On Unsubscribed {name}");
            if (obj == null) return;
            if (obj is not LocalTimeScale timeScale) return;
            if (timeScale != LocalTimeScale) return; // Don't do anything if this is not our local timeScale.

            timeScale.OnTimeScaleChanged -= OnTimeScaleChanged;
            timeScale.OnUnsubscribed -= OnUnsubscribed;
            HandleChangedTimeScaleValues();
        }*/
        
        protected virtual void RemoveNullTimeScales() => SubscribedTimeScales.RemoveAll(timeScale => timeScale == null);
        
        /// <summary>
        /// Called when a LocalTimeScale is destroyed. This will remove the LocalTimeScale from the list of subscribed timeScales.
        /// </summary>
        /// <param name="timeScale"></param>
        public virtual void OnTimeScaleDestroyed(LocalTimeScale timeScale)
        {
            if (timeScale == null)
                return;

            // We will remove, but not unsubscribe, as the timeScale is already destroyed.
            if (SubscribedTimeScales.Contains(timeScale))
                SubscribedTimeScales.Remove(timeScale);
        }
        
        /// <summary>
        /// Called when a LocalTimeScale changes. This will cause the object to recalculate its desired timeScale.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="localTimeScale"></param>
        public virtual void OnTimeScaleChanged(float value, LocalTimeScale localTimeScale)
        {
            HandleChangedTimeScaleValues();
        }

        // Call this whenever the timeScale values change.
        public virtual void HandleChangedTimeScaleValues()
        {
            if (transitionDuration <= 0)
            {
                // We aren't transitioning so set these immediately.
                SetDesiredCombinedTimeScale(CombinedTimeScale());
                SetCurrentTimeScale(CombinedTimeScale());
                _isTransitioning = false;
                return;
            }
            
            ResetDesiredTimeScale();
        }
        
        /// <summary>
        /// Forces the desired timeScale to be recalculated and optionally applied immediately.
        /// </summary>
        public virtual void ResetDesiredTimeScale(bool applyImmediately = false)
        {
            SetDesiredCombinedTimeScale(CombinedTimeScale());
            if (applyImmediately)
            {
                EndTransition(); // This will set the current timeScale to the desired timeScale.
                return;
            }
            StartTransition();
        }

        protected virtual void StartTransition()
        {
            if (transitionDuration <= 0)
            {
                SetDesiredCombinedTimeScale(CombinedTimeScale());
                SetCurrentTimeScale(CombinedTimeScale());
                _isTransitioning = false;
                return;
            }
            
            _isTransitioning = true;
            _transitionStartTime = UnityEngine.Time.time;
            _transitionStartValue = _currentTimeScale; // Set this to whatever it is right now
            SetDesiredCombinedTimeScale(CombinedTimeScale()); // Set this to the new combined value
        }
    }
}