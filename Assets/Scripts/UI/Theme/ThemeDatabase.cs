using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Theme
{
    public class ThemeDatabase : MonoBehaviour
    {
        [SerializeField] private List<Theme> themes;
        private Dictionary<string, Theme> themeDictionary;
        
        public void Initialize()
        {
            themeDictionary = new Dictionary<string, Theme>();
            foreach(var theme in themes)
            {
                if(theme != null && !themeDictionary.ContainsKey(theme.ThemeName))
                {
                    themeDictionary.Add(theme.ThemeName, theme);
                }
            }
        }

        public Theme GetTheme(string themeName)
        {
            if(themeDictionary != null && themeDictionary.ContainsKey(themeName))
                return themeDictionary[themeName];
            
            // Fallback to original approach if needed
            foreach (var theme in themes)
            {
                if (theme != null && theme.name == themeName)
                    return theme;
            }
            
            return null;
        }
    }
}