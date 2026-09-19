using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// C-O2-01: 금화 감사 원장 정적 집계기 (OpenMMO ECONOMY.md 벤치마크).
    /// - 카테고리(source 태그)별 수입/지출 집계
    /// - 총수입/총지출/순유입
    /// - 시간당 순유입(Net Inflow Rate) — 플레이 1시간 순유입 밴드 튜닝(C-O2-04)용 지표
    /// EconomyAuditSystem(MonoBehaviour)이 PlayerStats.GoldChanged를 구독하여 기록한다.
    /// </summary>
    public static class EconomyAuditLedger
    {
        // 카테고리별 (수입, 지출) 누계
        private static readonly Dictionary<string, (int income, int outflow)> _byCategory
            = new Dictionary<string, (int income, int outflow)>();

        private static long _totalIncome;
        private static long _totalOutflow;

        /// <summary>수입 기록 (amount <= 0은 무시)</summary>
        public static void RecordIncome(string source, int amount)
        {
            if (string.IsNullOrEmpty(source) || amount <= 0) return;
            if (_byCategory.TryGetValue(source, out var slot))
                _byCategory[source] = (slot.income + amount, slot.outflow);
            else
                _byCategory[source] = (amount, 0);
            _totalIncome += amount;
        }

        /// <summary>지출 기록 (amount <= 0은 무시)</summary>
        public static void RecordOutflow(string source, int amount)
        {
            if (string.IsNullOrEmpty(source) || amount <= 0) return;
            if (_byCategory.TryGetValue(source, out var slot))
                _byCategory[source] = (slot.income, slot.outflow + amount);
            else
                _byCategory[source] = (0, amount);
            _totalOutflow += amount;
        }

        /// <summary>원장 전체 초기화 (테스트/세션 리셋용)</summary>
        public static void ResetAudit()
        {
            _byCategory.Clear();
            _totalIncome = 0;
            _totalOutflow = 0;
        }

        /// <summary>카테고리 수입 누계 (없으면 0)</summary>
        public static int GetIncome(string source)
            => _byCategory.TryGetValue(source, out var slot) ? slot.income : 0;

        /// <summary>카테고리 지출 누계 (없으면 0)</summary>
        public static int GetOutflow(string source)
            => _byCategory.TryGetValue(source, out var slot) ? slot.outflow : 0;

        /// <summary>전체 수입 누계</summary>
        public static int GetTotalIncome() => (int)_totalIncome;

        /// <summary>전체 지출 누계</summary>
        public static int GetTotalOutflow() => (int)_totalOutflow;

        /// <summary>순유입 = 총수입 - 총지출</summary>
        public static int GetNet() => (int)(_totalIncome - _totalOutflow);

        /// <summary>
        /// 시간당 순유입 = (총수입 - 총지출) / 분 * 60. 분 <= 0이면 0.
        /// |rate|가 플레이 1시간 순유입 밴드 내인지 확인하는 튜닝 지표(C-O2-04).
        /// </summary>
        public static float GetHourlyNetRate(float sessionMinutes)
        {
            if (sessionMinutes <= 0f) return 0f;
            return GetNet() / sessionMinutes * 60f;
        }

        /// <summary>
        /// 감사 리포트 문자열 — 카테고리별 줄 + 총계 + 시간당 순유입 (Debug/UI 출력용)
        /// </summary>
        public static string GetReport(float sessionMinutes)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[경제 감사 리포트] (EconomyAuditLedger)");
            sb.AppendLine($"  세션 시간: {sessionMinutes:F1}분");

            // 카테고리 이름 정렬로 결정적(deterministic) 출력 보장
            var keys = new List<string>(_byCategory.Keys);
            keys.Sort();
            foreach (var key in keys)
            {
                var slot = _byCategory[key];
                sb.AppendLine($"  {key,-22}: 수입 {slot.income,8} G / 지출 {slot.outflow,8} G");
            }

            sb.AppendLine("  ── 총계 ──");
            sb.AppendLine($"  총수입: {GetTotalIncome()} G / 총지출: {GetTotalOutflow()} G / 순유입: {GetNet():+0;-0;0} G");
            sb.AppendLine($"  시간당 순유입: {GetHourlyNetRate(sessionMinutes):+0.0;-0.0;0.0} G/h");
            return sb.ToString();
        }
    }

    /// <summary>
    /// C-O2-01: 경제 감사 시스템 — PlayerStats.GoldChanged를 구독해 원장에 기록하는 싱글톤.
    /// DontDestroyOnLoad로 씬 전환 간 집계 유지. ContextMenu로 감사 리포트 출력.
    /// </summary>
    public class EconomyAuditSystem : MonoBehaviour
    {
        public static EconomyAuditSystem Instance { get; private set; }

        // 세션 시작 시각 (unscaledTime — 일시정지 영향 제외)
        private float _sessionStart;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            PlayerStats.GoldChanged += OnGoldChanged;
        }

        private void OnEnable()
        {
            _sessionStart = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            PlayerStats.GoldChanged -= OnGoldChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>세션 경과 분 (OnEnable 이후 unscaledTime 기준)</summary>
        public float SessionMinutes => Mathf.Max(0f, (Time.unscaledTime - _sessionStart) / 60f);

        // PlayerStats.GoldChanged → 원장 기록 (Core는 Systems를 참조할 수 없으므로 Systems가 구독)
        private void OnGoldChanged(PlayerStats.GoldLedgerEntry entry)
        {
            if (entry.isIncome)
                EconomyAuditLedger.RecordIncome(entry.source, entry.amount);
            else
                EconomyAuditLedger.RecordOutflow(entry.source, entry.amount);
        }

        /// <summary>현재 세션 감사 리포트를 콘솔에 출력 (인스펙터 우클릭 메뉴)</summary>
        [ContextMenu("감사 리포트")]
        private void LogAuditReport()
        {
            Debug.Log(EconomyAuditLedger.GetReport(SessionMinutes));
        }
    }
}
