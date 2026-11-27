using System.Collections.Generic;
using UnityEngine;
// [172차] RecipeCatalog의 중첩 타입 별칭 — 23카테고리/레시피 정의 가독성 유지.
using IngredientCategory = ProjectName.Systems.RecipeCatalog.IngredientCategory;
using RecipeDef = ProjectName.Systems.RecipeCatalog.RecipeDef;

namespace ProjectName.Systems
{
    /// <summary>
    /// [172차] 왕실 요리 GLB 버킷 카탈로그(static) — dish_001~dish_2024 (2,024종).
    ///
    /// 배선 계약(버킷 공유 — 2,024종 전용 파일 생성 금지):
    ///  - ModelKey(dishId): RecipeCatalog.Recipes에서 요리명 조회 → 요리명 "어미"가 TypeBuckets 접미와
    ///    EndsWith 매칭되면 해당 GLB 버킷 키 반환. 버킷은 17종으로 공유한다(요리 유형별 대표 모델).
    ///      · 기존 5키(파일 신규 복제 금지 — UserProvided GLB 원본 직결): soup / tempura /
    ///        grilled_skin / yakitori_skewer / puding
    ///      · 신규 12키(수용된 dish_*.glb): dish_stirfry / dish_stew / dish_steam / dish_steak /
    ///        dish_braise / dish_tea / dish_roll / dish_salad / dish_jelly / dish_ricebowl /
    ///        dish_pie / dish_pickle
    ///  - 미매칭 어미(사시미/카르파초/물회/포케/훈제/전/나물 등) → null 반환 → 호출처 절차 아이콘
    ///    폴백 유지(커버리지 테스트가 향후 신규 유형 어미를 잡아 TypeBuckets 확장).
    ///  - TintFor(CatsFor): RecipeDef.cats(재료 카테고리) → CatHues hue 각도평균(원평균) 틴트 색.
    ///    재료 구성이 다른 요리가 같은 버킷 GLB를 공유해도 틴트로 시각 구별.
    ///  - ApplyTint: MaterialPropertyBlock _BaseColor 곱셈만 사용 — rend.material 인스턴스 생성 금지
    ///    (아이콘 베이크 임시노드 머티리얼 누수 방지).
    /// </summary>
    public static class DishGlbCatalog
    {
        /// <summary>왕실 요리 아이템 id 접두 (dish_001 ~ dish_2024).</summary>
        public const string DishPrefix = "dish_";

        /// <summary>
        /// 요리명 어미 → GLB 버킷 키. <b>긴 어미 우선 정렬</b>(볶음밥이 볶음보다 먼저 —
        /// EndsWith 직렬 매칭이므로 짧은 어미가 먼저 오면 긴 어미가 가려진다).
        /// 버킷 키 17종은 Assets/Resources/Models/UserProvided/ 실측 파일명과 정확 일치(소문자 —
        /// 빌드 대소문자 구분, RuntimeModelLoader 검색경로 Models/UserProvided 직결).
        /// </summary>
        public static readonly (string suffix, string bucket)[] TypeBuckets =
        {
            // ── 4자 어미 ──
            ("스테이크", "dish_steak"),
            // ── 3자 어미 ──
            ("볶음밥", "dish_stirfry"),
            ("리조또", "dish_stirfry"),
            ("샐러드", "dish_salad"),
            ("비빔밥", "dish_salad"),
            ("사시미", "dish_sashimi"),
            // ── 2자 어미 ──
            ("볶음", "dish_stirfry"),
            ("수프", "soup"),
            ("스프", "soup"),      // [172차 보강] 수프 동의어
            ("찌개", "dish_stew"),
            ("전골", "dish_stew"),
            ("스튜", "dish_stew"),
            ("소스", "dish_stew"), // [172차 보강] 소스 마무리 요리
            ("튀김", "tempura"),
            ("조림", "dish_braise"),
            ("구이", "grilled_skin"),
            ("훈제", "grilled_skin"), // [172차 보강] 훈제 = 연기 조리
            ("고기", "grilled_skin"), // [172차 보강] 조리 고기 요리
            ("꼬치", "yakitori_skewer"),
            ("무침", "dish_salad"),
            ("푸딩", "puding"),
            ("젤리", "dish_jelly"),
            ("무스", "dish_jelly"),   // [172차 보강] 무스 = 젤리 계열
            ("덮밥", "dish_ricebowl"),
            ("파이", "dish_pie"),
            ("쿠키", "dish_pie"),
            ("김치", "dish_pickle"),
            ("절임", "dish_pickle"),  // [172차 보강] 절임 = 피클 계열
            ("젤", "dish_pickle"),    // [172차 보강] 겉절이 계열
            ("파초", "dish_sashimi"), // [172차 보강] 카르파초 = 생육
            ("물회", "dish_sashimi"), // [172차 보강] 회 = 생육
            ("포케", "dish_sashimi"), // [172차 보강] 포케볼 = 생육
            // ── 1자 어미 ──
            ("탕", "soup"),
            ("국", "soup"),
            ("죽", "soup"),
            ("찜", "dish_steam"),
            ("차", "dish_tea"),
            ("롤", "dish_roll"),
            ("전", "dish_jeon"), // [172차 보강] 전(186종) = 부침 — dish_jeon.glb(오믈렛 계열)
            // ── 특이 어미 (전수 커버 — 172차 잔여 8건 소화) ──
            ("겉절이", "dish_pickle"), // 무김치 계열
            ("나물", "dish_salad"),    // 나물 무침 계열
            ("라때", "soup"),          // 라멘 국물 계열
            ("도우", "dish_pie"),      // 피자 도우 = 반죽 베이스
            ("냐코타", "dish_stew"),   // 이탈리안 조림 계열
            ("시즈닝", "dish_stew"),   // 소스류
            ("드레싱", "dish_stew"),   // 소스류
        };

