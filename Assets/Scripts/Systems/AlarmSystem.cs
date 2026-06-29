using System.Collections;
using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 35: 자물쇠 따기 경보 시스템.
    /// 실패 시 병사 이동, 3회 연속 실패 시 모든 병사 경계 상태.
    /// </summary>
    public static class AlarmSystem
    {
        /// <summary>
        /// 경보 발생: 해당 위치로 병사를 이동시킵니다.
        /// </summary>
        /// <param name="locationId">TerritoryId.ToString() 형식의 위치 ID</param>
        /// <param name="position">경보 발생 위치 (월드 좌표)</param>
        public static void TriggerAlert(string locationId, Vector3 position)
        {
            Debug.Log($"[AlarmSystem] 🚨 경보 (위치 기반)! locationId={locationId}, position={position}");

            // 위치 기반으로 주변 병사 검색 및 이동
            GameObject[] guards = GameObject.FindGameObjectsWithTag("Guard");
            if (guards == null || guards.Length == 0)
            {
                Debug.LogWarning("[AlarmSystem] 경보를 받을 병사가 없습니다.");
                return;
            }

            // 거리순 정렬 (가장 가까운 병사 2명 이동)
            var sortedGuards = new List<GameObject>(guards);
            sortedGuards.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.transform.position, position);
                float distB = Vector3.Distance(b.transform.position, position);
                return distA.CompareTo(distB);
            });

            int moveCount = Mathf.Min(2, sortedGuards.Count);
            for (int i = 0; i < moveCount; i++)
            {
                var guard = sortedGuards[i];
                var awareness = guard.GetComponent<NPCAwarenessSystem>();
                if (awareness != null)
                {
                    awareness.SetSuspicious(position);
                    awareness.SetSearching();
                }

                var agent = guard.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled)
                {
                    agent.SetDestination(position);
                }
            }
        }

        /// <summary>
        /// 경보 발생: 해당 위치로 병사를 이동시킵니다.
        /// </summary>
        public static void TriggerAlarm(string locationId, LockpickingSystem.LockDifficulty difficulty)
        {
            Debug.Log($"[AlarmSystem] 🚨 경보! 위치={locationId}, 난이도={difficulty}");

            // 위치 기반으로 주변 병사 검색 및 이동
            GameObject[] guards = GameObject.FindGameObjectsWithTag("Guard");
            if (guards == null || guards.Length == 0)
            {
                Debug.LogWarning("[AlarmSystem] 경보를 받을 병사가 없습니다.");
                return;
            }

            // 가장 가까운 병사 2명을 문 앞으로 이동
            Vector3 alarmPosition = FindAlarmPosition(locationId);
            if (alarmPosition == Vector3.zero)
            {
                Debug.LogWarning($"[AlarmSystem] 위치 '{locationId}'를 찾을 수 없습니다.");
                return;
            }

            // 거리순 정렬
            var sortedGuards = new List<GameObject>(guards);
            sortedGuards.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.transform.position, alarmPosition);
                float distB = Vector3.Distance(b.transform.position, alarmPosition);
                return distA.CompareTo(distB);
            });

            // 가장 가까운 병사 2명 이동
            int moveCount = Mathf.Min(2, sortedGuards.Count);
            for (int i = 0; i < moveCount; i++)
            {
                var guard = sortedGuards[i];
                var awareness = guard.GetComponent<NPCAwarenessSystem>();
                if (awareness != null)
                {
                    // 경보 위치로 의심 상태 전환
                    awareness.SetSuspicious(alarmPosition);
                    awareness.SetSearching();
                }

                // GuardPlaceholder가 있다면 메시지 표시
                var placeholder = guard.GetComponent<GuardPlaceholder>();
                if (placeholder != null)
                {
                    Debug.Log($"[AlarmSystem] 병사 '{guard.name}'가 {locationId}로 이동 중!");
                }

                // 실제 이동 (NavMeshAgent가 있다면)
                var agent = guard.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled)
                {
                    agent.SetDestination(alarmPosition);
                }
            }
        }

        /// <summary>
        /// 전역 경계: 3회 연속 실패 시 모든 병사 경계 상태.
        /// </summary>
        public static void TriggerGlobalAlert(string locationId)
        {
            Debug.Log($"[AlarmSystem] 🚨🚨 전역 경계 발동! 위치={locationId}");

            GameObject player = GameObject.FindGameObjectWithTag("Player");

            GameObject[] guards = GameObject.FindGameObjectsWithTag("Guard");
            foreach (var guard in guards)
            {
                var awareness = guard.GetComponent<NPCAwarenessSystem>();
                if (awareness != null)
                {
                    if (player != null)
                    {
                        awareness.SetDetected(player);
                    }
                    else
                    {
                        awareness.SetSearching();
                    }
                }

                // 경계 상태 지속 시간 증가 (일반 경계 30초 → 60초)
                // (별도 처리)
            }

            Debug.Log("[AlarmSystem] 모든 병사가 경계 상태에 돌입했습니다!");
        }

        /// <summary>
        /// locationId로 알람 발생 위치를 찾습니다.
        /// LockedDoor의 위치를 GameObject.Find로 검색.
        /// </summary>
        private static Vector3 FindAlarmPosition(string locationId)
        {
            // 모든 LockedDoor 오브젝트 검색
            LockedDoor[] doors = GameObject.FindObjectsOfType<LockedDoor>(true);
            foreach (var door in doors)
            {
                if (door.LocationId == locationId)
                {
                    return door.transform.position;
                }
            }

            // GameObject 이름으로 검색
            GameObject doorObj = GameObject.Find(locationId);
            if (doorObj != null)
                return doorObj.transform.position;

            return Vector3.zero;
        }
    }
}