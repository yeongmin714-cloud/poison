using UnityEngine;

namespace CustomGameplay.Utilities
{
    public class SimpleFollower : MonoBehaviour
    {
        [Tooltip("Drag your player object here.")]
        public Transform Target;

        [Tooltip("How far behind the player should the Cube be?")]
        public float Distance = 3f;

        [Tooltip("How high above the ground should it hover?")]
        public float Height = 1.5f;

        private void LateUpdate()
        {
            if (Target == null) return;

            // Sets the Cube's position behind the player (-Target.forward) taking height into account.
            transform.position = Target.position - (Target.forward * Distance) + (Vector3.up * Height);

            // Makes the Cube look in the exact same direction as the player.
            transform.rotation = Target.rotation;
        }
    }
}