using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIContextUtils : MonoBehaviour
    {
        [Header("Context Settings")]
        public string context = "Default";

        public void SetContext(string newContext)
        {
            context = newContext;
            // Debug.Log($"Context changed to: {newContext}");
        }

        public string GetContext()
        {
            return context;
        }

        public void UpdateContext()
        {
            // Update context-specific logic
            // Debug.Log("Updating context");
        }
    }
}