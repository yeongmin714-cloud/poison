using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectName.Core;
using ProjectName.Core.Utils;
using ProjectName.Systems;
using ProjectName.UI;

public static class Phase3_TopDownSceneSetup
{
    [MenuItem("Tools/Phase 3 - Setup Top-Down Player Scene")]
    public static void SetupTopDownScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ===== 1. 기본 지형 (2000x2000 평지) =====
        // Plane = 10x10 기본, Scale 200 = 2000x2000
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(200, 1, 200);
        ground.transform.position = Vector3.zero;
        var groundMat = MaterialHelper.CreateLitMaterial(new Color(0.3f, 0.6f, 0.2f), "Ground_Grass");
        ground.GetComponent<MeshRenderer>().material = groundMat;

        // ===== 2. 환경 오브젝트 (방향감각용 기둥 200개) =====
        for (int i = 0; i < 200; i++)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = $"Pillar_{i}";
            float x = Random.Range(-900f, 900f);
            float z = Random.Range(-900f, 900f);
            // 플레이어 시작 위치 근처에는 배치하지 않음 (0,-950 주변 10m)
            if (Mathf.Abs(x) < 10f && Mathf.Abs(z + 950) < 10f)
            {
                x = (Random.value > 0.5f ? 1 : -1) * Random.Range(15f, 900f);
                z = (Random.value > 0.5f ? 1 : -1) * Random.Range(15f, 900f);
            }
            pillar.transform.position = new Vector3(x, 1f, z);
            pillar.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
            var mat = MaterialHelper.CreateLitMaterial(new Color(0.4f, 0.3f, 0.2f), $"Pillar_{i}_Mat");
            pillar.GetComponent<MeshRenderer>().material = mat;
        }

        // ===== 3. Directional Light =====
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        light.color = new Color(1f, 0.95f, 0.85f);
        light.shadowStrength = 0.5f;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

        // ===== 3.5. Global Volume (Fog + Tonemapping) =====
        var volumeGO = new GameObject("Global Volume");
        var volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        var volProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volProfile.name = "Default_VolumeProfile";
        volume.profile = volProfile;

        // ===== 4. 플레이어 캐릭터 =====
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 0, -950);
        player.tag = "Player";

        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0, 1, 0);

        player.AddComponent<PlayerMovement>();
        player.AddComponent<PlayerPlaceholder>();

        // ===== 5. Top-down 카메라 (마우스 회전 + 플레이어 추적) =====
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        cam.orthographic = false;   // Perspective (3/4 앵글 위해)
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 2000f;

        // TopDownCameraController: 플레이어 따라다님 + 마우스 에지 회전 + 우클릭 회전 + 휠 줌
        var camCtrl = camGO.AddComponent<TopDownCameraController>();

        // ===== 6. GameManager =====
        var gmGO = new GameObject("GameManager");
        gmGO.AddComponent<GameManager>();

        // ===== 7. UIManager + UI Windows (I 키 인벤토리 등) =====
        // 7a. KeyBindings ScriptableObject 생성 (기본값: Q=퀘스트, R=레시피, I=인벤토리, M=지도, ESC=닫기)
        var keyBindings = ScriptableObject.CreateInstance<KeyBindings>();
        // 에디터에서 보이도록 에셋 저장
        string settingsDir = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(settingsDir))
            AssetDatabase.CreateFolder("Assets", "Settings");
        string kbPath = "Assets/Settings/KeyBindings.asset";
        AssetDatabase.CreateAsset(keyBindings, kbPath);
        AssetDatabase.SaveAssets();

        // 7b. UI 부모 GameObject
        var uiGO = new GameObject("UI");
        uiGO.transform.SetParent(null); // 루트

        // 7c. UIManager (싱글톤)
        var uiManagerGO = new GameObject("UIManager");
        uiManagerGO.transform.SetParent(uiGO.transform);
        var uiManager = uiManagerGO.AddComponent<UIManager>();

        // 7d. 각 UIWindow GameObject 생성 (모두 초기 비활성화)
        // InventoryWindow — I 키
        var invGO = new GameObject("InventoryWindow");
        invGO.transform.SetParent(uiGO.transform);
        invGO.SetActive(false);
        var invWindow = invGO.AddComponent<InventoryWindow>();
        UIManager.inventoryWindow = invWindow;

        // LootWindow — 전리품
        var lootGO = new GameObject("LootWindow");
        lootGO.transform.SetParent(uiGO.transform);
        lootGO.SetActive(false);
        var lootWindow = lootGO.AddComponent<LootWindow>();
        UIManager.lootWindow = lootWindow;

        // QuestWindow — Q 키
        var questGO = new GameObject("QuestWindow");
        questGO.transform.SetParent(uiGO.transform);
        questGO.SetActive(false);
        var questWindow = questGO.AddComponent<QuestWindow>();
        UIManager.questWindow = questWindow;

        // RecipeWindow — R 키
        var recipeGO = new GameObject("RecipeWindow");
        recipeGO.transform.SetParent(uiGO.transform);
        recipeGO.SetActive(false);
        var recipeWindow = recipeGO.AddComponent<RecipeWindow>();
        UIManager.recipeWindow = recipeWindow;

        // MapWindow — M 키
        var mapGO = new GameObject("MapWindow");
        mapGO.transform.SetParent(uiGO.transform);
        mapGO.SetActive(false);
        var mapWindow = mapGO.AddComponent<MapWindow>();
        UIManager.mapWindow = mapWindow;

        // UIManager.KeyBindings 직접 할당
        uiManager.GetComponent<UIManager>().SetKeyBindings(keyBindings);

        Debug.Log("[Phase3] UIManager + UIWindows 설정 완료 (I=인벤토리, Q=퀘스트, R=레시피, M=지도)");

        // ===== 씬 저장 =====
        string path = "Assets/Scenes/TopDownScene.unity";
        // 기존 TopDownScene이 있으면 덮어쓰기
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[Phase3] Top-down scene created → {path}");
    }

    [MenuItem("Tools/Phase 3 - Setup Top-Down Player Scene", true)]
    private static bool Validate() => true;
}