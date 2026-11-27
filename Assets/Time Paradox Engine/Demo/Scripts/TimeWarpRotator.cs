using UnityEngine;
using TimeWarp.Core; // Using the framework's main namespace

namespace TimeWarp.Gameplay
{
    public class TimeWarpRotator : MonoBehaviour
    {
        [Header("Rotation Settings")]
        [Tooltip("Axis and direction of rotation (e.g., Vector3.forward for a propeller).")]
        public Vector3 rotationAxis = new Vector3(0, 0, 1);
        
        [Tooltip("Rotation speed in degrees per second.")]
        public float rotationSpeed = 360f;

        // Reference to the time identity to read the current time speed
        private TimeWarpIdentity _timeIdentity;

        private void Start()
        {
            // Find the TimeWarpIdentity component on this object or its parents
            _timeIdentity = GetComponentInParent<TimeWarpIdentity>();
            
            if (_timeIdentity == null)
            {
                Debug.LogWarning($"[TimeWarpRotator] Missing TimeWarpIdentity on object {gameObject.name}! The script will run in normal game time.");
            }
        }

        private void Update()
        {
            // 1. Get the local time scale (default to 1 if the object is not in the system)
            float localTimeScale = _timeIdentity != null ? _timeIdentity.LocalTimeScale : 1f;

            // 2. If time is completely stopped (Time Stop), we perform no calculations
            if (localTimeScale == 0f) return;

            // 3. Calculate the final speed (accounting for Slow-Mo or Rewind if LocalTimeScale is negative)
            float currentSpeed = rotationSpeed * localTimeScale;

            // 4. Rotate the object
            transform.Rotate(rotationAxis * currentSpeed * Time.deltaTime);
        }
    }
}