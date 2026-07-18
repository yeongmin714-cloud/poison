using UnityEngine;
using System.Linq;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI;
using ProjectName.UI.Themes;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_09_AllInOne 씬 전용: 전체 시스템 통합 런타임 검증.
    /// MainScene을 복제한 후 누락된 시스템/참조를 보완.
    /// GameManager, UIManager, Player, Territory, Craft, Time/Weather 등
    /// 모든 시스템이 통합된 상태에서 런타임 오류 검증.
    /// </summary>
    public class TestAllInOneSetup : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool _initializeMissingSystems = true;
        [SerializeField] private bool _logSystemStatus = true;

        private void Awake()
        {
            if (!_initializeMissingSystems) return;

            EnsureEventSystem();
            EnsureGameManager();
            EnsureUIManager();
            EnsureCanvas();
            EnsureUIWindows();
            EnsurePlayerSystems();
            EnsureTerritoryManager();
            EnsureTimeWeatherSystems();
            EnsureCraftSystems();
            VerifyIntegrity();
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[TestAllInOneSetup] ✅ EventSystem 생성");
            }
        }

        private void EnsureGameManager()
        {
            if (GameManager.Instance == null)
            {
                var gmGO = new GameObject("GameManager");
                gmGO.AddComponent<GameManager>();
                gmGO.AddComponent<BuffManager>();
                Debug.Log("[TestAllInOneSetup] ✅ GameManager 생성");
            }
        }

        private void EnsureUIManager()
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

                Debug.Log("[TestAllInOneSetup] ✅ UIManager 생성 + KeyBindings 연결");
            }

            // TooltipWindow 보장 (UI NPE 방지)
            if (TooltipWindow.Instance == null)
            {
                var ttwGO = new GameObject("TooltipWindow");
                ttwGO.AddComponent<TooltipWindow>();
                Debug.Log("[TestAllInOneSetup] ✅ TooltipWindow 생성");
            }
        }

        private void EnsureCanvas()
        {
            if (FindAnyObjectByType<Canvas>() == null)
            {
                var canvasGO = new GameObject("Canvas");
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Debug.Log("[TestAllInOneSetup] ✅ Canvas 생성");
            }
        }

        private void EnsureUIWindows()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            Transform canvasTransform = canvas != null ? canvas.transform : null;

            // MainScene에 UI 윈도우가 이미 있으면 스킵
            var existingWindows = FindObjectsByType<UIWindow>(FindObjectsSortMode.None);
            if (existingWindows.Length > 0)
            {
                Debug.Log($"[TestAllInOneSetup] ✅ UI 윈도우 {existingWindows.Length}개 이미 존재 — 스킵");
                return;
            }

            CreateWindow<InventoryWindow>("InventoryWindow", canvasTransform);
            CreateWindow<QuestWindow>("QuestWindow", canvasTransform);
            CreateWindow<RecipeWindow>("RecipeWindow", canvasTransform);
            CreateWindow<MapWindow>("MapWindow", canvasTransform);
            CreateWindow<LootWindow>("LootWindow", canvasTransform);

            // UIManager 정적 참조 등록
            if (UIManager.Instance != null)
            {
                UIManager.inventoryWindow = FindAnyObjectByType<InventoryWindow>();
                UIManager.recipeWindow = FindAnyObjectByType<RecipeWindow>();
                UIManager.craftingWindow = FindAnyObjectByType<CraftingUI>();
            }

            Debug.Log("[TestAllInOneSetup] ✅ UI 윈도우 5개 생성 완료");
        }

        private void CreateWindow<T>(string name, Transform parent) where T : UIWindow
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            var window = go.AddComponent<T>();
            window.ApplyTheme(Phase33_Themes.CreateInventoryTheme());
        }

        private void EnsurePlayerSystems()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
                player.transform.position = Vector3.zero;
                Debug.Log("[TestAllInOneSetup] ✅ Player 생성");
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

            // PlayerCombat
            if (player.GetComponent<PlayerCombat>() == null)
                player.AddComponent<PlayerCombat>();

            // PlayerHealth
            if (player.GetComponent<PlayerHealth>() == null)
                player.AddComponent<PlayerHealth>();

            // PlayerStats
            if (player.GetComponent<PlayerStats>() == null)
                player.AddComponent<PlayerStats>();

            // PlayerPlaceholder
            if (player.GetComponent<PlayerPlaceholder>() == null)
                player.AddComponent<PlayerPlaceholder>();

            // PlayerInventory
            if (player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();

            Debug.Log("[TestAllInOneSetup] ✅ Player 시스템 설정 완료");
        }

        private void EnsureTerritoryManager()
        {
            if (FindAnyObjectByType<TerritoryManager>() == null)
            {
                var tmGO = new GameObject("TerritoryManager");
                tmGO.AddComponent<TerritoryManager>();
                Debug.Log("[TestAllInOneSetup] ✅ TerritoryManager 생성");
            }
        }

        private void EnsureTimeWeatherSystems()
        {
            // TimeManager
            if (TimeManager.Instance == null)
            {
                var tmGO = new GameObject("TimeManager");
                var tm = tmGO.AddComponent<TimeManager>();
                tm.TimeScale = 60f;
                tm.GameTime = 6f * 3600f;
                Debug.Log("[TestAllInOneSetup] ✅ TimeManager 생성");
            }

            // DayNightCycle
            var tmGameObject = GameObject.Find("TimeManager");
            if (tmGameObject != null && tmGameObject.GetComponent<DayNightCycle>() == null)
            {
                var dnc = tmGameObject.AddComponent<DayNightCycle>();

                // Sun/Moon Light 참조 설정
                var sunField = typeof(DayNightCycle).GetField("_sunLight",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var sun = FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.name.Contains("Sun"));
                if (sunField != null && sun != null)
                    sunField.SetValue(dnc, sun);

                var moonField = typeof(DayNightCycle).GetField("_moonLight",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var moon = GameObject.Find("Moon Light")?.GetComponent<Light>();
                if (moonField != null && moon != null)
                    moonField.SetValue(dnc, moon);

                Debug.Log("[TestAllInOneSetup] ✅ DayNightCycle 부착");
            }

            // WeatherManager
            if (WeatherManager.Instance == null)
            {
                Debug.LogWarning("[TestAllInOneSetup] WeatherManager.Instance가 null — lazy 생성 실패");
            }
            else
            {
                var dirLight = FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .FirstOrDefault(l => l.type == LightType.Directional);
                if (dirLight != null)
                    WeatherManager.Instance.SetLightReference(dirLight);
                WeatherManager.Instance.SetWeather(WeatherManager.WeatherType.Clear);
                WeatherManager.Instance.SetTimer(9999f);
            }

            // WeatherParticleController
            if (FindAnyObjectByType<WeatherParticleController>() == null)
            {
                var particleGO = new GameObject("WeatherParticleController");
                particleGO.AddComponent<WeatherParticleController>();
                Debug.Log("[TestAllInOneSetup] ✅ WeatherParticleController 생성");
            }
        }

        private void EnsureCraftSystems()
        {
            // CraftPresetManager
            if (CraftPresetManager.Instance == null)
            {
                var cpmGO = new GameObject("CraftPresetManager");
                cpmGO.AddComponent<CraftPresetManager>();
                Debug.Log("[TestAllInOneSetup] ✅ CraftPresetManager 생성");
            }
        }

        private void VerifyIntegrity()
        {
            if (!_logSystemStatus) return;

            Debug.Log("=== [TestAllInOneSetup] 시스템 통합 상태 ===");
            Debug.Log($"  GameManager:    {(GameManager.Instance != null ? "✅" : "❌")}");
            Debug.Log($"  UIManager:      {(UIManager.Instance != null ? "✅" : "❌")}");
            Debug.Log($"  TimeManager:    {(TimeManager.Instance != null ? "✅" : "❌")}");
            Debug.Log($"  WeatherManager: {(WeatherManager.Instance != null ? "✅" : "❌")}");
            Debug.Log($"  TerritoryMgr:   {(FindAnyObjectByType<TerritoryManager>() != null ? "✅" : "❌")}");
            Debug.Log($"  CraftPresetMgr: {(CraftPresetManager.Instance != null ? "✅" : "❌")}");
            Debug.Log($"  Player:         {(GameObject.FindGameObjectWithTag("Player") != null ? "✅" : "❌")}");
            Debug.Log($"  PlayerInventory:{(PlayerInventory.Instance != null ? "✅" : "❌")}");
            Debug.Log($"  Canvas:         {(FindAnyObjectByType<Canvas>() != null ? "✅" : "❌")}");
            Debug.Log($"  EventSystem:    {(FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null ? "✅" : "❌")}");
            Debug.Log("=== [TestAllInOneSetup] 시스템 통합 상태 끝 ===");
        }
    }
}