using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class UICore : MonoBehaviour
    {
        public static UICore Instance { get; private set; }

        private Dictionary<string, GameObject> _uiElements = new Dictionary<string, GameObject>();
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

        public void RegisterUIElement(string name, GameObject element)
        {
            if (!_uiElements.ContainsKey(name))
            {
                _uiElements.Add(name, element);
            }
        }

        public GameObject GetUIElement(string name)
        {
            if (_uiElements.TryGetValue(name, out GameObject element))
            {
                return element;
            }
            return null;
        }

        public T GetUIComponent<T>(string name) where T : MonoBehaviour
        {
            if (_uiComponents.TryGetValue(name, out MonoBehaviour component))
            {
                return component as T;
            }
            return null;
        }

        public void RegisterUIComponent<T>(string name, T component) where T : MonoBehaviour
        {
            if (!_uiComponents.ContainsKey(name))
            {
                _uiComponents.Add(name, component);
            }
        }
    }
}