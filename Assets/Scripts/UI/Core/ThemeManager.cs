using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class ThemeManager : MonoBehaviour
    {
        [SerializeField] private Theme activeTheme;
        
        public Theme ActiveTheme => activeTheme;
        
        public void SetTheme(Theme theme)
        {
            activeTheme = theme;
        }
    }
}