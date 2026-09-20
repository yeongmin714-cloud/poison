using UnityEngine;

namespace ProjectName.Core
{
    [CreateAssetMenu(fileName = "New Recipe", menuName = "Crafting/Recipe")]
    public class Recipe : ScriptableObject
    {
        public enum RecipeType { Alchemy, Cooking }

        public string displayName;
        public string description;
        public PlayerInventory.ItemData requiredItem1;
        public PlayerInventory.ItemData requiredItem2;
        public PlayerInventory.ItemData resultItem;
        [Range(0, 100)] public int baseSuccessRate;
        public int difficultyPenalty; // 0, -5, -15, -30
        public int requiredLevel; // required level for this recipe (either Alchemy or Cooking)
        public RecipeType recipeType;
        public int expReward; // experience awarded on successful craft

        /// <summary>[Milestone A] 결과물 희귀도 — ComputeFinalCraftChance의 희귀도 페널티에 사용.</summary>
        public ItemRarity rarity = ItemRarity.Common;

        /// <summary>
        /// Checks if player meets the level requirement for this recipe.
        /// </summary>
        public bool CanCraft()
        {
            return PlayerStats.Instance != null && PlayerStats.Instance.Level >= requiredLevel;
        }

        /// <summary>
        /// Calculates final success rate based on player level (recipe-type-specific bonus),
        /// base success rate, difficulty penalty, plus [Milestone A] luck bonus and rarity penalty.
        /// Returns value clamped between 5 and 95 (CraftingHelper.MinCraftChance ~ MaxCraftChance).
        /// </summary>
        public int CalculateSuccessRate()
        {
            if (PlayerStats.Instance == null)
                return baseSuccessRate;

            // Use recipe-type-specific success bonus from PlayerStats
            // (알케미/요리 기존 로직 회귀 금지 — successBonus 합산 유지)
            float successBonus = recipeType == RecipeType.Alchemy
                ? PlayerStats.Instance.GetAlchemySuccessRate()
                : PlayerStats.Instance.GetCookingSuccessRate();

            int levelBonus = Mathf.RoundToInt(successBonus * 100f); // convert 0-1 float to percent

            // [Milestone A] 운 보너스(+Luck*2%p) + 희귀도 페널티 — 공용 헬퍼 표 재사용
            int luckBonus = Mathf.RoundToInt(PlayerStats.Instance.GetLuckCraftBonus());
            int rarityPenalty = CraftingHelper.GetRarityPenalty(rarity);

            int rate = baseSuccessRate + levelBonus + luckBonus + difficultyPenalty + rarityPenalty;
            return Mathf.Clamp(rate, CraftingHelper.MinCraftChance, CraftingHelper.MaxCraftChance);
        }
    }
}
