using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Core
{
    /// <summary>
    /// P18-C1 → [Milestone A] 무기/장비 제작 레시피 데이터베이스.
    /// 확정 제작에서 운·희귀도 기반 성공 롤로 전환(CraftingHelper.CraftWeapon 참조).
    /// 재료는 기존 드랍품(멧돼지 엄니/늑대 이빨/가죽/모피/토끼털)으로 구성 — 신규 아이템 0.
    /// 결과는 PlayerInventory의 Tiered 무기/방어구 ID 그대로 사용(단일 소스).
    /// 데이터 소스 우선순위: RecipeDB_SO(Resources/CraftingRecipeDB) → 정적 _recipes(폴백, 하위 호환).
    /// </summary>
    public static class WeaponCraftDatabase
    {
        public struct WeaponRecipe
        {
            public string ResultId;      // PlayerInventory.GetItemById로 조회되는 실제 아이템 ID
            public string Mat1Id; public int Mat1Count;
            public string Mat2Id; public int Mat2Count;   // 0개 = 재료 1종
            public int RequiredLevel;
            public ItemRarity rarity;    // [Milestone A] 성공률 희귀도 페널티용
        }

        private static readonly List<WeaponRecipe> _recipes = new List<WeaponRecipe>
        {
            // ── Wood 티어 (멧돼지 엄니 = 뿔 재료, 토끼털 = 묶음) — Common ──
            new WeaponRecipe { ResultId="weapon_dagger_wood", Mat1Id="mat_boar_tusk", Mat1Count=1, Mat2Count=0, RequiredLevel=1, rarity=ItemRarity.Common },
            new WeaponRecipe { ResultId="weapon_sword_wood",  Mat1Id="mat_boar_tusk", Mat1Count=2, Mat2Count=0, RequiredLevel=1, rarity=ItemRarity.Common },
            new WeaponRecipe { ResultId="weapon_spear_wood",  Mat1Id="mat_boar_tusk", Mat1Count=2, Mat2Id="mat_rabbit_fur", Mat2Count=1, RequiredLevel=2, rarity=ItemRarity.Common },
            new WeaponRecipe { ResultId="weapon_bow_wood",    Mat1Id="mat_boar_tusk", Mat1Count=1, Mat2Id="mat_rabbit_fur", Mat2Count=2, RequiredLevel=2, rarity=ItemRarity.Common },

            // ── Steel 티어 (늑대 이빨 = 날, 가죽/모피 = 손잡이/시위) — Uncommon ──
            new WeaponRecipe { ResultId="weapon_dagger_steel", Mat1Id="mat_wolf_tooth", Mat1Count=2, Mat2Count=0, RequiredLevel=5, rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="weapon_sword_steel",  Mat1Id="mat_wolf_tooth", Mat1Count=3, Mat2Id="mat_boar_leather", Mat2Count=1, RequiredLevel=6, rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="weapon_spear_steel",  Mat1Id="mat_wolf_tooth", Mat1Count=2, Mat2Id="mat_boar_leather", Mat2Count=2, RequiredLevel=6, rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="weapon_bow_steel",    Mat1Id="mat_wolf_tooth", Mat1Count=1, Mat2Id="mat_wolf_fur", Mat2Count=2, RequiredLevel=7, rarity=ItemRarity.Uncommon },

            // ── 방어구 (가죽 계열) ──
            new WeaponRecipe { ResultId="armor_leather",  Mat1Id="mat_boar_leather", Mat1Count=3, Mat2Count=0, RequiredLevel=3, rarity=ItemRarity.Common },
            new WeaponRecipe { ResultId="mat_rabbit_fur", Mat1Id="mat_rabbit_fur",   Mat1Count=0, Mat2Count=0, RequiredLevel=0, rarity=ItemRarity.Common },

            // ── [Milestone B] Stone 티어 (석재 + 늑대 이빨) — Uncommon ──
            new WeaponRecipe { ResultId="weapon_dagger_stone", Mat1Id="mat_stone", Mat1Count=2, Mat2Id="mat_wolf_tooth", Mat2Count=1, RequiredLevel=4, rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="weapon_sword_stone",  Mat1Id="mat_stone", Mat1Count=3, Mat2Id="mat_wolf_tooth", Mat2Count=1, RequiredLevel=5, rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="weapon_spear_stone",  Mat1Id="mat_stone", Mat1Count=3, Mat2Id="mat_wolf_tooth", Mat2Count=2, RequiredLevel=5, rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="weapon_bow_stone",    Mat1Id="mat_stone", Mat1Count=2, Mat2Id="mat_wolf_tooth", Mat2Count=1, RequiredLevel=6, rarity=ItemRarity.Uncommon },

            // ── [Milestone B] Crystal 티어 (수정석 + 미스릴) — Epic ──
            new WeaponRecipe { ResultId="weapon_dagger_crystal", Mat1Id="crystal_shard", Mat1Count=2, Mat2Id="mat_mythril_ore", Mat2Count=1, RequiredLevel=12, rarity=ItemRarity.Epic },
            new WeaponRecipe { ResultId="weapon_sword_crystal",  Mat1Id="crystal_shard", Mat1Count=3, Mat2Id="mat_mythril_ore", Mat2Count=1, RequiredLevel=13, rarity=ItemRarity.Epic },
            new WeaponRecipe { ResultId="weapon_spear_crystal",  Mat1Id="crystal_shard", Mat1Count=3, Mat2Id="mat_mythril_ore", Mat2Count=2, RequiredLevel=13, rarity=ItemRarity.Epic },
            new WeaponRecipe { ResultId="weapon_bow_crystal",    Mat1Id="crystal_shard", Mat1Count=2, Mat2Id="mat_mythril_ore", Mat2Count=1, RequiredLevel=14, rarity=ItemRarity.Epic },

            // ── [Milestone B] 방어구 세트 (광물 기반) — 헬멧/갑옷 ──
            new WeaponRecipe { ResultId="helmet_wood",   Mat1Id="mat_wood",   Mat1Count=2, Mat2Id="mat_boar_leather", Mat2Count=1, RequiredLevel=2, rarity=ItemRarity.Common },
            new WeaponRecipe { ResultId="armor_stone",   Mat1Id="mat_stone",  Mat1Count=3, Mat2Id="mat_wolf_fur",     Mat2Count=1, RequiredLevel=4, rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="helmet_stone",  Mat1Id="mat_stone",  Mat1Count=2, Mat2Id="mat_wolf_fur",     Mat2Count=1, RequiredLevel=4, rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="armor_steel",   Mat1Id="iron_ingot", Mat1Count=2, Mat2Id="mat_boar_leather", Mat2Count=1, RequiredLevel=8, rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="helmet_steel",  Mat1Id="iron_ingot", Mat1Count=1, Mat2Id="mat_boar_leather", Mat2Count=1, RequiredLevel=8, rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="armor_crystal", Mat1Id="crystal_shard", Mat1Count=3, Mat2Id="mat_mythril_ore", Mat2Count=1, RequiredLevel=14, rarity=ItemRarity.Epic },
            new WeaponRecipe { ResultId="helmet_crystal",Mat1Id="crystal_shard", Mat1Count=2, Mat2Id="mat_mythril_ore", Mat2Count=1, RequiredLevel=14, rarity=ItemRarity.Epic },

            // ── [Milestone B] 장신구 (반지/목걸이 — %버프) ──
            new WeaponRecipe { ResultId="ring_evasion",   Mat1Id="mat_silver_ore", Mat1Count=3, Mat2Id="mat_wolf_fur",     Mat2Count=1, RequiredLevel=6,  rarity=ItemRarity.Uncommon },
            new WeaponRecipe { ResultId="ring_vitality",  Mat1Id="mat_gold_ore",   Mat1Count=3, Mat2Id="mat_boar_leather", Mat2Count=1, RequiredLevel=8,  rarity=ItemRarity.Rare },
            new WeaponRecipe { ResultId="ring_power",     Mat1Id="crystal_shard",  Mat1Count=2, Mat2Id="iron_ingot",       Mat2Count=1, RequiredLevel=10, rarity=ItemRarity.Rare },
            new WeaponRecipe { ResultId="necklace_hp",    Mat1Id="mat_gold_ore",   Mat1Count=2, Mat2Id="crystal_shard",    Mat2Count=1, RequiredLevel=12, rarity=ItemRarity.Rare },
            new WeaponRecipe { ResultId="necklace_guard", Mat1Id="mat_mythril_ore", Mat1Count=2, Mat2Id="mat_stone", Mat2Count=1, RequiredLevel=13, rarity=ItemRarity.Epic },
        };

        /// <summary>[Milestone A] 마이그레이션용 원본 전체(더미 포함) — RecipeDB_SO 생성 메뉴에서 사용.</summary>
        public static IReadOnlyList<WeaponRecipe> AllIncludingRaw => _recipes;

        /// <summary>유효 레시피 전체 (더미 항목 제외 — 로드 시 정리).</summary>
        public static IReadOnlyList<WeaponRecipe> All
        {
            get { EnsureValid(); return _validRecipes; }
        }

        private static List<WeaponRecipe> _validRecipes;
        private static Dictionary<string, WeaponRecipe> _byResultId;

        private static void EnsureValid()
        {
            if (_validRecipes != null) return;
            _validRecipes = new List<WeaponRecipe>();
            _byResultId = new Dictionary<string, WeaponRecipe>();

            // [Milestone A] RecipeDB_SO(Resources/CraftingRecipeDB) 우선 — 데이터 기반 운영.
            // SO가 없거나 비어 있으면 아래 정적 _recipes 폴백(하위 호환).
            var so = RecipeDB_SO.Instance;
            if (so != null && so.All != null && so.All.Count > 0)
            {
                for (int i = 0; i < so.All.Count; i++)
                {
                    var e = so.All[i];
                    if (e == null || string.IsNullOrEmpty(e.ResultId)) continue;
                    if (e.Mat1Count <= 0) continue;                       // 무의미 더미 제외
                    if (PlayerInventory.GetItemById(e.ResultId) == null) continue; // 결과 아이템 미존재 제외
                    var r = new WeaponRecipe
                    {
                        ResultId = e.ResultId,
                        Mat1Id = e.Mat1Id, Mat1Count = e.Mat1Count,
                        Mat2Id = e.Mat2Id, Mat2Count = e.Mat2Count,
                        RequiredLevel = e.RequiredLevel,
                        rarity = e.rarity,
                    };
                    _validRecipes.Add(r);
                    _byResultId[r.ResultId] = r;
                }
                if (_validRecipes.Count > 0) return;
                _validRecipes.Clear(); // SO가 전부 무효였으면 정적 폴백으로 진행
                _byResultId.Clear();
            }

            foreach (var r in _recipes)
            {
                // 무의미 더미(재료 0개) 제외 + 결과 아이템이 실제 존재하는 것만
                if (r.Mat1Count <= 0) continue;
                var item = PlayerInventory.GetItemById(r.ResultId);
                if (item == null) continue;
                _validRecipes.Add(r);
                _byResultId[r.ResultId] = r;
            }
        }

        public static bool TryGetByResult(string resultId, out WeaponRecipe recipe)
        {
            EnsureValid();
            return _byResultId.TryGetValue(resultId, out recipe);
        }

        /// <summary>배치된 재료 ID 목록(멀티셋)과 일치하는 레시피 검색 — 순서 무관.</summary>
        public static bool TryMatch(List<string> placedItemIds, out WeaponRecipe recipe)
        {
            EnsureValid();
            recipe = default;
            if (placedItemIds == null || placedItemIds.Count == 0) return false;

            foreach (var r in _validRecipes)
            {
                var need = new List<string>();
                for (int i = 0; i < r.Mat1Count; i++) need.Add(r.Mat1Id);
                if (!string.IsNullOrEmpty(r.Mat2Id))
                    for (int i = 0; i < r.Mat2Count; i++) need.Add(r.Mat2Id);
                if (need.Count != placedItemIds.Count) continue;

                var pool = new List<string>(placedItemIds);
                bool all = true;
                foreach (var n in need)
                {
                    int idx = pool.FindIndex(p => p == n);
                    if (idx < 0) { all = false; break; }
                    pool.RemoveAt(idx);
                }
                if (all && pool.Count == 0)
                {
                    recipe = r;
                    return true;
                }
            }
            return false;
        }

        /// <summary>결과 아이템 표시명 (없으면 ID).</summary>
        public static string DisplayName(string resultId)
        {
            var item = PlayerInventory.GetItemById(resultId);
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : resultId;
        }
    }
}
