using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI;
using ProjectName.UI.Themes;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_05_Craft 씬 전용: 크래프트 + 인벤토리 시스템 통합 검증.
    /// PlayerInventory + CraftingUI + InventoryWindow + CraftingStation 생성.
    /// 테스트 아이템을 인벤토리에 미리 추가하여 조합 테스트 가능.
    /// </summary>
    public class TestCraftSetup : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool _addTestItems = true;
        [SerializeField] private bool _createCraftingStation = true;

        private void Awake()
        {
            EnsureEventSystem();
            CreateGameManager();
            CreateCraftPresetManager();
            CreateUIManager();
            CreateTooltipWindow();
            CreateCanvas();
            CreateUIWindows();
            SetupPlayer();
            SetupGround();
            SetupLight();

            if (_addTestItems)
                AddTestItems();

            if (_createCraftingStation)
                CreateCraftStation();

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

        private void CreateGameManager()
        {
            if (GameManager.Instance == null)
            {
                var gmGO = new GameObject("GameManager");
                gmGO.AddComponent<GameManager>();
                Debug.Log("[TestCraftSetup] ✅ GameManager 생성");
            }
        }

        private void CreateCraftPresetManager()
        {
            if (CraftPresetManager.Instance == null)
            {
                var cpmGO = new GameObject("CraftPresetManager");
                cpmGO.AddComponent<CraftPresetManager>();
                Debug.Log("[TestCraftSetup] ✅ CraftPresetManager 생성");
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
                var kbField = typeof(UIManager).GetField("_keyBindings",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (kbField != null)
                    kbField.SetValue(uiMgr, kb);

                Debug.Log("[TestCraftSetup] ✅ UIManager 생성 + KeyBindings 연결");
            }
        }

        private void CreateTooltipWindow()
        {
            // TooltipWindow — CraftingUI/InventoryWindow에서 마우스 호버 시 NullReference 방지
            if (TooltipWindow.Instance == null)
            {
                var ttwGO = new GameObject("TooltipWindow");
                ttwGO.AddComponent<TooltipWindow>();
                Debug.Log("[TestCraftSetup] ✅ TooltipWindow 생성 (툴팁 NPE 방지)");
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
            var canvas = FindAnyObjectByType<Canvas>();
            Transform canvasTransform = canvas != null ? canvas.transform : null;

            // InventoryWindow (인벤토리)
            CreateWindow<InventoryWindow>("InventoryWindow", canvasTransform);

            // RecipeWindow (레시피 북)
            CreateWindow<RecipeWindow>("RecipeWindow", canvasTransform);

            // CraftingUI (크래프트 테이블)
            CreateWindow<CraftingUI>("CraftingUI", canvasTransform);

            // UIManager 정적 참조 등록 (레거시 호환)
            UIManager.inventoryWindow = FindAnyObjectByType<InventoryWindow>();
            UIManager.recipeWindow = FindAnyObjectByType<RecipeWindow>();
            UIManager.craftingWindow = FindAnyObjectByType<CraftingUI>();

            // UIManager._windows 딕셔너리 등록 (키 바인딩/OpenWindow(typeof) 지원)
            RegisterUIWindow("Inventory", UIManager.inventoryWindow as UIWindow);
            RegisterUIWindow("Recipe", UIManager.recipeWindow as UIWindow);
            RegisterUIWindow("Crafting", UIManager.craftingWindow as UIWindow);

            Debug.Log("[TestCraftSetup] ✅ UI 윈도우 3개 생성 완료 (InventoryWindow + RecipeWindow + CraftingUI)");
        }

        private void RegisterUIWindow(string actionName, UIWindow window)
        {
            if (window == null) return;
            var uiMgr = UIManager.Instance;
            if (uiMgr == null) return;

            var windowsField = typeof(UIManager).GetField("_windows",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (windowsField == null) return;

            var dict = windowsField.GetValue(uiMgr) as System.Collections.Generic.Dictionary<string, UIWindow>;
            if (dict != null && !dict.ContainsKey(actionName))
            {
                dict[actionName] = window;
                Debug.Log($"[TestCraftSetup] ✅ UIManager에 '{actionName}' 윈도우 등록됨");
            }
        }

        private void CreateWindow<T>(string name, Transform parent) where T : UIWindow
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            var window = go.AddComponent<T>();
            window.ApplyTheme(UI.Themes.Phase33_Themes.CreateInventoryTheme());
            Debug.Log($"[TestCraftSetup] ✅ {name} 생성됨");
        }

        private void SetupPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
            }

            // PlayerInventory
            if (player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();

            // PlayerStats (CraftingUI에서 경험치 추가용)
            if (player.GetComponent<PlayerStats>() == null)
                player.AddComponent<PlayerStats>();

            player.transform.position = Vector3.zero;
            Debug.Log("[TestCraftSetup] ✅ Player + PlayerInventory 생성 완료");
        }

        private void AddTestItems()
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogError("[TestCraftSetup] PlayerInventory.Instance가 null입니다!");
                return;
            }

            // 기본 약초 5종 각 5개씩
            inventory.AddItem(PlayerInventory.Herb_Red, 5);
            inventory.AddItem(PlayerInventory.Herb_Purple, 5);
            inventory.AddItem(PlayerInventory.Herb_Yellow, 5);
            inventory.AddItem(PlayerInventory.Herb_Silver, 5);
            inventory.AddItem(PlayerInventory.Herb_Green, 5);

            // 고기 3종 각 3개씩
            inventory.AddItem(PlayerInventory.RabbitMeat, 3);
            inventory.AddItem(PlayerInventory.BoarMeat, 3);
            inventory.AddItem(PlayerInventory.WolfMeat, 3);

            // 재료 5종 각 2개씩
            inventory.AddItem(PlayerInventory.RabbitFur, 2);
            inventory.AddItem(PlayerInventory.BoarLeather, 2);
            inventory.AddItem(PlayerInventory.BoarTusk, 2);
            inventory.AddItem(PlayerInventory.WolfTooth, 2);
            inventory.AddItem(PlayerInventory.WolfFur, 2);

            Debug.Log("[TestCraftSetup] ✅ 테스트 아이템 13종 추가 완료 (약초5+고기3+재료5)");
        }

        private void CreateCraftStation()
        {
            // 크래프트 스테이션을 플레이어 근처에 배치
            var stationGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stationGO.name = "CraftingStation";
            stationGO.transform.position = new Vector3(3, 0, 0);
            stationGO.transform.localScale = new Vector3(1.5f, 1f, 1.5f);

            // 머티리얼 갈색
            var renderer = stationGO.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.5f, 0.3f, 0.1f);
                renderer.material = mat;
            }

            var station = stationGO.AddComponent<CraftingStation>();
            Debug.Log("[TestCraftSetup] ✅ CraftingStation 생성 (position: (3, 0, 0))");
        }

        private void SetupGround()
        {
            if (GameObject.Find("Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = new Vector3(0, -0.5f, 0);
                ground.transform.localScale = Vector3.one * 30f;

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
    }
}