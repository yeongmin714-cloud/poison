using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Core.Utils
{
    /// <summary>
    /// [100차 P1] 음식점 가격·수요 순수 수학 유틸리티 — 일반 음식 가격 조절/방문 수요 연동.
    /// MonoBehaviour 아님 — 수요 계수/방문율/등급 기본가만 계산한다. 상태·확률 롤(Random)은 호출부(Systems) 소유.
    ///
    /// 결정론 규약:
    ///   · UnityEngine.Random 사용 금지 — 동일 입력 → 항상 동일 출력 (EditMode 재현/QA 검증 가능).
    ///   · VillageDrugState/RestaurantSystem 상태를 만지지 않는다(순수 함수 — RestaurantSaleMath 동일 규약).
    ///
    /// API 규약(시그니처 고정 — 변경 금지, 타 시스템/UI가 의존):
    ///   · VisitorDemandFactor(price, basePrice) — 가격 대비 수요 계수(1.0=등가). 무료(price&lt;=0)는
    ///     최대 수요 2.0(하한 0.4 예외 — 무료=최대 수요; 실정산가는 호출부가 basePrice/등록가 그대로 유지).
    ///   · VisitorRatio(villagerCount, demandFactor) — 기본 방문율 0.12×계수, 클램프 [0.03, 0.5].
    ///     최대 정수 방문 수의 반올림은 호출부 책임(본 수학은 비율만 반환).
    ///   · GradeBasePrice(rarity, itemBasePrice) — 등급 배수 적용 반올림, 무가격(&lt;=0)은
    ///     EconomyPricing 등급표 폴백 그대로.
    /// </summary>
    public static class RestaurantDemandMath
    {
        /// <summary>수요 계수 하한 — 고가 책벌 클램프(기준가의 40% 가격에서도 수요 유지).</summary>
        private const float DemandFactorMin = 0.4f;

        /// <summary>수요 계수 상한 — 저가/무료 수요 폭발 클램프(최대 2배).</summary>
        private const float DemandFactorMax = 2.0f;

        /// <summary>기본 방문율 — 중립 계수 1.0에서 주민의 12%.</summary>
        private const float VisitorRateBase = 0.12f;

        /// <summary>방문율 하한 클램프.</summary>
        private const float VisitorRatioMin = 0.03f;

        /// <summary>방문율 상한 클램프.</summary>
        private const float VisitorRatioMax = 0.5f;

        /// <summary>
        /// 가격 대비 수요 계수 — 비율 = basePrice / max(price, 1), 클램프 [0.4, 2.0].
        /// 가격&lt;기준가 → &gt;1.0(싸면 수요 증가) / 등가 → 1.0 / 가격&gt;기준가 → &lt;1.0(비싸면 감소).
        /// price &lt;= 0(무료/음수) → 최대 수요 2.0 고정(무료=최대 수요 — 하한 0.4 예외 규약;
        /// 실정산가는 호출부가 basePrice/등록가를 그대로 사용 — 무료 판매 금지 규약과 협력).
        /// basePrice &lt;= 0(무기준가)는 비율 0 → 하한 0.4(기준가 없음 → 보수적 저수요).
        /// </summary>
        public static float VisitorDemandFactor(int price, int basePrice)
        {
            if (price <= 0)
                return DemandFactorMax; // [100차] 무료=최대 수요 — 하한 0.4 예외

            float ratio = Mathf.Max(basePrice, 0) / (float)price;
            return Mathf.Clamp(ratio, DemandFactorMin, DemandFactorMax);
        }

        /// <summary>
        /// 방문율 — 기본 0.12 × 수요 계수, 클램프 [0.03, 0.5].
        /// villagerCount는 호출부 맥락 입력이며 본 수학은 비율만 반환한다
        /// (최대 정수 방문 수의 반올림은 호출부 책임 — API 규약).
        /// </summary>
        public static float VisitorRatio(int villagerCount, float demandFactor)
        {
            float ratio = VisitorRateBase * Mathf.Max(demandFactor, 0f);
            return Mathf.Clamp(ratio, VisitorRatioMin, VisitorRatioMax);
        }

        /// <summary>
        /// 등급별 기본가 — itemBasePrice &gt; 0이면 등급 배수 적용 후 반올림:
        ///   Common 1.0 / Uncommon 1.3 / Rare 1.7 / Epic 2.2 / Legendary 3.0
        ///   (Unique/미정의 등급은 Legendary 배수 상속 — 방어 기본값).
        /// itemBasePrice &lt;= 0(무가격 규약 O2 C-O2-02)이면 EconomyPricing 등급표 폴백 그대로.
        /// 결정론 — Random 미사용. UnityEngine.Random 금지 규약 준수.
        /// </summary>
        public static int GradeBasePrice(ItemRarity rarity, int itemBasePrice)
        {
            if (itemBasePrice > 0)
                return Mathf.Max(1, Mathf.RoundToInt(itemBasePrice * RarityMultiplier(rarity)));
            return FallbackGradePrice(rarity);
        }

        /// <summary>등급 배수 — [100차] 계약 고정표(Common 1.0/Uncommon 1.3/Rare 1.7/Epic 2.2/Legendary 3.0).</summary>
        private static float RarityMultiplier(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common:    return 1.0f;
                case ItemRarity.Uncommon:  return 1.3f;
                case ItemRarity.Rare:      return 1.7f;
                case ItemRarity.Epic:      return 2.2f;
                case ItemRarity.Legendary: return 3.0f;
                default:                   return 3.0f; // Unique/미정의 — 최상위 배수 상속(방어)
            }
        }

        /// <summary>
        /// 등급표 폴백 — [실측] EconomyPricing.GetBasePrice 등급표 복제
        /// (Common 20/Uncommon 60/Rare 150/Epic 400/Legendary 1000/Unique 2500 — 동기화 대상 주석).
        /// "폴백 그대로" 규약 — 카테고리 배율 미적용(등급표 값만 반환).
        /// </summary>
        private static int FallbackGradePrice(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common:    return 20;
                case ItemRarity.Uncommon:  return 60;
                case ItemRarity.Rare:      return 150;
                case ItemRarity.Epic:      return 400;
                case ItemRarity.Legendary: return 1000;
                case ItemRarity.Unique:    return 2500;
                default:                   return 20; // EconomyPricing default 방어 일치
            }
        }
    }
}
