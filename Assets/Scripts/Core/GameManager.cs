using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Core
{
    /// <summary>
    /// Base game manager — entry point for game initialization.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>C20-01: Current game difficulty level (0=Easy, 1=Normal, 2=Hard).</summary>
        public static int CurrentDifficulty { get; set; } = 0;

        [SerializeField] private bool _debugMode = false;

        private void Awake()
        {
            Application.targetFrameRate = 60;
#if UNITY_EDITOR
            // Editor 전용 디버그/테스트 컴포넌트 (Play 모드에서도 데이터 검증용)
            gameObject.AddComponent<HerbTester>();
            gameObject.AddComponent<ComboTester>();
            gameObject.AddComponent<CookingTester>();
            gameObject.AddComponent<DishTester>();
#endif
            gameObject.AddComponent<BuffManager>();
        }

        private void Start()
        {
            if (_debugMode)
                Debug.Log("[GameManager] Game initialized in debug mode");
            else
                Debug.Log("[GameManager] Game initialized");

            InitializeSystems();
            EnsureTerritoryManager();
        }

        private void EnsureTerritoryManager()
        {
            // Use reflection to access Systems types (avoid circular reference)
            var tmType = System.Type.GetType("ProjectName.Systems.TerritoryManager");
            if (tmType == null)
                tmType = FindTypeInAssemblies("TerritoryManager");

            var instanceField = tmType?.GetField("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var instance = instanceField?.GetValue(null);

            if (instance == null && tmType != null)
            {
                var go = new GameObject("TerritoryManager");
                go.AddComponent(tmType);

                var tbType = System.Type.GetType("ProjectName.Systems.TerritoryBuilder");
                if (tbType == null)
                    tbType = FindTypeInAssemblies("TerritoryBuilder");
                if (tbType != null)
                    go.AddComponent(tbType);

                Debug.Log("[GameManager] TerritoryManager 자동 생성됨");
            }
        }

        private static System.Type FindTypeInAssemblies(string typeName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(typeName);
                if (type != null) return type;
                type = asm.GetType("ProjectName.Systems." + typeName);
                if (type != null) return type;
            }
            return null;
        }

        private void InitializeSystems()
        {
            // ========================================================
            // 🔴 REQUIRED SYSTEMS (no DontDestroyOnLoad, no lazy-create)
            // ========================================================

            // 1. EventSystem — Unity UI input handling (StandaloneInputModule + EventSystem)
            CreateEventSystemIfMissing();

            // 2. TimeManager + DayNightCycle — day/night cycle with moon light
            CreateTimeAndDayNightCycleIfMissing();

            // 3. GuardManager — soldier management
            CreateSystemIfMissing("GuardManager");

            // 4. TerritoryBattleManager — territory battle state
            CreateSystemIfMissing("TerritoryBattleManager");

            // 5. TutorialGuideSystem — tutorial guides
            CreateSystemIfMissing("TutorialGuideSystem");

            // ========================================================
            // 🟠 DONT DESTROY ON LOAD SYSTEMS (first-time create)
            // ========================================================

            // 6. SoundManager (Core)
            CreateSystemIfMissing("SoundManager");

            // 7. SaveManager
            CreateSystemIfMissing("SaveManager");

            // 8. AchievementSystem (UI)
            CreateSystemIfMissing("AchievementSystem");

            // 9. ControllerSupport
            CreateSystemIfMissing("ControllerSupport");

            // 10. MercenaryManager
            CreateSystemIfMissing("MercenaryManager");

            // 11. SettingsMenuUI (UI)
            CreateSystemIfMissing("SettingsMenuUI");

            // 12. EscMenuUI (UI)
            CreateSystemIfMissing("EscMenuUI");

            // 13. DeathScreenUI (UI)
            CreateSystemIfMissing("DeathScreenUI");

            // 14. MinimapUI (UI) — 우측 상단 원형 미니맵
            CreateSystemIfMissing("MinimapUI");

            // 15. LoadingScreenUI + LoadingManager (same GameObject)
            CreateLoadingScreenSystemIfMissing();

            // 16. PlayerStats — player experience, gold, and stats singleton
            CreateSystemIfMissing("PlayerStats");

            // 17. UIManager — UI window management (singleton with FindOrCreate fallback)
            CreateSystemIfMissing("UIManager");

            // 18. RevengeListIntegration — 복수명부 이벤트 구독 초기화 (정적 클래스, reflection)
            InitializeRevengeListIntegration();
        }

        /// <summary>
        /// RevengeListIntegration.Initialize()를 리플렉션으로 호출 (Core→Systems 크로스 어셈블리)
        /// </summary>
        private static void InitializeRevengeListIntegration()
        {
            var type = FindTypeInAssemblies("RevengeListIntegration");
            if (type == null)
            {
                Debug.LogWarning("[GameManager] RevengeListIntegration 타입을 찾을 수 없습니다.");
                return;
            }

            var method = type.GetMethod("Initialize",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Debug.LogWarning("[GameManager] RevengeListIntegration.Initialize() 메서드를 찾을 수 없습니다.");
                return;
            }

            method.Invoke(null, null);
            Debug.Log("[GameManager] RevengeListIntegration 초기화 완료 (reflection)");
        }

        // ================================================================
        // EventSystem
        // ================================================================

        private void CreateEventSystemIfMissing()
        {
            System.Type esType = System.Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
            if (esType == null)
            {
                Debug.LogWarning("[GameManager] EventSystem type not found. Is UnityEngine.UI module present?");
                return;
            }

            System.Type oldModuleType = System.Type.GetType("UnityEngine.EventSystems.StandaloneInputModule, UnityEngine.UI");
            System.Type newModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            var existing = FindFirstObjectByType(esType);
            if (existing != null)
            {
                // Replace StandaloneInputModule with InputSystemUIInputModule if present
                if (oldModuleType != null && newModuleType != null)
                {
                    Component oldModule = null;
                    oldModule = (existing as Component).GetComponent(oldModuleType);
                    if (oldModule != null)
                    {
                        Object.Destroy(oldModule);
                        (existing as Component).gameObject.AddComponent(newModuleType);
                        Debug.Log("[GameManager] StandaloneInputModule → InputSystemUIInputModule 교체");
                    }
                }
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent(esType);

            if (newModuleType != null)
                go.AddComponent(newModuleType);

            Debug.Log("[GameManager] EventSystem + InputSystemUIInputModule 자동 생성됨");
        }

        // ================================================================
        // TimeManager + DayNightCycle + Moon Light
        // ================================================================

        private void CreateTimeAndDayNightCycleIfMissing()
        {
            var tmType = FindTypeInAssemblies("TimeManager");
            if (tmType == null) return;

            var existingTM = FindFirstObjectByType(tmType);
            GameObject go;

            if (existingTM == null)
            {
                go = new GameObject("TimeAndDayNight");
                go.AddComponent(tmType);
                Debug.Log("[GameManager] TimeManager 자동 생성됨");
            }
            else
            {
                go = ((MonoBehaviour)existingTM).gameObject;
            }

            // Create Moon Light BEFORE adding DayNightCycle (so it's ready when DayNightCycle.Start() runs)
            var moonLight = CreateMoonLightIfMissing();

            // DayNightCycle on the same GameObject
            var dncType = FindTypeInAssemblies("DayNightCycle");
            if (dncType != null)
            {
                var existingDNC = go.GetComponent(dncType);
                if (existingDNC == null)
                {
                    var dnc = go.AddComponent(dncType);

                    // Assign moon light via reflection
                    if (moonLight != null)
                    {
                        var moonField = dncType.GetField("_moonLight",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        moonField?.SetValue(dnc, moonLight);
                    }

                    Debug.Log("[GameManager] DayNightCycle 자동 생성됨");
                }
            }
        }

        private Light CreateMoonLightIfMissing()
        {
            var existingMoon = GameObject.Find("MoonLight");
            if (existingMoon != null)
                return existingMoon.GetComponent<Light>();

            var moonGO = new GameObject("MoonLight");
            var moonLight = moonGO.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.6f, 0.7f, 1.0f); // cool blue-white
            moonLight.intensity = 0.2f;
            moonLight.shadowStrength = 0.3f;

            Debug.Log("[GameManager] Moon Light 자동 생성됨");
            return moonLight;
        }

        // ================================================================
        // Generic system creation
        // ================================================================

        /// <summary>
        /// Searches for a type by short name across Core, Systems, and UI namespaces.
        /// Creates a new GameObject with the component if not already present in the scene.
        /// </summary>
        private void CreateSystemIfMissing(string shortName)
        {
            var type = FindTypeAnyNamespace(shortName);
            if (type == null)
            {
                Debug.LogWarning($"[GameManager] {shortName} type not found in any namespace.");
                return;
            }

            var existing = FindFirstObjectByType(type);
            if (existing != null) return;

            var go = new GameObject(shortName);
            go.AddComponent(type);
            Debug.Log($"[GameManager] {shortName} 자동 생성됨");
        }

        // ================================================================
        // LoadingScreen (LoadingManager + LoadingScreenUI on same GO)
        // ================================================================

        private void CreateLoadingScreenSystemIfMissing()
        {
            var lmType = FindTypeInAssemblies("LoadingManager");
            var lsType = FindTypeInAssemblies("LoadingScreenUI");

            if (lmType == null || lsType == null) return;

            var existingLM = FindFirstObjectByType(lmType);
            if (existingLM != null) return;

            var go = new GameObject("LoadingScreen");
            go.AddComponent(lmType);
            go.AddComponent(lsType);
            Debug.Log("[GameManager] LoadingScreen (LoadingManager + LoadingScreenUI) 자동 생성됨");
        }

        /// <summary>
        /// Searches for a type by short name across Core, Systems, and UI namespaces
        /// in all loaded assemblies.
        /// </summary>
        private static System.Type FindTypeAnyNamespace(string typeName)
        {
            foreach (var ns in new[] { "", "ProjectName.Systems.", "ProjectName.Core.", "ProjectName.UI." })
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = asm.GetType(ns + typeName);
                    if (type != null) return type;
                }
            }
            return null;
        }
    }
}