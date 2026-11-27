using System;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.Generic;
using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Data;
using TimeWarp.Core.Managers;

namespace TimeWarp.Modules.Gameplay
{
    /// <summary>
    /// Acts as a universal bridge to modify variables inside foreign, custom scripts 
    /// (e.g., ammo count, light intensity, boss health) without writing dedicated adapters.
    /// Uses highly optimized Expression Trees and caching to bypass standard Reflection overhead.
    /// </summary>
    [RequireComponent(typeof(TimeWarpIdentity))]
    [AddComponentMenu("TimeWarp/Gameplay/Time Warp Bridge")]
    public class TimeWarpBridge : MonoBehaviour, ITimeWarpModule
    {
        // ==========================================
        // OPTIMIZATION (CACHE): Static dictionaries preventing continuous expression compilation
        // This ensures O(1) lightning-fast access to foreign variables across all instances.
        // ==========================================
        private static readonly Dictionary<(Type, string), Func<MonoBehaviour, float>> _getterCache = new Dictionary<(Type, string), Func<MonoBehaviour, float>>();
        private static readonly Dictionary<(Type, string), Action<MonoBehaviour, float>> _setterCache = new Dictionary<(Type, string), Action<MonoBehaviour, float>>();

        // The cached delegates are keyed by (Type, memberName) and bound to types, not
        // instances, so they stay technically valid across Play sessions when domain
        // reload is disabled. We still clear them for determinism: a script recompiled
        // mid-session would otherwise keep serving delegates compiled against the old type.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _getterCache.Clear();
            _setterCache.Clear();
        }

        private TimeWarpIdentity _identity;
        private bool _valuesAreScaled = false; 
        private float _lastOffsetSeconds = 0f;
        private CustomChannelSettings _channelSettings;
        
        // Tracks scripts that want to receive TimeWarp state updates
        private HashSet<ITimeWarpStateListener> _listeners = new HashSet<ITimeWarpStateListener>();
        
        /// <summary>
        /// Internal wrapper for a single bound variable.
        /// </summary>
        private class BindingCache
        {
            public MonoBehaviour TargetComponent;
            public CustomPropertyBinding Config;
            public float BaseValue;
            public float FrozenValue; 

            public Func<MonoBehaviour, float> FastGetter;
            public Action<MonoBehaviour, float> FastSetter;

            public float[] HistoryBuffer;
            public int MaxBufferSize = 0;

            public float GetValue() => FastGetter(TargetComponent);
            public void SetValue(float val) => FastSetter(TargetComponent, val);

            public void InitializeBuffer(int size) { MaxBufferSize = size; HistoryBuffer = new float[size]; }

            public void SetComponentActive(bool state)
            {
                if (TargetComponent == null) return;
                if (TargetComponent.enabled != state)
                {
                    TargetComponent.enabled = state;
                    // Note: Resume transitions are also handled globally by the ComponentManager,
                    // but we keep this here as a fallback if the bridge operates independently.
                    if (state == true && TargetComponent is ITimeWarpStateListener listener) 
                        listener.OnTimeWarpResume();
                }
            }

            public void ApplyMathScale(float timeScale, float deltaOffsetSec, bool isTimeStopped)
            {
                if (Config.MathMode == BindingMathMode.None || Config.MathMode == BindingMathMode.ResetToBase) return; 

                if (isTimeStopped)
                {
                    if (Config.TimeStopBehavior == StasisBehavior.Ignore) return;
                    if (Config.TimeStopBehavior == StasisBehavior.FreezeAtBase) { SetValue(BaseValue); return; }
                    if (Config.TimeStopBehavior == StasisBehavior.FreezeCurrent) { SetValue(FrozenValue); return; }
                }

                if (Config.MathMode == BindingMathMode.Offset) { SetValue(GetValue() + deltaOffsetSec); return; }

                float safeScale = Mathf.Abs(timeScale) < 0.0001f ? 0.0001f : timeScale;
                if (Config.MathMode == BindingMathMode.Multiply) SetValue(BaseValue * safeScale);
                else if (Config.MathMode == BindingMathMode.Divide) SetValue(BaseValue / safeScale);
            }
            
