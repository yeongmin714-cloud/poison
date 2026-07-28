using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class ThemeManager : MonoBehaviour
    {
        [SerializeField] private Material[] themes;
        private int currentThemeIndex = 0;

        public void Initialize()
        {
            if(themes.Length > 0)
                currentThemeIndex = 0;
        }

        public void ApplyTheme(int themeIndex)
        {
            if(themeIndex >= 0 && themeIndex < themes.Length)
            {
                currentThemeIndex = themeIndex;
                // Logic to apply theme
            }
        }

        public Material GetCurrentTheme()
        {
            if(themes.Length > 0)
                return themes[currentThemeIndex];
            return null;
        }
    }
}