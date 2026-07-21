using UnityEngine;
using System.Collections.Generic;

public class ThemeDatabase : ScriptableObject
{
    public List<Theme> themes;
    
    public Theme GetTheme(string themeName)
    {
        return themes.Find(t => t.themeName == themeName);
    }
}