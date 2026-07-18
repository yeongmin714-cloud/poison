#if false
using UnityEngine;
using ProjectName.UI;
using ProjectName.Core;
using ProjectName.UI.Themes;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_02_UI 전용: UI 창 전체 시스템을 테스트하기 위한 최소 구성.
    /// 클론 방식 대신 빈 씬에서 필요한 UI 구성 요소만 생성.
    /// GameManager를 통해 핵심 시스템 초기화 + UI 윈도우 생성.
    /// </summary>
    public class TestUISetup : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool _createAllWindows = true;
        [SerializeField] private bool _createPlayer = false; // UI만 테스트하려면 false

        private void Awake()
        {
            EnsureEventSystem();
            CreateGameManager();
            CreateUIManager();
            CreateCanvas();
            CreateUIWindows();
            SetupPlayerIfNeeded();
            SetupGround();
            SetupLight();
            Debug.Log("[TestUISetup] ✅ UI 테스트 씬 설정 완료");
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[TestUISetup] ✅ EventSystem 생성");
            }
        }

        private void CreateGameManager()
        {
            if (GameManager.Instance == null)
            {
                var gmGO = new GameObject("GameManager");
                gmGO.AddComponent<GameManager>();
                Debug.Log("[TestUISetup] ✅ GameManager 생성 (시스템 초기화 위임)");
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

                Debug.Log("[TestUISetup] ✅ UIManager 생성 + KeyBindings 연결");
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
                Debug.Log("[TestUISetup] ✅ Canvas 생성");
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
            // MapWindow
            CreateWindow<MapWindow>("MapWindow", canvasTransform);
            // LootWindow
            CreateWindow<LootWindow>("LootWindow", canvasTransform);

            Debug.Log("[TestUISetup] ✅ UI 윈도우 5개 생성 완료");
        }

        private void CreateWindow<T>(string name, Transform parent) where T : UIWindow
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            var window = go.AddComponent<T>();
            window.ApplyTheme(GetDefaultThemeForWindow(name));
            Debug.Log($"[TestUISetup] ✅ {name} 생성됨");
        }

        private UIDesignTheme GetDefaultThemeForWindow(string windowName)
        {
            // Phase33 테마 적용
            switch (windowName)
            {
                case "InventoryWindow": return Phase33_Themes.CreateInventoryTheme();
                case "QuestWindow": return Phase33_Themes.CreateQuestTheme();
                case "RecipeWindow": return Phase33_Themes.CreateRecipeTheme();
                case "MapWindow": return Phase33_Themes.CreateMedievalMapTheme();
                case "LootWindow": return Phase33_Themes.CreateMedievalShopTheme();
                default: return Phase33_Themes.CreateInventoryTheme();
            }
        }

        private void SetupPlayerIfNeeded()
        {
            if (!_createPlayer) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
                player.transform.position = Vector3.zero;
                Debug.Log("[TestUISetup] ✅ Player 생성 (최소 구성)");
            }
        }

        private void SetupGround()
        {
            if (GameObject.Find("Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = new Vector3(0, -0.5f, 0);
                Debug.Log("[TestUISetup] ✅ Ground 생성");
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
                Debug.Log("[TestUISetup] ✅ Directional Light 생성");
            }
        }
    }
}
#endif