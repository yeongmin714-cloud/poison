using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

/// <summary>
/// 각 시스템별 독립 테스트 씬 생성기.
/// 메인씬에서 복사하지 않고, 깨끗한 빈 씬에 필요한 시스템만 초기화합니다.
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

    [MenuItem("Tools/Test Scenes/🚀 Generate All Test Scenes")]
    private static void GenerateAllFromMenu()
    {
        if (EditorUtility.DisplayDialog("확인", "9개 테스트 씬을 생성합니다. 메인씬은 변경되지 않습니다.", "생성", "취소"))
        {
            var gen = GetWindow<TestSceneGenerator>("테스트 씬 생성기");
            gen.GenerateAllTestScenes();
            EditorUtility.DisplayDialog("완료", "✅ 9개 테스트 씬 생성 완료!\nAssets/Scenes/TestScenes/ 폴더를 확인하세요.", "확인");
        }
    }

    /// <summary>
    /// 배치모드 진입점: 모든 테스트 씬 생성
    /// </summary>
    public static void GenerateAllBatch()
    {
        var gen = new TestSceneGenerator();
        gen.GenerateAllTestScenes();
        Debug.Log("[TestSceneGenerator] ✅ 배치모드 생성 완료");
    }

    private void OnGUI()
    {
        GUILayout.Label("🧪 테스트 씬 생성기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("씬을 시스템 단위로 분할한 독립 테스트 씬을 생성합니다.\n"
            + "각 씬은 필요한 시스템만 로드하여 런타임 오류를 격리하여 테스트합니다.",
            MessageType.Info);

        _scenePath = EditorGUILayout.TextField("생성 경로", _scenePath);

        EditorGUILayout.Space(10);
        GUILayout.Label("🚀 전체 생성", EditorStyles.boldLabel);
        if (GUILayout.Button("전체 생성 (모든 테스트 씬)", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("확인", "9개 테스트 씬을 생성합니다. 메인씬은 변경되지 않습니다.", "생성", "취소"))
            {
                GenerateAllTestScenes();
            }
        }

        EditorGUILayout.Space(10);
        GUILayout.Label("📂 개별 생성:", EditorStyles.boldLabel);

        if (GUILayout.Button("🏃 Test_01_Player (이동+카메라+지형)")) GeneratePlayerScene();
        if (GUILayout.Button("🖥️ Test_02_UI (모든 UI 창)")) GenerateUIScene();
        if (GUILayout.Button("⚔️ Test_03_Combat (전투+몬스터)")) GenerateCombatScene();
        if (GUILayout.Button("🏰 Test_04_Territory (영지+병사+건물)")) GenerateTerritoryScene();
        if (GUILayout.Button("🧪 Test_05_Craft (크래프트+인벤토리)")) GenerateCraftScene();
        if (GUILayout.Button("🌙 Test_06_TimeWeather (시간+날씨)")) GenerateTimeWeatherScene();
        if (GUILayout.Button("💨 Test_07_GasBomb (가스분사기+폭탄)")) GenerateGasBombScene();
        if (GUILayout.Button("🧛 Test_08_Dracula (드라큘라+야간)")) GenerateDraculaScene();
        if (GUILayout.Button("🛡️ Test_09_AllInOne (모든 시스템+간소화)")) GenerateAllInOneSimpleScene();
    }

    private void GenerateAllTestScenes()
    {
        GeneratePlayerScene();
        GenerateUIScene();
        GenerateCombatScene();
        GenerateTerritoryScene();
        GenerateCraftScene();
        GenerateTimeWeatherScene();
        GenerateGasBombScene();
        GenerateDraculaScene();
        GenerateAllInOneSimpleScene();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>✅ 모든 테스트 씬 (9개) 생성 완료!</color>");
    }

    private void CreateScene(string sceneName, System.Action<GameObject> setupAction)
    {
        if (!Directory.Exists(_scenePath))
            Directory.CreateDirectory(_scenePath);

        string path = Path.Combine(_scenePath, sceneName + ".unity");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = sceneName;

        // Remove default camera and light (we'll add our own)
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<Camera>() || root.GetComponent<Light>())
                DestroyImmediate(root);
        }

        // Create root setup object
        var setupGo = new GameObject("_TestSceneSetup");
        setupAction?.Invoke(setupGo);

        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"✅ 생성: {path}");
    }

    private void AddTestConfig(GameObject target, string[] enabledSystems, string testFocus)
    {
        var config = target.AddComponent<TestSceneConfig>();
        config.testFocus = testFocus;
        config.enabledSystems = enabledSystems;
    }

    private void AddPlayerAndTerrain(GameObject parent)
    {
        // Ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -0.5f, 0);
        ground.transform.localScale = Vector3.one * 50;

        // Directional Light
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Player (placeholder sphere)
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0, 1, 0);
        player.transform.localScale = new Vector3(1, 2, 1);

        var cc = player.AddComponent<CharacterController>();
        cc.height = 2;
        cc.radius = 0.5f;

        // Main Camera
        var camGo = new GameObject("MainCamera");
        var cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.transform.position = new Vector3(0, 5, -5);
        cam.transform.LookAt(Vector3.zero);
    }

    private void GeneratePlayerScene()
    {
        CreateScene("Test_01_Player", root =>
        {
            AddPlayerAndTerrain(root);
            AddTestConfig(root, new[] { "Player", "Terrain", "Camera" }, "이동+카메라+지형 기본 테스트");
        });
    }

    private void GenerateUIScene()
    {
        CreateScene("Test_02_UI", root =>
        {
            AddPlayerAndTerrain(root);
            AddTestConfig(root, new[] { "UI", "UIManager", "Player" }, "모든 UI 창 열기/닫기/동작 테스트");
        });
    }

    private void GenerateCombatScene()
    {
        CreateScene("Test_03_Combat", root =>
        {
            AddPlayerAndTerrain(root);
            AddTestConfig(root, new[] { "Player", "Combat", "Monsters", "Camera" }, "근접/원거리 공격, 몬스터 AI, 드랍 테스트");
        });
    }

    private void GenerateTerritoryScene()
    {
        CreateScene("Test_04_Territory", root =>
        {
            AddPlayerAndTerrain(root);
            AddTestConfig(root, new[] { "Territory", "Guards", "Buildings", "Player", "Camera" }, "영지 병사, 건물, E키 상호작용 테스트");
        });
    }

    private void GenerateCraftScene()
    {
        CreateScene("Test_05_Craft", root =>
        {
            AddPlayerAndTerrain(root);
            AddTestConfig(root, new[] { "Craft", "Inventory", "Player", "Camera" }, "크래프트 테이블, 인벤토리, 조합 테스트");
        });
    }

    private void GenerateTimeWeatherScene()
    {
        CreateScene("Test_06_TimeWeather", root =>
        {
            AddPlayerAndTerrain(root);
            AddTestConfig(root, new[] { "Time", "Weather", "Player", "Camera" }, "주야간 전환, 날씨 변화, 시간 흐름 테스트");
        });
    }

    private void GenerateGasBombScene()
    {
        CreateScene("Test_07_GasBomb", root =>
        {
            AddPlayerAndTerrain(root);
            AddTestConfig(root, new[] { "Player", "Gas", "Bomb", "Camera" }, "가스 분사기 장전/분사, 폭탄 던지기 테스트");
        });
    }

    private void GenerateDraculaScene()
    {
        CreateScene("Test_08_Dracula", root =>
        {
            AddPlayerAndTerrain(root);
            AddTestConfig(root, new[] { "Dracula", "Time", "Player", "Camera" }, "드라큘라 영지 야간 전용, 스켈레톤 병사 테스트");
        });
    }

    private void GenerateAllInOneSimpleScene()
    {
        CreateScene("Test_09_AllInOne", root =>
        {
            AddPlayerAndTerrain(root);
            AddTestConfig(root, new[] { "All" }, "모든 시스템 간소화 버전 (배치 수 제한)");
        });
    }
}