using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// Manages temporary buffs that modify player stats.
    /// Buffs have a duration and can stack (same type adds duration or value depending on design).
    /// For simplicity, each buff type has a unique ID and stacking refreshes duration.
    /// </summary>
    public class BuffManager : MonoBehaviour
    {
        public static BuffManager Instance { get; private set; }

        public struct ActiveBuff
        {
            public string BuffId;        // e.g., "AttackUp", "DefenseUp"
            public float Value;          // amount to add to stat
            public float EndTime;        // Time.time when buff expires
        }

        private readonly List<ActiveBuff> _activeBuffs = new List<ActiveBuff>();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Returns a copy of the currently active buffs.
        /// </summary>
        public List<ActiveBuff> GetActiveBuffs()
        {
            return new List<ActiveBuff>(_activeBuffs);
        }

        private void Update()
        {
            float now = Time.time;
            // Remove expired buffs
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                if (now >= _activeBuffs[i].EndTime)
                {
                    RemoveBuff(_activeBuffs[i].BuffId, _activeBuffs[i].Value);
                    _activeBuffs[i] = _activeBuffs[_activeBuffs.Count - 1];
                    _activeBuffs.RemoveAt(_activeBuffs.Count - 1);
                }
            }
        }

        /// <summary>
        /// Adds a buff. If same buff already exists, refresh its duration.
        /// </summary>
        /// <param name="buffId">Identifier of the buff (e.g., "AttackUp")</param>
        /// <param name="value">Amount to add to the stat</param>
        /// <param name="duration">Duration in seconds</param>
        public void AddBuff(string buffId, float value, float duration)
        {
            if (duration <= 0f) duration = 5f; // default
            float endTime = Time.time + duration;

            // Check if same buff already active
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].BuffId == buffId)
                {
                    // Refresh duration (optional: could stack values)
                    var buff = _activeBuffs[i];
                    buff.EndTime = endTime;
                    _activeBuffs[i] = buff;
                    Debug.Log($"[BuffManager] Refreshed buff {buffId} (value {value}) for {duration}s");
                    return;
                }
            }

            // Apply buff immediately
            ApplyBuff(buffId, value);
            _activeBuffs.Add(new ActiveBuff { BuffId = buffId, Value = value, EndTime = endTime });
            Debug.Log($"[BuffManager] Added buff {buffId} (+{value}) for {duration}s");
        }

        /// <summary>
        /// Removes a buff (called when duration expires).
        /// </summary>
        private void RemoveBuff(string buffId, float value)
        {
            // Reverse the application
            ReverseBuff(buffId, value);
            Debug.Log($"[BuffManager] Removed buff {buffId} (-{value})");
        }

        // ===== Buff application logic =====
        private void ApplyBuff(string buffId, float value)
        {
            var stats = PlayerStats.Instance ?? FindObjectOfType<PlayerStats>();
            var health = PlayerHealth.Instance ?? FindObjectOfType<PlayerHealth>();
            if (stats == null && (buffId == "AttackUp" || buffId == "DefenseUp" || buffId == "SpeedUp" || buffId == "AlchemyBoost" || buffId == "CookingBoost" || buffId == "CritUp"))
            {
                Debug.LogWarning($"[BuffManager] PlayerStats not found for buff {buffId}");
                return;
            }
            if (health == null && buffId == "HealOverTime")
            {
                Debug.LogWarning("[BuffManager] PlayerHealth not found for HealOverTime buff");
                return;
            }

            switch (buffId)
            {
                case "AttackUp":
                    stats._attackDamageBase += value;
                    break;
                case "DefenseUp":
                    stats._defenseBase += value;
                    break;
                case "SpeedUp":
                    stats._moveSpeedBase += value;
                    break;
                case "Slowness":
                    stats._moveSpeedBase -= value;
                    break;
                case "AlchemyBoost":
                    stats._alchemyTempBonus += value;
                    break;
                case "CookingBoost":
                    stats._cookingTempBonus += value;
                    break;
                case "CritUp":
                    stats._critChanceBase += value;
                    break;
                case "HealOverTime":
                    health.Heal(value);
                    break;
                default:
                    Debug.LogWarning($"[BuffManager] Unknown buffId: {buffId}");
                    break;
            }
        }

        private void ReverseBuff(string buffId, float value)
        {
            var stats = PlayerStats.Instance ?? FindObjectOfType<PlayerStats>();
            var health = PlayerHealth.Instance ?? FindObjectOfType<PlayerHealth>();
            if (stats == null && (buffId == "AttackUp" || buffId == "DefenseUp" || buffId == "SpeedUp" || buffId == "AlchemyBoost" || buffId == "CookingBoost" || buffId == "CritUp"))
            {
                Debug.LogWarning($"[BuffManager] PlayerStats not found for buff {buffId} (reverse)");
                return;
            }
            if (health == null && buffId == "HealOverTime")
            {
                Debug.LogWarning("[BuffManager] PlayerHealth not found for HealOverTime buff (reverse)");
                return;
            }

            switch (buffId)
            {
                case "AttackUp":
                    stats._attackDamageBase -= value;
                    break;
                case "DefenseUp":
                    stats._defenseBase -= value;
                    break;
                case "SpeedUp":
                    stats._moveSpeedBase -= value;
                    break;
                case "Slowness":
                    // Restore speed — ApplyBuff subtracted it
                    stats._moveSpeedBase += value;
                    break;
                case "AlchemyBoost":
                    stats._alchemyTempBonus -= value;
                    break;
                case "CookingBoost":
                    stats._cookingTempBonus -= value;
                    break;
                case "CritUp":
                    stats._critChanceBase -= value;
                    break;
                case "HealOverTime":
                    // No reversal needed for instant heal
                    break;
                default:
                    Debug.LogWarning($"[BuffManager] Unknown buffId for reversal: {buffId}");
                    break;
            }
        }

        // ===== Getter helpers for other systems =====
        public float GetAttackDamageBase()
        {
            var stats = PlayerStats.Instance ?? FindObjectOfType<PlayerStats>();
            return stats != null ? stats._attackDamageBase : 0f;
        }
        public float GetDefenseBase()
        {
            var stats = PlayerStats.Instance ?? FindObjectOfType<PlayerStats>();
            return stats != null ? stats._defenseBase : 0f;
        }
        public float GetMoveSpeedBase()
        {
            var stats = PlayerStats.Instance ?? FindObjectOfType<PlayerStats>();
            return stats != null ? stats._moveSpeedBase : 0f;
        }
        public float GetAlchemyTempBonus()
        {
            var stats = PlayerStats.Instance ?? FindObjectOfType<PlayerStats>();
            return stats != null ? stats._alchemyTempBonus : 0f;
        }
        public float GetCookingTempBonus()
        {
            var stats = PlayerStats.Instance ?? FindObjectOfType<PlayerStats>();
            return stats != null ? stats._cookingTempBonus : 0f;
        }

        public float GetCritChanceBase()
        {
            var stats = PlayerStats.Instance ?? FindObjectOfType<PlayerStats>();
            return stats != null ? stats._critChanceBase : 0f;
        }
    }
}