using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Jobs;
using TimeWarp.Core.Jobs;
using TimeWarp.Core.Data;
using TimeWarp.Core.Profiling;

namespace TimeWarp.Core.Managers
{
    [DefaultExecutionOrder(-100)]
    public class TimeWarpManager : MonoBehaviour
    {
        public static TimeWarpManager Instance { get; private set; }

        // ==========================================
        // DOMAIN RELOAD SAFETY (Unity 6.x "Enter Play Mode Options")
        // ------------------------------------------
        // When "Reload Domain" is disabled, static fields survive between Play sessions.
        // Unity's fake-null operator makes a destroyed Instance compare equal to null,
        // so this is defensive rather than strictly required - but it removes any
        // ambiguity and guarantees a clean slate on every Play.
        // ==========================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (MaxRecordTime <= 0f)
            {
                Debug.LogWarning(
                    $"[TimeWarp] Max Record Time is {MaxRecordTime}s - no history can be recorded " +
                    $"and rewinding will do nothing.", this);
            }

            if (UseMaxDuration && MaxDuration <= 0f)
            {
                Debug.LogWarning(
                    $"[TimeWarp] Use Max Duration is enabled but Max Duration is {MaxDuration}s, " +
                    $"so every action will end immediately.", this);
            }

            // Warn rather than clamp: silently rewriting a value the user typed is worse
            // than telling them it is out of range.
            if (MaxParticleRamBudgetMB <= 0)
            {
                Debug.LogWarning(
                    $"[TimeWarp] Particle RAM budget is {MaxParticleRamBudgetMB} MB, so particle " +
                    $"caching is effectively disabled and all systems fall back to Procedural.", this);
            }
        }
#endif

        [Header("--- 1. CORE SETTINGS ---")]
        public EngineMode Mode = EngineMode.GlobalOnly;
        public GlobalAction ActionType = GlobalAction.Rewind;
        public int CaptureFramerate = 50;
        public float MaxRecordTime = 10f;

        [Space(10)]
        [Header("--- 2. TIMING & DURATION ---")]
        public float StartDelay = 0f; 
        public bool UseMaxDuration = false;
        public float MaxDuration = 5.0f;

        [Space(10)]
        [Header("--- 3. GLOBAL SMOOTHING (ENTRY & EXIT) ---")]
        public bool UseEntryCurve = true;
        public AnimationCurve EntryCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float EntryDuration = 0.5f;

        [Space(5)]
        public bool UseExitCurve = true;
        public AnimationCurve ExitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float ExitDuration = 0.3f;

