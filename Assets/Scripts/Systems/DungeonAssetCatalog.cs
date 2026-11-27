using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [147차 P-A4] 던전 팩 매핑 카탈로그 — DUNGEON EXPORT 320종 GLB의 카테고리 규칙 단일소스.
    /// 리소스 루트 = "Models/Dungeon/" (RuntimeModelLoader 2차 폴백 경로와 1:1 계약).
    /// 판정은 소문자 접두어 규칙표 기반(결정론 — Random 0건). 규칙표는 파일명 실측 320종 전수 분류 검산 통과
    /// (Structural 105 / Decor 71 / Props 96 / Furniture 39 / Lighting 9 / Misc 0 — Dungeon147CatalogTests 전수 계약).
    /// 접두어 추가는 신규 팩 수용 시 규칙표 확장으로만(개별 하드박제 금지).
    /// </summary>
    public static class DungeonAssetCatalog
    {
        public const string ResourceRoot = "Models/Dungeon/";

        public enum Category { Structural, Decor, Props, Furniture, Lighting, Misc }

        /// <summary>소문자 접두어 → 카테고리 규칙표 — 순서 우선(앞 항목 우승. floor가 floorcrate보다 뒤).</summary>
        private static readonly (string prefix, Category category)[] PrefixRules =
        {
            ("wall", Category.Structural),
            ("corner", Category.Structural),
            ("floorcrate", Category.Props),
            ("floor", Category.Structural),
            ("ground", Category.Structural),
            ("pilar", Category.Structural),
            ("pillar", Category.Structural),
            ("arch", Category.Structural),
            ("beam", Category.Structural),
            ("stairs", Category.Structural),
            ("fence", Category.Structural),
            ("wallend", Category.Structural),
            ("stoneadd", Category.Structural),
            ("plank", Category.Structural),
            ("holder", Category.Structural),
            ("windowsill", Category.Structural),
            ("monumenta", Category.Structural),
            ("crystal", Category.Decor),
            ("rocks", Category.Decor),
            ("peble", Category.Decor),
            ("vines", Category.Decor),
            ("plant", Category.Decor),
            ("chest", Category.Props),
            ("treasure", Category.Props),
            ("coin", Category.Props),
            ("key", Category.Props),
            ("potion", Category.Props),
            ("sword", Category.Props),
            ("shield", Category.Props),
            ("bone", Category.Props),
            ("skull", Category.Props),
            ("skeleton", Category.Props),
            ("cofin", Category.Props),
            ("cage", Category.Props),
            ("chain", Category.Props),
            ("barrel", Category.Props),
            ("meat", Category.Props),
            ("apple", Category.Props),
            ("bowl", Category.Props),
            ("cup", Category.Props),
            ("nail", Category.Props),
            ("plate", Category.Props),
            ("sztandar", Category.Props),
            ("table", Category.Furniture),
            ("chair", Category.Furniture),
            ("stool", Category.Furniture),
            ("hanging_shelf", Category.Furniture),
            ("shelf", Category.Furniture),
            ("wardrobe", Category.Furniture),
            ("book", Category.Furniture),
            ("candle", Category.Lighting),
            ("torch", Category.Lighting),
        };

        /// <summary>파일명/키(GLB명) → 카테고리. 규칙 미적중 시 Misc(전수 검산상 0건이 정상).</summary>
        public static Category CategoryOf(string glbName)
        {
            if (string.IsNullOrEmpty(glbName)) return Category.Misc;
            int dot = glbName.LastIndexOf('.');
            string baseName = dot > 0 ? glbName.Substring(0, dot) : glbName;
            string b = baseName.ToLowerInvariant();
            foreach (var rule in PrefixRules)
                if (b.StartsWith(rule.prefix)) return rule.category;
            return Category.Misc;
        }

        /// <summary>카테고리 소속 GLB 파일명 목록 — 정적 진실원(런타임 폴더 스캔 불가 원칙). Random 0건 하드박제.</summary>
        public static IReadOnlyList<string> NamesIn(Category category)
        {
            switch (category)
            {
                case Category.Structural: return _structural;
                case Category.Decor: return _decor;
                case Category.Props: return _props;
                case Category.Furniture: return _furniture;
                case Category.Lighting: return _lighting;
                case Category.Misc: return _misc;
                default: return System.Array.Empty<string>();
            }
        }

        /// <summary>전수 합계 — 320 계약(Dungeon147CatalogTests).</summary>
        public static int TotalCount => _structural.Length + _decor.Length + _props.Length +
            _furniture.Length + _lighting.Length + _misc.Length;

        private static readonly string[] _structural =
        {
            "Arch_1A.glb", "Arch_2A.glb", "Beam_1A.glb", "Beam_2A.glb", "Beam_3A.glb", "Beam_4A.glb", "Corner_1A.glb", "Corner_2A.glb", "Corner_3A.glb", "Corner_4.glb", "Fence_1_1A.glb", "Fence_1_2A.glb", "Fence_1_3A.glb", "Floor_1_1A.glb", "Floor_1_2A.glb", "Floor_1_3A.glb", "Floor_1_4A.glb", "Floor_1_5A.glb", "Floor_1_6A.glb", "Floor_2_1A.glb", "Floor_2_2.glb", "Floor_2_3A.glb", "Floor_2_4A.glb", "Floor_2_5A.glb", "Floor_2_6A.glb", "Floor_4_1.glb", "Floor_4_2.glb", "Floor_4_3.glb", "Floor_4_4.glb", "Floor_5_1.glb", "Floor_5_2.glb", "Floor_5_3.glb", "Floor_5_4.glb", "Ground_1_1.glb", "Ground_1_2.glb", "Ground_1_3.glb", "Ground_1_4.glb", "Ground_2_1.glb", "Ground_2_2.glb", "Ground_2_3.glb", "Ground_2_4.glb", "Holder_1A.glb", "Holder_2A.glb", "MonumentA.glb", "Pilar_1_1A.glb", "Pilar_1_2A.glb", "Pilar_1_3A.glb", "Pilar_1_4A.glb", "Pilar_1_5A.glb", "Pilar_1_6A.glb", "Pilar_2_1A.glb", "Pilar_2_2A.glb", "Pilar_2_3A.glb", "Pilar_2_4A.glb", "Pilar_2_5A.glb", "Pilar_2_6A.glb", "Pillar_3_1A.glb", "Pillar_3_2A.glb", "Pillar_3_3A.glb", "Pillar_3_4A.glb", "Plank_1_1.glb", "Plank_1_2.glb", "Plank_1_3.glb", "Plank_2_1.glb", "Plank_2_2.glb", "Plank_2_3.glb", "StairsRamp_A.glb", "StairsRoundA.glb", "Stairs_1A.glb", "Stairs_2.glb", "Stairs_2_2.glb", "Stairs_3A.glb", "Stairs_3_2.glb", "Stairs_4A.glb", "Stairs_5A.glb", "Stairs_6.glb", "Stairs_6_1.glb", "Stairs_7A.glb", "Stairs_7_1.glb", "StoneAdd_1A.glb", "StoneAdd_2A.glb", "StoneAdd_3A.glb", "StoneAdd_4A.glb", "WallEndA.glb", "Wall_1_1A.glb", "Wall_2_1A.glb", "Wall_2_2A.glb", "Wall_2_3A.glb", "Wall_3_1A.glb", "Wall_3_2A.glb", "Wall_3_3A.glb", "Wall_4_1A.glb", "Wall_4_2A.glb", "Wall_5_1A.glb", "Wall_6_1A.glb", "Wall_Connect.glb", "Wall_Crack.glb", "Wall_Door.glb", "Wall_T.glb", "Wall_T2.glb", "Wall_U_1A.glb", "Wall_U_2.glb", "Wall_Water.glb", "Wall_XA.glb", "windowsill_1.glb"
        };

        private static readonly string[] _decor =
        {
            "CrystalRock_1.glb", "CrystalRock_2.glb", "CrystalRock_3.glb", "CrystalRock_4.glb", "CrystalRock_5.glb", "Crystal_1.glb", "Crystal_2.glb", "Crystal_3.glb", "Crystal_4.glb", "Crystal_5.glb", "Peble_1_1.glb", "Peble_1_2.glb", "Peble_1_3.glb", "Peble_1_4.glb", "Peble_1_5.glb", "Peble_2_1.glb", "Peble_2_2.glb", "Peble_2_3.glb", "Peble_2_4.glb", "Peble_2_5.glb", "Peble_3_1.glb", "Peble_3_2.glb", "Peble_3_3.glb", "Peble_3_4.glb", "Peble_3_5.glb", "Peble_4_1.glb", "Peble_4_2.glb", "Peble_4_3.glb", "Peble_4_4.glb", "Peble_4_5.glb", "Peble_5_1.glb", "Peble_5_2.glb", "Peble_5_3.glb", "Peble_5_4.glb", "Peble_5_5.glb", "Plant_1.glb", "Plant_1_2.glb", "Plant_1_3.glb", "Plant_2.glb", "Rocks_1_1.glb", "Rocks_1_2.glb", "Rocks_1_3.glb", "Rocks_1_4.glb", "Rocks_2_1.glb", "Rocks_2_2.glb", "Rocks_2_3.glb", "Rocks_2_4.glb", "Rocks_3_1.glb", "Rocks_3_2.glb", "Rocks_3_3.glb", "Rocks_3_4.glb", "Rocks_3_5.glb", "Rocks_4_1.glb", "Rocks_4_2.glb", "Rocks_4_3.glb", "Rocks_4_4.glb", "Rocks_4_5.glb", "Rocks_5_1.glb", "Rocks_5_2.glb", "Rocks_5_3.glb", "Rocks_5_4.glb", "Vines_1_1.glb", "Vines_1_2.glb", "Vines_2_1.glb", "Vines_2_2.glb", "Vines_3_1.glb", "Vines_3_2.glb", "Vines_4_1.glb", "Vines_4_2.glb", "Vines_5_1.glb", "Vines_5_2.glb"
        };

        private static readonly string[] _props =
        {
            "Apple_1.glb", "Barrel_1_1.glb", "Barrel_1_2.glb", "Barrel_2.glb", "Barrel_3.glb", "Bone_1.glb", "Bone_2.glb", "Bone_3.glb", "Bowl_1_1.glb", "Bowl_1_2.glb", "Bowl_1_3.glb", "Bowl_2_1.glb", "Bowl_2_2.glb", "Bowl_2_3.glb", "Bowl_2_4.glb", "Bowl_2_5.glb", "Cage_1.glb", "Cage_2.glb", "Chain_1_1.glb", "Chain_1_2.glb", "Chain_1_3.glb", "Chain_2_1.glb", "Chain_2_2.glb", "Chain_2_3.glb", "Chain_3_1.glb", "Chain_3_3.glb", "Chest_1.glb", "Chest_2.glb", "Cofin.glb", "Coin_1.glb", "Coins_1.glb", "Cup_1_1.glb", "Cup_1_2.glb", "Cup_1_3.glb", "Cup_2_1.glb", "Cup_2_2.glb", "Cup_2_3.glb", "Cup_3_1.glb", "Cup_3_2.glb", "Cup_3_3.glb", "Cup_4_1.glb", "Cup_4_2.glb", "Cup_4_3.glb", "FloorCrateA.glb", "Key_1_1.glb", "Key_1_2.glb", "Keys_1_1.glb", "Keys_1_2.glb", "Meat_1.glb", "Nail_1_1.glb", "Nail_1_2.glb", "Plate_1_1.glb", "Plate_1_2.glb", "Plate_1_3.glb", "Potion_1_1.glb", "Potion_1_2.glb", "Potion_1_3.glb", "Potion_1_4.glb", "Potion_2_1.glb", "Potion_2_2.glb", "Potion_2_3.glb", "Potion_2_4.glb", "Potion_3_1.glb", "Potion_3_2.glb", "Potion_3_3.glb", "Potion_3_4.glb", "Potion_4_1.glb", "Potion_4_2.glb", "Potion_4_3.glb", "Potion_4_4.glb", "Potion_4_5.glb", "Shield_1_1.glb", "Shield_1_2.glb", "Shield_3.glb", "Shield_4.glb", "Shield_5.glb", "Skeleton.glb", "Skull.glb", "SkullCandle.glb", "Sword_1.glb", "Sword_2.glb", "Sztandar1_1.glb", "Sztandar1_10.glb", "Sztandar1_11.glb", "Sztandar1_12.glb", "Sztandar1_13.glb", "Sztandar1_2.glb", "Sztandar1_3.glb", "Sztandar1_4.glb", "Sztandar1_5.glb", "Sztandar1_6.glb", "Sztandar1_7.glb", "Sztandar1_8.glb", "Sztandar1_9.glb", "Treasure2.glb", "Treasure_1.glb"
        };

        private static readonly string[] _furniture =
        {
            "Book_1_1.glb", "Book_1_2.glb", "Book_1_3.glb", "Book_1_4.glb", "Book_1_6.glb", "Book_1_7.glb", "Book_2_1.glb", "Book_2_2.glb", "Book_2_3.glb", "Book_2_4.glb", "Book_2_5.glb", "Book_3_1.glb", "Book_3_2.glb", "Book_3_3.glb", "Book_3_4.glb", "Book_3_5.glb", "Book_3_6.glb", "Book_6.glb", "Chair_1.glb", "Shelf_Long_1.glb", "Shelf_Long_2.glb", "Stool_1.glb", "Stool_1_2.glb", "Table_1.glb", "Table_1_2.glb", "Table_2_1.glb", "Table_2_2.glb", "Table_2_3.glb", "Table_2_4.glb", "Table_2_5.glb", "Table_3.glb", "Table_3_2.glb", "Wardrobe_1.glb", "Wardrobe_2.glb", "Wardrobe_3.glb", "hanging_shelf_1.glb", "hanging_shelf_1_2.glb", "hanging_shelf_2.glb", "hanging_shelf_2_2.glb"
        };

        private static readonly string[] _lighting =
        {
            "CandleHolder_1_1.glb", "CandleHolder_1_2.glb", "CandleHolder_1_3.glb", "CandleHolder_1_4.glb", "Candle_1.glb", "Candle_2.glb", "Candle_3.glb", "Candle_4.glb", "Torch.glb"
        };

        private static readonly string[] _misc =
        {
            
        };
    }
}
