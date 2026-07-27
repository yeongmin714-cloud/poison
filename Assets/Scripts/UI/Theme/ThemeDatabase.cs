using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Theme
{
    public class ThemeDatabase : MonoBehaviour
    {
        [SerializeField] private List<Theme> themes;

        public Theme GetTheme(string themeName)
        {
            foreach (var theme in themes)
            {
                if (theme.name == themeName)
                    return theme;
            }
            
            return null;
        }
    }
}