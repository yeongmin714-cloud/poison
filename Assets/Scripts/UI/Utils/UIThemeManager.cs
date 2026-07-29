using UnityEngine;

public class UIThemeManager : MonoBehaviour
{
    private void Awake()
    {
        // Initialize theme manager
    }
    
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
        if (graphics != null)
        {
            foreach(Graphic graphic in graphics)
            {
                if (graphic != null)
                    graphic.color = color;
            }
        }
    }
}