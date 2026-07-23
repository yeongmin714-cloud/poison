using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

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
    
    public void ApplyTheme(string themeName)
    {
        // Apply theme by name
    }
}