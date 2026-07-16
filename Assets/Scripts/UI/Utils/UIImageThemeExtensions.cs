using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public static class UIImageThemeExtensions
    {
        public static void ApplyThemeToImage(this Image image, UIDesignTheme theme)
        {
            if (image == null || theme == null) return;
            
            // Apply theme colors to image
            // This is a placeholder implementation
        }
        
        public static void ApplyThemeToSprite(this Sprite sprite, UIDesignTheme theme)
        {
            if (sprite == null || theme == null) return;
            
            // Apply theme colors to sprite
            // This is a placeholder implementation
        }
    }
}