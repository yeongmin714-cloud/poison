using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Utils
{
    public static class WindowUtils
    {
        public static void CenterWindowOnScreen(RectTransform window)
        {
            if (window == null) return;
            
            window.anchorMin = new Vector2(0.5f, 0.5f);
            window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            window.anchoredPosition = Vector2.zero;
        }
        
        public static void SetWindowSize(RectTransform window, Vector2 size)
        {
            if (window == null) return;
            
            window.sizeDelta = size;
        }
        
        public static void SetWindowPosition(RectTransform window, Vector2 position)
        {
            if (window == null) return;
            
            window.anchoredPosition = position;
        }
        
        public static Vector2 GetWindowSize(RectTransform window)
        {
            if (window == null) return Vector2.zero;
            
            return window.sizeDelta;
        }
    }
}