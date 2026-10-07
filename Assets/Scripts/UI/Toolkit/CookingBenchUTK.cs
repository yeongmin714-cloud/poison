using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P18-C2 — 요리 제작대 (마인크래프트식, UTK).
    /// 레시피 = CookingDatabase.AllRecipes(GAME_DATA 38종) 그대로 — DB 이름이 단일 소스.
    /// 재료 소모: 고기는 별칭 매핑(토끼 고기→meat_rabbit 등), 약초 계열은 약초(herb_yakcho) 공용.
    /// 판정/지급: CraftSuccessSystem 등급판정 + DishDatabase 지급 (CookingUI.TryCook 단일 소스 이관).
    /// ⚠️ 알려진 간극: GAME_DATA 약초명(회복꽃 등) ↔ 인벤 약초(치유초 등) 1:1 매핑은 후속 데이터 패스.
    /// </summary>
    public class CookingBenchUTK : CraftBenchBaseUTK
    {
        private static CookingBenchUTK _instance;

        // [아카이브 계약 — BenchVariant 선언] 요리 벤치 변형 필터: 카테고리 탭 없이 "전체"만.
        // AlchemyBenchUTK(6탭)/WeaponForgeUTK(무기·장비·도구)와 같은 선언 패턴 — 베이스 기본값 의존 대신 변형이 명시.
        protected override IReadOnlyList<string> RecipeFilters => new[] { "전체" };

        /// <summary>DB 고기명 → 인벤 아이템 ID 별칭 (표기 차이 흡수).</summary>
        private static readonly Dictionary<string, string> MeatAlias = new Dictionary<string, string>
        {
            { "토끼 고기", "meat_rabbit" },
            { "멧돼지 고기", "meat_boar" },
            { "늑대 고기", "meat_wolf" },
        };
        private const string GenericHerbId = "herb_yakcho";   // 약초 공용 재료

        public static void Open()
        {
            if (_instance == null || _instance.panel == null)
                _instance = new CookingBenchUTK();
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) { Debug.LogWarning("[CookingBenchUTK] UIRoot 없음"); return; }
            if (_instance.parent == null) root.Add(_instance);
            _instance.Show();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        private CookingBenchUTK() : base("🍲 요리 화덕", 2, new Vector2(520f, 560f)) { }

        protected override string BenchSubtitle => "COOKING";
        protected override string CombinationHeading => "요리 재료 조합";
        protected override string RecipeHeading => "조리 가능한 요리 목록";
        protected override string DetailHeading => "선택한 요리 상세";
        protected override string DetailDescriptionHeading => "요리 및 효과 정보";
        protected override string StorageHeading => "요리 재료";
        protected override string StorageSource => "출처: 플레이어 인벤토리";
        protected override string CraftActionText => "요리하기 (COOK)";
        protected override bool ShowThirdIngredientPlaceholder => true;
        protected override PlayerInventory.ItemData GetResultItemData(BenchRecipe recipe)
            => ProjectName.Core.Data.DishDatabase.GetItemData(recipe.ResultName);

        protected override IReadOnlyList<BenchRecipe> Recipes
        {
            get
            {
                var list = new List<BenchRecipe>();
                foreach (var kv in ProjectName.Core.Data.CookingDatabase.AllRecipes)
                {
                    var parts = kv.Key.Split('|');
                    if (parts.Length != 2) continue;
                    if (!MeatAlias.ContainsKey(parts[0])) continue;   // 인벤 고기 3종 외 재료는 미지원
                    list.Add(new BenchRecipe
                    {
                        ResultId = kv.Key,                            // "고기DB명|약초DB명"
                        ResultName = kv.Value.DishName,
                        MatIds = new[] { MeatAlias[parts[0]], GenericHerbId },
                        Note = kv.Value.Effect,
                        rarity = ProjectName.Core.Data.DishDatabase.GetItemData(kv.Value.DishName) != null
                            ? ProjectName.Core.Data.DishDatabase.GetItemData(kv.Value.DishName).rarity : ItemRarity.Common,
                    });
                }
                return list;
            }
        }

        protected override bool TryCraft(BenchRecipe recipe, List<string> placedIds, out string message)
        {
            var parts = recipe.ResultId.Split('|');
            if (parts.Length != 2) { message = "잘못된 레시피"; return false; }
            return CraftCook(parts[0], parts[1], out message);
        }

        /// <summary>[P18-C2] 요리 제작 오케스트레이션 — CookingUI.TryCook 단일 소스 이관(UI 계층 규약).</summary>
        private static bool CraftCook(string meatDbName, string herbDbName, out string message)
        {
            message = "";
            var inventory = PlayerInventory.Instance;
            if (inventory == null) { message = "인벤토리를 찾을 수 없습니다."; return false; }

            string meatId = MeatAlias.TryGetValue(meatDbName, out var mid) ? mid : null;
            var meatItem = meatId != null ? PlayerInventory.GetItemById(meatId) : null;
            var herbItem = PlayerInventory.GetItemById(GenericHerbId);
            if (meatItem == null || herbItem == null) { message = "재료 아이템을 찾을 수 없습니다."; return false; }
            if (inventory.GetItemCount(meatId) < 1 || inventory.GetItemCount(GenericHerbId) < 1)
            {
                message = "재료가 부족합니다. (고기 1 + 약초 1)";
                return false;
            }

            var cookingResult = ProjectName.Core.Data.CookingDatabase.GetCooking(meatDbName, herbDbName);
            if (!cookingResult.HasValue) { message = "이 조합으로는 요리를 만들 수 없습니다."; return false; }
            var result = cookingResult.Value;

            string grade1 = ProjectName.Systems.CraftSuccessSystem.GetGradeFromItemId(meatId);
            string grade2 = ProjectName.Systems.CraftSuccessSystem.GetGradeFromItemId(GenericHerbId);
            var craftResult = ProjectName.Systems.CraftSuccessSystem.ExecuteCraft(false, grade1, grade2);

            switch (craftResult)
            {
                case ProjectName.Systems.CraftResult.Success:
                    inventory.RemoveItem(meatId, 1);
                    inventory.RemoveItem(GenericHerbId, 1);
                    var dishItem = ProjectName.Core.Data.DishDatabase.GetItemData(result.DishName);
                    if (dishItem != null) inventory.AddItem(dishItem, 1);
                    ProjectName.Core.RecipeDiscoverySystem.MarkDiscovered(result.DishName);
                    if (PlayerStats.Instance != null)
                        PlayerStats.Instance.AddExp(Random.Range(5, 16));
                    message = $"🟢 요리 성공! '{result.DishName}' — {result.Effect}";
                    ProjectName.Core.CraftingHelper.NotifyCraftSucceeded(result.DishId ?? "dish");
                    return true;

                case ProjectName.Systems.CraftResult.Fail_MaterialPreserved:
                    message = "🟡 요리 실패... 재료가 보존되었다.";
                    return false;

                case ProjectName.Systems.CraftResult.Fail_MaterialDestroyed:
                    bool destroyMeat = Random.value < 0.5f;
                    string destroyedId = destroyMeat ? meatId : GenericHerbId;
                    string destroyedName = destroyMeat ? meatItem.displayName : herbItem.displayName;
                    inventory.RemoveItem(destroyedId, 1);
                    message = $"🔴 요리 실패! '{destroyedName}'이(가) 소멸했다!";
                    return false;

                default: // Fail_Burned
                    inventory.RemoveItem(meatId, 1);
                    inventory.RemoveItem(GenericHerbId, 1);
                    message = "🔥 요리 대실패! 모든 재료가 타서 사라졌다!";
                    return false;
            }
        }
    }
}
