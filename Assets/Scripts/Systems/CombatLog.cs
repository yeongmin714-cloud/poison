using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// ⏱️ 전투 기록 로그 — 정적 클래스, MonoBehaviour 아님.
    /// 최대 100개의 CombatLogEntry를 유지하며, AddEntry/GetRecentEntries 등으로 접근.
    /// </summary>
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

    public static class CombatLog
    {
        private static List<CombatLogEntry> _entries = new List<CombatLogEntry>(100);
        private const int MAX_ENTRIES = 100;

        /// <summary>로그 엔트리 추가 (타임스탬프 자동)</summary>
        public static void AddEntry(string message, LogType type = LogType.Normal)
        {
            _entries.Add(new CombatLogEntry(Time.time, message, type));
            if (_entries.Count > MAX_ENTRIES)
                _entries.RemoveAt(0);
        }

        /// <summary>최근 N개 반환 (기본 20)</summary>
        public static List<CombatLogEntry> GetRecentEntries(int count = 20)
        {
            if (_entries.Count <= count)
                return new List<CombatLogEntry>(_entries);
            return _entries.GetRange(_entries.Count - count, count);
        }

        /// <summary>마지막 1개 반환</summary>
        public static CombatLogEntry? GetLastEntry()
        {
            if (_entries.Count == 0) return null;
            return _entries[_entries.Count - 1];
        }

        /// <summary>전체 로그 삭제</summary>
        public static void Clear()
        {
            _entries.Clear();
        }

        /// <summary>현재 로그 개수</summary>
        public static int Count => _entries.Count;
    }
}