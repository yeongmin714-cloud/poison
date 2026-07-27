using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Theme
{
    public class Theme : ScriptableObject
    {
        [SerializeField] private Color primaryColor;
        [SerializeField] private Color secondaryColor;
        [SerializeField] private string themeName;

        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public string ThemeName => themeName;
    }
}