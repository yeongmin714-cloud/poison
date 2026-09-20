using System.Collections.Generic;

namespace ProjectName.Core
{
    /// <summary>
    /// [Milestone C] 제작 불가(비제작) 최상위 아이템 카탈로그 — 희귀 드랍 / 비밀상점 전용.
    /// 무기·방어구 레시피(WeaponCraftDatabase)에 등재하면 절대 안 됨(제작 불가 보장).
    /// </summary>
    public static class NonCraftableCatalog
    {
        private static readonly HashSet<string> _ids = new HashSet<string>
        {
            "weapon_legendary",
            "weapon_unique_abyss",
            "weapon_unique_dawnblade",
            "armor_unique_voidplate",
        };

        /// <summary>이 아이템은 제작 불가(비제작)인지.</summary>
        public static bool IsNonCraftable(string itemId) => !string.IsNullOrEmpty(itemId) && _ids.Contains(itemId);

        /// <summary>비제작 최상위 아이템 전체.</summary>
        public static IReadOnlyCollection<string> All => _ids;
    }
}
