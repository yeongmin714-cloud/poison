using NUnit.Framework;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [113차] 영주 선호 음식 카탈로그 계약 — 계열 매칭(exact id 집합)/티어/재료 힌트.
    /// 실측 배경: 선호 음식 10종은 인벤 미실장 → "선호 음식 일치"는 재료 계열(소속) 판정.
    /// 씨앗(*_seed)은 exact id 집합 밖이라 오매칭되지 않는다. 미등록 음식(붉은 포도주)은 T3 폴백.
    /// </summary>
    [TestFixture]
    public class LordFoodCatalogTests
    {
        // ═══════════ 티어 계약 — GenerateLordInfo 링 가중 슬롯과 정합(T1 4종/T2 3종/T3 3종) ═══════════

        [Test]
        public void Tier1Foods_ReturnTier1()
        {
            Assert.AreEqual(LordFoodCatalog.Tier1, LordFoodCatalog.GetTier("구운 고기"), "T1: 구운 고기");
            Assert.AreEqual(LordFoodCatalog.Tier1, LordFoodCatalog.GetTier("야채 샐러드"), "T1: 야채 샐러드");
            Assert.AreEqual(LordFoodCatalog.Tier1, LordFoodCatalog.GetTier("빵과 치즈"), "T1: 빵과 치즈");
            Assert.AreEqual(LordFoodCatalog.Tier1, LordFoodCatalog.GetTier("버섯 수프"), "T1: 버섯 수프");
        }

        [Test]
        public void Tier2Foods_ReturnTier2()
        {
            Assert.AreEqual(LordFoodCatalog.Tier2, LordFoodCatalog.GetTier("생선 스튜"), "T2: 생선 스튜");
            Assert.AreEqual(LordFoodCatalog.Tier2, LordFoodCatalog.GetTier("로스트 치킨"), "T2: 로스트 치킨");
            Assert.AreEqual(LordFoodCatalog.Tier2, LordFoodCatalog.GetTier("포도주"), "T2: 포도주");
        }

        [Test]
        public void Tier3Foods_ReturnTier3()
        {
            Assert.AreEqual(LordFoodCatalog.Tier3, LordFoodCatalog.GetTier("꿀 케이크"), "T3: 꿀 케이크");
            Assert.AreEqual(LordFoodCatalog.Tier3, LordFoodCatalog.GetTier("양고기 스테이크"), "T3: 양고기 스테이크");
            Assert.AreEqual(LordFoodCatalog.Tier3, LordFoodCatalog.GetTier("해산물 파이"), "T3: 해산물 파이");
        }

        [Test]
        public void UnregisteredFood_FallsBackToTier3()
        {
            // 미등록(붉은 포도주 — 드라큘라 백작 전용) = 특이 음식은 최고급(T3) 폴백 — 무난화 금지 계약
            Assert.AreEqual(LordFoodCatalog.Tier3, LordFoodCatalog.GetTier("붉은 포도주"),
                "미등록 음식은 T3 폴백(최고급 취급)");
            Assert.AreEqual(LordFoodCatalog.Tier3, LordFoodCatalog.GetTier(null), "null도 T3 폴백");
        }

        // ═══════════ 계열 매칭 — IsPreferredGift(선호 음식명, 지급 아이템 id) ═══════════

        [Test]
        public void MeatAffinityLords_MatchMeatItems()
        {
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("구운 고기", "meat_rabbit"), "구운 고기 ← 토끼고기");
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("구운 고기", "meat_boar"), "구운 고기 ← 멧돼지고기");
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("로스트 치킨", "meat_wolf"), "로스트 치킨 ← 늑대고기(고기 계열 대체)");
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("양고기 스테이크", "meat_boar"), "양고기 스테이크 ← 멧돼지고기(고기 계열 대체)");
        }

        [Test]
        public void SeaAffinityLords_MatchFishItems()
        {
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("생선 스튜", "fish_common"), "생선 스튜 ← 일반 생선");
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("생선 스튜", "fish_legendary"), "생선 스튜 ← 전설 생선");
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("해산물 파이", "fish_rare"), "해산물 파이 ← 희귀 생선(어패 계열 대체)");
        }

        [Test]
        public void ProduceAffinityLord_MatchesCropsAndFruit()
        {
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("야채 샐러드", "crop_carrot"), "야채 샐러드 ← 당근");
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("야채 샐러드", "crop_corn"), "야채 샐러드 ← 옥수수");
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("야채 샐러드", "crop_pumpkin"), "야채 샐러드 ← 호박");
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("야채 샐러드", "crop_radish"), "야채 샐러드 ← 무");
            Assert.IsTrue(LordFoodCatalog.IsPreferredGift("야채 샐러드", "fruit_apple"), "야채 샐러드 ← 사과");
        }

        [Test]
        public void CrossAffinity_DoesNotMatch()
        {
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("생선 스튜", "meat_boar"), "어패 선호에 고기 지급 = 미일치");
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("야채 샐러드", "fish_common"), "채소 선호에 생선 지급 = 미일치");
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("구운 고기", "fish_common"), "고기 선호에 생선 지급 = 미일치");
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("구운 고기", "crop_carrot"), "고기 선호에 작물 지급 = 미일치");
        }

        [Test]
        public void Seeds_NeverMatch_ProduceAffinity()
        {
            // exact id 집합 계약 — 씨앗(*_seed)은 재료가 아니므로 오매칭 금지
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("야채 샐러드", "crop_carrot_seed"), "당근씨는 야채 샐러드 재료가 아님");
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("야채 샐러드", "crop_corn_seed"), "옥수수씨 오매칭 금지");
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("야채 샐러드", "crop_pumpkin_seed"), "호박씨 오매칭 금지");
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("야채 샐러드", "crop_radish_seed"), "무씨 오매칭 금지");
        }

        [Test]
        public void UnimplementedIngredientFoods_NeverMatch()
        {
            // 재료 미실장(빵과 치즈·버섯 수프·포도주·꿀 케이크) — 어떤 실장 음식으로도 일치 판정 금지
            string[] obtainableIds = { "meat_rabbit", "meat_boar", "meat_wolf", "fish_common", "fish_rare", "fish_legendary", "crop_carrot", "crop_corn", "crop_pumpkin", "crop_radish", "fruit_apple" };
            string[] unimplementedFoods = { "빵과 치즈", "버섯 수프", "포도주", "꿀 케이크" };
            foreach (string food in unimplementedFoods)
                foreach (string id in obtainableIds)
                    Assert.IsFalse(LordFoodCatalog.IsPreferredGift(food, id), $"{food} ← {id}: 재료 미실장 음식은 매칭 불가");
            // 미등록(붉은 포도주)도 매칭 불가
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("붉은 포도주", "meat_boar"), "미등록 음식 매칭 불가");
        }

        [Test]
        public void NullOrEmptyArguments_ReturnFalse()
        {
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift(null, "meat_boar"), "선호 음식 null");
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("구운 고기", null), "아이템 id null");
            Assert.IsFalse(LordFoodCatalog.IsPreferredGift("", ""), "빈 문자열");
        }

        // ═══════════ 재료 실장 여부 + 주재료 (B 힌트 조립 재료) ═══════════

        [Test]
        public void IngredientObtainable_FollowsAffinityPresence()
        {
            Assert.IsTrue(LordFoodCatalog.IsIngredientObtainable("구운 고기"), "고기 계열 = 조달 가능");
            Assert.IsTrue(LordFoodCatalog.IsIngredientObtainable("해산물 파이"), "어패 계열 = 조달 가능(낚시)");
            Assert.IsFalse(LordFoodCatalog.IsIngredientObtainable("빵과 치즈"), "밀·우유 미실장");
            Assert.IsFalse(LordFoodCatalog.IsIngredientObtainable("꿀 케이크"), "꿀·계란 미실장");
            Assert.IsFalse(LordFoodCatalog.IsIngredientObtainable("붉은 포도주"), "미등록 = 미실장 취급");
        }

        [Test]
        public void MainIngredients_NamedPerFood()
        {
            StringAssert.Contains("해산물", LordFoodCatalog.GetMainIngredients("해산물 파이"), "해산물 파이 주재료");
            StringAssert.Contains("꿀", LordFoodCatalog.GetMainIngredients("꿀 케이크"), "꿀 케이크 주재료");
            StringAssert.Contains("버섯", LordFoodCatalog.GetMainIngredients("버섯 수프"), "버섯 수프 주재료");
            StringAssert.Contains("불명", LordFoodCatalog.GetMainIngredients("붉은 포도주"), "미등록은 정직 폴백");
        }

        // ═══════════ [113차 B] 인텔 재료 힌트 — 정직성 계약(미실장 획득처 금지) ═══════════

        [Test]
        public void SourceHint_ObtainableFoods_NamedProcurementPath()
        {
            StringAssert.Contains("재료 힌트", LordFoodCatalog.BuildSourceHint("구운 고기"), "힌트 접두 계약");
            StringAssert.Contains("사냥", LordFoodCatalog.BuildSourceHint("구운 고기"), "고기 계열 = 사냥");
            StringAssert.Contains("낚시", LordFoodCatalog.BuildSourceHint("생선 스튜"), "어패 계열 = 낚시");
            StringAssert.Contains("농경", LordFoodCatalog.BuildSourceHint("야채 샐러드"), "채소·과일 계열 = 농경·채집");
            StringAssert.Contains("낚시", LordFoodCatalog.BuildSourceHint("해산물 파이"), "T3 어패도 낚시 대체 조달");
        }

        [Test]
        public void SourceHint_UnimplementedFoods_HonestNoSource()
        {
            StringAssert.Contains("미실장", LordFoodCatalog.BuildSourceHint("빵과 치즈"), "밀·우유 미실장 — 없는 획득처를 말하지 않는다");
            StringAssert.Contains("미실장", LordFoodCatalog.BuildSourceHint("꿀 케이크"), "꿀·계란 미실장");
            StringAssert.Contains("재료 힌트", LordFoodCatalog.BuildSourceHint("붉은 포도주"), "미등록도 힌트 라인 형식 유지");
            StringAssert.Contains("미실장", LordFoodCatalog.BuildSourceHint("붉은 포도주"), "미등록 = 미실장 취급");
        }
    }
}