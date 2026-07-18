using UnityEngine;

public class UIThemeManager : MonoBehaviour
{
    public Color primaryColor;
    public Color secondaryColor;
    
    private void Start()
    {
        // Initialize theme manager
        Debug.Log("UI Theme Manager initialized");
    }
    
    public void ApplyTheme(Color primary, Color secondary)
    {
        // Apply UI theme
        primaryColor = primary;
        secondaryColor = secondary;
    }
}