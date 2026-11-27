using UnityEngine;
using Unity.Collections; 
using TimeWarp.Core;
using TimeWarp.Core.Managers;
using TimeWarp.Core.Data;

namespace TimeWarp.Modules.Particles
{
    [RequireComponent(typeof(TimeWarpIdentity))]
    [RequireComponent(typeof(ParticleSystem))]
    [AddComponentMenu("TimeWarp/Particles/Time Particles (Shuriken)")]
    public class TimeParticles : MonoBehaviour, ITimeWarpModule
    {
        private TimeWarpIdentity _identity;
        private ParticleSystem _mainSystem;
        private ParticleSystemRenderer _renderer;
        
        private ParticleSystem.MainModule _mainModule;
        private ParticleSystem.CollisionModule _collisionModule;

        private float _realElapsedTime = 0f; 
        private float _previousOffset = 0f;
        private bool _wasPaused = false;
        
        private ParticleRewindMode _mode;
        private bool _originalCollisionState = false;
        private float _lastSimulatedTime = -1f;
        private int _lastRecordedHead = -1;

        private const int KEYFRAME_INTERVAL = 4; 
        private int _frameCount = 0;
        private int _allocatedPeakParticles = 0; 

        private Transform _mainCameraTransform;
        private const float CULLING_SQR_DISTANCE = 2500f;   
        private const float ULTRA_LOW_SQR_DISTANCE = 1225f; 
        private bool _wasFarLastFrame = false; 

        private GameObject _reverseInstance;
        private ParticleSystem _reverseSystem;

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            _mainSystem = GetComponent<ParticleSystem>();
            _renderer = GetComponent<ParticleSystemRenderer>();
            
            _mainModule = _mainSystem.main;
            _collisionModule = _mainSystem.collision;
            
            _originalCollisionState = _collisionModule.enabled;
            
            if (Camera.main != null) _mainCameraTransform = Camera.main.transform;

            bool wasPlaying = _mainSystem.isPlaying;
            if (wasPlaying) _mainSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            SetSeedRecursive(_mainSystem, (uint)Random.Range(1, int.MaxValue));
            
            if (wasPlaying) _mainSystem.Play(true);
        }

        private void Start()
        {
            if (_identity.ParticleSettings == null) return;
            _mode = _identity.ParticleSettings.RewindMode;

            if (_mode == ParticleRewindMode.DynamicRamCache && _identity.Buffer != null)
            {
                _allocatedPeakParticles = Mathf.Max(64, _mainSystem.particleCount * 3);
                _allocatedPeakParticles = Mathf.Min(_allocatedPeakParticles, 512);

                bool hasBudget = TimeWarpManager.Instance.RequestParticleRamBudget(_allocatedPeakParticles, _identity.Buffer.Count);

                if (hasBudget)
                {
                    _identity.Buffer.AllocateParticleBuffer(_mainModule.maxParticles, _allocatedPeakParticles);
                }
                else
                {
                    Debug.LogWarning($"[TimeWarp] RAM Budget Exceeded. Fallback to Procedural for {_mainSystem.gameObject.name}");
                    _mode = ParticleRewindMode.ProceduralResimulation;
                }
            }
            else if (_mode == ParticleRewindMode.ReversePlayback && _identity.ParticleSettings.ReversePrefab != null)
            {
                _reverseInstance = Instantiate(_identity.ParticleSettings.ReversePrefab, transform.position, transform.rotation, transform);
                _reverseSystem = _reverseInstance.GetComponent<ParticleSystem>();
                _reverseInstance.SetActive(false);
            }
        }

        private void SetSeedRecursive(ParticleSystem ps, uint seed)
        {
            ps.useAutoRandomSeed = false; 
            ps.randomSeed = seed;
            for (int i = 0; i < ps.transform.childCount; i++)
            {
                var childPs = ps.transform.GetChild(i).GetComponent<ParticleSystem>();
                if (childPs != null) SetSeedRecursive(childPs, seed + (uint)i + 1);
            }
        }

        public void OnWarpFixedUpdate() 
        {
            if (!_identity.IsRegistered || TimeWarpManager.Instance == null) return;

            if (_mode == ParticleRewindMode.DynamicRamCache && _identity.CanRecordHistory)
            {
                if (_identity.Buffer.ParticleHistory == null) return;

                int head = _identity.Buffer.GetIndex(0);
                if (head != _lastRecordedHead)
                {
                    _lastRecordedHead = head;
                    _frameCount++;
                    
                    ParticleFramePackage pkg = _identity.Buffer.ParticleHistory[head];
                    
                    bool isKeyframe = (_frameCount % KEYFRAME_INTERVAL == 0);
                    pkg.HasPlaybackState = isKeyframe;
                    
                    if (isKeyframe) 
                    {
                        pkg.PlaybackState = _mainSystem.GetPlaybackState();
                    }
                    
                    pkg.AliveParticlesCount = _mainSystem.GetParticles(pkg.Particles);
                    pkg.SimulatedTime = _realElapsedTime;
                    
                    _identity.Buffer.ParticleHistory[head] = pkg; 
                }
            }
        }

