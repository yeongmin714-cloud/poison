// File: TimeParadoxModule.cs
using UnityEngine;
using System.Collections.Generic;
using TimeWarp.Core;
using TimeWarp.Core.Data;
using TimeWarp.Core.Managers;
using TimeWarp.Modules.Animation; 
using TimeWarp.Modules.Gameplay; // Required for TimeEventRecorder
using TimeWarp.Core.Profiling;

namespace TimeWarp.Modules.Paradox
{
    /// <summary>
    /// Handles the creation of Time Echo Ghosts after a rewind has finished.
    /// Extracts historical data (Transforms, Animations, Events) and passes them to the clone.
    /// </summary>
    [RequireComponent(typeof(TimeWarpIdentity))]
    [DefaultExecutionOrder(-180)]
    [AddComponentMenu("TimeWarp/Paradox/Time Paradox Module")]
    public class TimeParadoxModule : MonoBehaviour, ITimeWarpModule
    {
        private TimeWarpIdentity _identity;
        private bool _wasRewinding;
        private int _rewindStartIndex;

        private List<TimeEchoGhost> _activeGhosts = new List<TimeEchoGhost>();
        private bool _isSpawningGhost = false;

        [Header("Performance")]
        [Tooltip("Reuse retired ghosts instead of destroying and re-cloning them.\n\n" +
                 "Spawning a ghost runs five GetComponentsInChildren sweeps and destroys every " +
                 "foreign component on the clone - on a rigged character that costs far more " +
                 "than the Instantiate itself. A pooled ghost has already been through that.\n\n" +
                 "Turn this off if you suspect a reused ghost is carrying over state.")]
        public bool UsePooling = true;
        
        // Prevents multiple ghosts from spawning in the exact same frame globally.
        // NOTE: compared against Time.unscaledTime, which RESETS TO 0 on every Play session.
        // Without the reset hook below, a stale value from a previous session (e.g. 300s)
        // would make "Time.unscaledTime - s_lastGlobalSpawnTime < 0.1f" evaluate to true
        // for minutes, silently disabling all ghost spawning.
        private static float s_lastGlobalSpawnTime = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_lastGlobalSpawnTime = float.NegativeInfinity;
        }

