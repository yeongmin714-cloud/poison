using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Utils
{
    public static class ColorUtils
    {
        public static Color IntToColor(int colorInt)
        {
            return new Color(
                ((colorInt >> 16) & 0xFF) / 255f,
                ((colorInt >> 8) & 0xFF) / 255f,
                (colorInt & 0xFF) / 255f
            );
        }
    }
}