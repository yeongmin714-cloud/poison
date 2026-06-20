using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Core
{
    /// <summary>
    /// Static helper for crafting operations.
    /// Used by AlchemyUI, CookingUI, and RecipeWindow.
    /// </summary>
    public static class CraftingHelper
    {
        /// <summary>
        /// Attempt to craft an alchemy recipe from two herb IDs.
        /// Returns true if successful.
        /// </summary>
        public static bool CraftAlchemy(string herbId1, string herbId2)
        {
            var comboResult = HerbComboDatabase.GetCombo(herbId1, herbId2);
            if (!comboResult.HasValue)
            {
                Debug.LogWarning($"[CraftingHelper] No alchemy combo for {herbId1} + {herbId2}");
                return false;
            }

            var result = comboResult.Value;
            var herbInfo1 = HerbDatabase.GetHerbInfo(herbId1);
            var herbInfo2 = HerbDatabase.GetHerbInfo(herbId2);

            if (string.IsNullOrEmpty(herbInfo1.id) || string.IsNullOrEmpty(herbInfo2.id))
            {
                Debug.LogWarning("[CraftingHelper] Could not find herb info for combo");
                return false;
            }

            var herbItem1 = CreateHerbItem(herbInfo1);
            var herbItem2 = CreateHerbItem(herbInfo2);
            var resultItem = CreatePotionItem(result);

            var recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.displayName = result.resultName;
            recipe.description = $"{herbInfo1.displayName} + {herbInfo2.displayName} → {result.resultName}";
            recipe.requiredItem1 = herbItem1;
            recipe.requiredItem2 = herbItem2;
            recipe.resultItem = resultItem;
            recipe.baseSuccessRate = 60;
            recipe.difficultyPenalty = 0;
            recipe.requiredLevel = 1;
            recipe.expReward = 25;
            recipe.recipeType = Recipe.RecipeType.Alchemy;

            return PerformCraft(recipe);
        }

        /// <summary>
        /// Perform the actual crafting logic: check level, consume materials, roll success, give result.
        /// </summary>
        private static bool PerformCraft(Recipe recipe)
        {
            if (!recipe.CanCraft())
            {
                Debug.Log($"[CraftingHelper] 레벨 부족. 필요: {recipe.requiredLevel}");
                return false;
            }

            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogError("[CraftingHelper] PlayerInventory not found.");
                return false;
            }

            bool hasItem1 = inventory.HasItem(recipe.requiredItem1.id);
            bool hasItem2 = recipe.requiredItem2 == null || inventory.HasItem(recipe.requiredItem2.id);

            if (!hasItem1 || !hasItem2)
            {
                Debug.Log($"[CraftingHelper] 재료 부족. 필요: {recipe.requiredItem1.displayName}");
                return false;
            }

            // Success rate
            int levelBonus = 0;
            if (PlayerStats.Instance != null)
                levelBonus = PlayerStats.Instance.Level;

            int successRate = Mathf.Clamp(recipe.baseSuccessRate + levelBonus + recipe.difficultyPenalty, 0, 100);
            bool success = Random.Range(0, 100) < successRate;

            if (success)
            {
                inventory.RemoveItem(recipe.requiredItem1.id, 1);
                if (recipe.requiredItem2 != null)
                    inventory.RemoveItem(recipe.requiredItem2.id, 1);

                inventory.AddItem(recipe.resultItem, 1);

                if (PlayerStats.Instance != null)
                    PlayerStats.Instance.AddEXP(recipe.expReward);

                RecipeDiscoverySystem.MarkDiscovered(recipe.resultItem.displayName);
                Debug.Log($"[CraftingHelper] ✅ {recipe.resultItem.displayName} 제작 성공!");
                return true;
            }
            else
            {
                // Failure: 30% preserve, 50% lose one, 20% lose all
                float roll = Random.value;
                if (roll < 0.3f)
                {
                    Debug.Log("[CraftingHelper] 제작 실패 but 재료 보존");
                }
                else if (roll < 0.8f)
                {
                    inventory.RemoveItem(recipe.requiredItem1.id, 1);
                    Debug.Log("[CraftingHelper] 제작 실패: 재료 하나 손실");
                }
                else
                {
                    inventory.RemoveItem(recipe.requiredItem1.id, 1);
                    if (recipe.requiredItem2 != null)
                        inventory.RemoveItem(recipe.requiredItem2.id, 1);
                    Debug.Log("[CraftingHelper] 제작 실패: 모든 재료 손실");
                }
                return false;
            }
        }

        private static PlayerInventory.ItemData CreateHerbItem(HerbInfo herb)
        {
            return new PlayerInventory.ItemData
            {
                id = herb.id,
                displayName = herb.displayName,
                description = herb.description,
                category = PlayerInventory.ItemCategory.Herb,
                maxStack = 20
            };
        }

        private static PlayerInventory.ItemData CreatePotionItem(HerbComboResult result)
        {
            var category = PlayerInventory.ItemCategory.Potion;

            string name = result.resultName;
            if (name.Contains("접착제") || name.Contains("코팅제") || name.Contains("도구") ||
                name.Contains("방패") || name.Contains("트랩") || name.Contains("용액"))
                category = PlayerInventory.ItemCategory.Material;
            else if (name.Contains("독") || name.Contains("맹독") || name.Contains("마비") ||
                     name.Contains("환각") || name.Contains("혼란") || name.Contains("수면"))
                category = PlayerInventory.ItemCategory.Potion;
            else if (name.Contains("치유") || name.Contains("회복") || name.Contains("해독") ||
                     name.Contains("생명") || name.Contains("치료"))
                category = PlayerInventory.ItemCategory.Potion;

            return new PlayerInventory.ItemData
            {
                id = $"combo_{result.resultName}",
                displayName = result.resultName,
                description = result.effect,
                category = category,
                maxStack = 10
            };
        }
    }
}