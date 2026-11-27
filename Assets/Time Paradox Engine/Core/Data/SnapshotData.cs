using Unity.Mathematics;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Collections;

namespace TimeWarp.Core.Data
{
    // =========================================================
    // --- REWIND MODES (STRATEGY PATTERN) ---
    // =========================================================

    /// <summary>
    /// Defines how standard Particle Systems (Shuriken) behave during rewind.
    /// </summary>
    public enum ParticleRewindMode
    {
        ProceduralResimulation = 0, // Classical Unity Simulate(). Heavy on CPU, zero RAM. Best for very short effects.
        DynamicRamCache = 1,        // Caches NativeArrays in RAM. Zero CPU, high RAM. Perfect, frame-accurate reverse.
        ReversePlayback = 2         // Disables original and plays a reverse prefab. Ultra-cheap hack.
    }

    /// <summary>
    /// Defines how Visual Effect Graph (VFX Graph) behaves during rewind.
    /// </summary>
    public enum VfxRewindMode
    {
        ProceduralResimulation = 0, // Reinitializes and simulates with time slicing to protect the GPU.
        EventCaching = 1            // Only records the trigger events (e.g., "OnPlay") and reverses them. Extremely cheap.
    }

    // =========================================================
    // --- SNAPSHOT DATA STRUCTURES ---
    // =========================================================

    /// <summary>
    /// Represents a single frame of Transform data (Position, Rotation, Scale).
    /// Uses Unity.Mathematics for Burst Compiler compatibility and memory efficiency.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TransformSnapshot
    {
        public float3 position;
        public quaternion rotation;
        public float3 scale;

        public TransformSnapshot(Transform t)
        {
            position = t.position;
            rotation = t.rotation;
            scale = t.localScale;
        }
    }

    /// <summary>
    /// Represents a single frame of 3D Rigidbody velocity and state.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VelocitySnapshot
    {
        public float3 linearVelocity;
        public float3 angularVelocity;
        public byte isKinematic; // 1 = true, 0 = false

        public VelocitySnapshot(Rigidbody rb, float timeScale)
        {
            linearVelocity = rb.linearVelocity;
            angularVelocity = rb.angularVelocity;
            isKinematic = rb.isKinematic ? (byte)1 : (byte)0;
        }
    }

    /// <summary>
    /// Represents a single frame of 2D Rigidbody velocity and state.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Velocity2DSnapshot
    {
        public float2 linearVelocity;
        public float angularVelocity; 
        public byte bodyType;         

        public Velocity2DSnapshot(Rigidbody2D rb, float timeScale)
        {
            linearVelocity = new float2(rb.linearVelocity.x, rb.linearVelocity.y);
            angularVelocity = rb.angularVelocity;
            bodyType = (byte)rb.bodyType;
        }
    }

    /// <summary>
    /// Represents a single frame of Animator State.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AnimSnapshot 
    { 
        public int StateHash; 
        public float NormalizedTime; 
    }

    /// <summary>
    /// A clean container to transfer all animation data (States, Bones, Parameters) 
    /// from the original object's history buffer to a Time Echo Ghost.
    /// </summary>
    public class GhostAnimPackage
    {
        // --- STATE MACHINE DATA ---
        public AnimSnapshot[] AnimPath;
        public int LayerCount;
        
        // --- BONE SNAPSHOT DATA ---
        public Vector3[] BonePos;
        public Quaternion[] BoneRot;
        public int BoneCount;

        /// <summary>
        /// Stores the names of the original bones. 
        /// Used by the Time Echo Ghost to dynamically map historical bone data 
        /// to its own skeleton, making the system model-agnostic.
        /// </summary>
        public string[] BoneNames;

        // --- ANIMATOR PARAMETERS DATA ---
        public float[] FloatHist; public int[] FloatHashes;
        public int[] IntHist;     public int[] IntHashes;
        public bool[] BoolHist;   public int[] BoolHashes;
    }

    /// <summary>
    /// A high-performance, Zero-GC container for Shuriken Particle System RAM caching.
    /// Uses Sparse Capture (Keyframing) to massively reduce CPU/RAM footprint.
    /// </summary>
    public struct ParticleFramePackage
    {
        public float SimulatedTime;
        public int AliveParticlesCount;

        // AAA OPTIMIZATION: Sparse Capture Flag
        public bool HasPlaybackState; 

        // The exact internal state of the engine (saved only on Keyframes)
        public ParticleSystem.PlaybackState PlaybackState;

        // Unmanaged memory blocks for blazing fast CPU-to-GPU transfers (saved every frame)
        public NativeArray<ParticleSystem.Particle> Particles;
    }

    /// <summary>
    /// A lightweight snapshot for VFX Graph to record when specific events were triggered.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VfxEventSnapshot
    {
        public int EventNameHash; // The hash of the VFX Event (e.g., "OnPlay", "OnStop")
        public float TriggerTime; // When it happened
    }
}