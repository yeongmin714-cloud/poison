using System;
using System.Collections.Generic;
using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Modules.Animation;
using TimeWarp.Modules.Zones;

namespace TimeWarp.Core.Data
{
    [Serializable]
    public class ShaderPropertyRule
    {
        public string PropertyName = "_OwnTime";
        public bool ReadFromMaterial = true;
        public float ManualBaseSpeed = 1.0f;
        public float RewindSpeed = -1.0f;
        public bool UseTimeAccumulation = true;
    }

    public enum CharacterAdapterType
    {
        None = 0, 
        UnityCharacterController = 1, 
        CustomKCC = 2 
    }

    /// <summary>
    /// Defines an automated setup rule for injecting TimeWarp modules into GameObjects.
    /// Can be saved as a ScriptableObject Preset.
    /// </summary>
    [Serializable]
    public class TimeWarpRule
    {
        public string RuleName = "New Rule";
        public bool Enabled = true;

        [Header("Targeting")]
        [Tooltip("The physics layers this rule applies to.")]
        public LayerMask TargetLayer;
        
        [Tooltip("OPTIONAL tag filter, combined with Target Layer using OR.\n\n" +
                 "Lets you write rules like \"inject TimeBody into everything tagged Destructible\" " +
                 "without dragging objects into the list below or spending a physics layer.\n\n" +
                 "Leave empty for the previous layer-only behaviour.")]
        public List<string> TargetTags = new List<string>();

        [Tooltip("Specific GameObjects to force this rule on, regardless of their layer.")]
        public List<GameObject> ExplicitObjects = new List<GameObject>();

        [Header("Core Settings")]
        [Tooltip("Which Time Channels should be activated for matching objects?")]
        public TimeChannels ActiveChannels = TimeChannels.Transform | TimeChannels.Velocity | TimeChannels.Custom | TimeChannels.Particles;
        public float DefaultLocalTimeScale = 1.0f;
        
        [Tooltip("Overrides the global Max Record Time if greater than 0.")]
        public float CustomRecordTime = 0f;

        [Tooltip("OPTIMIZATION: If enabled, the Native RAM buffer is destroyed when the object is disabled. Use this for pooled objects (bullets, enemies) to prevent massive Memory Leaks.")]
        public bool FreeMemoryOnDisable = false;

        [Space(10)]
        [Header("--- Action Filters & Zones ---")]
        public TimeWarpActionFilters ActionFilters = new TimeWarpActionFilters();

        [Space(10)]
        [Header("--- Channel: Transform ---")]
        public TransformChannelSettings TransformSettings = new TransformChannelSettings();

        [Space(5)]
        [Header("--- Channel: Velocity ---")]
        public VelocityChannelSettings VelocitySettings = new VelocityChannelSettings();

        [Space(5)]
        [Header("--- Channel: Velocity 2D ---")]
        public Velocity2DChannelSettings Velocity2DSettings = new Velocity2DChannelSettings();

        [Space(5)]
        [Header("--- Channel: Custom (Bridge) ---")]
        public CustomChannelSettings CustomSettings = new CustomChannelSettings();

        [Space(5)]
        [Header("--- Channel: Particles & VFX ---")]
        public ParticleChannelSettings ParticleSettings = new ParticleChannelSettings();
        public VfxChannelSettings VfxSettings = new VfxChannelSettings();

        [Space(5)]
        [Header("--- Channel: Audio ---")]
        public AudioChannelSettings AudioSettings = new AudioChannelSettings();

        [Space(5)]
        [Header("--- Channel: Navigation (AI) ---")]
        public NavigationChannelSettings NavigationSettings = new NavigationChannelSettings();

        [Space(5)]
        [Header("--- Channel: Paradox (Echoes) ---")]
        public EchoChannelSettings EchoSettings = new EchoChannelSettings();

        [Space(10)]
        [Header("Injection Flags (Auto-Add Modules)")]
        public bool InjectTimeBody = true;
        
        [Header("Kinetic Accumulator (Physics)")]
        public bool InjectKineticAccumulator = false;
        [Tooltip("How should the stored physics force be released?")]
        public TimeWarp.Modules.Physics.Dynamics.KineticReleaseMode KineticReleaseMode = TimeWarp.Modules.Physics.Dynamics.KineticReleaseMode.CombinedImpulse;
        public float MaxForceCap = 500f;
        public float SequentialDelay = 0.1f;

        public bool InjectTimeShader = false;

        [Header("Particles Setup")]
        [Tooltip("Automatically attaches TimeParticles to objects with a Shuriken ParticleSystem.")]
        public bool InjectTimeParticles = true;
        
