using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using TimeWarp.Core.Data;
using TimeWarp.Core.Managers;
using TimeWarp.Modules.Zones;

namespace TimeWarp.Core
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TimeWarp/Core/Time Warp Identity")]
    public class TimeWarpIdentity : MonoBehaviour
    {
        // Global gate used while cloning objects (see TimeParadoxModule) so that cloned
        // identities do not allocate a history buffer during Instantiate().
        // MUST be reset on Play: if it survives a session stuck at 'true'
        // (domain reload disabled, or an exception during cloning), NOTHING registers
        // with the manager and the engine appears completely dead with no error.
        public static bool GlobalMuteRegistration = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            GlobalMuteRegistration = false;
        }

        [HideInInspector] 
        public string AppliedRuleName = "";

        [Header("Core Settings")]
        public TimeChannels Channels = TimeChannels.Transform | TimeChannels.Velocity | TimeChannels.Custom | TimeChannels.Particles | TimeChannels.Audio | TimeChannels.Animation;
        public float LocalTimeScale = 1.0f;
        public float FractionalOffset = 0f;
        public bool FreeMemoryOnDisable = false;

        [Space(10)]
        public TimeWarpActionFilters ActionFilters = new TimeWarpActionFilters();

        private bool _cachedCanRecordHistory;
        private float _cachedEffectiveTimeScale;
        private bool _cachedIsRewinding;

        public bool CanRecordHistory => _cachedCanRecordHistory;
        public float EffectiveTimeScale => _cachedEffectiveTimeScale;
        public bool IsRewinding => _cachedIsRewinding;

        [Space(10)]
        public TransformChannelSettings TransformSettings = new TransformChannelSettings();
        public VelocityChannelSettings VelocitySettings = new VelocityChannelSettings();
        public Velocity2DChannelSettings Velocity2DSettings = new Velocity2DChannelSettings();
        public CustomChannelSettings CustomSettings = new CustomChannelSettings();
        public ParticleChannelSettings ParticleSettings = new ParticleChannelSettings();
        public VfxChannelSettings VfxSettings = new VfxChannelSettings();
        public AudioChannelSettings AudioSettings = new AudioChannelSettings();
        public NavigationChannelSettings NavigationSettings = new NavigationChannelSettings();
        public EchoChannelSettings EchoSettings = new EchoChannelSettings();

        [Space(10)]
        [Header("Local Events")]
        public UnityEvent OnLocalRewindStart;
        public UnityEvent OnLocalRewindStop;
        public UnityEvent OnLocalTimeStopStart;
        public UnityEvent OnLocalTimeStopStop;
        public UnityEvent OnLocalSlowMoStart;
        public UnityEvent OnLocalSlowMoStop;
        public UnityEvent<float> OnTimeScaleChanged;

        [HideInInspector] public float RecordTimeOverride = 0f;
        [HideInInspector] public int ManagerIndex = -1;
        
        public TimeWarpBuffer Buffer { get; private set; }
        public Rigidbody CachedRb { get; private set; }
        public Rigidbody2D CachedRb2D { get; private set; }
        
        public bool IsRegistered => ManagerIndex != -1 && Buffer != null;

        private enum LocalState { Normal, SlowMo, TimeStop, Rewind }
        private LocalState _currentLocalState = LocalState.Normal;
        private float _lastEffectiveScale = 1f;

        private ITimeWarpModule[] _modules;
        private bool _wasRecordingLastFrame = true;
        private List<ITimeWarpModule> _cachedModuleList = new List<ITimeWarpModule>();

        // ==========================================
        // MODULE FAULT ISOLATION
        // ==========================================
        // ITimeWarpModule is the engine's public extension point, so it runs the code most
        // likely to contain bugs: modules freshly written by the user. Until now these
        // calls had no protection at all, and the tick loops are nested - the manager
        // iterates identities, each identity iterates its modules. A single exception
        // therefore aborted the manager's loop, so EVERY object after the faulty one
        // silently stopped ticking, every frame, with a maximally confusing symptom
        // ("time stopped working for half the scene").
        //
        // Each module is now called inside try/catch, and a module that keeps throwing is
        // taken out of the rotation instead of spamming the console forever.
        //
        // try/catch costs nothing when nothing is thrown - there is no per-frame overhead.

        /// <summary>Consecutive-failure budget before a module is disabled for this session.</summary>
        private const int ModuleFailureLimit = 3;

        /// <summary>Failure counters, index-aligned with _modules. Null until a fault occurs.</summary>
        private int[] _moduleFailures;

        /// <summary>Modules taken out of the rotation, index-aligned with _modules.</summary>
        private bool[] _moduleDisabled;

        public void RefreshModules()
        {
            GetComponentsInChildren<ITimeWarpModule>(true, _cachedModuleList);
            var myModules = new List<ITimeWarpModule>();

            for (int i = 0; i < _cachedModuleList.Count; i++)
            {
                var monoBehavior = _cachedModuleList[i] as MonoBehaviour;
                if (monoBehavior != null && monoBehavior.GetComponentInParent<TimeWarpIdentity>() == this) 
                    myModules.Add(_cachedModuleList[i]);
            }
            _modules = myModules.ToArray();

            // The parallel fault arrays must match the new module layout, otherwise a
            // stale index would disable the wrong module.
            _moduleFailures = null;
            _moduleDisabled = null;
        }

        /// <summary>
        /// Invokes one module callback with fault isolation.
        /// Returns quietly if the module was already disabled by the circuit breaker.
        /// </summary>
        private void SafeInvokeModule(int index, bool fixedUpdate)
        {
            if (_moduleDisabled != null && _moduleDisabled[index]) return;

            ITimeWarpModule module = _modules[index];

            // A module destroyed at runtime without calling RefreshModules() leaves a
            // fake-null entry here. Comparing through the MonoBehaviour cast is what
            // actually triggers Unity's overloaded == operator.
            if (module == null || (module as MonoBehaviour) == null) return;

            try
            {
                if (fixedUpdate) module.OnWarpFixedUpdate();
                else module.OnWarpUpdate();

                // A clean frame forgives earlier trouble, so an intermittent hiccup does
                // not slowly accumulate into a permanent shutdown.
                if (_moduleFailures != null) _moduleFailures[index] = 0;
            }
            catch (System.Exception ex)
            {
                HandleModuleFailure(index, module, ex);
            }
        }

        private void HandleModuleFailure(int index, ITimeWarpModule module, System.Exception ex)
        {
            if (_moduleFailures == null) _moduleFailures = new int[_modules.Length];
            if (_moduleDisabled == null) _moduleDisabled = new bool[_modules.Length];

            _moduleFailures[index]++;

            string moduleName = module != null ? module.GetType().Name : "<destroyed module>";

            if (_moduleFailures[index] >= ModuleFailureLimit)
            {
                _moduleDisabled[index] = true;

                Debug.LogError(
                    $"[TimeWarp] Module \"{moduleName}\" on \"{name}\" threw {ModuleFailureLimit} times " +
                    $"and has been disabled for the rest of this session so it cannot stall the rest " +
                    $"of the scene. Fix the exception below, then re-enter Play mode " +
                    $"(or call RefreshModules() on this object).\n{ex}", this);
            }
            else
            {
                Debug.LogException(ex, this);
            }
        }

        /// <summary>
        /// Puts every circuit-broken module back into the rotation. Useful after fixing
        /// state at runtime; RefreshModules() does this implicitly.
        /// </summary>
        public void ResetModuleFaults()
        {
            _moduleFailures = null;
            _moduleDisabled = null;
        }

        public void PreCalculateState()
        {
            if (TimeWarpManager.Instance == null) return;

            // 1. Culling Permissions
            bool localAllowRewind = ActionFilters != null ? ActionFilters.AllowRewind : true;
            bool localAllowSlowMo = ActionFilters != null ? ActionFilters.AllowSlowMo : true;
            bool localAllowTimeStop = ActionFilters != null ? ActionFilters.AllowTimeStop : true;

            if (ActionFilters != null)
            {
                if (ActionFilters.RequiresZoneForRewind)
                {
                    localAllowRewind = false;
                    if (ActionFilters.RewindZones != null)
                    {
                        for (int i = 0; i < ActionFilters.RewindZones.Count; i++)
                        {
                            var z = ActionFilters.RewindZones[i];
                            if (z != null && z.IsZoneActive) { localAllowRewind = true; break; }
                        }
                    }
                }

                if (ActionFilters.RequiresZoneForSlowMo)
                {
                    localAllowSlowMo = false;
                    if (ActionFilters.SlowMoZones != null)
                    {
                        for (int i = 0; i < ActionFilters.SlowMoZones.Count; i++)
                        {
                            var z = ActionFilters.SlowMoZones[i];
                            if (z != null && z.IsZoneActive) { localAllowSlowMo = true; break; }
                        }
                    }
                }

                if (ActionFilters.RequiresZoneForTimeStop)
                {
                    localAllowTimeStop = false;
                    if (ActionFilters.TimeStopZones != null)
                    {
                        for (int i = 0; i < ActionFilters.TimeStopZones.Count; i++)
                        {
                            var z = ActionFilters.TimeStopZones[i];
                            if (z != null && z.IsZoneActive) { localAllowTimeStop = true; break; }
                        }
                    }
                }
            }

            // 2. CALCULATING RECORDING ZONES
            _cachedCanRecordHistory = true;
            if (ActionFilters != null && ActionFilters.RecordOnlyInZone)
            {
                _cachedCanRecordHistory = false;
                if (ActionFilters.RecordingZones != null)
                {
                    for (int i = 0; i < ActionFilters.RecordingZones.Count; i++)
                    {
                        var z = ActionFilters.RecordingZones[i];
                        if (z != null && z.IsZoneActive) 
                        { 
                            _cachedCanRecordHistory = true; 
                            break; 
                        }
                    }
                }
            }

            // 3. DETERMINING TIME SCALE
            EngineMode mode = TimeWarpManager.Instance.Mode;
            TimeState globalState = TimeWarpManager.Instance.GlobalTimeState;
            float finalGlobalScale = TimeWarpManager.Instance.CurrentGlobalTimeScale;

            if (ActionFilters != null)
            {
                if (globalState == TimeState.TimeStop && !localAllowTimeStop) 
                    finalGlobalScale = 1f;
                else if (globalState == TimeState.SlowMotion)
                {
                    if (!localAllowSlowMo) 
                        finalGlobalScale = 1f;
                    else if (ActionFilters.OverrideSlowMoScale) 
                        finalGlobalScale = ActionFilters.CustomSlowMoScale;
                }
            }

            if (mode == EngineMode.GlobalOnly) _cachedEffectiveTimeScale = finalGlobalScale;
            else if (mode == EngineMode.LocalZonesOnly) _cachedEffectiveTimeScale = LocalTimeScale;
            else _cachedEffectiveTimeScale = LocalTimeScale * finalGlobalScale;

            // 4. DETERMINING REWIND
            bool wantsToRewind = (TimeWarpManager.Instance.Mode != EngineMode.LocalZonesOnly && TimeWarpManager.Instance.GlobalTimeState == TimeState.Rewinding) || 
                                 LocalTimeScale < 0f || FractionalOffset > 0.001f;

            _cachedIsRewinding = wantsToRewind;

            if (ActionFilters != null)
            {
                if (_currentLocalState != LocalState.Rewind && FractionalOffset <= 0.001f)
                {
                    if (!localAllowRewind) _cachedIsRewinding = false;
                    else if (ActionFilters.RecordOnlyInZone && !_cachedCanRecordHistory) _cachedIsRewinding = false;
                }
            }

            // 5. BLOCKING RECORDING DURING REWIND
            if (_cachedIsRewinding || FractionalOffset > 0.001f) 
            {
                _cachedCanRecordHistory = false;
            }

            // 6. ANTI-TELEPORT FLUSH (OPTIMIZED)
            bool justFinishedRewinding = (_currentLocalState == LocalState.Rewind && !_cachedIsRewinding);

            if (Buffer != null && !_cachedIsRewinding && !justFinishedRewinding)
            {
                // FIX: Flush is executed only ONCE per frame, when CanRecordHistory becomes false
                if (!_cachedCanRecordHistory && _wasRecordingLastFrame) 
                {
                    FlushBufferToCurrentState();
                }
                else if (_cachedCanRecordHistory && !_wasRecordingLastFrame) 
                {
                    FlushBufferToCurrentState();
                }
                
                _wasRecordingLastFrame = _cachedCanRecordHistory;
            }
            else if (justFinishedRewinding)
            {
                _wasRecordingLastFrame = _cachedCanRecordHistory;
            }
        }

        private void Awake() 
        {
            CachedRb = GetComponent<Rigidbody>();
            CachedRb2D = GetComponent<Rigidbody2D>();
            RefreshModules();
        }

        private void OnEnable() 
        {
            if (GlobalMuteRegistration) return;
            if (TimeWarpManager.Instance != null) InitializeAndRegister();
            else StartCoroutine(RegisterWhenReady());
        }

        private IEnumerator RegisterWhenReady()
        {
            while (TimeWarpManager.Instance == null) yield return null;
            if (!this.enabled || !gameObject.activeInHierarchy) yield break;
            InitializeAndRegister();
        }

        private void InitializeAndRegister()
        {
            if (Buffer == null)
            {
                int framerate = TimeWarpManager.Instance.CaptureFramerate;
                float maxTime = RecordTimeOverride > 0.01f ? RecordTimeOverride : TimeWarpManager.Instance.MaxRecordTime;
                Buffer = new TimeWarpBuffer(framerate, maxTime);

                TransformSnapshot initialSnap = new TransformSnapshot(transform);
                for (int i = 0; i < Buffer.Count; i++) Buffer.TransformHistory[i] = initialSnap;
            }
            TimeWarpManager.Instance.RegisterIdentity(this);
        }

        public void TickFixedUpdate()
        {
            if (_modules != null)
                for (int i = 0; i < _modules.Length; i++) SafeInvokeModule(i, fixedUpdate: true);
        }

        private void FlushBufferToCurrentState()
        {
            if (Buffer == null || !Buffer.TransformHistory.IsCreated) return;
            TransformSnapshot currentSnap = new TransformSnapshot(transform);
            for (int i = 0; i < Buffer.Count; i++) Buffer.TransformHistory[i] = currentSnap;
        }

        // ==========================================
        // PUBLIC HISTORY RESET
        // ==========================================
        /// <summary>
        /// Discards all recorded history and refills it with the object's CURRENT pose, so
        /// a rewind started right after this call keeps the object where it is instead of
        /// dragging it back along the old path.
        ///
        /// CALL THIS AFTER ANY NON-CONTINUOUS MOVE - teleport, respawn, checkpoint restore,
        /// loading a save, or activating a pooled object. Without it the buffer still holds
        /// the route from before the jump, and a rewind will pull the object back across
        /// the level, straight through walls.
        ///
        /// The engine already does this internally whenever recording starts or stops; this
        /// simply exposes the same operation, which previously had no reachable equivalent
        /// from gameplay code.
        /// </summary>
        /// <param name="clearVelocities">
        /// Also zeroes the recorded velocity history. Leave true unless you deliberately
        /// want the object to inherit its pre-teleport momentum during a rewind.
        /// </param>
        public void ResetHistory(bool clearVelocities = true)
        {
            if (Buffer == null) return;

            FlushBufferToCurrentState();

            // Rewinding position without rewinding depth would leave the object pointing
            // into history that no longer exists.
            FractionalOffset = 0f;

            if (!clearVelocities) return;

            // Only touch the velocity arrays if they were actually allocated - reading the
            // properties would otherwise force an allocation we just avoided.
            if (Buffer.HasVelocityHistory)
            {
                var velocities = Buffer.VelocityHistory;
                System.Array.Clear(velocities, 0, velocities.Length);
            }

            if (Buffer.HasVelocity2DHistory)
            {
                var velocities2D = Buffer.Velocity2DHistory;
                System.Array.Clear(velocities2D, 0, velocities2D.Length);
            }
        }

        public void TickUpdate()
        {
            float currentScale = EffectiveTimeScale;
            bool isRewinding = IsRewinding;

            if (!Mathf.Approximately(currentScale, _lastEffectiveScale))
            {
                OnTimeScaleChanged?.Invoke(currentScale);
                _lastEffectiveScale = currentScale;
            }

            LocalState newState = LocalState.Normal;
            if (isRewinding) newState = LocalState.Rewind;
            else if (currentScale <= 0.001f) newState = LocalState.TimeStop;
            else if (currentScale < 0.99f) newState = LocalState.SlowMo;

            if (newState != _currentLocalState)
            {
                switch (_currentLocalState)
                {
                    case LocalState.Rewind: OnLocalRewindStop?.Invoke(); break;
                    case LocalState.TimeStop: OnLocalTimeStopStop?.Invoke(); break;
                    case LocalState.SlowMo: OnLocalSlowMoStop?.Invoke(); break;
                }
                switch (newState)
                {
                    case LocalState.Rewind: OnLocalRewindStart?.Invoke(); break;
                    case LocalState.TimeStop: OnLocalTimeStopStart?.Invoke(); break;
                    case LocalState.SlowMo: OnLocalSlowMoStart?.Invoke(); break;
                }
                _currentLocalState = newState;
            }

            if (_modules != null)
                for (int i = 0; i < _modules.Length; i++) SafeInvokeModule(i, fixedUpdate: false);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (TimeWarpManager.Instance != null && ManagerIndex != -1) 
            {
                TimeWarpManager.Instance.UnregisterIdentity(this);
                ManagerIndex = -1;
            }
            if (FreeMemoryOnDisable) RequestBufferDisposal();
        }

        private void OnApplicationQuit() => RequestBufferDisposal();
        private void OnDestroy() => RequestBufferDisposal();

        private void RequestBufferDisposal()
        {
            if (Buffer != null)
            {
                if (TimeWarpManager.Instance != null) TimeWarpManager.Instance.QueueBufferForDisposal(Buffer);
                else Buffer.Dispose();
                Buffer = null;
            }
        }
    }
}