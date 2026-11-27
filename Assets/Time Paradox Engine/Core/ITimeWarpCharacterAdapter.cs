namespace TimeWarp.Core
{
    /// <summary>
    /// Global interface for character controllers.
    /// Replaces the built-in Time.deltaTime with TimeWarp scaled time.
    /// </summary>
    public interface ITimeWarpCharacterAdapter
    {
        /// <summary>
        /// Returns Time.deltaTime multiplied by the local time scale.
        /// Returns 0 during a complete stop (TimeStop) and rewind (Rewind).
        /// </summary>
        float GetLocalDeltaTime();
        
        /// <summary>
        /// Returns Time.fixedDeltaTime multiplied by the local time scale.
        /// </summary>
        float GetLocalFixedDeltaTime();
        
        /// <summary>
        /// Returns TRUE if the player should completely lose control over the character
        /// (e.g., during time rewind, when the history buffer takes over the position).
        /// </summary>
        bool IsInputLocked();
    }
}