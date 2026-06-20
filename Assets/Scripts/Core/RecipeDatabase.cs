using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.Core
{
    /// <summary>
    /// 레시피 데이터베이스 ScriptableObject (선택적)
    /// 보유한 모든 레시피를 관리합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "RecipeDatabase", menuName = "ProjectName/RecipeDatabase", order = 2)]
    public class RecipeDatabase : ScriptableObject
    {
        [Header("레시피 목록")]
        public List<Recipe> recipes = new List<Recipe>();

        /// <summary>
        /// 레시피 이름으로 찾기 (표시명 기준)
        /// </summary>
        public Recipe GetRecipeByName(string name)
        {
            return recipes.Find(r => r.displayName == name);
        }

        /// <summary>
        /// 인덱스로 레시피 가져오기
        /// </summary>
        public Recipe GetRecipeAt(int index)
        {
            if (index < 0 || index >= recipes.Count) return null;
            return recipes[index];
        }

        /// <summary>
        /// 모든 레시피 수
        /// </summary>
        public int Count => recipes.Count;
    }
}