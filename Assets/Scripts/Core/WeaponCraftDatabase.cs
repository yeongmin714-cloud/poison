using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// P18-C1 — 무기/장비 제작 레시피 데이터베이스 (정적).
    /// 마인크래프트식 확정 제작: 재료 소모 → 결과 지급(성공률 100%).
    /// 재료는 기존 드랍품(멧돼지 엄니/늑대 이빨/가죽/모피/토끼털)으로 구성 — 신규 아이템 0.
    /// 결과는 PlayerInventory의 Tiered 무기/방어구 ID 그대로 사용(단일 소스).
    /// </summary>
    public static class WeaponCraftDatabase
    {
        public struct WeaponRecipe
        {
            public string ResultId;      // PlayerInventory.GetItemById로 조회되는 실제 아이템 ID
            public string Mat1Id; public int Mat1Count;
            public string Mat2Id; public int Mat2Count;   // 0개 = 재료 1종
            public int RequiredLevel;
        }

        private static readonly List<WeaponRecipe> _recipes = new List<WeaponRecipe>
        {
            // ── Wood 티어 (멧돼지 엄니 = 뿔 재료, 토끼털 = 묶음) ──
            new WeaponRecipe { ResultId="weapon_dagger_wood", Mat1Id="mat_boar_tusk", Mat1Count=1, Mat2Count=0, RequiredLevel=1 },
            new WeaponRecipe { ResultId="weapon_sword_wood",  Mat1Id="mat_boar_tusk", Mat1Count=2, Mat2Count=0, RequiredLevel=1 },
            new WeaponRecipe { ResultId="weapon_spear_wood",  Mat1Id="mat_boar_tusk", Mat1Count=2, Mat2Id="mat_rabbit_fur", Mat2Count=1, RequiredLevel=2 },
            new WeaponRecipe { ResultId="weapon_bow_wood",    Mat1Id="mat_boar_tusk", Mat1Count=1, Mat2Id="mat_rabbit_fur", Mat2Count=2, RequiredLevel=2 },

            // ── Steel 티어 (늑대 이빨 = 날, 가죽/모피 = 손잡이/시위) ──
            new WeaponRecipe { ResultId="weapon_dagger_steel", Mat1Id="mat_wolf_tooth", Mat1Count=2, Mat2Count=0, RequiredLevel=5 },
            new WeaponRecipe { ResultId="weapon_sword_steel",  Mat1Id="mat_wolf_tooth", Mat1Count=3, Mat2Id="mat_boar_leather", Mat2Count=1, RequiredLevel=6 },
            new WeaponRecipe { ResultId="weapon_spear_steel",  Mat1Id="mat_wolf_tooth", Mat1Count=2, Mat2Id="mat_boar_leather", Mat2Count=2, RequiredLevel=6 },
            new WeaponRecipe { ResultId="weapon_bow_steel",    Mat1Id="mat_wolf_tooth", Mat1Count=1, Mat2Id="mat_wolf_fur", Mat2Count=2, RequiredLevel=7 },

            // ── 방어구 (가죽 계열) ──
            new WeaponRecipe { ResultId="armor_leather",  Mat1Id="mat_boar_leather", Mat1Count=3, Mat2Count=0, RequiredLevel=3 },
            new WeaponRecipe { ResultId="mat_rabbit_fur", Mat1Id="mat_rabbit_fur",   Mat1Count=0, Mat2Count=0, RequiredLevel=0, },
        };

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
