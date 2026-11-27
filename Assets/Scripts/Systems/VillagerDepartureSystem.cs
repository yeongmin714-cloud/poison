using System.Collections.Generic;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [D4-B] 주민 이탈 시스템. 음식점 일일 판매(RestaurantSystem.RunAllDaily) 직후 같은 게임일 틱에서
    /// 음식점 마을 각각에 대해 중독/영주 폭력화/영주 성격 → VillageDrugMath.DepartureChance 롤을 수행하고,
    /// 이탈 발생 시 ①VillageDrugState.RecordDeparture ②VillageNpcSpawner.RemoveVillageNPCs(NPC 1명) ③로그.
    ///
    /// 결정론: 롤은 daySeed+마을 키에서 파생한 System.Random으로 수행한다 — 17·31 문자열 해시
    /// (RestaurantSystem.DeriveVillageSeed/VillageNpcSpawner.GetHash와 동일 알고리즘, 접두사만 도메인별).
    /// UnityEngine.Random 사용 없음 → 동일 시드 재주입 시 동일 결과(프로세스 재시작 간 재현).
    /// 마을 순회 순서: VillagePlacementSystem.GetAllVillages (동→서→남→북 × 인덱스 0..19 결정론).
    /// </summary>
    public static class VillagerDepartureSystem
    {
        /// <summary>이탈 이벤트 1건당 제거할 NPC 수 — 마을당 하루 최대 1명 (스펙 D4-B: count=1).</summary>
        public const int DepartureNpcCountPerEvent = 1;

        /// <summary>전 마을 일일 이탈 처리 — RestaurantDailyTicker.OnDayStart(RunAllDaily 직후)에서 호출.</summary>
        /// <param name="daySeed">현재 게임 일차 (시드 원본 — 동일 일차 재호출 시 동일 롤).</param>
        public static void RunDailyDeparture(int daySeed)
        {
            List<VillagePlacementSystem.VillageInfo> villages = VillagePlacementSystem.GetAllVillages();
            int departedVillages = 0;

            foreach (VillagePlacementSystem.VillageInfo village in villages)
            {
                try
                {
                    var rng = new System.Random(DeriveVillageSeed(daySeed, village.nation, village.index));
                    if (ProcessVillageDeparture(village.nation, village.index, rng))
                        departedVillages++;
                }
                catch (System.Exception ex)
                {
                    // 개별 마을 예외는 로그 후 계속 — 하나의 실패가 전체 틱을 중단시키지 않는다(GuardSalaryManager 패턴).
                    Debug.LogError($"[VillagerDepartureSystem] {village.nation}_v{village.index} 이탈 판정 실패: {ex.Message}");
                }
            }

            Debug.Log($"[VillagerDepartureSystem] RunDailyDeparture(day={daySeed}) — 이탈 발생 마을 {departedVillages}/{villages.Count}");
        }

        /// <summary>
        /// 단일 마을 이탈 판정. 음식점 마을만 대상이다(미건설 마을 스킵 — DepartureChance의
        /// 입력은 중독/영주 상태이므로 음식점 경제가 없는 마을은 이탈 대상이 아님 — 스펙 D4-B).
        /// 롤 규약: rng.NextDouble() < DepartureChance(중독, lordViolent, 성격 인덱스)면 이탈.
        /// </summary>
        /// <returns>이번 호출에서 이탈이 발생했는지 여부.</returns>
        public static bool ProcessVillageDeparture(NationType nation, int villageIndex, System.Random rng)
        {
            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(nation, villageIndex);
            if (!snapshot.restaurantBuilt)
                return false; // 음식점 미건설 마을 — 이탈 없음

            // 영주 참조 — 마을→영지 1:1 (T3 실측: villageIndex 0..19 ↔ 영지 인덱스 1..20)
            TerritoryId territoryId = new TerritoryId(nation, villageIndex + 1);
            TerritoryDatabase db = TerritoryDatabase.Instance;
            TerritoryDefinition definition = db != null
                ? db.GetDefinition(territoryId)
                : default(TerritoryDefinition);
            TerritoryState state = db != null ? db.GetState(territoryId) : null;

            // 영주 상태 — 폭력화 플래그(D4) + 성격(LordInfo.personality enum → int 인덱스.
            //   VillageDrugMath 실측 순서와 동일: Neutral=0, Greedy=1, Suspicious=2, Brave=3,
            //   Cowardly=4, Wise=5, Cruel=6 — TerritoryData.cs 선언 순서 실측 완료).
            bool lordViolent = state != null && state.lordViolent;
            int personalityIdx = (int)definition.lord.personality;

            float chance = VillageDrugMath.DepartureChance(snapshot.addiction, lordViolent, personalityIdx);
            double roll = rng.NextDouble();
            if (!WillDepart(chance, roll))
                return false;

            // ① 이탈 기록 — departedVillagers +1 → 마을 경제 지수/경제 연쇄(세금 감면)에 반영된다.
            VillageDrugState.RecordDeparture(nation, villageIndex);
            // ② 주민 NPC 제거 (마을 부모의 마지막 주민 1명) — 부모 부재(미빌드)면 상태만 기록됨.
            VillageNpcSpawner.RemoveVillageNPCs(nation, villageIndex, DepartureNpcCountPerEvent);
            Debug.Log($"[VillagerDepartureSystem] {nation}_v{villageIndex} 주민 이탈 — " +
                      $"중독 {snapshot.addiction:F1} / 폭력화 {lordViolent} / 확률 {chance:P0}");
            return true;
        }

        /// <summary>[순수] 이탈 롤 판정 — roll < chance면 이탈 (반개구간 — chance 0이면 항상 미이탈, 1이면 항상 이탈).</summary>
        public static bool WillDepart(float chance, double roll)
            => roll < Mathf.Clamp(chance, 0f, 1f);

        /// <summary>
        /// 마을별 결정론 시드 파생 — 17·31 문자열 해시(RestaurantSystem.DeriveVillageSeed와 동일 알고리즘,
        /// 접두사 "departure_"로 도메인 분리 — 판매 롤과 상관 없는 독립 시퀀스).
        /// string.GetHashCode 미사용(프로세스 재시작 간 비결정론 방지).
        /// </summary>
        public static int DeriveVillageSeed(int daySeed, NationType nation, int villageIndex)
        {
            unchecked
            {
                int hash = 17;
                string input = $"departure_{daySeed}_{(int)nation}_{villageIndex}";
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }
    }
}
