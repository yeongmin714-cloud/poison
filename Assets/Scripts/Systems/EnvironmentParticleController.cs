using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 41-2: 환경 파티클 시스템.
    /// WeatherManager/TimeManager 이벤트를 구독하여 비/눈/반딧불/먼지 파티클을 제어합니다.
    /// </summary>
    public class EnvironmentParticleController : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static EnvironmentParticleController _instance;
        private static bool _instanceQuitting;

        /// <summary>싱글톤 인스턴스</summary>
        public static EnvironmentParticleController Instance
        {
            get
            {
                if (_instanceQuitting) return null;
                if (_instance == null)
                {
                    var go = new GameObject("EnvironmentParticleController");
                    _instance = go.AddComponent<EnvironmentParticleController>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("파티클 설정")]
        [SerializeField, Tooltip("Rain emission rate")] private float _rainEmissionRate = 500f;
        [SerializeField, Tooltip("Snow emission rate")] private float _snowEmissionRate = 200f;
        [SerializeField, Tooltip("Fireflies emission rate")] private float _fireflyEmissionRate = 15f;
        [SerializeField, Tooltip("Dust emission rate")] private float _dustEmissionRate = 30f;

        [Header("참조")]
        [SerializeField, Tooltip("플레이어 Transform (자동 탐색)")] private Transform _playerTransform;

        // ================================================================
        // State
        // ================================================================

        private ParticleSystem _rainSystem;
        private ParticleSystem _snowSystem;
        private ParticleSystem _fireflySystem;
        private ParticleSystem _dustSystem;

        private WeatherManager _weatherManager;
        private TimeManager _timeManager;

        // 캐시된 바이옴 이름 (Fireflies 전용)
        private string _currentBiome;

        // 캐시된 IBiomeProvider (매 프레임 FindObjectsByType 방지)
        private IBiomeProvider _biomeProvider;
        private float _biomeProviderTimer;
        private const float BIOME_REFRESH_INTERVAL = 2f;

        // 캐시된 텍스처들
        private Texture2D _softCircleTex;
        private Texture2D _softCircleYellowTex;
        private Texture2D _softCircleWhiteTex;

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;

            // WeatherManager 이벤트 구독 해제
            if (_weatherManager != null)
                _weatherManager.OnWeatherChanged -= OnWeatherChanged;

            // TimeManager 이벤트 구독 해제
            if (_timeManager != null)
            {
                _timeManager.OnNightStart -= OnNightStart;
                _timeManager.OnDayStart -= OnDayStart;
            }
        }

        private void OnApplicationQuit()
        {
            _instanceQuitting = true;
        }

        private void Start()
        {
            // 플레이어 참조 해결
            ResolvePlayer();

            // WeatherManager 참조
            _weatherManager = WeatherManager.Instance;
            if (_weatherManager != null)
            {
                _weatherManager.OnWeatherChanged += OnWeatherChanged;
            }
            else
            {
                Debug.LogWarning("[EnvironmentParticleController] WeatherManager를 찾을 수 없습니다.");
            }

            // TimeManager 참조
            _timeManager = TimeManager.Instance;
            if (_timeManager != null)
            {
                _timeManager.OnNightStart += OnNightStart;
                _timeManager.OnDayStart += OnDayStart;
            }
            else
            {
                Debug.LogWarning("[EnvironmentParticleController] TimeManager를 찾을 수 없습니다.");
            }

            // 절차적 텍스처 생성 (소프트 서클)
            CreateProceduralTextures();

            // 4개의 파티클 시스템 생성
            CreateRainParticleSystem();
            CreateSnowParticleSystem();
            CreateFireflyParticleSystem();
            CreateDustParticleSystem();

            // 초기 상태 적용
            ApplyInitialStates();

            // IBiomeProvider 캐싱 (매 프레임 FindObjectsByType 방지)
            CacheBiomeProvider();
        }

        private void Update()
        {
            // 플레이어 따라가기
            FollowPlayer();

            // Fireflies: 바이옴 체크 (주기적 갱신, 매 프레임 FindObjectsByType 방지)
            _biomeProviderTimer += Time.deltaTime;
            if (_biomeProviderTimer >= BIOME_REFRESH_INTERVAL)
            {
                _biomeProviderTimer = 0f;
                CacheBiomeProvider();
            }
            UpdateFireflyBiomeState();
        }

        // ================================================================
        // 플레이어 Follow
        // ================================================================

        /// <summary>
        /// 모든 파티클 시스템이 플레이어를 따라다니도록 위치를 갱신합니다.
        /// </summary>
        private void FollowPlayer()
        {
            if (_playerTransform == null)
            {
                ResolvePlayer();
                if (_playerTransform == null) return;
            }

            Vector3 pos = _playerTransform.position;

            if (_rainSystem != null)
                _rainSystem.transform.position = pos;
            if (_snowSystem != null)
                _snowSystem.transform.position = pos;
            if (_fireflySystem != null)
                _fireflySystem.transform.position = pos;
            if (_dustSystem != null)
                _dustSystem.transform.position = pos;
        }

        // ================================================================
        // 플레이어 참조 해결
        // ================================================================

        /// <summary>
        /// 씬에서 플레이어 GameObject를 찾아 Transform을 캐싱합니다.
        /// </summary>
        private void ResolvePlayer()
        {
            if (_playerTransform != null) return;

            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                _playerTransform = playerGO.transform;
            }
            else
            {
                // 태그가 없으면 Camera.main의 부모 or 메인 카메라 사용
                var cam = Camera.main;
                if (cam != null)
                    _playerTransform = cam.transform;
            }
        }

        // ================================================================
        // 절차적 텍스처 생성
        // ================================================================

        /// <summary>
        /// 파티클용 소프트 서클 텍스처를 절차적으로 생성합니다.
        /// </summary>
        private void CreateProceduralTextures()
        {
            // 반투명 하늘색 소프트 서클 (Rain)
            _softCircleTex = CreateSoftCircleTexture(16, 16,
                new Color(0.5f, 0.7f, 1.0f, 0.4f));

            // 흰색 소프트 서클 (Snow)
            _softCircleWhiteTex = CreateSoftCircleTexture(16, 16,
                new Color(1f, 1f, 1f, 0.8f));

            // 황록색 소프트 서클 (Fireflies)
            _softCircleYellowTex = CreateSoftCircleTexture(16, 16,
                new Color(0.6f, 0.9f, 0.3f, 0.9f));
        }

        /// <summary>
        /// 수학적 원형 그라데이션으로 소프트 서클 텍스처를 생성합니다.
        /// </summary>
        /// <param name="width">텍스처 너비</param>
        /// <param name="height">텍스처 높이</param>
        /// <param name="color">베이스 색상</param>
        /// <returns>생성된 Texture2D</returns>
        private Texture2D CreateSoftCircleTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = width * 0.5f;
            float cy = height * 0.5f;
            float maxDist = Mathf.Min(cx, cy);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f) - cx;
                    float dy = (y + 0.5f) - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // 소프트 에지: 중앙 1.0 → 가장자리 0.0
                    float alpha = Mathf.Clamp01(1f - (dist / maxDist));
                    // 감마 보정 느낌의 부드러운 폴오프
                    alpha = alpha * alpha * (3f - 2f * alpha); // smoothstep

                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        // ================================================================
        // 파티클 시스템 생성
        // ================================================================

        /// <summary>
        /// Rain 파티클 시스템 생성.
        /// Emission=500, Lifetime=2s, Speed=20, Size=0.1×0.3
        /// Color: 반투명 하늘색, Shape: Box (50×20×50)
        /// </summary>
        private void CreateRainParticleSystem()
        {
            var go = new GameObject("RainParticles");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;

            _rainSystem = CreateParticleSystem(go, _softCircleTex ?? CreateSoftCircleTexture(16, 16, new Color(0.5f, 0.7f, 1.0f, 0.4f)));

            var main = _rainSystem.main;
            main.startLifetime = 2f;
            main.startSpeed = 20f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = new Color(0.5f, 0.7f, 1.0f, 0.4f); // 반투명 하늘색
            main.gravityModifier = 1.5f; // 중력 추가 (비가 아래로)

            var emission = _rainSystem.emission;
            emission.rateOverTime = _rainEmissionRate;

            var shape = _rainSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50f, 20f, 50f);
            // World Space는 파티클이 월드에 고정되도록

            var renderer = _rainSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.5f; // 빗줄기 길이

            // 초기 비활성
            go.SetActive(false);
        }

        /// <summary>
        /// Snow 파티클 시스템 생성.
        /// Emission=200, Lifetime=4s, Speed=3, Size=0.15
        /// Color: 흰색, Shape: Box (50×20×50), 수평 흔들림
        /// </summary>
        private void CreateSnowParticleSystem()
        {
            var go = new GameObject("SnowParticles");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;

            _snowSystem = CreateParticleSystem(go, _softCircleWhiteTex ?? CreateSoftCircleTexture(16, 16, new Color(1f, 1f, 1f, 0.8f)));

            var main = _snowSystem.main;
            main.startLifetime = 4f;
            main.startSpeed = 3f;
            main.startSize = 0.15f;
            main.startColor = new Color(1f, 1f, 1f, 0.8f); // 흰색 반투명
            main.gravityModifier = 0.2f; // 약한 중력

            var emission = _snowSystem.emission;
            emission.rateOverTime = _snowEmissionRate;

            var shape = _snowSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50f, 20f, 50f);

            // 수평 흔들림 (눈이 좌우로 흔들리게)
            var noise = _snowSystem.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.3f);
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.2f;

            // 초기 비활성
            go.SetActive(false);
        }

        /// <summary>
        /// Fireflies 파티클 시스템 생성.
        /// Emission=15, Lifetime=6s, Speed=1, Size=0.08
        /// Color: 황록색, 반짝임 (size over lifetime random 0.5~1.5)
        /// Shape: Box (30×5×30)
        /// </summary>
        private void CreateFireflyParticleSystem()
        {
            var go = new GameObject("FireflyParticles");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;

            _fireflySystem = CreateParticleSystem(go, _softCircleYellowTex ?? CreateSoftCircleTexture(16, 16, new Color(0.6f, 0.9f, 0.3f, 0.9f)));

            var main = _fireflySystem.main;
            main.startLifetime = 6f;
            main.startSpeed = 1f;
            main.startSize = 0.08f;
            main.startColor = new Color(0.6f, 0.9f, 0.3f, 0.9f); // 황록색
            main.gravityModifier = -0.05f; // 약간 위로 떠오름

            var emission = _fireflySystem.emission;
            emission.rateOverTime = _fireflyEmissionRate;

            var shape = _fireflySystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 5f, 30f);

            // 크기 반짝임 (size over lifetime)
            var sizeOverLifetime = _fireflySystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            // AnimationCurve: 시간에 따라 0.5 → 1.5 → 0.5 (반짝임)
            var curve = new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.5f, 1.5f),
                new Keyframe(1f, 0.5f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            // 노이즈로 무작위 움직임
            var noise = _fireflySystem.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.5f);
            noise.frequency = 0.8f;

            // 초기 비활성
            go.SetActive(false);
        }

        /// <summary>
        /// Dust/Pollen 파티클 시스템 생성.
        /// Emission=30, Lifetime=8s, Speed=0.5, Size=0.04
        /// Color: 매우 연한 황색, Shape: Box (40×10×40)
        /// </summary>
        private void CreateDustParticleSystem()
        {
            var go = new GameObject("DustParticles");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;

            Texture2D dustTex = CreateSoftCircleTexture(8, 8, new Color(1f, 0.95f, 0.7f, 0.3f));
            _dustSystem = CreateParticleSystem(go, dustTex);

            var main = _dustSystem.main;
            main.startLifetime = 8f;
            main.startSpeed = 0.5f;
            main.startSize = 0.04f;
            main.startColor = new Color(1f, 0.95f, 0.7f, 0.3f); // 매우 연한 황색
            main.gravityModifier = 0.01f;

            var emission = _dustSystem.emission;
            emission.rateOverTime = _dustEmissionRate;

            var shape = _dustSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 10f, 40f);

            // 미세하게 떠다니는 노이즈
            var noise = _dustSystem.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.1f);
            noise.frequency = 0.3f;

            // 초기 비활성
            go.SetActive(false);
        }

        /// <summary>
        /// ParticleSystem API를 사용하여 파티클 시스템을 생성하는 헬퍼 메서드.
        /// </summary>
        /// <param name="parentGO">파티클 시스템을 추가할 GameObject</param>
        /// <param name="texture">파티클 텍스처</param>
        /// <returns>생성된 ParticleSystem 컴포넌트</returns>
        private ParticleSystem CreateParticleSystem(GameObject parentGO, Texture2D texture)
        {
            if (parentGO == null)
            {
                Debug.LogError("[EnvironmentParticleController] parentGO가 null입니다.");
                return null;
            }

            // ParticleSystem 추가
            var ps = parentGO.AddComponent<ParticleSystem>();

            // Renderer 설정
            var renderer = parentGO.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = CreateParticleMaterial(texture);
                renderer.sortingFudge = 0f;
                renderer.minParticleSize = 0.01f;
                renderer.maxParticleSize = 1f;
            }

            // 기본 Main 모듈 설정
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // World Space

            return ps;
        }

        /// <summary>
        /// 파티클 전용 Material을 생성합니다.
        /// URP/Unlit 셰이더를 사용하여 퍼포먼스를 최적화합니다.
        /// </summary>
        private Material CreateParticleMaterial(Texture2D texture)
        {
            if (texture == null)
            {
                Debug.LogError("[EnvironmentParticleController] texture가 null입니다.");
                return null;
            }

            // URP/Unlit을 먼저 시도, 실패 시 URP/Lit 사용
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                Debug.LogError("[EnvironmentParticleController] URP 셰이더를 찾을 수 없습니다.");
                return null;
            }

            var mat = new Material(shader);
            mat.name = "ParticleMat_" + texture.name;
            mat.mainTexture = texture;

            // Particles/Unlit용 키워드 설정
            if (shader.name.Contains("Unlit"))
            {
                mat.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000; // Transparent
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHATEST_ON");
            }
            else
            {
                // Lit 폴백: 투명 설정
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            return mat;
        }

        // ================================================================
        // 이벤트 핸들러
        // ================================================================

        /// <summary>
        /// WeatherManager.OnWeatherChanged 콜백.
        /// 날씨 변경 시 Rain/Snow 파티클 상태를 갱신합니다.
        /// </summary>
        private void OnWeatherChanged(WeatherManager.WeatherType weatherType)
        {
            // Rain
            if (_rainSystem != null)
            {
                bool shouldRain = weatherType == WeatherManager.WeatherType.Rain;
                if (_rainSystem.gameObject.activeSelf != shouldRain)
                    _rainSystem.gameObject.SetActive(shouldRain);
                if (shouldRain && !_rainSystem.isPlaying)
                    _rainSystem.Play();
                else if (!shouldRain && _rainSystem.isPlaying)
                    _rainSystem.Stop();
            }

            // Snow
            if (_snowSystem != null)
            {
                bool shouldSnow = weatherType == WeatherManager.WeatherType.Snow;
                if (_snowSystem.gameObject.activeSelf != shouldSnow)
                    _snowSystem.gameObject.SetActive(shouldSnow);
                if (shouldSnow && !_snowSystem.isPlaying)
                    _snowSystem.Play();
                else if (!shouldSnow && _snowSystem.isPlaying)
                    _snowSystem.Stop();
            }

            // Dust: 맑은 날씨에 활성화 (비나 눈이 오면 비활성)
            UpdateDustState(weatherType);
        }

        /// <summary>
        /// TimeManager.OnNightStart 콜백.
        /// 밤이 시작되면 Fireflies 활성화를 시도합니다.
        /// </summary>
        private void OnNightStart()
        {
            UpdateFireflyState(true);
        }

        /// <summary>
        /// TimeManager.OnDayStart 콜백.
        /// 낮이 시작되면 Fireflies 비활성화합니다.
        /// </summary>
        private void OnDayStart()
        {
            UpdateFireflyState(false);

            // 낮에는 Dust 활성화 (날씨가 맑을 경우)
            if (_weatherManager != null)
                UpdateDustState(_weatherManager.CurrentWeather);
        }

        /// <summary>
        /// Dust 파티클 상태를 갱신합니다. 맑은 날씨에만 활성화됩니다.
        /// </summary>
        private void UpdateDustState(WeatherManager.WeatherType weather)
        {
            if (_dustSystem == null) return;

            bool shouldDust = (weather == WeatherManager.WeatherType.Clear || weather == WeatherManager.WeatherType.Fog)
                              && (_timeManager == null || !_timeManager.IsNight);

            if (_dustSystem.gameObject.activeSelf != shouldDust)
                _dustSystem.gameObject.SetActive(shouldDust);
            if (shouldDust && !_dustSystem.isPlaying)
                _dustSystem.Play();
            else if (!shouldDust && _dustSystem.isPlaying)
                _dustSystem.Stop();
        }

        /// <summary>
        /// Fireflies 파티클 상태를 갱신합니다.
        /// 밤 + Forest/Grass 바이옴에서만 활성화됩니다.
        /// </summary>
        private void UpdateFireflyState(bool isNight)
        {
            if (_fireflySystem == null) return;

            bool shouldFirefly = isNight && IsInForestOrGrassBiome();

            if (_fireflySystem.gameObject.activeSelf != shouldFirefly)
                _fireflySystem.gameObject.SetActive(shouldFirefly);
            if (shouldFirefly && !_fireflySystem.isPlaying)
                _fireflySystem.Play();
            else if (!shouldFirefly && _fireflySystem.isPlaying)
                _fireflySystem.Stop();
        }

        /// <summary>
        /// 매 프레임 Fireflies 바이옴 상태를 체크하여 업데이트합니다.
        /// </summary>
        private void UpdateFireflyBiomeState()
        {
            if (_fireflySystem == null) return;
            if (!_fireflySystem.gameObject.activeSelf) return;

            bool inBiome = IsInForestOrGrassBiome();
            if (!inBiome && _fireflySystem.isPlaying)
            {
                _fireflySystem.Stop();
                _fireflySystem.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// IBiomeProvider를 캐싱합니다 (매 프레임 FindObjectsByType 방지).
        /// </summary>
        private void CacheBiomeProvider()
        {
            if (_biomeProvider != null) return;
            var monoBehaviors = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            foreach (var mb in monoBehaviors)
            {
                if (mb is IBiomeProvider provider)
                {
                    _biomeProvider = provider;
                    return;
                }
            }
        }

        /// <summary>
        /// 현재 바이옴이 Forest 또는 Grass인지 확인합니다.
        /// 캐시된 IBiomeProvider를 우선 사용합니다.
        /// </summary>
        private bool IsInForestOrGrassBiome()
        {
            // 캐시된 IBiomeProvider 사용 (매 프레임 FindObjectsByType 방지)
            if (_biomeProvider != null)
            {
                string biome = _biomeProvider.GetCurrentBiome();
                if (!string.IsNullOrEmpty(biome))
                {
                    string lower = biome.ToLowerInvariant();
                    return lower.Contains("forest") || lower.Contains("grass");
                }
            }

            // 폴백: 씬 이름 키워드 분석
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLowerInvariant();
            return sceneName.Contains("forest") || sceneName.Contains("grass")
                || sceneName.Contains("field") || sceneName.Contains("plain");
        }

        // ================================================================
        // 초기 상태 적용
        // ================================================================

        /// <summary>
        /// 시작 시 현재 날씨/시간대에 맞는 초기 상태를 적용합니다.
        /// </summary>
        private void ApplyInitialStates()
        {
            // WeatherManager 기반 초기 상태
            if (_weatherManager != null)
            {
                OnWeatherChanged(_weatherManager.CurrentWeather);
            }

            // TimeManager 기반 초기 상태
            if (_timeManager != null)
            {
                if (_timeManager.IsNight)
                    OnNightStart();
                else
                    OnDayStart();
            }
        }

        // ================================================================
        // 공개 API
        // ================================================================

        /// <summary>Rain 파티클 시스템 (외부 접근용)</summary>
        public ParticleSystem RainSystem => _rainSystem;

        /// <summary>Snow 파티클 시스템 (외부 접근용)</summary>
        public ParticleSystem SnowSystem => _snowSystem;

        /// <summary>Fireflies 파티클 시스템 (외부 접근용)</summary>
        public ParticleSystem FireflySystem => _fireflySystem;

        /// <summary>Dust 파티클 시스템 (외부 접근용)</summary>
        public ParticleSystem DustSystem => _dustSystem;

        /// <summary>
        /// 강제로 모든 파티클 상태를 갱신합니다 (테스트/디버그용).
        /// </summary>
        public void RefreshAll()
        {
            if (_weatherManager != null)
                OnWeatherChanged(_weatherManager.CurrentWeather);

            if (_timeManager != null)
            {
                if (_timeManager.IsNight)
                    UpdateFireflyState(true);
                else
                    UpdateFireflyState(false);
            }
        }
    }
}