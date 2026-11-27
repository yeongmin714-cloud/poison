using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [D4-B] 마을 경제 지수 시스템. 마을 단위 마약 중독/주민 이탈 상태를 영지 경제(0.1..1.0)로
    /// 환산하고, AI 영지 주간 세수에 세금 감면(세금 타격)으로 반영한다.
    ///
    /// 구조 (순수 수학 단일 소스 — VillageEconomyMath):
    ///  · 마을→영지 매핑: [실측] VillagePlacementSystem — villageIndex 0..19 ↔ 영지 인덱스 1..20 1:1 (영지당 마을 1개).
    ///  · 주민 총원: [실측] VillageNpcSpawner(T3-2) 등급별 주민 수 일반 6~10/촌락 3~5 → 중앙값 근사 8/4
    ///    (RestaurantSystem.EstimateVillagerCount와 동일 규약 — 씬 의존 없는 결정론 근사).
    ///  · 상태 없는 마을(중독 0/이탈 0)은 항상 economy 1.0 = factor 1.0 → D4 이전 세수와 동일 (완전 하위호환).
    ///  · 마을 부재 국가(황제국 등)도 기본 스냅샷 → factor 1.0 (감면 없음).
    /// </summary>
    public static class VillageEconomySystem
    {
        /// <summary>주민 총원 중앙값 근사 — [실측] VillageNpcSpawner 일반 마을 6~10 → 8.</summary>
        private const int VillagerCountNormal = 8;

        /// <summary>주민 총원 중앙값 근사 — [실측] VillageNpcSpawner 촌락(isHamlet) 3~5 → 4.</summary>
        private const int VillagerCountHamlet = 4;

        /// <summary>촌락 판정 영지 인덱스 하한 — [T3-1 실측] Ring4 = 인덱스 16~20 (VillagePlacementSystem 동일 근거).</summary>
        private const int HamletTerritoryIndexMin = 16;

        /// <summary>촌락 판정 영지 인덱스 상한 — [T3-1 실측] Ring4 = 인덱스 16~20.</summary>
        private const int HamletTerritoryIndexMax = 20;

        /// <summary>
        /// 마을 경제 지수 (0.1..1.0). VillageDrugState 스냅샷을 읽기만 한다(상태 생성 없음).
        /// </summary>
        /// <param name="nation">마을 소속 국가.</param>
        /// <param name="villageIndex">마을 인덱스 (0..19).</param>
        public static float GetVillageEconomy(NationType nation, int villageIndex)
        {
            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(nation, villageIndex);
            return VillageEconomyMath.EconomyIndex(
                snapshot.addiction, snapshot.departedVillagers, EstimateVillagerCount(villageIndex));
        }

        /// <summary>
        /// 영지 주간 세금 계수 (0.55..1.0) = WeeklyTaxFactor(1:1 대응 마을의 경제 지수).
        /// 영지 인덱스 1..20 ↔ villageIndex 0..19 (T3 실측 1:1). 범위 밖 인덱스는 1.0 (감면 없음 — 하위호환).
        /// </summary>
        /// <param name="nation">영지 소속 국가.</param>
        /// <param name="territoryIndex">영지 인덱스 (1..20).</param>
        public static float GetTerritoryEconomyFactor(NationType nation, int territoryIndex)
        {
            int villageIndex = territoryIndex - 1; // 마을→영지 1:1 (T3 실측)
            if (villageIndex < 0 || villageIndex >= VillagePlacementSystem.VillagesPerNation)
                return 1f; // 마을 부재(범위 밖) — 세금 감면 없음
            return VillageEconomyMath.WeeklyTaxFactor(GetVillageEconomy(nation, villageIndex));
        }

        /// <summary>
        /// 해당 영지의 실제 주간 세수 = ApplyTaxFactor(GetWeeklyTax, factor) — half-up 정수 반올림.
        /// AITerritoryEconomySystem.ApplyDailyTax(적립)와 LordViolenceSystem(기준 판정)이
        /// 함께 소비하는 단일 소스 — 수식 분기(drift) 방지.
        /// </summary>
        /// <param name="difficulty">영지 난이도 (일세수 산출 입력).</param>
        /// <param name="taxPercent">프로필 세율 (TerritoryDifficultyProfile.TaxPercent).</param>
        /// <param name="nation">영지 소속 국가.</param>
        /// <param name="territoryIndex">영지 인덱스 (1..20).</param>
        public static int GetWeeklyCollectedTax(
            TerritoryDifficulty difficulty, int taxPercent, NationType nation, int territoryIndex)
        {
            int weeklyTax = AITerritoryEconomySystem.GetWeeklyTax(difficulty, taxPercent);
            float factor = GetTerritoryEconomyFactor(nation, territoryIndex);
            return VillageEconomyMath.ApplyTaxFactor(weeklyTax, factor);
        }

        /// <summary>주민 총원 중앙값 근사 — RestaurantSystem.EstimateVillagerCount와 동일 규약 (T3-2 실측).</summary>
        private static int EstimateVillagerCount(int villageIndex)
        {
            int territoryIndex = villageIndex + 1; // 마을→영지 1:1
            bool isHamlet = territoryIndex >= HamletTerritoryIndexMin && territoryIndex <= HamletTerritoryIndexMax;
            return isHamlet ? VillagerCountHamlet : VillagerCountNormal;
        }
    }
}
