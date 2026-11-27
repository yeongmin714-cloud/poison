using UnityEngine;

namespace ProjectName.Core.Utils
{
    /// <summary>
    /// 음식점 일일 판매 순수 수학 유틸리티 (Phase D3-B — RESTAURANT_DRUG_ECONOMY_PLAN.md).
    /// MonoBehaviour 아님 — 판매 수량/골드/수요 수치만 계산한다. 상태·확률 롤(Random)은 호출부(Systems) 소유.
    ///
    /// 결정론 규약:
    ///   · UnityEngine.Random 사용 금지 — 난수는 호출부가 System.Random 등에서 뽑아 값으로 주입한다
    ///     (rngNextDouble, 0..1). 동일 입력 → 항상 동일 출력 (EditMode 재현/QA 검증 가능).
    ///   · 마약 우선 소진 — 수요 한도 내에서 마약 음식을 먼저 판다(중독 유지 인센티브 — 규약 고정).
    ///
    /// 단위 규약:
    ///   · registeredPrice = 플레이어 등록 판매가(마약 음식에 적용), defaultBasePrice = 일반 음식 기본가.
    ///   · demandFactor = VillageDrugMath.DemandFactor(등록가, 권장가) 결과(0.2~1.0) 전달용 계수.
    /// </summary>
    public static class RestaurantSaleMath
    {
        /// <summary>중독 0 기준 기본 수요율 — 잔존 주민의 20%.</summary>
        private const float DemandRateBase = 0.2f;

        /// <summary>중독 만불(100) 시 추가 수요율 — +0.5 (0.2 → 최대 0.7).</summary>
        private const float DemandRateAddictionSpan = 0.5f;

        /// <summary>
        /// 일일 판매 계획 — 수요(demandCount) 한도 내에서 마약 음식을 우선 소진한다(결정론 규약).
        /// 골드 = (마약 판매 수 × 등록가) + (일반 판매 수 × 기본가).
        ///
        /// 방어 규약:
        ///   · demandCount ≤ 0 → 판매 없음. 재고 음수는 0 취급.
        ///   · registeredPrice ≤ 0(미등록) → 마약 판매 없음, defaultBasePrice ≤ 0 → 일반 판매 없음
        ///     (무료 판매 방지 — 가격 0으로 골드 0을 벌지 않는다).
        ///   · demandFactor < 0 → 0 취급. 유효 수요 = floor(demandCount × demandFactor).
        /// 재고 자체는 소모하지 않는다(계획만 반환 — 실소모는 호출부가 ConsumeStock으로 수행).
        /// </summary>
        public static (int soldNormal, int soldDrugged, int gold) PlanDailySale(
            int normalStock, int druggedStock, int demandCount,
            int registeredPrice, int defaultBasePrice, float demandFactor)
        {
            int demand = Mathf.Max(demandCount, 0);
            if (demand <= 0)
                return (0, 0, 0);

            int normal = Mathf.Max(normalStock, 0);
            int drugged = Mathf.Max(druggedStock, 0);

            float factorSafe = Mathf.Max(demandFactor, 0f);
            int effectiveDemand = Mathf.FloorToInt(demand * factorSafe);
            if (effectiveDemand <= 0)
                return (0, 0, 0);

            // 마약 우선 — 수요 한도 내 먼저 소진 (결정론 규약: 슬롯 순서와 무관하게 수량만 결정)
            int soldDrugged = registeredPrice > 0 ? Mathf.Min(drugged, effectiveDemand) : 0;
            int soldNormal = defaultBasePrice > 0
                ? Mathf.Min(normal, effectiveDemand - soldDrugged)
                : 0;

            int gold = soldDrugged * registeredPrice + soldNormal * defaultBasePrice;
            return (soldNormal, soldDrugged, gold);
        }

        /// <summary>
        /// 일일 기본 수요 산출:
        ///   기본 수요 = round((villagerCount − departedVillagers) × (0.2 + 중독도/100 × 0.5))
        ///   + 난수 지터 ±1(기본 수요가 1 이상일 때만 — 주민 0 마을에 지터로 가짜 수요가 생기지 않게 방어),
        ///   하한 0 클램프.
        ///
        /// 결정론: rngNextDouble은 호출부가 주입한 0..1 값이며, < 0.5면 −1, 아니면 +1로 해석한다.
        /// 중독도에 대해 단조 증가(같은 지터값 기준), 이탈 주민에 대해 단조 감소한다.
        /// </summary>
        public static int DemandCount(int villagerCount, float addiction, int departedVillagers, double rngNextDouble)
        {
            int residents = Mathf.Max(villagerCount - Mathf.Max(departedVillagers, 0), 0);
            float addictionClamped = Mathf.Clamp(addiction, 0f, 100f);
            float rate = DemandRateBase + addictionClamped / 100f * DemandRateAddictionSpan;
            int demand = Mathf.RoundToInt(residents * rate);

            if (demand >= 1)
                demand += rngNextDouble < 0.5 ? -1 : +1; // 소폭 지터(±1) — 주입값 기반 결정론

            return Mathf.Max(demand, 0);
        }
    }
}
