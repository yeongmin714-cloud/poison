using UnityEngine;
using UnityEngine.VFX;
using System.Collections.Generic;
using TimeWarp.Core;
using TimeWarp.Core.Managers;
using TimeWarp.Core.Data;

namespace TimeWarp.Modules.Particles
{
    [RequireComponent(typeof(TimeWarpIdentity))]
    [RequireComponent(typeof(VisualEffect))]
    [AddComponentMenu("TimeWarp/Particles/Time VFX (Graph)")]
    public class TimeVFX : MonoBehaviour, ITimeWarpModule
    {
        private TimeWarpIdentity _identity;
        private VisualEffect _vfx;
        private Renderer _renderer;

        private float _virtualTime = 0f;
        private float _previousOffset = 0f;
        private bool _wasPaused = false;
        private float _originalPlayRate = 1f;
        
        private float _lastSimulatedTime = -1f;

        private VfxRewindMode _mode;
        private bool _eventRewindTriggered = false;

        private static readonly int PlayEventId = Shader.PropertyToID("OnPlay");
        private static readonly int StopEventId = Shader.PropertyToID("OnStop");
        private static readonly int RewindEventId = Shader.PropertyToID("OnRewind");
        
        private List<int> _validTimePropertyIds = new List<int>();

        // --- OPTIMIZATION (YOUR IDEA) ---
        private Transform _mainCameraTransform;
        private const float CULLING_SQR_DISTANCE = 2500f;   // ~50m
        private const float ULTRA_LOW_SQR_DISTANCE = 1225f; // ~35m
        private bool _wasFarLastFrame = false; 

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            _vfx = GetComponent<VisualEffect>();
            _renderer = GetComponent<Renderer>();
            
            if (Camera.main != null) _mainCameraTransform = Camera.main.transform;

            _originalPlayRate = _vfx.playRate;
            _vfx.resetSeedOnPlay = false;
            
            _vfx.startSeed = (uint)Random.Range(1, int.MaxValue);
            _vfx.Reinit();
        }

        private void Start()
        {
            if (_identity.VfxSettings != null)
            {
                _mode = _identity.VfxSettings.RewindMode;
                
                if (_identity.VfxSettings.TimeProperties != null)
                {
                    foreach (string propName in _identity.VfxSettings.TimeProperties)
                    {
                        if (string.IsNullOrEmpty(propName)) continue;
                        
                        int propId = Shader.PropertyToID(propName);
                        if (_vfx.HasFloat(propId))
                        {
                            _validTimePropertyIds.Add(propId);
                        }
                    }
                }
            }
        }

        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate()
        {
            if (!_identity.IsRegistered || TimeWarpManager.Instance == null || (_identity.Channels & TimeChannels.Particles) == 0) return;

            float currentOffset = _identity.FractionalOffset;
            float framerate = TimeWarpManager.Instance.CaptureFramerate;
            bool isRewindingOffset = _identity.IsRewinding || currentOffset > 0.001f;

            if (!isRewindingOffset)
            {
                float scale = _identity.EffectiveTimeScale;
                
                if (scale <= 0.01f) 
                {
                    if (!_wasPaused) 
                    { 
                        _vfx.pause = true; 
                        _wasPaused = true; 
                    }
                }
                else
                {
                    if (_wasPaused) 
                    { 
                        _vfx.pause = false; 
                        
                        if (_mode == VfxRewindMode.EventCaching && _eventRewindTriggered)
                        {
                            _vfx.SendEvent(PlayEventId); 
                            _eventRewindTriggered = false;
                        }
                        else
                        {
                            _vfx.Play(); 
                        }
                        
                        _wasPaused = false; 
                    }

                    float targetSpeed = _originalPlayRate * scale;
                    if (Mathf.Abs(_vfx.playRate - targetSpeed) > 0.001f) _vfx.playRate = targetSpeed;
                    
                    _virtualTime += Time.deltaTime * scale;
                }
                _previousOffset = 0f;
            }
            else
            {
                if (!_wasPaused) 
                { 
                    _wasPaused = true; 
                    _vfx.playRate = _originalPlayRate; 
                    
                    if (_mode != VfxRewindMode.EventCaching)
                    {
                        _vfx.pause = true; 
                    }
                }

                float dt = (_previousOffset - currentOffset) / framerate;
                _previousOffset = currentOffset;

                if (Mathf.Abs(dt) < 0.0001f) return;

                _virtualTime += dt;
                
                if (_virtualTime <= 0f) 
                { 
                    _virtualTime = 0f; 
                    _vfx.Reinit(); 
                    _vfx.pause = true; 
                    SyncWarpTimeProperties();
                    return; 
                }

                // ADVANCED CULLING (OFF-SCREEN)
                if (_identity.VfxSettings != null && _identity.VfxSettings.OptimizeOffScreen)
                {
                    if (_mainCameraTransform != null)
                    {
                        float sqrDist = (_mainCameraTransform.position - transform.position).sqrMagnitude;
                        if (sqrDist > CULLING_SQR_DISTANCE) 
                        {
                            _wasFarLastFrame = true; 
                            return; // Do not calculate at all if it is very far
                        }
                    }
                    else if (_renderer != null && !_renderer.isVisible) return; 
                }

                ExecuteRewindStrategy(dt);
            }

            SyncWarpTimeProperties();
        }

