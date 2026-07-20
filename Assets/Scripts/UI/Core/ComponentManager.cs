using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class ComponentManager : MonoBehaviour
    {
        public static ComponentManager Instance { get; private set; }

        private Dictionary<string, MonoBehaviour> _components = new Dictionary<string, MonoBehaviour>();

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

        public T GetComponent<T>(string componentName) where T : MonoBehaviour
        {
            if (_components.TryGetValue(componentName, out MonoBehaviour component))
            {
                return component as T;
            }
            return null;
        }

        public void RegisterComponent<T>(string name, T component) where T : MonoBehaviour
        {
            _components.Add(name, component);
        }
    }
}