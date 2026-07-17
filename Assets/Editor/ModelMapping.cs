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
        {"arrow", ("Placeholder_Arrow", "replace")},
        {"arrow2", ("Placeholder_Arrow2", "replace")},
        {"arrow3", ("Placeholder_Arrow3", "replace")},
        {"banshee_rigged", ("Placeholder_BansheeRigged", "replace")},
        {"bar", ("Placeholder_Bar", "replace")},
        {"bard_rigged", ("Placeholder_BardRigged", "replace")},
        {"bat_rigged", ("Placeholder_BatRigged", "replace")},
        {"big_mouse_rigged", ("Placeholder_BigMouseRigged", "replace")},
        {"boar_rigged", ("Placeholder_BoarRigged", "replace")},
        {"bomb", ("Placeholder_Bomb", "replace")},
        {"castle", ("Placeholder_Castle", "replace")},
        {"chemical_pack", ("Placeholder_ChemicalPack", "replace")},
        {"craft_cook", ("Placeholder_CraftCook", "replace")},
        {"craft_equipment", ("Placeholder_CraftEquipment", "replace")},
        {"crow_rigged", ("Placeholder_CrowRigged", "replace")},
        {"crystal_armor", ("Placeholder_CrystalArmor", "replace")},
        {"crystal_boot_left", ("Placeholder_CrystalBootLeft", "replace")},
        {"crystal_boot_right", ("Placeholder_CrystalBootRight", "replace")},
        {"crystal_bow", ("Placeholder_CrystalBow", "replace")},
        {"crystal_dagger", ("Placeholder_CrystalDagger", "replace")},
        {"crystal_glove_left", ("Placeholder_CrystalGloveLeft", "replace")},
        {"crystal_glove_right", ("Placeholder_CrystalGloveRight", "replace")},
        {"crystal_helmet", ("Placeholder_CrystalHelmet", "replace")},
        {"crystal_spear", ("Placeholder_CrystalSpear", "replace")},
        {"crystal_sword", ("Placeholder_CrystalSword", "replace")},
        {"deer_rigged", ("Placeholder_DeerRigged", "replace")},
        {"east_flag", ("Placeholder_EastFlag", "replace")},
        {"electric_spine_hedgehog_rigged", ("Placeholder_ElectricSpineHedgehogRigged", "replace")},
        {"fire_lizard_rigged", ("Placeholder_FireLizardRigged", "replace")},
        {"golem_rigged", ("Placeholder_GolemRigged", "replace")},
        {"griffon_rigged", ("Placeholder_GriffonRigged", "replace")},
        {"grilled_skin", ("Placeholder_GrilledSkin", "replace")},
        {"herb_gold", ("Placeholder_HerbGold", "replace")},
        {"ice_spider", ("Placeholder_IceSpider", "replace")},
        {"kingdom_flag", ("Placeholder_KingdomFlag", "replace")},
        {"manticore_rigged", ("Placeholder_ManticoreRigged", "replace")},
        {"mercenary_rigged", ("Placeholder_MercenaryRigged", "replace")},
        {"minotaur_rigged", ("Placeholder_MinotaurRigged", "replace")},
        {"monstrous_deep_clam", ("Placeholder_MonstrousDeepClam", "replace")},
        {"north_flag", ("Placeholder_NorthFlag", "replace")},
        {"npc_dracula_rigged", ("Placeholder_NpcDraculaRigged", "replace")},
        {"npc_girl1_rigged", ("Placeholder_NpcGirl1Rigged", "replace")},
        {"npc_girl2_rigged", ("Placeholder_NpcGirl2Rigged", "replace")},
        {"npc_girl3_rigged", ("Placeholder_NpcGirl3Rigged", "replace")},
        {"npc_king_rigged", ("Placeholder_NpcKingRigged", "replace")},
        {"npc_man1_rigged", ("Placeholder_NpcMan1Rigged", "replace")},
        {"npc_man2_rigged", ("Placeholder_NpcMan2Rigged", "replace")},
        {"npc_oldman1_rigged", ("Placeholder_NpcOldman1Rigged", "replace")},
        {"npc_oldman2_rigged", ("Placeholder_NpcOldman2Rigged", "replace")},
        {"npc_shop_rigged", ("Placeholder_NpcShopRigged", "replace")},
        {"player_flag_1", ("Placeholder_PlayerFlag1", "replace")},
        {"player_flag_2", ("Placeholder_PlayerFlag2", "replace")},
        {"player_flag_3", ("Placeholder_PlayerFlag3", "replace")},
        {"player_flag_4", ("Placeholder_PlayerFlag4", "replace")},
        {"puding", ("Placeholder_Puding", "replace")},
        {"rabbit_rigged", ("Placeholder_RabbitRigged", "replace")},
        {"roasted_chicken", ("Placeholder_RoastedChicken", "replace")},
        {"salamander_rigged", ("Placeholder_SalamanderRigged", "replace")},
        {"scroll", ("Placeholder_Scroll", "replace")},
        {"shadow_assassin_rigged", ("Placeholder_ShadowAssassinRigged", "replace")},
        {"shop", ("Placeholder_Shop", "replace")},
        {"slime_rigged", ("Placeholder_SlimeRigged", "replace")},
        {"snake_rigged", ("Placeholder_SnakeRigged", "replace")},
        {"soldier_lv1-20_rigged", ("Placeholder_SoldierLv1-20Rigged", "replace")},
        {"soldier_lv20-40_rigged", ("Placeholder_SoldierLv20-40Rigged", "replace")},
        {"soldier_lv40-50_rigged", ("Placeholder_SoldierLv40-50Rigged", "replace")},
        {"soup", ("Placeholder_Soup", "replace")},
        {"south_flag", ("Placeholder_SouthFlag", "replace")},
        {"stake", ("Placeholder_Stake", "replace")},
        {"steel_armor", ("Placeholder_SteelArmor", "replace")},
        {"steel_boot_left", ("Placeholder_SteelBootLeft", "replace")},
        {"steel_boot_right", ("Placeholder_SteelBootRight", "replace")},
        {"steel_bow", ("Placeholder_SteelBow", "replace")},
        {"steel_chemical_pack", ("Placeholder_SteelChemicalPack", "replace")},
        {"steel_dagger", ("Placeholder_SteelDagger", "replace")},
        {"steel_gas_mask", ("Placeholder_SteelGasMask", "replace")},
        {"steel_glove_left", ("Placeholder_SteelGloveLeft", "replace")},
        {"steel_glove_right", ("Placeholder_SteelGloveRight", "replace")},
        {"steel_helmet", ("Placeholder_SteelHelmet", "replace")},
        {"steel_spear", ("Placeholder_SteelSpear", "replace")},
        {"steel_sword", ("Placeholder_SteelSword", "replace")},
        {"stone_armor", ("Placeholder_StoneArmor", "replace")},
        {"stone_boot_left", ("Placeholder_StoneBootLeft", "replace")},
        {"stone_boot_right", ("Placeholder_StoneBootRight", "replace")},
        {"stone_bow", ("Placeholder_StoneBow", "replace")},
        {"stone_chemical_pack", ("Placeholder_StoneChemicalPack", "replace")},
        {"stone_dagger", ("Placeholder_StoneDagger", "replace")},
        {"stone_gas_mask", ("Placeholder_StoneGasMask", "replace")},
        {"stone_glove_left", ("Placeholder_StoneGloveLeft", "replace")},
        {"stone_glove_right", ("Placeholder_StoneGloveRight", "replace")},
        {"stone_helmet", ("Placeholder_StoneHelmet", "replace")},
        {"stone_spear", ("Placeholder_StoneSpear", "replace")},
        {"stone_sword", ("Placeholder_StoneSword", "replace")},
        {"swamp_alligator_rigged", ("Placeholder_SwampAlligatorRigged", "replace")},
        {"swamp_ogre_rigged", ("Placeholder_SwampOgreRigged", "replace")},
        {"tempura", ("Placeholder_Tempura", "replace")},
        {"west_flag", ("Placeholder_WestFlag", "replace")},
        {"wild_troll_rigged", ("Placeholder_WildTrollRigged", "replace")},
        {"wolf_rigged", ("Placeholder_WolfRigged", "replace")},
        {"wood_armor", ("Placeholder_WoodArmor", "replace")},
        {"wood_boot_left", ("Placeholder_WoodBootLeft", "replace")},
        {"wood_boot_right", ("Placeholder_WoodBootRight", "replace")},
        {"wood_bow", ("Placeholder_WoodBow", "replace")},
        {"wood_chemical_pack", ("Placeholder_WoodChemicalPack", "replace")},
        {"wood_dagger", ("Placeholder_WoodDagger", "replace")},
        {"wood_gas_mask", ("Placeholder_WoodGasMask", "replace")},
        {"wood_glove_left", ("Placeholder_WoodGloveLeft", "replace")},
        {"wood_glove_right", ("Placeholder_WoodGloveRight", "replace")},
        {"wood_helmet", ("Placeholder_WoodHelmet", "replace")},
        {"wood_shield", ("Placeholder_WoodShield", "replace")},
        {"wood_spear", ("Placeholder_WoodSpear", "replace")},
        {"wood_sword", ("Placeholder_WoodSword", "replace")},
        {"yakitori_skewer", ("Placeholder_YakitoriSkewer", "replace")},
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