        private void SyncWarpTimeProperties()
        {
            for (int i = 0; i < _validTimePropertyIds.Count; i++)
            {
                _vfx.SetFloat(_validTimePropertyIds[i], _virtualTime);
            }
        }

        private void ExecuteRewindStrategy(float dt)
        {
            switch (_mode)
            {
                case VfxRewindMode.ProceduralResimulation:
                    if (dt > 0f) 
                    {
                        _vfx.Simulate(dt);
                        _lastSimulatedTime = _virtualTime;
                    }
                    else
                    {
                        float quality = Mathf.Clamp(TimeWarpManager.Instance.GlobalRewindVisualQuality, 10f, 120f);
                        
                        // --- NEW: DISTANCE-BASED TIME SLICING ---
                        if (_mainCameraTransform != null)
                        {
                            float sqrDist = (_mainCameraTransform.position - transform.position).sqrMagnitude;
                            bool isFar = sqrDist > ULTRA_LOW_SQR_DISTANCE; 

                            if (isFar)
                            {
                                _wasFarLastFrame = true;
                                quality = 5f; // Drastic drop in GPU simulation frames for distant objects
                            }
                            else if (_wasFarLastFrame)
                            {
                                // Player suddenly approached - force a hard sync
                                _vfx.Reinit();
                                _vfx.Simulate(_virtualTime);
                                _lastSimulatedTime = _virtualTime;
                                _wasFarLastFrame = false;
                                return;
                            }
                        }

                        float updateThreshold = 1f / quality;

                        if (Mathf.Abs(_lastSimulatedTime - _virtualTime) >= updateThreshold || _lastSimulatedTime < 0f)
                        {
                            _vfx.Reinit();
                            
                            float idealStepSize = 1f / 30f; 
                            uint desiredSteps = (uint)Mathf.Max(1, Mathf.RoundToInt(_virtualTime / idealStepSize));
                            uint maxGpuSteps = (uint)TimeWarpManager.Instance.MaxVFXRewindSteps;
                            
                            if (desiredSteps > maxGpuSteps)
                            {
                                desiredSteps = maxGpuSteps;
                                idealStepSize = _virtualTime / desiredSteps; 
                            }
                            
                            _vfx.Simulate(idealStepSize, desiredSteps);
                            _lastSimulatedTime = _virtualTime;
                        }
                    }
                    break;

                case VfxRewindMode.EventCaching:
                    if (!_eventRewindTriggered)
                    {
                        _vfx.SendEvent(RewindEventId);
                        _vfx.pause = false; 
                        _eventRewindTriggered = true;
                    }
                    break;
            }
        }
    }
}