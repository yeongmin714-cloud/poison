using System;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

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

        /// <summary>
        /// Represents a single active buff on the player.
        /// </summary>
        public struct ActiveBuff
        {
            /// <summary>Identifier of the buff (e.g., "AttackUp", "DefenseUp")</summary>
            public string BuffId { get; }

            /// <summary>Amount to add to (or subtract from) the stat</summary>
            public float Value { get; }

            /// <summary>Time.time when this buff expires</summary>
            public float EndTime { get; }

            public ActiveBuff(string buffId, float value, float endTime)
            {
                BuffId = buffId ?? throw new ArgumentNullException(nameof(buffId));
                Value = value;
                EndTime = endTime;
            }
        }

        // ── Constants ──────────────────────────────────────────────────────
        private const float DefaultDuration = 5f;

        // ── State ───────────────────────────────────────────────────────────
        private readonly List<ActiveBuff> _activeBuffs = new List<ActiveBuff>();

        // Cached component references to avoid repeated FindObjectOfType calls.
        private PlayerStats _stats;
        private PlayerHealth _health;

        // ── Lifecycle ───────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CacheReferences();
        }

        /// <summary>
        /// Caches PlayerStats and PlayerHealth references.
        /// Called on Awake and any time a null reference is detected.
        /// </summary>
        private void CacheReferences()
        {
            _stats = PlayerStats.Instance != null
                ? PlayerStats.Instance
                : FindAnyObjectByType<PlayerStats>();

            _health = PlayerHealth.Instance != null
                ? PlayerHealth.Instance
                : FindAnyObjectByType<PlayerHealth>();
        }

        /// <summary>
        /// Returns a snapshot (copy) of the currently active buffs.
        /// </summary>
        public IReadOnlyList<ActiveBuff> GetActiveBuffs()
        {
            return new List<ActiveBuff>(_activeBuffs);
        }

        // ── Per-frame expiry ────────────────────────────────────────────────
        private void Update()
        {
            float now = Time.time;

            // Iterate backwards so swap-remove does not skip elements.
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                if (now >= _activeBuffs[i].EndTime)
                {
                    // Reverse stat modification before removing.
                    ReverseBuff(_activeBuffs[i].BuffId, _activeBuffs[i].Value);
                    // Swap-remove: copy last element to current position, then trim.
                    int lastIndex = _activeBuffs.Count - 1;
                    _activeBuffs[i] = _activeBuffs[lastIndex];
                    _activeBuffs.RemoveAt(lastIndex);
                }
            }
        }

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>
        /// Adds a buff. If the same buff already exists, refresh its duration
        /// (value does NOT stack — only duration refreshes).
        /// </summary>
        /// <param name="buffId">Identifier of the buff (e.g., "AttackUp", "Slowness")</param>
        /// <param name="value">Amount to add to (or subtract from) the stat</param>
        /// <param name="duration">Duration in seconds. If zero or negative, uses <see cref="DefaultDuration"/>.</param>
        public void AddBuff(string buffId, float value, float duration)
        {
            if (duration <= 0f) duration = DefaultDuration;
            float endTime = Time.time + duration;

            // Refresh if already active (duration-only refresh; value unchanged).
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].BuffId == buffId)
                {
                    var newBuff = new ActiveBuff(buffId, _activeBuffs[i].Value, endTime);
                    _activeBuffs[i] = newBuff;
                    Debug.Log($"[BuffManager] Refreshed buff {buffId} (value {_activeBuffs[i].Value}) for {duration}s");
                    return;
                }
            }

            // Apply immediately and track.
            ApplyBuff(buffId, value);
            _activeBuffs.Add(new ActiveBuff(buffId, value, endTime));
            Debug.Log($"[BuffManager] Added buff {buffId} (+{value}) for {duration}s");
        }

        // ── Internal apply / reverse ────────────────────────────────────────

        /// <summary>
        /// Returns true when <paramref name="buffId"/> requires <see cref="PlayerStats"/>.
        /// </summary>
        private static bool RequiresStats(string buffId)
        {
            return buffId switch
            {
                "AttackUp" or "DefenseUp" or "SpeedUp" or "Slowness"
                    or "AlchemyBoost" or "CookingBoost" or "CritUp"
                    or "StealthInvisibility" => true,
                _ => false,
            };
        }

        private void ApplyBuff(string buffId, float value)
        {
            // Lazily re-cache if references were lost (e.g. scene reload).
            if (_stats == null) CacheReferences();
            if (_health == null) CacheReferences();

            bool usesStats = RequiresStats(buffId);

            if (usesStats && _stats == null)
            {
                Debug.LogWarning($"[BuffManager] PlayerStats not found for buff {buffId}");
                return;
            }

            if (buffId == "HealOverTime" && _health == null)
            {
                Debug.LogWarning("[BuffManager] PlayerHealth not found for HealOverTime buff");
                return;
            }

            switch (buffId)
            {
                case "AttackUp":
                    _stats.AttackDamageBase += value;
                    break;
                case "DefenseUp":
                    _stats.DefenseBase += value;
                    break;
                case "SpeedUp":
                    _stats.MoveSpeedBase += value;
                    break;
                case "Slowness":
                    _stats.MoveSpeedBase -= value;
                    break;
                case "AlchemyBoost":
                    _stats.AlchemyTempBonus += value;
                    break;
                case "CookingBoost":
                    _stats.CookingTempBonus += value;
                    break;
                case "CritUp":
                    _stats.CritChanceBase += value;
                    break;
                case "HealOverTime":
                    _health.Heal(value);
                    break;
                case "StealthInvisibility":
                    // StealthSystem에서 직접 사용하는 플래그 버프 (Stat 변경 불필요)
                    Debug.Log("[BuffManager] StealthInvisibility 버프 활성화 — 반투명 + 발소음 제로");
                    break;
                case "Bleeding":
                    // 암살 시 보스/영주에게 적용되는 출혈 DOT (시각/효과는 StealthAssassination에서 처리)
                    Debug.Log($"[BuffManager] 🩸 Bleeding 버프 활성화 — 초당 {value} 데미지, {_bleedDurationCache}s 지속");
                    break;
                default:
                    Debug.LogWarning($"[BuffManager] Unknown buffId: {buffId}");
                    break;
            }
        }

        // Bleeding 지속시간 캐시 (ApplyBuff에서 사용)
        private float _bleedDurationCache = 5f;

        private void ReverseBuff(string buffId, float value)
        {
            // Lazily re-cache if references were lost.
            if (_stats == null) CacheReferences();
            if (_health == null) CacheReferences();

            bool usesStats = RequiresStats(buffId);

            if (usesStats && _stats == null)
            {
                Debug.LogWarning($"[BuffManager] PlayerStats not found for buff {buffId} (reverse)");
                return;
            }

            if (buffId == "HealOverTime" && _health == null)
            {
                Debug.LogWarning("[BuffManager] PlayerHealth not found for HealOverTime buff (reverse)");
                return;
            }

            switch (buffId)
            {
                case "AttackUp":
                    _stats.AttackDamageBase -= value;
                    break;
                case "DefenseUp":
                    _stats.DefenseBase -= value;
                    break;
                case "SpeedUp":
                    _stats.MoveSpeedBase -= value;
                    break;
                case "Slowness":
                    // Restore speed — ApplyBuff subtracted it
                    _stats.MoveSpeedBase += value;
                    break;
                case "AlchemyBoost":
                    _stats.AlchemyTempBonus -= value;
                    break;
                case "CookingBoost":
                    _stats.CookingTempBonus -= value;
                    break;
                case "CritUp":
                    _stats.CritChanceBase -= value;
                    break;
                case "HealOverTime":
                    // No reversal needed for instant heal
                    break;
                case "StealthInvisibility":
                    Debug.Log("[BuffManager] StealthInvisibility 버프 만료");
                    break;
                case "Bleeding":
                    Debug.Log("[BuffManager] Bleeding 출혈 버프 만료");
                    break;
                default:
                    Debug.LogWarning($"[BuffManager] Unknown buffId for reversal: {buffId}");
                    break;
            }
        }

        // ===== Getter helpers for other systems =====
        // These proxy to PlayerStats for convenience. They use the cached
        // reference and lazily re-cache if needed.

        public float GetAttackDamageBase()
        {
            if (_stats == null) CacheReferences();
            return _stats != null ? _stats.AttackDamageBase : 0f;
        }

        public float GetDefenseBase()
        {
            if (_stats == null) CacheReferences();
            return _stats != null ? _stats.DefenseBase : 0f;
        }

        public float GetMoveSpeedBase()
        {
            if (_stats == null) CacheReferences();
            return _stats != null ? _stats.MoveSpeedBase : 0f;
        }

        public float GetAlchemyTempBonus()
        {
            if (_stats == null) CacheReferences();
            return _stats != null ? _stats.AlchemyTempBonus : 0f;
        }

        public float GetCookingTempBonus()
        {
            if (_stats == null) CacheReferences();
            return _stats != null ? _stats.CookingTempBonus : 0f;
        }

        public float GetCritChanceBase()
        {
            if (_stats == null) CacheReferences();
            return _stats != null ? _stats.CritChanceBase : 0f;
        }
    }
}
