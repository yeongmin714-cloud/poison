using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using ProjectName.Core;
using ProjectName.Core.Data;
using System;
using System.Reflection;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_05_Craft 씬 전용: 크래프트 + 인벤토리 시스템 통합 검증.
    /// CraftPresetManager, EquipmentManager, WarehouseSystem, PlayerInventory,
    /// CraftSuccessSystem, EquipmentDurabilitySystem, EquipmentRepairSystem,
    /// CraftResultPopup, UIManager, InventoryWindow, RecipeWindow 구성.
    /// UI 타입은 리플렉션으로 접근 (어셈블리 순환 참조 방지).
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

        // 리플렉션용 캐시
        private Type _uiManagerType;
        private Type _uiWindowType;
        private Type _keyBindingsType;
        private Type _phase33ThemesType;
        private Type _inventoryWindowType;
        private Type _questWindowType;
        private Type _recipeWindowType;
        private Type _craftResultPopupType;

        private void Awake()
        {
            CacheUIReflectionTypes();
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

        private void CacheUIReflectionTypes()
        {
            var uiAssembly = Assembly.Load("ProjectName.UI");
            if (uiAssembly == null)
            {
                Debug.LogWarning("[TestCraftSetup] ProjectName.UI 어셈블리를 찾을 수 없습니다. UI 테스트는 건너뜁니다.");
                _createAllWindows = false;
                return;
            }

            _uiManagerType = uiAssembly.GetType("ProjectName.UI.Core.UIManager");
            _uiWindowType = uiAssembly.GetType("ProjectName.UI.UIWindow");
            _keyBindingsType = uiAssembly.GetType("ProjectName.UI.KeyBindings");
            _phase33ThemesType = uiAssembly.GetType("ProjectName.UI.Themes.Phase33_Themes");
            _inventoryWindowType = uiAssembly.GetType("ProjectName.UI.InventoryWindow");
            _questWindowType = uiAssembly.GetType("ProjectName.UI.QuestWindow");
            _recipeWindowType = uiAssembly.GetType("ProjectName.UI.RecipeWindow");
            _craftResultPopupType = uiAssembly.GetType("ProjectName.UI.CraftResultPopup");

            if (_uiManagerType == null || _uiWindowType == null)
            {
                Debug.LogWarning("[TestCraftSetup] UI 핵심 타입을 찾을 수 없습니다. UI 테스트는 건너뜁니다.");
                _createAllWindows = false;
            }
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
            if (!_createAllWindows || _uiManagerType == null) return;

            if (_uiManagerType.GetProperty("Instance")?.GetValue(null) == null)
            {
                var uiMgrGO = new GameObject("UIManager");
                var uiMgr = uiMgrGO.AddComponent(_uiManagerType);
                var kb = ScriptableObject.CreateInstance(_keyBindingsType);
                _uiManagerType.GetMethod("SetKeyBindings")?.Invoke(uiMgr, new object[] { kb });
                Debug.Log("[TestCraftSetup] ✅ UIManager 생성 + KeyBindings 연결");
            }
        }

        private void CreateCanvas()
        {
            if (!_createAllWindows) return;

            if (FindAnyObjectByType<Canvas>() == null)
            {
                var canvasGO = new GameObject("Canvas");
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
                Debug.Log("[TestCraftSetup] ✅ Canvas 생성");
            }
        }

        private void CreateUIWindows()
        {
            if (!_createAllWindows || _uiWindowType == null) return;

            var canvas = FindAnyObjectByType<Canvas>();
            Transform canvasTransform = canvas != null ? canvas.transform : null;

            CreateUIWindow(_inventoryWindowType, "InventoryWindow", canvasTransform);
            CreateUIWindow(_questWindowType, "QuestWindow", canvasTransform);
            CreateUIWindow(_recipeWindowType, "RecipeWindow", canvasTransform);
            CreateUIWindow(_craftResultPopupType, "CraftResultPopup", canvasTransform);

            Debug.Log("[TestCraftSetup] ✅ UI 윈도우 생성 완료");
        }

        private void CreateUIWindow(Type windowType, string name, Transform parent)
        {
            if (windowType == null || _uiWindowType == null) return;

            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            var window = go.AddComponent(windowType);
            
            var theme = GetDefaultThemeForWindow(name);
            if (theme != null)
            {
                var applyThemeMethod = _uiWindowType.GetMethod("ApplyTheme");
                applyThemeMethod?.Invoke(window, new object[] { theme });
            }
            
            Debug.Log($"[TestCraftSetup] ✅ {name} 생성됨");
        }

        private object GetDefaultThemeForWindow(string windowName)
        {
            if (_phase33ThemesType == null) return null;

            return windowName switch
            {
                "InventoryWindow" => _phase33ThemesType.GetMethod("CreateInventoryTheme")?.Invoke(null, null),
                "QuestWindow" => _phase33ThemesType.GetMethod("CreateQuestTheme")?.Invoke(null, null),
                "RecipeWindow" => _phase33ThemesType.GetMethod("CreateRecipeTheme")?.Invoke(null, null),
                "CraftResultPopup" => _phase33ThemesType.GetMethod("CreateCraftingTheme")?.Invoke(null, null),
                _ => _phase33ThemesType.GetMethod("CreateInventoryTheme")?.Invoke(null, null)
            };
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
            if (player.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
            {
                var pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
            }

            // PlayerPlaceholder
            if (player.GetComponent<PlayerPlaceholder>() == null)
                player.AddComponent<PlayerPlaceholder>();

            // PlayerInventory
            if (player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();

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

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var inventory = player.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    // Use reflection to find AddItem method since parameter type may vary
                    var addItemMethod = inventory.GetType().GetMethod("AddItem", new[] { typeof(string), typeof(int) });
                    if (addItemMethod != null)
                    {
                        addItemMethod.Invoke(inventory, new object[] { "iron_ore", 50 });
                        addItemMethod.Invoke(inventory, new object[] { "wood_log", 50 });
                        addItemMethod.Invoke(inventory, new object[] { "herb_basic", 30 });
                        addItemMethod.Invoke(inventory, new object[] { "leather_scrap", 20 });
                        addItemMethod.Invoke(inventory, new object[] { "magic_crystal", 10 });
                        addItemMethod.Invoke(inventory, new object[] { "gold_coin", 1000 });
                    }

                    Debug.Log("[TestCraftSetup] ✅ 테스트 재료/아이템 추가 완료");
                }
            }

            if (CraftPresetManager.Instance != null)
            {
                Debug.Log("[TestCraftSetup] ✅ CraftPresetManager 사용 가능");
            }
        }
    }
}