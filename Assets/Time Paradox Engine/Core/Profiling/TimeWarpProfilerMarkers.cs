using Unity.Profiling;

namespace TimeWarp.Core.Profiling
{
    /// <summary>
    /// Named Profiler markers for the engine's hot path.
    ///
    /// Without these, every cost the engine incurs is lumped under a generic
    /// "Scripts" / PlayerLoop entry, so a user seeing a frame time regression in a
    /// scene with a few hundred time-warped objects has no way to tell whether it is
    /// recording, rewinding, the state pre-pass, or the particle cache.
    ///
    /// ProfilerMarker is effectively free when the Profiler is not attached, so these
    /// are always compiled in - no #if guards needed.
    ///
    /// Look for these under the "TimeWarp." prefix in the Profiler's CPU Usage
    /// hierarchy, or add them as custom charts.
    /// </summary>
    public static class TimeWarpProfilerMarkers
    {
        // --- Per-frame main-thread passes ---

        /// <summary>Pre-pass that resolves the effective time scale of every identity (FixedUpdate).</summary>
        public static readonly ProfilerMarker PreCalculateFixed =
            new ProfilerMarker("TimeWarp.PreCalculateState (Fixed)");

        /// <summary>Pre-pass that resolves the effective time scale of every identity (Update).</summary>
        public static readonly ProfilerMarker PreCalculateUpdate =
            new ProfilerMarker("TimeWarp.PreCalculateState (Update)");

        /// <summary>Writing one frame of history: velocities on the main thread + scheduling the transform job.</summary>
        public static readonly ProfilerMarker RecordFrame =
            new ProfilerMarker("TimeWarp.RecordFrame");

        /// <summary>Resolving rewind indices and scheduling the transform playback job.</summary>
        public static readonly ProfilerMarker ApplyRewindFrame =
            new ProfilerMarker("TimeWarp.ApplyRewindFrame");

        /// <summary>Per-module ticks driven from FixedUpdate.</summary>
        public static readonly ProfilerMarker TickFixedUpdate =
            new ProfilerMarker("TimeWarp.TickFixedUpdate");

        /// <summary>Per-module ticks driven from Update.</summary>
        public static readonly ProfilerMarker TickUpdate =
            new ProfilerMarker("TimeWarp.TickUpdate");

        /// <summary>
        /// Blocking wait for the transform job to finish. A high value here means the
        /// job did not have enough time to complete between schedule and sync point -
        /// usually a sign of too many identities rather than slow user code.
        /// </summary>
        public static readonly ProfilerMarker WaitForTransformJob =
            new ProfilerMarker("TimeWarp.WaitForTransformJob");

        /// <summary>Disposal of buffers belonging to destroyed identities.</summary>
        public static readonly ProfilerMarker ProcessDisposalQueue =
            new ProfilerMarker("TimeWarp.ProcessDisposalQueue");

        // --- Subsystems ---

        /// <summary>Capturing a particle system's state into the RAM cache.</summary>
        public static readonly ProfilerMarker ParticleCapture =
            new ProfilerMarker("TimeWarp.Particles.Capture");

        /// <summary>Restoring a cached particle frame during a rewind.</summary>
        public static readonly ProfilerMarker ParticleRestore =
            new ProfilerMarker("TimeWarp.Particles.Restore");

        /// <summary>Cloning and stripping a Time Echo Ghost (or fetching one from the pool).</summary>
        public static readonly ProfilerMarker GhostSpawn =
            new ProfilerMarker("TimeWarp.Ghost.Spawn");
    }
}
