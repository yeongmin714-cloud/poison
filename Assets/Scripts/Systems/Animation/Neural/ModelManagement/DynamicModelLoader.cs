using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;

namespace ProjectName.Systems.Animation.Neural.ModelManagement
{
    /// <summary>
    /// Priority-ordered fallback chain for Neural Animation models.
    /// Neural → Procedural → Keyframe (last resort).
    /// </summary>
    public enum FallbackLevel
    {
        /// <summary>Full Neural inference (ONNX policy).</summary>
        Neural = 0,
        /// <summary>Procedural fallback (HybridAnimationController procedural side).</summary>
        Procedural = 1,
        /// <summary>Keyframe/animation clip fallback (last resort).</summary>
        Keyframe = 2
    }

    /// <summary>
    /// Dynamic model loader with fallback chain support.
    /// Loads ONNX models asynchronously and provides fallback on failure.
    /// Attach to NeuralAnimationController for automatic management.
    /// </summary>
    [RequireComponent(typeof(NeuralAnimationController))]
    public class DynamicModelLoader : MonoBehaviour
    {
        [Header("Model Versioning")]
        [SerializeField] ModelVersioningConfig _versioningConfig;

        [Header("Fallback Settings")]
        [SerializeField] FallbackLevel _currentFallbackLevel = FallbackLevel.Neural;
        [SerializeField] bool _autoFallback = true;

        [Header("Dynamic Loading")]
        [SerializeField] bool _loadOnAwake = true;
        [SerializeField] string _modelBasePath = "NeuralModels/";

        [Header("Status")]
        [SerializeField] bool _allModelsLoaded;
        [SerializeField] int _modelsLoaded;
        [SerializeField] int _modelsFailed;

        // ──────────────────────────────────────────────
        //  Events
        // ──────────────────────────────────────────────

        /// <summary>Fired when fallback level changes.</summary>
        public event Action<FallbackLevel> OnFallbackChanged;

        // ──────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────

        NeuralAnimationController _controller;
        Dictionary<NeuralAnimationController.PolicyType, bool> _loadStatus = new Dictionary<NeuralAnimationController.PolicyType, bool>();

        // ──────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            _controller = GetComponent<NeuralAnimationController>();
            if (_loadOnAwake)
                StartCoroutine(LoadAllModelsCoroutine());
        }

        // ──────────────────────────────────────────────
        //  Dynamic Model Loading
        // ──────────────────────────────────────────────

        /// <summary>
        /// Load all policy models asynchronously with versioning support.
        /// </summary>
        public void LoadAllModels()
        {
            StartCoroutine(LoadAllModelsCoroutine());
        }

        IEnumerator LoadAllModelsCoroutine()
        {
            _allModelsLoaded = false;
            _modelsLoaded = 0;
            _modelsFailed = 0;
            _loadStatus.Clear();

            var policies = (NeuralAnimationController.PolicyType[])Enum.GetValues(typeof(NeuralAnimationController.PolicyType));
            int total = policies.Length;

            foreach (var policy in policies)
            {
                yield return StartCoroutine(LoadModelCoroutine(policy));
            }

            _allModelsLoaded = (_modelsLoaded + _modelsFailed) >= total;
            Debug.Log($"[DynamicModelLoader] Loaded {_modelsLoaded}/{total} models ({_modelsFailed} failed)");

            if (_modelsFailed > 0 && _autoFallback)
            {
                SetFallbackLevel(FallbackLevel.Procedural);
            }
        }

