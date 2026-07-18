using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIInputUtils : MonoBehaviour
    {
        [Header("Input Settings")]
        public bool enableInput = true;

        public void ProcessInput(string input)
        {
            // Process input string
            Debug.Log($"Processing input: {input}");
        }

        public void EnableInput(bool enable)
        {
            enableInput = enable;
            Debug.Log($"Input enabled: {enable}");
        }

        public bool IsInputValid(string input)
        {
            // Check if input is valid
            return !string.IsNullOrEmpty(input);
        }
    }
}