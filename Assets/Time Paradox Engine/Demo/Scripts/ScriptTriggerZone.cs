using UnityEngine;
using System.Collections.Generic;

namespace TimeWarp.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class ScriptTriggerZone : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The tag of the object that should activate the scripts (e.g., Player).")]
        public string targetTag = "Player";

        [Header("Scripts to Manage")]
        [Tooltip("Drag the scripts you want to enable here.")]
        public List<MonoBehaviour> scriptsToManage;

        [Header("Options")]
        [Tooltip("Should the scripts be disabled by default at the start of the game?")]
        public bool disableOnStart = true;
        
        [Tooltip("Should the scripts be disabled when the object leaves the trigger?")]
        public bool disableOnExit = true;

        private void Start()
        {
            // Disable scripts on start (if checked)
            if (disableOnStart)
            {
                SetScriptsState(false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if the entering object has the correct tag
            if (other.CompareTag(targetTag))
            {
                SetScriptsState(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Check if the exiting object has the correct tag
            if (disableOnExit && other.CompareTag(targetTag))
            {
                SetScriptsState(false);
            }
        }

        // Helper method to enable/disable scripts
        private void SetScriptsState(bool state)
        {
            foreach (var script in scriptsToManage)
            {
                if (script != null)
                {
                    script.enabled = state;
                }
            }
        }
    }
}