using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 메인씬을 복제 → 불필요 시스템 제거 방식으로 테스트 씬 생성.
/// 각 씬은 실제 PlayerMovement, TopDownCamera, Placeholder 등을 모두 포함합니다.
/// </summary>
public class TestSceneGenerator : EditorWindow
{
    private string _scenePath = "Assets/Scenes/TestScenes";
    private static readonly string MainScenePath = "Assets/Scenes/MainScene.unity";

    // 시스템별 제거할 오브젝트 이름 목록
    private static readonly string[] TerritoryRoots = {
        "Territory_A_", "Territory_B_", "Territory_C_", "Country_", "Label_Territory",
        "Border_A_", "Border_B_", "Border_C_", "Boundary_", "BoundaryMat_",
        "GuardPlaceholder", "SkeletonGuard", "GateGuard", "BuildingPlaceholder",
        "FlagPole", "Emblem"
    };

    private static readonly string[] MonsterRoots = {
        "MonsterSpawner", "AnimalAI", "Wolf", "Boar", "Rabbit", "Deer",
        "Monster_", "Slime_", "Bat_", "Crow_"
    };

    private static readonly string[] NPCAndMiscRoots = {
        "NpcQuestGiver", "MercenaryManager", "Mercenary", "BardMercenary",
        "ArenaSystem", "WanderingMerchant", "CraftingStation", "CraftingTable",
        "Tavern", "Church"
    };

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

    public static void GenerateAllBatch() => GenerateAllInternal();

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
        EditorGUILayout.HelpBox(
            "메인씬을 복제 → 불필요한 시스템만 제거하여 테스트 씬 생성.\n"
            + "실제 PlayerMovement, TopDownCamera, Placeholder 등이 모두 포함됩니다.",
            MessageType.Info);

