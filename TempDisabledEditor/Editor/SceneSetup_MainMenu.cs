using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// C17-01: MainMenu.unity 씬을 생성하는 Editor 스크립트.
/// 배치모드(batchmode)에서도 실행 가능하도록 MenuItem으로 제공됩니다.
/// EditorSceneManager.NewScene + EditorSceneManager.SaveScene 사용.
/// </summary>
public static class SceneSetup_MainMenu
{
    private const string SCENE_PATH = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/Create MainMenu Scene")]
    public static void CreateMainMenuScene()
    {
        // 새 씬 생성 (DefaultGameObjects 포함)
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ===== 카메라 설정 =====
        var camera = GameObject.Find("Main Camera");
        if (camera != null)
        {
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -10);
            var camComp = camera.GetComponent<Camera>();
            camComp.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            camComp.clearFlags = CameraClearFlags.SolidColor;
            camComp.orthographic = true;
            camComp.orthographicSize = 5f;
        }

        // ===== 조명 설정 =====
        var light = GameObject.Find("Directional Light");
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
        }
        else
        {
            // 조명이 없으면 생성
            var newLight = new GameObject("Directional Light");
            var lightComp = newLight.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            lightComp.intensity = 0.5f;
            newLight.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        // ===== MainMenuUI 게임 오브젝트 추가 (ProjectName.Systems) =====
        var uiGO = new GameObject("MainMenuUI");
        uiGO.AddComponent<ProjectName.Systems.MainMenuUI>();

        // ===== EventSystem 추가 (UI 입력 처리) =====
        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // ===== 씬 저장 =====
        EditorSceneManager.SaveScene(scene, SCENE_PATH);

        // Build Settings에 Scene 0 (index 0)으로 추가
        AddSceneToBuildSettings(SCENE_PATH, 0);

        Debug.Log($"[SceneSetup_MainMenu] MainMenu.unity 씬 생성 완료: {SCENE_PATH}");
    }

    [MenuItem("Tools/Create MainMenu Scene", true)]
    private static bool ValidateCreateMainMenuScene()
    {
        return !AssetDatabase.LoadAssetAtPath<SceneAsset>(SCENE_PATH);
    }

    /// <summary>
    /// 씬을 Build Settings에 추가합니다. 이미 있으면 인덱스 재정렬하지 않고 건너뜁니다.
    /// </summary>
    private static void AddSceneToBuildSettings(string scenePath, int targetIndex)
    {
        var buildScenes = EditorBuildSettings.scenes;

        // 이미 추가되어 있는지 확인
        foreach (var buildScene in buildScenes)
        {
            if (buildScene.path == scenePath)
            {
                Debug.Log($"[SceneSetup_MainMenu] 씬이 이미 Build Settings에 있습니다: {scenePath}");
                return;
            }
        }

        // 새 배열 생성 (targetIndex에 추가)
        var newScenes = new EditorBuildSettingsScene[buildScenes.Length + 1];
        int insertIndex = Mathf.Clamp(targetIndex, 0, buildScenes.Length);

        // targetIndex 이전 요소 복사
        for (int i = 0; i < insertIndex && i < buildScenes.Length; i++)
        {
            newScenes[i] = buildScenes[i];
        }

        // targetIndex에 새 씬 삽입
        newScenes[insertIndex] = new EditorBuildSettingsScene(scenePath, true);

        // 나머지 요소 복사
        for (int i = insertIndex; i < buildScenes.Length; i++)
        {
            newScenes[i + 1] = buildScenes[i];
        }

        EditorBuildSettings.scenes = newScenes;
        Debug.Log($"[SceneSetup_MainMenu] Build Settings에 씬 추가 (index {insertIndex}): {scenePath}");
    }
}