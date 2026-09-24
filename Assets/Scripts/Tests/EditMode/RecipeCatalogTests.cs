using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// 요리 카탈로그(RecipeCatalog, 09-24 신규) 데이터 무결성 검증 — 2024종.
    ///   - 총계/전수 조회, id·이름·조합키 유일성, FindRecipe 정렬집합(순서 무관) 계약,
    ///     한국어 카테고리 매핑 23종, 재료명 스모크 조회, 몬스터 ItemData 팩토리.
    /// EditMode 순수 데이터 테스트 — 씬/컴포넌트 불필요 (RecipeCatalog는 static 전용).
    /// </summary>
    public class RecipeCatalogTests
    {
        // 한국어 카테고리명 23종 (원본 cat_master 계약 순서: 작물 10 / 어류 7 / 몬스터 6)
        static readonly string[] CategoryKo =
        {
            "과일류", "채소류", "뿌리채소류", "엽채류", "곡류", "콩류", "견과류", "허브", "향신료", "음료류",
            "연어·송어류", "등푸른 해양어", "넙치·가자미류", "메기류", "잉어·붕어류", "소형 민물어", "장어류",
            "보통 육류", "파충류·용", "조류·익룡", "점액·피·액체", "살점·잔해", "영묘·뿔·비늘",
        };

        const int ValidCatMax = 22; // enum 23종: 0..22

        static string KeyOfCats(RecipeCatalog.RecipeDef r)
        {
            var vals = new int[r.cats.Length];
            for (int i = 0; i < r.cats.Length; i++) vals[i] = (int)r.cats[i];
            System.Array.Sort(vals);
            return string.Join(",", vals);
        }

        // ─────────────── a. 총계 ───────────────

        [Test]
        public void Catalog_TotalCount_Is2024()
        {
            Assert.AreEqual(2024, RecipeCatalog.TotalCount, "TotalCount 상수");
            var all = RecipeCatalog.All();
            Assert.AreEqual(2024, all.Count, "All() 개수");
            Assert.AreEqual(2024, RecipeCatalog.Recipes.Length, "원본 테이블 개수");
        }

        // ─────────────── b. id / 이름 / 조합키 유일성 ───────────────

        [Test]
        public void AllRecipes_UniqueId_And_UniqueName()
        {
            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            foreach (var r in RecipeCatalog.All())
            {
                Assert.IsFalse(string.IsNullOrEmpty(r.id), $"id 비어있음: {r.name}");
                Assert.IsFalse(string.IsNullOrEmpty(r.name), $"이름 비어있음: {r.id}");
                Assert.IsTrue(ids.Add(r.id), $"id 중복: {r.id}");
                Assert.IsTrue(names.Add(r.name), $"요리명 중복: {r.name}");
            }
            Assert.AreEqual(2024, ids.Count, "유일 id 개수");
            Assert.AreEqual(2024, names.Count, "유일 요리명 개수");
        }

        [Test]
        public void AllRecipes_CatSets_Sorted_Distinct_UniqueKeys()
        {
            // RecipeDef 계약: cats는 오름차순 정렬 + 중복 제거.
            // 조합키(정렬 집합) 중복 = 인덱스 덮어쓰기 → 조회 누락 버그이므로 전수 검증.
            var keys = new HashSet<string>();
            foreach (var r in RecipeCatalog.All())
            {
                Assert.GreaterOrEqual(r.cats.Length, 2, $"재료 수 2~3: {r.id}");
                Assert.LessOrEqual(r.cats.Length, 3, $"재료 수 2~3: {r.id}");
                for (int i = 0; i < r.cats.Length; i++)
                {
                    Assert.GreaterOrEqual((int)r.cats[i], 0, $"카테고리 범위: {r.id}");
                    Assert.LessOrEqual((int)r.cats[i], ValidCatMax, $"카테고리 범위: {r.id}");
                    if (i > 0)
                        Assert.Greater((int)r.cats[i], (int)r.cats[i - 1],
                            $"cats 정렬+중복제거 위반: {r.id} [{r.name}]");
                }
                Assert.IsTrue(keys.Add(KeyOfCats(r)), $"조합키 중복(인덱스 충돌): {r.id} [{r.name}]");
            }
            Assert.AreEqual(2024, keys.Count, "유일 조합키 개수");
        }

        // ─────────────── c/d. FindRecipe 순서 무관 전수 조회 ───────────────

        [Test]
        public void FindRecipe_TwoCat_OrderInsensitive_AllRecipes()
        {
            foreach (var r in RecipeCatalog.All())
            {
                if (r.cats.Length != 2) continue;
                var ab = RecipeCatalog.FindRecipe(r.cats[0], r.cats[1]);
                var ba = RecipeCatalog.FindRecipe(r.cats[1], r.cats[0]);
                Assert.IsTrue(ab.HasValue, $"정방향 조회 실패: {r.id} [{r.name}]");
                Assert.IsTrue(ba.HasValue, $"역방향 조회 실패: {r.id} [{r.name}]");
                Assert.AreEqual(r.id, ab.Value.id, $"정방향 불일치: {r.id}");
                Assert.AreEqual(r.id, ba.Value.id, $"역방향 불일치: {r.id} → {ba.Value.id}");
            }
        }

        [Test]
        public void FindRecipe_ThreeCat_AllResolve_And_OrderInsensitive()
        {
            foreach (var r in RecipeCatalog.All())
            {
                if (r.cats.Length != 3) continue;
                var hit = RecipeCatalog.FindRecipe(r.cats[0], r.cats[1], r.cats[2]);
                Assert.IsTrue(hit.HasValue, $"3재료 조회 실패: {r.id} [{r.name}]");
                Assert.AreEqual(r.id, hit.Value.id, $"3재료 불일치: {r.id} → {hit.Value.id}");

                var rev = RecipeCatalog.FindRecipe(r.cats[2], r.cats[1], r.cats[0]);
                Assert.IsTrue(rev.HasValue && rev.Value.id == r.id, $"3재료 역방향 불일치: {r.id}");
            }
        }

        [Test]
        public void FindRecipe_DuplicateCat_CollapsesToSet()
        {
            // 헤더 계약 예시: (과일류, 과일류, 견과류) → 과일류+견과류 2재료 요리.
            var two = RecipeCatalog.FindRecipe(RecipeCatalog.IngredientCategory.Fruit, RecipeCatalog.IngredientCategory.Nut);
            Assert.IsTrue(two.HasValue, "기준 2재료 조합 존재");
            var dup = RecipeCatalog.FindRecipe(
                RecipeCatalog.IngredientCategory.Fruit,
                RecipeCatalog.IngredientCategory.Fruit,
                RecipeCatalog.IngredientCategory.Nut);
            Assert.IsTrue(dup.HasValue, "중복 카테고리 집합 축소 조회");
            Assert.AreEqual(two.Value.id, dup.Value.id, "축소 결과 = 원 2재료 요리");
        }

        [Test]
        public void FindRecipe_UnknownCombo_ReturnsNull()
        {
            // 미등록 조합: 어류+어류(2재료 조합 테이블에 없음), 몬스터 3중복.
            Assert.IsNull(RecipeCatalog.FindRecipe(
                RecipeCatalog.IngredientCategory.Salmon, RecipeCatalog.IngredientCategory.BlueFish));
            Assert.IsNull(RecipeCatalog.FindRecipe(
                RecipeCatalog.IngredientCategory.Slime,
                RecipeCatalog.IngredientCategory.Slime,
                RecipeCatalog.IngredientCategory.Slime));
        }

        // ─────────────── e. 한국어 카테고리 매핑 23종 ───────────────

        [Test]
        public void CategoryFromKorean_All23Categories_Registered()
        {
            Assert.AreEqual(23, CategoryKo.Length, "계약 카테고리 수");
            var seen = new HashSet<RecipeCatalog.IngredientCategory>();
            for (int i = 0; i < CategoryKo.Length; i++)
            {
                var c = RecipeCatalog.CategoryFromKorean(CategoryKo[i]);
                Assert.AreNotEqual(RecipeCatalog.None, c, $"미등록 카테고리명: {CategoryKo[i]}");
                Assert.GreaterOrEqual((int)c, 0, $"유효 범위 밖: {CategoryKo[i]} → {c}");
                Assert.LessOrEqual((int)c, ValidCatMax, $"유효 범위 밖: {CategoryKo[i]} → {c}");
                Assert.IsTrue(seen.Add(c), $"카테고리명 매핑 충돌: {CategoryKo[i]}");
            }
            Assert.AreEqual(23, seen.Count, "23종 전부 서로 다른 enum으로 1:1 매핑");
        }

        [Test]
        public void CategoryFromKorean_Unknown_ReturnsNone()
        {
            Assert.AreEqual(RecipeCatalog.None, RecipeCatalog.CategoryFromKorean("존재하지않는카테고리"));
            Assert.AreEqual(RecipeCatalog.None, RecipeCatalog.CategoryFromKorean(null));
        }

        // ─────────────── f. 재료명 스모크 조회 ───────────────

        [Test]
        public void CropCategory_Smoke_ValidCategories()
        {
            Assert.AreEqual(RecipeCatalog.IngredientCategory.Fruit, RecipeCatalog.CropCategory("사과"));
            Assert.AreEqual(RecipeCatalog.IngredientCategory.RootVeg, RecipeCatalog.CropCategory("감자"));
            Assert.AreEqual(RecipeCatalog.IngredientCategory.LeafVeg, RecipeCatalog.CropCategory("배추"));
            Assert.AreEqual(RecipeCatalog.IngredientCategory.Legume, RecipeCatalog.CropCategory("콩"));
            Assert.AreEqual(RecipeCatalog.IngredientCategory.Nut, RecipeCatalog.CropCategory("호두"));
        }

        [Test]
        public void FishAndMonsterCategory_Smoke_ValidCategories()
        {
            Assert.AreEqual(RecipeCatalog.IngredientCategory.Salmon, RecipeCatalog.FishCategory("연어"));
            Assert.AreNotEqual(RecipeCatalog.None, RecipeCatalog.FishCategory("메기"));

            Assert.AreEqual(RecipeCatalog.IngredientCategory.RegularMeat, RecipeCatalog.MonsterCategory("토끼고기"));
            Assert.AreNotEqual(RecipeCatalog.None, RecipeCatalog.MonsterCategory("골렘 심장"));

            Assert.AreEqual(RecipeCatalog.None, RecipeCatalog.CropCategory("존재하지않는작물"));
        }

        [Test]
        public void MonsterGroups_23Ingredients_Consistent()
        {
            Assert.AreEqual(6, RecipeCatalog.MonsterGroups.Length, "몬스터 그룹 6종");
            Assert.AreEqual(23, RecipeCatalog.MonsterIngredients.Length, "몬스터 재료 23종");

            var names = new HashSet<string>();
            foreach (var g in RecipeCatalog.MonsterGroups)
            {
                Assert.AreNotEqual(RecipeCatalog.None, g.category,
                    $"그룹 카테고리 유효: {g.categoryKo}");
                Assert.IsFalse(string.IsNullOrEmpty(g.categoryKo));
                Assert.Greater(g.names.Length, 0);
                foreach (var n in g.names)
                {
                    Assert.IsTrue(names.Add(n), $"몬스터 재료명 중복: {n}");
                    Assert.AreNotEqual(RecipeCatalog.None, RecipeCatalog.MonsterCategory(n), $"역방향 매핑 누락: {n}");
                    Assert.AreNotEqual(RecipeCatalog.None, RecipeCatalog.CategoryFromKorean(g.categoryKo), $"그룹 카테고리명 매핑 누락: {g.categoryKo}");
                }
            }
            Assert.AreEqual(23, names.Count, "유일 몬스터 재료명 23종");
        }

        // ─────────────── g. 몬스터 ItemData 팩토리 ───────────────

        [Test]
        public void MonsterMeatItem_ProducesValidItemData()
        {
            var seenIds = new HashSet<string>();
            foreach (var g in RecipeCatalog.MonsterGroups)
            {
                foreach (var n in g.names)
                {
                    var item = RecipeCatalog.MonsterMeatItem(g.categoryKo, n);
                    Assert.IsNotNull(item, $"ItemData 생성: {n}");
                    Assert.IsNotEmpty(item.id, $"id 비어있음: {n}");
                    Assert.IsTrue(item.id.StartsWith("monster_meat_"), $"id 접두 규약: {item.id}");
                    Assert.IsNotEmpty(item.displayName, $"표시명 비어있음: {item.id}");
                    Assert.AreEqual(n, item.displayName, $"표시명 = 재료명: {item.id}");
                    Assert.AreEqual(PlayerInventory.ItemCategory.Meat, item.category, $"카테고리 Meat: {item.id}");
                    Assert.Greater(item.maxStack, 0, $"maxStack 양수: {item.id}");
                    Assert.IsTrue(seenIds.Add(item.id), $"ItemData id 중복: {item.id}");
                }
            }
            Assert.AreEqual(23, seenIds.Count, "23종 전부 유일 id");
        }
    }
}
