using UnityEditor;
using UnityEngine;

public static class SettingsFix
{
    [MenuItem("Tools/Fix ActiveInputHandler")]
    public static void Fix()
    {
        // Fix 1: activeInputHandler → 2 (Both)
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var playerSettings = typeof(PlayerSettings);
        var method = playerSettings.GetMethod("SetPropertyInt", flags);
        if (method != null)
            method.Invoke(null, new object[] { "activeInputHandler", 2, true });
        
        Debug.Log("[SettingsFix] activeInputHandler set to 2 (Both)");
        
        // Verify
        var getMethod = playerSettings.GetMethod("GetPropertyInt", flags);
        if (getMethod != null)
        {
            int value = (int)getMethod.Invoke(null, new object[] { "activeInputHandler", 2, true });
            Debug.Log($"[SettingsFix] Verified activeInputHandler = {value}");
        }

        // Fix 2: Graphics quality → Very High (4)
        var qs = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.GraphicsSettings>("ProjectSettings/QualitySettings.asset");
        // Use simple approach - set via PlayerSettings
        var setQuality = playerSettings.GetMethod("SetPropertyInt", flags);
        if (setQuality != null)
        {
            setQuality.Invoke(null, new object[] { "m_CurrentQuality", 4, true });
            Debug.Log("[SettingsFix] Quality set to Very High (4)");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SettingsFix] Complete!");
    }
}