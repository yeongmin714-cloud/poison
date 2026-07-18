using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIValidationUtils : MonoBehaviour
    {
        [Header("Validation Settings")]
        public bool enableValidation = true;

        public bool ValidateString(string input)
        {
            // Validate string input
            return !string.IsNullOrEmpty(input);
        }

        public bool ValidateNumber(float input)
        {
            // Validate number input
            return input >= 0;
        }

        public bool ValidateEmail(string email)
        {
            // Validate email address
            return email.Contains("@") && email.Contains(".");
        }

        public void ShowValidationError(string field, string message)
        {
            // Show validation error
            Debug.Log($"Validation error in {field}: {message}");
        }
    }
}