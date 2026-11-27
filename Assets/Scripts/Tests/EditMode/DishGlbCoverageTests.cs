using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEditor;
using UnityEngine;
using IngCat = ProjectName.Systems.RecipeCatalog.IngredientCategory;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [172차] 왕실 요리 dish GLB 버킷 배선 계약 — RecipeCatalog 2,024종 전수 커버 고정.
    /// 배경: dish_001~dish_2024는 공유 버킷 GLB로 배선된다(전용 파일 생성 금지) —
    ///   DishGlbCatalog.TypeBuckets 요리명 어미 매칭(신규 14키) + 기존 재사용 5키
    ///   (soup/tempura/grilled_skin/yakitori_skewer/puding), CatHues 재료 hue 틴트로 시각 구별.
    /// 목적: ① 버킷 파일 실존 ② 어미 파서 대표 케이스(긴 어미 우선 포함) ③ 2,024종 전수
    ///   ModelKey 해석(미래 신규 어미 추가 시 LOUD fail) ④ 반환 키 전체 실로드(오타 방지)
    ///   ⑤ 틴트 결정론 ⑥ 아이콘/월드 체인 ⑦ 일반 음식 격차 보완분(fruit_apple_seed/meat_horse).
    /// </summary>
    public class DishGlbCoverageTests
    {
        /// <summary>[172차] 신규 수용 dish 버킷 GLB 14종 — UserProvided 파일명(소문자 계약).</summary>
        private static readonly string[] NewDishBuckets =
        {
            "dish_braise", "dish_jelly", "dish_jeon", "dish_pickle", "dish_pie",
            "dish_ricebowl", "dish_roll", "dish_salad", "dish_sashimi", "dish_steak",
            "dish_steam", "dish_stew", "dish_stirfry", "dish_tea",
        };

        /// <summary>[172차] 기존 재사용 버킷 5키 — 파일 신규 복제 금지(UserProvided 원본 직결).</summary>
        private static readonly string[] ReusedBuckets =
        {
            "soup", "tempura", "grilled_skin", "yakitori_skewer", "puding",
        };

        // ================================================================
        //  1) 버킷 파일 실존
        // ================================================================

        [Test]
        public void BucketFiles_Exist()
        {
            foreach (var key in NewDishBuckets)
            {
                var glb = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Models/UserProvided/" + key + ".glb");
                Assert.That(glb, Is.Not.Null, "[172] 신규 dish 버킷 GLB: " + key);
                Assert.That(RuntimeModelLoader.HasModel(key), Is.True,
                    "[172] 신규 dish 버킷 실로드 가능: " + key);
            }
            foreach (var key in ReusedBuckets)
            {
                Assert.That(RuntimeModelLoader.HasModel(key), Is.True,
                    "[172] 기존 재사용 버킷 로드 가능: " + key);
            }
        }

        // ================================================================
        //  2) 어미 파서 대표 케이스 (id는 RecipeCatalog에서 이름으로 주입)
        // ================================================================

        /// <summary>요리명 어미 → dish id(RecipeCatalog.Recipes 이름 역검색). 미발견 null(호출처 어서션).</summary>
        private static string FindIdByNameSuffix(string suffix)
        {
            foreach (var r in RecipeCatalog.Recipes)
                if (!string.IsNullOrEmpty(r.name)
                    && r.name.EndsWith(suffix, System.StringComparison.Ordinal))
                    return r.id;
            return null;
        }

        /// <summary>dish id → 요리명 역검색. 미등록 null.</summary>
        private static string NameById(string dishId)
        {
            foreach (var r in RecipeCatalog.Recipes)
                if (r.id == dishId)
                    return r.name;
            return null;
        }

        /// <summary>대표 케이스 일괄 어서션 — 원본 요리 존재 + 버킷 해석 일치.</summary>
        private static void AssertBucketForNameSuffix(string suffix, string expectedBucket)
        {
            string id = FindIdByNameSuffix(suffix);
            Assert.That(id, Is.Not.Null,
                "[172] 어미 '" + suffix + "' 요리 원본 존재(카탈로그 훼손 검사).");
            Assert.That(DishGlbCatalog.ModelKey(id), Is.EqualTo(expectedBucket),
                "[172] 어미 '" + suffix + "' 파싱(" + id + ")");
        }

        [Test]
        public void TypeParser_KeyCases()
        {
            // dish_001 = "사과 가지 볶음" — id 직결 케이스(요리명 카탈로그 계약 동시 고정).
            Assert.That(NameById("dish_001"), Is.EqualTo("사과 가지 볶음"),
                "[172] dish_001 원본 요리명.");
            Assert.That(DishGlbCatalog.ModelKey("dish_001"), Is.EqualTo("dish_stirfry"),
                "[172] dish_001(사과 가지 볶음) 버킷.");

            AssertBucketForNameSuffix("탕", "soup");             // 1자 어미 → 기존 키 재사용
            AssertBucketForNameSuffix("볶음밥", "dish_stirfry"); // 긴 어미 우선 — 볶음에 가려지지 않음
            AssertBucketForNameSuffix("볶음", "dish_stirfry");   // 볶음밥과 동일 버킷(공유 계약)
            AssertBucketForNameSuffix("스테이크", "dish_steak");
            // [172차 QA] 카탈로그 실측 — '김치' 어미 요리 0종, pickle 버킷은 절임(19)/젤(11)/겉절이(1)로 커버.
            AssertBucketForNameSuffix("절임", "dish_pickle");
            AssertBucketForNameSuffix("전", "dish_jeon");
            AssertBucketForNameSuffix("사시미", "dish_sashimi");
            AssertBucketForNameSuffix("물회", "dish_sashimi");
        }

        // ================================================================
        //  3) 2,024종 전수 해석 — 미래 신규 어미 LOUD fail 계약
        // ================================================================

        [Test]
        public void FullCatalog_2024_AllResolved()
        {
            Assert.That(RecipeCatalog.Recipes.Length, Is.EqualTo(2024),
                "[172] 왕실 요리 카탈로그 총수 계약(증설 시 본 테스트와 버킷 확장 동시 갱신).");

            var unresolved = new System.Collections.Generic.List<string>();
            foreach (var r in RecipeCatalog.Recipes)
            {
                if (string.IsNullOrEmpty(DishGlbCatalog.ModelKey(r.id)))
                    unresolved.Add(r.id + "(" + r.name + ")");
            }
            string detail = unresolved.Count <= 12
                ? string.Join(", ", unresolved)
                : string.Join(", ", unresolved.GetRange(0, 12)) + " …외 "
                  + (unresolved.Count - 12) + "종";
            Assert.That(unresolved, Is.Empty,
                "[172] 미배선 dish " + unresolved.Count + "종 — TypeBuckets 확장 필요: " + detail);
        }

        // ================================================================
        //  4) 반환 버킷 키 전체 실로드 — 오타/파일 부재 원천 차단
        // ================================================================

        [Test]
        public void BucketKeys_AllHaveFiles()
        {
            var expected = new System.Collections.Generic.HashSet<string>(NewDishBuckets);
            foreach (var k in ReusedBuckets)
                expected.Add(k);

            var keys = new System.Collections.Generic.HashSet<string>();
            foreach (var r in RecipeCatalog.Recipes)
            {
                string k = DishGlbCatalog.ModelKey(r.id);
                if (!string.IsNullOrEmpty(k))
                    keys.Add(k);
            }
            Assert.That(keys.Count, Is.GreaterThan(0), "[172] 전수 매칭으로 버킷 키 노출.");

            foreach (var k in keys)
            {
                Assert.That(expected.Contains(k), Is.True,
                    "[172] 계약 외 버킷 키(오타 또는 미등록 신규키 — 테스트 갱신 필요): " + k);
                Assert.That(RuntimeModelLoader.HasModel(k), Is.True,
                    "[172] 버킷 키 실로드 불가(오타/파일 부재): " + k);
            }
        }

        // ================================================================
        //  5) 틴트 결정론 — EditMode 순수 경로(엔진 콜 없이 성립이 곧 증명)
        // ================================================================

        [Test]
        public void Tint_Determinism()
        {
            var fv = new[] { IngCat.Fruit, IngCat.Veg };
            var meatFish = new[] { IngCat.RegularMeat, IngCat.Salmon };

            Color a = DishGlbCatalog.TintFor(fv);
            Assert.That(DishGlbCatalog.TintFor(new[] { IngCat.Fruit, IngCat.Veg }),
                Is.EqualTo(a), "[172] 동일 cats → 동일 틴트(결정론).");
            Assert.That(DishGlbCatalog.TintFor(new[] { IngCat.Veg, IngCat.Fruit }),
                Is.EqualTo(a), "[172] 순서 무관(원평균 결정론).");

            Assert.That(DishGlbCatalog.TintFor(meatFish), Is.Not.EqualTo(a),
                "[172] 육+어 vs 과채 hue 상이(시각 구별 계약).");
            Assert.That(a, Is.Not.EqualTo(Color.white),
                "[172] 등록 cats는 무티ント(white)가 아님.");

            Assert.That(DishGlbCatalog.TintFor(null), Is.EqualTo(Color.white),
                "[172] cats null → white(틴트 없음).");
            Assert.That(DishGlbCatalog.TintFor(new IngCat[0]), Is.EqualTo(Color.white),
                "[172] cats 빈 → white.");
            Assert.That(DishGlbCatalog.TintFor(new[] { RecipeCatalog.None }),
                Is.EqualTo(Color.white), "[172] 미등록 센티널 → white.");
        }

        // ================================================================
        //  6) 아이콘 체인 — GblItemIconRenderer dish 분기
        // ================================================================

        [Test]
        public void IconChain_Dish001_ResolvesStirfryBucket()
        {
            string name = NameById("dish_001");
            Assert.That(name, Is.EqualTo("사과 가지 볶음"), "[172] dish_001 요리명 동적 확인.");
            var item = new PlayerInventory.ItemData { id = "dish_001", displayName = name };
            string key = ProjectName.UI.GblItemIconRenderer.ResolveModelKey(item);
            Assert.That(key, Is.EqualTo("dish_stirfry"), "[172] 아이콘 체인 dish 버킷 해석.");
            Assert.That(RuntimeModelLoader.HasModel(key), Is.True,
                "[172] 아이콘 체인 실로드 가능: " + key);
        }

        // ================================================================
        //  7) 월드 체인 — AcquisitionItemVisualResolver dish 분기
        // ================================================================

        [Test]
        public void WorldChain_Dish001_EditorPathEndsWithBucketGlb()
        {
            var item = new PlayerInventory.ItemData
            {
                id = "dish_001",
                displayName = NameById("dish_001") ?? "dish_001",
            };
            string path = AcquisitionItemVisualResolver.ResolveEditorAssetPath(item);
            Assert.That(path, Is.Not.Null, "[172] 월드 체인 에디터 경로 해석.");
            Assert.That(path, Does.EndWith("dish_stirfry.glb"),
                "[172] 월드 체인 경로가 버킷 GLB 지정: " + path);
        }

        // ================================================================
        //  8) 일반 음식 격차 보완분 — 사과 씨앗 + 말고기
        // ================================================================

        [Test]
        public void GeneralFoodGap_Wired()
        {
            var appleSeed = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/UserProvided/fruit_apple_seed.glb");
            Assert.That(appleSeed, Is.Not.Null, "[172] 일반 음식 격차 보완 — 사과 씨앗 GLB.");
            var horse = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/UserProvided/meat_horse.glb");
            Assert.That(horse, Is.Not.Null, "[172] 일반 음식 격차 보완 — 말고기 GLB.");

            Assert.That(RuntimeModelLoader.HasModel("fruit_apple_seed"), Is.True,
                "[172] 사과 씨앗 로드 가능.");
            Assert.That(RuntimeModelLoader.HasModel("meat_horse"), Is.True,
                "[172] 말고기 로드 가능.");

            // [172b] 가죽 체인 — 신장랙/저장고 파생: 멧돼지 가죽(flap 원본 탄색)·말가죽(flap 다크 틴트)·말 안장(rack에서 saddle 서브트리 분리).
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/UserProvided/boar_leather.glb"), Is.Not.Null, "[172b] 멧돼지 가죽 GLB.");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/UserProvided/horse_leather.glb"), Is.Not.Null, "[172b] 말가죽 GLB.");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/UserProvided/saddle.glb"), Is.Not.Null, "[172b] 말 안장 GLB.");
            Assert.That(RuntimeModelLoader.HasModel("boar_leather"), Is.True, "[172b] 멧돼지 가죽 로드 가능.");
            Assert.That(RuntimeModelLoader.HasModel("horse_leather"), Is.True, "[172b] 말가죽 로드 가능.");
            Assert.That(RuntimeModelLoader.HasModel("saddle"), Is.True, "[172b] 말 안장 로드 가능.");

            // [172L] 가죽 질감 파생 — 랙 가죽(모틀링+에지 다크닝+러프니스) 색 변형 2종 + bone 파생 2종
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/UserProvided/rabbit_fur.glb"), Is.Not.Null, "[172L] 토끼털 GLB.");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/UserProvided/wolf_fur.glb"), Is.Not.Null, "[172L] 늑대모피 GLB.");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/UserProvided/boar_tusk.glb"), Is.Not.Null, "[172L] 멧돼지 엄니 GLB.");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/UserProvided/wolf_tooth.glb"), Is.Not.Null, "[172L] 늑대 이빨 GLB.");
            Assert.That(RuntimeModelLoader.HasModel("rabbit_fur"), Is.True, "[172L] 토끼털 로드 가능.");
            Assert.That(RuntimeModelLoader.HasModel("wolf_fur"), Is.True, "[172L] 늑대모피 로드 가능.");
            Assert.That(RuntimeModelLoader.HasModel("boar_tusk"), Is.True, "[172L] 멧돼지 엄니 로드 가능.");
            Assert.That(RuntimeModelLoader.HasModel("wolf_tooth"), Is.True, "[172L] 늑대 이빨 로드 가능.");
        }
    }
}
