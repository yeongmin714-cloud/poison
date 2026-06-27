using UnityEditor;
using UnityEngine;
using ProjectName.Core;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Phase 4: GAME_DATA.md 데이터를 기반으로 80개 Recipe .asset + 4개 RecipeDatabase .asset 생성
/// Tools/Phase 4 - Generate Recipe Assets 메뉴에서 실행.
/// </summary>
public static class Phase4_GenerateRecipeAssets
{
    private const string MenuPath = "Tools/Phase 4 - Generate Recipe Assets";
    private const string RecipesDir = "Assets/Resources/Recipes";

    [MenuItem(MenuPath)]
    public static void GenerateAllRecipes()
    {
        Debug.Log("========================================");
        Debug.Log("[Phase4] Recipe Asset 생성 시작...");
        Debug.Log("========================================");

        Directory.CreateDirectory(RecipesDir);

        // 각 카테고리별 Recipe 리스트
        var attackRecipes = CreateAttackRecipes();
        var mentalRecipes = CreateMentalRecipes();
        var recoveryRecipes = CreateRecoveryRecipes();
        var physicalRecipes = CreatePhysicalRecipes();

        // Recipe .asset 파일 저장
        SaveRecipes(attackRecipes, "Attack");
        SaveRecipes(mentalRecipes, "Mental");
        SaveRecipes(recoveryRecipes, "Recovery");
        SaveRecipes(physicalRecipes, "Physical");

        // RecipeDatabase .asset 생성 및 저장
        CreateRecipeDatabase("AttackRecipes", attackRecipes);
        CreateRecipeDatabase("MentalRecipes", mentalRecipes);
        CreateRecipeDatabase("RecoveryRecipes", recoveryRecipes);
        CreateRecipeDatabase("PhysicalRecipes", physicalRecipes);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("========================================");
        Debug.Log("[Phase4] ✅ 80개 Recipe + 4개 Database 생성 완료!");
        Debug.Log("========================================");
    }

    // ================================================================
    //  Herb ItemData 생성 헬퍼
    // ================================================================

    private static PlayerInventory.ItemData MakeHerb(string id, string displayName)
    {
        return new PlayerInventory.ItemData
        {
            id = id,
            displayName = displayName,
            description = displayName,
            category = PlayerInventory.ItemCategory.Herb,
            maxStack = 20
        };
    }

    private static PlayerInventory.ItemData MakePotion(string id, string displayName, string description)
    {
        return new PlayerInventory.ItemData
        {
            id = id,
            displayName = displayName,
            description = description,
            category = PlayerInventory.ItemCategory.Potion,
            maxStack = 10
        };
    }

    // ================================================================
    //  Recipe 생성 헬퍼
    // ================================================================

    private static Recipe CreateRecipe(
        string assetName,
        string displayName,
        string description,
        PlayerInventory.ItemData requiredItem1,
        PlayerInventory.ItemData requiredItem2,
        PlayerInventory.ItemData resultItem,
        int baseSuccessRate,
        int difficultyPenalty,
        int requiredLevel,
        int expReward)
    {
        var recipe = ScriptableObject.CreateInstance<Recipe>();
        recipe.name = assetName;
        recipe.displayName = displayName;
        recipe.description = description;
        recipe.requiredItem1 = requiredItem1;
        recipe.requiredItem2 = requiredItem2;
        recipe.resultItem = resultItem;
        recipe.baseSuccessRate = baseSuccessRate;
        recipe.difficultyPenalty = difficultyPenalty;
        recipe.requiredLevel = requiredLevel;
        recipe.expReward = expReward;
        recipe.recipeType = Recipe.RecipeType.Alchemy;
        return recipe;
    }

    private static void SaveRecipes(List<Recipe> recipes, string category)
    {
        for (int i = 0; i < recipes.Count; i++)
        {
            string path = $"{RecipesDir}/{category}_{i + 1:D2}.asset";
            AssetDatabase.CreateAsset(recipes[i], path);
        }
        Debug.Log($"[Phase4] {category}: {recipes.Count}개 Recipe .asset 저장됨");
    }

