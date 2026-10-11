using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/*
 * This is the "brains" of the whole system. Each LocalTimeScale object will have its own Time Scale. Objects which
 * need to have their actions based on this can subscribe to it, or to multiple of them. Note that the LocalTimeScaleUser
 * will have its own personal LocalTimeScale by default, but can also subscribe to others.
 */

namespace MagicPigGames.MagicTime
{
    [CreateAssetMenu(fileName = "New Time Scale", menuName = "Local Time Scale/Time Scale")]
    public class LocalTimeScale : ScriptableObject
    {
        [Header("Time Scale Settings")]
        [Tooltip("The current value of the TimeScale. Use the \"Value\" property to get and set this at runtime.")]
        [SerializeField]
        protected float value = 1f; // Use the "Value" property to access this, and set it, at runtime.

        [Tooltip("Automatically remove this TimeScale when it has no subscribers. This will be set false for " +
                 "any TimeScale that is populated from the Inspector!")]
        [SerializeField]
        protected bool autoRemoveWhenEmpty = true;
        public bool AutoRemoveWhenEmpty
        {
            get => autoRemoveWhenEmpty;
            set => autoRemoveWhenEmpty = value;
        }
        
        public bool IsPaused => _isPaused;
        
        protected float _lastValue = 1f; // Used for the "Resume" method.
        protected bool _isPaused = false; // Paused is true when the value = 0. Cache this so we can trigger Pause and Resume events
        protected readonly HashSet<IHaveLocalTime> Subscribers = new HashSet<IHaveLocalTime>(); // Records all the subscribers to this LocalTimeScale

        private float _time = 0f;
        private float _unscaledTime = 0f;

        // These properties may be useful for getting more information about the subscribers.
        public string[] SubscriberNames => Subscribers.Select(s => s.ToString()).ToArray();
        public GameObject[] SubscriberGameObjects => Subscribers.Select(s => s.GameObject).ToArray();
        public int SubscriberCount => Subscribers.Count;

        /// <summary>
        /// The current value of the TimeScale. Setting this value will invoke the change events.
        /// </summary>
        public float Value
        {
            get => value;
            private set
            {
                if (value < 0f)
                {
                    Debug.LogWarning("Negative TimeScale is not supported. Setting to 0.");
                    value = 0f; // Clamp the incoming value -- writing this.value here is overwritten below.
                }
                
                this.value = value;
                RaiseTimeScaleChanged(this.value);
                OnTimeScaleChangedUnityEvent?.Invoke(this.value);

                // Handle pause and resume events
                if (this.value == 0f && !_isPaused)
                {
                    _isPaused = true;
                    OnPaused?.Invoke(this);
                    OnPausedUnityEvent?.Invoke();
                }
                else if (this.value > 0f && _isPaused)
                {
                    _isPaused = false;
                    OnResumed?.Invoke(this);
                    OnResumedUnityEvent?.Invoke();
                }
            }
        }
        
