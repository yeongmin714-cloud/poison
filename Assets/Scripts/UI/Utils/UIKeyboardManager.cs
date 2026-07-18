using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIKeyboardManager : MonoBehaviour
    {
        [Header("Keyboard Settings")]
        public KeyCode[] keyCodes = {KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D};

        public void HandleKeyPress(KeyCode keyCode)
        {
            // Handle keyboard input
            Debug.Log($"Key pressed: {keyCode}");
        }

        public void BindKey(KeyCode keyCode, string action)
        {
            // Bind key to action
            Debug.Log($"Binding {keyCode} to {action}");
        }

        public bool IsKeyPressed(KeyCode keyCode)
        {
            // Check if key is currently pressed
            return Input.GetKey(keyCode);
        }
    }
}