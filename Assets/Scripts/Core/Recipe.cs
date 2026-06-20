using UnityEngine;
using ProjectName.Core;

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

    /// <summary>
    /// Checks if player meets the level requirement for this recipe.
    /// </summary>
    public bool CanCraft()
    {
        return PlayerStats.Instance != null && PlayerStats.Instance.Level >= requiredLevel;
    }

    /// <summary>
    /// Calculates final success rate based on player level, base success rate, and difficulty penalty.
    /// Returns value clamped between 0 and 100.
    /// </summary>
    public int CalculateSuccessRate()
    {
        if (PlayerStats.Instance == null)
            return baseSuccessRate;
        int levelBonus = PlayerStats.Instance.Level; // +1% per level
        int rate = baseSuccessRate + levelBonus + difficultyPenalty;
        return Mathf.Clamp(rate, 0, 100);
    }
}
