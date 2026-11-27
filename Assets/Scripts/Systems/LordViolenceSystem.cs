using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [D4-B] 영주 폭력화 시스템. 주간 세금 정산 경계일에 영지의 실제 세수가 기준 미달이면
    /// (스펙 D4-B: 수취량 < GetWeeklyTax × 0.6 = 마을 경제 붕괴) 영주를 폭력화한다.
    ///
    /// 설계:
    ///  · 금고 잔액 의존 없음 — 세금 정산 수식(마을 경제 세금 계수)을 독립 재계산해 판정한다.
    ///    → GuardSalaryManager.OnDayStart 내 ApplyDailyTax/PayAILordMaintenance와 호출 순서 무관(결정론).
    ///  · 폭력화는 세이브/세탁 없이 유지 — lordViolent는 한번 true면 해제 경로가 없다(회복은 후속 지시 대기).
    ///  · playerHostile == true 영지는 제외(이미 플레이어 적대 — D3 발각 시스템 소관).
    ///  · 폭력화 시 loyaltyToPlayer −10 (접근자가 0..100 자동 클램프 — TerritoryData.cs 실측).
    ///  · 티커 씬 배선은 RestaurantDailyTicker와 동일 패턴(별도 Ensure* 배선 단계에서 추가).
    /// </summary>
    public static class LordViolenceSystem
    {
        /// <summary>폭력화 시 충성도 감소량 — 스펙 D4-B (−10).</summary>
        public const float LoyaltyPenaltyOnViolence = 10f;

        /// <summary>
        /// [순수] 세금 미달 판정 — 수취량 < 기준 × 0.6.
        /// [D4] 임계 0.5 → 0.6 상향 근거: WeeklyTaxFactor 하한 = 0.5 + 0.5 × 0.1(경제 하한) = 0.55
        /// — 0.5 임계에는 도달 불가 → 임계 0.6이 최소 도달 가능 기준(감독자 결정 2026-10-15).
        /// 부동소수 오차 없는 정수 연산(collected × 5 < standard × 3)으로 판정한다
        /// (collected < standard × 0.6 ⟺ 5×collected < 3×standard — 엄격 미달).
        /// </summary>
        /// <param name="collectedTax">이번 주 실제 수취 세액 (세금 계수 반영).</param>
        /// <param name="standardWeeklyTax">기준 주간 세액 (GetWeeklyTax — 감면 전).</param>
        public static bool IsTaxCollectionDeficient(int collectedTax, int standardWeeklyTax)
            => collectedTax * 5 < standardWeeklyTax * 3;

        /// <summary>
        /// 경계일 전 영지 스캔 (세금 정산 후 판정). LordOwned 영지만 대상이며,
        /// 미달 판정 시 ApplyTaxDeficiencyToState로 폭력화한다. 멱등 — 반복 호출해도 중복 페널티 없음.
        /// </summary>
        public static void EvaluateAllTerritories()
        {
            TerritoryDatabase db = TerritoryDatabase.Instance;
            if (db == null) return;

            foreach (TerritoryDefinition definition in db.GetAllDefinitions())
            {
                TerritoryState state = db.GetState(definition.id);
                if (state == null || state.ownership != TerritoryOwnership.LordOwned) continue;

                // 프로필 세율 해석 — ApplyDailyTax와 동일 패턴(결정론 Resolve).
                TerritoryDifficultyProfile profile = TerritoryDifficultyProfileSystem.Resolve(
                    AITerritoryEconomySystem.GetProfileTerritoryId(definition.id), definition.id.nation, definition.difficulty);

                int standard = AITerritoryEconomySystem.GetWeeklyTax(definition.difficulty, profile.TaxPercent);
                int collected = VillageEconomySystem.GetWeeklyCollectedTax(
                    definition.difficulty, profile.TaxPercent, definition.id.nation, definition.id.index);

                ApplyTaxDeficiencyToState(state, collected, standard, definition.id.ToString());
            }
        }

        /// <summary>
        /// [상태 반영] 미달 판정을 영지 상태에 반영 — 폭력화 시 ①lordViolent=true ②loyaltyToPlayer −10 ③로그.
        /// 이미 폭력화(유지 규약)/playerHostile(제외 규약)/미달 아님이면 상태를 만지지 않고 false 반환.
        /// </summary>
        /// <returns>이번 호출에서 실제로 폭력화됐는지 여부.</returns>
        public static bool ApplyTaxDeficiencyToState(
            TerritoryState state, int collectedTax, int standardWeeklyTax, string territoryLabel)
        {
            if (state == null) return false;
            if (state.lordViolent) return false;   // 한번 폭력화면 유지 (세탁/복구 없음 — 스펙 D4-B)
            if (state.playerHostile) return false; // 이미 플레이어 적대 — D3 발각 시스템 소관 (스펙 제외 조건)
            if (!IsTaxCollectionDeficient(collectedTax, standardWeeklyTax)) return false;

            state.lordViolent = true;
            state.loyaltyToPlayer = Mathf.Max(0f, state.loyaltyToPlayer - LoyaltyPenaltyOnViolence);
            Debug.Log($"[LordViolenceSystem] {territoryLabel} 주간 세수 기준 미달 " +
                      $"({collectedTax} < 기준 {standardWeeklyTax} × 0.6) — 영주 폭력화 (충성도 -{LoyaltyPenaltyOnViolence:0})");
            return true;
        }
    }

    /// <summary>
    /// [D4-B] 영주 폭력화 일일 부트스트래퍼 — TimeManager.OnDayStart(게임일 경계) 구독.
    /// 주 경계일(GuardSalaryManager 세금 정산일)에만 스캔한다. 판정은 정산 수식 독립 재계산이라
    /// 구독 순서와 무관하게 동일 결과. 구독 패턴은 GuardSalaryManager/RestaurantDailyTicker와 동일.
    /// </summary>
    public sealed class LordViolenceTicker : MonoBehaviour
    {
        private void OnEnable()
        {
            TimeManager tm = TimeManager.Instance;
            if (tm != null) tm.OnDayStart += OnDayStart;
        }

        private void OnDisable()
        {
            TimeManager tm = TimeManager.Instance;
            if (tm != null) tm.OnDayStart -= OnDayStart;
        }

        /// <summary>게임일 경계 → 주 정산 경계일에만 영주 폭력화 스캔.</summary>
        private void OnDayStart()
        {
            TimeManager tm = TimeManager.Instance;
            int day = tm != null ? tm.CurrentDay : 0;
            if (AITerritoryEconomySystem.IsTaxBoundaryDay(day))
                LordViolenceSystem.EvaluateAllTerritories();
        }
    }
}
