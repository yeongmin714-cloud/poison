using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class ComponentManager : MonoBehaviour
    {
        private Dictionary<string, IUIComponent> registeredComponents;

        public void Initialize()
        {
            registeredComponents = new Dictionary<string, IUIComponent>();
        }

        public void RegisterComponent(string id, IUIComponent component)
        {
            if(component != null)
            {
                if(!registeredComponents.ContainsKey(id))
                {
                    registeredComponents[id] = component;
                    component.Initialize();
                }
            }
        }

        public T GetComponent<T>(string id) where T : IUIComponent
        {
            if(registeredComponents.ContainsKey(id))
            {
                return (T)registeredComponents[id];
            }
            return default(T);
        }

        public void UnregisterComponent(string id)
        {
            if(registeredComponents.ContainsKey(id))
            {
                registeredComponents.Remove(id);
            }
        }
    }
}