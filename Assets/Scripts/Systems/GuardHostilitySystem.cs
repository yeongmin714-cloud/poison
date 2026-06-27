using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [5.3.6] 병사 적대 전환 시스템.
    /// GuardLoyaltySystem + GuardPlaceholder 연동.
    /// 호감도에 따라 적대 전환, 선공, 경보 발령 처리.
    /// </summary>
    public class GuardHostilitySystem : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float _checkInterval = 2f; // 체크 간격 (초)
        [SerializeField] private float _attackRange = 8f;    // 선공 거리

        // ===== 상수 (호감도 기준) =====
        public const float HOSTILE_THRESHOLD = 0f;       // 호감도 < 0 → 적대 전환
        public const float PREEMPTIVE_THRESHOLD = -30f;  // 호감도 < -30 → 선공
        public const float ALARM_THRESHOLD = -50f;       // 호감도 < -50 → 경보 발령

        private float _timer = 0f;

        // 적대 상태 병사 목록 (추적용)
        private readonly Dictionary<GuardPlaceholder, HostilityState> _hostileGuards
            = new Dictionary<GuardPlaceholder, HostilityState>();

        public static GuardHostilitySystem Instance { get; private set; }

        private enum HostilityState
        {
            Friendly,    // 호감도 >= 0
            Hostile,     // 0 > 호감도 >= -30
            Aggressive,  // -30 > 호감도 >= -50
            Alarm        // 호감도 < -50
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _checkInterval) return;
            _timer = 0f;

            ProcessHostility();
        }

        /// <summary>
        /// 주기적으로 모든 GuardPlaceholder의 호감도를 확인하고 적대 상태 전환
        /// </summary>
        private void ProcessHostility()
        {
            var guards = Object.FindObjectsByType<GuardPlaceholder>();
            var player = GameObject.FindGameObjectWithTag("Player");

            foreach (var guard in guards)
            {
                if (guard == null || !guard.IsAlive) continue;

                float loyalty = guard.Loyalty;
                HostilityState previousState = GetCurrentHostilityState(guard);
                HostilityState newState = CalculateHostilityState(loyalty);

                _hostileGuards[guard] = newState;

                // 상태 변화 로깅
                if (previousState != newState)
                {
                    Debug.Log($"[GuardHostility] {guard.GuardName} 호감도={loyalty:F0} → {newState} (이전: {previousState})");
                }

                switch (newState)
                {
                    case HostilityState.Friendly:
                        // 아무 처리 없음
                        break;

                    case HostilityState.Hostile:
                        // 적대 전환 (전투 상태 활성화)
                        if (previousState != HostilityState.Hostile)
                        {
                            ConvertToHostile(guard);
                        }
                        break;

                    case HostilityState.Aggressive:
                        // 적대 전환 + 선공
                        if (previousState != HostilityState.Aggressive)
                        {
                            ConvertToHostile(guard);
                        }
                        // 플레이어 근접 시 즉시 공격
                        if (player != null && IsPlayerNearby(guard, player.transform))
                        {
                            InitiateAttack(guard, player);
                        }
                        break;

                    case HostilityState.Alarm:
                        // 적대 전환 + 선공 + 경보 발령
                        if (previousState != HostilityState.Alarm)
                        {
                            ConvertToHostile(guard);
                            TriggerAlert(guard);
                        }
                        // 플레이어 근접 시 즉시 공격
                        if (player != null && IsPlayerNearby(guard, player.transform))
                        {
                            InitiateAttack(guard, player);
                        }
                        break;
                }
            }

            // 사라진 병사 정리
            CleanupDeadGuards();
        }

        // ===== 상태 전환 =====

        /// <summary>병사를 적대 상태로 전환</summary>
        private void ConvertToHostile(GuardPlaceholder guard)
        {
            guard.SetInCombat(true);
            Debug.Log($"[GuardHostility] ⚔️ {guard.GuardName} 적대 전환! (호감도: {guard.Loyalty:F0})");
        }

        /// <summary>선공 — 플레이어 공격</summary>
        private void InitiateAttack(GuardPlaceholder guard, GameObject player)
        {
            if (guard == null || player == null) return;

            // GuardCombatAI를 통해 공격 명령
            guard.SetCommandTarget(player.transform.position, true);
            guard.SetInCombat(true);

            Debug.Log($"[GuardHostility] 🗡️ {guard.GuardName} 플레이어 선공! (호감도: {guard.Loyalty:F0})");
        }

        /// <summary>경보 발령 — 주변 병사/영주 알림</summary>
        private void TriggerAlert(GuardPlaceholder guard)
        {
            // 해당 병사의 국가/영지 정보 찾기
            TerritoryId? territoryId = FindGuardTerritory(guard);
            if (territoryId.HasValue)
            {
                AlarmSystem.TriggerAlert(territoryId.Value, guard.transform.position);
                Debug.Log($"[GuardHostility] 🚨 {guard.GuardName} 경보 발령! 영지: {territoryId.Value}");
            }
            else
            {
                // 영지 정보 없으면 기본 위치로 경보
                Debug.Log($"[GuardHostility] ⚠️ {guard.GuardName} 경보 (영지 정보 없음)");
            }
        }

        // ===== 헬퍼 =====

        private bool IsPlayerNearby(GuardPlaceholder guard, Transform playerTransform)
        {
            if (guard == null || playerTransform == null) return false;
            float dist = Vector3.Distance(guard.transform.position, playerTransform.position);
            return dist <= _attackRange;
        }

        private HostilityState CalculateHostilityState(float loyalty)
        {
            if (loyalty < ALARM_THRESHOLD) return HostilityState.Alarm;
            if (loyalty < PREEMPTIVE_THRESHOLD) return HostilityState.Aggressive;
            if (loyalty < HOSTILE_THRESHOLD) return HostilityState.Hostile;
            return HostilityState.Friendly;
        }

        private HostilityState GetCurrentHostilityState(GuardPlaceholder guard)
        {
            if (_hostileGuards.TryGetValue(guard, out var state))
                return state;
            return HostilityState.Friendly;
        }

        private TerritoryId? FindGuardTerritory(GuardPlaceholder guard)
        {
            if (GuardManager.Instance == null) return null;

            var db = TerritoryDatabase.Instance;
            if (db == null) return null;

            foreach (var def in db.GetAllDefinitions())
            {
                var state = db.GetState(def.id);
                if (state == null) continue;

                var guardsInTerritory = GuardManager.Instance.GetGuardsInTerritory(def.id);
                if (guardsInTerritory.Contains(guard))
                {
                    return def.id;
                }
            }

            return null;
        }

        private void CleanupDeadGuards()
        {
            var deadKeys = new List<GuardPlaceholder>();
            foreach (var kvp in _hostileGuards)
            {
                if (kvp.Key == null || !kvp.Key.IsAlive)
                {
                    deadKeys.Add(kvp.Key);
                }
            }
            foreach (var key in deadKeys)
            {
                _hostileGuards.Remove(key);
            }
        }

        // ===== 퍼블릭 API (외부 호출용) =====

        /// <summary>특정 병사의 적대 상태 확인</summary>
        public bool IsHostile(GuardPlaceholder guard)
        {
            if (guard == null) return false;
            var state = GetCurrentHostilityState(guard);
            return state != HostilityState.Friendly;
        }

        /// <summary>특정 병사가 선공 상태인지 확인</summary>
        public bool IsAggressive(GuardPlaceholder guard)
        {
            if (guard == null) return false;
            var state = GetCurrentHostilityState(guard);
            return state == HostilityState.Aggressive || state == HostilityState.Alarm;
        }

        /// <summary>특정 병사가 경보를 발령했는지 확인</summary>
        public bool IsAlarmTriggered(GuardPlaceholder guard)
        {
            if (guard == null) return false;
            return GetCurrentHostilityState(guard) == HostilityState.Alarm;
        }

        /// <summary>현재 적대 병사 수</summary>
        public int HostileGuardCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _hostileGuards)
                {
                    if (kvp.Value != HostilityState.Friendly) count++;
                }
                return count;
            }
        }

        /// <summary>강제 적대 상태 설정 (테스트/디버그)</summary>
        public void ForceSetHostility(GuardPlaceholder guard, float loyalty)
        {
            if (guard == null) return;
            guard.Loyalty = loyalty;
            _hostileGuards[guard] = CalculateHostilityState(loyalty);
        }
    }
}