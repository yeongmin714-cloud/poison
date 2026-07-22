using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UICustomizationManager : MonoBehaviour
    {
        [Header("UI References")]
        public Image characterPreview;
        public Text customizationTitle;
        public Button saveButton;
        public Button cancelButton;
        
        [Header("Customization Data")]
        public string customizationType = "Character";
        public string currentColor = "Blue";
        public string currentShape = "Round";

        private void Start()
        {
            InitializeCustomization();
        }

        public void InitializeCustomization()
        {
            customizationTitle.text = $"{customizationType} Customization";
            // Initialize character preview based on current settings
        }

        public void ChangeColor(string color)
        {
            currentColor = color;
            // Update character preview
        }

        public void ChangeShape(string shape)
        {
            currentShape = shape;
            // Update character preview
        }

        public void SaveCustomization()
        {
            // Save customization settings
            // Debug.Log("Customization saved");
        }

        public void CancelCustomization()
        {
            // Revert to previous settings
            // Debug.Log("Customization cancelled");
        }
    }
}