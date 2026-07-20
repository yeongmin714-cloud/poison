using System.Collections.Generic;
using System.Linq;

namespace ProjectName.Editor
{
    public static class ModelMapping
    {
        public static readonly Dictionary<string, (string, string)> Map = new Dictionary<string, (string, string)>
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
            {"crystal_glove_left", ("CrystalGloveLeft", "replace")},
            {"crystal_glove_right", ("CrystalGloveRight", "replace")},
            {"crystal_helmet", ("CrystalHelmet", "replace")},
            {"crystal_spear", ("CrystalSpear", "replace")},
            {"crystal_sword", ("CrystalSword", "replace")},
            {"deer_rigged", ("DeerRigged", "replace")},
            {"east_flag", ("EastFlag", "replace")},
            {"electric_spine_hedgehog_rigged", ("ElectricSpineHedgehogRigged", "replace")},
            {"fire_lizard_rigged", ("FireLizardRigged", "replace")},
            {"golem_rigged", ("GolemRigged", "replace")},
            {"green_castle", ("GreenCastle", "replace")},
            {"griffon_rigged", ("GriffonRigged", "replace")},
            {"grilled_skin", ("GrilledSkin", "replace")},
            {"herb_blue", ("HerbBlue", "replace")},
            {"herb_gold", ("HerbGold", "replace")},
            {"herb_green", ("HerbGreen", "replace")},
            {"herb_purple", ("HerbPurple", "replace")},
            {"herb_red", ("HerbRed", "replace")},
            {"hut", ("Hut", "replace")},
            {"ice_spider", ("IceSpider", "replace")},
            {"kingdom", ("Kingdom", "replace")},
            {"kingdom_flag", ("KingdomFlag", "replace")},
            {"manticore_rigged", ("ManticoreRigged", "replace")},
            {"mercenary_rigged", ("MercenaryRigged", "replace")},
            {"minotaur_rigged", ("MinotaurRigged", "replace")},
            {"monstrous_deep_clam", ("MonstrousDeepClam", "replace")},
            {"north_flag", ("NorthFlag", "replace")},
            {"npc_dracula_rigged", ("NpcDraculaRigged", "replace")},
            {"npc_girl1_rigged", ("NpcGirl1Rigged", "replace")},
            {"npc_girl2_rigged", ("NpcGirl2Rigged", "replace")},
            {"npc_girl3_rigged", ("NpcGirl3Rigged", "replace")},
            {"npc_king_rigged", ("NpcKingRigged", "replace")},
            {"npc_lord_rigged", ("NpcLordRigged", "replace")},
            {"npc_man1_rigged", ("NpcMan1Rigged", "replace")},
            {"npc_man2_rigged", ("NpcMan2Rigged", "replace")},
            {"npc_oldman1_rigged", ("NpcOldman1Rigged", "replace")},
            {"npc_oldman2_rigged", ("NpcOldman2Rigged", "replace")},
            {"npc_shop_rigged", ("NpcShopRigged", "replace")},
            {"player_flag_1", ("PlayerFlag1", "replace")},
            {"player_flag_2", ("PlayerFlag2", "replace")},
            {"player_flag_3", ("PlayerFlag3", "replace")},
            {"player_flag_4", ("PlayerFlag4", "replace")},
            {"player_rigged", ("PlayerRigged", "replace")},
            {"puding", ("Puding", "replace")},
            {"purple_castle", ("PurpleCastle", "replace")},
            {"rabbit_rigged", ("RabbitRigged", "replace")},
            {"red_castle", ("RedCastle", "replace")},
            {"roasted_chicken", ("RoastedChicken", "replace")},
            {"salamander_rigged", ("SalamanderRigged", "replace")},
            {"scroll", ("Scroll", "replace")},
            {"shadow_assassin_rigged", ("ShadowAssassinRigged", "replace")},
            {"shop", ("Shop", "replace")},
            {"slime_rigged", ("SlimeRigged", "replace")},
            {"snake_rigged", ("SnakeRigged", "replace")},
            {"soldier_lv1-20_rigged", ("SoldierLv1-20Rigged", "replace")},
            {"soldier_lv20-40_rigged", ("SoldierLv20-40Rigged", "replace")},
            {"soldier_lv40-50_rigged", ("SoldierLv40-50Rigged", "replace")},
            {"soup", ("Soup", "replace")},
            {"south_flag", ("SouthFlag", "replace")},
            {"stake", ("Stake", "replace")},
            {"steel_armor", ("SteelArmor", "replace")},
            {"steel_boot_left", ("SteelBootLeft", "replace")},
            {"steel_boot_right", ("SteelBootRight", "replace")},
            {"steel_bow", ("SteelBow", "replace")},
            {"steel_chemical_pack", ("SteelChemicalPack", "replace")},
            {"steel_dagger", ("SteelDagger", "replace")},
            {"steel_gas_mask", ("SteelGasMask", "replace")},
            {"steel_glove_left", ("SteelGloveLeft", "replace")},
            {"steel_glove_right", ("SteelGloveRight", "replace")},
            {"steel_helmet", ("SteelHelmet", "replace")},
            {"steel_spear", ("SteelSpear", "replace")},
            {"steel_sword", ("SteelSword", "replace")},
            {"stone_armor", ("StoneArmor", "replace")},
            {"stone_boot_left", ("StoneBootLeft", "replace")},
            {"stone_boot_right", ("StoneBootRight", "replace")},
            {"stone_bow", ("StoneBow", "replace")},
            {"stone_chemical_pack", ("StoneChemicalPack", "replace")},
            {"stone_dagger", ("StoneDagger", "replace")},
            {"stone_gas_mask", ("StoneGasMask", "replace")},
            {"stone_glove_left", ("StoneGloveLeft", "replace")},
            {"stone_glove_right", ("StoneGloveRight", "replace")},
            {"stone_helmet", ("StoneHelmet", "replace")},
            {"stone_spear", ("StoneSpear", "replace")},
            {"stone_sword", ("StoneSword", "replace")},
            {"swamp_alligator_rigged", ("SwampAlligatorRigged", "replace")},
            {"swamp_ogre_rigged", ("SwampOgreRigged", "replace")},
            {"tempura", ("Tempura", "replace")},
            {"west_flag", ("WestFlag", "replace")},
            {"wild_troll_rigged", ("WildTrollRigged", "replace")},
            {"wolf_rigged", ("WolfRigged", "replace")},
            {"wood_armor", ("WoodArmor", "replace")},
            {"wood_boot_left", ("WoodBootLeft", "replace")},
            {"wood_boot_right", ("WoodBootRight", "replace")},
            {"wood_bow", ("WoodBow", "replace")},
            {"wood_chemical_pack", ("WoodChemicalPack", "replace")},
            {"wood_dagger", ("WoodDagger", "replace")},
            {"wood_gas_mask", ("WoodGasMask", "replace")},
            {"wood_glove_left", ("WoodGloveLeft", "replace")},
            {"wood_glove_right", ("WoodGloveRight", "replace")},
            {"wood_helmet", ("WoodHelmet", "replace")},
            {"wood_shield", ("WoodShield", "replace")},
            {"wood_spear", ("WoodSpear", "replace")},
            {"wood_sword", ("WoodSword", "replace")},
            {"wooden_forest_spirit", ("WoodenForestSpirit", "replace")},
            {"yakitori_skewer", ("YakitoriSkewer", "replace")}
        };

