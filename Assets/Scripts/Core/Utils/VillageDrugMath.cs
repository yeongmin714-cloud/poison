using UnityEngine;
using ProjectName.Core.Data; // AddictionLevel (DrugDatabase.cs) — 동일 어셈블리(ProjectName.Core) 내 참조

namespace ProjectName.Core.Utils
{
    /// <summary>
    /// 음식점 마약 경제 순수 수학 유틸리티 (Phase D2 — RESTAURANT_DRUG_ECONOMY_PLAN.md 사용자 승인 스펙).
    /// MonoBehaviour 아님 — 발각/적대/이탈 확률과 가격/수요 수치만 계산한다.
    ///
    /// 롤 규약: 발각·적대 전환·이탈의 실제 판정 롤(Random)은 호출부(Systems)가 수행한다.
    ///   본 클래스는 확률값만 반환한다 — UnityEngine.Random을 사용하지 않으므로
    ///   동일 입력에 대해 항상 동일 출력이 보장된다(결정론 — EditMode 재현/QA 검증 가능).
    ///
    /// 단위 규약:
    ///   · 중독도(addiction)는 0..100 백분율 스칼라 — 마을 단위 공유값(VillageDrugState.addiction).
    ///   · 중독 상승량(AddictionGainPerSale)은 "판매 1건당" %p — 초당 수치가 아니다.
    ///   · 확률 반환값은 전부 0..1 스칼라(클램프 포함) — Random.value와 바로 비교 가능.
    ///
    /// 등급 규약: AddictionLevel 실측 순서 Low=0, Medium=1, High=2, VeryHigh=3, Extreme=4, Fatal=5
    ///   (gradeIdx = (int)level). 범위 백 캐스트 방어를 위해 gradeIdx는 0..5로 클램프한다.
    ///   LordPersonality 실측 순서: Neutral=0, Greedy=1, Suspicious=2, Brave=3, Cowardly=4, Wise=5, Cruel=6.
    /// </summary>
    public static class VillageDrugMath
    {
        /// <summary>발각 기본 확률 — 마약 음식 판매 1건 기준 최하한치.</summary>
        private const float DetectionBase = 0.15f;

        /// <summary>호감 만불(loyalty 0) 시 가산 계수 — 발각 = base + (1 − 호감/100) × 0.65.</summary>
        private const float DetectionLoyaltySpan = 0.65f;

        /// <summary>중독등급 1단계당 발각 가산치 (gradeIdx × 0.03).</summary>
        private const float DetectionGradeStep = 0.03f;

        /// <summary>발각 확률 상한 클램프 — 최악이라도 95%.</summary>
        private const float DetectionClampMax = 0.95f;

        /// <summary>적대 전환 기본 확률 (40%) — 발각 별도 롤.</summary>
        private const float HostileBase = 0.40f;

        /// <summary>중독등급 1단계당 적대 전환 가산치 (gradeIdx × 0.05).</summary>
        private const float HostileGradeStep = 0.05f;

        /// <summary>중독 만불(100) 시 권장가 배율 가산 — 권장가 = base × (1 + 중독/100 × 1.5) = 최대 ×2.5.</summary>
        private const float PriceAddictionMultiplier = 1.5f;

        /// <summary>과대 가격 시 수요 감소 기울기 — 권장가 대비 초과분 비율 × 2.0.</summary>
        private const float DemandPenaltySlope = 2.0f;

        /// <summary>수요 계수 하한 — 과대 가격이라도 수요는 20% 남는다.</summary>
        private const float DemandClampMin = 0.2f;

        /// <summary>이탈 확률 상한 클램프 — 최악이라도 90% (완전 소진 방지 여유).</summary>
        private const float DepartureClampMax = 0.9f;

        // ===== 중독 상승량 =====

        /// <summary>
        /// 판매 1건당 마을 중독 상승량(%p — 초당 아님). 마약 희귀도(AddictionLevel) 비례 상수표:
        /// Low=2.0 / Medium=4.5 / High=8.0 / VeryHigh=12.0 / Extreme=17.0 / Fatal=23.0.
        /// </summary>
        public static float AddictionGainPerSale(AddictionLevel level)
        {
            switch (level)
            {
                case AddictionLevel.Low: return 2.0f;
                case AddictionLevel.Medium: return 4.5f;
                case AddictionLevel.High: return 8.0f;
                case AddictionLevel.VeryHigh: return 12.0f;
                case AddictionLevel.Extreme: return 17.0f;
                case AddictionLevel.Fatal: return 23.0f;
                default:
                    // 미정의 등급(범위 백 캐스트 등)은 최하등급으로 방어 — 결정론 유지.
                    return 2.0f;
            }
        }

        // ===== 발각 / 적대 =====

        /// <summary>
        /// 마약 음식 판매 1건의 발각 확률:
        /// p = 0.15 + (1 − 호감도/100) × 0.65 + 중독등급 가산(gradeIdx × 0.03).
        /// 호감도(0~100)와 결과(0..0.95)는 클램프한다. 영주 호감이 높을수록 발각이 어렵다.
        /// </summary>
        public static float DetectionProbability(float loyalty, AddictionLevel level)
        {
            float loyaltyClamped = Mathf.Clamp(loyalty, 0f, 100f);
            int gradeIdx = Mathf.Clamp((int)level, 0, 5); // AddictionLevel 6단계 방어 클램프
            float p = DetectionBase
                + (1f - loyaltyClamped / 100f) * DetectionLoyaltySpan
                + gradeIdx * DetectionGradeStep;
            return Mathf.Clamp(p, 0f, DetectionClampMax);
        }

