using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase H: 하루 단위 전쟁 전보(전투 로그) 요약.
    /// AIWarSystem의 전쟁 시작/점령/상실 이벤트를 문자열 로그로 누적하고,
    /// 게임일 경계(TimeManager.OnDayStart → GuardSalaryManager)에 전일 요약을 플레이어에게 전달한다.
    /// TerritoryWarManager의 즉각 배너(발생 순간)와 달리, '어제 전역 전보' 종합 역할.
    /// </summary>
    public static class DailyWarLogSystem
    {
        private static readonly List<string> _log = new List<string>();
        private static readonly int MAX_LOG = 40;

        /// <summary>전쟁/점령 이벤트 로그 누적.</summary>
        public static void Record(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _log.Add(message);
            if (_log.Count > MAX_LOG) _log.RemoveAt(0);
        }

        /// <summary>전쟁 시작 로그.</summary>
        public static void RecordWarStarted(TerritoryId attacker, TerritoryId defender)
        {
            Record($"⚔️ {Name(attacker)} → {Name(defender)} 전쟁 시작");
        }

        /// <summary>점령(완료) 로그.</summary>
        public static void RecordTerritoryConquered(TerritoryId attacker, TerritoryId defender)
        {
            Record($"🏴 {Name(defender)}이(가) {Name(attacker)}에게 점령당함");
        }

        /// <summary>영토 상실 로그.</summary>
        public static void RecordTerritoryLost(TerritoryId territory)
        {
            Record($"🏳️ {Name(territory)} 영토 상실");
        }

        /// <summary>현재 누적 로그 수 (디버그/테스트).</summary>
        public static int PendingCount => _log.Count;

        /// <summary>
        /// 게임일 경계 호출 — 누적된 전쟁 전보를 요약해 비둘기 비행 연출로 전달하고 비운다.
        /// [79-R19] 비둘기 연출: 오전 6시(게임일 경계 = OnDayStart, IsDay = Hour>=6)에 호출되며
        /// PigeonMessenger가 플레이어에게 날아와 도착·디스폰 직후 배너를 표시한다.
        /// 폴백: 실내 씬/플레이어 부재/GLB 부재/예외 → 즉시 배너(전보 유실 방지 계약).
        /// 로그가 비어 있으면 아무것도 안 함.
        /// </summary>
        public static void FlushDayLog()
        {
            if (_log.Count == 0) return;

            string summary = "📜 어제 전역 전보\n" + string.Join("\n", _log);
            _log.Clear();

            try
            {
                // [79-R19] 전보 비둘기 — 실외 씬에서만 스폰, 실패 시 즉시 배너 폴백
                if (!ProjectName.Systems.ConstructionManager.IsIndoorScene())
                {
                    var pigeon = PigeonMessenger.Spawn(summary, () =>
                    {
                        try
                        {
                            WarNotificationUI.ShowNotification(summary, WarNotificationUI.NotificationType.Info);
                        }
                        catch (System.Exception e)
                        {
                            Debug.Log("[DailyWarLog] 어제 전역 전보: " + summary);
                            Debug.LogWarning($"[DailyWarLog] 전보 배너 표시 실패 — 콘솔 로그 대체: {e.Message}");
                        }
                        Debug.Log("[DailyWarLog] 전역 전보: " + summary.Replace("\n", " | "));
                    });
                    if (pigeon != null) return;   // 비둘기 배달 위임 — 폴백 배너 즉시 표시 안 함
                    Debug.Log("[DailyWarLog] 비둘기 스폰 실패 — 즉시 배너 폴백");
                }
                else
                {
                    Debug.Log("[DailyWarLog] 실내 씬 — 비둘기 스폰 생략, 즉시 배너 폴백");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DailyWarLog] 비둘기 스폰 예외 — 즉시 배너 폴백: {e.Message}");
            }

            ShowImmediateBanner(summary);
        }

        /// <summary>폴백 경로 — 비둘기 없이 즉시 배너(기존 FlushDayLog 동작).</summary>
        private static void ShowImmediateBanner(string summary)
        {
            try
            {
                WarNotificationUI.ShowNotification(summary, WarNotificationUI.NotificationType.Info);
            }
            catch (System.Exception e)
            {
                Debug.Log("[DailyWarLog] 어제 전역 전보: " + summary);
                Debug.LogWarning($"[DailyWarLog] 전보 배너 표시 실패 — 콘솔 로그 대체: {e.Message}");
            }

            Debug.Log("[DailyWarLog] 전역 전보: " + summary.Replace("\n", " | "));
        }

        /// <summary>테스트/초기화 — 누적 로그 클리어.</summary>
        public static void ResetAll()
        {
            _log.Clear();
        }

        private static string Name(TerritoryId id)
        {
            try
            {
                return TerritoryDatabase.Instance.GetDefinition(id).territoryName;
            }
            catch (System.Exception)
            {
                return id.ToString();
            }
        }
    }
}