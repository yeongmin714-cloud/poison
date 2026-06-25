using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

public static class InstallInputSystem
{
    [MenuItem("Tools/Install InputSystem")]
    public static void Install()
    {
        var request = Client.Add("com.unity.inputsystem");
        EditorApplication.update += () =>
        {
            if (request.IsCompleted)
            {
                if (request.Status == StatusCode.Success)
                {
                    UnityEngine.Debug.Log("InputSystem installed successfully");
                }
                else
                {
                    UnityEngine.Debug.LogError("Failed to install InputSystem: " + request.Error?.message);
                }
                EditorApplication.Exit(0);
            }
        };
    }
}