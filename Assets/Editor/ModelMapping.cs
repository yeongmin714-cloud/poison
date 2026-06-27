using System;
using System.Collections.Generic;

/// <summary>
/// Maps GLB filename (without extension) to placeholder name and swap mode.
/// </summary>
public static class ModelMapping
{
    
    // Forward mapping: GLB key (lowercase, no extension) -> (placeholderName, mode)
    private static readonly Dictionary<string, (string objectName, string mode)> _map = new Dictionary<string, (string, string)>
    {
        {"banshee", ("Placeholder_Banshee", "replace")},
        {"bat", ("Placeholder_Bat", "replace")},
        {"blue_castle", ("Placeholder_Castle_Blue", "replace")},
        {"boar", ("Placeholder_Boar", "replace")},
        {"craft_blend", ("Placeholder_CraftingTable", "replace")},
        {"cook", ("Placeholder_CookingStation", "replace")},
        {"crow", ("Placeholder_Crow", "replace")},
        {"deer", ("Placeholder_Deer", "replace")},
        {"electric_spine_hedgehog", ("Placeholder_ElectricPorcupine", "replace")},
        {"fire_lizard", ("Placeholder_FireLizard", "replace")},
        {"giant_rat", ("Placeholder_GiantRat", "replace")},
        {"golem", ("Placeholder_Golem", "replace")},
        {"green_castle", ("Placeholder_Castle_Green", "replace")},
        {"griffon", ("Placeholder_Griffin", "replace")},
        {"herb_blue", ("Placeholder_Herb_Blue", "replace")},
        {"herb_green", ("Placeholder_Herb_Green", "replace")},
        {"herb_purple", ("Placeholder_Herb_Purple", "replace")},
        {"herb_red", ("Placeholder_Herb_Red", "replace")},
        {"herb_silver", ("Placeholder_Herb_Silver", "replace")},
        {"herb_yellow", ("Placeholder_Herb_Yellow", "replace")},
        {"hut", ("Placeholder_Hut", "replace")},
        {"kingdom", ("Placeholder_Kingdom", "replace")},
        {"manticore", ("Placeholder_Manticore", "replace")},
        {"minotaur", ("Placeholder_Minotaur", "replace")},
        {"npc_lord_rigged", ("Placeholder_Lord", "replace")},
        {"player_rigged", ("Player", "child")},
        {"potion_antidote", ("Placeholder_Potion_Antidote", "replace")},
        {"potion_drug", ("Placeholder_Potion_Drug", "replace")},
        {"potion_heal", ("Placeholder_Potion_Heal", "replace")},
        {"potion_poison", ("Placeholder_Potion_Poison", "replace")},
        {"purple_castle", ("Placeholder_Castle_Purple", "replace")},
        {"rabbit", ("Placeholder_Rabbit", "replace")},
        {"recipebook", ("Placeholder_RecipeBook", "replace")},
        {"red_castle", ("Placeholder_Castle_Red", "replace")},
        {"salamander", ("Placeholder_Salamander", "replace")},
        {"shadow_assassin", ("Placeholder_ShadowAssassin", "replace")},
        {"slime", ("Placeholder_Slime", "replace")},
        {"snake", ("Placeholder_Snake", "replace")},
        {"soldier_rigged", ("Placeholder_Soldier", "replace")},
        {"swamp_alligator", ("Placeholder_SwampCroc", "replace")},
        {"swamp_ogre", ("Placeholder_Ogre", "replace")},
        {"wild_troll", ("Placeholder_WildTroll", "replace")},
        {"wolf", ("Placeholder_Wolf", "replace")},
        {"wooden_forest_spirit", ("Placeholder_ForestSpirit", "replace")}
    };

    /// <summary>
    /// Gets the placeholder name and swap mode for the given GLB filename (without extension).
    /// Returns (null, null) if not found.
    /// </summary>
    public static (string objectName, string mode) GetMapping(string glbFileName)
    {
        if (string.IsNullOrEmpty(glbFileName))
            return (null, null);
        string key = glbFileName.ToLowerInvariant();
        if (_map.TryGetValue(key, out var result))
        {
            return result;
        }
        return (null, null);
    }

    // Tier support
    private static readonly string[] _tiers = new[] { "_tier1", "_tier2", "_tier3", "_tier4", "_tier5" };

    /// <summary>
    /// Attempts to parse a tier suffix (e.g., _tier1) from the filename.
    /// </summary>
    public static bool TryParseTierSuffix(string fileName, out string baseName, out string suffix)
    {
        foreach (var tier in _tiers)
        {
            if (fileName.EndsWith(tier))
            {
                baseName = fileName.Substring(0, fileName.Length - tier.Length);
                suffix = tier;
                return true;
            }
        }
        baseName = null;
        suffix = null;
        return false;
    }

    /// <summary>
    /// Returns the available tier suffixes (same for all base names).
    /// </summary>
    public static string[] GetAvailableTiers(string baseName)
    {
        return _tiers;
    }

    // Optional: GetRecognizedFiles (not strictly needed for the cronjob but useful)
    public static string[] GetRecognizedFiles(string[] fileNames)
    {
        var result = new List<string>();
        foreach (var fileName in fileNames)
        {
            string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrEmpty(nameWithoutExt))
                continue;
            // Check if it's a tiered file
            string baseName;
            string suffix;
            if (TryParseTierSuffix(nameWithoutExt, out baseName, out suffix))
            {
                if (GetMapping(baseName).objectName != null)
                {
                    result.Add(nameWithoutExt);
                }
            }
            else
            {
                if (GetMapping(nameWithoutExt).objectName != null)
                {
                    result.Add(nameWithoutExt);
                }
            }
        }
        return result.ToArray();
    }
}