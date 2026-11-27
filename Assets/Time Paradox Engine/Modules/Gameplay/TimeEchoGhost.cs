// File: TimeEchoGhost.cs
using UnityEngine;
using TimeWarp.Core;      // Required for IGhostPreserve
using TimeWarp.Core.Data; // Required for TimeEventPayload

namespace TimeWarp.Modules.Paradox
{
    /// <summary>
    /// Represents the physical manifestation of a recorded timeline.
    /// Replays transforms, animations, and gameplay events from the past.
    /// </summary>
    public class TimeEchoGhost : MonoBehaviour
    {
        private TransformSnapshot[] _path;
        private GhostAnimPackage _animPkg;
        
        private Transform[] _ghostBones;
        
        /// <summary>
        /// Maps the Ghost's bone indices to the Original recorded bone indices based on bone names.
        /// Allows the Ghost prefab to have a completely different skeletal hierarchy.
        /// </summary>
        private int[] _boneMapping; 

        private EchoChannelSettings _settings;
        private Animator[] _animators;
        
        private float _framerate;
        private float _timer;
        private float _postPlaybackLife;
        private bool _isPlaying = true;
        
        // --- UNIVERSAL EVENT SYSTEM ---
        private TimeEventPayload[] _eventPath;
        private GhostEventReceiver _eventReceiver;
        private int _lastFiredFrame = -1;

        private Rigidbody _rb;
        private bool _physicsEnabled;
        
        // --- NEW: End of playback state ---
        private bool _isFinished = false;

        // ==========================================
        // POOLING
        // ==========================================
        /// <summary>
        /// The prefab this ghost was built from. Set by the spawner; when non-null the
        /// ghost is returned to TimeGhostPool at end of life instead of being destroyed.
        /// </summary>
        public GameObject PoolPrefabKey { get; set; }

        /// <summary>True once the expensive one-time stripping pass has already run.</summary>
        private bool _hasBeenStripped = false;

        private TimeGhostPusher _pusher;

        /// <summary>
        /// Assigning Renderer.material instantiates a new Material. Doing that on every
        /// reuse would leak one material per playback, so it is applied only once.
        /// </summary>
        private bool _hologramApplied = false;

        /// <summary>
        /// Logged once per session rather than once per ghost - otherwise a misconfigured
        /// layer would spam the console on every spawn.
        /// </summary>
        private static bool s_layerWarningIssued = false;

        /// <summary>Logged once per session, not once per ghost.</summary>
        private static bool s_hologramWarningIssued = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_layerWarningIssued = false;
            s_hologramWarningIssued = false;
        }

        /// <summary>
        /// Clears every piece of per-playback state so a pooled ghost behaves exactly
        /// like a freshly instantiated one.
        ///
        /// NOTE: _timer, _isFinished and _lastFiredFrame were previously never reset,
        /// because a fresh clone always starts with their default values. A reused
        /// ghost would otherwise start already "finished" and never play back.
        /// </summary>
        public void ResetForReuse()
        {
            _timer = 0f;
            _isFinished = false;
            _lastFiredFrame = -1;
            _isPlaying = true;
            gameObject.SetActive(true);
        }

