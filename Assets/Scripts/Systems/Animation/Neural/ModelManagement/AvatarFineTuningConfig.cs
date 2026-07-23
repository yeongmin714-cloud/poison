using System;
using UnityEngine;

namespace ProjectName.Systems.Animation.Neural.ModelManagement
{
    /// <summary>
    /// Avatar-specific fine-tuning configuration for Neural Animation policies.
    /// Allows per-avatar style overrides, equipment adaptation, and boss-specific policies.
    /// </summary>
    [CreateAssetMenu(fileName = "AvatarFineTuningConfig", menuName = "Animation/Avatar Fine-Tuning Config")]
    public class AvatarFineTuningConfig : ScriptableObject
    {
        [Header("Player Personalization")]
        [SerializeField] PlayerStyle _playerStyle = PlayerStyle.Balanced;
        [SerializeField, Range(0f, 1f)] float _aggressionBias = 0.5f;
        [SerializeField, Range(0f, 1f)] float _defenseBias = 0.5f;
        [SerializeField, Range(0f, 1f)] float _evasionBias = 0.3f;

        [Header("Equipment Adaptation")]
        [SerializeField] WeaponClass _currentWeapon = WeaponClass.Unarmed;
        [SerializeField] ArmorClass _currentArmor = ArmorClass.Light;
        [SerializeField] float _equipmentWeight = 0f; // kg
        [SerializeField] float _equipmentSpeedModifier = 1f;

        [Header("Boss Overrides")]
        [SerializeField] BossType _bossType = BossType.None;
        [SerializeField] bool _overrideLocomotion;
        [SerializeField] bool _overrideCombat;
        [SerializeField] string _bossModelPath = "";

        // ──────────────────────────────────────────────
        //  Enums
        // ──────────────────────────────────────────────

        public enum PlayerStyle
        {
            Aggressive,
            Defensive,
            Evasive,
            Balanced,
            Stealth
        }

        public enum WeaponClass
        {
            Unarmed,
            Sword,
            Axe,
            Spear,
            Bow,
            Staff,
            Dagger,
            Shield,
            TwoHanded
        }

        public enum ArmorClass
        {
            Unarmored,
            Light,
            Medium,
            Heavy
        }

        public enum BossType
        {
            None,
            Humanoid,
            Beast,
            Dragon,
            Giant,
            Undead
        }

        // ──────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Get style embedding vector based on player style settings.
        /// 8-dim vector matching the style embedding space.
        /// </summary>
        public float[] GetStyleEmbedding()
        {
            float[] embedding = new float[8];

            switch (_playerStyle)
            {
                case PlayerStyle.Aggressive:
                    embedding[0] = 0.7f; // speed
                    embedding[1] = 0.9f; // aggression
                    embedding[2] = 0.4f; // fluidity
                    embedding[3] = 0.7f; // amplitude
                    embedding[4] = 0.3f; // grounding
                    embedding[5] = 0.4f; // symmetry
                    embedding[6] = 0.1f; // head height
                    embedding[7] = 0.6f; // arm swing
                    break;

                case PlayerStyle.Defensive:
                    embedding[0] = 0.3f;
                    embedding[1] = 0.2f;
                    embedding[2] = 0.6f;
                    embedding[3] = 0.3f;
                    embedding[4] = 0.7f;
                    embedding[5] = 0.8f;
                    embedding[6] = -0.1f;
                    embedding[7] = 0.3f;
                    break;

                case PlayerStyle.Evasive:
                    embedding[0] = 0.6f;
                    embedding[1] = 0.1f;
                    embedding[2] = 0.8f;
                    embedding[3] = 0.5f;
                    embedding[4] = 0.2f;
                    embedding[5] = 0.3f;
                    embedding[6] = 0.0f;
                    embedding[7] = 0.2f;
                    break;

                case PlayerStyle.Stealth:
                    embedding[0] = 0.2f;
                    embedding[1] = 0.0f;
                    embedding[2] = 0.7f;
                    embedding[3] = 0.2f;
                    embedding[4] = 0.8f;
                    embedding[5] = 0.9f;
                    embedding[6] = -0.2f;
                    embedding[7] = 0.1f;
                    break;

                case PlayerStyle.Balanced:
                default:
                    embedding[0] = 0.5f;
                    embedding[1] = 0.5f;
                    embedding[2] = 0.5f;
                    embedding[3] = 0.5f;
                    embedding[4] = 0.5f;
                    embedding[5] = 0.5f;
                    embedding[6] = 0.0f;
                    embedding[7] = 0.5f;
                    break;
            }

            // Apply equipment modifiers
            float speedMod = _equipmentSpeedModifier;
            embedding[0] *= speedMod; // speed
            embedding[3] *= (1f + (_equipmentWeight / 50f) * 0.5f); // amplitude increases with weight

            return embedding;
        }

        /// <summary>
        /// Get equipment-adjusted speed modifier.
        /// </summary>
        public float GetSpeedModifier()
        {
            float baseMod = 1f;
            baseMod -= _currentArmor switch
            {
                ArmorClass.Light => 0.05f,
                ArmorClass.Medium => 0.15f,
                ArmorClass.Heavy => 0.3f,
                _ => 0f
            };
            baseMod -= _equipmentWeight * 0.005f;
            return Mathf.Max(0.3f, baseMod);
        }

        /// <summary>
        /// Get weapon-specific combat style tag for policy selection.
        /// </summary>
        public string GetCombatStyleTag()
        {
            return _currentWeapon switch
            {
                WeaponClass.Sword => "melee_medium",
                WeaponClass.Axe => "melee_heavy",
                WeaponClass.Spear => "melee_ranged",
                WeaponClass.Bow => "ranged",
                WeaponClass.Staff => "magic",
                WeaponClass.Dagger => "melee_fast",
                WeaponClass.Shield => "defense",
                WeaponClass.TwoHanded => "melee_slow",
                _ => "unarmed"
            };
        }

        /// <summary>
        /// Whether this is a boss avatar with custom model override.
        /// </summary>
        public bool IsBoss => _bossType != BossType.None;

        /// <summary>
        /// Get boss-specific model path override.
        /// </summary>
        public string GetBossModelPath(NeuralAnimationController.PolicyType policy)
        {
            if (string.IsNullOrEmpty(_bossModelPath))
                return "";

            return policy switch
            {
                NeuralAnimationController.PolicyType.Locomotion when _overrideLocomotion => _bossModelPath + "_locomotion",
                NeuralAnimationController.PolicyType.Combat when _overrideCombat => _bossModelPath + "_combat",
                _ => ""
            };
        }
    }
}