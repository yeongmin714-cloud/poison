// File: TimeEventRecorder.cs
using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Data;

namespace TimeWarp.Modules.Gameplay
{
    /// <summary>
    /// Records custom gameplay events (like shooting, dropping items) into a cyclical buffer.
    /// Passes the data to the Paradox Ghost when time is rewound.
    /// </summary>
    [RequireComponent(typeof(TimeWarpIdentity))]
    [AddComponentMenu("TimeWarp/Gameplay/Time Event Recorder")]
    public class TimeEventRecorder : MonoBehaviour, ITimeWarpModule
    {
        private TimeWarpIdentity _identity;
        private TimeEventPayload[] _eventHistory;
        private bool _isInitialized = false;
        
        // Temporarily holds the payload triggered in the current frame
        private TimeEventPayload _payloadThisFrame; 
        private int _lastRecordedHead = -1;

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
        }

        /// <summary>
        /// Call this method from your custom game scripts (e.g., Weapon, Magic).
        /// Example: GetComponent<TimeEventRecorder>().RecordEvent(new TimeEventPayload("Shoot"));
        /// </summary>
        public void RecordEvent(TimeEventPayload payload)
        {
            _payloadThisFrame = payload;
        }

        public void OnWarpFixedUpdate()
        {
            if (!_identity.IsRegistered) return;

            if (!_isInitialized)
            {
                _eventHistory = new TimeEventPayload[_identity.Buffer.Count];
                _isInitialized = true;
            }

            // --- CULLING OPTIMIZATION: Only record if allowed ---
            if (_identity.CanRecordHistory)
            {
                int head = _identity.Buffer.GetIndex(0);
                if (head != _lastRecordedHead)
                {
                    _lastRecordedHead = head;
                    
                    // Save the payload (or default empty struct if nothing happened) and reset
                    _eventHistory[head] = _payloadThisFrame;
                    _payloadThisFrame = default; // Resets all fields to 0
                }
            }
        }

        public void OnWarpUpdate() { }

        /// <summary>
        /// Extracts the recorded events for the Paradox Ghost.
        /// </summary>
        public TimeEventPayload[] ExtractEventData(int currentIndex, int framesRewound)
        {
            if (!_isInitialized) return null;

            TimeEventPayload[] ghostEvents = new TimeEventPayload[framesRewound];
            for (int i = 0; i < framesRewound; i++)
            {
                int bufferIndex = (currentIndex + i) % _identity.Buffer.Count;
                ghostEvents[i] = _eventHistory[bufferIndex];
            }
            return ghostEvents;
        }
    }
}