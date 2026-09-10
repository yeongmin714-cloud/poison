using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;
using ProjectName.Core.Data;
using System;
using System.Reflection;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_09_AllInOne 씬 전용: 모든 시스템 종합 검증.
    /// GameManager + 모든 매니저 + Player + Combat + Territory + Craft + Time/Weather + UI + Dracula + GasBomb
    /// 모든 시스템을 한 번에 로드하여 상호작용 테스트.
    /// UI 타입은 리플렉션으로 접근 (어셈블리 순환 참조 방지).
    /// </summary>
    public class TestAllInOneSetup : MonoBehaviour
    {
        [Header("Test Modules")]
        [SerializeField] private bool _includeCombat = true;
        [SerializeField] private bool _includeTerritory = true;
        [SerializeField] private bool _includeCraft = true;
        [SerializeField] private bool _includeTimeWeather = true;
        [SerializeField] private bool _includeUI = true;
        [SerializeField] private bool _includeDracula = true;
        [SerializeField] private bool _includeGasBomb = true;
        [SerializeField] private bool _includeProceduralAnim = true;

        [Header("Player Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _runSpeed = 10f;
        [SerializeField] private float _jumpHeight = 2f;

        [Header("Camera Settings")]
        [SerializeField] private float _orbitRadius = 20f;
        [SerializeField] private float _defaultPitch = 45f;

        [Header("World Settings")]
        [SerializeField] private int _monsterCount = 5;
        [SerializeField] private int _guardCount = 3;
        [SerializeField] private int _dummyCount = 3;

        [Header("Monster vs Guard Combat Scenario")]
        [Tooltip("게임 시작 후 이 시간(초)에 몬스터-병사를 강제 '전투 상태'로 전환 (어그로/MonsterAggroSystem 활성화 검증)")]
        [SerializeField] private float _combatStartDelaySeconds = 15f;
        [Tooltip("몬스터 스폰 원 반경(m) — 기존 15m 원형 배치 유지")]
        [SerializeField] private float _monsterSpawnRadius = 15f;
        [Tooltip("병사 배치 반경(m) — 몬스터 원(15m) 안 10~12m 구간. 몬스터 detection(10~18m)/MonsterAggroSystem.AGGRO_RANGE(10m) 내 진입")]
        [SerializeField] private float _guardSpawnRadius = 11f;

        // [전투 시나리오] 런타임 상태
        private readonly System.Collections.Generic.List<Vector3> _monsterSpawnPositions =
            new System.Collections.Generic.List<Vector3>(); // SetupCombat에서 기록, SetupTerritory가 같은 각도 정렬에 사용
        private int _knownLootBasketCount;                  // LootBasket 신규 생성 감지용(전리품 드랍 검증)
        private bool _guardDeathHooked;                     // GuardPlaceholder.OnAnyGuardDied 구독 여부

        // 리플렉션용 캐시
        private Type _uiManagerType;
        private Type _uiWindowType;
        private Type _keyBindingsType;
        private Type _phase33ThemesType;
        private Type _inventoryWindowType;
        private Type _questWindowType;
        private Type _recipeWindowType;
        private Type _mapWindowType;
        private Type _lootWindowType;
        private Type _hudType;          // 플레이어 HUD(하트 시스템, IMGUI 기반) — ProjectName.UI.HUD

        private void Awake()
        {
            Debug.Log("[TestAllInOneSetup] 🚀 전체 시스템 종합 테스트 시작...");

            CacheUIReflectionTypes();
            EnsureEventSystem();
            EnsureGameManager();
            SetupTimeAndWeather();
            SetupTerritoryAndNation();
            SetupPlayer();
            SetupCamera();
            SetupGround();
            SetupLight();
            SetupSkybox();
            SetupUI();

            if (_includeCombat)
                SetupCombat();
            if (_includeTerritory)
                SetupTerritory();
            if (_includeCraft)
                SetupCraft();
            if (_includeDracula)
                SetupDracula();
            if (_includeGasBomb)
                SetupGasBomb();
            if (_includeProceduralAnim)
                EnsureProceduralAnimation();

            // [전투 시나리오] 몬스터(AnimalAI) ↔ 병사(GuardPlaceholder) 전투 상태 유도 + 전리품 드랍 검증.
            // 몬스터가 병사를 공격하는 실제 로직은 AnimalAI 측에 병렬로 추가될 예정이며,
            // 본 셋업은 "전투 가능한 위치 배치 + 15초 후 강제 어그로 + 드랍/LootBasket 검증 로그"를 담당한다.
            if (_includeCombat)
                StartCoroutine(CombatScenarioRoutine());

            Debug.Log("[TestAllInOneSetup] ✅ Test_09_AllInOne 전체 시스템 설정 완료!");
        }

        private void CacheUIReflectionTypes()
        {
            var uiAssembly = Assembly.Load("ProjectName.UI");
            if (uiAssembly == null)
            {
                Debug.LogWarning("[TestAllInOneSetup] ProjectName.UI 어셈블리를 찾을 수 없습니다. UI 테스트는 건너뜁니다.");
                _includeUI = false;
                return;
            }

            _uiManagerType = uiAssembly.GetType("ProjectName.UI.Core.UIManager");
            _uiWindowType = uiAssembly.GetType("ProjectName.UI.UIWindow");
            _keyBindingsType = uiAssembly.GetType("ProjectName.UI.KeyBindings");
            _phase33ThemesType = uiAssembly.GetType("ProjectName.UI.Themes.Phase33_Themes");
            _inventoryWindowType = uiAssembly.GetType("ProjectName.UI.InventoryWindow");
            _questWindowType = uiAssembly.GetType("ProjectName.UI.QuestWindow");
            _recipeWindowType = uiAssembly.GetType("ProjectName.UI.RecipeWindow");
            _mapWindowType = uiAssembly.GetType("ProjectName.UI.MapWindow");
            _lootWindowType = uiAssembly.GetType("ProjectName.UI.LootWindow");
            _hudType = uiAssembly.GetType("ProjectName.UI.HUD");

            if (_uiManagerType == null || _uiWindowType == null)
            {
                Debug.LogWarning("[TestAllInOneSetup] UI 핵심 타입을 찾을 수 없습니다. UI 테스트는 건너뜁니다.");
                _includeUI = false;
            }
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
                gmGO.AddComponent<MonsterLevelManager>();
                gmGO.AddComponent<MonsterAggroSystem>();
                gmGO.AddComponent<MonsterSkillSystem>();
                Debug.Log("[TestAllInOneSetup] ✅ GameManager + 핵심 시스템 생성");
            }
        }

        private void SetupTimeAndWeather()
        {
            if (!_includeTimeWeather) return;

            // TimeManager
            if (TimeManager.Instance == null)
            {
                var tmGO = new GameObject("TimeManager");
                var tm = tmGO.AddComponent<TimeManager>();
                tm.TimeScale = 60f;
                tm.GameTime = 43200f; // 정오
                Debug.Log("[TestAllInOneSetup] ✅ TimeManager 생성");
            }

            // DayNightCycle
            var timeManagerGO = GameObject.Find("TimeManager");
            if (timeManagerGO != null && timeManagerGO.GetComponent<DayNightCycle>() == null)
            {
                var dnc = timeManagerGO.AddComponent<DayNightCycle>();
                var sun = GameObject.Find("Sun Light")?.GetComponent<Light>();
                var moon = GameObject.Find("Moon Light")?.GetComponent<Light>();
                if (sun != null)
                {
                    var sunField = typeof(DayNightCycle).GetField("_sunLight",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    sunField?.SetValue(dnc, sun);
                }
                if (moon != null)
                {
                    var moonField = typeof(DayNightCycle).GetField("_moonLight",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    moonField?.SetValue(dnc, moon);
                }
                Debug.Log("[TestAllInOneSetup] ✅ DayNightCycle 부착 + Light 참조 연결");
            }

            // WeatherManager
            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.SetWeather(WeatherManager.WeatherType.Clear);
                WeatherManager.Instance.SetTimer(9999f);
                Debug.Log("[TestAllInOneSetup] ✅ WeatherManager 설정 (Clear)");
            }

            // WeatherParticleController
            if (FindAnyObjectByType<WeatherParticleController>() == null)
            {
                var wpcGO = new GameObject("WeatherParticleController");
                wpcGO.AddComponent<WeatherParticleController>();
                Debug.Log("[TestAllInOneSetup] ✅ WeatherParticleController 생성");
            }

            // WindZone
            if (FindAnyObjectByType<WindZone>() == null)
            {
                var windGO = new GameObject("WindZone");
                var wind = windGO.AddComponent<WindZone>();
                wind.windMain = 0f;
                wind.windTurbulence = 0.5f;
                wind.mode = WindZoneMode.Directional;
                Debug.Log("[TestAllInOneSetup] ✅ WindZone 생성");
            }
        }

        private void SetupTerritoryAndNation()
        {
            if (!_includeTerritory && !_includeDracula) return;

            // NationTerrainController
            if (FindAnyObjectByType<NationTerrainController>() == null)
            {
                var ntcGO = new GameObject("NationTerrainController");
                ntcGO.AddComponent<NationTerrainController>();
                Debug.Log("[TestAllInOneSetup] ✅ NationTerrainController 생성");
            }

            // TerritoryManager
            if (TerritoryManager.Instance == null)
            {
                var tmGO = new GameObject("TerritoryManager");
                tmGO.AddComponent<TerritoryManager>();
                Debug.Log("[TestAllInOneSetup] ✅ TerritoryManager 생성");
            }

            // TerritoryBuilder
            if (FindAnyObjectByType<TerritoryBuilder>() == null)
            {
                var tbGO = new GameObject("TerritoryBuilder");
                tbGO.AddComponent<TerritoryBuilder>();
                Debug.Log("[TestAllInOneSetup] ✅ TerritoryBuilder 생성");
            }

            // TownBuilder is a static class - no component needed
            // if (FindAnyObjectByType<TownBuilder>() == null)
            // {
            //     var twnGO = new GameObject("TownBuilder");
            //     twnGO.AddComponent<TownBuilder>();
            //     Debug.Log("[TestAllInOneSetup] ✅ TownBuilder 생성");
            // }

            // GuardManager
            if (GuardManager.Instance == null)
            {
                var gmGO = new GameObject("GuardManager");
                gmGO.AddComponent<GuardManager>();
                Debug.Log("[TestAllInOneSetup] ✅ GuardManager 생성");
            }

            // TerritoryCaptureSystem is a static class - no component needed
            // if (FindAnyObjectByType<TerritoryCaptureSystem>() == null)
            // {
            //     var tcsGO = new GameObject("TerritoryCaptureSystem");
            //     tcsGO.AddComponent<TerritoryCaptureSystem>();
            //     Debug.Log("[TestAllInOneSetup] ✅ TerritoryCaptureSystem 생성");
            // }

            // TerritoryWarManager
            if (TerritoryWarManager.Instance == null)
            {
                var twmGO = new GameObject("TerritoryWarManager");
                twmGO.AddComponent<TerritoryWarManager>();
                Debug.Log("[TestAllInOneSetup] ✅ TerritoryWarManager 생성");
            }

            // DraculaTerritoryController
            if (_includeDracula && DraculaTerritoryController.Instance == null)
            {
                var dtcGO = new GameObject("DraculaTerritoryController");
                dtcGO.AddComponent<DraculaTerritoryController>();
                Debug.Log("[TestAllInOneSetup] ✅ DraculaTerritoryController 생성");
            }
        }

        private void SetupPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
            }

            // Rigidbody
            if (player.GetComponent<Rigidbody>() == null)
            {
                var rb = player.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // CharacterController
            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
            }

            // PlayerInput
            if (player.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
            {
                var pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
            }

            // Animator
            if (player.GetComponent<Animator>() == null)
                player.AddComponent<Animator>();

            // PlayerMovement
            var pmType = typeof(ProjectName.Systems.PlayerMovement);
            if (player.GetComponent(pmType) == null)
                player.AddComponent(pmType);

            // PlayerCombat
            if (player.GetComponent<PlayerCombat>() == null)
                player.AddComponent<PlayerCombat>();

            // PlayerHealth
            if (player.GetComponent<PlayerHealth>() == null)
                player.AddComponent<PlayerHealth>();

            // PlayerStats
            if (player.GetComponent<PlayerStats>() == null)
                player.AddComponent<PlayerStats>();

            // PlayerInventory
            if (player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();

            // Procedural Animation
            if (player.GetComponent<ProjectName.Systems.Animation.Procedural.Bones.ProceduralBoneMap>() == null)
                player.AddComponent<ProjectName.Systems.Animation.Procedural.Bones.ProceduralBoneMap>();
            if (player.GetComponent<ProjectName.Systems.Animation.Procedural.ProceduralAnimStateMachine>() == null)
                player.AddComponent<ProjectName.Systems.Animation.Procedural.ProceduralAnimStateMachine>();
            if (player.GetComponent<ProjectName.Systems.Animation.Procedural.ProceduralAnimationController>() == null)
                player.AddComponent<ProjectName.Systems.Animation.Procedural.ProceduralAnimationController>();

            // PlayerPlaceholder
            if (player.GetComponent<PlayerPlaceholder>() == null)
                player.AddComponent<PlayerPlaceholder>();

            // Damageable
            if (player.GetComponent<Damageable>() == null)
                player.AddComponent<Damageable>();

            // GasSprayerController
            if (_includeGasBomb && player.GetComponent<GasSprayerController>() == null)
                player.AddComponent<GasSprayerController>();

            // BombThrower
            if (_includeGasBomb && player.GetComponent<BombThrower>() == null)
                player.AddComponent<BombThrower>();

            player.transform.position = Vector3.zero;
            Debug.Log("[TestAllInOneSetup] ✅ Player 설정 완료 (전체 시스템 포함)");
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

            Debug.Log("[TestAllInOneSetup] ✅ 카메라 설정 완료");
        }

        private void SetupGround()
        {
            if (GameObject.Find("Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = new Vector3(0, -0.5f, 0);
                ground.transform.localScale = Vector3.one * 100f;

                var renderer = ground.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
                    mat.SetFloat("_Smoothness", 0f);
                    renderer.material = mat;
                }
                Debug.Log("[TestAllInOneSetup] ✅ Ground 생성 (100x100)");
            }
        }

        private void SetupLight()
        {
            // Sun Light
            var sun = GameObject.Find("Sun Light");
            if (sun == null)
            {
                var lightGO = new GameObject("Sun Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.95f, 0.8f);
                light.intensity = 1.2f;
                light.shadowStrength = 1f;
                light.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            }

            // Moon Light
            var moon = GameObject.Find("Moon Light");
            if (moon == null)
            {
                var moonGO = new GameObject("Moon Light");
                var moonLight = moonGO.AddComponent<Light>();
                moonLight.type = LightType.Directional;
                moonLight.color = new Color(0.6f, 0.7f, 1.0f);
                moonLight.intensity = 0.2f;
                moonLight.shadowStrength = 0.3f;
                moonLight.transform.rotation = Quaternion.Euler(230f, 210f, 0f);
                moonLight.enabled = false;
            }

            Debug.Log("[TestAllInOneSetup] ✅ Sun/Moon Light 생성");
        }

        private void SetupSkybox()
        {
            if (RenderSettings.skybox == null)
            {
                var skyboxMat = new Material(Shader.Find("Skybox/Procedural"));
                if (skyboxMat != null && skyboxMat.shader != null)
                {
                    skyboxMat.name = "TestSkybox_AllInOne";
                    skyboxMat.SetColor("_SkyTint", new Color(0.4f, 0.6f, 0.9f));
                    skyboxMat.SetColor("_GroundColor", new Color(0.5f, 0.5f, 0.5f));
                    skyboxMat.SetFloat("_Exposure", 1.0f);
                    skyboxMat.SetFloat("_AtmosphereThickness", 0.8f);
                    skyboxMat.SetFloat("_SunSize", 0.04f);
                    RenderSettings.skybox = skyboxMat;
                    Debug.Log("[TestAllInOneSetup] ✅ Procedural Skybox 생성");
                }
            }
        }

        private void SetupUI()
        {
            if (!_includeUI || _uiManagerType == null) return;

            // UIManager
            if (_uiManagerType.GetProperty("Instance")?.GetValue(null) == null)
            {
                var uiMgrGO = new GameObject("UIManager");
                var uiMgr = uiMgrGO.AddComponent(_uiManagerType);
                var kb = ScriptableObject.CreateInstance(_keyBindingsType);
                _uiManagerType.GetMethod("SetKeyBindings")?.Invoke(uiMgr, new object[] { kb });
                Debug.Log("[TestAllInOneSetup] ✅ UIManager 생성");
            }

            // Canvas
            if (FindAnyObjectByType<Canvas>() == null)
            {
                var canvasGO = new GameObject("Canvas");
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
                Debug.Log("[TestAllInOneSetup] ✅ Canvas 생성");
            }

            // Key Windows
            var foundCanvas = FindAnyObjectByType<Canvas>();
            Transform canvasTransform = foundCanvas != null ? foundCanvas.transform : null;

            CreateUIWindow(_inventoryWindowType, "InventoryWindow", canvasTransform);
            CreateUIWindow(_questWindowType, "QuestWindow", canvasTransform);
            CreateUIWindow(_recipeWindowType, "RecipeWindow", canvasTransform);
            CreateUIWindow(_mapWindowType, "MapWindow", canvasTransform);
            CreateUIWindow(_lootWindowType, "LootWindow", canvasTransform);

            // 플레이어 HUD(하트 시스템) 자동 부착 — IMGUI(OnGUI) 기반이라 Canvas 불필요
            EnsurePlayerHUD();

            Debug.Log("[TestAllInOneSetup] ✅ UI 시스템 + 주요 윈도우 생성 완료");
        }

        /// <summary>
        /// 플레이어 HUD(하트 시스템, IMGUI 기반) 자동 부착.
        /// - OnGUI 기반이므로 Canvas 없이 독립 GameObject에 AddComponent만으로 즉시 동작.
        /// - 씬에 이미 활성화된 HUD가 있으면 중복 생성하지 않는다.
        /// </summary>
        private void EnsurePlayerHUD()
        {
            if (_hudType == null)
            {
                Debug.LogWarning("[TestAllInOneSetup] ProjectName.UI.HUD 타입을 찾을 수 없습니다. HUD 생성을 건너뜁니다.");
                return;
            }

            // Unity 6 API: FindObjectsByType(Type, FindObjectsInactive, FindObjectsSortMode) 정적 오버로드 사용 (FindObjectOfType 폐지)
            var existingHuds = Object.FindObjectsByType(_hudType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (existingHuds != null && existingHuds.Length > 0)
            {
                Debug.Log("[TestAllInOneSetup] ℹ️ 이미 활성 HUD가 존재하여 HUD 생성을 건너뜁니다.");
                return;
            }

            var hudGO = new GameObject("HUD");
            hudGO.AddComponent(_hudType);
            Debug.Log("[TestAllInOneSetup] ✅ 플레이어 HUD(하트) 부착");
        }

        private void CreateUIWindow(Type windowType, string name, Transform parent)
        {
            if (windowType == null || _uiWindowType == null) return;

            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            var window = go.AddComponent(windowType);
            
            // ApplyTheme 호출
            var theme = GetDefaultThemeForWindow(name);
            if (theme != null)
            {
                var applyThemeMethod = _uiWindowType.GetMethod("ApplyTheme");
                applyThemeMethod?.Invoke(window, new object[] { theme });
            }
            
            Debug.Log($"[TestAllInOneSetup] ✅ {name} 생성됨");
        }

        private object GetDefaultThemeForWindow(string windowName)
        {
            if (_phase33ThemesType == null) return null;

            return windowName switch
            {
                "InventoryWindow" => _phase33ThemesType.GetMethod("CreateInventoryTheme")?.Invoke(null, null),
                "QuestWindow" => _phase33ThemesType.GetMethod("CreateQuestTheme")?.Invoke(null, null),
                "RecipeWindow" => _phase33ThemesType.GetMethod("CreateRecipeTheme")?.Invoke(null, null),
                "MapWindow" => _phase33ThemesType.GetMethod("CreateMedievalMapTheme")?.Invoke(null, null),
                "LootWindow" => _phase33ThemesType.GetMethod("CreateMedievalShopTheme")?.Invoke(null, null),
                _ => _phase33ThemesType.GetMethod("CreateInventoryTheme")?.Invoke(null, null)
            };
        }

        private void SetupCombat()
        {
            var monsters = new[]
            {
                ("wolf", MonsterTier.Beginner),
                ("boar", MonsterTier.Beginner),
                ("deer", MonsterTier.Beginner),
                ("slime", MonsterTier.Intermediate),
                ("ogre", MonsterTier.Advanced)
            };

            // [전투 시나리오] 몬스터는 기존대로 반경 15m 원형 배치(각도: i/monsters.Length*360°)를 유지한다.
            // 스폰 위치를 기록해두면 SetupTerritory가 같은 각도의 반경 11m 지점에 병사를 배치하여
            // "몬스터 ↔ 병사"가 같은 방사선상에서 약 4m 거리로 마주 보게 된다.
            // → Beginner 몬스터 detection(10m), MonsterAggroSystem.AGGRO_RANGE(10m) 내에 병사가 들어와 자연 어그로 유발.
            _monsterSpawnPositions.Clear();
            for (int i = 0; i < Mathf.Min(_monsterCount, monsters.Length); i++)
            {
                var (monsterId, tier) = monsters[i];
                float angle = (i / (float)monsters.Length) * 360f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * _monsterSpawnRadius, 0f, Mathf.Sin(angle) * _monsterSpawnRadius);

                _monsterSpawnPositions.Add(pos);
                SpawnMonster(monsterId, pos, tier);
            }

            Debug.Log($"[TestAllInOneSetup] ✅ 전투 테스트 몬스터 {_monsterCount}마리 생성 (반경 {_monsterSpawnRadius:0}m 원형 — 병사와 대치 배치)");
        }

        private void SpawnMonster(string monsterId, Vector3 position, MonsterTier tier)
        {
            GameObject go = null;
            string modelPath = GetMonsterModelPath(monsterId);
            if (!string.IsNullOrEmpty(modelPath))
            {
                var modelPrefab = Resources.Load<GameObject>($"Models/UserProvided/{modelPath}");
                if (modelPrefab != null)
                    go = Instantiate(modelPrefab, position, Quaternion.identity);
            }

            if (go == null)
                go = CreatePrimitiveMonster(monsterId, position, tier);

            go.name = $"Monster_{monsterId}";
            go.tag = "Monster";

            // AnimalAI
            var ai = go.GetComponent<AnimalAI>();
            if (ai == null)
                ai = go.AddComponent<AnimalAI>();
            ai.SetMonsterId(monsterId);

            // Collider
            if (go.GetComponent<Collider>() == null)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(1, 1, 1);
            }

            // Rigidbody
            if (go.GetComponent<Rigidbody>() == null)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 1f;
            }

            // HitReaction
            if (go.GetComponent<HitReaction>() == null)
                go.AddComponent<HitReaction>();

            // 4족 몬스터 처리
            if (IsQuadruped(monsterId))
            {
                if (go.GetComponent<QuadrupedProceduralAnimation>() == null)
                {
                    Debug.Log($"[TestAllInOneSetup] ⚠️ {monsterId}: QuadrupedProceduralAnimation 미탑재");
                }
            }
        }

        private bool IsQuadruped(string monsterId)
        {
            return monsterId switch { "wolf" => true, "boar" => true, "deer" => true, _ => false };
        }

        private string GetMonsterModelPath(string monsterId)
        {
            return monsterId switch
            {
                "rabbit" => "Rabbit_Rigged",
                "wolf" => "Wolf_Rigged",
                "boar" => "Boar_Rigged",
                "deer" => "Deer_Rigged",
                "slime" => "Slime_Rigged",
                "ogre" => "Swamp_Ogre_Rigged",
                _ => null
            };
        }

        private GameObject CreatePrimitiveMonster(string monsterId, Vector3 position, MonsterTier tier)
        {
            Color color = tier switch
            {
                MonsterTier.Beginner => Color.green,
                MonsterTier.Intermediate => Color.yellow,
                MonsterTier.Advanced => Color.red,
                _ => Color.white
            };

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = position;
            go.transform.localScale = tier switch
            {
                MonsterTier.Beginner => Vector3.one * 1f,
                MonsterTier.Intermediate => Vector3.one * 1.5f,
                MonsterTier.Advanced => Vector3.one * 2f,
                _ => Vector3.one
            };

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                renderer.material = mat;
            }

            return go;
        }

        private void SetupTerritory()
        {
            if (GuardManager.Instance != null && _guardCount > 0)
            {
                var territoryId = new TerritoryId(NationType.Dracula, 1);

                for (int i = 0; i < _guardCount; i++)
                {
                    // [전투 시나리오] 병사를 기존 8m 원형 → 반경 11m(몬스터 15m 원 안 10~12m 구간)로 이동.
                    // i번째 병사는 i번째 몬스터와 같은 각도(방사선)에 배치하여 몬스터-병사 거리를
                    // 반경 차(15m − 11m = 4m)로 만든다. 이는 Beginner 몬스터의 detection range(10m) 및
                    // MonsterAggroSystem.AGGRO_RANGE(10m) 이내 → 몬스터가 병사를 인지/공격할 수 있다.
                    // (몬스터 미생성 시 Combat 모듈 off 등 → 기존 8m 원형으로 폴백)
                    Vector3 pos;
                    if (_monsterSpawnPositions.Count > 0)
                    {
                        Vector3 dir = _monsterSpawnPositions[i % _monsterSpawnPositions.Count];
                        dir.y = 0f;
                        pos = dir.normalized * _guardSpawnRadius;
                    }
                    else
                    {
                        float angle = (i / (float)_guardCount) * 360f * Mathf.Deg2Rad;
                        pos = new Vector3(Mathf.Cos(angle) * 8f, 0f, Mathf.Sin(angle) * 8f);
                    }

                    var guardGO = new GameObject($"Guard_{i}");
                    guardGO.transform.position = pos;
                    var guard = guardGO.AddComponent<GuardPlaceholder>();
                    // Initialize via reflection if available
                    var initMethod = typeof(GuardPlaceholder).GetMethod("Initialize", new[] { typeof(TerritoryId) });
                    if (initMethod != null)
                        initMethod.Invoke(guard, new object[] { territoryId });

                    var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    visual.name = $"Guard_Visual_{i}";
                    visual.transform.SetParent(guardGO.transform, false);
                    visual.transform.localPosition = new Vector3(0, 1f, 0);
                    visual.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

                    var renderer = visual.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        mat.color = new Color(0.2f, 0.6f, 0.2f, 1f);
                        renderer.material = mat;
                    }

                    var col = visual.GetComponent<Collider>();
                    if (col != null)
                        DestroyImmediate(col);
                }

                Debug.Log($"[TestAllInOneSetup] ✅ 테스트 병사 {_guardCount}명 생성 (반경 {_guardSpawnRadius:0}m — 몬스터와 전투 대치 배치)");
            }
        }

        private void SetupCraft()
        {
            if (CraftPresetManager.Instance != null)
                Debug.Log("[TestAllInOneSetup] ✅ CraftPresetManager 사용 가능");

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
                        addItemMethod.Invoke(inventory, new object[] { "iron_ore", 100 });
                        addItemMethod.Invoke(inventory, new object[] { "wood_log", 100 });
                        addItemMethod.Invoke(inventory, new object[] { "herb_basic", 50 });
                        addItemMethod.Invoke(inventory, new object[] { "leather_scrap", 30 });
                        addItemMethod.Invoke(inventory, new object[] { "magic_crystal", 20 });
                        addItemMethod.Invoke(inventory, new object[] { "gold_coin", 5000 });
                    }
                    Debug.Log("[TestAllInOneSetup] ✅ 크래프트 테스트 재료 추가");
                }
            }
        }

        private void SetupDracula()
        {
            if (FindAnyObjectByType<DraculaLord>() == null)
            {
                var lordGO = new GameObject("DraculaLord");
                lordGO.transform.position = new Vector3(0f, 0f, 20f);
                lordGO.tag = "DraculaLord";

                var lord = lordGO.AddComponent<DraculaLord>();
                lord.SetTerritoryId(new TerritoryId(NationType.Dracula, 1));

                Debug.Log($"[TestAllInOneSetup] ✅ DraculaLord 생성 (HP: {lord.MaxHP}, ATK: {lord.AttackDamage})");
            }

            for (int i = 0; i < 3; i++)
            {
                float angle = (i / 3f) * 360f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * 10f,
                    0f,
                    20f + Mathf.Sin(angle) * 10f
                );

                var guardGO = new GameObject($"SkeletonGuard_{i}");
                guardGO.transform.position = pos;
                guardGO.AddComponent<SkeletonGuardPlaceholder>();

                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = $"SkeletonGuard_Visual_{i}";
                visual.transform.SetParent(guardGO.transform, false);
                visual.transform.localPosition = new Vector3(0, 1f, 0);
                visual.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.7f, 0.1f, 0.1f, 1f);
                    renderer.material = mat;
                }

                var col = visual.GetComponent<Collider>();
                if (col != null)
                    DestroyImmediate(col);
            }

            Debug.Log("[TestAllInOneSetup] ✅ Dracula + Skeleton Guards 생성");
        }

        private void SetupGasBomb()
        {
            if (SpecialEffectsController.Instance == null)
            {
                var secGO = new GameObject("SpecialEffectsController");
                secGO.AddComponent<SpecialEffectsController>();
                Debug.Log("[TestAllInOneSetup] ✅ SpecialEffectsController 생성");
            }

            for (int i = 0; i < _dummyCount; i++)
            {
                float angle = (i / (float)_dummyCount) * 360f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * 5f,
                    0f,
                    Mathf.Sin(angle) * 5f
                );

                var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = $"TestDummy_{i}";
                dummy.transform.position = pos;
                dummy.transform.localScale = Vector3.one * 0.8f;

                var renderer = dummy.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(1f, 0.2f, 0.2f, 1f);
                    renderer.material = mat;
                }

                var rb = dummy.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.useGravity = true;

                dummy.AddComponent<Damageable>();

                Debug.Log($"[TestAllInOneSetup] ✅ TestDummy_{i} 생성: 위치 {pos}");
            }

            var controller = FindAnyObjectByType<GasSprayerController>();
            if (controller != null)
            {
                controller.Equip(GasSprayerGrade.Wood);
                controller.LoadPotion("potion_poison_test", 5);
                Debug.Log("[TestAllInOneSetup] ✅ 가스 분사기 장착됨 (Wood + 물약 5개)");
            }
        }

        private void EnsureProceduralAnimation()
        {
            // ProceduralAnimDebugger is a static class - no component needed
            // if (FindAnyObjectByType<ProjectName.Systems.Animation.Procedural.Debug.ProceduralAnimDebugger>() == null)
            // {
            //     var dbgGO = new GameObject("ProceduralAnimDebugger");
            //     dbgGO.AddComponent<ProjectName.Systems.Animation.Procedural.Debug.ProceduralAnimDebugger>();
            //     Debug.Log("[TestAllInOneSetup] ✅ ProceduralAnimDebugger 생성");
            // }

            // TerrainCache is a plain class, not MonoBehaviour - no component needed
            // if (FindAnyObjectByType<ProjectName.Systems.Animation.Procedural.Locomotion.Ground.TerrainCache>() == null)
            // {
            //     var tcGO = new GameObject("TerrainCache");
            //     tcGO.AddComponent<ProjectName.Systems.Animation.Procedural.Locomotion.Ground.TerrainCache>();
            //     Debug.Log("[TestAllInOneSetup] ✅ TerrainCache 생성");
            // }

            if (FindAnyObjectByType<ProjectName.Systems.Animation.Procedural.LOD.ProceduralLODManager>() == null)
            {
                var lodGO = new GameObject("ProceduralLODSystem");
                lodGO.AddComponent<ProjectName.Systems.Animation.Procedural.LOD.ProceduralLODManager>();
                Debug.Log("[TestAllInOneSetup] ✅ ProceduralLODSystem 생성");
            }

            if (FindAnyObjectByType<ProjectName.Systems.ParentVelocityProvider>() == null)
            {
                var pvGO = new GameObject("ParentVelocityProvider");
                pvGO.AddComponent<ProjectName.Systems.ParentVelocityProvider>();
                Debug.Log("[TestAllInOneSetup] ✅ ParentVelocityProvider 생성");
            }
        }

        // ===================== 전투 시나리오: 몬스터(AnimalAI) ↔ 병사(GuardPlaceholder) =====================

        private void OnDestroy()
        {
            // 정적 이벤트 구독 해제 (씬 언로드 시 누수 방지)
            if (_guardDeathHooked)
                GuardPlaceholder.OnAnyGuardDied -= OnGuardDiedCheckLoot;
        }

        /// <summary>
        /// [전투 시나리오] 게임 시작 후 _combatStartDelaySeconds(기본 15초)에 몬스터-병사를 '전투 상태'로 만든다.
        ///  1) MonsterAggroSystem 활성화 확인 (AnimalAI는 Start에서 자가 등록됨)
        ///  2) 각 몬스터에 가장 가까운 병사를 어그로 대상으로 설정 → Idle → Alert → (3초 후) Combat 전이
        ///  3) 병사 사망 시 전리품(LootBasket) 드랍 여부를 로그로 검증 (DropTable 수정은 병렬 에이전트 담당)
        /// </summary>
        private System.Collections.IEnumerator CombatScenarioRoutine()
        {
            // 스폰/셋업 안정화 후 지정 시간(15초) 대기
            yield return new WaitForSeconds(_combatStartDelaySeconds);

            var monsters = FindObjectsByType<AnimalAI>(FindObjectsSortMode.None);
            var guards = FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None);

            Debug.Log($"[TestAllInOneSetup] ⚔️ 전투 시나리오 시작 (시작 후 {_combatStartDelaySeconds:0}초): 몬스터 {monsters.Length}마리, 병사 {guards.Length}명");

            // MonsterAggroSystem 활성화 상태 로그 (몬스터-병사 어그로 전파 담당)
            if (MonsterAggroSystem.Instance != null)
                Debug.Log($"[TestAllInOneSetup] 🎯 MonsterAggroSystem 활성: 등록 몬스터 {MonsterAggroSystem.Instance.MonsterCount}마리, 어그로 전파 범위 {MonsterAggroSystem.AGGRO_RANGE:0}m");
            else
                Debug.LogWarning("[TestAllInOneSetup] ⚠️ MonsterAggroSystem 없음 — 어그로 전파 검증 불가");

            if (guards.Length == 0)
            {
                Debug.LogWarning("[TestAllInOneSetup] ⚠️ 병사가 없어 몬스터-병사 전투 시나리오를 건너뜁니다 (_includeTerritory 확인)");
                yield break;
            }

            // 병사 사망 → 전리품 드랍 검증 훅
            if (!_guardDeathHooked)
            {
                GuardPlaceholder.OnAnyGuardDied += OnGuardDiedCheckLoot;
                _guardDeathHooked = true;
                _knownLootBasketCount = FindObjectsByType<LootBasket>(FindObjectsSortMode.None).Length;
            }

            // 시나리오 검증 로그: 병사가 몬스터 detection range 안에 놓였는지 확인 (자연 어그로 가능 여부)
            foreach (var monster in monsters)
            {
                if (monster == null || monster.IsDead) continue;

                GuardPlaceholder nearest = null;
                float bestDist = float.MaxValue;
                foreach (var guard in guards)
                {
                    if (guard == null || guard.IsDead) continue;
                    float d = Vector3.Distance(monster.transform.position, guard.transform.position);
                    if (d < bestDist) { bestDist = d; nearest = guard; }
                }
                if (nearest == null) continue;

                // 강제 '전투 상태' 진입: IAggroable.SetAggroTarget → Idle → Alert (AnimalAI 내부에서 3초 후 Combat)
                monster.SetAggroTarget(nearest.gameObject);

                float detect = GetExpectedDetectRange(monster.Tier);
                string rangeCheck = bestDist <= detect ? "✅ 인지 범위 내" : "⚠️ 인지 범위 밖";
                Debug.Log($"[TestAllInOneSetup] ⚔️ {monster.name}({monster.Tier}) ↔ {nearest.name} 거리 {bestDist:0.0}m / detection {detect:0}m [{rangeCheck}] → 어그로 설정");
            }

            // 전투 상태 진입 및 병사 피해/전리품 드랍을 로그로 추적 (최대 60초, 3초 간격)
            for (int tick = 0; tick < 20; tick++)
            {
                yield return new WaitForSeconds(3f);
                LogCombatStatus();
            }
        }

        /// <summary>전투 상태 요약 로그: 전투 중인 몬스터, 피해를 입은 병사, 신규 LootBasket.</summary>
        private void LogCombatStatus()
        {
            var monsters = FindObjectsByType<AnimalAI>(FindObjectsSortMode.None);
            var guards = FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None);

            foreach (var monster in monsters)
            {
                if (monster != null && monster.IsInCombat)
                {
                    string targetName = monster.AggroTarget != null ? monster.AggroTarget.name : "null";
                    Debug.Log($"[TestAllInOneSetup] ⚔️ {monster.name} 전투 상태 진입 → 대상: {targetName}");
                }
            }

            foreach (var guard in guards)
            {
                // 몬스터가 병사를 공격해 HP가 깎이면 로그로 확인 (병사 타격 로그 대역)
                if (guard != null && !guard.IsDead && guard.CurrentHP < guard.MaxHP)
                    Debug.Log($"[TestAllInOneSetup] 🩸 {guard.name} 피해: HP {guard.CurrentHP:0}/{guard.MaxHP:0} — 몬스터 공격 검증 중");
            }

            int basketCount = FindObjectsByType<LootBasket>(FindObjectsSortMode.None).Length;
            if (basketCount > _knownLootBasketCount)
            {
                Debug.Log($"[TestAllInOneSetup] 🎁 LootBasket 생성 확인: 신규 {basketCount - _knownLootBasketCount}개 (총 {basketCount}) — 병사 처치 → 전리품 드랍 검증 성공!");
                _knownLootBasketCount = basketCount;
            }
        }

        /// <summary>병사 사망 콜백: 전리품(LootBasket) 드랍 여부를 짧은 폴링으로 검증.</summary>
        private void OnGuardDiedCheckLoot(GuardPlaceholder guard)
        {
            if (guard == null) return;
            Debug.Log($"[TestAllInOneSetup] 💀 병사 사망 감지: {guard.name} — 전리품(LootBasket) 드랍 확인 대기...");
            StartCoroutine(CheckLootBasketSpawned());
        }

        private System.Collections.IEnumerator CheckLootBasketSpawned()
        {
            for (int i = 0; i < 10; i++) // 최대 10초 대기
            {
                yield return new WaitForSeconds(1f);
                int basketCount = FindObjectsByType<LootBasket>(FindObjectsSortMode.None).Length;
                if (basketCount > _knownLootBasketCount)
                {
                    Debug.Log($"[TestAllInOneSetup] 🎁 LootBasket 생성 확인: 총 {basketCount}개 — 병사 사망 → 드랍 → 바구니 검증 성공!");
                    _knownLootBasketCount = basketCount;
                    yield break;
                }
            }
            Debug.LogWarning("[TestAllInOneSetup] ⚠️ 병사 사망 후 10초 내 LootBasket 미확인 — DropTable 연동 확인 필요");
        }

        /// <summary>MonsterTier별 AnimalAI 감지 범위(AnimalAI.ApplyMonsterDefinition 기준) — 검증 로그용.</summary>
        private static float GetExpectedDetectRange(MonsterTier tier) => tier switch
        {
            MonsterTier.Beginner => 10f,
            MonsterTier.Intermediate => 14f,
            MonsterTier.Advanced => 18f,
            _ => 10f
        };
    }
}