using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// Base game manager — entry point for game initialization.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
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