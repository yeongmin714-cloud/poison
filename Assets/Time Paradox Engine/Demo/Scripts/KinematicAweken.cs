using System.Collections.Generic;
using UnityEngine;
using TimeWarp.Core;

namespace TimeWarp.Gameplay
{
    /// <summary>
    /// A trap volume that "wakes up" kinematic rigidbodies when something enters it.
    ///
    /// Both sides of this - what triggers the trap, and what the trap wakes - can now be
    /// selected by TAG as well as by layer. Layers are a scarce resource (32 in total, most
    /// of them already spoken for by the collision matrix), so burning one just to mark
    /// "these crates are the ones that fall" is rarely a good trade.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("TimeWarp/Gameplay/Kinematic Awakener")]
    public class KinematicAwakener : MonoBehaviour
    {
        [Header("What triggers the trap")]
        [Tooltip("RECOMMENDED. Objects carrying any of these tags trigger the trap.")]
        public List<string> ActivatorTags = new List<string> { "Player" };

        [Tooltip("Objects on any of these layers trigger the trap. Combined with the tags above using OR.")]
        public LayerMask ActivatorLayers = 0;

        [Tooltip("LEGACY. Matches by comparing the GameObject's name to this prefab's name.\n\n" +
                 "Fragile: renaming the object breaks it, two prefabs sharing a name both match, and " +
                 "Unity appends \" (1)\" to scene duplicates. Prefer the tag list above.")]
        public GameObject ActivatorPrefab;

        [Tooltip("Should the trap trigger only once?")]
        public bool TriggerOnlyOnce = true;

        [Header("What gets woken up")]
        [Tooltip("RECOMMENDED. Rigidbodies carrying any of these tags are released.")]
        public List<string> TargetTags = new List<string>();

        [Tooltip("Rigidbodies on any of these layers are released. Combined with the tags above using OR.")]
        public LayerMask TargetLayer;

        [Header("Release")]
        [Tooltip("Optional nudge applied to each woken body, so they start moving instead of " +
                 "sitting perfectly still until something touches them.")]
        public Vector3 WakeImpulse = Vector3.zero;

        private bool _hasTriggered = false;

        private void Awake()
        {
            // Ensure the collider on this object is set to Trigger
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // If the trap has already been triggered and should only work once, abort
            if (_hasTriggered && TriggerOnlyOnce) return;

            if (IsActivator(other))
            {
                _hasTriggered = true;
                WakeUpTargets();
            }
        }

        private bool IsActivator(Collider col)
        {
            // Test the physics BODY, not the collider. A character with child hitboxes or a
            // ragdoll has untagged child colliders, so testing the collider itself would
            // silently miss the very object we are looking for.
            GameObject go = col.attachedRigidbody != null
                ? col.attachedRigidbody.gameObject
                : col.gameObject;

            // 1. Tag or layer - robust, survives renaming and duplication.
            if (TimeWarpTagUtility.MatchesLayerOrTag(go, ActivatorLayers, ActivatorTags)) return true;

            // 2. LEGACY name match, kept so existing scenes keep working.
            if (ActivatorPrefab != null)
            {
                string objName = go.name.Replace("(Clone)", "").Trim();
                if (objName == ActivatorPrefab.name) return true;
            }

            return false;
        }

        private void WakeUpTargets()
        {
            Rigidbody[] allRigidbodies = FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude);

            int woken = 0;

            foreach (Rigidbody rb in allRigidbodies)
            {
                if (!TimeWarpTagUtility.MatchesLayerOrTag(rb.gameObject, TargetLayer, TargetTags)) continue;

                if (!rb.isKinematic) continue;

                rb.isKinematic = false;
                rb.WakeUp();

                if (WakeImpulse != Vector3.zero) rb.AddForce(WakeImpulse, ForceMode.Impulse);

                woken++;
            }

            if (woken == 0)
            {
                Debug.LogWarning(
                    $"[TimeWarp] \"{name}\" triggered but woke nothing. Check that the targets are " +
                    $"KINEMATIC rigidbodies and that they match the tags or layers configured here.", this);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            bool hasActivator = (ActivatorTags != null && ActivatorTags.Count > 0)
                             || ActivatorLayers.value != 0
                             || ActivatorPrefab != null;

            if (!hasActivator)
                Debug.LogWarning($"[TimeWarp] \"{name}\": nothing can trigger this trap - set an activator tag, layer or prefab.", this);

            bool hasTarget = (TargetTags != null && TargetTags.Count > 0) || TargetLayer.value != 0;

            if (!hasTarget)
                Debug.LogWarning($"[TimeWarp] \"{name}\": no targets configured - the trap will wake nothing.", this);
        }
#endif
    }
}
