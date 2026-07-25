using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThemeManager : MonoBehaviour
{
    public static ThemeManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ApplyTheme(string themeName)
    {
        // Implementation would go here
    }
    
    public void LoadTheme(string themePath)
    {
        // Implementation would go here
    }
}