using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.UI.Core
{
    public class ComponentManager : MonoBehaviour
    {
        private Dictionary<string, MonoBehaviour> components = new Dictionary<string, MonoBehaviour>();

        public void RegisterComponent(string name, MonoBehaviour component)
        {
            if (components.ContainsKey(name))
            {
                Debug.LogWarning($"Component with name '{name}' already registered.");
                return;
            }
            components.Add(name, component);
        }

        public T GetComponent<T>(string name) where T : MonoBehaviour
        {
            if (components.TryGetValue(name, out MonoBehaviour component))
            {
                return component as T;
            }
            Debug.LogWarning($"Component with name '{name}' not found.");
            return null;
        }

        public void UnregisterComponent(string name)
        {
            components.Remove(name);
        }
    }
}