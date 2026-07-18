using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UITextFieldUtils : MonoBehaviour
    {
        [Header("Text Field Settings")]
        public InputField textField;
        public Text placeholderText;

        public void SetText(string text)
        {
            // Set text of input field
            if (textField != null)
            {
                textField.text = text;
            }
        }

        public string GetText()
        {
            // Get text from input field
            if (textField != null)
            {
                return textField.text;
            }
            return "";
        }

        public void ClearText()
        {
            // Clear text field
            if (textField != null)
            {
                textField.text = "";
            }
        }

        public void SetPlaceholder(string placeholder)
        {
            // Set placeholder text
            if (placeholderText != null)
            {
                placeholderText.text = placeholder;
            }
        }
    }
}