        public void Initialize(TransformSnapshot[] path, GhostAnimPackage animPkg, TimeEventPayload[] eventPath, float captureFramerate, EchoChannelSettings settings)
        {
            // Safe stripping process that preserves original hitboxes as triggers.
            // Only ever needed once: a pooled ghost has already been stripped, and
            // re-running the sweep would destroy components we deliberately keep.
            if (!_hasBeenStripped)
            {
                SoftStripForeignScripts();
                _hasBeenStripped = true;
            }

            if (path != null) _path = (TransformSnapshot[])path.Clone();
            _animPkg = animPkg;
            _eventPath = eventPath; 
            
            _framerate = captureFramerate;
            _settings = settings;
            _postPlaybackLife = _settings.LifetimeAfterPlayback;
            _isPlaying = _settings.PlayAutomatically;

            // Explicitly reset playback state. Harmless on a fresh clone (these are
            // already at their defaults) and mandatory on a pooled one.
            _timer = 0f;
            _isFinished = false;
            _lastFiredFrame = -1;

            _animators = GetComponentsInChildren<Animator>(true);
            bool useBones = (_animPkg != null && _animPkg.BonePos != null && _animPkg.BoneCount > 0);

            // FIX: FORCE STATE RESET (Renderers)
            // If the ghost was cloned from a dead or invisible player, we must manually ensure it is fully visible.
            foreach (var rnd in GetComponentsInChildren<Renderer>(true))
            {
                rnd.enabled = true;
            }

            foreach (var anim in _animators)
            {
                if (anim.gameObject.GetComponent<TimeWarpDummyEventReceiver>() == null)
                    anim.gameObject.AddComponent<TimeWarpDummyEventReceiver>();

                anim.speed = 0f; 
                anim.applyRootMotion = false; 
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                
                if (useBones)
                {
                    anim.enabled = false; 
                }
                else
                {
                    // FIX: FORCE STATE RESET (Animator)
                    // If the kill-switch disabled the animator on the original prefab, force it on here.
                    anim.enabled = true; 
                }
            }

            // --- INTELLIGENT BONE MAPPING ---
            if (useBones)
            {
                _ghostBones = GetComponentsInChildren<Transform>(true);
                _boneMapping = new int[_ghostBones.Length];

                for (int i = 0; i < _ghostBones.Length; i++)
                {
                    _boneMapping[i] = -1; // Default to 'no match'
                    
                    if (_animPkg.BoneNames != null)
                    {
                        for (int j = 0; j < _animPkg.BoneCount; j++)
                        {
                            if (_ghostBones[i].name == _animPkg.BoneNames[j])
                            {
                                _boneMapping[i] = j; // Match found!
                                break;
                            }
                        }
                    }
                }
            }

            // Fetch the Event Receiver from the Ghost Prefab
            _eventReceiver = GetComponentInChildren<GhostEventReceiver>();

            if (_settings.HologramMaterial != null && !_hologramApplied)
            {
                // ==========================================
                // MAGENTA ("pink") GHOST DIAGNOSTIC
                // ==========================================
                // This is the ONLY place the engine replaces a renderer's material, so a
                // ghost that renders bright magenta almost always means this material's
                // shader is not usable: typically a Built-in Render Pipeline shader
                // (Standard, Legacy/*) assigned in a URP/HDRP project, a shader stripped
                // from the build, or a broken/missing shader reference.
                //
                // Unity substitutes its magenta error shader silently, so without this
                // check the only symptom is "the ghost is pink".
                Shader shader = _settings.HologramMaterial.shader;

                if (shader == null || !shader.isSupported)
                {
                    if (!s_hologramWarningIssued)
                    {
                        s_hologramWarningIssued = true;
                        Debug.LogWarning(
                            $"[TimeWarp] Hologram Material \"{_settings.HologramMaterial.name}\" uses a " +
                            $"shader that is not supported in the active render pipeline " +
                            $"(shader: {(shader == null ? "<missing>" : shader.name)}). Ghosts will render " +
                            $"magenta. Pick a shader that matches your pipeline - e.g. a URP shader in a " +
                            $"URP project - or clear the Hologram Material to keep the original look.", this);
                    }
                }

                foreach (var rnd in GetComponentsInChildren<Renderer>())
                    rnd.material = _settings.HologramMaterial;

                _hologramApplied = true;
            }

            if (_path != null && _path.Length > 0)
            {
                transform.position = _path[0].position;
                transform.rotation = _path[0].rotation;
                transform.localScale = _path[0].scale;
            }

            ForceVisualsToFrameZero();
        }