        _scenePath = EditorGUILayout.TextField("생성 경로", _scenePath);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("🚀 모든 테스트 씬 생성 (9개)", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("확인", "메인씬을 복제하여 9개 테스트 씬 생성", "생성", "취소"))
                GenerateAllInternal();
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("🏃 Test_01_Player")) GeneratePlayerScene();
        if (GUILayout.Button("🖥️ Test_02_UI")) GenerateUIScene();
        if (GUILayout.Button("⚔️ Test_03_Combat")) GenerateCombatScene();
        if (GUILayout.Button("🏰 Test_04_Territory")) GenerateTerritoryScene();
        if (GUILayout.Button("🧪 Test_05_Craft")) GenerateCraftScene();
        if (GUILayout.Button("🌙 Test_06_TimeWeather")) GenerateTimeWeatherScene();
        if (GUILayout.Button("💨 Test_07_GasBomb")) GenerateGasBombScene();
        if (GUILayout.Button("🧛 Test_08_Dracula")) GenerateDraculaScene();
        if (GUILayout.Button("🛡️ Test_09_AllInOne")) GenerateAllInOneSimpleScene();
    }

    /// <summary>
    /// 메인씬을 열고, 지정된 패턴과 일치하는 루트 오브젝트들을 제거한 후 저장.
    /// </summary>
    private void CloneAndStrip(string sceneName, string[] removePatterns, string focusLabel)
    {
        if (!Directory.Exists(_scenePath))
            Directory.CreateDirectory(_scenePath);

        string path = Path.Combine(_scenePath, sceneName + ".unity");
        var mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        var rootObjects = mainScene.GetRootGameObjects();

        foreach (var root in rootObjects)
        {
            if (ShouldRemove(root.name, removePatterns))
            {
                DestroyImmediate(root);
            }
        }

        // Add TestSceneConfig
        var configGO = new GameObject("_TestSceneConfig");
        var config = configGO.AddComponent<TestSceneConfig>();
        config.testFocus = focusLabel;

        EditorSceneManager.SaveScene(mainScene, path);
        Debug.Log($"✅ 생성: {path} ({focusLabel})");
    }

    private bool ShouldRemove(string name, string[] patterns)
    {
        foreach (var p in patterns)
        {
            if (name.StartsWith(p)) return true;
            if (name.Contains(p)) return true;
        }
        return false;
    }

    // ── 각 씬별 제거 패턴 정의 ──

    private void GeneratePlayerScene()
    {
        // Player + 지형 + 카메라만 유지
        CloneAndStrip("Test_01_Player", 
            TerritoryRoots.Concat(MonsterRoots).Concat(NPCAndMiscRoots)
                .Concat(new[] { "MapWindow", "InventoryWindow", "QuestWindow", "RecipeWindow",
                                "LootWindow", "UI", "UIManager", "Global Volume",
                                "MapBoundary", "CountryTerritories_Overlay",
                                "GameManager" }).ToArray(),
            "Player 이동 + 카메라 + 지형 (가장 기본)");
    }

    private void GenerateUIScene()
    {
        // UI 창 테스트 - 게임매니저, UI 시스템 유지, 몬스터/영지 제거
        CloneAndStrip("Test_02_UI",
            TerritoryRoots.Concat(MonsterRoots)
                .Concat(new[] { "MapBoundary", "CountryTerritories_Overlay" }).ToArray(),
            "모든 UI 창 테스트");
    }

    private void GenerateCombatScene()
    {
        // 전투 테스트 - 몬스터 유지, 영지/UI 제거
        CloneAndStrip("Test_03_Combat",
            TerritoryRoots.Concat(NPCAndMiscRoots)
                .Concat(new[] { "MapWindow", "InventoryWindow", "QuestWindow", "RecipeWindow",
                                "LootWindow", "MapBoundary", "CountryTerritories_Overlay" }).ToArray(),
            "전투 + 몬스터 테스트");
    }

    private void GenerateTerritoryScene()
    {
        // 영지 테스트 - 영지 유지, 몬스터 제거
        CloneAndStrip("Test_04_Territory",
            MonsterRoots.Concat(NPCAndMiscRoots)
                .Concat(new[] { "MapWindow", "InventoryWindow", "QuestWindow", "RecipeWindow",
                                "LootWindow" }).ToArray(),
            "영지 + 병사 + 건물 테스트");
    }

    private void GenerateCraftScene()
    {
        // 크래프트 테스트 - 크래프트 관련 유지, 영지/몬스터 제거
        CloneAndStrip("Test_05_Craft",
            TerritoryRoots.Concat(MonsterRoots)
                .Concat(new[] { "MapWindow", "QuestWindow", "LootWindow" }).ToArray(),
            "크래프트 + 인벤토리 테스트");
    }

    private void GenerateTimeWeatherScene()
    {
        // 시간/날씨 테스트
        CloneAndStrip("Test_06_TimeWeather",
            TerritoryRoots.Concat(MonsterRoots).Concat(NPCAndMiscRoots)
                .Concat(new[] { "MapWindow", "InventoryWindow", "QuestWindow", "RecipeWindow",
                                "LootWindow", "MapBoundary", "CountryTerritories_Overlay" }).ToArray(),
            "주야간 + 날씨 변화 테스트");
    }

    private void GenerateGasBombScene()
    {
        // 가스/폭탄 테스트
        CloneAndStrip("Test_07_GasBomb",
            TerritoryRoots.Concat(MonsterRoots).Concat(NPCAndMiscRoots)
                .Concat(new[] { "MapWindow", "InventoryWindow", "QuestWindow", "RecipeWindow",
                                "LootWindow", "MapBoundary" }).ToArray(),
            "가스분사기 + 폭탄 테스트");
    }

    private void GenerateDraculaScene()
    {
        // 드라큘라 테스트 - 거의 전체 유지
        CloneAndStrip("Test_08_Dracula",
            MonsterRoots.Concat(new[] { "MercenaryManager", "BardMercenary", "ArenaSystem",
                                        "WanderingMerchant", "MapBoundary" }).ToArray(),
            "드라큘라 + 야간 테스트");
    }

    private void GenerateAllInOneSimpleScene()
    {
        // 모든 시스템 포함, 일부 무거운 시스템만 제거
        CloneAndStrip("Test_09_AllInOne",
            new[] { "MercenaryManager", "BardMercenary", "ArenaSystem",
                    "MapBoundary", "CountryTerritories_Overlay" },
            "모든 시스템 (간소화)");
    }
}