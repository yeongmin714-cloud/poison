using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIResourceLoader : MonoBehaviour
    {
        [Header("Resource Settings")]
        public string resourcePath = "UI/Resources/";

        public T LoadResource<T>(string resourceName) where T : UnityEngine.Object
        {
            // Load a resource from the resources folder
            T resource = Resources.Load<T>(resourcePath + resourceName);
            if (resource == null)
            {
                Debug.LogError($"Failed to load resource: {resourceName}");
            }
            return resource;
        }

        public void LoadResources(string[] resourceNames)
        {
            // Load multiple resources
            foreach(string name in resourceNames)
            {
                Debug.Log($"Loading resource: {name}");
            }
        }
    }
}