        [Tooltip("Automatically attaches TimeVFX to objects with a Visual Effect Graph.")]
        public bool InjectTimeVFX = true;
        
        [Header("Animation Setup")]
        public bool InjectTimeAnimator = true; 
        public AnimatorRecordMode AnimatorMode = AnimatorRecordMode.StateMachine; 
        
        [Tooltip("Custom framerate for recording animations. Lowering this (e.g. to 15) saves massive amounts of RAM!")]
        [Range(5, 60)] public int AnimationFramerate = 15;

        [Tooltip("Safely handles Root Motion during rewinds to prevent the character from sliding.")]
        public bool HandleRootMotion = true; 
        
        [Header("Other Modules")]
        public bool InjectTimeAudio = true; 
        public bool InjectTimeNavMeshAgent = true; 
        public bool InjectTimeParadoxModule = false; 
        
        [Tooltip("Automatically attaches TimeEventRecorder to record generic actions (shooting, dashing, etc.) via string hashes.")]
        public bool InjectTimeEventRecorder = false; 
        
        public bool InjectTimeCinemachine = true; 
        public bool InjectTimeHingeJoint = true; 
        
        [Header("Lifecycle Settings")]
        [Tooltip("Automatically attaches TimeWarpLifecycle to handle death and resurrection during rewinds.")]
        public bool InjectTimeLifecycle = false;
        public bool LifecycleKeepInMemory = true;
        public bool LifecycleDestroyBeforeBirth = true;
        
        [Header("Controller Setup")]
        public CharacterAdapterType ControllerAdapter = CharacterAdapterType.UnityCharacterController;
        
        public List<ShaderPropertyRule> ShaderProperties = new List<ShaderPropertyRule>();

        [Header("Component Manager (Kill Switch)")]
        [Tooltip("Automatically attaches a manager that disables foreign logic scripts during time anomalies.")]
        public bool InjectComponentManager = false;
        public bool DisableOnRewind = true;
        public bool DisableOnTimeStop = true;
        public bool AutoDetectForeignScripts = false;
        public List<string> SpecificComponentsToDisable = new List<string>();

        public bool Matches(GameObject go)
        {
            if (!Enabled) return false;

            bool layerMatch = (TargetLayer.value & (1 << go.layer)) != 0;
            bool tagMatch = TimeWarpTagUtility.HasAnyTag(go, TargetTags);
            bool explicitMatch = ExplicitObjects != null && ExplicitObjects.Contains(go);

            // OR, consistent with how ExplicitObjects already widens the layer match.
            return layerMatch || tagMatch || explicitMatch;
        }