        public static (string, string) GetMapping(string key)
        {
            if (Map.TryGetValue(key, out var value))
                return value;
            return (null, null);
        }

        public static bool TryParseTierSuffix(string fileName, out string baseName, out string suffix)
        {
            baseName = fileName;
            suffix = null;

            // Check for tier suffixes like _tier1, _tier2, _tier3, _tier4, _tier5
            var tierSuffixes = new[] { "_tier1", "_tier2", "_tier3", "_tier4", "_tier5" };
            foreach (var ts in tierSuffixes)
            {
                if (fileName.EndsWith(ts))
                {
                    baseName = fileName.Substring(0, fileName.Length - ts.Length);
                    suffix = ts;
                    return true;
                }
            }

            // Also check for _tier1-5 format
            for (int i = 1; i <= 5; i++)
            {
                var ts = $"_tier{i}";
                if (fileName.EndsWith(ts))
                {
                    baseName = fileName.Substring(0, fileName.Length - ts.Length);
                    suffix = ts;
                    return true;
                }
            }

            return false;
        }

        public static string[] GetAvailableTiers(string baseName)
        {
            var tiers = new List<string>();
            for (int i = 1; i <= 5; i++)
            {
                var ts = $"_tier{i}";
                var key = baseName + ts;
                if (Map.ContainsKey(key))
                    tiers.Add(i.ToString());
            }
            return tiers.ToArray();
        }

        /// <summary>
        /// 인식된 모든 GLB 파일명 배열 반환 (테스트용)
        /// </summary>
        public static string[] GetRecognizedFiles()
        {
            return Map.Keys.ToArray();
        }

        /// <summary>
        /// 주어진 파일명 배열 중 Map에 있는 키만 필터링하여 반환 (테스트용)
        /// </summary>
        public static string[] GetRecognizedFiles(string[] fileNames)
        {
            var result = new List<string>();
            foreach (var fileName in fileNames)
            {
                // 확장자 제거
                var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                if (Map.ContainsKey(nameWithoutExt))
                    result.Add(nameWithoutExt);
            }
            return result.ToArray();
        }
    }
}