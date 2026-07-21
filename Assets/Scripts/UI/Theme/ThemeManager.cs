using UnityEngine;
using System.Collections.Generic;

public class ThemeManager : MonoBehaviour
{
    private static ThemeManager instance;
    public static ThemeManager Instance => instance;
    
    [SerializeField] private ColorPalette currentPalette;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void ApplyTheme(ColorPalette palette)
    {
        currentPalette = palette;
        // Apply theme to UI elements
    }
    
    public ColorPalette GetCurrentPalette()
    {
        return currentPalette;
    }
}