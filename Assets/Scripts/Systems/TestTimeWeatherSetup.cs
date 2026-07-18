using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_06_TimeWeather 전용: 시간+날씨 시스템 런타임 검증을 위한 최소 구성.
    /// TimeManager, DayNightCycle, WeatherManager, WeatherParticleController,
    /// Sun/Moon Light, Skybox, Fog를 빈 씬에 구성합니다.
    /// </summary>
    public class TestTimeWeatherSetup : MonoBehaviour
    {
        [Header("Time Settings")]
        [SerializeField] private float _startHour = 6f; // 시작 시간 (06:00 = 아침)

        [Header("Light Settings")]
        [SerializeField] private Color _sunColor = new Color(1f, 0.95f, 0.8f);
        [SerializeField] private float _sunIntensity = 1.2f;
        [SerializeField] private Color _moonColor = new Color(0.6f, 0.7f, 1.0f);
        [SerializeField] private float _moonIntensity = 0.2f;

        private void Awake()
        {
            SetupTimeManager();
            SetupSunLight();
            SetupMoonLight();
            SetupDayNightCycle();
            SetupWeatherManager();
            SetupWeatherParticles();
            SetupWindZone();
            SetupSkybox();
            SetupFog();
            SetupCamera();
            SetupGround();
            EnsureEventSystem();
            Debug.Log("[TestTimeWeatherSetup] ✅ 시간+날씨 테스트 씬 설정 완료");
        }

        private void SetupTimeManager()
        {
            // TimeManager는 싱글톤 — Instance가 없으면 생성
            if (TimeManager.Instance == null)
            {
                var tmGO = new GameObject("TimeManager");
                var tm = tmGO.AddComponent<TimeManager>();
                tm.TimeScale = 60f; // 현실 1초 = 게임 1분
                tm.GameTime = _startHour * 3600f; // 시작 시간 설정
                Debug.Log($"[TestTimeWeatherSetup] ✅ TimeManager 생성 (시작: {_startHour:D2}:00)");
            }
            else
            {
                Debug.Log("[TestTimeWeatherSetup] ✅ TimeManager 이미 존재");
            }
        }

        private void SetupSunLight()
        {
            if (FindAnyObjectByType<Light>() == null || 
                FindAnyObjectByType<Light>().type != LightType.Directional)
            {
                var sunGO = new GameObject("Sun Light");
                var sun = sunGO.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = _sunColor;
                sun.intensity = _sunIntensity;
                sun.shadowStrength = 1f;
                sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
                Debug.Log("[TestTimeWeatherSetup] ✅ Sun Light 생성");
            }
        }

        private void SetupMoonLight()
        {
            // Moon Light — 두 번째 Directional Light, 차가운 청백색
            var moonGO = new GameObject("Moon Light");
            var moon = moonGO.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = _moonColor;
            moon.intensity = _moonIntensity;
            moon.shadowStrength = 0.3f;
            moon.transform.rotation = Quaternion.Euler(230f, 210f, 0f); // 태양 반대 방향
            moon.enabled = false; // 낮에는 비활성화 (DayNightCycle이 제어)
            Debug.Log("[TestTimeWeatherSetup] ✅ Moon Light 생성");
        }

        private void SetupDayNightCycle()
        {
            // DayNightCycle은 [RequireComponent(typeof(TimeManager))] — TimeManager가 있는 오브젝트에 부착
            var tmGO = GameObject.Find("TimeManager");
            if (tmGO != null)
            {
                if (tmGO.GetComponent<DayNightCycle>() == null)
                {
                    var dnc = tmGO.AddComponent<DayNightCycle>();

                    // Sun/Moon Light 참조 설정 (리플렉션으로 private 필드 설정)
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

                    Debug.Log("[TestTimeWeatherSetup] ✅ DayNightCycle 부착 + Light 참조 연결");
                }
            }
            else
            {
                Debug.LogError("[TestTimeWeatherSetup] TimeManager 오브젝트를 찾을 수 없습니다.");
            }
        }

        private void SetupWeatherManager()
        {
            // WeatherManager는 싱글톤 + lazy 생성 — Instance 접근만으로 생성됨
            if (WeatherManager.Instance == null)
            {
                Debug.LogError("[TestTimeWeatherSetup] WeatherManager.Instance 생성 실패");
            }
            else
            {
                // Directional Light 참조 연결
                var dirLight = FindAnyObjectByType<Light>();
                if (dirLight != null && dirLight.type == LightType.Directional)
                {
                    WeatherManager.Instance.SetLightReference(dirLight);
                }

                // Clear 날씨로 시작 (타이머 정지)
                WeatherManager.Instance.SetWeather(WeatherManager.WeatherType.Clear);
                WeatherManager.Instance.SetTimer(9999f); // 오래 지속 (자동 전환 방지)

                Debug.Log("[TestTimeWeatherSetup] ✅ WeatherManager 생성 (Clear, 자동전환 OFF)");
            }
        }

        private void SetupWeatherParticles()
        {
            if (FindAnyObjectByType<WeatherParticleController>() == null)
            {
                var particleGO = new GameObject("WeatherParticleController");
                var wpc = particleGO.AddComponent<WeatherParticleController>();
                Debug.Log("[TestTimeWeatherSetup] ✅ WeatherParticleController 생성");
            }
        }

        private void SetupWindZone()
        {
            if (FindAnyObjectByType<WindZone>() == null)
            {
                var windGO = new GameObject("WindZone");
                var wind = windGO.AddComponent<WindZone>();
                wind.windMain = 0f;
                wind.windTurbulence = 0.5f;
                wind.windPulseMagnitude = 0.5f;
                wind.windPulseFrequency = 0.5f;
                wind.mode = WindZoneMode.Directional;
                Debug.Log("[TestTimeWeatherSetup] ✅ WindZone 생성");
            }
        }

        private void SetupSkybox()
        {
            if (RenderSettings.skybox == null)
            {
                // Procedural Skybox Material 생성
                var skyboxMat = new Material(Shader.Find("Skybox/Procedural"));
                if (skyboxMat != null && skyboxMat.shader != null)
                {
                    skyboxMat.name = "TestSkybox_Procedural";
                    skyboxMat.SetColor("_SkyTint", new Color(0.4f, 0.6f, 0.9f));
                    skyboxMat.SetColor("_GroundColor", new Color(0.5f, 0.5f, 0.5f));
                    skyboxMat.SetFloat("_Exposure", 1.0f);
                    skyboxMat.SetFloat("_AtmosphereThickness", 0.8f);
                    skyboxMat.SetFloat("_SunSize", 0.04f);
                    RenderSettings.skybox = skyboxMat;
                    Debug.Log("[TestTimeWeatherSetup] ✅ Procedural Skybox 생성");
                }
                else
                {
                    Debug.LogWarning("[TestTimeWeatherSetup] Skybox/Procedural shader를 찾을 수 없습니다. Skybox 기본값 사용.");
                }
            }
        }

        private void SetupFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = new Color(0.7f, 0.75f, 0.8f);
            Debug.Log("[TestTimeWeatherSetup] ✅ Fog 활성화");
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
            cam.transform.position = new Vector3(0, 15, -15);
            cam.transform.rotation = Quaternion.Euler(45, 0, 0);

            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();

            Debug.Log("[TestTimeWeatherSetup] ✅ Camera 설정 완료");
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
                    if (mat != null && mat.shader != null)
                    {
                        mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
                        mat.SetFloat("_Smoothness", 0f);
                        renderer.material = mat;
                    }
                }
                Debug.Log("[TestTimeWeatherSetup] ✅ Ground 생성");
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[TestTimeWeatherSetup] ✅ EventSystem 생성");
            }
        }
    }
}