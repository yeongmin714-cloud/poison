using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Test_08_Dracula 씬 생성: 드라큘라 영주 + 야간 시스템 테스트.
/// Tools > Create Test_08_Dracula Scene 실행.
/// </summary>
public static class CreateTest08DraculaScene
{
    private const string SCENE_PATH = "Assets/Scenes/TestScenes/Test_08_Dracula.unity";

    [MenuItem("Tools/Create Test_08_Dracula Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Remove default Camera, Light
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.scene == scene && (go.GetComponent<Camera>() || go.GetComponent<Light>()))
                Object.DestroyImmediate(go);
        }

        // _TestDraculaSetup GameObject 생성
        var setupGO = new GameObject("_TestDraculaSetup");
        setupGO.AddComponent<ProjectName.Systems.TestDraculaSetup>();

        // 씬 저장
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        Debug.Log($"[CreateTest08DraculaScene] ✅ 씬 생성 완료: {SCENE_PATH}");
    }
}