using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Test_07_GasBomb 씬 생성: 가스 분사기 + 폭탄 시스템 테스트.
/// Tools > Create Test_07_GasBomb Scene 실행.
/// </summary>
public static class CreateTest07GasBombScene
{
    private const string SCENE_PATH = "Assets/Scenes/TestScenes/Test_07_GasBomb.unity";

    [MenuItem("Tools/Create Test_07_GasBomb Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // _TestGasBombSetup GameObject 생성
        var setupGO = new GameObject("_TestGasBombSetup");
        setupGO.AddComponent<TestGasBombSetup>();

        // 씬 저장
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        Debug.Log($"[CreateTest07GasBombScene] ✅ 씬 생성 완료: {SCENE_PATH}");
    }
}