        public void OnWarpUpdate()
        {
            if (!_identity.IsRegistered || TimeWarpManager.Instance == null || (_identity.Channels & TimeChannels.Particles) == 0) return;

            bool isActuallyRewinding = _identity.FractionalOffset > 0.005f || _identity.IsRewinding;

            if (!isActuallyRewinding)
            {
                float scale = _identity.EffectiveTimeScale;

                if (Mathf.Abs(scale - 1f) < 0.01f)
                {
                    if (_wasPaused) 
                    { 
                        _mainSystem.Play(true); 
                        _wasPaused = false;
                        RestoreForwardState();
                    }
                    if (_mainSystem.isPlaying) _realElapsedTime += Time.unscaledDeltaTime;
                }
                else
                {
                    if (!_wasPaused)
                    {
                        _mainSystem.Pause(true);
                        _wasPaused = true;
                        RestoreForwardState();
                    }
                    
                    float dt = Time.unscaledDeltaTime * scale;
                    if (dt > 0f)
                    {
                        _mainSystem.Simulate(dt, true, false, false);
                        _realElapsedTime += dt;
                    }
                }
                
                _previousOffset = 0f;
            }
            else
            {
                if (!_wasPaused) 
                { 
                    _mainSystem.Pause(true); 
                    _wasPaused = true; 

                    if (_identity.ParticleSettings.DisableCollisionsOnRewind && _originalCollisionState)
                    {
                        _collisionModule.enabled = false;
                    }

                    if (_mode == ParticleRewindMode.ReversePlayback && _reverseInstance != null)
                    {
                        if (_renderer != null) _renderer.enabled = false;
                        _reverseInstance.SetActive(true);
                        _reverseSystem.Play(true);
                    }
                }

                float currentOffset = _identity.FractionalOffset;
                float framerate = TimeWarpManager.Instance.CaptureFramerate;
                float dt = (_previousOffset - currentOffset) / framerate; 
                _previousOffset = currentOffset;

                if (Mathf.Abs(dt) < 0.0001f) return;
                _realElapsedTime += dt;

                if (_realElapsedTime <= 0f) 
                { 
                    _realElapsedTime = 0f; 
                    _mainSystem.Clear(true); 
                    return; 
                }

                if (_identity.ParticleSettings != null && _identity.ParticleSettings.OptimizeOffScreen && _mainCameraTransform != null)
                {
                    float sqrDist = (_mainCameraTransform.position - transform.position).sqrMagnitude;
                    if (sqrDist > CULLING_SQR_DISTANCE) 
                    {
                        _wasFarLastFrame = true; 
                        return;
                    }
                }

                ExecuteRewindStrategy(dt);
            }
        }

        private void RestoreForwardState()
        {
            if (_identity.ParticleSettings.DisableCollisionsOnRewind && _originalCollisionState)
            {
                _collisionModule.enabled = true;
            }

            if (_mode == ParticleRewindMode.ReversePlayback && _reverseInstance != null)
            {
                _reverseInstance.SetActive(false);
                if (_renderer != null) _renderer.enabled = true;
            }
        }

        private void ExecuteRewindStrategy(float dt)
        {
            switch (_mode)
            {
                case ParticleRewindMode.ProceduralResimulation:
                    if (dt > 0f) 
                    {
                        _mainSystem.Simulate(dt, true, false, true);
                        _lastSimulatedTime = _realElapsedTime;
                    }
                    else 
                    {
                        float quality = Mathf.Clamp(TimeWarpManager.Instance.GlobalRewindVisualQuality, 10f, 120f);

                        if (_mainCameraTransform != null)
                        {
                            float sqrDist = (_mainCameraTransform.position - transform.position).sqrMagnitude;
                            bool isFar = sqrDist > ULTRA_LOW_SQR_DISTANCE; 

                            if (isFar)
                            {
                                _wasFarLastFrame = true;
                                quality = 2f; 
                            }
                            else if (_wasFarLastFrame)
                            {
                                _mainSystem.Simulate(_realElapsedTime, true, true, true);
                                _lastSimulatedTime = _realElapsedTime;
                                _wasFarLastFrame = false;
                                return;
                            }
                        }

                        float updateThreshold = 1f / quality;

                        if (Mathf.Abs(_lastSimulatedTime - _realElapsedTime) >= updateThreshold || _lastSimulatedTime < 0f)
                        {
                            _mainSystem.Simulate(_realElapsedTime, true, true, true);
                            _lastSimulatedTime = _realElapsedTime;
                        }
                    }
                    break;

                case ParticleRewindMode.DynamicRamCache:
                    if (_identity.Buffer.ParticleHistory != null && _identity.Buffer.ParticleHistory.Length > 0)
                    {
                        _identity.Buffer.GetFractionalIndex(_identity.FractionalOffset, out int idxA, out int idxB, out float t);
                        ParticleFramePackage pkg = _identity.Buffer.ParticleHistory[idxA];
                        
                        if (pkg.HasPlaybackState)
                        {
                            _mainSystem.SetPlaybackState(pkg.PlaybackState);
                        }
                        
                        _mainSystem.SetParticles(pkg.Particles, pkg.AliveParticlesCount);
                    }
                    break;

                case ParticleRewindMode.ReversePlayback:
                    break;
            }
        }

        private void OnDestroy()
        {
            if (_mode == ParticleRewindMode.DynamicRamCache && TimeWarpManager.Instance != null && _identity != null && _identity.Buffer != null)
            {
                TimeWarpManager.Instance.ReleaseParticleRamBudget(_allocatedPeakParticles, _identity.Buffer.Count);
            }
        }
    }
}