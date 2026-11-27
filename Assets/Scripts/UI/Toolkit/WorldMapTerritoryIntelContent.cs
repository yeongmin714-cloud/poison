using System.Collections.Generic;
using ProjectName.Core.Data;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [124차-ImplB] 월드맵 우측 QuestDetailPanel 실데이터 포맷터 (순수 함수, static만 구성).
    /// WorldMapWindowUTK.BuildQuestDetailPanel/RefreshQuestDetailPanel이 소비.
    /// 123차 호버 툴팁 포맷터(WorldMapTerritoryTooltipContent)와 MapWindow.TerritoryTooltipContent
    /// (저장고/배치 라인)와 명칭 충돌 없는 별도 클래스.
    /// 위조 금지 계약 — 모든 값은 기존 데이터 소스 조합이며 신산식/새 소스 신설 없음:
    ///   영주 이름 = state.ownerLordName(PlayerOwned→"플레이어" — 123차 계약) /
    ///   병사 수 = 실배치 집계 우선(GuardManager.GetGuardsInTerritory) → def.guardCount 폴백 /
    ///   평균레벨 = GuardPlaceholder.Level 평균 / 전투력 = GuardEquipmentSystem.CalculateGuardCombatPower 합산(기존 산식 재사용 — 신산식 금지).
    /// </summary>
    public static class WorldMapTerritoryIntelContent
    {
        /// <summary>QuestHeroHeader 배지 텍스트 — 123차 OwnershipStateLine 계약 재사용(플레이어 소유/영주 소유/분쟁/무주).</summary>
        public static string OwnershipBadgeText(TerritoryOwnership o)
        {
            return WorldMapTerritoryTooltipContent.OwnershipStateLine(o);
        }

        /// <summary>영주 정보 ①"영주 이름" 값 — PlayerOwned→"플레이어"(123차 ownerLordName 매핑 계약),
        /// Unoccupied→"무주", 그 외(LordOwned/Contested)→state.ownerLordName(빈 값이면 def.lord.lordName 폴백 — 툴팁 OwnerLordLine과 동일 규칙).
        /// 최종 빈 값은 "—"(위조 금지).</summary>
        public static string LordNameValue(TerritoryDefinition def, TerritoryState state)
        {
            TerritoryOwnership own = state != null ? state.ownership : TerritoryOwnership.Unoccupied;
            if (own == TerritoryOwnership.PlayerOwned) return "플레이어";
            if (own == TerritoryOwnership.Unoccupied) return "무주";
            string tracked = state != null ? state.ownerLordName : null;
            if (string.IsNullOrEmpty(tracked)) tracked = def.lord.lordName;   // TerritoryDefinition은 struct — null 검사 불요
            return string.IsNullOrEmpty(tracked) ? "—" : tracked;
        }

        /// <summary>영주 정보 ②"병사 수" 값 — 실배치 집계(deployedCount, GuardManager.GetGuardsInTerritory 소속) 우선,
        /// 집계 0이면 def.guardCount 폴백, 둘 다 0이면 "—"(위조 금지).</summary>
        public static string SoldierCountValue(int deployedCount, int guardCountFallback)
        {
            if (deployedCount > 0) return deployedCount + "명";
            if (guardCountFallback > 0) return guardCountFallback + "명";
            return "—";
        }

        /// <summary>영주 정보 ③"병사 평균레벨" 값 — GuardPlaceholder.Level 평균(F1 표기). 집계 0이면 "—".</summary>
        public static string SoldierAvgLevelValue(IReadOnlyList<int> levels)
        {
            if (levels == null || levels.Count == 0) return "—";
            long sum = 0;
            for (int i = 0; i < levels.Count; i++) sum += levels[i];
            return "Lv. " + (sum / (float)levels.Count).ToString("F1");
        }

        /// <summary>영주 정보 ④"전투력" 값 — 기존 GuardEquipmentSystem.CalculateGuardCombatPower 합산(신산식 금지 계약).
        /// 합계 0(실배치 없음/시스템 부재)이면 "—".</summary>
        public static string CombatPowerValue(float aggregatedPower)
        {
            return aggregatedPower > 0f ? aggregatedPower.ToString("F1") : "—";
        }

        /// <summary>BriefingCard "해당 구역 주요 상태" 본문 — 기존 데이터 조합 4행:
        /// 소유 상태(OwnershipStateLine 계약)/난이도(WorldMapWindowUTK.DifficultyLabel 재사용)/수비대/전쟁 여부
        /// (전쟁 판정은 ownership==Contested — TerritoryWarManager 분쟁 상태와 동일 데이터 소스).
        /// TerritoryDefinition은 struct — null 검사 불요.</summary>
        public static string BriefingBody(TerritoryDefinition def, TerritoryState state, int deployedCount)
        {
            TerritoryOwnership own = state != null ? state.ownership : TerritoryOwnership.Unoccupied;
            return "소유 상태: " + WorldMapTerritoryTooltipContent.OwnershipStateLine(own) + "\n"
                 + "난이도: " + WorldMapWindowUTK.DifficultyLabel(def.difficulty) + "\n"
                 + "수비대: " + SoldierCountValue(deployedCount, def.guardCount) + "\n"
                 + "전쟁 여부: " + (own == TerritoryOwnership.Contested ? "전쟁 중" : "평시");
        }
    }
}