        /// <summary>재료 카테고리 23종 → 틴트 hue(도). RecipeCatalog.IngredientCategory 선언 순서와 무관
        /// 이름 매칭(카탈로그 enum 재배열에도 안전).</summary>
        public static readonly (IngredientCategory cat, float hueDeg)[] CatHues =
        {
            // 작물 (crop_cat)
            (IngredientCategory.Fruit, 350f),      // 과일류
            (IngredientCategory.Veg, 120f),        // 채소류
            (IngredientCategory.RootVeg, 30f),     // 뿌리채소류
            (IngredientCategory.LeafVeg, 90f),     // 엽채류
            (IngredientCategory.Grain, 50f),       // 곡류
            (IngredientCategory.Legume, 20f),      // 콩류
            (IngredientCategory.Nut, 25f),         // 견과류
            (IngredientCategory.Herb, 100f),       // 허브
            (IngredientCategory.Spice, 10f),       // 향신료
            (IngredientCategory.Drink, 190f),      // 음료류
            // 어류 (fish_cat)
            (IngredientCategory.Salmon, 15f),      // 연어·송어류
            (IngredientCategory.BlueFish, 210f),   // 등푸른 해양어
            (IngredientCategory.FlatFish, 200f),   // 넙치·가자미류
            (IngredientCategory.Catfish, 230f),    // 메기류
            (IngredientCategory.Carp, 180f),       // 잉어·붕어류
            (IngredientCategory.SmallFresh, 170f), // 소형 민물어
            (IngredientCategory.Eel, 280f),        // 장어류
            // 몬스터 (mon_cat)
            (IngredientCategory.RegularMeat, 5f),  // 보통 육류
            (IngredientCategory.Reptile, 260f),    // 파충류·용
            (IngredientCategory.Poultry, 40f),     // 조류·익룡
            (IngredientCategory.Slime, 300f),      // 점액·피·액체
            (IngredientCategory.Flesh, 340f),      // 살점·잔해
            (IngredientCategory.Mystic, 310f),     // 영묘·뿔·비늘
        };

        /// <summary>틴트 채도(고정) — 식욕 톤 틴트(원본 질감 유지, 과채도 방지).</summary>
        const float TintSaturation = 0.30f;

        // [172차] _BaseColor 셰이더 속성 id — 지연 초기화(Shader.PropertyToID는 Unity 네이티브 콜 —
        //   타입 초기화에서 분리해 ModelKey/CatsFor/TintFor 순수 경로가 엔진 콜 없이 동작).
        static int _baseColorId;
        static bool _baseColorIdInit;
        static int BaseColorId
        {
            get
            {
                if (!_baseColorIdInit)
                {
                    _baseColorId = Shader.PropertyToID("_BaseColor");
                    _baseColorIdInit = true;
                }
                return _baseColorId;
            }
        }

        /// <summary>dish id → RecipeDef 지연 캐시(최초 조회 1회 구축 — 2,024건 선형 스캔 방지).</summary>
        static Dictionary<string, RecipeDef> _byId;

        // ================================================================
        //  공개 API
        // ================================================================

        /// <summary>
        /// dish_XXX → GLB 버킷 모델키(요리명 어미 매칭). 미매칭/미등록 id → null(절차 폴백 유지).
        /// 반환 키는 RuntimeModelLoader.HasModel 통과 대상(소문자 파일명 — 빌드 대소문자 구분 계약).
        /// </summary>
        public static string ModelKey(string dishId)
        {
            if (string.IsNullOrEmpty(dishId))
                return null;

            EnsureIndex();
            if (!_byId.TryGetValue(dishId, out RecipeDef def) || string.IsNullOrEmpty(def.name))
                return null;

            string name = def.name;
            for (int i = 0; i < TypeBuckets.Length; i++)
            {
                if (name.EndsWith(TypeBuckets[i].suffix, System.StringComparison.Ordinal))
                    return TypeBuckets[i].bucket;
            }
            return null;
        }

