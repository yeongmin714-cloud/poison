using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Removes all missing script references (broken PPtr) from the MainScene.
/// Run via: Tools > Fix Scene > Remove Broken References
/// Or in batchmode: -executeMethod FixBrokenReferences.CleanMainScene
/// </summary>
public static class FixBrokenReferences
{
    [MenuItem("Tools/Fix Scene/Remove Broken References")]
    public static void CleanMainScene()
    {
        string scenePath = "Assets/Scenes/MainScene.unity";
        EditorSceneManager.OpenScene(scenePath);

        int removed = 0;
        int scanned = 0;

        // Get all GameObjects in the scene including inactive
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        HashSet<GameObject> processed = new HashSet<GameObject>();

        foreach (var go in allObjects)
        {
            if (processed.Contains(go)) continue;
            processed.Add(go);

            scanned++;

            // Remove missing MonoBehaviours (broken PPtr to scripts)
            var components = go.GetComponents<Component>();
            int before = go.GetComponents<Component>().Length;
            int removedHere = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removedHere > 0)
            {
                removed += removedHere;
                Debug.Log($"[Fix] Removed {removedHere} missing script(s) from: {GetPath(go)}");
            }
        }

        Debug.Log($"[Fix] ✅ Scene scan complete. Scanned {scanned} GameObjects, removed {removed} missing references.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[Fix] ✅ Scene saved. Total missing references removed: {removed}");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    private static string GetPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}