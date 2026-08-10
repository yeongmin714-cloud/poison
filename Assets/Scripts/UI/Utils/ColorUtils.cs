using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Utils
{
    public static class ColorUtils
    {
        public static Color GetRandomColor()
        {
            return new Color(Random.value, Random.value, Random.value);
        }
        
        public static Color GetColorFromHex(string hexCode)
        {
            if(hexCode.StartsWith("#"))
                hexCode = hexCode.Substring(1);
                
            if(ColorUtility.TryParseHtmlString("#" + hexCode, out Color color))
            {
                return color;
            }
            
            return Color.white;
        }
        
        public static Color GetColorWithAlpha(Color baseColor, float alpha)
        {
            return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
    }
}