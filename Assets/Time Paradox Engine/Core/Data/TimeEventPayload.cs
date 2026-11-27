// File: TimeEventPayload.cs
using UnityEngine;

namespace TimeWarp.Core.Data
{
    /// <summary>
    /// A lightweight, zero-allocation value type (Struct) used to pass complex data 
    /// from the Player to the Time Echo Ghost without triggering Garbage Collection.
    /// </summary>
    [System.Serializable]
    public struct TimeEventPayload
    {
        [Tooltip("The hashed ID of the event.")]
        public int EventHash;
        
        // Pockets for floating-point numbers (e.g., charge power, damage multiplier)
        public float Float1;
        public float Float2;
        
        // Pockets for integers (e.g., weapon ID, item index, enum state)
        public int Int1;
        public int Int2;
        
        // Pockets for vectors (e.g., aim direction, hit point, target position)
        public Vector3 Vector1;
        public Vector3 Vector2;
        
        // Pocket for an additional string hash (e.g., specific bone name)
        public int StringHash;

        /// <summary>
        /// Creates a clean payload with a hashed event name.
        /// </summary>
        public TimeEventPayload(string eventName)
        {
            EventHash = Animator.StringToHash(eventName);
            Float1 = 0f;
            Float2 = 0f;
            Int1 = 0;
            Int2 = 0;
            Vector1 = Vector3.zero;
            Vector2 = Vector3.zero;
            StringHash = 0;
        }
    }
}