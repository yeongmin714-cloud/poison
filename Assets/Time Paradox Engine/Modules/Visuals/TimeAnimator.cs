using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TimeWarp.Core;
using TimeWarp.Core.Data; 
using TimeWarp.Core.Managers; // Required for CaptureFramerate

namespace TimeWarp.Modules.Animation
{
    public enum AnimatorRecordMode
    {
        StateMachine,
        BoneSnapshot
    }

    [RequireComponent(typeof(TimeWarpIdentity))]
    [AddComponentMenu("TimeWarp/Animation/Time Animator")]
    public class TimeAnimator : MonoBehaviour, ITimeWarpModule
    {
        [Header("Recording Strategy")]
        [Tooltip("StateMachine is highly optimized but requires Blend Trees. BoneSnapshot consumes more RAM but perfectly rewinds any Animator by overriding bone transforms in LateUpdate.")]
        public AnimatorRecordMode RecordMode = AnimatorRecordMode.StateMachine;

        // ==========================================
        // OPTIMIZATION: DECOUPLED FRAMERATE
        // ==========================================
        [Space(10)]
        [Tooltip("Custom framerate for recording animations. Lowering this (e.g., to 15) saves massive amounts of RAM, especially in BoneSnapshot mode, while remaining perfectly smooth!")]
        [Range(5, 60)]
        public int AnimationFramerate = 15;

        [Space(10)]
        [Tooltip("If true, the engine will safely disable Root Motion during rewinds to prevent the character from sliding, and restore it seamlessly afterward.")]
        public bool HandleRootMotion = true;

        private TimeWarpIdentity _identity;
        private Animator[] _animators;
        private Animator _mainAnimator;

        // --- SHARED DATA (Parameters) ---
        private int[] _floatHashes;   private float[] _floatHistory;
        private int[] _intHashes;     private int[] _intHistory;
        private int[] _boolHashes;    private bool[] _boolHistory;

        // --- STATE MACHINE DATA ---
        private AnimSnapshot[] _stateHistory;
        private int _layerCount;

        // --- BONE SNAPSHOT DATA ---
        private Transform[] _bones;
        private string[] _boneNames; 
        private Vector3[] _bonePosHistory;
        private Quaternion[] _boneRotHistory;

        // --- INTERNAL DECOUPLED BUFFER STATE ---
        private int _animBufferCount;
        private int _animHead = 0;
        private float _recordTimer = 0f;
        
        private bool _isInitialized = false;
        private bool _wasApplyingRootMotion = false;
        private bool _rootMotionDisabledByWarp = false;
        private bool _isRecovering = false; 

        private bool _wasRewindingLocal = false;
        private float _lastAnimOffset = 0f;

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            _animators = GetComponentsInChildren<Animator>();
            
            if (_animators.Length > 0) 
            {
                _mainAnimator = _animators[0]; 
                _wasApplyingRootMotion = _mainAnimator.applyRootMotion;
                _layerCount = _mainAnimator.layerCount;
            }
        }

        private void InitializeModule()
        {
            if (_mainAnimator != null && TimeWarpManager.Instance != null)
            {
                // Calculate independent buffer size based on custom AnimationFramerate
                float maxTime = _identity.RecordTimeOverride > 0.01f ? _identity.RecordTimeOverride : TimeWarpManager.Instance.MaxRecordTime;
                _animBufferCount = Mathf.CeilToInt(AnimationFramerate * maxTime); 
                
                // 1. Analyze Main Animator Parameters
                AnimatorControllerParameter[] parameters = _mainAnimator.parameters;
                int fCount = 0, iCount = 0, bCount = 0;
                
                foreach (var p in parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Float) fCount++;
                    else if (p.type == AnimatorControllerParameterType.Int) iCount++;
                    else if (p.type == AnimatorControllerParameterType.Bool) bCount++;
                }

                // 2. Allocate Arrays using the smaller _animBufferCount
                _floatHashes = new int[fCount]; _floatHistory = new float[fCount * _animBufferCount];
                _intHashes = new int[iCount];   _intHistory = new int[iCount * _animBufferCount];
                _boolHashes = new int[bCount];  _boolHistory = new bool[bCount * _animBufferCount];

                // 3. Cache Hashes
                int f = 0, i = 0, b = 0;
                foreach (var p in parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Float) _floatHashes[f++] = p.nameHash;
                    else if (p.type == AnimatorControllerParameterType.Int) _intHashes[i++] = p.nameHash;
                    else if (p.type == AnimatorControllerParameterType.Bool) _boolHashes[b++] = p.nameHash;
                }

