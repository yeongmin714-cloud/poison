using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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
        
        private Dictionary<string, Color> availableColors = new Dictionary<string, Color>();
        private List<string> availableShapes = new List<string>();
        
        private void Start()
        {
            InitializeCustomization();
        }
        
        public void InitializeCustomization()
        {
            // Initialize available colors and shapes
            InitializeColorPalette();
            InitializeAvailableShapes();
            
            customizationTitle.text = $"{customizationType} Customization";
            // Initialize character preview based on current settings
            Debug.Log("Customization manager initialized");
        }
        
        private void InitializeColorPalette()
        {
            availableColors["Blue"] = Color.blue;
            availableColors["Red"] = Color.red;
            availableColors["Green"] = Color.green;
            availableColors["Yellow"] = Color.yellow;
            availableColors["Purple"] = Color.magenta;
            availableColors["Orange"] = new Color(1, 0.65f, 0);
        }
        
        private void InitializeAvailableShapes()
        {
            availableShapes.Add("Round");
            availableShapes.Add("Square");
            availableShapes.Add("Triangle");
        }
        
        public void ChangeColor(string color)
        {
            if (availableColors.ContainsKey(color))
            {
                currentColor = color;
                // Update character preview
                characterPreview.color = availableColors[color];
                Debug.Log("Color changed to: " + color);
            }
            else
            {
                Debug.LogWarning("Invalid color: " + color);
            }
        }
        
        public void ChangeShape(string shape)
        {
            if (availableShapes.Contains(shape))
            {
                currentShape = shape;
                // Update character preview
                Debug.Log("Shape changed to: " + shape);
            }
            else
            {
                Debug.LogWarning("Invalid shape: " + shape);
            }
        }
        
        public void SaveCustomization()
        {
            // Save customization settings
            Debug.Log("Customization saved");
        }
        
        public void CancelCustomization()
        {
            // Revert to previous settings
            Debug.Log("Customization cancelled");
        }
    }
}