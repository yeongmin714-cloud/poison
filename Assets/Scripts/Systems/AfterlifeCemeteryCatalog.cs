using System.Collections.Generic;

namespace ProjectName.Systems
{
    /// <summary>
    /// [155차 P0] 이계 공동묘지 에셋 카탈로그 — Necropoly(EmaceArt) 선별 프리팹 키 단일소스.
    /// 실물: Assets/Resources/Models/Cemetery/Cem_*.prefab (24종 — 2026-11-05 수용).
    /// 로드: RuntimeModelLoader 3차 폴백(Models/UserProvided → Models/Dungeon → Models/Cemetery).
    /// 던전 팩 보조 소품(Cofin/Skeleton/Skull/SkullCandle 등)은 DungeonAssetCatalog 키 재사용.
    /// </summary>
    public static class AfterlifeCemeteryCatalog
    {
        public enum Category { Statue, Grave, Structure, Prop }

        /// <summary>키 → 카테고리. 키는 Resources 파일명(.prefab 제외)과 1:1 — 오타 금지 계약.</summary>
        private static readonly Dictionary<string, Category> Keys = new Dictionary<string, Category>
        {
            // 석상 — 사용자 핵심 요구(공동묘지 석상)
            { "Cem_Statue_01", Category.Statue },
            { "Cem_Statue_02", Category.Statue },
            { "Cem_Statue_03", Category.Statue },
            { "Cem_Statue_04", Category.Statue },
            { "Cem_Statue_05", Category.Statue },
            { "Cem_Statue_06", Category.Statue },   // Gargoyle
            { "Cem_Statue_07", Category.Statue },   // Eagle
            // 무덤
            { "Cem_Grave_01", Category.Grave },     // Stone Mini
            { "Cem_Grave_02", Category.Grave },     // Wooden Mini
            { "Cem_Grave_03", Category.Grave },     // Broken Full
            { "Cem_Grave_04", Category.Grave },     // Massive Clean
            { "Cem_Grave_05", Category.Grave },     // Modern Full
            // 구조물
            { "Cem_Tomb_Niche", Category.Structure },   // 납골묘
            { "Cem_Obelisk", Category.Structure },
            { "Cem_Monument", Category.Structure },
            { "Cem_Fountain", Category.Structure },
            // 소품
            { "Cem_Coffin", Category.Prop },
            { "Cem_Cross", Category.Prop },
            { "Cem_Candle", Category.Prop },
            { "Cem_Bone", Category.Prop },
            { "Cem_Urn", Category.Prop },
            { "Cem_Lantern", Category.Prop },
            { "Cem_Bench", Category.Prop },
            { "Cem_Fence", Category.Prop },
        };

        public const string ResourceRoot = "Models/Cemetery/";

        public static IReadOnlyDictionary<string, Category> All => Keys;

        public static bool TryGet(string key, out Category category) => Keys.TryGetValue(key, out category);

        public static Category CategoryOf(string key) => Keys.TryGetValue(key, out var c) ? c : Category.Prop;

        public static IEnumerable<string> NamesIn(Category category)
        {
            foreach (var kv in Keys)
                if (kv.Value == category) yield return kv.Key;
        }
    }
}
