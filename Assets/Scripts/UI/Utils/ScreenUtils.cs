using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Utils
{
    public static class ScreenUtils
    {
        public static Vector2Int GetScreenSize()
        {
            return new Vector2Int(Screen.width, Screen.height);
        }
    }
}