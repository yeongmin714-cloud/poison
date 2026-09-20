using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// [Milestone A] 데이터 기반 제작 레시피 DB — ScriptableObject 에셋(Resources/CraftingRecipeDB)에 저장.
    /// WeaponCraftDatabase.EnsureValid가 SO 우선(Instance?), 없으면 정적 폴백(하위 호환).
    /// 사용자가 새 GLB/무기/방어구를 가져오면 여기에 레시피 행만 추가(코드 수정 최소화).
    /// </summary>
    [CreateAssetMenu(fileName = "RecipeDB", menuName = "Crafting/RecipeDB")]
    public class RecipeDB_SO : ScriptableObject
    {
        [System.Serializable]
        public class RecipeEntry
        {
            public string ResultId;          // 결과 아이템 ID (PlayerInventory 등록분)
            public string Mat1Id;  public int Mat1Count;
            public string Mat2Id;  public int Mat2Count;   // 0 = 재료 1종
            public int RequiredLevel;
            public ItemRarity rarity = ItemRarity.Common;  // 성공률 희귀도 페널티용
            public string category;                        // Weapon/Armor/Accessory/Food/Potion
        }

        public List<RecipeEntry> recipes = new List<RecipeEntry>();

        public IReadOnlyList<RecipeEntry> All => recipes;

        // Resources/CraftingRecipeDB.asset — 없으면 null → 호출부(WeaponCraftDatabase)가 정적 폴백.
        private static RecipeDB_SO _instance;
        public static RecipeDB_SO Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<RecipeDB_SO>("CraftingRecipeDB");
                return _instance;
            }
        }

        /// <summary>결과 아이템 ID로 레시피 검색 (없으면 false).</summary>
        public bool TryMatchResult(string resultId, out RecipeEntry entry)
        {
            entry = null;
            if (recipes != null)
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    var e = recipes[i];
                    if (e != null && !string.IsNullOrEmpty(e.ResultId) && e.ResultId == resultId)
                    {
                        entry = e;
                        return true;
                    }
                }
            }
            return false;
        }
    }

#if UNITY_EDITOR
    public static class RecipeDBEditor
    {
        /// <summary>현재 정적 WeaponCraftDatabase를 RecipeDB_SO 에셋으로 마이그레이션(데이터 기반 운영 전환).</summary>
        [UnityEditor.MenuItem("Tools/Crafting/Generate RecipeDB From WeaponCraftDatabase")]
        public static void GenerateFromStatic()
        {
            var db = ScriptableObject.CreateInstance<RecipeDB_SO>();
            foreach (var r in WeaponCraftDatabase.AllIncludingRaw)
            {
                if (string.IsNullOrEmpty(r.Mat1Id) || r.Mat1Count <= 0) continue; // 더미 제외
                db.recipes.Add(new RecipeDB_SO.RecipeEntry
                {
                    ResultId = r.ResultId,
                    Mat1Id = r.Mat1Id, Mat1Count = r.Mat1Count,
                    Mat2Id = r.Mat2Id, Mat2Count = r.Mat2Count,
                    RequiredLevel = r.RequiredLevel,
                    rarity = r.rarity,
                    category = (r.ResultId != null && r.ResultId.StartsWith("armor_", System.StringComparison.Ordinal))
                        ? "Armor" : "Weapon",
                });
            }

            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");

            const string path = "Assets/Resources/CraftingRecipeDB.asset";
            UnityEditor.AssetDatabase.CreateAsset(db, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[RecipeDBEditor] {db.recipes.Count}개 레시피 → {path} (데이터 기반 운영 시작)");
        }
    }
#endif
}
