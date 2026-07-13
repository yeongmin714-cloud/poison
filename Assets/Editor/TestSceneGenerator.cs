using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 메인씬을 복제 → 불필요 시스템 제거 방식으로 테스트 씬 생성.
/// 실제 PlayerMovement, TopDownCamera, Placeholder 등이 모두 포함됩니다.
/// </summary>
public class TestSceneGenerator : EditorWindow
{
    private string _scenePath = "Assets/Scenes/TestScenes";
    private static readonly string MainScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("Tools/Test Scenes/📋 Open Generator Window")]
    private static void ShowWindow()
    {
        GetWindow<TestSceneGenerator>("테스트 씬 생성기");
    }

    [MenuItem("Tools/Test Scenes/🚀 모든 테스트 씬 생성")]
    private static void GenerateAllFromMenu()
    {
        if (EditorUtility.DisplayDialog("확인", "메인씬을 복제하여 9개 테스트 씬을 생성합니다.\n메인씬은 변경되지 않습니다.", "생성", "취소"))
        {
            GenerateAllInternal();
            EditorUtility.DisplayDialog("완료", "✅ 9개 테스트 씬 생성 완료!\nAssets/Scenes/TestScenes/ 폴더를 확인하세요.", "확인");
        }
    }

    /// <summary>
    /// 배치모드 진입점
    /// </summary>
    public static void GenerateAllBatch()
    {
        GenerateAllInternal();
    }

    private static void GenerateAllInternal()
    {
        var gen = new TestSceneGenerator();
        gen.GeneratePlayerScene();
        gen.GenerateUIScene();
        gen.GenerateCombatScene();
        gen.GenerateTerritoryScene();
        gen.GenerateCraftScene();
        gen.GenerateTimeWeatherScene();
        gen.GenerateGasBombScene();
        gen.GenerateDraculaScene();
        gen.GenerateAllInOneSimpleScene();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>✅ 모든 테스트 씬 (9개) 생성 완료!</color>");
    }

    private void OnGUI()
    {
        GUILayout.Label("🧪 테스트 씬 생성기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("메인씬을 복제하여 시스템별로 분할한 테스트 씬을 생성합니다.\n"
            + "PlayerMovement, TopDownCamera, Placeholder 등 실제 컴포넌트가 모두 포함됩니다.",
            MessageType.Info);

        _scenePath = EditorGUILayout.TextField("생성 경로", _scenePath);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("🚀 모든 테스트 씬 생성 (9개)", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("확인", "메인씬을 복제하여 9개 테스트 씬을 생성합니다.", "생성", "취소"))
            {
                GenerateAllInternal();
                EditorUtility.DisplayDialog("완료", "✅ 9개 테스트 씬 생성 완료!", "확인");
            }
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("🏃 Test_01_Player (이동+카메라+지형)")) GeneratePlayerScene();
        if (GUILayout.Button("🖥️ Test_02_UI (모든 UI 창)")) GenerateUIScene();
        if (GUILayout.Button("⚔️ Test_03_Combat (전투+몬스터)")) GenerateCombatScene();
        if (GUILayout.Button("🏰 Test_04_Territory (영지+병사+건물)")) GenerateTerritoryScene();
        if (GUILayout.Button("🧪 Test_05_Craft (크래프트+인벤토리)")) GenerateCraftScene();
        if (GUILayout.Button("🌙 Test_06_TimeWeather (시간+날씨)")) GenerateTimeWeatherScene();
        if (GUILayout.Button("💨 Test_07_GasBomb (가스분사기+폭탄)")) GenerateGasBombScene();
        if (GUILayout.Button("🧛 Test_08_Dracula (드라큘라+야간)")) GenerateDraculaScene();
        if (GUILayout.Button("🛡️ Test_09_AllInOne (모든 시스템 간소화)")) GenerateAllInOneSimpleScene();
    }

    /// <summary>
    /// 메인씬을 열고, 특정 루트 오브젝트만 남기고 나머지 제거 후 저장.
    /// </summary>
    private void CloneAndStrip(string sceneName, string[] keepRoots, string[] removeRoots, string[] additionalRemove)
    {
        if (!Directory.Exists(_scenePath))
            Directory.CreateDirectory(_scenePath);

        string path = Path.Combine(_scenePath, sceneName + ".unity");

        // Open main scene
        var mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        var rootObjects = mainScene.GetRootGameObjects();

        // Determine which roots to keep
        var keepSet = new HashSet<string>(keepRoots ?? new string[0]);
        var removeSet = new HashSet<string>(removeRoots ?? new string[0]);

        // Strip: destroy root objects that are NOT in the keep list
        foreach (var root in rootObjects)
        {
            bool shouldRemove = removeSet.Contains(root.name);

            if (keepSet.Count > 0 && !removeSet.Contains(root.name))
            {
                // If we have a keep list, only keep those
                shouldRemove = !keepSet.Contains(root.name);
            }

            if (shouldRemove)
            {
                DestroyImmediate(root);
            }
        }

        // Add TestSceneConfig
        var configGO = new GameObject("_TestSceneConfig");
        var config = configGO.AddComponent<TestSceneConfig>();
        config.testFocus = sceneName;
        config.enabledSystems = keepRoots ?? new string[] { "All" };

        // Remove any additional system objects (non-root)
        if (additionalRemove != null)
        {
            foreach (var typeName in additionalRemove)
            {
                // Find and destroy objects with specific component types
                System.Type type = System.Type.GetType(typeName);
                if (type != null)
                {
                    var components = FindAnyObjectByType(type) as Component;
                    if (components != null)
                    {
                        DestroyImmediate(components.gameObject);
                    }
                }
            }
        }

        EditorSceneManager.SaveScene(mainScene, path);
        Debug.Log($"✅ 생성: {path}");
    }

    private void GeneratePlayerScene()
    {
        CloneAndStrip("Test_01_Player",
            keepRoots: new[] { "Player", "Ground", "Ground_Inner", "Ground_Mid", "Ground_Outer", "Directional Light", "Camera", "Cinemachine Brain" },
            removeRoots: null,
            additionalRemove: null
        );
    }

    private void GenerateUIScene()
    {
        CloneAndStrip("Test_02_UI",
            keepRoots: new[] { "Player", "Ground", "Directional Light", "Camera", "Cinemachine Brain", "UIManager", "EventSystem", "HUD", "MinimapUI", "Canvas" },
            removeRoots: null,
            additionalRemove: null
        );
    }

    private void GenerateCombatScene()
    {
        CloneAndStrip("Test_03_Combat",
            keepRoots: new[] { "Player", "Ground", "Ground_Inner", "Ground_Mid", "Ground_Outer", "Directional Light", "Camera", "Cinemachine Brain", "MonsterSpawner" },
            removeRoots: null,
            additionalRemove: null
        );
    }

    private void GenerateTerritoryScene()
    {
        CloneAndStrip("Test_04_Territory",
            keepRoots: new[] { "Player", "Ground", "Ground_Inner", "Ground_Mid", "Ground_Outer", "Directional Light", "Camera", "Cinemachine Brain", "TerritoryManager", "GuardManager" },
            removeRoots: null,
            additionalRemove: null
        );
    }

    private void GenerateCraftScene()
    {
        CloneAndStrip("Test_05_Craft",
            keepRoots: new[] { "Player", "Ground", "Ground_Inner", "Directional Light", "Camera", "Cinemachine Brain", "CraftingStation", "CraftingTable" },
            removeRoots: null,
            additionalRemove: null
        );
    }

    private void GenerateTimeWeatherScene()
    {
        CloneAndStrip("Test_06_TimeWeather",
            keepRoots: new[] { "Player", "Ground", "Ground_Inner", "Ground_Mid", "Ground_Outer", "Directional Light", "Camera", "Cinemachine Brain", "TimeManager", "WeatherManager" },
            removeRoots: null,
            additionalRemove: null
        );
    }

    private void GenerateGasBombScene()
    {
        CloneAndStrip("Test_07_GasBomb",
            keepRoots: new[] { "Player", "Ground", "Ground_Inner", "Directional Light", "Camera", "Cinemachine Brain" },
            removeRoots: null,
            additionalRemove: null
        );
    }

    private void GenerateDraculaScene()
    {
        CloneAndStrip("Test_08_Dracula",
            keepRoots: null,
            removeRoots: new[] { "MonsterSpawner", "MercenaryManager", "ArenaSystem" },
            additionalRemove: null
        );
    }

    private void GenerateAllInOneSimpleScene()
    {
        CloneAndStrip("Test_09_AllInOne",
            keepRoots: null,
            removeRoots: new[] { "MercenaryManager", "ArenaSystem", "BardMercenary" },
            additionalRemove: null
        );
    }
}