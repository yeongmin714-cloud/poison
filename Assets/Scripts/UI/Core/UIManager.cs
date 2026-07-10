using UnityEngine;

namespace ProjectName.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void ShowUI(string uiName)
        {
            // Implementation for showing UI
        }

        public void HideUI(string uiName)
        {
            // Implementation for hiding UI
        }

        public void ToggleUI(string uiName)
        {
            // Implementation for toggling UI
        }

        public void OpenWindow(System.Type windowType)
        {
            Debug.Log("[UIManager] OpenWindow: " + windowType.Name);
        }

        public void ToggleWindow(System.Type windowType)
        {
            Debug.Log("[UIManager] ToggleWindow: " + windowType.Name);
        }

        public void ToggleWindow(UIWindow window)
        {
            Debug.Log("[UIManager] ToggleWindow: " + window.GetType().Name);
        }

        public static CraftingUI craftingWindow { get; set; }
        public static InventoryWindow inventoryWindow { get; set; }
        public static WarehouseUI warehouseWindow { get; set; }
        public static LootWindow lootWindow { get; set; }
        public static QuestWindow questWindow { get; set; }
        public static RecipeWindow recipeWindow { get; set; }
        public static MapWindow mapWindow { get; set; }

        public void SetKeyBindings(KeyBindings keyBindings)
        {
            // 키 바인딩 설정 (placeholder)
            Debug.Log("[UIManager] SetKeyBindings: " + keyBindings);
        }
    }
}