        /// <summary>
        /// Invokes <see cref="OnTimeScaleChanged"/> one subscriber at a time.
        /// </summary>
        /// <remarks>
        /// A plain multicast invoke stops at the first subscriber that throws and silently never calls
        /// the ones behind it in the list -- on a Resume that leaves live objects sitting at the paused
        /// value, with nothing in the log but the one exception. It would also abandon the pause/resume
        /// bookkeeping that follows it in the setter. Exceptions are logged, not swallowed, so a broken
        /// subscriber stays exactly as visible as it was before.
        /// </remarks>
        protected virtual void RaiseTimeScaleChanged(float newValue)
        {
            var handlers = OnTimeScaleChanged;
            if (handlers == null) return;

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<float, LocalTimeScale>)handler).Invoke(newValue, this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>
        /// Returns DeltaTime adjusted by the current TimeScale value.
        /// </summary>
        public float DeltaTime => UnityEngine.Time.deltaTime * Value;

        /// <summary>
        /// Returns UnscaledDeltaTime adjusted by the current TimeScale value.
        /// </summary>
        public float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime * Value;

        /// <summary>
        /// Returns FixedDeltaTime adjusted by the current TimeScale value.
        /// </summary>
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime * Value;

        /// <summary>
        /// Gets the time since the start of the game affected by the local time scale.
        /// This time accumulates based on DeltaTime and is affected by the local time scale.
        /// </summary>
        public float Time => _time;

        /// <summary>
        /// Gets the unscaled time since the start of the game.
        /// This time accumulates based on Unity's unscaledDeltaTime and is not affected by the local time scale.
        /// </summary>
        public float UnscaledTime => _unscaledTime;

        /// <summary>
        /// Increments the internal time accumulators. Should be called every frame to update Time and UnscaledTime.
        /// </summary>
        public void Tick()
        {
            _time += DeltaTime;
            _unscaledTime += UnityEngine.Time.unscaledDeltaTime;
        }
        
        /// <summary>
        /// Sets the TimeScale value. If the new value is different from the current value, it will invoke the change events.
        /// </summary>
        /// <param name="newValue"></param>
        public void SetValue(float newValue)
        {
            if (Mathf.Approximately(Value, newValue)) return;
            Value = newValue;
        }

        /// <summary>
        /// Pauses the TimeScale, setting the value to 0, and caching the previous value.
        /// </summary>
        public void Pause()
        {
            _lastValue = Value;
            Value = 0f;
        }
        
        /// <summary>
        /// Resumes the TimeScale, setting the value back to the previous value before pausing.
        /// </summary>
        public void Resume() => Value = _lastValue;

        /// <summary>
        /// Regular C# event for subscribers to respond to TimeScale changes.
        /// </summary>
        public event Action<float, LocalTimeScale> OnTimeScaleChanged;
        public event Action<LocalTimeScale> OnPaused;
        public event Action<LocalTimeScale> OnResumed;

        /// <summary>
        /// Initialize the LocalTimeScale instance. This should be called after creation.
        /// </summary>
        public void Initialize(string nameValue, float initialValue = 1f)
        {
            name = nameValue;
            Value = initialValue;
        }

        /// <summary>
        /// Adds a subscriber to this TimeScale.
        /// </summary>
        public void Subscribe(IHaveLocalTime subscriber)
        {
            Subscribers.Add(subscriber);
            OnSubscribed?.Invoke(subscriber);
            OnSubscribedUnityEvent?.Invoke(subscriber);
        }

        /// <summary>
        /// Removes a subscriber from this TimeScale.
        /// </summary>
        public void Unsubscribe(IHaveLocalTime subscriber)
        {
            if (!Subscribers.Contains(subscriber))
                return;
            
            Subscribers.Remove(subscriber);
            subscriber.UnsubscribeFromLocalTimeScale(this);
            OnUnsubscribed?.Invoke(subscriber);
            OnUnsubscribedUnityEvent?.Invoke(subscriber);

            if (Subscribers.Count == 0 && AutoRemoveWhenEmpty)
                MagicTimeManager.Instance?.RemoveTimeScale(name);
        }
        
        /// <summary>
        /// Removes all subscribers from this TimeScale.
        /// </summary>
        public void UnsubscribeAll()
        {
            foreach (var subscriber in Subscribers.ToArray())
                Unsubscribe(subscriber);
        }

        /// <summary>
        /// This will call the OnTimeScaleDestroyed event on subscribers, and clear the list. If AutoRemoveWhenEmpty
        /// is true, this instance will be removed.
        /// </summary>
        public void Dispose()
        {
            foreach (var subscriber in Subscribers)
                subscriber.OnTimeScaleDestroyed(this);

            UnsubscribeAll();
            Subscribers.Clear();

            if (AutoRemoveWhenEmpty && MagicTimeManager.Instance != null)
                MagicTimeManager.Instance.RemoveTimeScale(name);
        }
        
        protected void OnDisable()
        { 
            // Prevent early calls during editor reloading or asset refreshing
            if (!Application.isPlaying)
                return;

            // Ensure LocalTimeScaleManager.Instance exists before calling Dispose
            if (MagicTimeManager.Instance != null)
                Dispose();
        }

        /// <summary>
        /// Regular C# event invoked when a subscriber is added.
        /// </summary>
        public event Action<object> OnSubscribed;

        /// <summary>
        /// Regular C# event invoked when a subscriber is removed.
        /// </summary>
        public event Action<object> OnUnsubscribed;

        /// <summary>
        /// UnityEvent for TimeScale changes, visible and assignable in the Inspector.
        /// </summary>
        [Header("Events")]
        
        [Tooltip("Invoked when the TimeScale value changes.")]
        [SerializeField]
        protected UnityEvent<float> onTimeScaleChangedUnityEvent = new UnityEvent<float>();
        public UnityEvent<float> OnTimeScaleChangedUnityEvent => onTimeScaleChangedUnityEvent;
        
        /// <summary>
        /// UnityEvent invoked when a subscriber is added, visible in the Inspector.
        /// </summary>
        [Tooltip("Invoked when an object subscribes to this TimeScale.")]
        [SerializeField]
        protected UnityEvent<object> onSubscribedUnityEvent = new UnityEvent<object>();
        public UnityEvent<object> OnSubscribedUnityEvent => onSubscribedUnityEvent;

        /// <summary>
        /// UnityEvent invoked when a subscriber is removed, visible in the Inspector.
        /// </summary>
        [Tooltip("Invoked when an object unsubscribes from this TimeScale.")]
        [SerializeField]
        protected UnityEvent<object> onUnsubscribedUnityEvent = new UnityEvent<object>();
        public UnityEvent<object> OnUnsubscribedUnityEvent => onUnsubscribedUnityEvent;
        
        /// <summary>
        /// UnityEvent invoked when the TimeScale is paused, visible in the Inspector.
        /// </summary>
        [SerializeField]
        protected UnityEvent onPausedUnityEvent = new UnityEvent();
        public UnityEvent OnPausedUnityEvent => onPausedUnityEvent;

        /// <summary>
        /// UnityEvent invoked when the TimeScale is resumed, visible in the Inspector.
        /// </summary>
        [SerializeField]
        protected UnityEvent onResumedUnityEvent = new UnityEvent();
        public UnityEvent OnResumedUnityEvent => onResumedUnityEvent;
    }
}