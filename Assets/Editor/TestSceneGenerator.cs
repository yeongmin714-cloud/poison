using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using ProjectName.Systems;

/// <summary>
/// 깨끗한 빈 씬 + 최소 구성 요소만 추가하는 테스트 씬 생성기.
/// GameManager/UIManager 등 모든 시스템을 사용하지 않음.
/// </summary>
public class TestSceneGenerator : EditorWindow
{
    private string _scenePath = "Assets/Scenes/TestScenes";

    [MenuItem("Tools/Test Scenes/📋 Open Generator Window")]
    private static void ShowWindow() => GetWindow<TestSceneGenerator>("테스트 씬 생성기");

    [MenuItem("Tools/Test Scenes/🚀 모든 테스트 씬 생성")]
    private static void GenerateAll()
    {
        if (EditorUtility.DisplayDialog("확인", "깨끗한 빈 씬 9개를 생성합니다.", "생성", "취소"))
        {
            var gen = new TestSceneGenerator();
            gen.GenPlayer();
            gen.GenUI();
            gen.GenCombat();
            gen.GenTerritory();
            gen.GenCraft();
            gen.GenTime();
            gen.GenGas();
            gen.GenDracula();
            gen.GenAllInOne();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("완료", "✅ 9개 테스트 씬 생성 완료!", "확인");
        }
    }

    public static void GenerateAllBatch()
    {
        var gen = new TestSceneGenerator();
        gen.GenPlayer();
        Debug.Log("[Batch] ✅ 생성 완료");
    }

    private void OnGUI()
    {
        GUILayout.Label("🧪 테스트 씬 생성기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("깨끗한 빈 씬에 필요한 최소 구성 요소만 추가합니다.\nGameManager/UIManager 등 불필요한 시스템 없음.", MessageType.Info);
        _scenePath = EditorGUILayout.TextField("생성 경로", _scenePath);
        EditorGUILayout.Space(10);
        if (GUILayout.Button("🚀 모든 테스트 씬 생성 (9개)", GUILayout.Height(30)))
            GenerateAll();
        if (GUILayout.Button("🏃 Test_01_Player")) GenPlayer();
        if (GUILayout.Button("🖥️ Test_02_UI")) GenUI();
        if (GUILayout.Button("⚔️ Test_03_Combat")) GenCombat();
        if (GUILayout.Button("🏰 Test_04_Territory")) GenTerritory();
        if (GUILayout.Button("🧪 Test_05_Craft")) GenCraft();
        if (GUILayout.Button("🌙 Test_06_Time")) GenTime();
        if (GUILayout.Button("💨 Test_07_Gas")) GenGas();
        if (GUILayout.Button("🧛 Test_08_Dracula")) GenDracula();
        if (GUILayout.Button("🛡️ Test_09_AllInOne")) GenAllInOne();
    }

    private void SaveScene(string name)
    {
        if (!Directory.Exists(_scenePath))
            Directory.CreateDirectory(_scenePath);
        string path = Path.Combine(_scenePath, name + ".unity");
        EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single), path);
    }

    private void GenPlayer()
    {
        SaveScene("Test_01_Player");
        foreach (var go in FindObjectsOfType<GameObject>())
        {
            if (go.GetComponent<Camera>() || go.GetComponent<Light>())
                DestroyImmediate(go);
        }
        var setup = new GameObject("_TestPlayerSetup").AddComponent<TestPlayerSetup>();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    private void GenUI()
    {
        SaveScene("Test_02_UI");
        Debug.Log("TODO: UI 테스트 씬");
    }

    private void GenCombat()
    {
        SaveScene("Test_03_Combat");
        Debug.Log("TODO: Combat 테스트 씬");
    }

    private void GenTerritory()
    {
        SaveScene("Test_04_Territory");
        Debug.Log("TODO: Territory 테스트 씬");
    }

    private void GenCraft()
    {
        SaveScene("Test_05_Craft");
        Debug.Log("TODO: Craft 테스트 씬");
    }

    private void GenTime()
    {
        SaveScene("Test_06_Time");
        Debug.Log("TODO: Time 테스트 씬");
    }

    private void GenGas()
    {
        SaveScene("Test_07_Gas");
        Debug.Log("TODO: Gas 테스트 씬");
    }

    private void GenDracula()
    {
        SaveScene("Test_08_Dracula");
        Debug.Log("TODO: Dracula 테스트 씬");
    }

    private void GenAllInOne()
    {
        SaveScene("Test_09_AllInOne");
        Debug.Log("TODO: AllInOne 테스트 씬");
    }
}