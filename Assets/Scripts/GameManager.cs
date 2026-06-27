using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// Base game manager — entry point for game initialization.
    /// </summary>
    public class GameManager_Obsolete : MonoBehaviour
    {
        /// <summary>C20-01: Current game difficulty level (0=Easy, 1=Normal, 2=Hard).</summary>
        public static int CurrentDifficulty { get; set; } = 0;

        [SerializeField] private bool _debugMode = false;

        private void Awake()
        {
            Application.targetFrameRate = 60;
        }

        private void Start()
        {
            if (_debugMode)
                Debug.Log("[GameManager] Game initialized in debug mode");
            else
                Debug.Log("[GameManager] Game initialized");

            InitializeSystems();
        }

        private void InitializeSystems()
        {
            // TODO: Initialize game systems
        }
    }
}