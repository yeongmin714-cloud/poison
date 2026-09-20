using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P18-C2 — 연금술 제작대 (마인크래프트식, UTK).
    /// 레시피 = HerbComboDatabase.AllCombos(GAME_DATA 조합법) 전체 나열 — 재료는 DB 약초.
    /// 제작 = CraftingHelper.CraftAlchemy(성공률 롤/재료 소모/실패 페널티 — 기존 로직 재사용).
    /// ⚠️ 알려진 간극: DB 약초(붉은줄기 등)는 인벤 약초(치유초 등)와 별개 체계 — DB 약초 아이템
    ///   획득 경로(채집/재배 데이터 패스) 연결 전까지는 재료 부족으로 실패할 수 있음(정상 경로).
    /// </summary>
    public class AlchemyBenchUTK : CraftBenchBaseUTK
    {
        private static AlchemyBenchUTK _instance;

        public static void Open()
        {
            if (_instance == null || _instance.panel == null)
                _instance = new AlchemyBenchUTK();
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) { Debug.LogWarning("[AlchemyBenchUTK] UIRoot 없음"); return; }
            if (_instance.parent == null) root.Add(_instance);
            UTKThreeColumnLayout.Place(_instance, 1);
            _instance.Show();
        }

        private AlchemyBenchUTK() : base("⚗️ 연금술 대", 2, new Vector2(520f, 560f)) { }

        protected override IReadOnlyList<BenchRecipe> Recipes
        {
            get
            {
                var list = new List<BenchRecipe>();
                foreach (var kv in ProjectName.Core.Data.HerbComboDatabase.AllCombos)
                {
                    // 키 = "id1_id2" (순서무관, MakeKey 언더스코어 결합) — 첫 '_'에서 분리
                    int sep = kv.Key.IndexOf('_');
                    if (sep <= 0) continue;
                    var pair = new[] { kv.Key.Substring(0, sep), kv.Key.Substring(sep + 1) };
                    var h1 = ProjectName.Core.Data.HerbDatabase.GetHerbInfo(pair[0]);
                    var h2 = ProjectName.Core.Data.HerbDatabase.GetHerbInfo(pair[1]);
                    if (string.IsNullOrEmpty(h1.id) || string.IsNullOrEmpty(h2.id)) continue;
                    list.Add(new BenchRecipe
                    {
                        ResultId = kv.Key,
                        ResultName = kv.Value.resultName ?? "???",
                        MatIds = new[] { pair[0], pair[1] },
                        Note = kv.Value.effect,
                    });
                }
                return list;
            }
        }

        protected override bool TryCraft(BenchRecipe recipe, List<string> placedIds, out string message)
        {
            int sep = recipe.ResultId.IndexOf('_');
            if (sep <= 0) { message = "잘못된 레시피"; return false; }
            bool ok = CraftingHelper.CraftAlchemy(recipe.ResultId.Substring(0, sep), recipe.ResultId.Substring(sep + 1));
            message = ok ? "🟢 물약 제작 성공!" : "🔴 물약 제작 실패 — 재료가 없거나 손실되었다.";
            return ok;
        }
    }
}