    private static void CreateRecipeDatabase(string dbName, List<Recipe> recipes)
    {
        var db = ScriptableObject.CreateInstance<RecipeDatabase>();
        db.name = dbName;
        db.SetRecipes(new List<Recipe>(recipes));
        string path = $"{RecipesDir}/{dbName}.asset";
        AssetDatabase.CreateAsset(db, path);
        Debug.Log($"[Phase4] RecipeDatabase 생성: {dbName} ({recipes.Count}개 레시피)");
    }

    // ================================================================
    //  2.1 공격성 조합 (Attack) — 20종
    // ================================================================

    private static List<Recipe> CreateAttackRecipes()
    {
        var list = new List<Recipe>();

        string catId = "herb_red";

        // 레시피 데이터: (assetName, displayName, description, 재료1이름, 재료2이름, baseSuccess, difficultyPenalty, requiredLevel, expReward)
        var data = new[]
        {
            //   이름               효과                     재료1      재료2      성공률  난이도  레벨  경험치
            ("Attack_01", "독성 가시액",     "적 체력 점진적 감소",        "쓴풀",      "가시덤불",    80, 0,   1,  15),
            ("Attack_02", "마비 독침액",     "적 공격 속도 저하",         "쓴풀",      "독가시꽃",    75, -5,  3,  20),
            ("Attack_03", "출혈 유발제",     "회복 불가 상태",            "가시덤불",   "붉은줄기",    80, 0,   5,  25),
            ("Attack_04", "부식 독액",       "방어력 영구 감소",          "붉은줄기",   "검은진액풀",  75, -5,  7,  30),
            ("Attack_05", "괴사 독약",       "초당 데미지 급증",          "검은진액풀", "괴사뿌리",    70, -5,  10, 35),
            ("Attack_06", "맹독 포자",       "범위 내 체력 감소",         "괴사뿌리",   "맹독이끼",    70, -5,  12, 40),
            ("Attack_07", "오염된 가스액",   "적 방어력 절반 하락",       "맹독이끼",   "썩은덩굴",    75, -5,  14, 40),
            ("Attack_08", "재앙의 맹독",     "영주 즉사 확률",            "썩은덩굴",   "재앙의씨앗",  70, -15, 20, 60),
            ("Attack_09", "화염 맹독",       "추가 화염 데미지",          "재앙의씨앗", "불꽃꽃잎",    70, -15, 22, 65),
            ("Attack_10", "작열 독약",       "지병 유발",                 "불꽃꽃잎",   "쓴풀",        75, -5,  18, 50),
            ("Attack_11", "마비 환각제",     "행동 불능 상태",            "독가시꽃",   "향기꽃",      70, -15, 15, 45),
            ("Attack_12", "혈압 상승제",     "적 자멸 유도",              "붉은줄기",   "회복꽃",      75, -5,  16, 45),
            ("Attack_13", "용해액",          "무기 내구도 하락",          "괴사뿌리",   "맑은잎",      80, 0,   12, 35),
            ("Attack_14", "신경 마비액",     "조준 저하",                 "맹독이끼",   "신경마비꽃",  75, -5,  15, 40),
            ("Attack_15", "위장 독약",       "음식물로 오인",             "썩은덩굴",   "해독초",      80, -5,  14, 38),
            ("Attack_16", "고열 발생제",     "알레르기 반응용",           "불꽃꽃잎",   "이슬맺힌잎",  80, 0,   10, 30),
            ("Attack_17", "그림자 독약",     "밤 시간 데미지 2배",        "쓴풀",      "그림자덩굴",   70, -15, 18, 50),
            ("Attack_18", "점착성 독액",     "이동 봉쇄",                 "검은진액풀", "푸른이끼",    75, -5,  14, 40),
            ("Attack_19", "뼈 강화 파괴제",  "방어 무시",                 "재앙의씨앗", "마른뿌리",    70, -15, 25, 80),
            ("Attack_20", "상처 벌림제",     "치유 불가",                 "가시덤불",   "피부재생풀",   75, -5,  13, 35),
        };

        foreach (var d in data)
        {
            var item1 = MakeHerb(catId, d.Item4);
            var item2 = MakeHerb(catId, d.Item5);
            var result = MakePotion("potion_attack_" + d.Item1.Substring(7).ToLower(), d.Item2, d.Item3);
            var recipe = CreateRecipe(d.Item1, d.Item2, d.Item3, item1, item2, result, d.Item6, d.Item7, d.Item8, d.Item9);
            list.Add(recipe);
        }

        return list;
    }

