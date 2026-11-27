using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// [툴팁 1차] 플레이어 소유 영지 호버 툴팁의 저장고/배치 라인 포맷터.
    /// 순수 함수(static)로만 구성 — EditMode 계약 테스트 대상 (TerritoryTooltipContentTests).
    /// MapWindow.BuildHoverTooltipText에서 호출.
    /// 경고 기준: 저장고 사용률 100% = "가득" / ≥80% = "곧 가득". 배치 전원 휴식 시 "배치 없음" 경고.
    /// </summary>
    public static class TerritoryTooltipContent
    {
        /// <summary>"곧 가득" 경고 임계 사용률 (0.8 = 80%).</summary>
        public const float NearFullRatio = 0.8f;

        /// <summary>역할 표기 순서 (고정 — 테스트 계약). None(휴식)은 항상 맨 뒤.</summary>
        private static readonly GuardTaskSystem.GuardTask[] RoleOrder =
        {
            GuardTaskSystem.GuardTask.Defend,
            GuardTaskSystem.GuardTask.Gather,
            GuardTaskSystem.GuardTask.Hunt,
            GuardTaskSystem.GuardTask.Farm,
            GuardTaskSystem.GuardTask.Mine,
            GuardTaskSystem.GuardTask.Envoy,
            GuardTaskSystem.GuardTask.Attack,
        };

        /// <summary>
        /// 저장고 상태 라인. capacity &lt;= 0 → "정보 없음".
        /// used == capacity → "🔴 가득" / used ≥ 80% → "⚠️ 곧 가득" / 그 외 사용률 표기만.
        /// </summary>
        public static string BuildStorageLine(int used, int capacity)
        {
            if (capacity <= 0) return "저장고: 정보 없음";
            if (used >= capacity)
                return $"저장고: {used}/{capacity} (🔴 가득 — 파견 물품 반입 불가)";
            if (used >= Mathf.CeilToInt(capacity * NearFullRatio))
                return $"저장고: {used}/{capacity} (⚠️ 곧 가득 — 확장 권장)";
            return $"저장고: {used}/{capacity}";
        }

        /// <summary>
        /// 배치 현황 라인. counts null/empty → "정보 없음".
        /// 배치 인원 0 + 휴식 존재 → "배치: 없음 (⚠️ 전원 휴식 — N명)".
        /// 배치 있으면 역할별 표기 (수비·채집·사냥·농사·광부·특사·동행) + 휴식 수.
        /// </summary>
        public static string BuildDeploymentLine(IReadOnlyDictionary<GuardTaskSystem.GuardTask, int> counts)
        {
            if (counts == null || counts.Count == 0) return "배치: 정보 없음";

            counts.TryGetValue(GuardTaskSystem.GuardTask.None, out int resting);
            int deployed = 0;
            foreach (var kvp in counts)
            {
                if (kvp.Key != GuardTaskSystem.GuardTask.None) deployed += kvp.Value;
            }

            if (deployed == 0)
            {
                if (resting > 0)
                    return $"배치: 없음 (⚠️ 전원 휴식 — {resting}명)";
                return "배치: 정보 없음";
            }

            var parts = new StringBuilder();
            foreach (var role in RoleOrder)
            {
                if (counts.TryGetValue(role, out int n) && n > 0)
                {
                    if (parts.Length > 0) parts.Append(" · ");
                    parts.Append($"{RoleLabel(role)} {n}");
                }
            }
            if (resting > 0) parts.Append($" · 휴식 {resting}");
            return $"배치: {parts}";
        }

        private static string RoleLabel(GuardTaskSystem.GuardTask task)
        {
            switch (task)
            {
                case GuardTaskSystem.GuardTask.Defend: return "수비";
                case GuardTaskSystem.GuardTask.Gather: return "채집";
                case GuardTaskSystem.GuardTask.Hunt: return "사냥";
                case GuardTaskSystem.GuardTask.Farm: return "농사";
                case GuardTaskSystem.GuardTask.Mine: return "광부";
                case GuardTaskSystem.GuardTask.Envoy: return "특사";
                case GuardTaskSystem.GuardTask.Attack: return "동행";
                default: return "휴식";
            }
        }
    }
}
