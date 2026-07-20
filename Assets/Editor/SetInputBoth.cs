using UnityEditor;
using UnityEngine;

public static class SetInputBoth
{
    [UnityEditor.MenuItem("Tools/Set Input Both")]
    public static void SetActiveInputHandler()
    {
        var serializedObject = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var prop = serializedObject.FindProperty("activeInputHandler");
        if (prop != null)
        {
            prop.intValue = 2; // Both
            serializedObject.ApplyModifiedProperties();
            Debug.Log("Set activeInputHandler to 2 (Both)");
        }
        else
        {
            Debug.LogError("Could not find activeInputHandler property");
        }
    }
}