    // ================================================================
    //  2.2 정신성 조합 (Mental) — 20종
    // ================================================================

    private static List<Recipe> CreateMentalRecipes()
    {
        var list = new List<Recipe>();

        string catId = "herb_purple";

        var data = new[]
        {
            ("Mental_01", "혼란의 향수",      "영주 판단력 흐림",          "향기꽃",      "환각포자",     80, 0,   1,  15),
            ("Mental_02", "수면의 향기",      "잠입용",                    "향기꽃",      "신경마비꽃",   80, 0,   3,  20),
            ("Mental_03", "환영 안개제",      "성 안으로 잠입",            "환각포자",    "안개꽃",       75, -5,  5,  25),
            ("Mental_04", "영혼 공포액",      "병사 사기 저하",            "안개꽃",      "영혼꽃",       75, -5,  7,  30),
            ("Mental_05", "망령의 속삭임",    "영주 비밀 대화 유출",       "영혼꽃",      "그림자덩굴",   70, -5,  10, 35),
            ("Mental_06", "기억 삭제제",      "암살 후 증거 인멸",         "그림자덩굴",  "시간지연꽃",   70, -15, 15, 45),
            ("Mental_07", "환상 마약",        "영주를 매료시킴",           "시간지연꽃",  "변이초",       70, -15, 18, 50),
            ("Mental_08", "냉정 제어제",      "정보원 검거 확률 감소",     "변이초",      "차가운서리풀", 75, -5,  12, 38),
            ("Mental_09", "무기력 약물",      "영주 자택 연금 상태",       "차가운서리풀","마비버섯",     70, -15, 20, 55),
            ("Mental_10", "매혹 버섯액",      "영주가 선물 선호",          "마비버섯",    "향기꽃",       70, -15, 16, 45),
            ("Mental_11", "정신 강화제",      "정보원 회복",               "환각포자",    "회복꽃",       80, 0,   8,  28),
            ("Mental_12", "정지제",           "말을 멈추게 함",            "신경마비꽃",  "잡초",         85, 0,   6,  22),
            ("Mental_13", "투명 은신제",      "경비병 회피",               "안개꽃",      "맑은잎",       75, -5,  14, 40),
            ("Mental_14", "영혼 결박액",      "도망 방지",                 "영혼꽃",      "푸른이끼",     80, 0,   12, 35),
            ("Mental_15", "심문 강화제",      "정보 획득",                 "그림자덩굴",  "마른뿌리",     75, -5,  16, 42),
            ("Mental_16", "평온 약물",        "적대감 감소",               "시간지연꽃",  "진정초",       85, 0,   5,  20),
            ("Mental_17", "탐색의 눈",        "성 내부 위치 파악",         "변이초",      "시야확장풀",   75, -5,  14, 40),
            ("Mental_18", "의지 고취제",      "병사 사기 증진",            "차가운서리풀","근육강화풀",   85, 0,   8,  25),
            ("Mental_19", "눈부신 환각제",    "시야 차단",                 "마비버섯",    "빛나는이끼",   75, -5,  13, 38),
            ("Mental_20", "매력 증폭제",      "친밀도 급상승",             "향기꽃",      "이슬맺힌잎",   80, 0,   8,  25),
        };

        foreach (var d in data)
        {
            var item1 = MakeHerb(catId, d.Item4);
            var item2 = MakeHerb(catId, d.Item5);
            var result = MakePotion("potion_mental_" + d.Item1.Substring(10).ToLower(), d.Item2, d.Item3);
            var recipe = CreateRecipe(d.Item1, d.Item2, d.Item3, item1, item2, result, d.Item6, d.Item7, d.Item8, d.Item9);
            list.Add(recipe);
        }

        return list;
    }

