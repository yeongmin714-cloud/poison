using System;
using UnityEngine;
using System.Collections.Generic;
#pragma warning disable 0414

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
        [SerializeField] private List<Recipe> recipes = new List<Recipe>();

        /// <summary>
        /// 인스펙터/에디터에서 리스트 읽기 전용 접근 (에디터 스크립트용)
        /// </summary>
        public IReadOnlyList<Recipe> Recipes => recipes;

        /// <summary>
        /// 레시피 목록을 설정합니다 (에디터 스크립트에서 사용).
        /// </summary>
        public void SetRecipes(List<Recipe> newRecipes)
        {
            recipes = newRecipes ?? new List<Recipe>();
        }

        /// <summary>
        /// 레시피 이름으로 찾기 (표시명 기준, 대소문자 무시)
        /// </summary>
        public Recipe GetRecipeByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return recipes.Find(r =>
                r != null && string.Equals(r.displayName, name, StringComparison.OrdinalIgnoreCase));
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