        IEnumerator LoadModelCoroutine(NeuralAnimationController.PolicyType policy)
        {
            string path = GetModelPath(policy);
            var request = Resources.LoadAsync<ModelAsset>(path);
            yield return request;

            var asset = request.asset as ModelAsset;
            if (asset != null)
            {
#if UNITY_SENTIS
                try
                {
                    _loadStatus[policy] = true;
                    _modelsLoaded++;
                    Debug.Log($"[DynamicModelLoader] Loaded {policy} from {path}");
                }
                catch
                {
                    _loadStatus[policy] = false;
                    _modelsFailed++;
                    Debug.LogWarning($"[DynamicModelLoader] Failed to load {policy} from {path}");
                }
#else
                _loadStatus[policy] = true;
                _modelsLoaded++;
#endif
            }
            else
            {
                // Try fallback path
                string fallbackPath = GetFallbackPath(policy);
                var fallbackRequest = Resources.LoadAsync<ModelAsset>(fallbackPath);
                yield return fallbackRequest;

                if (fallbackRequest.asset != null)
                {
                    _loadStatus[policy] = true;
                    _modelsLoaded++;
                    Debug.Log($"[DynamicModelLoader] Loaded {policy} from fallback: {fallbackPath}");
                }
                else
                {
                    _loadStatus[policy] = false;
                    _modelsFailed++;
                    Debug.LogWarning($"[DynamicModelLoader] Model not found: {path} (fallback: {fallbackPath})");
                }
            }
        }

        /// <summary>
        /// Get model path with versioning.
        /// </summary>
        string GetModelPath(NeuralAnimationController.PolicyType policy)
        {
            if (_versioningConfig != null)
                return _versioningConfig.GetModelPath(policy);
            return $"{_modelBasePath}{policy.ToString().ToLower()}_base";
        }

        /// <summary>
        /// Get fallback model path (without version suffix).
        /// </summary>
        string GetFallbackPath(NeuralAnimationController.PolicyType policy)
        {
            if (_versioningConfig != null)
                return _versioningConfig.GetFallbackModelPath(policy);
            return $"{_modelBasePath}{policy.ToString().ToLower()}_base";
        }

        // ──────────────────────────────────────────────
        //  Fallback Chain
        // ──────────────────────────────────────────────

        /// <summary>
        /// Set the current fallback level.
        /// </summary>
        public void SetFallbackLevel(FallbackLevel level)
        {
            if (_currentFallbackLevel == level) return;
            _currentFallbackLevel = level;
            OnFallbackChanged?.Invoke(level);
            Debug.Log($"[DynamicModelLoader] Fallback level: {level}");

            ApplyFallback();
        }

        /// <summary>
        /// Attempt recovery to Neural from fallback.
        /// </summary>
        public void AttemptRecovery()
        {
            if (_currentFallbackLevel == FallbackLevel.Neural) return;
            Debug.Log("[DynamicModelLoader] Attempting recovery to Neural...");
            StartCoroutine(LoadAllModelsCoroutine());
        }

        void ApplyFallback()
        {
            var hybridCtrl = GetComponent<HybridAnimationController>();
            if (hybridCtrl == null) return;

            switch (_currentFallbackLevel)
            {
                case FallbackLevel.Neural:
                    // Full Neural — restore rollout phase settings
                    if (ProgressiveRolloutManager.Instance != null)
                        ProgressiveRolloutManager.Instance.ConfigureHybridController(hybridCtrl);
                    break;

                case FallbackLevel.Procedural:
                    // 100% Procedural
                    hybridCtrl.SetBaseWeights(1f, 0f);
                    hybridCtrl.ClearAllPolicyOverrides();
                    Debug.Log("[DynamicModelLoader] Fallback: Procedural mode");
                    break;

                case FallbackLevel.Keyframe:
                    // Disable both, let Animator play default clips
                    hybridCtrl.SetBaseWeights(0f, 0f);
                    hybridCtrl.ClearAllPolicyOverrides();
                    var animator = GetComponent<Animator>();
                    if (animator != null)
                        animator.applyRootMotion = true;
                    Debug.Log("[DynamicModelLoader] Fallback: Keyframe mode");
                    break;
            }
        }

        /// <summary>
        /// Current fallback level.
        /// </summary>
        public FallbackLevel CurrentFallbackLevel => _currentFallbackLevel;

        /// <summary>
        /// Whether all models are successfully loaded.
        /// </summary>
        public bool AllModelsLoaded => _allModelsLoaded;

        /// <summary>
        /// Check if a specific policy model loaded successfully.
        /// </summary>
        public bool IsModelLoaded(NeuralAnimationController.PolicyType policy)
        {
            return _loadStatus.TryGetValue(policy, out bool loaded) && loaded;
        }
    }
}