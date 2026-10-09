using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P18-C2 — 연금술 제작대 (마인크래프트식, UTK).
    /// 레시피 = HerbComboDatabase.AllCombos(GAME_DATA 조합법) 전체 나열 — 재료는 DB 약초.
    /// 제작 = CraftingHelper.CraftAlchemy(성공률 롤/재료 소모/실패 페널티 — 기존 로직 재사용).
    /// ⚠️ 알려진 간극: DB 약초(붉은줄기 등)는 인벤 약초(치유초 등)와 별개 체계 — DB 약초 아이템
    ///   획득 경로(채집/재배 데이터 패스) 연결 전까지는 재료 부족으로 실패할 수 있음(정상 경로).
    /// </summary>
    public class AlchemyBenchUTK : CraftBenchBaseUTK
    {
        private static AlchemyBenchUTK _instance;

        public static void Open()
        {
            if (_instance == null || _instance.panel == null)
                _instance = new AlchemyBenchUTK();
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) { Debug.LogWarning("[AlchemyBenchUTK] UIRoot 없음"); return; }
            if (_instance.parent == null) root.Add(_instance);
            _instance.Show();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        private AlchemyBenchUTK() : base("⚗️ 연금술 대", 2, new Vector2(520f, 560f)) { }

        protected override string BenchSubtitle => "ALCHEMY";
        protected override string CombinationHeading => "연금 레시피 슬롯";
        protected override string RecipeHeading => "제조 가능한 물약 목록";
        protected override string DetailHeading => "물약 정보";
        protected override string DetailDescriptionHeading => "물약 성분 및 제조 정보";
        protected override string StorageHeading => "연금술 재료";
        protected override string StorageSource => "출처: 플레이어 인벤토리";
        protected override string CraftActionText => "물약 제조하기 (BREW)";
        protected override bool ShowThirdIngredientPlaceholder => true;
        protected override IReadOnlyList<string> RecipeFilters => new[] { "전체", "공격성", "회복성", "마약성", "정신성", "물리성" };

        protected override bool MatchesFilter(BenchRecipe recipe, string filter)
        {
            if (filter == "전체") return true;
            if (filter == "마약성") return recipe.ResultId.StartsWith("drug_", System.StringComparison.Ordinal);
            if (recipe.ResultId.StartsWith("drug_", System.StringComparison.Ordinal) || recipe.MatIds == null) return false;
            foreach (var id in recipe.MatIds)
            {
                var herb = ProjectName.Core.Data.HerbDatabase.GetHerbInfo(id);
                if (string.IsNullOrEmpty(herb.id)) continue;
                switch (filter)
                {
                    case "공격성": if (herb.attribute == ProjectName.Core.Data.HerbAttribute.Attack) return true; break;
                    case "회복성": if (herb.attribute == ProjectName.Core.Data.HerbAttribute.Recovery) return true; break;
                    case "정신성": if (herb.attribute == ProjectName.Core.Data.HerbAttribute.Mental) return true; break;
                    case "물리성": if (herb.attribute == ProjectName.Core.Data.HerbAttribute.Physical) return true; break;
                }
            }
            return false;
        }

        protected override PlayerInventory.ItemData GetResultItemData(BenchRecipe recipe)
        {
            return recipe.ResultId.StartsWith("drug_", System.StringComparison.Ordinal)
                ? DrugEffectSystem.CreateDrugItem(ParseDrugStage(recipe.ResultId))
                : base.GetResultItemData(recipe);
        }

        protected override string IngredientDisplayName(string itemId)
        {
            var herb = ProjectName.Core.Data.HerbDatabase.GetHerbInfo(itemId);
            return !string.IsNullOrEmpty(herb.id) ? herb.displayName : base.IngredientDisplayName(itemId);
        }

        private static int ParseDrugStage(string resultId)
        {
            return resultId != null && resultId.StartsWith("drug_", System.StringComparison.Ordinal) &&
                   int.TryParse(resultId.Substring("drug_".Length), out int stage) ? stage : -1;
        }

        private static string[] ResolveDrugIngredientIds(ProjectName.Core.Data.DrugInfo drug)
        {
            if (string.IsNullOrWhiteSpace(drug.Ingredients)) return null;
            string[] names = drug.Ingredients.Split('+');
            if (names.Length < 2 || names.Length > 3) return null;
            var ids = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i].Trim();
                var herb = ProjectName.Core.Data.HerbDatabase.GetHerbInfoByDisplayName(name);
                if (!string.IsNullOrEmpty(herb.id))
                {
                    ids[i] = herb.id;
                    continue;
                }

                // The 10-stage catalog references special raw materials not yet represented by
                // an inventory definition/harvest mapping. Do not silently substitute other herbs.
                return null;
            }
            return ids;
        }

        protected override IReadOnlyList<BenchRecipe> Recipes
        {
            get
            {
                var list = new List<BenchRecipe>();
                foreach (var kv in ProjectName.Core.Data.HerbComboDatabase.AllCombos)
                {
                    // Key = "id1_id2"; current authored IDs contain no underscores.
                    int sep = kv.Key.IndexOf('_');
                    if (sep <= 0) continue;
                    var pair = new[] { kv.Key.Substring(0, sep), kv.Key.Substring(sep + 1) };
                    var h1 = ProjectName.Core.Data.HerbDatabase.GetHerbInfo(pair[0]);
                    var h2 = ProjectName.Core.Data.HerbDatabase.GetHerbInfo(pair[1]);
                    if (string.IsNullOrEmpty(h1.id) || string.IsNullOrEmpty(h2.id)) continue;
                    list.Add(new BenchRecipe
                    {
                        ResultId = kv.Key,
                        ResultName = kv.Value.resultName ?? "???",
                        MatIds = new[] { pair[0], pair[1] },
                        Note = kv.Value.effect,
                        rarity = ItemRarity.Common,
                    });
                }

                foreach (var drug in ProjectName.Core.Data.DrugDatabase.All)
                {
                    if (drug.Stage == 10) continue; // Stage 10 explicitly requires an undefined rare ingredient.
                    string[] ingredients = ResolveDrugIngredientIds(drug);
                    if (ingredients == null) continue;
                    list.Add(new BenchRecipe
                    {
                        ResultId = "drug_" + drug.Stage.ToString("D2"),
                        ResultName = drug.DrugName,
                        MatIds = ingredients,
                        Note = drug.Description + " · 중독성: " + drug.Addiction,
                        rarity = ItemRarity.Common,
                    });
                }
                return list;
            }
        }

        protected override bool TryCraft(BenchRecipe recipe, List<string> placedIds, out string message)
        {
            if (recipe.ResultId.StartsWith("drug_", System.StringComparison.Ordinal))
                return TryCraftDrug(recipe, out message);

            int sep = recipe.ResultId.IndexOf('_');
            if (sep <= 0) { message = "잘못된 레시피"; return false; }
            bool ok = CraftingHelper.CraftAlchemy(recipe.ResultId.Substring(0, sep), recipe.ResultId.Substring(sep + 1));
            message = ok ? "🟢 물약 제작 성공!" : "🔴 물약 제작 실패 — 재료가 없거나 손실되었다.";
            return ok;
        }

        private static bool TryCraftDrug(BenchRecipe recipe, out string message)
        {
            var inventory = PlayerInventory.Instance;
            int stage = ParseDrugStage(recipe.ResultId);
            if (inventory == null || stage < 1 || stage > 9 || recipe.MatIds == null || recipe.MatIds.Length < 2)
            {
                message = "약물 레시피 또는 인벤토리를 사용할 수 없습니다.";
                return false;
            }

            var resultItem = DrugEffectSystem.CreateDrugItem(stage);
            if (resultItem == null)
            {
                message = "약물 데이터가 없습니다.";
                return false;
            }

            var required = new Dictionary<string, int>();
            foreach (string id in recipe.MatIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                required.TryGetValue(id, out int count);
                required[id] = count + 1;
            }
            foreach (var pair in required)
            {
                if (inventory.GetItemCount(pair.Key) < pair.Value)
                {
                    message = "재료 부족 — " + ProjectName.Core.Data.HerbDatabase.GetHerbInfo(pair.Key).displayName;
                    return false;
                }
            }

            foreach (var pair in required)
                if (!inventory.RemoveItem(pair.Key, pair.Value))
                {
                    // The inventory was prevalidated; return any consumed items if a removal unexpectedly fails.
                    foreach (var rollback in required) inventory.AddItem(PlayerInventory.GetItemById(rollback.Key), rollback.Value);
                    message = "재료를 인벤토리에서 제거할 수 없습니다.";
                    return false;
                }

            if (!inventory.AddItem(resultItem, 1))
            {
                foreach (var rollback in required) inventory.AddItem(PlayerInventory.GetItemById(rollback.Key), rollback.Value);
                message = "인벤토리가 가득 찼습니다. 재료는 반환했습니다.";
                return false;
            }

            CraftingHelper.NotifyCraftSucceeded(resultItem.id);
            message = "🟢 " + resultItem.displayName + " 제조 완료! (중독성: " + ProjectName.Core.Data.DrugDatabase.GetByStage(stage)?.Addiction + ")";
            return true;
        }
    }
}
