using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimeWarp.Core.Data
{
    // ==========================================
    // ENUMS FOR CHANNELS AND BRIDGE
    // ==========================================
    
    public enum RewindInterpolation 
    { 
        Snap, 
        Lerp 
    }
    
    public enum StasisBehavior 
    { 
        Ignore, 
        FreezeAtBase, 
        FreezeCurrent 
    }
    
    public enum BindingMathMode 
    { 
        None, 
        Multiply, 
        Divide, 
        Offset, 
        ResetToBase 
    }

    public enum CustomChannelOperation
    {
        MathOnly,      // Operates live, multiplies/divides, ignores history during rewind.
        RecordOnly,    // Ignores live math, but records frames and rewinds them smoothly.
        MathAndRecord  // HYBRID: Applies math forward, uses recorded history during rewind.
    }

    // ==========================================
    // 1. CHANNEL: TRANSFORM
    // ==========================================
    [Serializable]
    public class TransformChannelSettings
    {
        [Header("Transform Tracking")]
        [Tooltip("Should the object's position be recorded? (If disabled, the object will lock its position during rewind)")]
        public bool RecordPosition = true;
        
        [Tooltip("Should the object's rotation be recorded?")]
        public bool RecordRotation = true;
        
        [Tooltip("Scaling rarely rewinds in games. Disable this to save RAM!")]
        public bool RecordScale = false;

        [Header("Playback Smoothing")]
        [Tooltip("How should the object behave between recorded frames? Lerp is smooth, Snap teleports instantly.")]
        public RewindInterpolation Interpolation = RewindInterpolation.Lerp;
    }

    // ==========================================
    // 2. CHANNEL: VELOCITY (3D PHYSICS)
    // ==========================================
    [Serializable]
    public class VelocityChannelSettings
    {
        [Header("Physics Recovery")]
        [Tooltip("Should the object regain its past velocity vector after a time anomaly (e.g., rewind) ends?")]
        public bool RestoreRecordedVelocity = true;
        
        [Header("Safety Limits")]
        [Tooltip("Maximum allowed speed of the object in SlowMo (prevents physics glitches sending objects into orbit)")]
        public float MaxAllowedSpeed = 50f;
    }

    // ==========================================
    // 3. CHANNEL: VELOCITY 2D (2D PHYSICS)
    // ==========================================
    [Serializable]
    public class Velocity2DChannelSettings
    {
        [Header("Physics 2D Recovery")]
        [Tooltip("Should the object regain its past 2D velocity vector after a time anomaly (e.g., rewind) ends?")]
        public bool RestoreRecordedVelocity = true;
        
        [Header("Safety Limits")]
        [Tooltip("Maximum allowed 2D speed of the object in SlowMo (prevents physics glitches)")]
        public float MaxAllowedSpeed = 50f;
    }

    // ==========================================
    // 4. CHANNEL: CUSTOM (External Variables)
    // ==========================================
    [Serializable]
    public class CustomPropertyBinding
    {
        [Header("Target Component")]
        [Tooltip("The name of the script containing the variable (e.g., 'Light', 'PlayerHealth', 'AudioSource')")]
        public string ComponentName; 
        
        [Tooltip("The exact name of the variable or property (e.g., 'intensity', 'volume', 'currentHP')")]
        public string PropertyName;  
        
        [Header("Mode & Operation")]
        [Tooltip("Choose whether you want only mathematical operations (Math), history recording (Record), or both simultaneously.")]
        public CustomChannelOperation OperationMode = CustomChannelOperation.RecordOnly;
        
        [Tooltip("Works only for MathOnly and MathAndRecord modes. What to do with the variable when time slows down?")]
        public BindingMathMode MathMode = BindingMathMode.Multiply;
        
        [Tooltip("What to do with this variable when the player completely stops time (Time Stop)?")]
        public StasisBehavior TimeStopBehavior = StasisBehavior.FreezeCurrent;

        [Header("Rewind Settings")]
        [Tooltip("For RecordOnly / MathAndRecord modes. How to smooth the value during rewind? (Lerp for smooth transitions, Snap for hard jumps)")]
        public RewindInterpolation RewindSmoothing = RewindInterpolation.Lerp;
    }

    [Serializable]
    public class CustomChannelSettings
    {
        [Header("Global Custom Settings")]
        [Tooltip("Should components (scripts) handling bound variables be fully disabled automatically during rewind and time stop? (Recommended for optimization)")]
        public bool DisableComponentsOnAnomalies = true;
        
        [Space(10)]
        [Header("Variable Bindings")]
        public List<CustomPropertyBinding> Bindings = new List<CustomPropertyBinding>();
    }

    // ==========================================
    // 5. CHANNEL: PARTICLES (Shuriken) & VFX (Graph)
    // ==========================================
    [Serializable]
    public class ParticleChannelSettings
    {
        [Header("Rewind Strategy")]
        [Tooltip("Procedural: High CPU, No RAM. | DynamicRamCache: Low CPU, High RAM (Best for dynamic). | ReversePlayback: Plays reverse prefab.")]
        public ParticleRewindMode RewindMode = ParticleRewindMode.ProceduralResimulation;

        [Tooltip("Prefab to instantiate when using ReversePlayback mode.")]
        public GameObject ReversePrefab;

        [Header("Performance Optimization")]
        [Tooltip("CULLING: If enabled, stops calculating time rewind for particles not currently seen by any camera (Massive CPU/GPU gain).")]
        public bool OptimizeOffScreen = true;
        
        [Tooltip("If enabled, collisions are disabled during rewind to save CPU.")]
        public bool DisableCollisionsOnRewind = true;
    }

    [Serializable]
    public class VfxChannelSettings
    {
        [Header("Rewind Strategy")]
        [Tooltip("Procedural: Simulates backwards with time slicing. | EventCaching: Records triggers and reverses them (Very cheap).")]
        public VfxRewindMode RewindMode = VfxRewindMode.ProceduralResimulation;

        [Header("Performance Optimization")]
        [Tooltip("CULLING: If enabled, stops calculations for VFX not currently seen by any camera.")]
        public bool OptimizeOffScreen = true;

        [Header("Custom Time Properties")]
        [Tooltip("Names of Exposed Float properties in the VFX Graph that should receive the Virtual Time (e.g., 'WarpTime', 'SmokeAge').")]
        public List<string> TimeProperties = new List<string> { "WarpTime" };
    }

    // ==========================================
    // 6. CHANNEL: AUDIO
    // ==========================================
    [Serializable]
    public class AudioChannelSettings
    {
        [Header("Pitch & Volume Control")]
        [Tooltip("Should the audio slow down (lower pitch) proportionally to the time scale?")]
        public bool AffectPitch = true;

        [Range(0.1f, 3f)]
        [Tooltip("Maximum pitch limit to prevent annoying squeaks if time is drastically accelerated.")]
        public float MaxPitchLimit = 2.0f;

        [Tooltip("Should this audio be completely muted during time rewind? (Recommended)")]
        public bool MuteOnRewind = true;

        [Header("Anomaly Filters")]
        [Tooltip("Should a Low Pass filter (muffling) be applied when time slows down or rewinds? The system will automatically add and handle the AudioLowPassFilter component.")]
        public bool ApplyLowPassFilter = true;
    }

    // ==========================================
    // 7. CHANNEL: NAVIGATION (AI)
    // ==========================================
    [Serializable]
    public class NavigationChannelSettings
    {
        [Header("AI Recovery")]
        [Tooltip("Should the enemy automatically return to chasing its old target after the time anomaly ends?")]
        public bool RestoreDestination = true;
    }

    // ==========================================
    // 8. CHANNEL: PARADOX / ECHOES
    // ==========================================
    [Serializable]
    public class EchoChannelSettings
    {
        [Header("Time Paradox Settings")]
        [Tooltip("Should the echo phenomenon (creating clones) be enabled after time rewind ends?")]
        public bool EnableEchoes = true;

        [Tooltip("The Ghost prefab. It should have at least one Collider (e.g., CapsuleCollider).")]
        public GameObject GhostPrefab;

        [Tooltip("Holographic material applied to the Ghost (optional, overrides prefab materials).")]
        public Material HologramMaterial;

        [Tooltip("Time (in seconds) after which the Ghost will disappear once it finishes replaying its recorded movement.")]
        public float LifetimeAfterPlayback = 1.0f;

        [Header("Playback Settings")]
        [Tooltip("Should the Ghost immediately replay the action upon spawning? If disabled, it will stand still and wait for a signal.")]
        public bool PlayAutomatically = true;

        [Tooltip("Maximum number of concurrent clones for this object. Upon exceeding this limit, the oldest Ghost will be destroyed.")]
        [Range(1, 20)] public int MaxConcurrentEchoes = 1;

        [Header("Ghost Physics & Interactions")]
        [Tooltip("Should the Ghost activate physical Triggers (e.g., floor buttons, lasers, projectiles)? This will change its colliders to isTrigger and add a Rigidbody.")]
        public bool EnableGhostPhysics = true;

        [Tooltip("The force with which the Ghost pushes dynamic physics objects (e.g., rolling balls, crates) when passing through them.")]
        public float GhostPushForce = 50f;

        [Tooltip("Which Layer should the Ghost be moved to? (e.g., create a 'Ghost' layer in Unity that ignores collisions with the 'Player' layer).")]
        public string GhostLayerName = "Default";

        [Header("Ghost Animation & Blend Trees")]
        [Tooltip("If enabled, the Ghost will automatically search for speed parameters. If disabled, it will only use the ones specified below.")]
        public bool AutoDetectAnimationParameters = true;
        
        [Tooltip("Exact names of Blend Tree parameters into which the Ghost will pump calculated speed (e.g., Speed, Velocity).")]
        public string[] CustomSpeedParameters = new string[] { "Speed" };
        
        [Tooltip("Name of the animation speed multiplier parameter (e.g., MotionSpeed for StarterAssets). It will be forced to 1.0.")]
        public string MotionSpeedParameter = "MotionSpeed";
        
        [Tooltip("Artificial speed multiplier passed to the Animator. Increase if the Ghost's feet 'slide' too slowly compared to its movement.")]
        [Range(0.1f, 3f)] public float GhostSpeedMultiplier = 1.0f;
        
        [Tooltip("Smoothing time (Smooth Damp). Higher values (e.g., 0.2) fix jitter, but the Ghost reacts slower to sudden stops.")]
        [Range(0.01f, 0.5f)] public float AnimationDampTime = 0.15f;
    }
}