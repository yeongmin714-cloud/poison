using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private Dictionary<string, GameObject> _uiPanels = new Dictionary<string, GameObject>();
        private Dictionary<string, MonoBehaviour> _uiComponents = new Dictionary<string, MonoBehaviour>();

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

        public void ShowPanel(string panelName)
        {
            if (_uiPanels.TryGetValue(panelName, out GameObject panel))
            {
                panel.SetActive(true);
            }
        }

        public void HidePanel(string panelName)
        {
            if (_uiPanels.TryGetValue(panelName, out GameObject panel))
            {
                panel.SetActive(false);
            }
        }

        public GameObject GetPanel(string panelName)
        {
            if (_uiPanels.TryGetValue(panelName, out GameObject panel))
            {
                return panel;
            }
            return null;
        }

        public T GetComponent<T>(string componentName) where T : MonoBehaviour
        {
            if (_uiComponents.TryGetValue(componentName, out MonoBehaviour component))
            {
                return component as T;
            }
            return null;
        }

        public void RegisterPanel(string name, GameObject panel)
        {
            if (!_uiPanels.ContainsKey(name))
            {
                _uiPanels.Add(name, panel);
            }
        }

        public void RegisterComponent<T>(string name, T component) where T : MonoBehaviour
        {
            if (!_uiComponents.ContainsKey(name))
            {
                _uiComponents.Add(name, component);
            }
        }
    }
}