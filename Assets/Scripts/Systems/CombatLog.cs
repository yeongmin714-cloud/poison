using System.Collections.Generic;
using UnityEngine;

// [123차] P3-2 전투로그 폐기 — no-op 스텁.
// 사유: 전투로그 시스템 폐기(사용자 요청 3건) — UI 창(CombatLogUTK.cs)은 삭제 완료,
//       호출부 12파일/22곳 무수정을 위해 본 스텁이 시그니처 전부를 유지한다(119차 미커밋
//       WandererDriver.cs:269, GuardPlaceholder.cs:589~598 보호).
// 계약: AddEntry=빈 몸체, GetRecentEntries=빈 배열, GetLastEntry=null, Clear=빈 몸체, Count=0.
//       링버퍼/상태 저장소는 폐기 — LogType/CombatLogEntry 형은 유지(호출부 컴파일 보장).
// 복원 경로: git 이력(본 파일 이전 리비전) + 이 스텁을 원본 구현으로 교체하는 1파일 수정만으로 복원 가능.
//           (CombatLogUTK UI 창 복원 시에도 git 이력에서 파일을 되살리면 된다.)
namespace ProjectName.Systems
{
    public enum LogType
    {
        Normal,
        Damage,
        Heal,
        Kill,
        Warning
    }

    public struct CombatLogEntry
    {
        public float timestamp;
        public string message;
        public LogType type;

        public CombatLogEntry(float timestamp, string message, LogType type)
        {
            this.timestamp = timestamp;
            this.message = message;
            this.type = type;
        }
    }

    /// <summary>
    /// [123차] 전투 기록 로그 — 폐기된 no-op 스텁. 정적 클래스, MonoBehaviour 아님.
    /// 모든 동작은 무동작(빈 몸체/빈 배열/null/0) — 호출부 12파일 시그니처 호환 유지 목적.
    /// </summary>
    public static class CombatLog
    {
        /// <summary>로그 엔트리 추가 — [123차] 폐기: 무동작 스텁.</summary>
        public static void AddEntry(string message, LogType type = LogType.Normal)
        {
        }

        /// <summary>최근 N개 반환 (기본 20) — [123차] 폐기: 항상 빈 배열.</summary>
        public static List<CombatLogEntry> GetRecentEntries(int count = 20)
        {
            return new List<CombatLogEntry>();
        }

        /// <summary>마지막 1개 반환 — [123차] 폐기: 항상 null.</summary>
        public static CombatLogEntry? GetLastEntry()
        {
            return null;
        }

        /// <summary>전체 로그 삭제 — [123차] 폐기: 무동작 스텁.</summary>
        public static void Clear()
        {
        }

        /// <summary>현재 로그 개수 — [123차] 폐기: 항상 0.</summary>
        public static int Count => 0;
    }
}