        private void Awake() => _identity = GetComponent<TimeWarpIdentity>();

        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate()
        {
            if (!_identity.IsRegistered || (_identity.Channels & TimeChannels.Echoes) == 0) return;

            bool isRewinding = _identity.IsRewinding;

            // Detect the exact moment the rewind starts
            if (isRewinding && !_wasRewinding)
            {
                _rewindStartIndex = _identity.Buffer.Head;
                _wasRewinding = true;
            }
            // Detect the exact moment the rewind stops
            else if (!isRewinding && _wasRewinding)
            {
                _wasRewinding = false;
                if (_identity.EchoSettings != null && _identity.EchoSettings.EnableEchoes)
                {
                    SpawnGhostFromHistory();
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-time checks for the ghost setup. Every one of these is a failure mode
        /// that is completely silent at runtime.
        /// </summary>
        private void OnValidate()
        {
            var identity = GetComponent<TimeWarpIdentity>();
            if (identity == null) return;

            var settings = identity.EchoSettings;
            if (settings == null) return;

            // A missing layer is the worst case: ghosts land on Default and collide with
            // everything, including the player, while the inspector still shows the name.
            if (!string.IsNullOrEmpty(settings.GhostLayerName) &&
                LayerMask.NameToLayer(settings.GhostLayerName) == -1)
            {
                Debug.LogWarning(
                    $"[TimeWarp] \"{name}\": ghost layer \"{settings.GhostLayerName}\" does not exist. " +
                    $"Ghosts will fall back to Default and collide with everything. " +
                    $"Create it under Project Settings > Tags and Layers, then set its " +
                    $"collision matrix under Project Settings > Physics.", this);
            }

            if (settings.HologramMaterial != null)
            {
                Shader shader = settings.HologramMaterial.shader;
                if (shader == null || !shader.isSupported)
                {
                    Debug.LogWarning(
                        $"[TimeWarp] \"{name}\": Hologram Material " +
                        $"\"{settings.HologramMaterial.name}\" uses an unsupported shader " +
                        $"({(shader == null ? "<missing>" : shader.name)}). Ghosts will render magenta. " +
                        $"This usually means a Built-in RP shader assigned in a URP/HDRP project.", this);
                }
            }

            if (settings.GhostPrefab == null)
            {
                Debug.Log(
                    $"[TimeWarp] \"{name}\": no Ghost Prefab assigned, so this object will clone " +
                    $"ITSELF for echoes. That is supported, but a dedicated lightweight prefab " +
                    $"is usually cheaper.", this);
            }
        }
#endif

        /// <summary>
        /// Removes the components that must never run on a ghost. This is the expensive
        /// part of spawning (four full hierarchy sweeps) and is exactly what pooling
        /// lets us skip on reuse.
        /// </summary>
        private static void StripClone(GameObject ghostObj)
        {
            var allIdentities = ghostObj.GetComponentsInChildren<TimeWarpIdentity>(true);
            foreach (var id in allIdentities) Destroy(id);

            var allParadoxes = ghostObj.GetComponentsInChildren<TimeParadoxModule>(true);
            foreach (var p in allParadoxes) Destroy(p);

            var cameras = ghostObj.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cameras) Destroy(cam);

            var behaviours = ghostObj.GetComponentsInChildren<Behaviour>(true);
            foreach (var comp in behaviours)
            {
                if (comp == null) continue;
                string tName = comp.GetType().Name;
                if (tName.Contains("Cinemachine") || tName.Contains("AudioListener")) Destroy(comp);
            }
        }

        private void SpawnGhostFromHistory()
        {
            if (_isSpawningGhost) return;
            
            int currentIndex = _identity.Buffer.Head;
            int framesRewound = _rewindStartIndex - currentIndex;
            
            // Handle circular buffer wrap-around
            if (framesRewound < 0) framesRewound += _identity.Buffer.Count;
            
            // Do not spawn a ghost if the rewind was extremely short (less than 10 frames)
            if (framesRewound < 10) return; 

            EchoChannelSettings settings = _identity.EchoSettings;

            // Global spawn throttling to prevent massive lag spikes
            if (Time.unscaledTime - s_lastGlobalSpawnTime < 0.1f) return;
            s_lastGlobalSpawnTime = Time.unscaledTime;

            _isSpawningGhost = true;

            // Clean up destroyed ghosts and enforce the maximum concurrent echoes limit
            _activeGhosts.RemoveAll(g => g == null);
            if (_activeGhosts.Count >= settings.MaxConcurrentEchoes && _activeGhosts.Count > 0)
            {
                var oldest = _activeGhosts[0];
                _activeGhosts.RemoveAt(0);
                // RetireNow pools the ghost when pooling is on, destroys it otherwise.
                if (oldest != null) oldest.RetireNow();
            }

            GameObject prefabToSpawn = settings.GhostPrefab != null ? settings.GhostPrefab : gameObject;

            using var spawnMarker = TimeWarpProfilerMarkers.GhostSpawn.Auto();

            // FAST PATH: a retired ghost built from this same prefab is already stripped
            // and shaped correctly, so we skip the clone and all five component sweeps.
            TimeEchoGhost echoComp = UsePooling
                ? TimeGhostPool.TryRent(prefabToSpawn, transform.position, transform.rotation)
                : null;

            GameObject ghostObj;

            if (echoComp != null)
            {
                ghostObj = echoComp.gameObject;
            }
            else
            {
                // ==========================================
                // STATIC MUTE FLAG WRAPPER
                // ==========================================
                // 1. Lock the registration gate so cloned identities allocate no buffers
                // 2. Clone. Awake/OnEnable still run, but they exit early.
                // 3. Unlock the gate in a finally block.
                //
                // SAFETY: try/finally is mandatory. If Instantiate throws (bad prefab,
                // broken component in Awake), leaving the gate stuck at 'true' would
                // silently disable registration for EVERY TimeWarpIdentity spawned
                // afterwards, killing the whole engine with nothing pointing back here.
                GameObject freshObj = null;
                TimeWarpIdentity.GlobalMuteRegistration = true;
                try
                {
                    freshObj = Instantiate(prefabToSpawn, transform.position, transform.rotation);
                }
                finally
                {
                    TimeWarpIdentity.GlobalMuteRegistration = false;
                }

                ghostObj = freshObj;
                StripClone(ghostObj);
                echoComp = ghostObj.AddComponent<TimeEchoGhost>();
            }

            // Only pool-managed ghosts carry a key; without it they are destroyed as before.
            echoComp.PoolPrefabKey = UsePooling ? prefabToSpawn : null;
            // ==========================================

            // Extract the positional and rotational history (Path)
            TransformSnapshot[] extractedPath = new TransformSnapshot[framesRewound];
            for (int i = 0; i < framesRewound; i++)
            {
                int bufferIndex = (currentIndex + i) % _identity.Buffer.Count;
                extractedPath[i] = _identity.Buffer.TransformHistory[bufferIndex];
            }

            float fps = TimeWarpManager.Instance.CaptureFramerate;
            
            // 1. Extract Animation Data (if available)
            GhostAnimPackage animPkg = null;
            if (TryGetComponent(out TimeAnimator timeAnimator))
            {
                animPkg = timeAnimator.ExtractGhostData(currentIndex, framesRewound);
            }
            
            // 2. Extract Event Data (if available)
            // UPDATED: Now uses the robust TimeEventPayload array instead of simple ints
            TimeEventPayload[] eventPkg = null;
            if (TryGetComponent(out TimeEventRecorder eventRecorder))
            {
                eventPkg = eventRecorder.ExtractEventData(currentIndex, framesRewound);
            }
            
            // Initialize Ghost with the extracted data packages
            echoComp.Initialize(extractedPath, animPkg, eventPkg, fps, settings);
            
            // Initialize physical interactions (colliders, triggers, pushing)
            echoComp.InitializePhysics(settings.EnableGhostPhysics, settings.GhostLayerName);

            // Activate and register the new Ghost
            ghostObj.SetActive(true);
            _activeGhosts.Add(echoComp);
            _isSpawningGhost = false;
        }
    }
}