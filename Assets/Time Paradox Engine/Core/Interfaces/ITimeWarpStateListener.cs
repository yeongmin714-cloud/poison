namespace TimeWarp.Core
{
    /// <summary>
    /// Universal interface for objects with their own logic (e.g., turrets, enemy AI, traps).
    /// Covers all possible TimeWarp system states to ensure perfect synchronization.
    /// </summary>
    public interface ITimeWarpStateListener
    {
        // ==========================================
        // 1. ONE-TIME EVENTS (Start)
        // Called on the exact first frame a new anomaly begins.
        // ==========================================

        /// <summary>
        /// Called when Slow Motion starts. 
        /// The script remains enabled, but you can use this to trigger sound effects or visual changes.
        /// </summary>
        /// <param name="timeScale">The current target slow-motion scale (e.g., 0.2f).</param>
        void OnTimeWarpSlowMoStart(float timeScale);

        /// <summary>
        /// Called when a complete Time Stop starts, right before the manager disables this script.
        /// Ideal for pausing AudioSources, ParticleSystems, or saving the current state.
        /// </summary>
        void OnTimeWarpTimeStopStart();

        /// <summary>
        /// Called when a Rewind starts, right before the manager disables this script.
        /// </summary>
        void OnTimeWarpRewindStart();


        // ==========================================
        // 2. RETURN TO NORMAL
        // Called ONCE when time returns to 1.0 (End of anomaly)
        // ==========================================

        /// <summary>
        /// Called when the anomaly ends and time flows normally again.
        /// Ideal for clearing dead targets (e.g., currentTarget = null), 
        /// resuming Audio/Particles, or resetting internal logic timers.
        /// </summary>
        void OnTimeWarpResume();


        // ==========================================
        // 3. VIRTUAL UPDATE (During Sleep)
        // Called EVERY FRAME while your script is disabled (enabled = false)
        // by the manager (i.e., during Time Stop and Rewind).
        // ==========================================

        /// <summary>
        /// Allows you to manually update visuals (like lights, shaders, or moving parts) 
        /// while the main logic is completely frozen by the Kill Switch.
        /// </summary>
        /// <param name="timeScale">The current global time scale.</param>
        /// <param name="rewindDeltaTime">The exact amount of time (usually negative) we rewound this frame. Will be 0f during a Time Stop.</param>
        void OnTimeWarpAnomalyUpdate(float timeScale, float rewindDeltaTime);
    }
}