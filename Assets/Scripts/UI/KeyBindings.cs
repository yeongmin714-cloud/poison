using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// 키 설정을 저장하는 ScriptableObject.
    /// 
    /// [Unity 초보자 설명]
    /// ScriptableObject = Unity에서 데이터를 저장하는 특별한 파일.
    /// 게임 실행 중에도 값을 바꿀 수 있고, 에디터에서도 설정 가능.
    /// 
    /// 사용법:
    /// 1. 프로젝트 창에서 우클릭 → Create → UI → Key Bindings
    /// 2. 각 액션에 원하는 키를 설정
    /// 3. UIManager가 이 설정을 읽어서 사용
    /// </summary>
    [CreateAssetMenu(fileName = "KeyBindings", menuName = "포이즌/Key Bindings")]
    public class KeyBindings : ScriptableObject
    {
        [Header("=== 단축키 설정 ===")]
        [SerializeField] private KeyCode _questKey = KeyCode.Q;
        [SerializeField] private KeyCode _recipeKey = KeyCode.R;
        [SerializeField] private KeyCode _inventoryKey = KeyCode.I;
        [SerializeField] private KeyCode _mapKey = KeyCode.M;
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

        [Header("Status Window")]
        [SerializeField] private KeyCode _statusKey = KeyCode.C;

        [Header("Revenge List")]
        [SerializeField] private KeyCode _revengeListKey = KeyCode.K;

        [Header("Crafting Table")]
        [SerializeField] private KeyCode _craftingKey = KeyCode.C;

        [Header("Equipment Window")]
        [SerializeField] private KeyCode _equipmentKey = KeyCode.E;

        [Header("Warehouse")]
        [SerializeField] private KeyCode _warehouseKey = KeyCode.W;

        // 액션 이름 → KeyCode 매핑 (코드에서 사용)
        private Dictionary<string, KeyCode> _bindings;

        /// <summary>
        /// 모든 키 바인딩을 딕셔너리로 반환
        /// </summary>
        public Dictionary<string, KeyCode> GetAllBindings()
        {
            if (_bindings == null)
            {
                _bindings = new Dictionary<string, KeyCode>
                {
                    { "Quest", _questKey },
                    { "Recipe", _recipeKey },
                    { "Inventory", _inventoryKey },
                    { "Map", _mapKey },
                    { "Close", _closeKey },
                    { "Status", _statusKey },
                    { "RevengeList", _revengeListKey },
                    { "Crafting", _craftingKey },
                    { "Equipment", _equipmentKey },
                    { "Warehouse", _warehouseKey }
                };
            }
            return _bindings;
        }

        /// <summary>
        /// 특정 액션의 키를 변경 (런타임 중에도 가능)
        /// </summary>
        public void SetKey(string actionName, KeyCode newKey)
        {
            var bindings = GetAllBindings();
            if (bindings.ContainsKey(actionName))
            {
                bindings[actionName] = newKey;
                
                // Serialized 필드도 업데이트
                switch (actionName)
                {
                    case "Quest": _questKey = newKey; break;
                    case "Recipe": _recipeKey = newKey; break;
                    case "Inventory": _inventoryKey = newKey; break;
                    case "Map": _mapKey = newKey; break;
                    case "Close": _closeKey = newKey; break;
                    case "Status": _statusKey = newKey; break;
                    case "RevengeList": _revengeListKey = newKey; break;
                    case "Crafting": _craftingKey = newKey; break;
                    case "Equipment": _equipmentKey = newKey; break;
                    case "Warehouse": _warehouseKey = newKey; break;
                }
            }
            else
            {
                Debug.LogWarning($"[KeyBindings] 알 수 없는 액션: {actionName}");
            }
        }

        /// <summary>
        /// 특정 액션의 현재 키 반환
        /// </summary>
        public KeyCode GetKey(string actionName)
        {
            var bindings = GetAllBindings();
            return bindings.ContainsKey(actionName) ? bindings[actionName] : KeyCode.None;
        }

        /// <summary>
        /// 가능한 모든 액션 이름 목록
        /// </summary>
        public static string[] GetActionNames()
        {
            return new[] { "Quest", "Recipe", "Inventory", "Map", "Close", "Status", "RevengeList", "Crafting", "Equipment", "Warehouse" };
        }
    }
}