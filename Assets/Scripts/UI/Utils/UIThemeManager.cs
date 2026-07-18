using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIThemeManager : MonoBehaviour
    {
        [Header("UI References")]
        public Canvas canvas;
        public Graphic[] graphics;
        
        [Header("Theme Data")]
        public string currentTheme = "Light";

        public void ApplyTheme(string themeName)
        {
            currentTheme = themeName;
            // Apply theme to all graphics
            Debug.Log($"Applied theme: {themeName}");
        }

        public void SetThemeColor(Color color)
        {
            // Set theme color for all graphics
            foreach(Graphic graphic in graphics)
            {
                graphic.color = color;
            }
        }
    }
}