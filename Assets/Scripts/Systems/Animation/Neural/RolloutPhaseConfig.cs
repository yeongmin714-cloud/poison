using System;
using UnityEngine;

namespace ProjectName.Systems.Animation.Neural
{
    /// <summary>
    /// Progressive rollout phases for the Hybrid Animation Controller bridge.
    /// Each phase progressively enables Neural policies for more avatar types.
    /// </summary>
    public enum RolloutPhase
    {
        /// <summary>Phase 4.6.1 — Player only, Locomotion policy only (Neural), rest Procedural</summary>
        Phase1_PlayerLocomotion = 0,
        /// <summary>Phase 4.6.2 — Player + Soldiers, Locomotion + Combat (Neural)</summary>
        Phase2_PlayerSoldiers = 1,
        /// <summary>Phase 4.6.3 — All Bipeds, all policies (Neural)</summary>
        Phase3_AllBipeds = 2,
        /// <summary>Phase 4.6.4 — Quadrupeds, Locomotion + React (Neural)</summary>
        Phase4_Quadrupeds = 3,
        /// <summary>Phase 4.6.5 — All Creatures, Full Neural (Procedural fallback only on error)</summary>
        Phase5_AllCreatures = 4
    }

    /// <summary>
    /// Per-phase configuration data for progressive rollout.
    /// Defines which avatar types, policies, and blend weights apply.
    /// </summary>
    [Serializable]
    public struct PhaseConfig
    {
        /// <summary>Display name for this phase.</summary>
        public string phaseName;

        /// <summary>Avatar types that get Neural policies in this phase.</summary>
        public SelectorAvatarType[] avatarTypes;

        /// <summary>Policies that use Neural inference in this phase.</summary>
        public AnimationPolicy[] activePolicies;

        /// <summary>Neural blend weight (0.0 = fully Procedural, 1.0 = fully Neural).</summary>
        [Range(0f, 1f)]
        public float neuralWeight;

        /// <summary>Whether to enable LOD-based neural weight reduction.</summary>
        public bool enableLOD;

        /// <summary>LOD distance threshold in meters (beyond this, neural weight reduces).</summary>
        public float lodThreshold;

        public static PhaseConfig Default => new PhaseConfig
        {
            phaseName = "Phase 4.6.1 - Player Locomotion",
            avatarTypes = new[] { SelectorAvatarType.Biped },
            activePolicies = new[] { AnimationPolicy.Locomotion },
            neuralWeight = 0.3f,
            enableLOD = true,
            lodThreshold = 30f
        };
    }

    /// <summary>
    /// ScriptableObject providing all phase configurations for the progressive rollout.
    /// Create via Assets/Create/Animation/ProgressiveRolloutConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressiveRolloutConfig", menuName = "Animation/Progressive Rollout Config")]
    public class ProgressiveRolloutConfig : ScriptableObject
    {
        [Header("Rollout Phases")]
        [SerializeField] PhaseConfig[] _phases = GetDefaultPhases();

        /// <summary>All phase configurations indexed by RolloutPhase enum.</summary>
        public PhaseConfig[] Phases => _phases;

        /// <summary>
        /// Get the configuration for a specific rollout phase.
        /// </summary>
        public PhaseConfig GetPhaseConfig(RolloutPhase phase)
        {
            int idx = (int)phase;
            if (idx >= 0 && idx < _phases.Length)
                return _phases[idx];
            return PhaseConfig.Default;
        }

        /// <summary>
        /// Returns the default phase configurations for progressive rollout.
        /// </summary>
        static PhaseConfig[] GetDefaultPhases()
        {
            return new PhaseConfig[]
            {
                // Phase 1: Player only, Locomotion Neural
                new PhaseConfig
                {
                    phaseName = "Phase 4.6.1 - Player Locomotion",
                    avatarTypes = new[] { SelectorAvatarType.Biped },
                    activePolicies = new[] { AnimationPolicy.Locomotion },
                    neuralWeight = 0.3f,
                    enableLOD = true,
                    lodThreshold = 30f
                },
                // Phase 2: Player + Soldiers, Locomotion + Combat Neural
                new PhaseConfig
                {
                    phaseName = "Phase 4.6.2 - Player + Soldiers",
                    avatarTypes = new[] { SelectorAvatarType.Biped },
                    activePolicies = new[] { AnimationPolicy.Locomotion, AnimationPolicy.Combat },
                    neuralWeight = 0.5f,
                    enableLOD = true,
                    lodThreshold = 40f
                },
                // Phase 3: All Bipeds, all policies Neural
                new PhaseConfig
                {
                    phaseName = "Phase 4.6.3 - All Bipeds",
                    avatarTypes = new[] { SelectorAvatarType.Biped },
                    activePolicies = new[] {
                        AnimationPolicy.Locomotion, AnimationPolicy.Combat,
                        AnimationPolicy.React, AnimationPolicy.Interact,
                        AnimationPolicy.Run, AnimationPolicy.Crouch
                    },
                    neuralWeight = 0.8f,
                    enableLOD = true,
                    lodThreshold = 50f
                },
                // Phase 4: Quadrupeds, Locomotion + React Neural
                new PhaseConfig
                {
                    phaseName = "Phase 4.6.4 - Quadrupeds",
                    avatarTypes = new[] { SelectorAvatarType.Quadruped },
                    activePolicies = new[] {
                        AnimationPolicy.Locomotion, AnimationPolicy.React,
                        AnimationPolicy.Run, AnimationPolicy.Crouch
                    },
                    neuralWeight = 0.6f,
                    enableLOD = true,
                    lodThreshold = 40f
                },
                // Phase 5: All Creatures, Full Neural
                new PhaseConfig
                {
                    phaseName = "Phase 4.6.5 - All Creatures",
                    avatarTypes = new[] {
                        SelectorAvatarType.Biped, SelectorAvatarType.Quadruped,
                        SelectorAvatarType.Flying, SelectorAvatarType.Swimming,
                        SelectorAvatarType.LargeMonster
                    },
                    activePolicies = new[] {
                        AnimationPolicy.Locomotion, AnimationPolicy.Combat,
                        AnimationPolicy.React, AnimationPolicy.Interact,
                        AnimationPolicy.Fly, AnimationPolicy.Swim,
                        AnimationPolicy.Mount, AnimationPolicy.Climb,
                        AnimationPolicy.Run, AnimationPolicy.Crouch
                    },
                    neuralWeight = 1.0f,
                    enableLOD = true,
                    lodThreshold = 60f
                }
            };
        }
    }
}