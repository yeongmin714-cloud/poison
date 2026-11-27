using System;
using ProjectName.Core.Data; // CookingDatabase — Core.Data 참조 (QA 실측 CS0246 수리)

namespace ProjectName.Core.Utils
{
    /// <summary>
    /// 요리크래프트 마약 확장 순수 헬퍼 (Phase D3-A — 계획서 §1-2-3, 사용자 확정 스펙).
    /// MonoBehaviour 아님 — 조합레시피 신설 없이, 기존 요리 결과에 마약 메타데이터(문자열)만 탑재한다.
    ///
    /// 규약 (D3-A 스펙 고정 — 변경 금지, 테스트 CookingDrugTests가 계약 검증):
    ///   · itemId      = "drugged_{dishId}"            — 예: cook_01 → drugged_cook_01
    ///   · displayName = "마약 뿌려진 {dishName}"       — 예: 토끼 허브 구이 → 마약 뿌려진 토끼 허브 구이
    ///   · description = {기존 description} [마약: stage {n}] — 기존 설명 뒤에 마약 마커 공백 1칸으로 부착
    ///   · drugStage   = 마약 희귀도(Stage) — 인벤 마약 id "drug_{stage:D2}"(실측: DrugEffectSystem.CreateDrugItem)에서
    ///     ParseDrugStage로 파싱한다. stage가 높을수록 마커 값이 커지며, 실제 중독 상승 판정은
    ///     D3-B VillageDrugMath(음식점 판매 시)가 담당 — 본 클래스는 표기 메타데이터만 다룬다.
    ///   · 결정론: Random/시간 의존 없음 — 동일 입력에 항상 동일 출력(EditMode 재현 가능).
    /// </summary>
    public static class CookingDrugHelper
    {
        /// <summary>
        /// 기존 요리 결과(CookingDatabase.CookingResult)를 마약 뿌려진 음식 규약 문자열로 변환한다.
        /// 기반 요리의 DishId/DishName/Description은 절대 변경하지 않는다(파생 문자열만 생성).
        /// </summary>
        public static (string itemId, string displayName, string description) MakeDruggedFood(
            CookingResult baseFood, int drugStage)
        {
            string dishId = baseFood.DishId ?? string.Empty;
            string dishName = baseFood.DishName ?? string.Empty;
            string description = baseFood.Description ?? string.Empty;

            string itemId = $"drugged_{dishId}";
            string displayName = $"마약 뿌려진 {dishName}";
            string druggedDescription = $"{description} [마약: stage {drugStage}]";
            return (itemId, displayName, druggedDescription);
        }

        /// <summary>
        /// 인벤 마약 아이템 id에서 마약 stage를 파싱한다.
        /// 실측 id 체계: "drug_{stage:D2}" (예: drug_01 → 1, drug_10 → 10).
        /// 체계 밖 id(null 포함)는 방어적으로 -1을 반환한다(호출부는 -1을 유효 stage로 사용하지 않음).
        /// </summary>
        public static int ParseDrugStage(string drugItemId)
        {
            const string prefix = "drug_";
            if (string.IsNullOrEmpty(drugItemId) ||
                !drugItemId.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(drugItemId.Substring(prefix.Length), out int stage))
            {
                return -1;
            }
            return stage;
        }
    }
}
