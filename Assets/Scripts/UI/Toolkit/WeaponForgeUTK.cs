using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P18-C2 — 무기/장비 제작대 (마인크래프트식, UTK).
    /// 재료 3칸 + 결과 1칸. 레시피 = WeaponCraftDatabase(멧돼지 엄니/늑대 이빨/가죽 계열).
    /// 제작 = CraftingHelper.CraftWeapon(확정 성공, EXP+20, CraftSucceeded 발화).
    /// </summary>
    public class WeaponForgeUTK : CraftBenchBaseUTK
    {
        private static WeaponForgeUTK _instance;

        public static void Open()
        {
            if (_instance == null || _instance.panel == null)
                _instance = new WeaponForgeUTK();
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) { Debug.LogWarning("[WeaponForgeUTK] UIRoot 없음"); return; }
            if (_instance.parent == null) root.Add(_instance);
            UTKThreeColumnLayout.Place(_instance, 1);
            _instance.Show();
        }

        private WeaponForgeUTK() : base("⚒️ 무기 제작대", 3, new Vector2(520f, 560f)) { }

        protected override IReadOnlyList<BenchRecipe> Recipes
        {
            get
            {
                var list = new List<BenchRecipe>();
                foreach (var r in WeaponCraftDatabase.All)
                {
                    var mats = new string[3];
                    mats[0] = r.Mat1Id;
                    if (r.Mat1Count >= 2) mats[1] = r.Mat1Id;
                    if (r.Mat1Count >= 3) mats[2] = r.Mat1Id;
                    if (r.Mat2Count >= 1 && !string.IsNullOrEmpty(r.Mat2Id))
                        mats[r.Mat1Count >= 2 ? 2 : 1] = r.Mat2Id;

                    list.Add(new BenchRecipe
                    {
                        ResultId = r.ResultId,
                        ResultName = WeaponCraftDatabase.DisplayName(r.ResultId),
                        MatIds = mats,
                        Note = $"Lv.{r.RequiredLevel}+",
                        rarity = r.rarity,   // [Milestone A/D] 성공률 희귀도 페널티
                    });
                }
                return list;
            }
        }

        // [Milestone D] 미발견 레시피 = "?" 블라인드 — 성공 시 CraftWeapon이 MarkDiscovered → 공개.
        protected override bool IsDiscovered(BenchRecipe r) => RecipeDiscoverySystem.IsDiscovered(r.ResultName);

        // [Milestone D] 발견된 레시피의 성공률·운 표시.
        protected override string RateHint(BenchRecipe r)
        {
            int chance = CraftingHelper.ComputeFinalCraftChance(CraftingHelper.WeaponBaseSuccessRate, r.rarity);
            string luck = "";
            if (PlayerStats.Instance != null)
            {
                int luckBonus = Mathf.RoundToInt(PlayerStats.Instance.GetLuckCraftBonus());
                if (luckBonus > 0) luck = $" (운 +{luckBonus}%)";
            }
            return $"성공률 {chance}%{luck}";
        }

        protected override bool TryCraft(BenchRecipe recipe, List<string> placedIds, out string message)
        {
            return CraftingHelper.CraftWeapon(recipe.ResultId, out message);
        }
    }
}
