using System.Collections.Generic;
using UnityEngine;
using TimeWarp.Core;

namespace TimeWarp.Modules.Paradox
{
    /// <summary>
    /// Automatically attached to the Time Echo Ghost.
    /// Allows the Ghost's trigger colliders to physically push other rigidbodies
    /// (like rolling balls or debris) without getting stuck on static environment geometry.
    /// </summary>
    public class TimeGhostPusher : MonoBehaviour
    {
        [Tooltip("Multiplier for the push force applied to dynamic rigidbodies.")]
        public float PushForceMultiplier = 50f;

        [Header("Do Not Push")]
        [Tooltip("Bodies with any of these tags are never pushed.\n\n" +
                 "Defaults to Player so the ghost cannot shove the original character - " +
                 "this used to be hardcoded in the script.")]
        public List<string> IgnoredTags = new List<string> { "Player" };

        [Tooltip("Bodies on any of these layers are never pushed. Set to Nothing to ignore.")]
        public LayerMask IgnoredLayers = 0;
        
        private Vector3 _lastPosition;
        private Vector3 _velocity;

        private void Start()
        {
            _lastPosition = transform.position;
        }

        private void FixedUpdate()
        {
            // Calculate the Ghost's velocity for the current frame based on forced positional changes
            _velocity = (transform.position - _lastPosition) / Time.fixedDeltaTime;
            _lastPosition = transform.position;
        }

        private void OnTriggerStay(Collider other)
        {
            // Check if the overlapped object has a Rigidbody and is physically simulated (not kinematic)
            if (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic)
            {
                // ==========================================
                // IMPORTANT: test the BODY, not the collider.
                // ==========================================
                // This check used to be "other.CompareTag(\"Player\")", which reads the tag
                // off the collider's own GameObject. That works for a character with one
                // collider on the root, but a ragdoll or any character with child hitboxes
                // has UNTAGGED child colliders - so the guard silently failed and the ghost
                // shoved the player's bones around: exactly what it was meant to prevent.
                //
                // attachedRigidbody is the root of the physics body and is already resolved
                // above, so testing it costs nothing and is correct in both layouts.
                GameObject body = other.attachedRigidbody.gameObject;

                if (TimeWarpTagUtility.HasAnyTag(body, IgnoredTags)) return;

                if (IgnoredLayers.value != 0 &&
                    TimeWarpTagUtility.IsInLayerMask(body, IgnoredLayers)) return;

                Vector3 pushDirection = _velocity.normalized;
                float speed = _velocity.magnitude;

                // Apply force only if the Ghost is actively moving
                if (speed > 0.1f)
                {
                    // Using VelocityChange applies an instant velocity change to the rigidbody, 
                    // ignoring its mass. This makes the anomaly feel unstoppable.
                    other.attachedRigidbody.AddForce(pushDirection * speed * PushForceMultiplier * Time.fixedDeltaTime, ForceMode.VelocityChange);
                }
            }
        }
    }
}