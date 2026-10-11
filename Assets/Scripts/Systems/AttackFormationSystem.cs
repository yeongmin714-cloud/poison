using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [40차] 공격대 배치 시스템 — 피그마 "공격조 전투 배치"(204:1963) 구현.
    /// 공격대(Attack 역할 배정 병사)를 5열(A~E) × 4행(1=전열, 2=중열, 3=지원, 4=후열) 전투 배치도의
    /// 칸(A1..E4)에 배치해 "누가 선봉에 서고 누가 후방에서 공격할지"를 정한다.
    /// - AssignCell/ClearCell/ClearAll: 배치 UI 편집
    /// - ApplyFormation: 편성 확정 — 각 대원에게 칸 오프셋 위치로 보행 명령(SetCommandTarget)
    ///   (실내 기준점 = 로비 앵커, 월드 전투 시 = TerritoryDeploymentSystem 공격 목표 지점)
    /// 셀 좌표계: 열 A..E(좌→우), 행 1..4(적 방향→후방). 한 셀에 대원 1명.
    /// </summary>
    public static class AttackFormationSystem
    {
        public const int Columns = 5;   // A..E
        public const int Rows = 4;      // 1=전열 2=중열 3=지원 4=후열

        private static readonly Dictionary<GuardPlaceholder, string> _cells = new();

        public static readonly string[] RowNames = { "전열", "중열", "지원", "후열" };
        public static readonly string[] ColumnNames = { "A", "B", "C", "D", "E" };

        /// <summary>칸 → 전투 오프셋(미터). 행1(전열)이 적에 가장 가깝다(+Z = 공격 방향).</summary>
        public static Vector3 CellOffset(string cell)
        {
            if (!TryParseCell(cell, out int col, out int row))
                return Vector3.zero;
            return new Vector3((col - (Columns - 1) * 0.5f) * 2.0f, 0f, (row - 1) * 2.2f);
        }

        /// <summary>행 이름(1=전열..4=후열). 유효하지 않으면 "".</summary>
        public static string RowName(int row)
        {
            return row >= 1 && row <= Rows ? RowNames[row - 1] : "";
        }

        public static bool TryParseCell(string cell, out int col, out int row)
        {
            col = row = 0;
            if (string.IsNullOrEmpty(cell) || cell.Length < 2) return false;
            col = cell[0] - 'A';
            if (col < 0 || col >= Columns) return false;
            if (!int.TryParse(cell.Substring(1), out row)) return false;
            if (row < 1 || row > Rows) return false;
            return true;
        }

        public static bool IsValidCell(string cell) => TryParseCell(cell, out _, out _);

        public static string GetCell(GuardPlaceholder g)
        {
            return g != null && _cells.TryGetValue(g, out var cell) ? cell : null;
        }

        /// <summary>칸 배치 — 이미 점유된 칸이면 false(한 셀 1명).</summary>
        public static bool AssignCell(GuardPlaceholder g, string cell)
        {
            if (g == null || !IsValidCell(cell)) return false;
            foreach (var kvp in _cells)
            {
                if (kvp.Key != g && kvp.Value == cell && kvp.Key != null)
                    return false;   // 점유 칸
            }
            _cells[g] = cell;
            return true;
        }

        public static void ClearCell(GuardPlaceholder g)
        {
            if (g != null) _cells.Remove(g);
        }

        public static void ClearAll() => _cells.Clear();

        /// <summary>편성 스냅샷 열거(칸 배치 조회/UI 갱신용).</summary>
        public static IEnumerable<KeyValuePair<GuardPlaceholder, string>> EnumerateCells()
        {
            return _cells;
        }

        public static int AssignedCount => _cells.Count;

        /// <summary>편성 확정 — 배치된 대원에게 칸 오프셋 보행 명령. anchor = 전투 기준점(적 방향 +Z).</summary>
        public static int ApplyFormation(Vector3 anchor)
        {
            int applied = 0;
            foreach (var kvp in _cells)
            {
                var g = kvp.Key;
                if (g == null) continue;
                Vector3 target = anchor + CellOffset(kvp.Value);
                g.IndoorMoveExempt = true;          // 배치 UI 의도 명령 — P14 예외
                g.SetCommandTarget(target, false);
                applied++;
            }
            Debug.Log($"[AttackFormation] ⚔️ 전투 배치 적용 — {applied}명 (anchor {anchor})");
            return applied;
        }

        /// <summary>편성 요약 — "5 / 7 명" 표기용.</summary>
        public static (int assigned, int total) Summarize(IEnumerable<GuardPlaceholder> squad)
        {
            int total = 0, assigned = 0;
            foreach (var g in squad)
            {
                if (g == null) continue;
                total++;
                if (_cells.ContainsKey(g)) assigned++;
            }
            return (assigned, total);
        }
    }
}
