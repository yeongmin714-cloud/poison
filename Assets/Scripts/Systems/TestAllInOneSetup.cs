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

            // TownBuilder
            if (FindAnyObjectByType<TownBuilder>() == null)
            {
                var twnGO = new GameObject("TownBuilder");
                twnGO.AddComponent<TownBuilder>();
                Debug.Log("[TestAllInOneSetup] ✅ TownBuilder 생성");
            }

            // GuardManager
            if (GuardManager.Instance == null)
            {
                var gmGO = new GameObject("GuardManager");
                gmGO.AddComponent<GuardManager>();
                Debug.Log("[TestAllInOneSetup] ✅ GuardManager 생성");
            }

            // TerritoryCaptureSystem
            if (FindAnyObjectByType<TerritoryCaptureSystem>() == null)
            {
                var tcsGO = new GameObject("TerritoryCaptureSystem");
                tcsGO.AddComponent<TerritoryCaptureSystem>();
                Debug.Log("[TestAllInOneSetup] ✅ TerritoryCaptureSystem 생성");
            }

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

            Debug.Log("[TestAllInOneSetup] ✅ UI 시스템 + 주요 윈도우 생성 완료");
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

            for (int i = 0; i < Mathf.Min(_monsterCount, monsters.Length); i++)
            {
                var (monsterId, tier) = monsters[i];
                float angle = (i / (float)monsters.Length) * 360f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * 15f, 0f, Mathf.Sin(angle) * 15f);

                SpawnMonster(monsterId, pos, tier);
            }

            Debug.Log($"[TestAllInOneSetup] ✅ 전투 테스트 몬스터 {_monsterCount}마리 생성");
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
                    float angle = (i / (float)_guardCount) * 360f * Mathf.Deg2Rad;
                    Vector3 pos = new Vector3(Mathf.Cos(angle) * 8f, 0f, Mathf.Sin(angle) * 8f);

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

                Debug.Log($"[TestAllInOneSetup] ✅ 테스트 병사 {_guardCount}명 생성");
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

            if (FindAnyObjectByType<ProjectName.Systems.Animation.Procedural.ParentVelocityProvider>() == null)
            {
                var pvGO = new GameObject("ParentVelocityProvider");
                pvGO.AddComponent<ProjectName.Systems.Animation.Procedural.ParentVelocityProvider>();
                Debug.Log("[TestAllInOneSetup] ✅ ParentVelocityProvider 생성");
            }
        }
    }
}