        private void SoftStripForeignScripts()
        {
            Collider[] originalColliders = GetComponentsInChildren<Collider>(true);
            foreach (var col in originalColliders)
            {
                col.isTrigger = true; 
            }

            // NOTE: the Rigidbody is deliberately NOT destroyed here.
            //
            // It used to be, and that was a real bug: Destroy() is deferred to the end of
            // the frame, so the GetComponent<Rigidbody>() call in InitializePhysics - which
            // runs moments later in the SAME frame - still found the doomed component and
            // its "== null" check returned false. The intended kinematic Rigidbody was
            // therefore never added, and one frame later _rb pointed at a destroyed object,
            // silently downgrading every physics-enabled ghost to transform-only movement.
            //
            // InitializePhysics now owns the Rigidbody's fate entirely: it reconfigures the
            // existing one when physics is enabled, or removes it when it is not.
            if (TryGetComponent(out UnityEngine.AI.NavMeshAgent nma)) Destroy(nma);

            MonoBehaviour[] allScripts = GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var script in allScripts)
            {
                if (script == null || 
                    script is TimeEchoGhost || 
                    script is TimeWarpDummyEventReceiver || 
                    script is GhostEventReceiver ||
                    script is IGhostPreserve) 
                    continue;
                    
                Destroy(script); 
            }
        }

        private void ForceVisualsToFrameZero()
        {
            if (_animPkg == null) return;

            if (_ghostBones != null && _animPkg.BonePos != null && _animPkg.BoneCount > 0)
            {
                ApplyBoneFrame(0, 0, 0f);
            }
            else if (_animators != null && _animators.Length > 0 && _animPkg.AnimPath != null && _animPkg.LayerCount > 0)
            {
                ApplyStateFrame(0, 0, 0f);
                foreach (var anim in _animators)
                {
                    if (anim.enabled) anim.Update(0f);
                }
            }
        }

        private void OnEnable()
        {
            if (_animators == null) return;
            foreach (var anim in _animators)
            {
                if (anim != null && anim.enabled) anim.Update(0f);
            }
        }

        public void StartPlayback() => _isPlaying = true;

        public void InitializePhysics(bool enablePhysics, string layerName)
        {
            _physicsEnabled = enablePhysics;
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            
            if (!enablePhysics)
            {
                foreach (var col in colliders) col.enabled = false;

                // Physics off: the source Rigidbody (kept alive by SoftStrip) must go,
                // otherwise the ghost would still be simulated.
                if (TryGetComponent(out Rigidbody unusedRb)) Destroy(unusedRb);
                _rb = null;
                return;
            }

            foreach (var col in colliders) col.enabled = true;

            // ==========================================
            // GHOST LAYER
            // ==========================================
            // NameToLayer returns -1 for a layer that does not exist. This used to fall
            // through to layer 0 (Default) in silence, which is the worst possible
            // outcome: the ghost ends up colliding with everything, including the player,
            // and the setup looks correct in the inspector because the NAME is still there.
            //
            // The engine also never touches the collision matrix, so even a correct layer
            // needs Project Settings > Physics configured by hand.
            int layerIndex = LayerMask.NameToLayer(layerName);

            if (layerIndex == -1)
            {
                layerIndex = 0;

                if (!s_layerWarningIssued)
                {
                    s_layerWarningIssued = true;
                    Debug.LogWarning(
                        $"[TimeWarp] Ghost layer \"{layerName}\" does not exist, so ghosts are being " +
                        $"placed on Default and will collide with everything - including the player.\n" +
                        $"Create the layer under Project Settings > Tags and Layers, then set which " +
                        $"layers it collides with under Project Settings > Physics.");
                }
            }

            foreach (Transform child in transform.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layerIndex;

            // Reuse the existing Rigidbody if the source had one, otherwise create it.
            // Either way it is reconfigured into a kinematic playback body.
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            
            _rb.isKinematic = true; 
            _rb.useGravity = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.interpolation = RigidbodyInterpolation.Interpolate; 
            
            // Guarded: without this a pooled ghost would accumulate one extra pusher
            // on every reuse.
            if (_pusher == null && !TryGetComponent(out _pusher))
            {
                _pusher = gameObject.AddComponent<TimeGhostPusher>();
            }
            _pusher.PushForceMultiplier = _settings.GhostPushForce;
        }

        private void Update()
        {
            if (!_isPlaying || _path == null || _path.Length == 0) return;

            if (!_physicsEnabled) ProcessPlayback(Time.deltaTime);
            if (_animPkg != null && _animPkg.AnimPath != null) UpdateAnimatorVisuals();
        }

        private void FixedUpdate()
        {
            if (!_isPlaying || _path == null || _path.Length == 0) return;
            if (_physicsEnabled) ProcessPlayback(Time.fixedDeltaTime);
        }

        private void ProcessPlayback(float dt)
        {
            // 1. WAITING FOR DESTRUCTION AT THE END
            if (_isFinished)
            {
                _postPlaybackLife -= dt;
                
                if (_postPlaybackLife <= 0) Retire();
                return;
            }

            _timer += dt;
            float totalFrames = _timer * _framerate;
            int frameA = Mathf.FloorToInt(totalFrames);

            // 2. REACHED THE END OF THE PATH
            if (frameA >= _path.Length - 1)
            {
                _isFinished = true; // Stop evaluating animations and bones!
                
                Vector3 finalPos = _path[_path.Length - 1].position;
                Quaternion finalRot = _path[_path.Length - 1].rotation;
                Vector3 finalScale = _path[_path.Length - 1].scale; // Get the final scale
                
                if (_physicsEnabled && _rb != null) { _rb.MovePosition(finalPos); _rb.MoveRotation(finalRot); }
                else { transform.position = finalPos; transform.rotation = finalRot; }

                transform.localScale = finalScale;
                return;
            }

            int frameB = frameA + 1;
            float lerpT = totalFrames - frameA;

            // 3. SMOOTH MOVEMENT AND REAL SCALE
            Vector3 targetPos = Vector3.Lerp(_path[frameA].position, _path[frameB].position, lerpT);
            Quaternion targetRot = Quaternion.Slerp(_path[frameA].rotation, _path[frameB].rotation, lerpT);
            Vector3 targetScale = Vector3.Lerp(_path[frameA].scale, _path[frameB].scale, lerpT);

            if (_physicsEnabled && _rb != null) { _rb.MovePosition(targetPos); _rb.MoveRotation(targetRot); }
            else { transform.position = targetPos; transform.rotation = targetRot; }

            transform.localScale = targetScale; // Apply the recorded scale!

            // Trigger recorded historical events (shooting, etc.)
            if (_eventPath != null && frameA < _eventPath.Length)
            {
                TimeEventPayload currentPayload = _eventPath[frameA];
                if (currentPayload.EventHash != 0 && frameA != _lastFiredFrame)
                {
                    _lastFiredFrame = frameA;
                    if (_eventReceiver != null) _eventReceiver.ReceiveEvent(currentPayload);
                }
            }
        }

        /// <summary>
        /// Retire this ghost right now (used when the MaxConcurrentEchoes limit evicts
        /// the oldest one). Pools it when possible instead of destroying it.
        /// </summary>
        public void RetireNow() => Retire();

        /// <summary>
        /// End of life. Parks the ghost in the pool when it is pool-managed, otherwise
        /// falls back to the original destroy behaviour.
        /// </summary>
        private void Retire()
        {
            _isPlaying = false;

            if (PoolPrefabKey != null && TimeGhostPool.TryReturn(this))
            {
                gameObject.SetActive(false);
                return;
            }

            Destroy(gameObject);
        }

        private void ApplyStateFrame(int frameA, int frameB, float lerpT)
        {
            foreach (var anim in _animators)
            {
                if (anim == null) continue;

                if (_animPkg.FloatHashes != null)
                {
                    for (int f = 0; f < _animPkg.FloatHashes.Length; f++)
                    {
                        float valA = _animPkg.FloatHist[frameA * _animPkg.FloatHashes.Length + f];
                        float valB = _animPkg.FloatHist[frameB * _animPkg.FloatHashes.Length + f];
                        anim.SetFloat(_animPkg.FloatHashes[f], Mathf.Lerp(valA, valB, lerpT));
                    }
                }
                    
                if (_animPkg.IntHashes != null)
                {
                    for (int i = 0; i < _animPkg.IntHashes.Length; i++) 
                        anim.SetInteger(_animPkg.IntHashes[i], _animPkg.IntHist[frameA * _animPkg.IntHashes.Length + i]);
                }
                    
                if (_animPkg.BoolHashes != null)
                {
                    for (int b = 0; b < _animPkg.BoolHashes.Length; b++) 
                        anim.SetBool(_animPkg.BoolHashes[b], _animPkg.BoolHist[frameA * _animPkg.BoolHashes.Length + b]);
                }

                for (int layer = 0; layer < _animPkg.LayerCount; layer++)
                {
                    int indexA = frameA * _animPkg.LayerCount + layer;
                    int indexB = frameB * _animPkg.LayerCount + layer;

                    if (indexA >= _animPkg.AnimPath.Length) break;

                    AnimSnapshot snapA = _animPkg.AnimPath[indexA];
                    AnimSnapshot snapB = (indexB < _animPkg.AnimPath.Length) ? _animPkg.AnimPath[indexB] : snapA;

                    if (snapA.StateHash != 0) 
                    {
                        float lerpedTime = (snapA.StateHash == snapB.StateHash) 
                            ? Mathf.Lerp(snapA.NormalizedTime, snapB.NormalizedTime, lerpT) 
                            : snapA.NormalizedTime;

                        anim.Play(snapA.StateHash, layer, lerpedTime);
                    }
                }
            }
        }

        private void UpdateAnimatorVisuals()
        {
            // Safeguard: If we finished, DO NOT update the Animator (prevents mesh collapse)
            if (_isFinished || _animators == null || _animators.Length == 0 || _animPkg == null || _animPkg.LayerCount <= 0) return;

            float totalFrames = _timer * _framerate;
            int frameA = Mathf.FloorToInt(totalFrames);
            int frameB = frameA + 1;
            float lerpT = totalFrames - frameA;

            if (frameB >= _path.Length) 
            {
                frameA = _path.Length - 1;
                frameB = frameA;
                lerpT = 0f;
            }

            ApplyStateFrame(frameA, frameB, lerpT);
            
            foreach (var anim in _animators)
            {
                if (anim != null && anim.enabled) anim.Update(0f);
            }
        }

        private void ApplyBoneFrame(int frameA, int frameB, float lerpT)
        {
            for (int k = 0; k < _ghostBones.Length; k++)
            {
                int recordIndex = _boneMapping[k];
                if (recordIndex == -1) continue; 
                
                if (_ghostBones[k] == transform || _ghostBones[k].parent == null) continue;

                int offsetA = frameA * _animPkg.BoneCount + recordIndex;
                int offsetB = frameB * _animPkg.BoneCount + recordIndex;

                _ghostBones[k].localPosition = Vector3.Lerp(_animPkg.BonePos[offsetA], _animPkg.BonePos[offsetB], lerpT);
                _ghostBones[k].localRotation = Quaternion.Slerp(_animPkg.BoneRot[offsetA], _animPkg.BoneRot[offsetB], lerpT);
            }
        }

        private void LateUpdate()
        {
            // Safeguard: Stop calculating bones after reaching the end
            if (_isFinished || !_isPlaying || _path == null || _path.Length == 0) return;
            if (_ghostBones == null || _animPkg == null || _animPkg.BonePos == null || _animPkg.BoneCount <= 0) return;

            float totalFrames = _timer * _framerate;
            int frameA = Mathf.FloorToInt(totalFrames);
            int frameB = frameA + 1;
            float lerpT = totalFrames - frameA;

            if (frameB >= _path.Length) 
            {
                frameA = _path.Length - 1;
                frameB = frameA;
                lerpT = 0f;
            }

            ApplyBoneFrame(frameA, frameB, lerpT);
        }
    }

    /// <summary>
    /// Prevents Animator from throwing warnings about missing Animation Events on the Ghost.
    /// </summary>
    public class TimeWarpDummyEventReceiver : MonoBehaviour
    {
        [UnityEngine.Scripting.Preserve]
        public void OnFootstep(AnimationEvent animationEvent) { }

        [UnityEngine.Scripting.Preserve]
        public void OnLand(AnimationEvent animationEvent) { }
    }
}