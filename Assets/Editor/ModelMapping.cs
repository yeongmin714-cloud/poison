using ProjectName.Core;

/// <summary>
/// GLB 파일명 → 게임 오브젝트 이름 매핑
/// 사장님이 UserProvided/ 폴더에 GLB를 넣으면 이 매핑을 보고 자동 교체합니다.
/// 레벨별 티어드 GLB 파일명도 지원합니다 (예: "soldier_tier1").
/// </summary>
public static class ModelMapping
{
    /// <summary>
    /// GLB 파일명(확장자 제외) → 교체할 GameObject 이름 / 태그
    /// key = GLB 파일명 (소문자)
    /// value = (gameObjectName, replaceMode)
    ///   replaceMode: "replace" = GameObject 통째로 교체
    ///                "child" = Placeholder 스크립트의 자식들만 교체
    /// </summary>
    public static (string objectName, string mode) GetMapping(string glbFileName)
    {
        return glbFileName.ToLowerInvariant() switch
        {
            // ===== 플레이어 =====
            // player_rigged 우선, 기본 player.glb는 미사용 (Static 모델로 덮어쓰기 방지)
            "player_rigged"      => ("Player", "child"),

            // ===== 건물 =====
            "hut"                => ("Placeholder_Hut", "replace"),
            "blue_castle"        => ("Placeholder_Castle_Blue", "replace"),
            "green_castle"       => ("Placeholder_Castle_Green", "replace"),
            "purple_castle"      => ("Placeholder_Castle_Purple", "replace"),
            "red_castle"         => ("Placeholder_Castle_Red", "replace"),
            "kingdom"            => ("Placeholder_Kingdom", "replace"),

            // ===== 건물 (from glb/건물/) =====
            "bar"                => ("Placeholder_Bar", "replace"),
            "castle"             => ("Placeholder_Castle", "replace"),
            "shop"               => ("Placeholder_Shop", "replace"),
            "east_flag"          => ("Placeholder_EastFlag", "replace"),
            "west_flag"          => ("Placeholder_WestFlag", "replace"),
            "south_flag"         => ("Placeholder_SouthFlag", "replace"),
            "north_flag"         => ("Placeholder_NorthFlag", "replace"),
            "kingdom_flag"       => ("Placeholder_KingdomFlag", "replace"),
            "player_flag_1"      => ("Placeholder_PlayerFlag1", "replace"),
            "player_flag_2"      => ("Placeholder_PlayerFlag2", "replace"),
            "player_flag_3"      => ("Placeholder_PlayerFlag3", "replace"),
            "player_flag_4"      => ("Placeholder_PlayerFlag4", "replace"),

            // ===== 도구/제작대 =====
            "craft_blend"        => ("Placeholder_CraftingTable", "replace"),
            "craft_cook"         => ("Placeholder_CookingStation", "replace"),
            "craftingtable"      => ("Placeholder_CraftingTable", "replace"),
            "craft_equipment"    => ("Placeholder_CraftEquipment", "replace"),
            "hut_interior"       => ("Placeholder_HutInterior", "replace"),

            // ===== NPC =====
            "lord_npc"           => ("Placeholder_Lord", "replace"),
            "npc_lord_rigged"    => ("Placeholder_Lord", "replace"),

            // ===== NPC 기본 버전 =====
            "npc_girl1"           => ("NPC_Girl1", "replace"),
            "npc_girl2"           => ("NPC_Girl2", "replace"),
            "npc_girl3"           => ("NPC_Girl3", "replace"),
            "npc_man1"            => ("NPC_Man1", "replace"),
            "npc_man2"            => ("NPC_Man2", "replace"),
            "npc_oldman1"         => ("NPC_Oldman1", "replace"),
            "npc_oldman2"         => ("NPC_Oldman2", "replace"),
            "npc_shop"            => ("NPC_Shop", "replace"),
            "npc_king"            => ("NPC_King", "replace"),
            "npc_dracula"         => ("NPC_Dracula", "replace"),

            // ===== NPC (Rigged — from glb/Npc/Rigged/) =====
            "npc_shop_rigged"     => ("NPC_Shop", "replace"),
            "npc_girl1_rigged"    => ("NPC_Girl1", "replace"),
            "npc_girl2_rigged"    => ("NPC_Girl2", "replace"),
            "npc_girl3_rigged"    => ("NPC_Girl3", "replace"),
            "npc_man1_rigged"     => ("NPC_Man1", "replace"),
            "npc_man2_rigged"     => ("NPC_Man2", "replace"),
            "npc_oldman1_rigged"  => ("NPC_Oldman1", "replace"),
            "npc_oldman2_rigged"  => ("NPC_Oldman2", "replace"),
            "npc_dracula_rigged"  => ("NPC_Dracula", "replace"),
            "npc_king_rigged"     => ("NPC_King", "replace"),

            // ===== 병사 =====
            "soldier"            => ("Placeholder_Soldier", "replace"),
            "soldier_rigged"     => ("Placeholder_Soldier", "replace"),

            // ===== 병사 (티어드 — from glb/병사/Rigged/) =====
            "soldier_lv1-20_rigged"   => ("Placeholder_Soldier", "replace"),
            "soldier_lv20-40_rigged"  => ("Placeholder_Soldier", "replace"),
            "soldier_lv40-50_rigged"  => ("Placeholder_Soldier", "replace"),

            // ===== 용병 기본 =====
            "bard"                    => ("Placeholder_Bard", "replace"),
            "mercenary"               => ("Placeholder_Mercenary", "replace"),

            // ===== 용병 (from glb/용병/Rigged/) =====
            "bard_rigged"              => ("Placeholder_Bard", "replace"),
            "mercenary_rigged"         => ("Placeholder_Mercenary", "replace"),

            // ===== 약초 =====
            "herb_red"           => ("Placeholder_Herb_Red", "replace"),
            "herb_green"         => ("Placeholder_Herb_Green", "replace"),
            "herb_blue"          => ("Placeholder_Herb_Blue", "replace"),
            "herb_purple"        => ("Placeholder_Herb_Purple", "replace"),
            "herb_yellow"        => ("Placeholder_Herb_Yellow", "replace"),
            "herb_silver"        => ("Placeholder_Herb_Silver", "replace"),
            "herb_gold"          => ("Placeholder_Herb_Gold", "replace"),

            // ===== 몬스터 (초반) =====
            "rabbit"             => ("Placeholder_Rabbit", "replace"),
            "wolf"               => ("Placeholder_Wolf", "replace"),
            "boar"               => ("Placeholder_Boar", "replace"),
            "deer"               => ("Placeholder_Deer", "replace"),
            "crow"               => ("Placeholder_Crow", "replace"),
            "bat"                => ("Placeholder_Bat", "replace"),
            "snake"              => ("Placeholder_Snake", "replace"),
            "giant_rat"          => ("Placeholder_GiantRat", "replace"),
            "big_mouse"          => ("Placeholder_BigMouse", "replace"),
            "ice_spider"         => ("Placeholder_IceSpider", "replace"),

            // ===== 몬스터 (중반) =====
            "slime"              => ("Placeholder_Slime", "replace"),
            "golem"              => ("Placeholder_Golem", "replace"),
            "fire_lizard"        => ("Placeholder_FireLizard", "replace"),
            "electric_spine_hedgehog" => ("Placeholder_ElectricPorcupine", "replace"),
            "swamp_alligator"    => ("Placeholder_SwampCroc", "replace"),
            "wild_troll"         => ("Placeholder_WildTroll", "replace"),
            "wooden_forest_spirit" => ("Placeholder_ForestSpirit", "replace"),

            // ===== 몬스터 (후반) =====
            "swamp_ogre"         => ("Placeholder_Ogre", "replace"),
            "banshee"            => ("Placeholder_Banshee", "replace"),
            "griffon"            => ("Placeholder_Griffin", "replace"),
            "minotaur"           => ("Placeholder_Minotaur", "replace"),
            "manticore"          => ("Placeholder_Manticore", "replace"),
            "salamander"         => ("Placeholder_Salamander", "replace"),
            "shadow_assassin"    => ("Placeholder_ShadowAssassin", "replace"),

            // ===== 몬스터 (rigged — from glb/몬스터/rigged/) =====
            "rabbit_rigged"               => ("Placeholder_Rabbit", "replace"),
            "wolf_rigged"                 => ("Placeholder_Wolf", "replace"),
            "boar_rigged"                 => ("Placeholder_Boar", "replace"),
            "deer_rigged"                 => ("Placeholder_Deer", "replace"),
            "crow_rigged"                 => ("Placeholder_Crow", "replace"),
            "bat_rigged"                  => ("Placeholder_Bat", "replace"),
            "snake_rigged"                => ("Placeholder_Snake", "replace"),
            "big_mouse_rigged"            => ("Placeholder_GiantRat", "replace"),
            "slime_rigged"                => ("Placeholder_Slime", "replace"),
            "golem_rigged"                => ("Placeholder_Golem", "replace"),
            "fire_lizard_rigged"          => ("Placeholder_FireLizard", "replace"),
            "electric_spine_hedgehog_rigged" => ("Placeholder_ElectricPorcupine", "replace"),
            "swamp_alligator_rigged"      => ("Placeholder_SwampCroc", "replace"),
            "wild_troll_rigged"           => ("Placeholder_WildTroll", "replace"),
            "wooden_forest_spirit_rigged" => ("Placeholder_ForestSpirit", "replace"),
            "swamp_ogre_rigged"           => ("Placeholder_Ogre", "replace"),
            "banshee_rigged"              => ("Placeholder_Banshee", "replace"),
            "griffon_rigged"              => ("Placeholder_Griffin", "replace"),
            "minotaur_rigged"             => ("Placeholder_Minotaur", "replace"),
            "manticore_rigged"            => ("Placeholder_Manticore", "replace"),
            "salamander_rigged"           => ("Placeholder_Salamander", "replace"),
            "shadow_assassin_rigged"      => ("Placeholder_ShadowAssassin", "replace"),

            // ===== 요리 (씬에 있는 경우 대비) =====
            "grilled_skin"         => ("Placeholder_Food", "replace"),
            "roasted_chicken"      => ("Placeholder_Food", "replace"),
            "soup"                 => ("Placeholder_Food", "replace"),
            "stake"                => ("Placeholder_Food", "replace"),
            "tempura"              => ("Placeholder_Food", "replace"),
            "yakitori_skewer"      => ("Placeholder_Food", "replace"),
            "puding"               => ("Placeholder_Food", "replace"),

            // ===== 포션/아이템 =====
            "potion_heal"        => ("Placeholder_Potion_Heal", "replace"),
            "potion_poison"      => ("Placeholder_Potion_Poison", "replace"),
            "potion_drug"        => ("Placeholder_Potion_Drug", "replace"),
            "potion_antidote"    => ("Placeholder_Potion_Antidote", "replace"),
            "recipebook"         => ("Placeholder_RecipeBook", "replace"),

            // ===== 화살/폭탄/소모품 =====
            "arrow"              => ("Placeholder_Arrow", "replace"),
            "arrow2"             => ("Placeholder_Arrow", "replace"),
            "arrow3"             => ("Placeholder_Arrow", "replace"),
            "bomb"               => ("Placeholder_Bomb", "replace"),
            "scroll"             => ("Placeholder_Scroll", "replace"),
            "gas_mask"           => ("Placeholder_GasMask", "replace"),

            _                    => (null, null),
        };
    }

