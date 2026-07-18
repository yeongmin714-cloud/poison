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
        gen.GenTerritory();
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
        if (GUILayout.Button("🌙 Test_06_TimeWeather")) GenTime();
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
        // Remove default objects (Camera, Light)
        foreach (var go in FindObjectsOfType<GameObject>())
        {
            if (go.GetComponent<Camera>() || go.GetComponent<Light>())
                DestroyImmediate(go);
        }
        var setup = new GameObject("_TestUISetup").AddComponent<TestUISetup>();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[TestSceneGenerator] ✅ Test_02_UI 생성 완료 (TestUISetup 기반)");
    }

    private void GenCombat()
    {
        SaveScene("Test_03_Combat");
        Debug.Log("TODO: Combat 테스트 씬");
    }

    private void GenTerritory()
    {
        SaveScene("Test_04_Territory");
        // 깨끗한 씬에서 시작: Camera, Light 제거
        foreach (var go in FindObjectsOfType<GameObject>())
        {
            if (go.GetComponent<Camera>() || go.GetComponent<Light>())
                DestroyImmediate(go);
        }

        // Player + 기본 설정
        var setup = new GameObject("_TestPlayerSetup").AddComponent<TestPlayerSetup>();

        // 영지 구성: 건물 Placeholder들
        Vector3 center = Vector3.zero;
        CreateBuilding("Shop", BuildingPlaceholder.BuildingType.Shop, center + new Vector3(-5, 0, 0));
        CreateBuilding("CraftHouse", BuildingPlaceholder.BuildingType.CraftHouse, center + new Vector3(5, 0, 0));
        CreateBuilding("Church", BuildingPlaceholder.BuildingType.Church, center + new Vector3(0, 0, -5));
        CreateBuilding("NPCHouse1", BuildingPlaceholder.BuildingType.NPCHouse, center + new Vector3(-5, 0, -5));
        CreateBuilding("NPCHouse2", BuildingPlaceholder.BuildingType.NPCHouse, center + new Vector3(5, 0, -5));
        CreateBuilding("NPCHouse3", BuildingPlaceholder.BuildingType.NPCHouse, center + new Vector3(-5, 0, 5));
        CreateBuilding("NPCHouse4", BuildingPlaceholder.BuildingType.NPCHouse, center + new Vector3(5, 0, 5));

        // 병사 Placeholder들 (입구 경비 + 순찰)
        CreateGuard("GuardLeft", center + new Vector3(-2, 0, 2));
        CreateGuard("GuardRight", center + new Vector3(2, 0, 2));
        CreateGuard("GuardPatrol1", center + new Vector3(0, 0, 8));
        CreateGuard("GuardPatrol2", center + new Vector3(8, 0, 0));
        CreateGuard("GuardPatrol3", center + new Vector3(-8, 0, 0));
        CreateGuard("GuardPatrol4", center + new Vector3(0, 0, -8));

        // TerritoryManager (싱글톤)
        var tmGO = new GameObject("TerritoryManager");
        tmGO.AddComponent<TerritoryManager>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[TestSceneGenerator] ✅ Test_04_Territory 생성 완료 (건물 7개 + 병사 6명 + TerritoryManager)");
    }

    private static void CreateBuilding(string name, BuildingPlaceholder.BuildingType type, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(3, 2, 3);

        // Collider isTrigger 해제 (물리 충돌 유지)
        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = false;

        var placeholder = go.AddComponent<BuildingPlaceholder>();
        placeholder.buildingType = type;
        placeholder.buildingName = name;
    }

    private static void CreateGuard(string name, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(1.5f, 2f, 1.5f);

        // Animator + Soldier_Animator.controller 연결
        var animator = go.AddComponent<Animator>();
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Animations/Soldier_Animator.controller");
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
            animator.avatar = null; // Generic Avatar — 런타임 생성
        }

        // RigAnimationController (애니메이션 상태 관리)
        go.AddComponent<RigAnimationController>();

        // ProceduralPoseController (프로시저럴 포즈 보정)
        go.AddComponent<ProceduralPoseController>();

        // GuardPlaceholder
        var placeholder = go.AddComponent<GuardPlaceholder>();
        placeholder.GetType().GetField("guardName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(placeholder, name);
        placeholder.GetType().GetField("level", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(placeholder, 1);
        placeholder.GetType().GetField("nation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(placeholder, "동");
    }

    private void GenCraft()
    {
        SaveScene("Test_05_Craft");
        // Remove default objects (Camera, Light)
        foreach (var go in FindObjectsOfType<GameObject>())
        {
            if (go.GetComponent<Camera>() || go.GetComponent<Light>())
                DestroyImmediate(go);
        }
        var setup = new GameObject("_TestCraftSetup").AddComponent<TestCraftSetup>();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[TestSceneGenerator] ✅ Test_05_Craft 생성 완료 (TestCraftSetup 기반 — 크래프트+인벤토리+스테이션+테스트 아이템)");
    }

    private void GenTime()
    {
        SaveScene("Test_06_TimeWeather");
        // Remove default Camera, Light (깨끗한 씬)
        foreach (var go in FindObjectsOfType<GameObject>())
        {
            if (go.GetComponent<Camera>() || go.GetComponent<Light>())
                DestroyImmediate(go);
        }
        // TestTimeWeatherSetup — 시간+날씨 시스템 최소 구성
        var setup = new GameObject("_TestTimeWeatherSetup").AddComponent<TestTimeWeatherSetup>();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[TestSceneGenerator] ✅ Test_06_TimeWeather 생성 완료 (TestTimeWeatherSetup 기반)");
    }

    private void GenGas()
    {
        SaveScene("Test_07_Gas");
        Debug.Log("TODO: Gas 테스트 씬");
    }

    private void GenDracula()
    {
        SaveScene("Test_08_Dracula");
        // Remove default Camera, Light (깨끗한 씬)
        foreach (var go in FindObjectsOfType<GameObject>())
        {
            if (go.GetComponent<Camera>() || go.GetComponent<Light>())
                DestroyImmediate(go);
        }
        // TestDraculaSetup — 드라큘라+야간 시스템 최소 구성
        var setup = new GameObject("_TestDraculaSetup").AddComponent<TestDraculaSetup>();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[TestSceneGenerator] ✅ Test_08_Dracula 생성 완료 (TestDraculaSetup 기반 — DraculaLord + NightCycle + SkeletonGuards)");
    }

    private void GenAllInOne()
    {
        // 1. MainScene 열기
        string mainScenePath = "Assets/Scenes/MainScene.unity";
        var mainScene = EditorSceneManager.OpenScene(mainScenePath, OpenSceneMode.Single);

        // 2. Test_09_AllInOne으로 복제 저장
        string savePath = Path.Combine(_scenePath, "Test_09_AllInOne.unity");
        if (!Directory.Exists(_scenePath))
            Directory.CreateDirectory(_scenePath);
        EditorSceneManager.SaveScene(mainScene, savePath);
        EditorSceneManager.OpenScene(savePath, OpenSceneMode.Single);

        // 3. 불필요 요소 제거 (디버그/에디터 전용 오브젝트)
        StripUnnecessaryObjects();

        // 4. TestAllInOneSetup 부착 (누락 시스템 보완)
        var existingSetup = FindAnyObjectByType<TestAllInOneSetup>();
        if (existingSetup == null)
        {
            var setupGO = new GameObject("_TestAllInOneSetup");
            setupGO.AddComponent<TestAllInOneSetup>();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[TestSceneGenerator] ✅ Test_09_AllInOne 생성 완료 (MainScene 복제 → 테스트 최적화)");
    }

    private void StripUnnecessaryObjects()
    {
        // MonsterSpawner 제거 (테스트 환경에 맞춰 개별 생성)
        var spawner = GameObject.Find("MonsterSpawner");
        if (spawner != null)
        {
            Debug.Log("[TestSceneGenerator] ⚠️ MonsterSpawner 제거됨");
            Object.DestroyImmediate(spawner);
        }

        // CountryTerritories_Overlay 제거 (시각적 오버레이 — 테스트 불필요)
        var overlay = GameObject.Find("CountryTerritories_Overlay");
        if (overlay != null)
        {
            Debug.Log("[TestSceneGenerator] ⚠️ CountryTerritories_Overlay 제거됨");
            Object.DestroyImmediate(overlay);
        }

        // MapBoundary 제거 (씬 경계 — 테스트 불필요)
        var mapBoundary = GameObject.Find("MapBoundary");
        if (mapBoundary != null)
        {
            Debug.Log("[TestSceneGenerator] ⚠️ MapBoundary 제거됨");
            Object.DestroyImmediate(mapBoundary);
        }

        // Boundary visual elements 제거
        foreach (var dir in new[] { "East", "West", "North", "South" })
        {
            var boundary = GameObject.Find($"Boundary_{dir}");
            if (boundary != null)
            {
                Object.DestroyImmediate(boundary);
                Debug.Log($"[TestSceneGenerator] ⚠️ Boundary_{dir} 제거됨");
            }
        }

        Debug.Log("[TestSceneGenerator] ✅ 불필요 오브젝트 7개 제거 완료");
    }
}