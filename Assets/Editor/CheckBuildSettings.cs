using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;

public static class CheckBuildSettings
{
    [MenuItem("Tools/Check Build Settings")]
    public static void Run()
    {
        var scenes = EditorBuildSettings.scenes;
        Debug.Log($"=== Build Settings Scenes ({scenes.Length}) ===");
        foreach (var s in scenes)
        {
            Debug.Log($"  {(s.enabled ? "✅" : "❌")} {s.path}");
        }
        
        // Check current active scene
        var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        Debug.Log($"\nActive Scene: {activeScene.path}");
        
        // Check script references
        Debug.Log("\n=== Checking for key scripts ===");
        string[] keyScripts = {
            "TopDownCameraController",
            "PlayerMovement", 
            "PlayerHealth",
            "GameManager",
            "UIManager",
            "GameSetup",
            "SoundManager"
        };
        foreach (var name in keyScripts)
        {
            var objs = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            bool found = false;
            foreach (var obj in objs)
            {
                if (obj != null && obj.GetType().Name == name)
                {
                    found = true;
                    Debug.Log($"  ✅ {name} → {obj.gameObject.name}");
                    break;
                }
            }
            if (!found)
            {
                var type = System.Type.GetType(name);
                if (type == null)
                {
                    type = System.AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.Name == name);
                }
                Debug.Log($"  {(type != null ? "⚠️ EXISTS but not in scene" : "❌ NOT FOUND")}: {name}");
            }
        }
    }
}