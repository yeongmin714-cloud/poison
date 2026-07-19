using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_05_Craft 씬 전용: 크래프트 + 인벤토리 시스템 통합 검증.
    /// CraftPresetManager, EquipmentManager, WarehouseSystem, PlayerInventory,
    /// CraftSuccessSystem, EquipmentDurabilitySystem, EquipmentRepairSystem,
    /// CraftResultPopup, UIManager, InventoryWindow, RecipeWindow 구성.
    /// </summary>
    public class TestCraftSetup : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool _createAllWindows = true;
        [SerializeField] private bool _createPlayer = true;
        [SerializeField] private bool _addTestMaterials = true;

        [Header("Player Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _runSpeed = 10f;

        [Header("Camera Settings")]
        [SerializeField] private float _orbitRadius = 30f;
        [SerializeField] private float _defaultPitch = 45f;

        private void Awake()
        {
            EnsureEventSystem();
            EnsureGameManager();
            CreateUIManager();
            CreateCanvas();
            CreateUIWindows();
            SetupPlayer();
            SetupCamera();
            SetupGround();
            SetupLight();
            AddTestMaterials();
            
            Debug.Log("[TestCraftSetup] ✅ 크래프트+인벤토리 테스트 씬 설정 완료");
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[TestCraftSetup] ✅ EventSystem 생성");
            }
        }

        private void EnsureGameManager()
        {
            if (GameManager.Instance == null)
            {
                var gmGO = new GameObject("GameManager");
                gmGO.AddComponent<GameManager>();
                gmGO.AddComponent<BuffManager>();
                gmGO.AddComponent<MonsterLevelManager>();
                gmGO.AddComponent<MonsterAggroSystem>();
                gmGO.AddComponent<MonsterSkillSystem>();
                Debug.Log("[TestCraftSetup] ✅ GameManager 생성");
            }
        }

        private void CreateUIManager()
        {
            if (UIManager.Instance == null)
            {
                var uiMgrGO = new GameObject("UIManager");
                var uiMgr = uiMgrGO.AddComponent<UIManager>();

                // KeyBindings 자동 생성
                var kb = ScriptableObject.CreateInstance<KeyBindings>();
                uiMgr.SetKeyBindings(kb);

                Debug.Log("[TestCraftSetup] ✅ UIManager 생성 + KeyBindings 연결");
            }
        }

        private void CreateCanvas()
        {
            if (FindAnyObjectByType<Canvas>() == null)
            {
                var canvasGO = new GameObject("Canvas");
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Debug.Log("[TestCraftSetup] ✅ Canvas 생성");
            }
        }

        private void CreateUIWindows()
        {
            if (!_createAllWindows) return;

            var canvas = FindAnyObjectByType<Canvas>();
            Transform canvasTransform = canvas != null ? canvas.transform : null;

            // InventoryWindow
            CreateWindow<InventoryWindow>("InventoryWindow", canvasTransform);
            // QuestWindow
            CreateWindow<QuestWindow>("QuestWindow", canvasTransform);
            // RecipeWindow
            CreateWindow<RecipeWindow>("RecipeWindow", canvasTransform);
            // CraftResultPopup
            CreateWindow<CraftResultPopup>("CraftResultPopup", canvasTransform);
            // EquipmentWindow (있다면)
            // CreateWindow<EquipmentWindow>("EquipmentWindow", canvasTransform);

            Debug.Log("[TestCraftSetup] ✅ UI 윈도우 생성 완료");
        }

        private void CreateWindow<T>(string name, Transform parent) where T : UIWindow
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            var window = go.AddComponent<T>();
            window.ApplyTheme(GetDefaultThemeForWindow(name));
            Debug.Log($"[TestCraftSetup] ✅ {name} 생성됨");
        }

        private UIDesignTheme GetDefaultThemeForWindow(string windowName)
        {
            switch (windowName)
            {
                case "InventoryWindow": return Phase33_Themes.CreateInventoryTheme();
                case "QuestWindow": return Phase33_Themes.CreateQuestTheme();
                case "RecipeWindow": return Phase33_Themes.CreateRecipeTheme();
                case "CraftResultPopup": return Phase33_Themes.CreateCraftingTheme();
                default: return Phase33_Themes.CreateInventoryTheme();
            }
        }

        private void SetupPlayer()
        {
            if (!_createPlayer) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
            }

            // CharacterController
            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
            }

            // PlayerMovement
            var pmType = typeof(ProjectName.Systems.PlayerMovement);
            if (player.GetComponent(pmType) == null)
                player.AddComponent(pmType);

            // PlayerInput
            if (player.GetComponent<PlayerInput>() == null)
            {
                var pi = player.AddComponent<PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
            }

            // PlayerPlaceholder
            if (player.GetComponent<PlayerPlaceholder>() == null)
                player.AddComponent<PlayerPlaceholder>();

            // PlayerInventory
            if (player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();

            // CraftPresetManager 접근용
            // EquipmentManager는 씬에 있으면 자동으로 사용

            player.transform.position = Vector3.zero;
            Debug.Log("[TestCraftSetup] ✅ Player 설정 완료 (인벤토리 포함)");
        }

        private void SetupCamera()
        {
            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
            }

            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null)
                cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;

            var tdcType = typeof(ProjectName.Systems.TopDownCameraController);
            if (camGO.GetComponent(tdcType) == null)
                camGO.AddComponent(tdcType);

            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();

            Debug.Log("[TestCraftSetup] ✅ 카메라 설정 완료");
        }

        private void SetupGround()
        {
            if (GameObject.Find("Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = new Vector3(0, -0.5f, 0);
                ground.transform.localScale = Vector3.one * 50f;

                var renderer = ground.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
                    mat.SetFloat("_Smoothness", 0f);
                    renderer.material = mat;
                }
                Debug.Log("[TestCraftSetup] ✅ Ground 생성");
            }
        }

        private void SetupLight()
        {
            if (FindAnyObjectByType<Light>() == null)
            {
                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                Debug.Log("[TestCraftSetup] ✅ Directional Light 생성");
            }
        }

        private void AddTestMaterials()
        {
            if (!_addTestMaterials) return;

            // PlayerInventory에 테스트 재료 추가
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var inventory = player.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    // 기본 재료들 추가
                    inventory.AddItem("iron_ore", 50);
                    inventory.AddItem("wood_log", 50);
                    inventory.AddItem("herb_basic", 30);
                    inventory.AddItem("leather_scrap", 20);
                    inventory.AddItem("magic_crystal", 10);
                    inventory.AddItem("gold_coin", 1000);

                    Debug.Log("[TestCraftSetup] ✅ 테스트 재료/아이템 추가 완료");
                }
            }

            // CraftPresetManager 확인
            if (CraftPresetManager.Instance != null)
            {
                Debug.Log("[TestCraftSetup] ✅ CraftPresetManager 사용 가능");
            }
        }
    }
}