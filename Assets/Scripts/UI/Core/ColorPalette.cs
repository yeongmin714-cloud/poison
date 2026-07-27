using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorPalette : MonoBehaviour
{
    public Color primaryColor;
    public Color secondaryColor;
    public Color accentColor;
    
    public Color GetColor(string colorName)
    {
        // Implementation would go here
        return Color.white;
    }
    
    public void SetColor(string colorName, Color color)
    {
        // Implementation would go here
    }
    
    /// <summary>
    /// Gets all colors in palette
    /// </summary>
    public Dictionary<string, Color> GetAllColors()
    {
        // Implementation would go here
        return new Dictionary<string, Color>();
    }
}