    /// <summary>
    /// UserProvided 폴더에서 찾은 GLB 파일들을 검증
    /// </summary>
    public static string[] GetRecognizedFiles(string[] foundFiles)
    {
        var recognized = new System.Collections.Generic.List<string>();
        foreach (var file in foundFiles)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            var (objName, _) = GetMapping(name);
            if (objName != null)
                recognized.Add(name);
        }
        return recognized.ToArray();
    }

    #region Tiered Mapping

    /// <summary>
    /// GLB 파일명(확장자 제외)에서 티어 접미사를 파싱하여 기본 이름과 접미사를 분리합니다.
    /// </summary>
    /// <param name="name">파일명 (확장자 제외, 예: "soldier_tier1")</param>
    /// <param name="baseName">분리된 기본 이름 (예: "soldier")</param>
    /// <param name="suffix">분리된 티어 접미사 (예: "_tier1")</param>
    /// <returns>티어 접미사가 발견되었으면 true, 아니면 false</returns>
    /// <example>
    /// <code>
    /// bool result = TryParseTierSuffix("soldier_tier2", out string baseName, out string suffix);
    /// // result = true, baseName = "soldier", suffix = "_tier2"
    /// </code>
    /// </example>
    public static bool TryParseTierSuffix(string name, out string baseName, out string suffix)
    {
        baseName = null;
        suffix = null;

        if (string.IsNullOrEmpty(name))
            return false;

        string lower = name.ToLowerInvariant();

        // 순서대로 티어 접미사 검사 (_tier5 → _tier1)
        // 긴 것부터 검사하여 "soldier_tier12" 같은 잘못된 매칭 방지
        for (int tier = 5; tier >= 1; tier--)
        {
            string tierSuffix = "_tier" + tier;
            if (lower.EndsWith(tierSuffix))
            {
                baseName = name.Substring(0, name.Length - tierSuffix.Length);
                suffix = tierSuffix;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 티어 접미사가 포함된 GLB 파일명을 매핑합니다.
    /// 기본 이름을 추출하여 GetMapping()으로 Placeholder 이름을 찾고, 티어 접미사는 별도 반환합니다.
    /// </summary>
    /// <param name="glbFileName">GLB 파일명 (확장자 제외, 예: "soldier_tier1")</param>
    /// <returns>
    /// (objectName, mode, visualSuffix) 튜플.
    /// 티어 접미사가 없거나 매핑되지 않은 이름이면 (null, null, null) 반환.
    /// plain 이름(티어 미포함)은 GetMapping()으로 직접 처리.
    /// </returns>
    /// <example>
    /// <code>
    /// var (objName, mode, suffix) = GetTieredMapping("soldier_tier2");
    /// // objName = "Placeholder_Soldier", mode = "replace", suffix = "_tier2"
    /// </code>
    /// </example>
    public static (string objectName, string mode, string visualSuffix) GetTieredMapping(string glbFileName)
    {
        // 먼저 티어 접미사 파싱 시도
        if (!TryParseTierSuffix(glbFileName, out string baseName, out string suffix))
        {
            // 티어 접미사가 없으면 GetMapping()으로 fallback (기존 호환성)
            var (objName, mode) = GetMapping(glbFileName);
            return (objName, mode, null);
        }

        // 기본 이름으로 Placeholder 매핑 조회
        var (placeholderName, placeholderMode) = GetMapping(baseName);
        if (placeholderName == null)
            return (null, null, null);

        return (placeholderName, placeholderMode, suffix);
    }

    /// <summary>
    /// 주어진 기본 이름에 대해 LevelGroupManager에 정의된 모든 티어 접미사를 반환합니다.
    /// </summary>
    /// <param name="baseName">기본 이름 (예: "soldier") — 현재는 조회에 사용되지 않지만 확장성을 위해 유지</param>
    /// <returns>정의된 모든 티어 접미사 배열 (예: ["_tier1", "_tier2", "_tier3", "_tier4", "_tier5"])</returns>
    /// <example>
    /// <code>
    /// string[] tiers = GetAvailableTiers("soldier");
    /// // 결과: ["_tier1", "_tier2", "_tier3", "_tier4", "_tier5"]
    /// </code>
    /// </example>
    public static string[] GetAvailableTiers(string baseName)
    {
        var groups = LevelGroupManager.GetLevelGroups();
        var suffixes = new System.Collections.Generic.List<string>();
        foreach (var group in groups)
        {
            suffixes.Add(group.visualSuffix);
        }
        return suffixes.ToArray();
    }

    #endregion
}