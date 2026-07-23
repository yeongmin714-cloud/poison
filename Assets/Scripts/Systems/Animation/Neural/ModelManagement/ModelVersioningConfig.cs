using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems.Animation.Neural.ModelManagement
{
    /// <summary>
    /// Version info for a single ONNX policy model.
    /// </summary>
    [Serializable]
    public struct ModelVersionInfo
    {
        public string modelName;
        public string version;
        public string dateTrained;
        public int epochCount;
        public float validationScore;
        public string hash;
        public string notes;

        public string DisplayName => $"{modelName} v{version}";
    }

    /// <summary>
    /// Manages model versioning, rollback, and A/B testing for Neural Animation policies.
    /// Tracks the active and available versions for each policy type.
    /// </summary>
    [CreateAssetMenu(fileName = "ModelVersioningConfig", menuName = "Animation/Model Versioning Config")]
    public class ModelVersioningConfig : ScriptableObject
    {
        [Header("Active Versions")]
        [SerializeField] string _locomotionVersion = "1.0.0";
        [SerializeField] string _combatVersion = "1.0.0";
        [SerializeField] string _reactVersion = "1.0.0";
        [SerializeField] string _interactVersion = "1.0.0";
        [SerializeField] string _flyVersion = "1.0.0";
        [SerializeField] string _swimVersion = "1.0.0";

        [Header("Version History")]
        [SerializeField] List<ModelVersionInfo> _versionHistory = new List<ModelVersionInfo>();

        [Header("Rollback")]
        [SerializeField] string _rollbackVersion = "";

        // ──────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Get the active version string for a policy.
        /// </summary>
        public string GetActiveVersion(NeuralAnimationController.PolicyType policy)
        {
            return policy switch
            {
                NeuralAnimationController.PolicyType.Locomotion => _locomotionVersion,
                NeuralAnimationController.PolicyType.Combat => _combatVersion,
                NeuralAnimationController.PolicyType.React => _reactVersion,
                NeuralAnimationController.PolicyType.Interact => _interactVersion,
                NeuralAnimationController.PolicyType.Fly => _flyVersion,
                NeuralAnimationController.PolicyType.Swim => _swimVersion,
                _ => "1.0.0"
            };
        }

        /// <summary>
        /// Get the model path for a policy with version.
        /// </summary>
        public string GetModelPath(NeuralAnimationController.PolicyType policy)
        {
            string baseName = policy switch
            {
                NeuralAnimationController.PolicyType.Locomotion => "locomotion",
                NeuralAnimationController.PolicyType.Combat => "combat",
                NeuralAnimationController.PolicyType.React => "react",
                NeuralAnimationController.PolicyType.Interact => "interact",
                NeuralAnimationController.PolicyType.Fly => "fly",
                NeuralAnimationController.PolicyType.Swim => "swim",
                _ => "locomotion"
            };

            // Check for rollback
            string version = !string.IsNullOrEmpty(_rollbackVersion) ? _rollbackVersion : GetActiveVersion(policy);
            return $"NeuralModels/{baseName}_base_v{version.Replace(".", "_")}";
        }

        /// <summary>
        /// Get fallback model path (earlier version).
        /// </summary>
        public string GetFallbackModelPath(NeuralAnimationController.PolicyType policy)
        {
            string baseName = policy.ToString().ToLower();
            return $"NeuralModels/{baseName}_base"; // No version suffix = base fallback
        }

        /// <summary>
        /// Rollback a policy to a previous version.
        /// </summary>
        public void RollbackTo(string version)
        {
            _rollbackVersion = version;
            Debug.Log($"[ModelVersioning] Rollback set to v{version}");
        }

        /// <summary>
        /// Clear rollback and use active versions.
        /// </summary>
        public void ClearRollback()
        {
            _rollbackVersion = "";
            Debug.Log("[ModelVersioning] Rollback cleared");
        }

        /// <summary>
        /// Record a new model version in history.
        /// </summary>
        public void RecordVersion(ModelVersionInfo info)
        {
            _versionHistory.Add(info);
            Debug.Log($"[ModelVersioning] Recorded {info.DisplayName}");
        }

        /// <summary>
        /// Get all version history entries for a model.
        /// </summary>
        public ModelVersionInfo[] GetHistory(string modelName)
        {
            return _versionHistory.FindAll(v => v.modelName == modelName).ToArray();
        }

        /// <summary>
        /// Currently in rollback mode.
        /// </summary>
        public bool IsRollbackActive => !string.IsNullOrEmpty(_rollbackVersion);
    }
}