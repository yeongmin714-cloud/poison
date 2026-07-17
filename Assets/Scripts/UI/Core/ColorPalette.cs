using UnityEngine;

namespace ProjectName.UI.Core
{
    [CreateAssetMenu(fileName = "ColorPalette", menuName = "UI/ColorPalette")]
    public class ColorPalette : ScriptableObject
    {
        public Color primaryColor;
        public Color secondaryColor;
        public Color accentColor;
        public Color backgroundColor;
        public Color textColor;
    }
}