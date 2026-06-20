using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// G2-07: 몬스터 드랍 항목 정의 구조체.
    /// 티어, 아이템ID, 확률, 개수 정보를 담습니다.
    /// </summary>
    public struct MonsterDropEntry
    {
        public MonsterTier tier;
        public string itemId;
        public float probability;
        public int minCount;
        public int maxCount;

        public MonsterDropEntry(MonsterTier tier, string itemId, float probability, int minCount, int maxCount = -1)
        {
            this.tier = tier;
            this.itemId = itemId;
            this.probability = Mathf.Clamp01(probability);
            this.minCount = Mathf.Max(1, minCount);
            this.maxCount = maxCount >= this.minCount ? maxCount : this.minCount;
        }
    }

    /// <summary>
    /// G2-07: 간소화된 정적 드랍 테이블 유틸리티.
    /// MonsterDropEntry 기반으로 몬스터/병사 사망 시 드랍 목록을 생성합니다.
    ///
    /// 등급 확률:
    ///   Common(90%), Uncommon(45%), Rare(10%), Epic(3%), Legendary(1%)
    /// </summary>
    public static class DropTableUtility
    {
        // ================================================================
        // 등급별 기본 확률
        // ================================================================
        public const float CommonChance     = 0.90f;
        public const float UncommonChance   = 0.45f;
        public const float RareChance       = 0.10f;
        public const float EpicChance       = 0.03f;
        public const float LegendaryChance  = 0.01f;

        // 희귀 보너스 확률 (모든 티어에 공통 적용)
        private const float RareDropBonus = 0.10f;

        // ================================================================
        // 아이템 ID 상수
        // ================================================================
        public const string GoldCoinId      = "gold_coin";
        public const string SilverCoinId    = "silver_coin";

        // Common 재료
        private static readonly string[] CommonMeatIds =
            { "meat", "raw_meat", "tough_meat" };
        private static readonly string[] CommonMaterialIds =
            { "leather", "fur", "bone", "feather" };

        // Uncommon 재료
        private static readonly string[] UncommonMaterialIds =
            { "iron_ore", "copper_ore", "thick_hide", "sharp_fang" };

        // Rare 재료
        private static readonly string[] RareMaterialIds =
            { "gold_ore", "ruby", "sapphire", "magic_crystal" };

        // Epic 재료
        private static readonly string[] EpicMaterialIds =
            { "dragon_scale", "phoenix_feather", "dark_essence" };

        // Legendary 재료
        private static readonly string[] LegendaryMaterialIds =
            { "excalibur_shard", "crown_of_light", "void_stone" };

        // 장비 ID (병사 드랍)
        private static readonly string[] GuardWeaponIds =
            { "iron_sword", "steel_sword", "spear", "war_axe", "mace" };
        private static readonly string[] GuardArmorIds =
            { "iron_helmet", "iron_armor", "iron_boots", "iron_shield" };

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// 몬스터 티어별 드랍 목록을 생성합니다.
        /// 기본적으로 1~3개의 일반/언커먼 아이템과 10% 확률의 희귀 아이템을 추가합니다.
        /// </summary>
        /// <param name="tier">몬스터 티어</param>
        /// <returns>아이템ID-개수 쌍의 리스트</returns>
        public static List<KeyValuePair<string, int>> GetMonsterDrops(MonsterTier tier)
        {
            var drops = new List<KeyValuePair<string, int>>();

            // 기본 드랍 1~3개 (티어가 높을수록 더 많은 드랍)
            int baseCount = tier switch
            {
                MonsterTier.Beginner     => Random.Range(1, 3),
                MonsterTier.Intermediate => Random.Range(2, 4),
                MonsterTier.Advanced     => Random.Range(2, 5),
                _                        => Random.Range(1, 3)
            };

            for (int i = 0; i < baseCount; i++)
            {
                string itemId = RollCommonDrop(tier);
                if (!string.IsNullOrEmpty(itemId))
                {
                    int count = Random.Range(1, 4);
                    drops.Add(new KeyValuePair<string, int>(itemId, count));
                }
            }

            // 희귀 드랍: 10% 확률로 추가
            if (Random.value <= RareDropBonus)
            {
                string rareId = RollRareDrop(tier);
                if (!string.IsNullOrEmpty(rareId))
                {
                    int rareCount = Random.Range(1, 3);
                    drops.Add(new KeyValuePair<string, int>(rareId, rareCount));
                }
            }

            return drops;
        }

        /// <summary>
        /// 병사 드랍 목록을 생성합니다.
        /// 금화(레벨 비례) + 은화 + 장비 (등급별 확률)
        /// </summary>
        /// <param name="level">병사 레벨</param>
        /// <returns>아이템ID-개수 쌍의 리스트</returns>
        public static List<KeyValuePair<string, int>> GetGuardDrops(int level)
        {
            var drops = new List<KeyValuePair<string, int>>();

            // 금화: 레벨 비례
            int goldCount = Random.Range(level, Mathf.Max(level + 1, level * 2 + 1));
            drops.Add(new KeyValuePair<string, int>(GoldCoinId, goldCount));

            // 은화
            int silverCount = Random.Range(level * 2, level * 4 + 1);
            drops.Add(new KeyValuePair<string, int>(SilverCoinId, silverCount));

            // 장비: 레벨 기반 등급 확률
            TryRollGuardEquipment(drops, level);

            return drops;
        }

        /// <summary>
        /// MonsterDropEntry 배열을 기반으로 드랍을 생성합니다.
        /// 각 엔트리의 확률을 개별적으로 검사하여 드랍 여부를 결정합니다.
        /// </summary>
        public static List<KeyValuePair<string, int>> RollFromEntries(MonsterDropEntry[] entries)
        {
            var drops = new List<KeyValuePair<string, int>>();

            if (entries == null || entries.Length == 0)
                return drops;

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.itemId)) continue;

                if (Random.value <= entry.probability)
                {
                    int count = Random.Range(entry.minCount, entry.maxCount + 1);
                    drops.Add(new KeyValuePair<string, int>(entry.itemId, count));
                }
            }

            return drops;
        }

        // ================================================================
        // Private Helpers
        // ================================================================

        /// <summary>
        /// 티어 기반 일반 드랍 아이템 롤 (Common/Uncommon 위주)
        /// </summary>
        private static string RollCommonDrop(MonsterTier tier)
        {
            float roll = Random.value;

            // Legendary (1%) — 모든 티어에서 극소확률
            if (roll <= LegendaryChance)
                return LegendaryMaterialIds[Random.Range(0, LegendaryMaterialIds.Length)];

            // Epic (3%) — Intermediate 이상에서
            if (roll <= LegendaryChance + EpicChance && tier >= MonsterTier.Intermediate)
                return EpicMaterialIds[Random.Range(0, EpicMaterialIds.Length)];

            // Rare (10%) — Advanced에서 더 높은 확률
            if (roll <= LegendaryChance + EpicChance + RareChance)
            {
                if (tier == MonsterTier.Advanced && Random.value < 0.5f)
                    return RareMaterialIds[Random.Range(0, RareMaterialIds.Length)];
                // Intermediate에서도 소량 Rare
                if (tier >= MonsterTier.Intermediate && Random.value < 0.3f)
                    return RareMaterialIds[Random.Range(0, RareMaterialIds.Length)];
            }

            // Uncommon (45%) — Intermediate 이상
            if (roll <= LegendaryChance + EpicChance + RareChance + UncommonChance)
            {
                if (tier >= MonsterTier.Intermediate)
                    return UncommonMaterialIds[Random.Range(0, UncommonMaterialIds.Length)];
            }

            // Common (90%) — 모든 티어
            if (Random.value < 0.6f)
                return CommonMeatIds[Random.Range(0, CommonMeatIds.Length)];
            else
                return CommonMaterialIds[Random.Range(0, CommonMaterialIds.Length)];
        }

        /// <summary>
        /// 희귀 드랍 아이템 롤 (Rare/Epic/Legendary)
        /// </summary>
        private static string RollRareDrop(MonsterTier tier)
        {
            float roll = Random.value;

            if (roll <= LegendaryChance)
                return LegendaryMaterialIds[Random.Range(0, LegendaryMaterialIds.Length)];

            if (roll <= LegendaryChance + EpicChance && tier >= MonsterTier.Intermediate)
                return EpicMaterialIds[Random.Range(0, EpicMaterialIds.Length)];

            return RareMaterialIds[Random.Range(0, RareMaterialIds.Length)];
        }

        /// <summary>
        /// 병사 장비 드랍 롤 (레벨 기반 등급 확률)
        /// </summary>
        private static void TryRollGuardEquipment(List<KeyValuePair<string, int>> drops, int level)
        {
            // Common 장비 (레벨 1+): ~90%
            if (Random.value <= CommonChance)
            {
                string weapon = GuardWeaponIds[Random.Range(0, GuardWeaponIds.Length)];
                drops.Add(new KeyValuePair<string, int>(weapon, 1));
            }

            // Uncommon 장비 (레벨 3+): ~45%
            if (level >= 3 && Random.value <= UncommonChance)
            {
                string armor = GuardArmorIds[Random.Range(0, GuardArmorIds.Length)];
                drops.Add(new KeyValuePair<string, int>(armor, 1));
            }

            // Rare 장비 (레벨 5+): ~10%
            if (level >= 5 && Random.value <= RareChance)
            {
                string rareItem = RareMaterialIds[Random.Range(0, RareMaterialIds.Length)];
                drops.Add(new KeyValuePair<string, int>(rareItem, Random.Range(1, 3)));
            }

            // Epic 장비 (레벨 10+): ~3%
            if (level >= 10 && Random.value <= EpicChance)
            {
                drops.Add(new KeyValuePair<string, int>(GoldCoinId, Random.Range(5, 15)));
            }
        }
    }
}