                // 4. Allocate Strategy Arrays
                if (RecordMode == AnimatorRecordMode.StateMachine)
                {
                    _stateHistory = new AnimSnapshot[_layerCount * _animBufferCount];
                }
                else if (RecordMode == AnimatorRecordMode.BoneSnapshot)
                {
                    var validBones = new List<Transform>();
                    foreach(var t in GetComponentsInChildren<Transform>(true))
                    {
                        if (t == transform || t.parent == null) continue;
                        validBones.Add(t);
                    }
                    
                    _bones = validBones.ToArray();
                    int boneCount = _bones.Length;
                    
                    _bonePosHistory = new Vector3[boneCount * _animBufferCount];
                    _boneRotHistory = new Quaternion[boneCount * _animBufferCount];

                    _boneNames = new string[boneCount];
                    for (int k = 0; k < boneCount; k++) _boneNames[k] = _bones[k].name;
                }
            }
            
            _isInitialized = true;
        }

        // Helper to get historical indices properly wrapped around the circular buffer
        private int GetAnimIndex(int offset)
        {
            int index = _animHead - 1 - offset;
            while (index < 0) index += _animBufferCount;
            while (index >= _animBufferCount) index -= _animBufferCount;
            return index;
        }

        public void OnWarpFixedUpdate()
        {
            if (_mainAnimator == null || !_mainAnimator.isActiveAndEnabled || !_identity.IsRegistered || (_identity.Channels & TimeChannels.Animation) == 0) return;
            if (!_isInitialized) InitializeModule();

            // Record data decoupled from the Physics framerate
            if (_identity.CanRecordHistory)
            {
                // Accumulate physical time
                _recordTimer += Time.fixedDeltaTime;
                float interval = 1f / AnimationFramerate;

                // Time to record an animation frame!
                if (_recordTimer >= interval)
                {
                    _recordTimer -= interval; 
                    
                    if (RecordMode == AnimatorRecordMode.StateMachine)
                    {
                        for (int i = 0; i < _layerCount; i++)
                        {
                            var info = _mainAnimator.GetCurrentAnimatorStateInfo(i);
                            _stateHistory[i * _animBufferCount + _animHead] = new AnimSnapshot { StateHash = info.fullPathHash, NormalizedTime = info.normalizedTime };
                        }
                    }
                    else if (RecordMode == AnimatorRecordMode.BoneSnapshot)
                    {
                        for (int k = 0; k < _bones.Length; k++)
                        {
                            int offset = k * _animBufferCount + _animHead;
                            _bonePosHistory[offset] = _bones[k].localPosition;
                            _boneRotHistory[offset] = _bones[k].localRotation;
                        }
                    }

                    for (int f = 0; f < _floatHashes.Length; f++) _floatHistory[f * _animBufferCount + _animHead] = _mainAnimator.GetFloat(_floatHashes[f]);
                    for (int i = 0; i < _intHashes.Length; i++) _intHistory[i * _animBufferCount + _animHead] = _mainAnimator.GetInteger(_intHashes[i]);
                    for (int b = 0; b < _boolHashes.Length; b++) _boolHistory[b * _animBufferCount + _animHead] = _mainAnimator.GetBool(_boolHashes[b]);

                    // Advance our independent head
                    _animHead++;
                    if (_animHead >= _animBufferCount) _animHead = 0;
                }
            }
        }

        public void OnWarpUpdate()
        {
            if (_mainAnimator == null || !_mainAnimator.isActiveAndEnabled || !_identity.IsRegistered || (_identity.Channels & TimeChannels.Animation) == 0) return;
            if (!_isInitialized) InitializeModule();

            bool isRewinding = _identity.IsRewinding || _identity.FractionalOffset > 0.001f;

            if (!isRewinding)
            {
                // Commit rewind if we just finished
                if (_wasRewindingLocal)
                {
                    _wasRewindingLocal = false;
                    int framesRewound = Mathf.FloorToInt(_lastAnimOffset);
                    _animHead = GetAnimIndex(framesRewound);
                    _animHead++; 
                    if (_animHead >= _animBufferCount) _animHead = 0;
                    _lastAnimOffset = 0f;
                    _recordTimer = 0f;
                }

                float speed = Mathf.Max(0f, _identity.EffectiveTimeScale);
                foreach (var anim in _animators) if(anim != null) anim.speed = speed;
                
                if (HandleRootMotion && _rootMotionDisabledByWarp && !_isRecovering) 
                    StartCoroutine(SafeRootMotionRecovery());
            }
            else
            {
                _wasRewindingLocal = true;
                foreach (var anim in _animators) if(anim != null) anim.speed = 0f;
                
                if (HandleRootMotion && !_rootMotionDisabledByWarp) 
                { 
                    _wasApplyingRootMotion = _mainAnimator.applyRootMotion; 
                    foreach (var anim in _animators) if(anim != null) anim.applyRootMotion = false; 
                    _rootMotionDisabledByWarp = true; 
                }

                // Map Physics offset to Animation offset
                float secondsRewound = _identity.FractionalOffset / TimeWarpManager.Instance.CaptureFramerate;
                _lastAnimOffset = secondsRewound * AnimationFramerate;
                
                int baseOffset = Mathf.FloorToInt(_lastAnimOffset);
                float t = _lastAnimOffset - baseOffset;
                
                int idxA = GetAnimIndex(baseOffset);
                int idxB = GetAnimIndex(baseOffset + 1);

                if (RecordMode == AnimatorRecordMode.StateMachine)
                {
                    for (int layer = 0; layer < _layerCount; layer++)
                    {
                        int offset = layer * _animBufferCount;
                        var snapA = _stateHistory[offset + idxA];
                        var snapB = _stateHistory[offset + idxB];

                        if (snapA.StateHash == 0 || snapB.StateHash == 0) continue;

                        var dominantSnap = (t < 0.5f) ? snapA : snapB;
                        float lerpedTime = (snapA.StateHash == snapB.StateHash) 
                            ? Mathf.Lerp(snapA.NormalizedTime, snapB.NormalizedTime, t) 
                            : dominantSnap.NormalizedTime;

                        _mainAnimator.Play(dominantSnap.StateHash, layer, lerpedTime);
                    }
                }

                for (int fIdx = 0; fIdx < _floatHashes.Length; fIdx++) _mainAnimator.SetFloat(_floatHashes[fIdx], Mathf.Lerp(_floatHistory[fIdx * _animBufferCount + idxA], _floatHistory[fIdx * _animBufferCount + idxB], t));
                for (int iIdx = 0; iIdx < _intHashes.Length; iIdx++) _mainAnimator.SetInteger(_intHashes[iIdx], _intHistory[iIdx * _animBufferCount + idxA]);
                for (int bIdx = 0; bIdx < _boolHashes.Length; bIdx++) _mainAnimator.SetBool(_boolHashes[bIdx], _boolHistory[bIdx * _animBufferCount + idxA]);

                if (RecordMode == AnimatorRecordMode.StateMachine) _mainAnimator.Update(0f); 
            }
        }

        private void LateUpdate()
        {
            if (RecordMode != AnimatorRecordMode.BoneSnapshot || _mainAnimator == null || !_mainAnimator.isActiveAndEnabled || !_identity.IsRegistered || (_identity.Channels & TimeChannels.Animation) == 0) return;
            
            bool isRewinding = _identity.IsRewinding || _identity.FractionalOffset > 0.001f;
            
            if (isRewinding)
            {
                int baseOffset = Mathf.FloorToInt(_lastAnimOffset);
                float t = _lastAnimOffset - baseOffset;
                
                int idxA = GetAnimIndex(baseOffset);
                int idxB = GetAnimIndex(baseOffset + 1);

                for (int k = 0; k < _bones.Length; k++)
                {
                    int offsetA = k * _animBufferCount + idxA;
                    int offsetB = k * _animBufferCount + idxB;
                    
                    _bones[k].localPosition = Vector3.Lerp(_bonePosHistory[offsetA], _bonePosHistory[offsetB], t);
                    _bones[k].localRotation = Quaternion.Slerp(_boneRotHistory[offsetA], _boneRotHistory[offsetB], t);
                }
            }
        }

        private IEnumerator SafeRootMotionRecovery()
        {
            _isRecovering = true;
            _mainAnimator.Update(0f);
            yield return new WaitForEndOfFrame();
            
            if (HandleRootMotion) 
            {
                foreach (var anim in _animators) if(anim != null) anim.applyRootMotion = _wasApplyingRootMotion;
            }
            
            _rootMotionDisabledByWarp = false;
            _isRecovering = false;
        }

        /// <summary>
        /// Extracts recorded animation data into a clean package for the Echo Ghost.
        /// Performs real-time up-sampling (Lerp) to match the physics high-framerate buffer perfectly.
        /// </summary>
        public GhostAnimPackage ExtractGhostData(int currentIndex, int framesRewound)
        {
            if (!_isInitialized) InitializeModule();

            var pkg = new GhostAnimPackage();

            int fLen = _floatHashes != null ? _floatHashes.Length : 0;
            int iLen = _intHashes != null ? _intHashes.Length : 0;
            int bLen = _boolHashes != null ? _boolHashes.Length : 0;

            pkg.FloatHashes = _floatHashes; 
            pkg.IntHashes = _intHashes; 
            pkg.BoolHashes = _boolHashes;

            // Allocate ghost arrays matching the Physics framerate size
            if (fLen > 0) pkg.FloatHist = new float[framesRewound * fLen];
            if (iLen > 0) pkg.IntHist = new int[framesRewound * iLen];
            if (bLen > 0) pkg.BoolHist = new bool[framesRewound * bLen];

            // Determine the absolute oldest animation frame we rewound to
            int framesRewoundAnim = Mathf.FloorToInt(_lastAnimOffset);
            int ghostStartAnimIndex = GetAnimIndex(framesRewoundAnim); 

            float captureFramerate = TimeWarpManager.Instance.CaptureFramerate;

            // ==========================================
            // UP-SAMPLING ENGINE
            // Generates perfectly smooth 50 FPS data out of a 15 FPS buffer
            // ==========================================
            for (int i = 0; i < framesRewound; i++)
            {
                // Calculate how many seconds into the playback we are
                float elapsedSec = (float)i / captureFramerate;
                
                // Map seconds to our animation framerate
                float animFramesForward = elapsedSec * AnimationFramerate;
                int intFrames = Mathf.FloorToInt(animFramesForward);
                float lerpT = animFramesForward - intFrames;

                int idxA = (ghostStartAnimIndex + intFrames) % _animBufferCount;
                int idxB = (ghostStartAnimIndex + intFrames + 1) % _animBufferCount;
                
                if (fLen > 0) 
                    for (int f = 0; f < fLen; f++) 
                        pkg.FloatHist[i * fLen + f] = Mathf.Lerp(_floatHistory[idxA * fLen + f], _floatHistory[idxB * fLen + f], lerpT);
                
                if (iLen > 0) 
                    for (int j = 0; j < iLen; j++) 
                        pkg.IntHist[i * iLen + j] = _intHistory[idxA * iLen + j];
                
                if (bLen > 0) 
                    for (int b = 0; b < bLen; b++) 
                        pkg.BoolHist[i * bLen + b] = _boolHistory[idxA * bLen + b];
            }

            if (RecordMode == AnimatorRecordMode.StateMachine && _stateHistory != null)
            {
                pkg.LayerCount = _layerCount;
                pkg.AnimPath = new AnimSnapshot[framesRewound * _layerCount];
                
                for (int i = 0; i < framesRewound; i++)
                {
                    float elapsedSec = (float)i / captureFramerate;
                    float animFramesForward = elapsedSec * AnimationFramerate;
                    int intFrames = Mathf.FloorToInt(animFramesForward);
                    float lerpT = animFramesForward - intFrames;

                    int idxA = (ghostStartAnimIndex + intFrames) % _animBufferCount;
                    int idxB = (ghostStartAnimIndex + intFrames + 1) % _animBufferCount;

                    for (int layer = 0; layer < _layerCount; layer++)
                    {
                        AnimSnapshot snapA = _stateHistory[layer * _animBufferCount + idxA];
                        AnimSnapshot snapB = _stateHistory[layer * _animBufferCount + idxB];

                        float lerpedTime = (snapA.StateHash == snapB.StateHash) 
                            ? Mathf.Lerp(snapA.NormalizedTime, snapB.NormalizedTime, lerpT) 
                            : snapA.NormalizedTime;

                        pkg.AnimPath[i * _layerCount + layer] = new AnimSnapshot { StateHash = snapA.StateHash, NormalizedTime = lerpedTime };
                    }
                }
            }
            else if (RecordMode == AnimatorRecordMode.BoneSnapshot && _bones != null && _bonePosHistory != null)
            {
                pkg.BoneCount = _bones.Length;
                pkg.BoneNames = _boneNames; 
                
                pkg.BonePos = new Vector3[framesRewound * pkg.BoneCount];
                pkg.BoneRot = new Quaternion[framesRewound * pkg.BoneCount];
                
                for (int i = 0; i < framesRewound; i++)
                {
                    float elapsedSec = (float)i / captureFramerate;
                    float animFramesForward = elapsedSec * AnimationFramerate;
                    int intFrames = Mathf.FloorToInt(animFramesForward);
                    float lerpT = animFramesForward - intFrames;

                    int idxA = (ghostStartAnimIndex + intFrames) % _animBufferCount;
                    int idxB = (ghostStartAnimIndex + intFrames + 1) % _animBufferCount;

                    for (int k = 0; k < pkg.BoneCount; k++)
                    {
                        int ghostOffset = i * pkg.BoneCount + k;
                        
                        int origA = k * _animBufferCount + idxA;
                        int origB = k * _animBufferCount + idxB;
                        
                        pkg.BonePos[ghostOffset] = Vector3.Lerp(_bonePosHistory[origA], _bonePosHistory[origB], lerpT);
                        pkg.BoneRot[ghostOffset] = Quaternion.Slerp(_boneRotHistory[origA], _boneRotHistory[origB], lerpT);
                    }
                }
            }

            return pkg;
        }
    }
}