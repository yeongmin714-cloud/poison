using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class SceneSetup
{
    [MenuItem("Tools/Create MainScene")]
    public static void CreateMainScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Rename objects
        var camera = GameObject.Find("Main Camera");
        if (camera != null)
        {
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 2, -5);
            camera.transform.rotation = Quaternion.Euler(15, 0, 0);
            var camComp = camera.GetComponent<Camera>();
            camComp.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            camComp.clearFlags = CameraClearFlags.SolidColor;
        }

        var light = GameObject.Find("Directional Light");
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var lightComp = light.GetComponent<Light>();
            lightComp.shadowStrength = 0.8f;
        }

        // Add URP volume
        var volumeGO = new GameObject("Global Volume");
        var volume = volumeGO.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volumeGO.transform.position = Vector3.zero;

        // Add GameManager
        var gmGO = new GameObject("GameManager");
        gmGO.AddComponent<ProjectName.Core.GameManager>();

        var path = "Assets/Scenes/MainScene.unity";
        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.OpenScene(path);

        Debug.Log($"[SceneSetup] MainScene created at {path}");
    }

    [MenuItem("Tools/Create MainScene", true)]
    private static bool ValidateCreateMainScene()
    {
        return !AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainScene.unity");
    }
}