            public void ApplyRewindFrame(int idxA, int idxB, float lerpT)
            {
                if (HistoryBuffer == null || HistoryBuffer.Length == 0) return;

                float valA = HistoryBuffer[idxA];
                float valB = HistoryBuffer[idxB];

                if (Config.RewindSmoothing == RewindInterpolation.Lerp) SetValue(Mathf.Lerp(valA, valB, lerpT));
                else SetValue(valA); 
            }
        }

        private List<BindingCache> _activeBindings = new List<BindingCache>();

        private void Awake() => _identity = GetComponent<TimeWarpIdentity>();

        public void InitializeBindings(CustomChannelSettings settings)
        {
            _channelSettings = settings;
            if (_channelSettings == null || _channelSettings.Bindings.Count == 0) return;
            
            int bufferSize = TimeWarpManager.Instance != null ? Mathf.CeilToInt(TimeWarpManager.Instance.CaptureFramerate * TimeWarpManager.Instance.MaxRecordTime) : 0;

            foreach (var cfg in _channelSettings.Bindings)
            {
                Component rawComp = null;
                foreach (var comp in GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == cfg.ComponentName) { rawComp = comp; break; }
                }

                if (rawComp == null || !(rawComp is MonoBehaviour targetMono)) continue;
                
                // If the target script implements our interface, track it
                if (targetMono is ITimeWarpStateListener listener) _listeners.Add(listener);

                Type type = targetMono.GetType();
                var cacheKey = (type, cfg.PropertyName); // Unique key for the Cache
                
                var binding = new BindingCache { TargetComponent = targetMono, Config = cfg };

                // Check if we already have compiled delegates in the Cache
                if (!_getterCache.TryGetValue(cacheKey, out var fastGetter))
                {
                    // If not, we search via Reflection and compile the Expression Tree on the fly
                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    FieldInfo field = type.GetField(cfg.PropertyName, flags);
                    PropertyInfo prop = type.GetProperty(cfg.PropertyName, flags);
                    MemberInfo validMember = null;
                    
                    if (field != null && field.FieldType == typeof(float)) validMember = field;
                    else if (prop != null && prop.PropertyType == typeof(float) && prop.CanWrite) validMember = prop;

                    if (validMember == null) continue;

                    var targetParam = Expression.Parameter(typeof(MonoBehaviour), "target");
                    var castTarget = Expression.Convert(targetParam, type);
                    var getMember = Expression.PropertyOrField(castTarget, cfg.PropertyName);

                    fastGetter = Expression.Lambda<Func<MonoBehaviour, float>>(getMember, targetParam).Compile();
                    _getterCache[cacheKey] = fastGetter; // Save to Cache

                    var valueParam = Expression.Parameter(typeof(float), "value");
                    var setMember = Expression.Assign(getMember, valueParam);
                    var fastSetter = Expression.Lambda<Action<MonoBehaviour, float>>(setMember, targetParam, valueParam).Compile();
                    _setterCache[cacheKey] = fastSetter; // Save to Cache
                }

                // Assign the compiled (or cached) delegates
                binding.FastGetter = fastGetter;
                binding.FastSetter = _setterCache[cacheKey];

                binding.BaseValue = binding.GetValue();
                
                bool needsBuffer = cfg.OperationMode == CustomChannelOperation.RecordOnly || cfg.OperationMode == CustomChannelOperation.MathAndRecord;
                if (needsBuffer && bufferSize > 0) binding.InitializeBuffer(bufferSize);

                _activeBindings.Add(binding);
            }
        }

        public void OnWarpFixedUpdate()
        {
            if (_identity == null || _activeBindings.Count == 0 || TimeWarpManager.Instance == null) return;

            bool isRewinding = _identity.IsRewinding || _identity.FractionalOffset > 0.001f;
            if (!isRewinding && _identity.EffectiveTimeScale >= 0f)
            {
                var state = TimeWarpManager.Instance.GlobalTimeState;
                if (state == TimeState.Normal || state == TimeState.SlowMotion || state == TimeState.PreAction || state == TimeState.PostAction)
                {
                    int head = _identity.Buffer.GetIndex(0);
                    for (int i = 0; i < _activeBindings.Count; i++)
                    {
                        var op = _activeBindings[i].Config.OperationMode;
                        if (op == CustomChannelOperation.RecordOnly || op == CustomChannelOperation.MathAndRecord) 
                        {
                            _activeBindings[i].HistoryBuffer[head] = _activeBindings[i].GetValue();
                        }
                    }
                }
            }
        }

        public void OnWarpUpdate()
        {
            if (_identity == null || _activeBindings.Count == 0) return;

            float scale = _identity.EffectiveTimeScale;
            bool isRewinding = _identity.IsRewinding;
            bool isTimeStopped = Mathf.Abs(scale) <= 0.001f && !isRewinding;

            float currentOffsetSec = 0f;
            float deltaOffsetSec = 0f;
            
            if (TimeWarpManager.Instance != null && isRewinding)
            {
                currentOffsetSec = _identity.FractionalOffset / TimeWarpManager.Instance.CaptureFramerate;
                deltaOffsetSec = currentOffsetSec - _lastOffsetSeconds; 
            }
            _lastOffsetSeconds = currentOffsetSec;

            if (Mathf.Abs(scale - 1f) < 0.001f && !isRewinding)
            {
                if (_valuesAreScaled) { RestoreAllOriginals(); _valuesAreScaled = false; }
                return; 
            }

            _valuesAreScaled = true;
            bool shouldSleep = isRewinding || isTimeStopped;

            _identity.Buffer.GetFractionalIndex(_identity.FractionalOffset, out int idxA, out int idxB, out float t);

            for (int i = 0; i < _activeBindings.Count; i++)
            {
                var binding = _activeBindings[i];
                var op = binding.Config.OperationMode;

                if (!isTimeStopped && !isRewinding) binding.FrozenValue = binding.GetValue();
                if (_channelSettings.DisableComponentsOnAnomalies) binding.SetComponentActive(!shouldSleep);

                if (isRewinding)
                {
                    if (op == CustomChannelOperation.RecordOnly || op == CustomChannelOperation.MathAndRecord) 
                    {
                        binding.ApplyRewindFrame(idxA, idxB, t);
                    }
                }
                else
                {
                    if (op == CustomChannelOperation.MathOnly || op == CustomChannelOperation.MathAndRecord) 
                    {
                        binding.ApplyMathScale(scale, deltaOffsetSec, isTimeStopped);
                    }
                }
            }

            // ==========================================
            // CRITICAL FIX: Updated Interface Call
            // ==========================================
            // We call the new AnomalyUpdate method, passing the scale and the exact rewind DeltaTime
            if (shouldSleep) 
            {
                foreach (var listener in _listeners) 
                {
                    listener.OnTimeWarpAnomalyUpdate(scale, isRewinding ? -deltaOffsetSec : 0f);
                }
            }
        }

        private void RestoreAllOriginals()
        {
            for (int i = 0; i < _activeBindings.Count; i++)
            {
                var cfg = _activeBindings[i].Config;
                
                if (cfg.OperationMode == CustomChannelOperation.MathOnly || cfg.OperationMode == CustomChannelOperation.MathAndRecord)
                {
                    if (cfg.MathMode != BindingMathMode.None && cfg.MathMode != BindingMathMode.Offset)
                    {
                        _activeBindings[i].SetValue(_activeBindings[i].BaseValue);
                    }
                }

                if (_channelSettings.DisableComponentsOnAnomalies) _activeBindings[i].SetComponentActive(true);
            }
            _lastOffsetSeconds = 0f;
        }

        private void OnDisable() { if (_valuesAreScaled) RestoreAllOriginals(); }
        private void OnDestroy() { if (_valuesAreScaled) RestoreAllOriginals(); }
    }
}