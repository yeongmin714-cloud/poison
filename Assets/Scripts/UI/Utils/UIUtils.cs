using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIUtils : MonoBehaviour
    {
        [Header("Utility Settings")]
        public bool debugMode = false;

        public void LogDebug(string message)
        {
            if (debugMode)
            {
                Debug.Log(message);
            }
        }

        public void ShowMessage(string message)
        {
            // Display a simple message
            Debug.Log(message);
        }

        public void ValidateUIElement(UIElement element)
        {
            // Validate UI element
            Debug.Log($"Validating element: {element.name}");
        }
    }
}