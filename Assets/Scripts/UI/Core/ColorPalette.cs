using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class ColorPalette : ScriptableObject
    {
        [SerializeField] private Color primaryColor;
        [SerializeField] private Color secondaryColor;

        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
    }
}