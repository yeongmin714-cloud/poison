using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_04_Territory 씬 전용: 영지+병사+건물 시스템 통합 검증.
    /// TerritoryManager, TerritoryBuilder, TownBuilder, GuardManager, GuardRecruitSystem,
    /// TerritoryCaptureSystem, TerritoryWarManager, NationTerrainController 구성.
    /// </summary>
    public class TestTerritorySetup : MonoBehaviour
    {
        [Header("Territory Settings")]
        [SerializeField] private NationType _testNation = NationType.Dracula; // 테스트용 국가
        [SerializeField] private int _territoryId = 1;
        [SerializeField] private bool _spawnGuards = true;
        [SerializeField] private int _guardCount = 5;
        [SerializeField] private bool _buildTown = true;

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
            SetupTimeAndWeather();
            SetupTerritorySystem();
            SetupPlayer();
            SetupCamera();
            SetupGround();
            SetupLight();
            SetupSkybox();

            if (_spawnGuards)
                SpawnTestGuards();

            if (_buildTown)
                BuildTestTown();

            Debug.Log($"[TestTerritorySetup] ✅ 영지+병사+건물 테스트 씬 설정 완료 (국가: {_testNation})");
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[TestTerritorySetup] ✅ EventSystem 생성");
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
                Debug.Log("[TestTerritorySetup] ✅ GameManager 생성");
            }
        }

        private void SetupTimeAndWeather()
        {
            // TimeManager
            if (TimeManager.Instance == null)
            {
                var tmGO = new GameObject("TimeManager");
                var tm = tmGO.AddComponent<TimeManager>();
                tm.TimeScale = 60f;
                tm.GameTime = 43200f; // 정오
                Debug.Log("[TestTerritorySetup] ✅ TimeManager 생성 (정오)");
            }

            // DayNightCycle
            var tmGO = GameObject.Find("TimeManager");
            if (tmGO != null && tmGO.GetComponent<DayNightCycle>() == null)
            {
                var dnc = tmGO.AddComponent<DayNightCycle>();
                var sun = GameObject.Find("Sun Light")?.GetComponent<Light>();
                var moon = GameObject.Find("Moon Light")?.GetComponent<Light>();
                if (sun != null)
                {
                    var sunField = typeof(DayNightCycle).GetField("_sunLight",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    sunField?.SetValue(dnc, sun);
                }
                if (moon != null)
                {
                    var moonField = typeof(DayNightCycle).GetField("_moonLight",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    moonField?.SetValue(dnc, moon);
                }
                Debug.Log("[TestTerritorySetup] ✅ DayNightCycle 부착");
            }

            // WeatherManager
            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.SetWeather(WeatherManager.WeatherType.Clear);
                WeatherManager.Instance.SetTimer(9999f);
                Debug.Log("[TestTerritorySetup] ✅ WeatherManager 설정 (Clear)");
            }
        }

        private void SetupTerritorySystem()
        {
            // TerritoryManager
            if (TerritoryManager.Instance == null)
            {
                var tmGO = new GameObject("TerritoryManager");
                tmGO.AddComponent<TerritoryManager>();
                Debug.Log("[TestTerritorySetup] ✅ TerritoryManager 생성");
            }

            // NationTerrainController
            if (NationTerrainController.Instance == null)
            {
                var ntcGO = new GameObject("NationTerrainController");
                ntcGO.AddComponent<NationTerrainController>();
                Debug.Log("[TestTerritorySetup] ✅ NationTerrainController 생성");
            }

            // TerritoryBuilder
            if (TerritoryBuilder.Instance == null)
            {
                var tbGO = new GameObject("TerritoryBuilder");
                tbGO.AddComponent<TerritoryBuilder>();
                Debug.Log("[TestTerritorySetup] ✅ TerritoryBuilder 생성");
            }

            // TownBuilder
            if (TownBuilder.Instance == null)
            {
                var twnGO = new GameObject("TownBuilder");
                twnGO.AddComponent<TownBuilder>();
                Debug.Log("[TestTerritorySetup] ✅ TownBuilder 생성");
            }

            // GuardManager
            if (GuardManager.Instance == null)
            {
                var gmGO = new GameObject("GuardManager");
                gmGO.AddComponent<GuardManager>();
                Debug.Log("[TestTerritorySetup] ✅ GuardManager 생성");
            }

            // TerritoryCaptureSystem
            if (TerritoryCaptureSystem.Instance == null)
            {
                var tcsGO = new GameObject("TerritoryCaptureSystem");
                tcsGO.AddComponent<TerritoryCaptureSystem>();
                Debug.Log("[TestTerritorySetup] ✅ TerritoryCaptureSystem 생성");
            }

            // TerritoryWarManager
            if (TerritoryWarManager.Instance == null)
            {
                var twmGO = new GameObject("TerritoryWarManager");
                twmGO.AddComponent<TerritoryWarManager>();
                Debug.Log("[TestTerritorySetup] ✅ TerritoryWarManager 생성");
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

            player.transform.position = Vector3.zero;
            Debug.Log("[TestTerritorySetup] ✅ Player 설정 완료");
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

            Debug.Log("[TestTerritorySetup] ✅ 카메라 설정 완료");
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
                Debug.Log("[TestTerritorySetup] ✅ Ground 생성");
            }
        }

        private void SetupLight()
        {
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

            Debug.Log("[TestTerritorySetup] ✅ Sun/Moon Light 생성");
        }

        private void SetupSkybox()
        {
            if (RenderSettings.skybox == null)
            {
                var skyboxMat = new Material(Shader.Find("Skybox/Procedural"));
                if (skyboxMat != null && skyboxMat.shader != null)
                {
                    skyboxMat.name = "TestSkybox_Day";
                    skyboxMat.SetColor("_SkyTint", new Color(0.4f, 0.6f, 0.9f));
                    skyboxMat.SetColor("_GroundColor", new Color(0.5f, 0.5f, 0.5f));
                    skyboxMat.SetFloat("_Exposure", 1.0f);
                    skyboxMat.SetFloat("_AtmosphereThickness", 0.8f);
                    skyboxMat.SetFloat("_SunSize", 0.04f);
                    RenderSettings.skybox = skyboxMat;
                    Debug.Log("[TestTerritorySetup] ✅ Procedural Skybox 생성");
                }
            }
        }

        private void SpawnTestGuards()
        {
            var guardManager = GuardManager.Instance;
            if (guardManager == null)
            {
                Debug.LogWarning("[TestTerritorySetup] GuardManager가 없습니다.");
                return;
            }

            var territoryId = new TerritoryId(_testNation, _territoryId);

            for (int i = 0; i < _guardCount; i++)
            {
                float angle = (i / (float)_guardCount) * 360f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * 10f,
                    0f,
                    Mathf.Sin(angle) * 10f
                );

                var guardGO = new GameObject($"Guard_{i}");
                guardGO.transform.position = pos;

                // GuardPlaceholder 사용
                var guard = guardGO.AddComponent<GuardPlaceholder>();
                guard.Initialize(territoryId);

                // 시각 표현
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = $"Guard_Visual_{i}";
                visual.transform.SetParent(guardGO.transform, false);
                visual.transform.localPosition = new Vector3(0, 1f, 0);
                visual.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.2f, 0.6f, 0.2f, 1f); // 녹색 병사
                    renderer.material = mat;
                }

                var col = visual.GetComponent<Collider>();
                if (col != null)
                    DestroyImmediate(col);

                Debug.Log($"[TestTerritorySetup] ✅ Guard_{i} 생성: 위치 {pos}");
            }

            Debug.Log($"[TestTerritorySetup] ✅ 테스트 병사 {_guardCount}명 생성 완료");
        }

        private void BuildTestTown()
        {
            var townBuilder = TownBuilder.Instance;
            if (townBuilder == null)
            {
                Debug.LogWarning("[TestTerritorySetup] TownBuilder가 없습니다.");
                return;
            }

            // TownBuilder가 자동으로 영지 내 건물을 배치하도록 유도
            // 실제로는 TerritoryBuilder가 TownBuilder를 호출함
            Debug.Log("[TestTerritorySetup] ✅ TownBuilder 준비됨 (런타임 시 영지 건물 자동 생성)");
        }
    }
}