using System;
using System.Collections.Generic;
using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace TimeWarp.Modules.Visuals
{
    [Serializable]
    public class ShaderPropertyConfig
    {
        public string PropertyName;
        public bool ReadFromMaterial = true;
        public float ManualBaseSpeed;
        public float RewindSpeed = -1f;
        public bool UseTimeAccumulation = true;

        [HideInInspector] public int PropertyID;
        [HideInInspector] public float ActualBaseSpeed;
        [HideInInspector] public float CurrentSpeed;
        [HideInInspector] public float AccumulatedValue; 
    }

    [RequireComponent(typeof(TimeWarpIdentity))]
    [RequireComponent(typeof(Renderer))]
    [DefaultExecutionOrder(10)]
    public class TimeShader : MonoBehaviour, ITimeWarpModule
    {
        public float SmoothingSpeed = 12f;
        public List<ShaderPropertyConfig> Properties = new List<ShaderPropertyConfig>();

        private TimeWarpIdentity _identity;
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;
        private float _previousOffset = 0f;
        
        // NEW: Memory of the previous frame to detect the moment the direction of time changes
        private bool _wasRewindingLastFrame = false;

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            foreach (var prop in Properties)
            {
                prop.PropertyID = Shader.PropertyToID(prop.PropertyName);

                if (prop.UseTimeAccumulation)
                {
                    if (_renderer.sharedMaterial.HasProperty(prop.PropertyID))
                        prop.AccumulatedValue = _renderer.sharedMaterial.GetFloat(prop.PropertyID);
                    
                    prop.ActualBaseSpeed = prop.ManualBaseSpeed; 
                }
                else
                {
                    if (prop.ReadFromMaterial && _renderer.sharedMaterial.HasProperty(prop.PropertyID))
                        prop.ActualBaseSpeed = _renderer.sharedMaterial.GetFloat(prop.PropertyID);
                    else
                        prop.ActualBaseSpeed = prop.ManualBaseSpeed;
                }
                
                prop.CurrentSpeed = prop.ActualBaseSpeed;
            }
        }

        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate()
        {
            if (!_identity.IsRegistered || TimeWarpManager.Instance == null) return;
            
            _renderer.GetPropertyBlock(_propBlock);
            
            float scale = _identity.EffectiveTimeScale;
            float currentOffset = _identity.FractionalOffset;
            bool isRewinding = _identity.IsRewinding || currentOffset > 0.005f;
            float framerate = TimeWarpManager.Instance.CaptureFramerate;

            // CRITICAL FIX: "Hard Clutch" - kills jumping and inertia!
            if (isRewinding && !_wasRewindingLastFrame)
            {
                _previousOffset = currentOffset; // Ignore the jump from the first frame
                foreach (var prop in Properties) prop.CurrentSpeed = prop.RewindSpeed; // Hard reset to negative speed
            }
            else if (!isRewinding && _wasRewindingLastFrame)
            {
                foreach (var prop in Properties) prop.CurrentSpeed = prop.ActualBaseSpeed * Mathf.Max(0, scale); // Hard reset to positive speed
            }

            foreach (var prop in Properties)
            {
                float target = isRewinding ? prop.RewindSpeed : (prop.ActualBaseSpeed * Mathf.Max(0, scale));
                
                // Smooth deceleration (Smoothing) will now only work if we DID NOT suddenly change the direction of time
                prop.CurrentSpeed = Mathf.MoveTowards(prop.CurrentSpeed, target, Time.unscaledDeltaTime * SmoothingSpeed);

                float deltaVirtualTime;
                if (!isRewinding) 
                {
                    deltaVirtualTime = prop.CurrentSpeed * Time.unscaledDeltaTime;
                }
                else
                {
                    float dt = (currentOffset - _previousOffset) / framerate;
                    if (dt <= 0f) dt = Time.unscaledDeltaTime; 
                    
                    deltaVirtualTime = prop.CurrentSpeed * dt;
                }

                if (prop.UseTimeAccumulation)
                {
                    prop.AccumulatedValue += deltaVirtualTime;
                    _propBlock.SetFloat(prop.PropertyID, prop.AccumulatedValue);
                }
                else 
                {
                    _propBlock.SetFloat(prop.PropertyID, prop.CurrentSpeed);
                }
            }
            
            _renderer.SetPropertyBlock(_propBlock);
            
            // Save state for the next frame
            _previousOffset = isRewinding ? currentOffset : 0f;
            _wasRewindingLastFrame = isRewinding; 
        }
    }
}