using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural;

namespace ProjectName.Systems.Animation.Neural
{
    /// <summary>
    /// Singleton manager for progressive rollout of Neural animation policies.
    /// Configures HybridAnimationController instances based on the current rollout phase.
    /// </summary>
    public class ProgressiveRolloutManager : MonoBehaviour
    {
        [Header("Rollout Configuration")]
        [SerializeField] ProgressiveRolloutConfig _config;
        [SerializeField] RolloutPhase _currentPhase = RolloutPhase.Phase1_PlayerLocomotion;

        [Header("Debug")]
        [SerializeField] bool _verboseLogging = false;

        /// <summary>Current active rollout phase.</summary>
        public RolloutPhase CurrentPhase
        {
            get => _currentPhase;
            set
            {
                if (_currentPhase != value)
                {
                    _currentPhase = value;
                    OnPhaseChanged?.Invoke(value);
                    if (_verboseLogging)
                        Debug.Log($"[ProgressiveRolloutManager] Phase changed to {value}");
                }
            }
        }

        /// <summary>Event fired when the rollout phase changes.</summary>
        public event Action<RolloutPhase> OnPhaseChanged;

        /// <summary>Singleton instance.</summary>
        public static ProgressiveRolloutManager Instance { get; private set; }

        // ──────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning("[ProgressiveRolloutManager] Duplicate instance destroyed");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_config == null)
                _config = Resources.Load<ProgressiveRolloutConfig>("ProgressiveRolloutConfig");
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ──────────────────────────────────────────────
        //  Hybrid Controller Configuration
        // ──────────────────────────────────────────────

        /// <summary>
        /// Configure a HybridAnimationController for the current rollout phase.
        /// Sets policy overrides, blend weights, and LOD settings.
        /// </summary>
        public void ConfigureHybridController(HybridAnimationController controller)
        {
            if (controller == null) return;
            ConfigureHybridController(controller, _currentPhase);
        }

        /// <summary>
        /// Configure a HybridAnimationController for a specific rollout phase.
        /// </summary>
        public void ConfigureHybridController(HybridAnimationController controller, RolloutPhase phase)
        {
            if (controller == null) return;

            PhaseConfig config = GetPhaseConfig(phase);
            if (_verboseLogging)
                Debug.Log($"[ProgressiveRolloutManager] Configuring {controller.name} for {config.phaseName}");

            // Set base blend weights
            controller.SetBaseWeights(1f - config.neuralWeight, config.neuralWeight);

            // Clear all overrides first
            controller.ClearAllPolicyOverrides();

            // Set policy overrides based on active policies
            bool isPlayer = controller.CompareTag("Player");
            bool isSoldier = controller.CompareTag("Soldier") || controller.CompareTag("Enemy");
            bool isBiped = IsBipedAvatar(controller);
            bool isQuadruped = IsQuadrupedAvatar(controller);

            // Determine if this avatar qualifies for this phase
            bool qualifies = false;
            switch (phase)
            {
                case RolloutPhase.Phase1_PlayerLocomotion:
                    qualifies = isPlayer;
                    break;
                case RolloutPhase.Phase2_PlayerSoldiers:
                    qualifies = isPlayer || isSoldier;
                    break;
                case RolloutPhase.Phase3_AllBipeds:
                    qualifies = isBiped;
                    break;
                case RolloutPhase.Phase4_Quadrupeds:
                    qualifies = isQuadruped;
                    break;
                case RolloutPhase.Phase5_AllCreatures:
                    qualifies = true;
                    break;
            }

            if (!qualifies)
            {
                // Fallback: all procedural for non-qualifying avatars
                controller.SetBaseWeights(1f, 0f);
                return;
            }

            // Set overrides for each active policy
            foreach (var policy in config.activePolicies)
            {
                // Convert AnimationPolicy to PolicyType
                NeuralAnimationController.PolicyType policyType = PolicySelector.PolicyTypeFromAnimationPolicy(policy);
                controller.SetPolicyOverride(policyType, true);
            }

            // Configure LOD if enabled
            if (config.enableLOD)
            {
                controller.SetLODThreshold(config.lodThreshold);
            }
        }

        /// <summary>
        /// Configure all registered HybridAnimationControllers.
        /// Call this when the phase changes to update all active controllers.
        /// </summary>
        public void ConfigureAllControllers()
        {
            var controllers = FindObjectsOfType<HybridAnimationController>();
            foreach (var ctrl in controllers)
            {
                ConfigureHybridController(ctrl);
            }
        }

        // ──────────────────────────────────────────────
        //  Phase Management
        // ──────────────────────────────────────────────

        /// <summary>
        /// Advance to the next rollout phase.
        /// </summary>
        public void AdvancePhase()
        {
            int next = (int)_currentPhase + 1;
            if (next < Enum.GetValues(typeof(RolloutPhase)).Length)
            {
                CurrentPhase = (RolloutPhase)next;
                ConfigureAllControllers();
            }
        }

        /// <summary>
        /// Set the rollout phase and reconfigure all controllers.
        /// </summary>
        public void SetPhase(RolloutPhase phase)
        {
            CurrentPhase = phase;
            ConfigureAllControllers();
        }

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        PhaseConfig GetPhaseConfig(RolloutPhase phase)
        {
            if (_config != null)
                return _config.GetPhaseConfig(phase);

            // Fallback: return default phase config
            return PhaseConfig.Default;
        }

        bool IsBipedAvatar(HybridAnimationController controller)
        {
            var navMeshAgent = controller.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var characterController = controller.GetComponent<CharacterController>();
            // Bipeds typically have CharacterController or are humanoid tagged
            return characterController != null || controller.CompareTag("Player");
        }

        bool IsQuadrupedAvatar(HybridAnimationController controller)
        {
            // Quadrupeds typically have NavMeshAgent and no CharacterController
            var navMeshAgent = controller.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var characterController = controller.GetComponent<CharacterController>();
            return navMeshAgent != null && characterController == null;
        }
    }
}