        public void ApplyToIdentity(TimeWarpIdentity identity)
        {
            // PASSING THE RULE NAME TO THE OBJECT (For zones)
            identity.AppliedRuleName = this.RuleName;

            identity.Channels = ActiveChannels;
            identity.LocalTimeScale = DefaultLocalTimeScale;
            identity.RecordTimeOverride = CustomRecordTime;
            identity.FreeMemoryOnDisable = FreeMemoryOnDisable;

            // ==========================================
            // FULL DEEP COPY: Action Filters
            // Updated to the new zone lists (Culling for a specific Rule)
            // ==========================================
            identity.ActionFilters = new TimeWarpActionFilters
            {
                AllowRewind = ActionFilters.AllowRewind,
                AllowTimeStop = ActionFilters.AllowTimeStop,
                AllowSlowMo = ActionFilters.AllowSlowMo,
                OverrideSlowMoScale = ActionFilters.OverrideSlowMoScale,
                CustomSlowMoScale = ActionFilters.CustomSlowMoScale,
                OverrideRewindSpeed = ActionFilters.OverrideRewindSpeed,
                CustomRewindSpeedMultiplier = ActionFilters.CustomRewindSpeedMultiplier,
                
                RequiresZoneForRewind = ActionFilters.RequiresZoneForRewind,
                RewindZones = ActionFilters.RewindZones != null ? new List<TimeWarpPermissionZone>(ActionFilters.RewindZones) : new List<TimeWarpPermissionZone>(),
                
                RequiresZoneForSlowMo = ActionFilters.RequiresZoneForSlowMo,
                SlowMoZones = ActionFilters.SlowMoZones != null ? new List<TimeWarpPermissionZone>(ActionFilters.SlowMoZones) : new List<TimeWarpPermissionZone>(),
                
                RequiresZoneForTimeStop = ActionFilters.RequiresZoneForTimeStop,
                TimeStopZones = ActionFilters.TimeStopZones != null ? new List<TimeWarpPermissionZone>(ActionFilters.TimeStopZones) : new List<TimeWarpPermissionZone>(),
                
                RecordOnlyInZone = ActionFilters.RecordOnlyInZone,
                RecordingZones = ActionFilters.RecordingZones != null ? new List<TimeWarpPermissionZone>(ActionFilters.RecordingZones) : new List<TimeWarpPermissionZone>()
            };

            // ==========================================
            // FULL DEEP COPY: Core Channels
            // ==========================================
            identity.TransformSettings = new TransformChannelSettings
            {
                RecordPosition = TransformSettings.RecordPosition,
                RecordRotation = TransformSettings.RecordRotation,
                RecordScale = TransformSettings.RecordScale,
                Interpolation = TransformSettings.Interpolation
            };

            identity.VelocitySettings = new VelocityChannelSettings
            {
                RestoreRecordedVelocity = VelocitySettings.RestoreRecordedVelocity,
                MaxAllowedSpeed = VelocitySettings.MaxAllowedSpeed
            };

            identity.Velocity2DSettings = new Velocity2DChannelSettings
            {
                RestoreRecordedVelocity = Velocity2DSettings.RestoreRecordedVelocity,
                MaxAllowedSpeed = Velocity2DSettings.MaxAllowedSpeed
            };

            // ==========================================
            // FULL DEEP COPY: Custom Bridge Bindings
            // ==========================================
            identity.CustomSettings = new CustomChannelSettings
            {
                DisableComponentsOnAnomalies = CustomSettings.DisableComponentsOnAnomalies,
                Bindings = new List<CustomPropertyBinding>()
            };

            if (CustomSettings.Bindings != null)
            {
                foreach (var binding in CustomSettings.Bindings)
                {
                    identity.CustomSettings.Bindings.Add(new CustomPropertyBinding
                    {
                        ComponentName = binding.ComponentName,
                        PropertyName = binding.PropertyName,
                        OperationMode = binding.OperationMode,
                        MathMode = binding.MathMode,
                        TimeStopBehavior = binding.TimeStopBehavior,
                        RewindSmoothing = binding.RewindSmoothing
                    });
                }
            }

            // ==========================================
            // FULL DEEP COPY: Visuals & Audio
            // ==========================================
            identity.ParticleSettings = new ParticleChannelSettings
            {
                RewindMode = ParticleSettings.RewindMode,
                ReversePrefab = ParticleSettings.ReversePrefab,
                OptimizeOffScreen = ParticleSettings.OptimizeOffScreen,
                DisableCollisionsOnRewind = ParticleSettings.DisableCollisionsOnRewind
            };

            identity.VfxSettings = new VfxChannelSettings
            {
                RewindMode = VfxSettings.RewindMode,
                OptimizeOffScreen = VfxSettings.OptimizeOffScreen,
                TimeProperties = VfxSettings.TimeProperties != null ? new List<string>(VfxSettings.TimeProperties) : new List<string>()
            };

            identity.AudioSettings = new AudioChannelSettings
            {
                AffectPitch = AudioSettings.AffectPitch,
                MaxPitchLimit = AudioSettings.MaxPitchLimit,
                MuteOnRewind = AudioSettings.MuteOnRewind,
                ApplyLowPassFilter = AudioSettings.ApplyLowPassFilter
            };

            // ==========================================
            // FULL DEEP COPY: Navigation & Echoes
            // ==========================================
            identity.NavigationSettings = new NavigationChannelSettings
            {
                RestoreDestination = NavigationSettings.RestoreDestination
            };

            identity.EchoSettings = new EchoChannelSettings
            {
                EnableEchoes = EchoSettings.EnableEchoes,
                GhostPrefab = EchoSettings.GhostPrefab,
                HologramMaterial = EchoSettings.HologramMaterial,
                LifetimeAfterPlayback = EchoSettings.LifetimeAfterPlayback,
                PlayAutomatically = EchoSettings.PlayAutomatically,
                MaxConcurrentEchoes = EchoSettings.MaxConcurrentEchoes,
                EnableGhostPhysics = EchoSettings.EnableGhostPhysics,
                GhostPushForce = EchoSettings.GhostPushForce,
                GhostLayerName = EchoSettings.GhostLayerName,
                AutoDetectAnimationParameters = EchoSettings.AutoDetectAnimationParameters,
                CustomSpeedParameters = EchoSettings.CustomSpeedParameters != null ? (string[])EchoSettings.CustomSpeedParameters.Clone() : new string[0],
                MotionSpeedParameter = EchoSettings.MotionSpeedParameter,
                GhostSpeedMultiplier = EchoSettings.GhostSpeedMultiplier,
                AnimationDampTime = EchoSettings.AnimationDampTime
            };
        }
    }
}