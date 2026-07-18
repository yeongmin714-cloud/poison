using UnityEngine;

public class UIUtils : MonoBehaviour
{
    public static Vector2 GetScreenSize()
    {
        // Get screen size
        return new Vector2(Screen.width, Screen.height);
    }
    
    public static void LogDebug(string message)
    {
        // Debug logging utility
        Debug.Log($"UI Util: {message}");
    }
}