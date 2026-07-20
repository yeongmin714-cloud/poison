using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.UI;  // UIWindow 참조용

namespace ProjectName.UI.Core
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI References")]
        public UIScreen[] screens;

        // 개별 윈도우 레퍼런스들
        public WarehouseUI warehouseWindow;
        public InventoryWindow inventoryWindow;
        public CraftingUI craftingWindow;
        public ChurchUI churchWindow;
        public AlchemyUI alchemyWindow;
        public CookingUI cookingWindow;
        public QuickSlotUI quickSlotWindow;
        public RepairStationUI repairWindow;

        // UIWindow 레퍼런스들 (타입별 열기용)
        public UIWindow shopWindow;
        public UIWindow craftingWindowUI;
        public UIWindow repairWindowUI;
        public UIWindow alchemyWindowUI;
        public UIWindow cookingWindowUI;

        void Awake()
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

        private void Start()
        {
            Debug.Log("UI Manager initialized");
        }

        public void ShowScreen(UIScreen screen)
        {
            if (screens == null) return;
            foreach (var s in screens)
            {
                if (s != null)
                    s.gameObject.SetActive(s == screen);
            }
        }

        public void ToggleWindow(UIScreen screen)
        {
            if (screen != null)
                screen.gameObject.SetActive(!screen.gameObject.activeSelf);
        }

        public void ToggleWindow(string screenName)
        {
            if (screens == null) return;
            foreach (var s in screens)
            {
                if (s != null && s.name == screenName)
                {
                    s.gameObject.SetActive(!s.gameObject.activeSelf);
                    break;
                }
            }
        }

        // 타입으로 UIScreen 열기
        public void OpenWindow(System.Type windowType)
        {
            var screen = FindScreenByType(windowType);
            if (screen != null)
            {
                screen.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"[UIManager] Window of type {windowType.Name} not found in screens array.");
            }
        }

        // UIScreen으로 직접 열기
        public void OpenWindow(UIScreen screen)
        {
            if (screen != null)
            {
                screen.gameObject.SetActive(true);
            }
        }

        // UIWindow 타입으로 열기 (ShopWindow 등)
        public void OpenWindow<UIWindowType>() where UIWindowType : UIWindow
        {
            var window = FindWindowByType<UIWindowType>();
            if (window != null)
            {
                window.Show();
            }
            else
            {
                Debug.LogWarning($"[UIManager] Window of type {typeof(UIWindowType).Name} not found.");
            }
        }

        // UIWindow 인스턴스로 직접 열기
        public void OpenWindow(UIWindow window)
        {
            if (window != null)
            {
                window.Show();
            }
        }

        // 타입으로 UIScreen 찾기
        private UIScreen FindScreenByType(System.Type type)
        {
            if (screens == null) return null;
            foreach (var s in screens)
            {
                if (s != null && s.GetType() == type)
                    return s;
            }
            return null;
        }

        // 제네릭으로 UIWindow 찾기
        private UIWindowType FindWindowByType<UIWindowType>() where UIWindowType : UIWindow
        {
            var type = typeof(UIWindowType);
            
            // 개별 필드에서 찾기
            if (type.Name.Contains("Shop") && shopWindow != null)
                return (UIWindowType)(object)shopWindow;
            if (type.Name.Contains("Crafting") && craftingWindowUI != null)
                return (UIWindowType)(object)craftingWindowUI;
            if (type.Name.Contains("Repair") && repairWindowUI != null)
                return (UIWindowType)(object)repairWindowUI;
            if (type.Name.Contains("Alchemy") && alchemyWindowUI != null)
                return (UIWindowType)(object)alchemyWindowUI;
            if (type.Name.Contains("Cooking") && cookingWindowUI != null)
                return (UIWindowType)(object)cookingWindowUI;

            // screens 배열에서 찾기
            if (screens != null)
            {
                foreach (var s in screens)
                {
                    if (s != null && s.GetType() == type)
                        return s as UIWindowType;
                }
            }
            return null;
        }

        // 개별 윈도우 필드로 찾기
        public UIWindow GetWindow(string windowName)
        {
            switch (windowName.ToLower())
            {
                case "warehouse": return warehouseWindow;
                case "inventory": return inventoryWindow;
                case "crafting": return craftingWindow;
                case "church": return churchWindow;
                case "alchemy": return alchemyWindow;
                case "cooking": return cookingWindow;
                case "quickslot": return quickSlotWindow;
                case "repair": return repairWindow;
                default: return null;
            }
        }
    }
}