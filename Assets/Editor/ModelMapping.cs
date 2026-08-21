using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static class ModelMapping
{
    public static Dictionary<string, (string, string)> Map = new Dictionary<string, (string, string)>
    {
        {"arrow", ("Arrow", "replace")},
        {"arrow2", ("Arrow2", "replace")},
        {"arrow3", ("Arrow3", "replace")},
        {"banshee_rigged", ("BansheeRigged", "replace")},
        {"bar", ("Bar", "replace")},
        {"bard_rigged", ("BardRigged", "replace")},
        {"bat_rigged", ("BatRigged", "replace")},
        {"big_mouse_rigged", ("BigMouseRigged", "replace")},
        {"blue_castle", ("BlueCastle", "replace")},
        {"boar_rigged", ("BoarRigged", "replace")},
        {"bomb", ("Bomb", "replace")},
        {"castle", ("Castle", "replace")},
        {"chemical_pack", ("ChemicalPack", "replace")},
        {"craft_blend", ("CraftBlend", "replace")},
        {"craft_cook", ("CraftCook", "replace")},
        {"craft_equipment", ("CraftEquipment", "replace")},
        {"crow_rigged", ("CrowRigged", "replace")},
        {"crystal_armor", ("CrystalArmor", "replace")},
        {"crystal_boot_left", ("CrystalBootLeft", "replace")},
        {"crystal_boot_right", ("CrystalBootRight", "replace")},
        {"crystal_bow", ("CrystalBow", "replace")},
        {"crystal_dagger", ("CrystalDagger", "replace")},
        {"crystal_helmet", ("CrystalHelmet", "replace")},
        {"crystal_leg_left", ("CrystalLegLeft", "replace")},
        {"crystal_leg_right", ("CrystalLegRight", "replace")},
        {"crystal_sword", ("CrystalSword", "replace")},
        {"deer_rigged", ("DeerRigged", "replace")},
        {"dino_rigged", ("DinoRigged", "replace")},
        {"dragon_rigged", ("DragonRigged", "replace")},
        {"eagle_rigged", ("EagleRigged", "replace")},
        {"electric_guitar", ("ElectricGuitar", "replace")},
        {"electric_guitar2", ("ElectricGuitar2", "replace")},
        {"electric_guitar3", ("ElectricGuitar3", "replace")},
        {"elf_rigged", ("ElfRigged", "replace")},
        {"fantasy_shield", ("FantasyShield", "replace")},
        {"fox_rigged", ("FoxRigged", "replace")},
        {"giant_spider_rigged", ("GiantSpiderRigged", "replace")},
        {"goblin_rigged", ("GoblinRigged", "replace")},
        {"gold_chest", ("GoldChest", "replace")},
        {"gold_coin", ("GoldCoin", "replace")},
        {"gold_crown", ("GoldCrown", "replace")},
        {"guard_rigged", ("GuardRigged", "replace")},
        {"hammer", ("Hammer", "replace")},
        {"harpy_rigged", ("HarpyRigged", "replace")},
        {"health_potion", ("HealthPotion", "replace")},
        {"herb_basket", ("HerbBasket", "replace")},
        {"human_male_rigged", ("HumanMaleRigged", "replace")},
        {"human_female_rigged", ("HumanFemaleRigged", "replace")},
        {"hydra_rigged", ("HydraRigged", "replace")},
        {"iron_ingot", ("IronIngot", "replace")},
        {"iron_sword", ("IronSword", "replace")},
        {"knife", ("Knife", "replace")},
        {"kraken_rigged", ("KrakenRigged", "replace")},
        {"large_chest", ("LargeChest", "replace")},
        {"leather_armor", ("LeatherArmor", "replace")},
        {"leather_boots", ("LeatherBoots", "replace")},
        {"leather_glove_left", ("LeatherGloveLeft", "replace")},
        {"leather_glove_right", ("LeatherGloveRight", "replace")},
        {"leather_helmet", ("LeatherHelmet", "replace")},
        {"leather_shield", ("LeatherShield", "replace")},
        {"magic_staff", ("MagicStaff", "replace")},
        {"mana_potion", ("ManaPotion", "replace")},
        {"medieval_house", ("MedievalHouse", "replace")},
        {"medieval_tower", ("MedievalTower", "replace")},
        {"minotaur_rigged", ("MinotaurRigged", "replace")},
        {"orc_rigged", ("OrcRigged", "replace")},
        {"phoenix_rigged", ("PhoenixRigged", "replace")},
        {"pig_rigged", ("PigRigged", "replace")},
        {"rat_rigged", ("RatRigged", "replace")},
        {"red_dragon_rigged", ("RedDragonRigged", "replace")},
        {"rock", ("Rock", "replace")},
        {"ruby", ("Ruby", "replace")},
        {"shield", ("Shield", "replace")},
        {"silver_sword", ("SilverSword", "replace")},
        {"skeleton_rigged", ("SkeletonRigged", "replace")},
        {"slime_rigged", ("SlimeRigged", "replace")},
        {"spear", ("Spear", "replace")},
        {"spider_rigged", ("SpiderRigged", "replace")},
        {"stone", ("Stone", "replace")},
        {"stone_golem_rigged", ("StoneGolemRigged", "replace")},
        {"tree", ("Tree", "replace")},
        {"troll_rigged", ("TrollRigged", "replace")},
        {"unicorn_rigged", ("UnicornRigged", "replace")},
        {"vampire_rigged", ("VampireRigged", "replace")},
        {"werewolf_rigged", ("WerewolfRigged", "replace")},
        {"witch_rigged", ("WitchRigged", "replace")},
        {"wolf_rigged", ("WolfRigged", "replace")},
        {"wood_axe", ("WoodAxe", "replace")},
        {"wood_boots", ("WoodBoots", "replace")},
        {"wood_chest", ("WoodChest", "replace")},
        {"wood_glove_left", ("WoodGloveLeft", "replace")},
        {"wood_glove_right", ("WoodGloveRight", "replace")},
        {"wood_helmet", ("WoodHelmet", "replace")},
        {"wood_shield", ("WoodShield", "replace")},
        {"wood_spear", ("WoodSpear", "replace")},
        {"wood_sword", ("WoodSword", "replace")},
        {"wooden_forest_spirit", ("WoodenForestSpirit", "replace")},
        {"yakitori_skewer", ("YakitoriSkewer", "replace")},
        {"player_rigged", ("Player", "child")},
        {"Player_Rigged", ("Player", "child")},
        {"east_flag", ("EastFlag", "replace")},
        {"west_flag", ("WestFlag", "replace")},
        {"north_flag", ("NorthFlag", "replace")},
        {"south_flag", ("SouthFlag", "replace")},
        {"kingdom_flag", ("KingdomFlag", "replace")},
        {"player_flag_1", ("PlayerFlag1", "replace")},
        {"player_flag_2", ("PlayerFlag2", "replace")},
        {"player_flag_3", ("PlayerFlag3", "replace")},
        {"player_flag_4", ("PlayerFlag4", "replace")},
        {"green_castle", ("GreenCastle", "replace")},
        {"purple_castle", ("PurpleCastle", "replace")},
        {"red_castle", ("RedCastle", "replace")},
        {"hut", ("Hut", "replace")},
        {"shop", ("Shop", "replace")}
    };

    private static readonly Regex TierRegex = new Regex(@"^(.+?)_(tier\d+)$", RegexOptions.IgnoreCase);

    public static bool TryParseTierSuffix(string fileName, out string baseName, out string tierSuffix)
    {
        baseName = fileName;
        tierSuffix = "";
        var match = TierRegex.Match(fileName);
        if (match.Success)
        {
            baseName = match.Groups[1].Value;
            tierSuffix = match.Groups[2].Value;
            return true;
        }
        return false;
    }

    public static (string placeholderName, string mode) GetMapping(string baseName)
    {
        if (Map.TryGetValue(baseName, out var mapping))
        {
            return mapping;
        }
        return (baseName, "replace");
    }

    public static string[] GetAvailableTiers()
    {
        return new[] { "tier1", "tier2", "tier3", "tier4", "tier5" };
    }
}