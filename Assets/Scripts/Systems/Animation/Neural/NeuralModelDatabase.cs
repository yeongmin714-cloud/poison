using System;
using System.Linq;
using UnityEngine;

namespace ProjectName.Systems.Animation.Neural
{
    /// <summary>
    /// ScriptableObject database that maps NeuralAnimationController.PolicyType
    /// to ONNX model paths (Resources-relative) and their metadata.
    ///
    /// Used by NeuralAnimationController at runtime to locate and load policy models,
    /// and by NeuralModelAutoSetup (Editor) for automated population.
    ///
    /// Usage:
    ///   var db = Resources.Load<NeuralModelDatabase>("NeuralModelDatabase");
    ///   string path = db.GetModelPath(NeuralAnimationController.PolicyType.Locomotion);
    ///   var meta = db.GetMetadata(NeuralAnimationController.PolicyType.Combat);
    /// </summary>
    [CreateAssetMenu(
        menuName = "Poison/Neural/Model Database",
        fileName = "NeuralModelDatabase.asset")]
    public class NeuralModelDatabase : ScriptableObject
    {
        // ──────────────────────────────────────────────
        // Serialized Data
        // ──────────────────────────────────────────────

        [Serializable]
        public struct PolicyEntry
        {
            /// <summary>Which policy type this entry describes.</summary>
            public NeuralAnimationController.PolicyType policyType;

            /// <summary>Resources-relative path to the .onnx model (e.g. "NeuralModels/locomotion_biped_base").</summary>
            public string modelPath;

            /// <summary>Metadata about the model (I/O tensor sizes, joint count, version, etc.).</summary>
            public PolicyMetadata metadata;
        }

        /// <summary>
        /// All registered policy entries.
        /// Populated manually or via Tools/Neural/Auto-Setup Model Database.
        /// </summary>
        [SerializeField] private PolicyEntry[] _policies = Array.Empty<PolicyEntry>();

        /// <summary>
        /// Default avatar type for this database.
        /// Used when a policy entry doesn't specify an avatar type.
        /// </summary>
        [SerializeField] private AvatarType _defaultAvatarType = AvatarType.Humanoid;

        // ──────────────────────────────────────────────
        // Public Accessors
        // ──────────────────────────────────────────────

        /// <summary>
        /// All registered policy entries.
        /// </summary>
        public PolicyEntry[] Policies => _policies;

        /// <summary>
        /// Default avatar type.
        /// </summary>
        public AvatarType DefaultAvatarType
        {
            get => _defaultAvatarType;
            set => _defaultAvatarType = value;
        }

        /// <summary>
        /// Get the Resources-relative model path for the given policy type.
        /// Returns null if not found.
        /// </summary>
        public string GetModelPath(NeuralAnimationController.PolicyType type)
        {
            var entry = FindEntry(type);
            return entry.HasValue ? entry.Value.modelPath : null;
        }

        /// <summary>
        /// Get the metadata for the given policy type.
        /// Returns default(PolicyMetadata) if not found — check IsValid on the result.
        /// </summary>
        public PolicyMetadata GetMetadata(NeuralAnimationController.PolicyType type)
        {
            var entry = FindEntry(type);
            return entry.HasValue ? entry.Value.metadata : default;
        }

        /// <summary>
        /// Try to get metadata for the given policy type.
        /// </summary>
        /// <param name="type">The policy type to look up.</param>
        /// <param name="metadata">Output metadata if found.</param>
        /// <returns>True if the policy type was found.</returns>
        public bool TryGetMetadata(NeuralAnimationController.PolicyType type, out PolicyMetadata metadata)
        {
            var entry = FindEntry(type);
            if (entry.HasValue)
            {
                metadata = entry.Value.metadata;
                return true;
            }
            metadata = default;
            return false;
        }

        /// <summary>
        /// Check whether a policy type is registered in this database.
        /// </summary>
        public bool HasPolicy(NeuralAnimationController.PolicyType type)
        {
            return FindEntry(type).HasValue;
        }

        /// <summary>
        /// Return the number of registered policies.
        /// </summary>
        public int Count => _policies.Length;

        /// <summary>
        /// Set the entire policies array (used by Editor auto-setup).
        /// </summary>
        public void SetPolicies(PolicyEntry[] entries)
        {
            _policies = entries;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        // ──────────────────────────────────────────────
        // Internal Helpers
        // ──────────────────────────────────────────────

        private PolicyEntry? FindEntry(NeuralAnimationController.PolicyType type)
        {
            for (int i = 0; i < _policies.Length; i++)
            {
                if (_policies[i].policyType == type)
                    return _policies[i];
            }
            return null;
        }

        // ──────────────────────────────────────────────
        // Validation
        // ──────────────────────────────────────────────

        /// <summary>
        /// Validate that all entries have valid model paths and metadata.
        /// Logs warnings for missing or invalid entries.
        /// </summary>
        public void Validate()
        {
            foreach (var entry in _policies)
            {
                if (string.IsNullOrEmpty(entry.modelPath))
                {
                    Debug.LogWarning($"[NeuralModelDatabase] Entry {entry.policyType} has empty modelPath.");
                }
                if (!entry.metadata.IsValid)
                {
                    Debug.LogWarning($"[NeuralModelDatabase] Entry {entry.policyType} has invalid metadata.");
                }
            }
        }
    }
}