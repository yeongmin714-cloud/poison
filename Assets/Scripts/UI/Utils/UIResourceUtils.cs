using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIResourceUtils : MonoBehaviour
    {
        [Header("Resource Settings")]
        public string resourceFolder = "Resources/";

        public T LoadResource<T>(string resourceName) where T : Object
        {
            // Load resource from Resources folder
            T resource = Resources.Load<T>(resourceFolder + resourceName);
            if (resource == null)
            {
                Debug.LogError($"Resource not found: {resourceName}");
            }
            return resource;
        }

        public void UnloadResource(string resourceName)
        {
            // Unload specific resource
            Debug.Log($"Unloading resource: {resourceName}");
        }

        public void UnloadAllResources()
        {
            // Unload all loaded resources
            Resources.UnloadUnusedAssets();
        }
    }
}