        /// <summary>dish_XXX → 재료 카테고리 집합(RecipeDef.cats — 정렬/중복제거 보증값). 미등록 id → null.</summary>
        public static IngredientCategory[] CatsFor(string dishId)
        {
            if (string.IsNullOrEmpty(dishId))
                return null;

            EnsureIndex();
            return _byId.TryGetValue(dishId, out RecipeDef def) ? def.cats : null;
        }

        /// <summary>
        /// 재료 카테고리 집합 → 틴트 색. hue는 각도평균(원평균 atan2(Σsin, Σcos) — 350°+10°=0° 함정 방지),
        /// 채도 0.30 고정, 명도 1. cats null/빈(또는 미등록 카테고리뿐) → Color.white(틴트 없음).
        /// </summary>
        public static Color TintFor(IngredientCategory[] cats)
        {
            if (cats == null || cats.Length == 0)
                return Color.white;

            float sinSum = 0f, cosSum = 0f;
            int used = 0;
            for (int i = 0; i < cats.Length; i++)
            {
                if (!TryHueOf(cats[i], out float hueDeg))
                    continue;   // 미등록 카테고리(None 센티널 등) — 스킵
                float rad = hueDeg * Mathf.Deg2Rad;
                sinSum += Mathf.Sin(rad);
                cosSum += Mathf.Cos(rad);
                used++;
            }
            if (used == 0)
                return Color.white;

            float meanDeg = Mathf.Atan2(sinSum, cosSum) * Mathf.Rad2Deg;
            if (meanDeg < 0f)
                meanDeg += 360f;   // atan2 반환 (-180,180] → hue 규약 [0,360)
            return Color.HSVToRGB(meanDeg / 360f, TintSaturation, 1f);
        }

        /// <summary>
        /// 노드 전체 렌더러에 _BaseColor 곱셈 틴트 적용(MaterialPropertyBlock — 머티리얼 인스턴스 생성 금지).
        /// tint == Color.white면 무동작. _BaseColor 미보유 셰이더 렌더러는 스킵(멀티 머티리얼은
        /// _BaseColor 보유 첫 머티리얼 기준 — GLB 음식 소품은 단일 머티리얼이 일반).
        /// </summary>
        public static void ApplyTint(GameObject node, Color tint)
        {
            if (node == null || tint == Color.white)
                return;   // 무노드/무티ント — 무동작

            Renderer[] rends = node.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0)
                return;

            var block = new MaterialPropertyBlock();
            for (int i = 0; i < rends.Length; i++)
            {
                Renderer rend = rends[i];
                if (rend == null)
                    continue;

                // sharedMaterial 판독만 — rend.material(인스턴스 복제) 금지(누수 방지 계약)
                Material[] mats = rend.sharedMaterials;
                if (mats == null || mats.Length == 0)
                    continue;

                Color baseColor = Color.white;
                bool hasBase = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null || !mat.HasProperty(BaseColorId))
                        continue;   // _BaseColor 없는 셰이더 — 스킵
                    baseColor = mat.GetColor(BaseColorId);
                    hasBase = true;
                    break;
                }
                if (!hasBase)
                    continue;

                block.SetColor(BaseColorId, baseColor * tint);   // Color*Color = 성분별 곱셈
                rend.SetPropertyBlock(block);
            }
        }

        // ================================================================
        //  내부 보조
        // ================================================================

        /// <summary>카테고리 → hue 도 조회(이름 매칭). 미등록 → false.</summary>
        static bool TryHueOf(IngredientCategory cat, out float hueDeg)
        {
            for (int i = 0; i < CatHues.Length; i++)
            {
                if (CatHues[i].cat == cat)
                {
                    hueDeg = CatHues[i].hueDeg;
                    return true;
                }
            }
            hueDeg = 0f;   // CS0177 방지 명시 할당
            return false;
        }

        /// <summary>RecipeCatalog.Recipes → id 사전 지연 구축(최초 1회, 이후 캐시 재사용).</summary>
        static void EnsureIndex()
        {
            if (_byId != null)
                return;

            var map = new Dictionary<string, RecipeDef>(RecipeCatalog.TotalCount);
            RecipeDef[] recipes = RecipeCatalog.Recipes;
            for (int i = 0; i < recipes.Length; i++)
            {
                RecipeDef r = recipes[i];
                if (!string.IsNullOrEmpty(r.id) && !map.ContainsKey(r.id))
                    map.Add(r.id, r);
            }
            _byId = map;
        }
    }
}
