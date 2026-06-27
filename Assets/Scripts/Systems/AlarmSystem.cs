using System;
using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-09: 3단계 경보 시스템 — 문지기 공격 시 모든 병사에게 경보를 전파합니다.
    /// 
    /// AlarmState:
    ///   Peaceful — 평화 상태, 모든 병사 일반 근무
    ///   Alert — 1단계: 문지기 공격 감지, 근처 병사 전투 준비
    ///   Battle — 3단계: 사이렌 울림 → 모든 병사 배치
    /// 
    /// 사용법:
    ///   AlarmSystem.TriggerAlert(territoryId, attackPosition);
    ///   AlarmSystem.RestorePeace(territoryId);
    /// </summary>
    public static class AlarmSystem
    {
        /// <summary>
        /// 경보 상태 열거형
        /// </summary>
        public enum AlarmState
        {
            /// <summary>평화 — 일반 근무</summary>
            Peaceful,
            /// <summary>경계 — 문지기 공격 감지, 근처 병사 전투 준비</summary>
            Alert,
            /// <summary>전투 — 사이렌 울림, 모든 병사 배치 완료</summary>
            Battle
        }

        // ===== 이벤트 =====

        /// <summary>경보가 발령되었을 때 발생 (territoryId, alarmState)</summary>
        public static event Action<TerritoryId, AlarmState> OnAlarmTriggered;

        /// <summary>평화가 복구되었을 때 발생 (territoryId)</summary>
        public static event Action<TerritoryId> OnPeaceRestored;

        /// <summary>경보 단계가 변경되었을 때 발생 (territoryId, oldState, newState)</summary>
        public static event Action<TerritoryId, AlarmState, AlarmState> OnAlarmStateChanged;

        // ===== 상수 =====

        /// <summary>Alert → Battle 전환 시간 (초)</summary>
        public const float ALARM_TO_BATTLE_DELAY = 3f;

        /// <summary>전투 후 자동 Peace 전환 시간 (초)</summary>
        public const float BATTLE_TO_PEACE_TIMEOUT = 60f;

        /// <summary>Alert 단계에서 근처 병사 활성화 범위</summary>
        public const float ALERT_RADIUS = GuardCombatAI.COMBAT_DETECT_RANGE * 2f;

        // ===== 내부 상태 =====

        private static readonly Dictionary<TerritoryId, AlarmState> _alarmStates = new Dictionary<TerritoryId, AlarmState>();
        private static readonly Dictionary<TerritoryId, float> _alarmTimers = new Dictionary<TerritoryId, float>();
        private static readonly Dictionary<TerritoryId, Vector3> _alertPositions = new Dictionary<TerritoryId, Vector3>();

        /// <summary>
        /// 현재 영지의 경보 상태 반환
        /// </summary>
        public static AlarmState GetState(TerritoryId territoryId)
        {
            if (_alarmStates.TryGetValue(territoryId, out var state))
                return state;
            return AlarmState.Peaceful;
        }

        /// <summary>
        /// 경보 발령 (Alert 단계).
        /// 문지기가 공격당했을 때 호출합니다.
        /// 근처 모든 GuardCombatAI를 활성화하고, ALARM_TO_BATTLE_DELAY 초 후 Battle로 전환됩니다.
        /// </summary>
        /// <param name="territoryId">공격받은 영지 ID</param>
        /// <param name="attackPosition">공격 발생 위치</param>
        public static void TriggerAlert(TerritoryId territoryId, Vector3 attackPosition)
        {
            AlarmState previousState = GetState(territoryId);
            if (previousState == AlarmState.Battle)
                return; // 이미 최고 경보

            _alarmStates[territoryId] = AlarmState.Alert;
            _alertPositions[territoryId] = attackPosition;
            _alarmTimers[territoryId] = 0f;

            // 1단계: 근처 모든 병사 전투 활성화
            ActivateNearbyGuards(territoryId, attackPosition);

            Debug.Log($"[AlarmSystem] ⚠️ 경보 발령! 영지:{territoryId} 위치:{attackPosition}");

            OnAlarmTriggered?.Invoke(territoryId, AlarmState.Alert);
            OnAlarmStateChanged?.Invoke(territoryId, previousState, AlarmState.Alert);
        }

        /// <summary>
        /// 매 프레임 호출하여 경보 단계 진행 및 타임아웃을 처리합니다.
        /// </summary>
        public static void UpdateAlarm(TerritoryId territoryId, float deltaTime)
        {
            AlarmState currentState = GetState(territoryId);
            if (currentState == AlarmState.Peaceful)
                return;

            if (!_alarmTimers.ContainsKey(territoryId))
                _alarmTimers[territoryId] = 0f;

            _alarmTimers[territoryId] += deltaTime;

            switch (currentState)
            {
                case AlarmState.Alert:
                    // ALARM_TO_BATTLE_DELAY 후 Battle로 전환
                    if (_alarmTimers[territoryId] >= ALARM_TO_BATTLE_DELAY)
                    {
                        EscalateToBattle(territoryId);
                    }
                    break;

                case AlarmState.Battle:
                    // BATTLE_TO_PEACE_TIMEOUT 후 자동 Peace
                    if (_alarmTimers[territoryId] >= BATTLE_TO_PEACE_TIMEOUT)
                    {
                        RestorePeace(territoryId);
                    }
                    break;
            }
        }

        /// <summary>
        /// Alert → Battle 단계로 전환 (사이렌 울림, 모든 병사 배치)
        /// </summary>
        private static void EscalateToBattle(TerritoryId territoryId)
        {
            AlarmState previousState = GetState(territoryId);
            _alarmStates[territoryId] = AlarmState.Battle;

            // 2단계: 사이렌 — 해당 영지의 모든 GateGuardPlaceholder 활성화
            ActivateAllTerritoryGuards(territoryId);

            Debug.Log($"[AlarmSystem] 🚨 사이렌! 영지:{territoryId} — 모든 병사 배치 완료!");

            OnAlarmTriggered?.Invoke(territoryId, AlarmState.Battle);
            OnAlarmStateChanged?.Invoke(territoryId, previousState, AlarmState.Battle);
        }

        /// <summary>
        /// 평화 상태 복구. 전투 종료 또는 타임아웃 후 호출.
        /// </summary>
        public static void RestorePeace(TerritoryId territoryId)
        {
            AlarmState previousState = GetState(territoryId);
            if (previousState == AlarmState.Peaceful)
                return;

            _alarmStates[territoryId] = AlarmState.Peaceful;
            _alarmTimers.Remove(territoryId);
            _alertPositions.Remove(territoryId);

            // 모든 병사 전투 해제 (GuardCombatAI.RecallAll 등)
            DeactivateAllGuards();

            Debug.Log($"[AlarmSystem] ✅ 평화 복구! 영지:{territoryId}");

            OnPeaceRestored?.Invoke(territoryId);
            OnAlarmStateChanged?.Invoke(territoryId, previousState, AlarmState.Peaceful);
        }

        /// <summary>
        /// 경보 상태 강제 설정 (테스트/디버그용)
        /// </summary>
        public static void ForceState(TerritoryId territoryId, AlarmState state)
        {
            AlarmState previousState = GetState(territoryId);
            _alarmStates[territoryId] = state;

            if (state == AlarmState.Peaceful)
            {
                _alarmTimers.Remove(territoryId);
                _alertPositions.Remove(territoryId);
            }
            else if (state == AlarmState.Alert)
            {
                if (!_alarmTimers.ContainsKey(territoryId))
                    _alarmTimers[territoryId] = 0f;
            }
            else if (state == AlarmState.Battle)
            {
                _alarmTimers[territoryId] = ALARM_TO_BATTLE_DELAY; // 즉시 Battle
                ActivateAllTerritoryGuards(territoryId);
            }

            OnAlarmTriggered?.Invoke(territoryId, state);
            OnAlarmStateChanged?.Invoke(territoryId, previousState, state);
        }

        /// <summary>
        /// 경보 발생 위치 반환 (없으면 null)
        /// </summary>
        public static Vector3? GetAlertPosition(TerritoryId territoryId)
        {
            if (_alertPositions.TryGetValue(territoryId, out var pos))
                return pos;
            return null;
        }

        /// <summary>
        /// 모든 영지의 경보 상태 초기화 (테스트용)
        /// </summary>
        public static void ResetAll()
        {
            _alarmStates.Clear();
            _alarmTimers.Clear();
            _alertPositions.Clear();
        }

        // ===== 내부 헬퍼 =====

        /// <summary>
        /// 공격 위치 근처의 병사들(GuardPlaceholder)을 전투 상태로 전환
        /// </summary>
        private static void ActivateNearbyGuards(TerritoryId territoryId, Vector3 attackPosition)
        {
            var guards = UnityEngine.Object.FindObjectsByType<GuardPlaceholder>();
            foreach (var guard in guards)
            {
                if (guard == null || !guard.IsAlive) continue;

                float dist = Vector3.Distance(guard.transform.position, attackPosition);
                if (dist <= ALERT_RADIUS)
                {
                    guard.SetInCombat(true);
                    Debug.Log($"[AlarmSystem] ⚔️ 병사 {guard.GuardName} 전투 활성화 (거리:{dist:F1})");
                }
            }
        }

        /// <summary>
        /// 영지 소속 모든 문지기 및 병사 전면 배치 (Battle 단계)
        /// </summary>
        private static void ActivateAllTerritoryGuards(TerritoryId territoryId)
        {
            var gateGuards = UnityEngine.Object.FindObjectsByType<GateGuardPlaceholder>();
            foreach (var gate in gateGuards)
            {
                if (gate.Nation != territoryId.nation || gate.TerritoryIndex != territoryId.index)
                    continue;

                foreach (var guard in gate.SpawnedGuards)
                {
                    if (guard != null && guard.IsAlive)
                    {
                        guard.SetInCombat(true);
                    }
                }
            }

            // 전체 영지의 모든 GuardPlaceholder 전투 활성화
            var allGuards = UnityEngine.Object.FindObjectsByType<GuardPlaceholder>();
            foreach (var guard in allGuards)
            {
                if (guard != null && guard.IsAlive)
                {
                    guard.SetInCombat(true);
                }
            }
        }

        /// <summary>
        /// 모든 병사 전투 해제
        /// </summary>
        private static void DeactivateAllGuards()
        {
            var guards = UnityEngine.Object.FindObjectsByType<GuardPlaceholder>();
            foreach (var guard in guards)
            {
                if (guard != null)
                {
                    guard.SetInCombat(false);
                    guard.ClearCommand();
                }
            }
        }
    }
}