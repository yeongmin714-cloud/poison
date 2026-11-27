using UnityEngine;
using Unity.Collections;
using System;

namespace TimeWarp.Core.Data
{
    /// <summary>
    /// A highly optimized circular buffer that stores the history of a TimeWarpIdentity.
    /// Uses NativeArrays for Job System compatibility and Zero-GC allocation per frame.
    /// AAA Optimizations: Power-of-Two sizing, Bitmask indexing, Adaptive RAM allocation.
    /// </summary>
    public class TimeWarpBuffer : IDisposable
    {
        public int Head { get; private set; }
        public int Count { get; private set; }
        public bool IsDisposed { get; private set; }

        // --- NATIVE ARRAYS (For Burst / Job System) ---
        public NativeArray<TransformSnapshot> TransformHistory;

        // --- MANAGED ARRAYS (For standard modules) ---
        // MEMORY OPTIMIZATION: allocated lazily on first access.
        // Previously both arrays were allocated for EVERY identity, even purely
        // decorative objects with no Rigidbody at all. At 28 B + 16 B per frame that
        // is ~22 KB of wasted managed heap per object on a 512-frame buffer.
        // Exposing them as lazy properties keeps every existing call site working
        // (and makes a NullReferenceException impossible) while objects that never
        // touch the velocity channels now allocate nothing.
        private VelocitySnapshot[] _velocityHistory;
        private Velocity2DSnapshot[] _velocity2DHistory;

        public VelocitySnapshot[] VelocityHistory
            => _velocityHistory ??= new VelocitySnapshot[Count];

        public Velocity2DSnapshot[] Velocity2DHistory
            => _velocity2DHistory ??= new Velocity2DSnapshot[Count];

        /// <summary>
        /// True if the 3D velocity history has actually been allocated.
        /// Lets callers probe without forcing an allocation.
        /// </summary>
        public bool HasVelocityHistory => _velocityHistory != null;

        /// <summary>
        /// True if the 2D velocity history has actually been allocated.
        /// </summary>
        public bool HasVelocity2DHistory => _velocity2DHistory != null;

        // --- ULTRA PARTICLE RAM CACHE ---
        public ParticleFramePackage[] ParticleHistory; 

        private readonly int _mask;

        public TimeWarpBuffer(int framerate, float maxTime)
        {
            // AAA OPTIMIZATION: Power of 2 sizing for bitwise masking
            int rawCount = Mathf.CeilToInt(framerate * maxTime);
            Count = Mathf.NextPowerOfTwo(rawCount); 
            _mask = Count - 1; // Used for ultra-fast wrapping

            Head = 0;
            IsDisposed = false;

            TransformHistory = new NativeArray<TransformSnapshot>(Count, Allocator.Persistent);
            // Velocity arrays are NOT allocated here - see the lazy properties above.
        }

        /// <summary>
        /// Allocates unmanaged NativeArray memory for particle RAM caching.
        /// Uses Adaptive RAM limits to prevent memory explosion.
        /// </summary>
        public void AllocateParticleBuffer(int maxParticlesInSystem, int estimatedPeak)
        {
            if (ParticleHistory != null) return; 

            // AAA OPTIMIZATION: Adaptive Buffer
            // We don't blindly allocate 'maxParticlesInSystem'. We use the estimated peak
            // and cap it to prevent a single 10,000 max-particle system from eating 30MB of RAM.
            int safeMax = Mathf.Min(maxParticlesInSystem, estimatedPeak);
            
            // Hard cap for extreme safety
            safeMax = Mathf.Min(safeMax, 512);

            ParticleHistory = new ParticleFramePackage[Count];
            for (int i = 0; i < Count; i++)
            {
                ParticleHistory[i] = new ParticleFramePackage
                {
                    AliveParticlesCount = 0,
                    SimulatedTime = 0f,
                    PlaybackState = default,
                    Particles = new NativeArray<ParticleSystem.Particle>(safeMax, Allocator.Persistent)
                };
            }
        }

        public void AdvanceHead()
        {
            // Bitwise wrapping: significantly faster than modulo (%) or if-statements
            Head = (Head + 1) & _mask;
        }

        public int GetIndex(int offset)
        {
            // AAA OPTIMIZATION: O(1) Bitwise indexing. 
            // Completely eliminates while loops and modulo operations.
            return (Head - 1 - offset) & _mask;
        }

        public void GetFractionalIndex(float offset, out int indexA, out int indexB, out float t)
        {
            int baseOffset = Mathf.FloorToInt(offset);
            t = offset - baseOffset;

            indexA = GetIndex(baseOffset);
            indexB = GetIndex(baseOffset + 1);
        }

        public void CommitRewind(int framesRewound)
        {
            Head = GetIndex(framesRewound);
            AdvanceHead(); 
        }

        /// <summary>
        /// Crucial memory cleanup. Called by TimeWarpIdentity OnDestroy.
        /// NEVER use a finalizer (~TimeWarpBuffer) for this, as NativeArrays must be disposed on the Main Thread.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed) return;

            if (TransformHistory.IsCreated) 
            {
                TransformHistory.Dispose();
            }

            if (ParticleHistory != null)
            {
                for (int i = 0; i < ParticleHistory.Length; i++)
                {
                    if (ParticleHistory[i].Particles.IsCreated)
                    {
                        ParticleHistory[i].Particles.Dispose();
                    }
                }
                ParticleHistory = null;
            }

            // Drop managed references so the GC can reclaim them immediately
            // instead of waiting for the whole buffer object to become unreachable.
            _velocityHistory = null;
            _velocity2DHistory = null;

            IsDisposed = true;
        }
    }
}