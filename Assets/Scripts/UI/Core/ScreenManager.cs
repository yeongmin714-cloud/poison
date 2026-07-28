using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class ScreenManager : MonoBehaviour
    {
        public Vector2Int GetScreenSize()
        {
            return new Vector2Int(Screen.width, Screen.height);
        }

        public void SetFullscreen(bool fullscreen)
        {
            Screen.fullScreen = fullscreen;
        }

        public bool IsFullscreen()
        {
            return Screen.fullScreen;
        }
    }
}