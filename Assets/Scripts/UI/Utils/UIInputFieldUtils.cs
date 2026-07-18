using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIInputFieldUtils : MonoBehaviour
    {
        [Header("Input Field Settings")]
        public InputField inputField;

        public void SetText(string text)
        {
            // Set text in input field
            if (inputField != null)
            {
                inputField.text = text;
            }
        }

        public string GetText()
        {
            // Get text from input field
            if (inputField != null)
            {
                return inputField.text;
            }
            return "";
        }

        public void ClearText()
        {
            // Clear text from input field
            if (inputField != null)
            {
                inputField.text = "";
            }
        }

        public void SetPlaceholder(string placeholder)
        {
            // Set placeholder text
            if (inputField != null)
            {
                inputField.placeholder.GetComponent<Text>().text = placeholder;
            }
        }
    }
}