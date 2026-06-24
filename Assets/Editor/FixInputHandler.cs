using UnityEditor;
using UnityEngine;

public static class FixInputHandler
{
    [MenuItem("Tools/Fix ActiveInputHandler")]
    public static void Fix()
    {
        // Force-set activeInputHandler to 2 (Both) via PlayerSettings internal API
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var ps = typeof(PlayerSettings);

        var setMethod = ps.GetMethod("SetPropertyInt", flags);
        if (setMethod != null)
        {
            setMethod.Invoke(null, new object[] { "activeInputHandler", 2, true });
            Debug.Log("[Fix] activeInputHandler set to 2 (Both)");
        }

        var getMethod = ps.GetMethod("GetPropertyInt", flags);
        if (getMethod != null)
        {
            int val = (int)getMethod.Invoke(null, new object[] { "activeInputHandler", 2, true });
            Debug.Log($"[Fix] Verified: activeInputHandler = {val}");
        }

        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }
}