    // ================================================================
    //  2.3 회복성 조합 (Recovery) — 20종
    // ================================================================

    private static List<Recipe> CreateRecoveryRecipes()
    {
        var list = new List<Recipe>();

        string catId = "herb_green";

        var data = new[]
        {
            ("Recovery_01", "만능 치유액",     "체력 풀회복",               "회복꽃",      "생명수뿌리",   85, 0,   1,  20),
            ("Recovery_02", "기력 회복제",     "병사 전투 지속력 증가",     "회복꽃",      "활력잎",       85, 0,   3,  22),
            ("Recovery_03", "불안 해소제",     "공포 무효화",               "생명수뿌리",  "진정초",       80, 0,   5,  25),
            ("Recovery_04", "괴력 물약",       "공격력 대폭 상승",          "활력잎",      "근육강화풀",   75, -5,  8,  30),
            ("Recovery_05", "흉터 치료제",     "부상 패널티 제거",          "진정초",      "피부재생풀",   80, 0,   7,  28),
            ("Recovery_06", "강철 해독제",     "방어력+독 제거",            "근육강화풀",  "해독초",       80, 0,   10, 32),
            ("Recovery_07", "통찰의 치유제",   "회피율 상승",               "피부재생풀",  "시야확장풀",   75, -5,  12, 35),
            ("Recovery_08", "신속 해독제",     "독 제거 후 즉시 탈출",      "해독초",      "속도증가꽃",   80, 0,   10, 30),
            ("Recovery_09", "예리한 감각",     "함정 탐지",                 "시야확장풀",  "정신안정잎",   85, 0,   6,  22),
            ("Recovery_10", "신속 치유제",     "전투 중 사용 가능",         "속도증가꽃",  "회복꽃",       80, 0,   8,  28),
            ("Recovery_11", "소화 보조제",     "음식 부작용 방지",          "정신안정잎",  "잡초",         90, 0,   2,  15),
            ("Recovery_12", "순수한 생명수",   "최상위 치유",               "생명수뿌리",  "맑은잎",       75, -5,  18, 55),
            ("Recovery_13", "생명 보호막",     "일시적 무적",               "활력잎",      "푸른이끼",     70, -15, 25, 80),
            ("Recovery_14", "피로 누적 해소제","휴식 효과",                 "진정초",      "마른뿌리",     85, 0,   5,  20),
            ("Recovery_15", "유연한 근육제",   "민첩성 상승",               "근육강화풀",  "이슬맺힌잎",   80, 0,   10, 30),
            ("Recovery_16", "재생 강화액",     "초당 회복",                 "피부재생풀",  "빛나는이끼",   75, -5,  15, 42),
            ("Recovery_17", "화염 내성제",     "적 화염 공격 무효화",       "해독초",      "용의숨결풀",   70, -15, 20, 60),
            ("Recovery_18", "암흑 시야제",     "지하 감옥 탐색",            "시야확장풀",  "타락한뿌리",   75, -5,  16, 45),
            ("Recovery_19", "영웅의 신속",     "이동 속도 2배",             "속도증가꽃",  "황금약초",     70, -15, 22, 70),
            ("Recovery_20", "심신 안락제",     "상태이상 방지",             "정신안정잎",  "회복꽃",       80, 0,   8,  25),
        };

        foreach (var d in data)
        {
            var item1 = MakeHerb(catId, d.Item4);
            var item2 = MakeHerb(catId, d.Item5);
            var result = MakePotion("potion_recovery_" + d.Item1.Substring(12).ToLower(), d.Item2, d.Item3);
            var recipe = CreateRecipe(d.Item1, d.Item2, d.Item3, item1, item2, result, d.Item6, d.Item7, d.Item8, d.Item9);
            list.Add(recipe);
        }

        return list;
    }

