using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.UI;

namespace Game.UI.Core
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private List<GameObject> uiElements;
        
        // Window references
        public UIWindow craftingWindow;
        public UIWindow inventoryWindow;
        public UIWindow churchWindow;
        public UIWindow shopWindow;
        public UIWindow warehouseWindow;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize()
        {
            if (uiElements != null)
            {
                foreach (var element in uiElements)
                {
                    if (element != null)
                        element.SetActive(true);
                }
            }
            else
            {
                Debug.LogWarning("UIElements list is null in UIManager");
            }
        }

        public void OpenWindow(System.Type windowType)
        {
            // Find and activate window
            var window = FindAnyObjectByType(windowType) as MonoBehaviour;
            if (window != null)
            {
                window.gameObject.SetActive(true);
            }
        }
        
        public void OpenWindow(UIWindow window)
        {
            if (window != null)
            {
                window.gameObject.SetActive(true);
            }
        }
        
        public void CloseWindow(UIWindow window)
        {
            if (window != null)
            {
                window.gameObject.SetActive(false);
            }
        }
        
        public bool IsWindowOpen(UIWindow window)
        {
            return window != null && window.gameObject.activeInHierarchy;
        }
    }
}