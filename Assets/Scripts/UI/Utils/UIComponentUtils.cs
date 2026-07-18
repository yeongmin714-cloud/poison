using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIComponentUtils : MonoBehaviour
    {
        [Header("Component Settings")]
        public bool autoInitialize = true;

        public T GetComponent<T>(GameObject target) where T : Component
        {
            // Get component from target object
            T component = target.GetComponent<T>();
            if (component == null)
            {
                Debug.LogError($"Component of type {typeof(T)} not found on {target?.name ?? "null"}");
            }
            return component;
        }

        public void SetActive(GameObject target, bool active)
        {
            // Set active state of target object
            if (target != null)
                target.SetActive(active);
        }

        public void DestroyObject(GameObject target)
        {
            // Destroy target object
            if (target != null)
                Destroy(target);
        }
    }
}