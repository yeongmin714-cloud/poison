using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine.InputSystem;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_08_Dracula 씬 전용: 드라큘라 영주 + 야간 시스템 통합 검증.
    /// DraculaLord, DraculaTerritoryController, DayNightCycle, MonsterSkillSystem,
    /// TimeManager, BloodMist(시각 효과), Player target 구성.
    /// </summary>
    public class TestDraculaSetup : MonoBehaviour
    {
        [Header("Time Settings")]
        [SerializeField] private float _startHour = 0f; // 시작 시간 (00:00 = 자정, 밤)

        [Header("Dracula Settings")]
        [SerializeField] private bool _spawnDraculaLord = true;
        [SerializeField] private Vector3 _draculaSpawnPosition = new Vector3(0f, 0f, 10f);
        [SerializeField] private bool _verbose = true;

        [Header("Light Settings")]
        [SerializeField] private Color _moonColor = new Color(0.6f, 0.7f, 1.0f);
        [SerializeField] private float _moonIntensity = 0.2f;

        [Header("Skeleton Guards")]
        [SerializeField] private bool _spawnSkeletonGuards = true;
        [SerializeField] private int _guardCount = 3;
        [SerializeField] private float _guardSpreadRadius = 8f;

        private void Awake()
        {
            SetupTimeManager();
            SetupSunLight();
            SetupMoonLight();
            SetupDayNightCycle();
            SetupMonsterSkillSystem();
            SetupDraculaTerritoryController();
            SetupPlayerTarget();
            SetupCamera();
            SetupGround();
            SetupSkybox();
            SetupFog();
            EnsureEventSystem();

            if (_spawnDraculaLord)
                SpawnDraculaLord();

            if (_spawnSkeletonGuards)
                SpawnSkeletonGuards();

            Log($"[TestDraculaSetup] ✅ Test_08_Dracula 설정 완료! (자정: {_startHour:D2}:00)");
        }

        private void Log(string msg)
        {
            if (_verbose)
                Debug.Log(msg);
        }

        // ================================================================
        // TimeManager
        // ================================================================

        private void SetupTimeManager()
        {
            if (TimeManager.Instance == null)
            {
                var tmGO = new GameObject("TimeManager");
                var tm = tmGO.AddComponent<TimeManager>();
                tm.TimeScale = 60f; // 현실 1초 = 게임 1분
                tm.GameTime = _startHour * 3600f; // 자정 시작
                Log("[TestDraculaSetup] ✅ TimeManager 생성 (자정)");
            }
            else
            {
                Log("[TestDraculaSetup] ✅ TimeManager 이미 존재");
                TimeManager.Instance.GameTime = _startHour * 3600f;
            }
        }

        // ================================================================
        // Sun & Moon Light
        // ================================================================

        private void SetupSunLight()
        {
            var existing = FindAnyObjectByType<Light>();
            if (existing == null || existing.type != LightType.Directional)
            {
                var sunGO = new GameObject("Sun Light");
                var sun = sunGO.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.95f, 0.8f);
                sun.intensity = 1.2f;
                sun.shadowStrength = 1f;
                sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
                Log("[TestDraculaSetup] ✅ Sun Light 생성");
            }
        }

        private void SetupMoonLight()
        {
            var moonGO = new GameObject("Moon Light");
            var moon = moonGO.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = _moonColor;
            moon.intensity = _moonIntensity;
            moon.shadowStrength = 0.3f;
            moon.transform.rotation = Quaternion.Euler(230f, 210f, 0f);
            moon.enabled = false; // DayNightCycle이 제어
            Log("[TestDraculaSetup] ✅ Moon Light 생성 (차가운 청백색)");
        }

        // ================================================================
        // DayNightCycle
        // ================================================================

        private void SetupDayNightCycle()
        {
            var tmGO = GameObject.Find("TimeManager");
            if (tmGO != null && tmGO.GetComponent<DayNightCycle>() == null)
            {
                var dnc = tmGO.AddComponent<DayNightCycle>();

                var sunField = typeof(DayNightCycle).GetField("_sunLight",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var sun = GameObject.Find("Sun Light")?.GetComponent<Light>();
                if (sunField != null && sun != null)
                    sunField.SetValue(dnc, sun);

                var moonField = typeof(DayNightCycle).GetField("_moonLight",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var moon = GameObject.Find("Moon Light")?.GetComponent<Light>();
                if (moonField != null && moon != null)
                    moonField.SetValue(dnc, moon);

                Log("[TestDraculaSetup] ✅ DayNightCycle 부착 + Light 참조 연결");
            }
        }

        // ================================================================
        // MonsterSkillSystem (Dracula 스킬 연동)
        // ================================================================

        private void SetupMonsterSkillSystem()
        {
            // MonsterSkillSystem은 싱글톤 — Instance 접근으로 자동 생성
            if (MonsterSkillSystem.Instance != null)
            {
                Log("[TestDraculaSetup] ✅ MonsterSkillSystem 자동 생성됨");
            }
        }

        // ================================================================
        // DraculaTerritoryController
        // ================================================================

        private void SetupDraculaTerritoryController()
        {
            if (DraculaTerritoryController.Instance == null)
            {
                var dtcGO = new GameObject("DraculaTerritoryController");
                dtcGO.AddComponent<DraculaTerritoryController>();
                Log("[TestDraculaSetup] ✅ DraculaTerritoryController 생성");
            }
            else
            {
                Log("[TestDraculaSetup] ✅ DraculaTerritoryController 이미 존재");
            }
        }

        // ================================================================
        // Player Target (DraculaLord가 추적할 대상)
        // ================================================================

        private void SetupPlayerTarget()
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

            // PlayerMovement (이동 가능)
            var pmType = typeof(ProjectName.Systems.PlayerMovement);
            if (player.GetComponent(pmType) == null)
            {
                player.AddComponent(pmType);
            }

            // PlayerInput
            if (player.GetComponent<PlayerInput>() == null)
            {
                var pi = player.AddComponent<PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
            }

            // PlayerPlaceholder (시각적 표현)
            if (player.GetComponent<PlayerPlaceholder>() == null)
            {
                player.AddComponent<PlayerPlaceholder>();
            }

            // Damageable (드라큘라 공격 대상)
            if (player.GetComponent<Damageable>() == null)
            {
                player.AddComponent<Damageable>();
            }

            player.transform.position = new Vector3(0f, 0f, -5f);
            Log("[TestDraculaSetup] ✅ Player 생성 (DraculaLord 타겟)");
        }

        // ================================================================
        // Camera
        // ================================================================

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
            {
                camGO.AddComponent(tdcType);
            }

            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();

            Log("[TestDraculaSetup] ✅ Camera 설정 완료");
        }

        // ================================================================
        // Ground
        // ================================================================

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
                    mat.color = new Color(0.1f, 0.1f, 0.15f, 1f); // 어두운 밤 느낌
                    mat.SetFloat("_Smoothness", 0f);
                    renderer.material = mat;
                }

                Log("[TestDraculaSetup] ✅ Ground 생성 (어두운 밤 느낌)");
            }
        }

        // ================================================================
        // Skybox
        // ================================================================

        private void SetupSkybox()
        {
            if (RenderSettings.skybox == null)
            {
                var skyboxMat = new Material(Shader.Find("Skybox/Procedural"));
                if (skyboxMat != null && skyboxMat.shader != null)
                {
                    skyboxMat.name = "TestSkybox_Night";
                    skyboxMat.SetColor("_SkyTint", new Color(0.05f, 0.05f, 0.15f)); // 밤 하늘
                    skyboxMat.SetColor("_GroundColor", new Color(0.1f, 0.1f, 0.12f));
                    skyboxMat.SetFloat("_Exposure", 0.5f);
                    skyboxMat.SetFloat("_AtmosphereThickness", 1.2f);
                    skyboxMat.SetFloat("_SunSize", 0.02f);
                    RenderSettings.skybox = skyboxMat;
                    Log("[TestDraculaSetup] ✅ 밤 Skybox 생성");
                }
                else
                {
                    Debug.LogWarning("[TestDraculaSetup] Skybox/Procedural shader를 찾을 수 없습니다.");
                }
            }
        }

        // ================================================================
        // Fog (밤 안개)
        // ================================================================

        private void SetupFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.1f);
            Log("[TestDraculaSetup] ✅ 밤 Fog 활성화 (짙은 어둠)");
        }

        // ================================================================
        // EventSystem
        // ================================================================

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Log("[TestDraculaSetup] ✅ EventSystem 생성");
            }
        }

        // ================================================================
        // DraculaLord Spawn
        // ================================================================

        private void SpawnDraculaLord()
        {
            var existing = FindAnyObjectByType<DraculaLord>();
            if (existing != null)
            {
                Log("[TestDraculaSetup] ⚠️ DraculaLord 이미 존재, 추가 생성하지 않음");
                return;
            }

            var lordGO = new GameObject("DraculaLord");
            lordGO.transform.position = _draculaSpawnPosition;
            lordGO.tag = "DraculaLord";

            var lord = lordGO.AddComponent<DraculaLord>();

            // 영지 ID 설정 (ND-04의 Dracula 영지와 연결)
            lord.SetTerritoryId(new TerritoryId(NationType.Dracula, 1));

            Log($"[TestDraculaSetup] ✅ DraculaLord 생성: 위치 {_draculaSpawnPosition}");
            Log($"[TestDraculaSetup]    HP: {lord.MaxHP}, ATK: {lord.AttackDamage}, DEF: {lord.Defense}");
            Log($"[TestDraculaSetup]    특수 패턴: 순간이동(5초 쿨다운), 흡혈(20%), MonsterSkill 연동");
        }

        // ================================================================
        // Skeleton Guards (드라큘라 영지 병사)
        // ================================================================

        private void SpawnSkeletonGuards()
        {
            int existingCount = FindObjectsByType<SkeletonGuardPlaceholder>(FindObjectsSortMode.None).Length;
            if (existingCount > 0)
            {
                Log($"[TestDraculaSetup] ⚠️ SkeletonGuardPlaceholder {existingCount}개 이미 존재");
                return;
            }

            for (int i = 0; i < _guardCount; i++)
            {
                float angle = (i / (float)_guardCount) * 360f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(
                    _draculaSpawnPosition.x + Mathf.Cos(angle) * _guardSpreadRadius,
                    0f,
                    _draculaSpawnPosition.z + Mathf.Sin(angle) * _guardSpreadRadius
                );

                var guardGO = new GameObject($"SkeletonGuard_{i}");
                guardGO.transform.position = pos;

                // SkeletonGuardPlaceholder는 IDamageable 구현
                guardGO.AddComponent<SkeletonGuardPlaceholder>();

                // 시각 표현 (빨간색 캡슐)
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = $"SkeletonGuard_Visual_{i}";
                visual.transform.SetParent(guardGO.transform, false);
                visual.transform.localPosition = new Vector3(0, 1f, 0);
                visual.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.7f, 0.1f, 0.1f, 1f); // 스켈레톤 붉은색
                    renderer.material = mat;
                }

                // Collider 제거 (시각 전용)
                var col = visual.GetComponent<Collider>();
                if (col != null)
                    DestroyImmediate(col);

                Log($"[TestDraculaSetup] ✅ SkeletonGuard_{i} 생성: 위치 {pos}");
            }
        }

        // ================================================================
        // 편의 메서드: 테스트 런타임 제어
        // ================================================================

        /// <summary>
        /// 밤으로 전환 (빠른 테스트용)
        /// </summary>
        public void SetNight()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.GameTime = 0f; // 자정
                Log("[TestDraculaSetup] 🌙 밤으로 전환 (자정)");
            }
        }

        /// <summary>
        /// 낮으로 전환
        /// </summary>
        public void SetDay()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.GameTime = 43200f; // 정오
                Log("[TestDraculaSetup] ☀️ 낮으로 전환 (정오)");
            }
        }

        /// <summary>
        /// DraculaLord HP 출력
        /// </summary>
        public void LogLordHP()
        {
            var lord = FindAnyObjectByType<DraculaLord>();
            if (lord != null)
            {
                Debug.Log($"[TestDraculaSetup] 🧛 DraculaLord HP: {lord.HP}/{lord.MaxHP} ({(lord.HPPercentage * 100f):F1}%)");
            }
            else
            {
                Debug.LogWarning("[TestDraculaSetup] DraculaLord가 없습니다.");
            }
        }

        /// <summary>
        /// DraculaLord 부활
        /// </summary>
        public void ResurrectLord()
        {
            var lord = FindAnyObjectByType<DraculaLord>();
            if (lord != null && lord.IsDead)
            {
                lord.Resurrect();
                Log("[TestDraculaSetup] ✅ DraculaLord 부활!");
            }
        }

        /// <summary>
        /// 박쥐 소환 강제 트리거
        /// </summary>
        public void ForceSummonBats()
        {
            var lord = FindAnyObjectByType<DraculaLord>();
            if (lord != null)
            {
                lord.ForceSummonBats();
                Log("[TestDraculaSetup] 🦇 박쥐 소환 트리거!");
            }
        }

        private void Update()
        {
            if (Keyboard.current != null)
            {
                // N = 밤으로 전환
                if (Keyboard.current.nKey.wasPressedThisFrame)
                {
                    if (Keyboard.current.shiftKey.isPressed)
                        SetNight();
                }

                // D = 낮으로 전환 (Shift+D)
                if (Keyboard.current.dKey.wasPressedThisFrame)
                {
                    if (Keyboard.current.shiftKey.isPressed)
                        SetDay();
                }

                // H = Lord HP 출력
                if (Keyboard.current.hKey.wasPressedThisFrame)
                {
                    LogLordHP();
                }

                // R = Lord 부활 (Shift+R)
                if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    if (Keyboard.current.shiftKey.isPressed)
                        ResurrectLord();
                }
            }
        }
    }
}