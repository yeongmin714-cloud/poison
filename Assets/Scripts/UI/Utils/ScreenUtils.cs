using UnityEngine;
using System.Collections.Generic;

public class ScreenUtils : MonoBehaviour
{
    public static Vector2 GetScreenSize()
    {
        return new Vector2(Screen.width, Screen.height);
    }
    
    public static Vector2 GetScreenResolution()
    {
        return new Vector2(Screen.currentResolution.width, Screen.currentResolution.height);
    }
}