        /// <summary>
        /// 발각 시 영지 적대 전환 확률: 0.40 + gradeIdx × 0.05 (등급일수록 치명).
        /// 결과는 0..1 클램프. 발각 롤과 별도 판정이다(발각 → 이 확률로 적대 전환 2차 롤).
        /// </summary>
        public static float HostileConversionProbability(AddictionLevel level)
        {
            int gradeIdx = Mathf.Clamp((int)level, 0, 5);
            float p = HostileBase + gradeIdx * HostileGradeStep;
            return Mathf.Clamp(p, 0f, 1f);
        }

        // ===== 가격 / 수요 =====

        /// <summary>
        /// 권장 판매가: ceil(기본가 × (1 + 중독도/100 × 1.5)) — 중독 0이면 기본가, 100이면 ×2.5.
        /// 최소 1골드. 중독도는 0..100 클램프한다. (부동소수 나머지로 ±1 오차 가능 — ceil 규약)
        /// </summary>
        public static int RecommendedPrice(int basePrice, float addiction)
        {
            float addictionClamped = Mathf.Clamp(addiction, 0f, 100f);
            int basePriceSafe = Mathf.Max(basePrice, 0);
            int price = Mathf.CeilToInt(basePriceSafe * (1f + addictionClamped / 100f * PriceAddictionMultiplier));
            return Mathf.Max(price, 1);
        }

        /// <summary>
        /// 수요 계수: 등록가 ≤ 권장가면 1.0(풀수요). 초과하면
        /// max(0.2, 1.0 − (초과분/권장가) × 2.0) — 과대 가격일수록 수요 감소, 0.2 하한.
        /// 권장가 0 방어: max(권장가, 1)을 분모로 사용한다.
        /// </summary>
        public static float DemandFactor(int playerPrice, int recommendedPrice)
        {
            if (playerPrice <= recommendedPrice)
                return 1.0f;

            float recommendedSafe = Mathf.Max(recommendedPrice, 1);
            float factor = 1.0f
                - (playerPrice - recommendedPrice) / recommendedSafe * DemandPenaltySlope;
            return Mathf.Max(factor, DemandClampMin);
        }

        // ===== 이탈 =====

        /// <summary>
        /// 주민 이탈 확률: 중독도/100 × 0.30 + (영주 폭력화 시 +0.20) + 성격 가중
        /// → 0..0.9 클램프. 성격 가중은 LordPersonality 실측 순서 기준:
        /// Cruel(6)=+0.10, Wise(5)=−0.10, 그 외(Neutral~Cowardly)=0.
        /// </summary>
        /// <param name="addiction">마을 중독도 (0..100 — 클램프됨).</param>
        /// <param name="lordViolent">영주 폭력화 플래그 (세금 미달 누적 등 — D4 연계).</param>
        /// <param name="personalityIdx">영주 성격 인덱스 0..6 (LordPersonality: Neutral/Greedy/Suspicious/Brave/Cowardly/Wise/Cruel 실측 순서 — 클램프됨).</param>
        public static float DepartureChance(float addiction, bool lordViolent, int personalityIdx)
        {
            float addictionClamped = Mathf.Clamp(addiction, 0f, 100f);
            int personality = Mathf.Clamp(personalityIdx, 0, 6); // LordPersonality 7종 방어 클램프

            float chance = addictionClamped / 100f * 0.30f
                + (lordViolent ? 0.20f : 0f)
                + PersonalityDepartureWeight(personality);
            return Mathf.Clamp(chance, 0f, DepartureClampMax);
        }

        /// <summary>성격별 이탈 가중 — Cruel(6)=+0.10, Wise(5)=−0.10, 그 외 0 (실측 순서 기반).</summary>
        private static float PersonalityDepartureWeight(int personalityIdx)
        {
            switch (personalityIdx)
            {
                case 5: return -0.10f;  // Wise — 현명한 영주는 이탈을 완화
                case 6: return +0.10f;  // Cruel — 잔인한 영주는 이탈을 가중
                default: return 0f;     // Neutral/Greedy/Suspicious/Brave/Cowardly
            }
        }

        // ===== [D4-A] 음식점 건설 허가세 =====

        /// <summary>
        /// 음식점 건설 허가세 (골드) — 마을 기준 영지 난이도 링 배율 상수표 (계획서 D4-A 스펙).
        /// Ring4(황제국 인접·최위험)=150 / Ring3=100 / Ring2=60 / Ring1(최외곽·최안전)=40.
        /// Empire(황제국 본성)에는 마을이 없어(마을은 4국가 영지 인덱스 1~20에만 존재 — VillagePlacementSystem)
        /// 도달하지 않는다 — 방어 차원으로 Ring4와 동일 최고값 150 반환(미정의 등급 방어 패턴 준용).
        /// 결정론 순수 함수 — 소비/지출 수행은 호출부(ConstructionManager.TryPlace) 책임.
        /// </summary>
        public static int PermitFee(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 40;
                case TerritoryDifficulty.Ring2: return 60;
                case TerritoryDifficulty.Ring3: return 100;
                case TerritoryDifficulty.Ring4: return 150;
                default: return 150; // Empire/미정의 — 방어 기본값 (최고 링 요율)
            }
        }
    }
}
