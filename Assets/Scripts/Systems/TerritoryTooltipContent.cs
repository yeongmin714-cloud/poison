using System.Collections.Generic;
using System.Text;

namespace ProjectName.Systems
{
    /// <summary>
    /// [툴팁 1차] 영지 툴팁 확장 라인 포맷터 — 순수 함수 계약.
    /// MapWindow 호버 툴팁에서 플레이어 소유 영지의 저장고 상태/병사 배치 현황을
    /// 문자열로 변환한다. Unity API 비의존 (EditMode 단위 테스트 대상).
    ///
    /// 경고 기준:
    ///   저장고 — 사용률 ≥100% "🔴 가득", ≥80% "⚠️ 곧 가득", 그 외 무경고.
    ///   배치   — 배정 병사 0명(전원 휴식) "⚠️ 배치 없음 — 전원 휴식".
    /// </summary>
    public static class TerritoryTooltipContent
    {
        /// <summary>저장고 경고 임계 — 사용률이 이 값 이상이면 "곧 가득" 경고.</summary>
        public const float StorageWarnRatio = 0.8f;

        /// <summary>
        /// 저장고 상태 라인 생성. capacity 0 이하(시스템 비정상)는 "정보 없음".
        /// </summary>
        /// <example>저장고: 17/20 (⚠️ 곧 가득 — 확장 권장)</example>
        public static string BuildStorageLine(int used, int capacity)
        {
            if (capacity <= 0)
                return "저장고: 정보 없음";
            float ratio = (float)used / capacity;
            if (ratio >= 1f)
                return $"저장고: {used}/{capacity} (🔴 가득 — 파견 물품 반입 불가)";
            if (ratio >= StorageWarnRatio)
                return $"저장고: {used}/{capacity} (⚠️ 곧 가득 — 확장 권장)";
            return $"저장고: {used}/{capacity}";
        }

        /// <summary>
        /// 배치 현황 라인 생성. None(휴식)은 마지막에 집계하며,
        /// 배정 병사가 0명이면 "⚠️ 배치 없음 — 전원 휴식" 경고.
        /// </summary>
        /// <example>배치: 수비 3 · 채집 2 · 광부 2 · 동행 1 · 휴식 0</example>
        public static string BuildDeploymentLine(Dictionary<GuardTaskSystem.GuardTask, int> counts)
        {
            if (counts == null || counts.Count == 0)
                return "배치: 정보 없음";

            int assigned = 0;
            int resting = 0;
            var parts = new List<string>();

            // 표시 순서 고정 (None 제외) — 결정론 출력.
            var order = new[]
            {
                GuardTaskSystem.GuardTask.Defend,
                GuardTaskSystem.GuardTask.Attack,
                GuardTaskSystem.GuardTask.Gather,
                GuardTaskSystem.GuardTask.Hunt,
                GuardTaskSystem.GuardTask.Farm,
                GuardTaskSystem.GuardTask.Mine,
                GuardTaskSystem.GuardTask.Envoy
            };
            var names = new Dictionary<GuardTaskSystem.GuardTask, string>
            {
                { GuardTaskSystem.GuardTask.Defend, "수비" },
                { GuardTaskSystem.GuardTask.Attack, "동행" },
                { GuardTaskSystem.GuardTask.Gather, "채집" },
                { GuardTaskSystem.GuardTask.Hunt,   "사냥" },
                { GuardTaskSystem.GuardTask.Farm,   "농사" },
                { GuardTaskSystem.GuardTask.Mine,   "광부" },
                { GuardTaskSystem.GuardTask.Envoy,  "특사" }
            };

            foreach (var task in order)
            {
                if (!counts.TryGetValue(task, out int n) || n <= 0) continue;
                parts.Add($"{names[task]} {n}");
                assigned += n;
            }
            if (counts.TryGetValue(GuardTaskSystem.GuardTask.None, out int noneCount))
                resting = noneCount;
            parts.Add($"휴식 {resting}");

            var sb = new StringBuilder("배치: ");
            if (assigned == 0)
                sb.Append("⚠️ 배치 없음 — 전원 휴식");
            else
                sb.Append(string.Join(" · ", parts));
            return sb.ToString();
        }
    }
}
