using UnityEngine;

namespace ProjectName.Core.Utils
{
    /// <summary>
    /// [D4-B] 마을 경제 순수 수학 유틸리티. 마을 단위 중독/주민 이탈 상태를
    /// 마을 경제 지수(0.1..1.0)로 환산하고, 그 지수에 따라 AI 영지 주간 세수에
    /// 적용할 세금 계수(0.55..1.0)를 계산한다.
    ///
    /// MonoBehaviour 아님 — 상태 없음, 난수 없음: 동일 입력에 항상 동일 출력(결정론 — EditMode 재현/QA 검증 가능).
    ///
    /// 단위 규약:
    ///  · addiction은 0..100 백분율 스칼라 — 마을 단위 공유값(VillageDrugState.addiction).
    ///  · departed는 이탈 주민 누적 수, villagers는 (근사) 주민 총원.
    ///  · EconomyIndex는 0.1..1.0 (하한 0.1 — 마을이 완전히 소진되지 않는 방어선),
    ///    WeeklyTaxFactor는 0.55..1.0 (economy 하한 0.1 대응).
    /// </summary>
    public static class VillageEconomyMath
    {
        /// <summary>중독 +1(%p)당 경제 감소 가중 — 중독 100이면 −0.6 (스펙 D4-B).</summary>
        public const float AddictionEconomyWeight = 0.6f;

        /// <summary>주민 이탈 1인당 경제 감소 가중 — 이탈 비율(departed/villagers)에 곱해진다 (스펙 D4-B).</summary>
        public const float DepartureEconomyWeight = 0.3f;

        /// <summary>경제 지수 하한 — 아무리 무너져도 경제는 0.1 (완전 소진 → 세금 0/이탈 폭주 방지선).</summary>
        public const float EconomyIndexFloor = 0.1f;

        /// <summary>주간 세금 계수 기본 항 — economy 0일 때의 최소 계수 (스펙 D4-B: 0.5).</summary>
        public const float TaxFactorBase = 0.5f;

        /// <summary>경제가 세금 계수에 미치는 계수 — factor = 0.5 + 0.5 × economy (스펙 D4-B).</summary>
        public const float TaxFactorEconomySpan = 0.5f;

        /// <summary>
        /// 마을 경제 지수 = 1 − 중독/100 × 0.6 − 이탈/max(주민수,1) × 0.3
        /// 결과는 0.1..1.0 클램프. 중독 0 + 이탈 0 = 1.0 (깨끗한 마을 — D4 이전과 동일).
        /// </summary>
        /// <param name="addiction">마을 중독도 (0..100 — 클램프됨).</param>
        /// <param name="departed">이탈 주민 누적 수 (음수 방어).</param>
        /// <param name="villagers">주민 총원 (0 이하 방어 — max(1)로 나눈다).</param>
        public static float EconomyIndex(float addiction, int departed, int villagers)
        {
            float addictionClamped = Mathf.Clamp(addiction, 0f, 100f);
            int departedSafe = Mathf.Max(departed, 0);
            int villagersSafe = Mathf.Max(villagers, 1); // 분모 0 방어

            float index = 1f
                - addictionClamped / 100f * AddictionEconomyWeight
                - (float)departedSafe / villagersSafe * DepartureEconomyWeight;
            return Mathf.Clamp(index, EconomyIndexFloor, 1f);
        }

        /// <summary>
        /// 주간 세금 계수 = 0.5 + 0.5 × 영지 평균 경제.
        /// 깨끗한 마을(economy 1.0) = 1.0 (하위호환 — D4 이전 세수와 동일),
        /// 붕괴 마을(economy 하한 0.1) = 0.55. economy에 대해 단조 증가.
        /// </summary>
        /// <param name="territoryAvgEconomy">영지 평균 경제 지수 (0..1 — 범위 백 입력은 클램프 방어).</param>
        public static float WeeklyTaxFactor(float territoryAvgEconomy)
        {
            float economy = Mathf.Clamp(territoryAvgEconomy, 0f, 1f);
            return TaxFactorBase + TaxFactorEconomySpan * economy;
        }

        /// <summary>
        /// 세금 계수가 반영된 주간 세수 = floor(weeklyTax × factor + 0.5) — 정수 half-up 반올림
        /// (AITerritoryEconomySystem.GetWeeklyTax의 half-up 규약 준용).
        /// factor 1.0이면 정확히 weeklyTax (하위호환 — 기존 세수와 동일).
        /// </summary>
        /// <param name="weeklyTax">계수 적용 전 주간 세액 (음수 방어).</param>
        /// <param name="taxFactor">주간 세금 계수 (0..1 클램프 방어).</param>
        public static int ApplyTaxFactor(int weeklyTax, float taxFactor)
        {
            int weeklySafe = Mathf.Max(weeklyTax, 0);
            float factorClamped = Mathf.Clamp(taxFactor, 0f, 1f);
            return Mathf.FloorToInt(weeklySafe * factorClamped + 0.5f);
        }
    }
}