        [Space(10)]
        [Header("--- 4. ACTION SPECIFIC SETTINGS ---")]
        public AnimationCurve RewindAccelerationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1.5f);
        [Range(0.1f, 10f)] public float RewindSpeedMultiplier = 1.0f;
        [Range(0.01f, 10f)] public float TargetSlowMoScale = 0.2f;

        [Space(10)]
        [Header("--- 5. VISUAL EFFECTS SETTINGS ---")]
        [Range(10, 120)] public int GlobalRewindVisualQuality = 60;
        [Range(50, 1000)] public int MaxVFXRewindSteps = 300;

        // ==========================================
        // --- AAA OPTIMIZATION: GLOBAL RAM BUDGET ---
        // ==========================================
        [Space(10)]
        [Header("--- 6. PARTICLE RAM BUDGET ---")]
        [Tooltip("Maximum allowed RAM in Megabytes for Particle caching. If exceeded, new particles fallback to Procedural mode.")]
        public float MaxParticleRamBudgetMB = 50f; 
        
        private long _currentParticleRamUsageBytes = 0;

        // ==========================================
        // Actual size of one cached particle, queried from the engine.
        // ------------------------------------------
        // This used to be hardcoded as 136 bytes. ParticleSystem.Particle has changed
        // layout between Unity versions before, and a stale constant makes the RAM
        // budget silently wrong in both directions: too small and caching falls back
        // to Procedural for no reason, too large and we blow past MaxParticleRamBudgetMB.
        // UnsafeUtility.SizeOf is exact here because ParticleHistory stores these in a
        // NativeArray<ParticleSystem.Particle>, so this IS the allocated stride.
        // ==========================================
        private static readonly long ParticleStructSizeBytes =
            UnsafeUtility.SizeOf<ParticleSystem.Particle>();

        [Space(10)]
        [Header("--- 7. GLOBAL EVENTS ---")]
        public UnityEvent OnGlobalRewindStart;
        public UnityEvent OnGlobalRewindStop;
        public UnityEvent OnGlobalTimeStop;
        public UnityEvent OnGlobalSlowMoStart;

        public TimeState GlobalTimeState { get; private set; } = TimeState.Normal;
        public float CurrentGlobalTimeScale { get; private set; } = 1f;

        // ==========================================
        // READ-ONLY STATE FOR UI / GAMEPLAY CODE
        // ==========================================

        /// <summary>
        /// Seconds the current global action has been running, when UseMaxDuration is on.
        /// </summary>
        public float CurrentActionTimer => _currentActionTimer;

        /// <summary>
        /// How much of the current action has elapsed, 0..1.
        /// Returns 0 when no duration-limited action is running, so a bar bound to this
        /// sits empty rather than jumping to a stale value.
        /// </summary>
        public float ActionProgress01
        {
            get
            {
                if (!UseMaxDuration || MaxDuration <= 0f) return 0f;
                if (GlobalTimeState == TimeState.Normal) return 0f;

                return Mathf.Clamp01(_currentActionTimer / MaxDuration);
            }
        }

        /// <summary>
        /// Seconds of the current action still available, or -1 when the action is
        /// unlimited (UseMaxDuration off).
        /// </summary>
        public float ActionTimeRemaining
        {
            get
            {
                if (!UseMaxDuration || MaxDuration <= 0f) return -1f;
                if (GlobalTimeState == TimeState.Normal) return MaxDuration;

                return Mathf.Max(0f, MaxDuration - _currentActionTimer);
            }
        }

        /// <summary>Total seconds of history the engine is configured to keep.</summary>
        public float MaxRecordSeconds => MaxRecordTime;

        /// <summary>True while a global action (rewind, slow motion or time stop) is running.</summary>
        public bool IsActionActive =>
            GlobalTimeState != TimeState.Normal && GlobalTimeState != TimeState.PostAction;

        private float _globalFractionalFrames = 0f;
        private float _stateTimer = 0f; 
        private float _currentActionTimer = 0f; 
        
        private float _currentDelayTimer = 0f;
        private bool _isWaitingForDelay = false;

        private List<TimeWarpIdentity> _identities = new List<TimeWarpIdentity>();

        private TransformAccessArray _transformAccessArray;
        private NativeArray<int> _headIndices;
        private NativeArray<int> _readIndicesA;
        private NativeArray<int> _readIndicesB;
        private NativeArray<float> _lerpFactors;
        private NativeArray<IntPtr> _historyBufferPointers;
        
        private NativeArray<byte> _recordFlags; 
        private NativeArray<byte> _applyFlags;  

        private JobHandle _transformJobHandle;
        private bool _isInitialized = false;

        // --- Safe Memory Disposal Queue ---
        private Queue<TimeWarpBuffer> _disposalQueue = new Queue<TimeWarpBuffer>();

        private void Awake()
        {
            if (Instance != null && Instance != this) 
            { 
                Destroy(gameObject); 
                return; 
            }
            Instance = this;

            CaptureFramerate = Mathf.RoundToInt(1f / Time.fixedDeltaTime);
            InitializeSystem();
        }

        private void InitializeSystem()
        {
            int capacity = 256; 
            
            _transformAccessArray = new TransformAccessArray(capacity);
            _headIndices = new NativeArray<int>(capacity, Allocator.Persistent);
            _readIndicesA = new NativeArray<int>(capacity, Allocator.Persistent);
            _readIndicesB = new NativeArray<int>(capacity, Allocator.Persistent);
            _lerpFactors = new NativeArray<float>(capacity, Allocator.Persistent);
            _historyBufferPointers = new NativeArray<IntPtr>(capacity, Allocator.Persistent);
            
            _recordFlags = new NativeArray<byte>(capacity, Allocator.Persistent); 
            _applyFlags = new NativeArray<byte>(capacity, Allocator.Persistent); 
            
            _isInitialized = true;
        }

        /// <summary>
        /// Asks the global manager if there is enough RAM budget to cache this particle system.
        /// Uses long-safe multiplication against the engine-reported struct size.
        /// </summary>
        public bool RequestParticleRamBudget(int estimatedParticles, int frameCount)
        {
            // Force long arithmetic from the start to prevent int overflow
            long requestedBytes = (long)estimatedParticles * ParticleStructSizeBytes * (long)frameCount;
            long maxBytes = (long)(MaxParticleRamBudgetMB * 1024f * 1024f);

            if (_currentParticleRamUsageBytes + requestedBytes <= maxBytes)
            {
                _currentParticleRamUsageBytes += requestedBytes;
                return true; 
            }
            
            return false; 
        }

        /// <summary>
        /// Frees up the RAM budget when a cached particle system is destroyed.
        /// Manual clamp to 0L to avoid Math.Max overhead and long-underflow.
        /// </summary>
        public void ReleaseParticleRamBudget(int estimatedParticles, int frameCount)
        {
            // Everything calculated as long to prevent int overflow
            long freedBytes = (long)estimatedParticles * ParticleStructSizeBytes * (long)frameCount;

            long newValue = _currentParticleRamUsageBytes - freedBytes;

            // Engine-style clamp: faster and more predictable than Math.Max for native types
            _currentParticleRamUsageBytes = newValue > 0 ? newValue : 0L;
        }

        private void SetState(TimeState newState)
        {
            if (newState == GlobalTimeState) return;

            switch (newState)
            {
                case TimeState.Rewinding: OnGlobalRewindStart?.Invoke(); break;
                case TimeState.TimeStop: OnGlobalTimeStop?.Invoke(); break;
                case TimeState.SlowMotion: OnGlobalSlowMoStart?.Invoke(); break;
                case TimeState.Normal: 
                    if (GlobalTimeState == TimeState.Rewinding || GlobalTimeState == TimeState.PostAction) 
                        OnGlobalRewindStop?.Invoke(); 
                    break;
            }

            GlobalTimeState = newState;
        }

        public void TriggerAction(GlobalAction action, float duration = -1f, float delay = -1f, float targetScale = -1f)
        {
            ActionType = action;
            if (duration >= 0) { UseMaxDuration = true; MaxDuration = duration; }
            if (delay >= 0) StartDelay = delay;
            if (targetScale >= 0) TargetSlowMoScale = targetScale;
            
            StartGlobalAction();
        }

        public void StartGlobalAction() 
        {
            if (Mode == EngineMode.LocalZonesOnly) return; 
            if (GlobalTimeState != TimeState.Normal && GlobalTimeState != TimeState.PostAction) return;

            if (StartDelay > 0f)
            {
                _isWaitingForDelay = true;
                _currentDelayTimer = StartDelay;
                return; 
            }

            ExecuteActionImmediate();
        }

        private void ExecuteActionImmediate()
        {
            _currentActionTimer = 0f; 

            if (UseEntryCurve) 
                SetState(TimeState.PreAction);
            else
            {
                SetState(GetStateForActionType());
                CurrentGlobalTimeScale = GetTargetTimeScaleForAction();
            }
            _stateTimer = 0f; 
        }

        public void StopGlobalAction() 
        {
            if (Mode == EngineMode.LocalZonesOnly) return;
            
            _isWaitingForDelay = false;

            if (GlobalTimeState == TimeState.PreAction)
            {
                SetState(UseExitCurve ? TimeState.PostAction : TimeState.Normal);
                _stateTimer = 0f;
                return;
            }

            if (GlobalTimeState == TimeState.Rewinding)
            {
                _transformJobHandle.Complete();

                for (int i = 0; i < _identities.Count; i++)
                {
                    _identities[i].Buffer.CommitRewind(Mathf.FloorToInt(_identities[i].FractionalOffset));
                    _identities[i].FractionalOffset = 0f;
                }
                _globalFractionalFrames = 0f; 
            }

            if (UseExitCurve) 
                SetState(TimeState.PostAction);
            else
            {
                SetState(TimeState.Normal);
                CurrentGlobalTimeScale = 1f;
            }
            _stateTimer = 0f; 
        }

        private void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= _headIndices.Length) return;

            int newCapacity = Mathf.Max(_headIndices.Length * 2, requiredCapacity);

            _transformAccessArray.capacity = newCapacity;

            ResizeNativeArray(ref _headIndices, newCapacity);
            ResizeNativeArray(ref _readIndicesA, newCapacity);
            ResizeNativeArray(ref _readIndicesB, newCapacity);
            ResizeNativeArray(ref _lerpFactors, newCapacity);
            ResizeNativeArray(ref _historyBufferPointers, newCapacity);
            ResizeNativeArray(ref _recordFlags, newCapacity);
            ResizeNativeArray(ref _applyFlags, newCapacity);
        }

        private void ResizeNativeArray<T>(ref NativeArray<T> oldArray, int newCapacity) where T : struct
        {
            NativeArray<T> newArray = new NativeArray<T>(newCapacity, Allocator.Persistent);
            
            if (oldArray.IsCreated)
            {
                NativeArray<T>.Copy(oldArray, newArray, oldArray.Length);
                oldArray.Dispose(); 
            }
            
            oldArray = newArray;
        }

        public unsafe void RegisterIdentity(TimeWarpIdentity identity)
        {
            if (!_isInitialized) return;
            
            _transformJobHandle.Complete();
            
            EnsureCapacity(_identities.Count + 1);

            identity.ManagerIndex = _identities.Count;
            _identities.Add(identity);
            _transformAccessArray.Add(identity.transform);

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(identity.Buffer.TransformHistory);
            _historyBufferPointers[identity.ManagerIndex] = (IntPtr)ptr;
            
            _recordFlags[identity.ManagerIndex] = 0;
            _applyFlags[identity.ManagerIndex] = 0;
        }

        public void UnregisterIdentity(TimeWarpIdentity identity)
        {
            if (!_isInitialized || !_identities.Contains(identity)) return;

            _transformJobHandle.Complete();

            int indexToRemove = identity.ManagerIndex;
            int lastIndex = _identities.Count - 1;

            _transformAccessArray.RemoveAtSwapBack(indexToRemove);
            _identities[indexToRemove] = _identities[lastIndex];
            _identities[indexToRemove].ManagerIndex = indexToRemove;
            _identities.RemoveAt(lastIndex);

            if (indexToRemove != lastIndex) 
            {
                _historyBufferPointers[indexToRemove] = _historyBufferPointers[lastIndex];
                _recordFlags[indexToRemove] = _recordFlags[lastIndex]; 
                _applyFlags[indexToRemove] = _applyFlags[lastIndex]; 
                
                _headIndices[indexToRemove] = _headIndices[lastIndex];
                _readIndicesA[indexToRemove] = _readIndicesA[lastIndex];
                _readIndicesB[indexToRemove] = _readIndicesB[lastIndex];
                _lerpFactors[indexToRemove] = _lerpFactors[lastIndex];
            }
        }

        // --- Method queuing orphaned buffers ---
        public void QueueBufferForDisposal(TimeWarpBuffer buffer)
        {
            if (buffer == null) return;
            _disposalQueue.Enqueue(buffer);
        }

        private void ProcessDisposalQueue()
        {
            while (_disposalQueue.Count > 0)
            {
                var buffer = _disposalQueue.Dequeue();
                buffer?.Dispose();
            }
        }

        private void FixedUpdate()
        {
            if (!_isInitialized || _identities.Count == 0) return;

            using (TimeWarpProfilerMarkers.WaitForTransformJob.Auto())
            {
                _transformJobHandle.Complete();
            }

            // ==========================================
            // PERFORMANCE FIX: CACHE STATES (FIXED UPDATE)
            // ==========================================
            using (TimeWarpProfilerMarkers.PreCalculateFixed.Auto())
            {
                for (int i = 0; i < _identities.Count; i++)
                {
                    _identities[i].PreCalculateState();
                }
            }

            if (GlobalTimeState == TimeState.Normal || 
                GlobalTimeState == TimeState.PreAction || 
                GlobalTimeState == TimeState.PostAction || 
                GlobalTimeState == TimeState.SlowMotion)
            {
                RecordFrame();
            }

            using (TimeWarpProfilerMarkers.TickFixedUpdate.Auto())
            {
                for (int i = 0; i < _identities.Count; i++)
                {
                    _identities[i].TickFixedUpdate();
                }
            }
        }

        private void Update()
        {
            if (!_isInitialized || _identities.Count == 0) return;

            _transformJobHandle.Complete();

            // ==========================================
            // PERFORMANCE FIX: CACHE STATES (UPDATE)
            // ==========================================
            for (int i = 0; i < _identities.Count; i++)
            {
                _identities[i].PreCalculateState();
            }

            if (_isWaitingForDelay)
            {
                _currentDelayTimer -= Time.unscaledDeltaTime;
                if (_currentDelayTimer <= 0)
                {
                    _isWaitingForDelay = false;
                    ExecuteActionImmediate();
                }
                return; 
            }

            float targetTimeScale = GetTargetTimeScaleForAction();
            bool runTransformJob = false;

            if (UseMaxDuration && (GlobalTimeState == TimeState.SlowMotion || GlobalTimeState == TimeState.Rewinding || GlobalTimeState == TimeState.TimeStop))
            {
                _currentActionTimer += Time.unscaledDeltaTime;
                if (_currentActionTimer >= MaxDuration) StopGlobalAction();
            }

            switch (GlobalTimeState)
            {
                case TimeState.PreAction:
                    _stateTimer += Time.unscaledDeltaTime;
                    float preT = Mathf.Clamp01(_stateTimer / EntryDuration);
                    CurrentGlobalTimeScale = Mathf.Lerp(1f, targetTimeScale, EntryCurve.Evaluate(preT));
                    if (_stateTimer >= EntryDuration)
                    {
                        SetState(GetStateForActionType());
                        _stateTimer = 0f;
                        CurrentGlobalTimeScale = targetTimeScale; 
                    }
                    break;

                case TimeState.Rewinding:
                    _stateTimer += Time.unscaledDeltaTime;
                    float currentRewindSpeed = RewindAccelerationCurve.Evaluate(_stateTimer) * RewindSpeedMultiplier;
                    _globalFractionalFrames += currentRewindSpeed * (Time.unscaledDeltaTime * CaptureFramerate);
                    runTransformJob = true; 
                    break;
                    
                case TimeState.SlowMotion:
                    CurrentGlobalTimeScale = TargetSlowMoScale;
                    break;

                case TimeState.PostAction:
                    _stateTimer += Time.unscaledDeltaTime;
                    float postT = Mathf.Clamp01(_stateTimer / ExitDuration);
                    CurrentGlobalTimeScale = Mathf.Lerp(targetTimeScale, 1f, ExitCurve.Evaluate(postT));
                    if (_stateTimer >= ExitDuration)
                    {
                        SetState(TimeState.Normal);
                        CurrentGlobalTimeScale = 1f;
                    }
                    break;
            }

            for (int i = 0; i < _identities.Count; i++)
            {
                var id = _identities[i];
                float eScale = id.EffectiveTimeScale;
                float maxAllowedOffset = id.Buffer.Count - 2;

                if (GlobalTimeState == TimeState.Rewinding && Mode != EngineMode.LocalZonesOnly)
                {
                    if (id.IsRewinding) 
                    {
                        float speedMultiplier = (id.ActionFilters != null && id.ActionFilters.OverrideRewindSpeed) 
                                                ? id.ActionFilters.CustomRewindSpeedMultiplier 
                                                : 1.0f;
                        float customRewindFrames = _globalFractionalFrames * speedMultiplier;
                        
                        id.FractionalOffset = Mathf.Min(customRewindFrames, maxAllowedOffset);
                        runTransformJob = true;
                    }
                    else
                    {
                        id.FractionalOffset = 0f;
                    }
                }
                else 
                {
                    if (eScale < 0)
                    {
                        id.FractionalOffset += Mathf.Abs(eScale) * (Time.unscaledDeltaTime * CaptureFramerate);
                        if (id.FractionalOffset >= maxAllowedOffset) id.FractionalOffset = maxAllowedOffset; 
                        runTransformJob = true;
                    }
                    else if (eScale > 0 && id.FractionalOffset > 0)
                    {
                        id.FractionalOffset -= eScale * (Time.unscaledDeltaTime * CaptureFramerate);
                        if (id.FractionalOffset <= 0) 
                        {
                            id.FractionalOffset = 0;
                            id.Buffer.CommitRewind(0); 
                        }
                        runTransformJob = true;
                    }
                    else if (eScale == 0 && id.FractionalOffset > 0)
                    {
                        runTransformJob = true; 
                    }
                }
            }

            if (runTransformJob) ApplyRewindFrame();

            using (TimeWarpProfilerMarkers.TickUpdate.Auto())
            {
                for (int i = 0; i < _identities.Count; i++)
                {
                    _identities[i].TickUpdate();
                }
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized) return;

            using (TimeWarpProfilerMarkers.WaitForTransformJob.Auto())
            {
                _transformJobHandle.Complete();
            }

            using (TimeWarpProfilerMarkers.ProcessDisposalQueue.Auto())
            {
                ProcessDisposalQueue();
            }
        }

        private float GetTargetTimeScaleForAction()
        {
            switch (ActionType)
            {
                case GlobalAction.SlowMotion: return TargetSlowMoScale;
                case GlobalAction.Rewind: return 0f;   
                case GlobalAction.TimeStop: return 0f; 
                default: return 1f;
            }
        }

        private TimeState GetStateForActionType()
        {
            switch (ActionType)
            {
                case GlobalAction.SlowMotion: return TimeState.SlowMotion;
                case GlobalAction.Rewind: return TimeState.Rewinding;
                case GlobalAction.TimeStop: return TimeState.TimeStop;
                default: return TimeState.Normal;
            }
        }

        private unsafe void RecordFrame()
        {
            // 'using var' scopes the marker to the whole method, so it closes correctly
            // even if an early return is added here later.
            using var marker = TimeWarpProfilerMarkers.RecordFrame.Auto();

            for (int i = 0; i < _identities.Count; i++)
            {
                var id = _identities[i];
                if (!id.CanRecordHistory)
                {
                    _recordFlags[i] = 0; 
                    continue;
                }

                byte rMask = 0;
                if ((id.Channels & TimeChannels.Transform) != 0)
                {
                    if (id.TransformSettings.RecordPosition) rMask |= 1;
                    if (id.TransformSettings.RecordRotation) rMask |= 2;
                    if (id.TransformSettings.RecordScale) rMask |= 4;
                }
                _recordFlags[i] = rMask;

                _headIndices[i] = id.Buffer.Head;
                
                if ((id.Channels & TimeChannels.Velocity) != 0 && id.CachedRb != null)
                {
                    if (id.EffectiveTimeScale <= 0.001f)
                    {
                        int prevIndex = id.Buffer.GetIndex(0);
                        id.Buffer.VelocityHistory[id.Buffer.Head] = id.Buffer.VelocityHistory[prevIndex];
                    }
                    else
                    {
                        id.Buffer.VelocityHistory[id.Buffer.Head] = new VelocitySnapshot(id.CachedRb, id.EffectiveTimeScale);
                    }
                }

                if ((id.Channels & TimeChannels.Velocity2D) != 0 && id.CachedRb2D != null)
                {
                    if (id.EffectiveTimeScale <= 0.001f)
                    {
                        int prevIndex = id.Buffer.GetIndex(0);
                        id.Buffer.Velocity2DHistory[id.Buffer.Head] = id.Buffer.Velocity2DHistory[prevIndex];
                    }
                    else
                    {
                        id.Buffer.Velocity2DHistory[id.Buffer.Head] = new Velocity2DSnapshot(id.CachedRb2D, id.EffectiveTimeScale);
                    }
                }
                
                id.Buffer.AdvanceHead();
            }

            var recordJob = new RecordTransformsJob
            {
                HistoryBuffers = (TransformSnapshot**)NativeArrayUnsafeUtility.GetUnsafePtr(_historyBufferPointers),
                HeadIndices = _headIndices,
                RecordFlags = _recordFlags 
            };
            
            _transformJobHandle = recordJob.Schedule(_transformAccessArray);
        }

        private unsafe void ApplyRewindFrame()
        {
            // 'using var' scopes the marker to the whole method, so it closes correctly
            // even if an early return is added here later.
            using var marker = TimeWarpProfilerMarkers.ApplyRewindFrame.Auto();

            for (int i = 0; i < _identities.Count; i++)
            {
                var id = _identities[i];
                bool isRewinding = id.EffectiveTimeScale < 0f || id.FractionalOffset > 0f;

                byte aMask = 0;
                if (isRewinding && (id.Channels & TimeChannels.Transform) != 0)
                {
                    if (id.TransformSettings.RecordPosition) aMask |= 1;
                    if (id.TransformSettings.RecordRotation) aMask |= 2;
                    if (id.TransformSettings.RecordScale) aMask |= 4;
                    if (id.TransformSettings.Interpolation == RewindInterpolation.Lerp) aMask |= 8;
                }
                _applyFlags[i] = aMask;

                id.Buffer.GetFractionalIndex(id.FractionalOffset, out int idxA, out int idxB, out float lerpT);

                _readIndicesA[i] = idxA;
                _readIndicesB[i] = idxB;
                _lerpFactors[i] = lerpT;
            }

            var rewindJob = new ApplyTransformsJob
            {
                HistoryBuffers = (TransformSnapshot**)NativeArrayUnsafeUtility.GetUnsafePtr(_historyBufferPointers),
                ReadIndicesA = _readIndicesA,
                ReadIndicesB = _readIndicesB,
                LerpFactors = _lerpFactors,
                ApplyFlags = _applyFlags 
            };

            _transformJobHandle = rewindJob.Schedule(_transformAccessArray);
        }

        private void OnDestroy()
        {
            // Release the singleton slot so a manager in a newly loaded scene can take over
            // immediately, instead of relying on Unity's fake-null behaviour.
            if (Instance == this) Instance = null;

            if (!_isInitialized) return;
            _transformJobHandle.Complete();
            CurrentGlobalTimeScale = 1f; 
            ProcessDisposalQueue();
            
            if (_transformAccessArray.isCreated) _transformAccessArray.Dispose();
            if (_headIndices.IsCreated) _headIndices.Dispose();
            if (_readIndicesA.IsCreated) _readIndicesA.Dispose();
            if (_readIndicesB.IsCreated) _readIndicesB.Dispose();
            if (_lerpFactors.IsCreated) _lerpFactors.Dispose();
            if (_historyBufferPointers.IsCreated) _historyBufferPointers.Dispose();
            if (_recordFlags.IsCreated) _recordFlags.Dispose(); 
            if (_applyFlags.IsCreated) _applyFlags.Dispose(); 
        }
    }
}