    // ================================================================
    //  2.4 물리성 조합 (Physical) — 20종
    // ================================================================

    private static List<Recipe> CreatePhysicalRecipes()
    {
        var list = new List<Recipe>();

        string catId = "herb_silver";

        var data = new[]
        {
            ("Physical_01", "기초 접착제",     "장비 수리",                  "잡초",        "맑은잎",       90, 0,   1,  10),
            ("Physical_02", "강력한 덫",       "영주 이동 제한",             "잡초",        "끈끈이풀",     80, 0,   5,  22),
            ("Physical_03", "습기 차단액",     "장비 부식 방지",             "맑은잎",      "푸른이끼",     85, 0,   3,  18),
            ("Physical_04", "결속제",          "도구 제작 필수품",           "끈끈이풀",    "마른뿌리",     85, 0,   5,  20),
            ("Physical_05", "강화 코팅제",     "무기 방어력 상승",           "푸른이끼",    "이슬맺힌잎",   80, 0,   8,  28),
            ("Physical_06", "마력 도구",       "희귀 제작 가능",             "마른뿌리",    "빛나는이끼",   75, -5,  12, 38),
            ("Physical_07", "단단한 합금제",   "최고급 장비 재료",           "이슬맺힌잎",  "용의숨결풀",   70, -15, 20, 60),
            ("Physical_08", "암흑 광택제",     "위장 은신",                  "빛나는이끼",  "타락한뿌리",   75, -5,  15, 42),
            ("Physical_09", "황금 강화액",     "장비 레벨 상승",             "용의숨결풀",  "황금약초",     70, -15, 25, 80),
            ("Physical_10", "튼튼한 밧줄 용액","포획",                       "타락한뿌리",  "잡초",         80, 0,   10, 30),
            ("Physical_11", "금속 보호제",     "내구도 무한",                "황금약초",    "맑은잎",       70, -15, 22, 70),
            ("Physical_12", "유연한 도구",     "사용 횟수 증가",             "끈끈이풀",    "회복꽃",       85, 0,   6,  22),
            ("Physical_13", "가시 방패",       "공격자에게 데미지",          "푸른이끼",    "가시덤불",     80, 0,   10, 30),
            ("Physical_14", "파괴의 도구",     "성벽 파괴용",               "마른뿌리",    "붉은줄기",     75, -5,  16, 48),
            ("Physical_15", "마비 장착액",     "공격 시 마비",               "이슬맺힌잎",  "신경마비꽃",   75, -5,  14, 40),
            ("Physical_16", "환영 도구",       "분신 생성",                  "빛나는이끼",  "환각포자",     70, -15, 18, 55),
            ("Physical_17", "화염 도구",       "성문 불태우기",              "용의숨결풀",  "불꽃꽃잎",     70, -15, 22, 65),
            ("Physical_18", "부식 도구",       "상대 무기 파괴",             "타락한뿌리",  "괴사뿌리",     75, -5,  20, 58),
            ("Physical_19", "불사의 장비",     "죽어도 파괴 안 됨",          "황금약초",    "생명수뿌리",   70, -15, 28, 100),
            ("Physical_20", "끈적한 마비 트랩","마을 방어",                  "잡초",        "마비버섯",     80, 0,   10, 30),
        };

        foreach (var d in data)
        {
            var item1 = MakeHerb(catId, d.Item4);
            var item2 = MakeHerb(catId, d.Item5);
            var result = MakePotion("potion_physical_" + d.Item1.Substring(12).ToLower(), d.Item2, d.Item3);
            var recipe = CreateRecipe(d.Item1, d.Item2, d.Item3, item1, item2, result, d.Item6, d.Item7, d.Item8, d.Item9);
            list.Add(recipe);
        }

        return list;
    }
}