using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// D3-A2 요리 마약 확장 — CookingDrugHelper 규약 검증 (EditMode).
    ///
    /// [QA-판정 선언] 본 테스트 파일은 구현 서브에이전트(D3-A2)가 작성했으며,
    /// 실행 여부/통과 판정은 별도 QA 서브에이전트가 수행한다(위임 정책 §1: 구현/QA 분리).
    /// 구현 에이전트는 Unity를 실행하지 않았다(정책 §3-4 준수 선언).
    ///
    /// 참고: CookingDatabase.GetCooking은 Resources(GAME_DATA.md) 런타임 로딩 의존이 있어
    /// 본 테스트에서 조회 회귀를 다루지 않는다 — 규약 검증에 필요한 CookingResult는
    /// public 생성자로 직접 구성한다(일반 요리 결과 무변경은 Helper가 파생 문자열만
    /// 생성한다는 구조로 보장).
    /// </summary>
    public sealed class CookingDrugTests
    {
        /// <summary>규약 검증용 기반 요리 결과 고정 fixture (Random/시간 의존 없음).</summary>
        private static CookingResult MakeBaseFood()
        {
            return new CookingResult(
                "cook_01",
                "토끼 허브 구이",
                "토끼고기와 허브로 만든 구이.",
                "체력 회복 +20");
        }

        // ── 1. MakeDruggedFood 규약 — id / displayName / description 마커 ──

        [Test]
        public void MakeDruggedFood_FollowsNamingContract()
        {
            var (itemId, displayName, description) =
                CookingDrugHelper.MakeDruggedFood(MakeBaseFood(), 3);

            // itemId = "drugged_{dishId}"
            Assert.That(itemId, Is.EqualTo("drugged_cook_01"));
            // displayName = "마약 뿌려진 {dishName}"
            Assert.That(displayName, Is.EqualTo("마약 뿌려진 토끼 허브 구이"));
            // description = {기존} + " [마약: stage n]" — 공백 1칸으로 부착
            Assert.That(description, Is.EqualTo("토끼고기와 허브로 만든 구이. [마약: stage 3]"));
        }

        [Test]
        public void MakeDruggedFood_DoesNotMutateBaseFood()
        {
            var baseFood = MakeBaseFood();
            string idBefore = baseFood.DishId;
            string nameBefore = baseFood.DishName;
            string descBefore = baseFood.Description;

            CookingDrugHelper.MakeDruggedFood(baseFood, 5);

            // 기반 요리의 DishId/DishName/Description 불변 — 파생 문자열만 생성
            Assert.That(baseFood.DishId, Is.EqualTo(idBefore), "DishId 불변");
            Assert.That(baseFood.DishName, Is.EqualTo(nameBefore), "DishName 불변");
            Assert.That(baseFood.Description, Is.EqualTo(descBefore), "Description 불변");
            Assert.That(baseFood.DishId, Is.EqualTo("cook_01"));
            Assert.That(baseFood.DishName, Is.EqualTo("토끼 허브 구이"));
        }

        // ── 2. ParseDrugStage — 실측 id 체계 "drug_{stage:D2}" 파싱 + 이상 형식 방어 ──

        [TestCase("drug_01", 1)]
        [TestCase("drug_03", 3)]
        [TestCase("drug_10", 10)]
        public void ParseDrugStage_ParsesTwoDigitStageIds(string drugItemId, int expectedStage)
        {
            // 실측: DrugEffectSystem.CreateDrugItem → id = $"drug_{stage:D2}"
            Assert.That(CookingDrugHelper.ParseDrugStage(drugItemId), Is.EqualTo(expectedStage));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("herb_red")]
        [TestCase("drug_")]
        [TestCase("drugXX")]
        [TestCase("drug_abc")]
        public void ParseDrugStage_DefendsAgainstMalformedIds(string drugItemId)
        {
            // 방어 규약: 체계 밖 id(null/빈값/접두사 불일치/숫자 아님)는 음수(-1) 반환.
            // 호출부(CookingUI)는 음수를 stage 0으로 치환하여 마커가 명시적으로 남도록 방어.
            Assert.That(CookingDrugHelper.ParseDrugStage(drugItemId), Is.LessThan(0),
                "이상 형식은 유효 stage(>=0)가 아니어야 한다");
        }

        // ── 3. stage 전달 — 마약 stage별 마커 차이 ──

        [Test]
        public void MakeDruggedFood_MarkerDiffersByDrugStage()
        {
            var baseFood = MakeBaseFood();
            var stage1 = CookingDrugHelper.MakeDruggedFood(baseFood, 1);
            var stage10 = CookingDrugHelper.MakeDruggedFood(baseFood, 10);

            Assert.That(stage1.description, Does.Contain("[마약: stage 1]"));
            Assert.That(stage10.description, Does.Contain("[마약: stage 10]"));
            Assert.That(stage1.description, Is.Not.EqualTo(stage10.description),
                "stage가 다르면 description 마커가 달라야 한다");

            // itemId/displayName은 stage와 무관하게 동일 규약 유지
            Assert.That(stage1.itemId, Is.EqualTo(stage10.itemId));
            Assert.That(stage1.displayName, Is.EqualTo(stage10.displayName));
        }

        // ── 4. UI 호출부 방어 규약 — ParseDrugStage 실패(-1) → 0 치환 시 마커 명시 ──

        [Test]
        public void StageZeroDefense_ProducesExplicitMarker()
        {
            // CookingUI.TryCook의 방어 흐름: ParseDrugStage < 0 → 0 치환 후 전달.
            // 이때 마커가 생략되지 않고 "stage 0"으로 명시되어야 한다.
            var (_, _, description) = CookingDrugHelper.MakeDruggedFood(MakeBaseFood(), 0);

            Assert.That(description, Is.EqualTo("토끼고기와 허브로 만든 구이. [마약: stage 0]"));
        }
    }
}
