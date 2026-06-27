using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C13-02/03 + G3-01: 주야간 조명 및 환경 제어 시스템.
    /// TimeManager의 DayProgress에 따라 태양/달/주변광/안개/스카이박스를 변경합니다.
    /// 
    /// [G3-01] 개선 사항:
    ///   - Moon Light 추가 (두 번째 Light, 밤에만 활성화, 차가운 청백색)
    ///   - Skybox Lerp 강화 (Procedural Skybox 파라미터 부드럽게 전환)
    ///   - WeatherManager 연동 (비 오는 날 낮/밤 전환 효과 보정)
    ///   - SmoothStep 기반 부드러운 전환 곡선
    /// </summary>
    [RequireComponent(typeof(TimeManager))]
    public class DayNightCycle : MonoBehaviour
    {
        // ================================================================
        // Sun Reference
        // ================================================================

        [Header("Sun Reference")]
        [SerializeField] private Light _sunLight;

        [Header("Sun Rotation")]
        [SerializeField] private float _longitude = 30f;

        [Header("Sun Colors")]
        [SerializeField] private Color _noonColor = new Color(1f, 0.95f, 0.8f);
        [SerializeField] private Color _eveningColor = new Color(1f, 0.5f, 0.2f);
        [SerializeField] private Color _nightColor = new Color(0.1f, 0.1f, 0.3f);

        [Header("Sun Intensity")]
        [SerializeField] private float _noonIntensity = 1f;
        [SerializeField] private float _eveningIntensity = 0.3f;
        [SerializeField] private float _nightIntensity = 0f;

        [Header("Shadow Strength")]
        [SerializeField] private float _noonShadowStrength = 1f;
        [SerializeField] private float _nightShadowStrength = 0f;

        // ================================================================
        // Moon Light (G3-01)
        // ================================================================

        [Header("Moon Light (G3-01)")]
        [SerializeField] private Light _moonLight;
        [SerializeField] private Color _moonLightColor = new Color(0.6f, 0.7f, 1.0f); // 차가운 청백색
        [SerializeField] private float _moonIntensity = 0.2f;
        [SerializeField] private float _moonShadowStrength = 0.3f;

        // ================================================================
        // Ambient / Fog
        // ================================================================

        [Header("Ambient Light")]
        [SerializeField] private Color _dayAmbient = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color _nightAmbient = new Color(0.05f, 0.05f, 0.1f);

        [Header("Fog Settings")]
        [SerializeField] private Color _dayFogColor = new Color(0.7f, 0.75f, 0.8f);
        [SerializeField] private Color _nightFogColor = new Color(0.05f, 0.05f, 0.1f);
        [SerializeField] private float _dayFogDensity = 0.01f;
        [SerializeField] private float _nightFogDensity = 0.03f;

        // ================================================================
        // Skybox Lerp (G3-01)
        // ================================================================

        [Header("Skybox Lerp (G3-01)")]
        [SerializeField] private Material _skyboxMaterialOverride; // 미지정 시 RenderSettings.skybox 사용
        [SerializeField] private Color _daySkyTint = new Color(0.4f, 0.6f, 0.9f);
        [SerializeField] private Color _nightSkyTint = new Color(0.05f, 0.05f, 0.15f);
        [SerializeField] private Color _dayGroundColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color _nightGroundColor = new Color(0.1f, 0.1f, 0.12f);
        [SerializeField] private float _dayExposure = 1.0f;
        [SerializeField] private float _nightExposure = 0.5f;
        [SerializeField] private float _dayAtmoThickness = 0.8f;
        [SerializeField] private float _nightAtmoThickness = 1.2f;
        [SerializeField] private float _daySunSize = 0.04f;
        [SerializeField] private float _nightSunSize = 0.02f;

        // ================================================================
        // Weather Integration (G3-01)
        // ================================================================

        [Header("Weather Integration (G3-01)")]
        [SerializeField, Range(0f, 1f)] private float _rainLightMultiplier = 0.6f;

        // ================================================================
        // Private State
        // ================================================================

        private TimeManager _timeManager;
        private Light _resolvedSun;
        private Light _resolvedMoon;
        private bool _hasSun;
        private bool _hasMoon;
        private WeatherManager _weatherManager;
        private Material _resolvedSkybox;
        private bool _hasSkybox;
        private float _sunAngle; // 현재 태양 각도 (디버깅/참조용)

        // ================================================================
        // Cached Constants (GC 방지)
        // ================================================================

        private static readonly Color RainColorTint = new Color(0.7f, 0.75f, 0.9f);

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void Start()
        {
            _timeManager = TimeManager.Instance;
            if (_timeManager == null)
            {
                Debug.LogError("[DayNightCycle] TimeManager.Instance가 없습니다.");
                enabled = false;
                return;
            }

            // Sun 참조 해결
            ResolveSun();

            // Moon 참조 해결
            ResolveMoon();

            // Skybox Material 참조 해결
            ResolveSkybox();

            // WeatherManager 참조 (없어도 무방)
            ResolveWeatherManager();

            // 초기 상태 즉시 적용
            ApplyImmediate();
        }

        private void Update()
        {
            if (_timeManager == null) return;

            float dayProgress = _timeManager.DayProgress; // 0.0 ~ 1.0

            // ===== 1. 태양 회전 (Cos 기반) =====
            // dayFactor: 0(자정) → 0.5(아침/저녁) → 1(정오)
            float cosValue = Mathf.Cos(dayProgress * Mathf.PI * 2f);
            float dayFactor = (1f - cosValue) / 2f;

            // SmoothStep 적용: dayFactor의 S-커브 보정
            // 자정/정오 부근에서 더 부드럽게, 중간 전환 구간에서 더 가파르게
            float smoothDayFactor = Mathf.SmoothStep(0f, 1f, dayFactor);

            // 천체 회전 적용
            UpdateCelestialRotation(smoothDayFactor);

            // ===== 2. 날씨 영향 계산 =====
            bool isRaining = IsRaining();
            float weatherMultiplier = isRaining ? _rainLightMultiplier : 1f;

            // ===== 3. SmoothStep 기반 색상/강도 보간 =====
            UpdateSunProperties(smoothDayFactor, weatherMultiplier);
            UpdateMoonProperties(smoothDayFactor);

            // ===== 4. 환경 설정 (Cos 기반 SmoothStep 보간) =====
            // ambientT: 0(자정, 완전 밤) → 1(정오, 완전 낮)
            float ambientT = (-cosValue + 1f) / 2f;
            float smoothAmbientT = Mathf.SmoothStep(0f, 1f, ambientT);

            RenderSettings.ambientLight = Color.Lerp(_nightAmbient, _dayAmbient, smoothAmbientT);
            RenderSettings.fogColor = Color.Lerp(_nightFogColor, _dayFogColor, smoothAmbientT);
            RenderSettings.fogDensity = Mathf.Lerp(_nightFogDensity, _dayFogDensity, smoothAmbientT);
            RenderSettings.fog = true;

            // ===== 5. Skybox Lerp =====
            UpdateSkybox(smoothAmbientT);
        }

        // ================================================================
        // Sun & Moon Rotation
        // ================================================================

        /// <summary>
        /// 태양과 달의 회전을 업데이트합니다.
        /// 태양: X축 0°(정오) ~ 180°(자정)
        /// 달: 태양의 반대 방향 (밤에만 보임)
        /// </summary>
        private void UpdateCelestialRotation(float smoothDayFactor)
        {
            // 태양 X축 회전: 0°(정오) ~ 180°(자정)
            _sunAngle = Mathf.Lerp(180f, 0f, smoothDayFactor);

            if (_hasSun && _resolvedSun != null)
            {
                _resolvedSun.transform.eulerAngles = new Vector3(
                    _sunAngle,
                    _longitude,
                    0f
                );
            }

            if (_hasMoon && _resolvedMoon != null)
            {
                // 달은 태양의 반대 방향 (180° 차이)
                float moonAngle = (_sunAngle + 180f) % 360f;
                _resolvedMoon.transform.eulerAngles = new Vector3(
                    moonAngle,
                    _longitude + 180f,
                    0f
                );
            }
        }

        // ================================================================
        // Sun Properties (SmoothStep 기반)
        // ================================================================

        /// <summary>
        /// 태양의 색상, 강도, 그림자 강도를 SmoothStep 기반으로 보간합니다.
        /// 날씨(비)에 따른 보정도 함께 적용합니다.
        /// </summary>
        private void UpdateSunProperties(float smoothDayFactor, float weatherMultiplier)
        {
            if (!_hasSun || _resolvedSun == null) return;

            Color sunColor;
            float sunIntensity;
            float shadowStrength;

            if (smoothDayFactor < 0.25f)
            {
                // 자정 → 아침 (밤 → 새벽) - SmoothStep 적용
                float t = smoothDayFactor / 0.25f;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                sunColor = Color.Lerp(_nightColor, _eveningColor, smoothT);
                sunIntensity = Mathf.Lerp(_nightIntensity, _eveningIntensity, smoothT);
                shadowStrength = Mathf.Lerp(_nightShadowStrength, _noonShadowStrength, smoothT);
            }
            else if (smoothDayFactor < 0.5f)
            {
                // 아침 → 정오
                float t = (smoothDayFactor - 0.25f) / 0.25f;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                sunColor = Color.Lerp(_eveningColor, _noonColor, smoothT);
                sunIntensity = Mathf.Lerp(_eveningIntensity, _noonIntensity, smoothT);
                shadowStrength = Mathf.Lerp(_nightShadowStrength, _noonShadowStrength, smoothT);
            }
            else if (smoothDayFactor < 0.75f)
            {
                // 정오 → 저녁
                float t = (smoothDayFactor - 0.5f) / 0.25f;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                sunColor = Color.Lerp(_noonColor, _eveningColor, smoothT);
                sunIntensity = Mathf.Lerp(_noonIntensity, _eveningIntensity, smoothT);
                shadowStrength = Mathf.Lerp(_noonShadowStrength, _nightShadowStrength, smoothT);
            }
            else
            {
                // 저녁 → 자정
                float t = (smoothDayFactor - 0.75f) / 0.25f;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                sunColor = Color.Lerp(_eveningColor, _nightColor, smoothT);
                sunIntensity = Mathf.Lerp(_eveningIntensity, _nightIntensity, smoothT);
                shadowStrength = Mathf.Lerp(_noonShadowStrength, _nightShadowStrength, smoothT);
            }

            // 비 오는 날: 강도 보정 (밤에도 영향)
            if (IsRaining())
            {
                sunIntensity *= _rainLightMultiplier;
                // 비 오는 날 태양 색상 약간 차갑게
                sunColor = Color.Lerp(sunColor, RainColorTint, 0.3f);
            }

            _resolvedSun.color = sunColor;
            _resolvedSun.intensity = sunIntensity;

            // Shadow Strength (Unity 2019+)
#if UNITY_2019_1_OR_NEWER
            _resolvedSun.shadowStrength = shadowStrength;
#endif

            // 태양이 지평선 아래(밤)면 비활성화
            _resolvedSun.enabled = _sunAngle > 5f && _sunAngle < 175f;
        }

        // ================================================================
        // Moon Properties (G3-01)
        // ================================================================

        /// <summary>
        /// Moon Light의 활성화/비활성화, 색상, 강도를 제어합니다.
        /// 밤(dayFactor 0~0.25, 0.75~1.0)에만 활성화되며 차가운 청백색을 사용합니다.
        /// </summary>
        private void UpdateMoonProperties(float smoothDayFactor)
        {
            if (!_hasMoon || _resolvedMoon == null) return;

            // 밤 판정: dayFactor 0~0.25(자정→새벽) 또는 0.75~1.0(저녁→자정)
            bool isNight = smoothDayFactor < 0.25f || smoothDayFactor > 0.75f;

            if (isNight)
            {
                _resolvedMoon.enabled = true;

                // Moon 강도: 한밤중(smoothDayFactor=0 or 1)에 최대, 새벽/저녁에 최소
                float moonFactor;
                if (smoothDayFactor < 0.25f)
                {
                    // 자정(0) → 새벽(0.25): 1 → 0
                    moonFactor = 1f - (smoothDayFactor / 0.25f);
                }
                else
                {
                    // 저녁(0.75) → 자정(1): 0 → 1
                    moonFactor = (smoothDayFactor - 0.75f) / 0.25f;
                }

                // SmoothStep으로 문 등장/퇴장 부드럽게
                float smoothMoonFactor = Mathf.SmoothStep(0f, 1f, moonFactor);

                // 비 오는 날: 달빛 약하게
                float weatherMod = IsRaining() ? _rainLightMultiplier : 1f;

                _resolvedMoon.color = _moonLightColor;
                _resolvedMoon.intensity = _moonIntensity * smoothMoonFactor * weatherMod;
                _resolvedMoon.shadowStrength = _moonShadowStrength * smoothMoonFactor;

                // Moon Light 방향: 태양 반대 방향 (UpdateCelestialRotation에서 설정)
            }
            else
            {
                // 낮: Moon 비활성화
                _resolvedMoon.enabled = false;
            }
        }

        // ================================================================
        // Skybox Lerp (G3-01)
        // ================================================================

        /// <summary>
        /// Procedural Skybox의 파라미터를 부드럽게 전환합니다.
        /// _SkyTint, _GroundColor, _Exposure, _AtmosphereThickness, _SunSize
        /// </summary>
        private void UpdateSkybox(float smoothAmbientT)
        {
            if (!_hasSkybox || _resolvedSkybox == null) return;

            string shaderName = _resolvedSkybox.shader != null ? _resolvedSkybox.shader.name : "";
            if (shaderName != "Skybox/Procedural") return;

            // SkyTint: 낮(밝은 파랑) ↔ 밤(어두운 청색)
            _resolvedSkybox.SetColor("_SkyTint",
                Color.Lerp(_nightSkyTint, _daySkyTint, smoothAmbientT));

            // GroundColor: 낮(회색) ↔ 밤(어두운 회색)
            _resolvedSkybox.SetColor("_GroundColor",
                Color.Lerp(_nightGroundColor, _dayGroundColor, smoothAmbientT));

            // Exposure: 낮(밝음) ↔ 밤(어두움)
            _resolvedSkybox.SetFloat("_Exposure",
                Mathf.Lerp(_nightExposure, _dayExposure, smoothAmbientT));

            // AtmosphereThickness: 낮(얇음) ↔ 밤(두꺼움)
            _resolvedSkybox.SetFloat("_AtmosphereThickness",
                Mathf.Lerp(_nightAtmoThickness, _dayAtmoThickness, smoothAmbientT));

            // SunSize: 낮(크게) ↔ 밤(작게)
            _resolvedSkybox.SetFloat("_SunSize",
                Mathf.Lerp(_nightSunSize, _daySunSize, smoothAmbientT));
        }

        // ================================================================
        // Weather Integration (G3-01)
        // ================================================================

        /// <summary>
        /// 현재 날씨가 Rain인지 확인합니다.
        /// WeatherManager가 없으면 false를 반환합니다.
        /// </summary>
        private bool IsRaining()
        {
            if (_weatherManager == null) return false;

            WeatherManager.WeatherType weather = _weatherManager.CurrentWeather;
            return weather == WeatherManager.WeatherType.Rain;
        }

        /// <summary>
        /// 날씨 변경 시 호출됩니다 (OnWeatherChanged 이벤트 구독).
        /// 비가 오면 낮/밤 전환 효과에 보정을 가합니다.
        /// </summary>
        private void OnWeatherChanged(WeatherManager.WeatherType weatherType)
        {
            if (weatherType == WeatherManager.WeatherType.Rain)
            {
                Debug.Log("[DayNightCycle] 비 오는 날 낮/밤 전환 보정 적용.");
            }
        }

        // ================================================================
        // 초기 상태 즉시 적용
        // ================================================================

        /// <summary>
        /// Start()에서 초기 상태를 즉시 적용합니다.
        /// </summary>
        private void ApplyImmediate()
        {
            if (_timeManager == null) return;

            float dayProgress = _timeManager.DayProgress;
            float cosValue = Mathf.Cos(dayProgress * Mathf.PI * 2f);
            float dayFactor = (1f - cosValue) / 2f;
            float smoothDayFactor = Mathf.SmoothStep(0f, 1f, dayFactor);
            float ambientT = (-cosValue + 1f) / 2f;
            float smoothAmbientT = Mathf.SmoothStep(0f, 1f, ambientT);

            // 천체 회전
            UpdateCelestialRotation(smoothDayFactor);

            // 태양 속성
            UpdateSunProperties(smoothDayFactor, 1f);

            // Moon 속성
            UpdateMoonProperties(smoothDayFactor);

            // 환경 설정
            RenderSettings.ambientLight = Color.Lerp(_nightAmbient, _dayAmbient, smoothAmbientT);
            RenderSettings.fogColor = Color.Lerp(_nightFogColor, _dayFogColor, smoothAmbientT);
            RenderSettings.fogDensity = Mathf.Lerp(_nightFogDensity, _dayFogDensity, smoothAmbientT);
            RenderSettings.fog = true;

            // Skybox
            UpdateSkybox(smoothAmbientT);
        }

        // ================================================================
        // 참조 해결
        // ================================================================

        private void ResolveSun()
        {
            if (_sunLight != null)
            {
                _resolvedSun = _sunLight;
                _hasSun = true;
            }
            else
            {
                var found = FindObjectOfType<Light>();
                if (found != null && found.type == LightType.Directional)
                {
                    _resolvedSun = found;
                    _hasSun = true;
                    _sunLight = found;
                }
                else
                {
                    Debug.LogWarning("[DayNightCycle] Directional Light를 찾을 수 없습니다.");
                    _hasSun = false;
                }
            }
        }

        private void ResolveMoon()
        {
            if (_moonLight != null)
            {
                _resolvedMoon = _moonLight;
                _hasMoon = true;
            }
            else
            {
                Debug.LogWarning("[DayNightCycle] Moon Light가 설정되지 않았습니다. Moon 기능이 비활성화됩니다.");
                _hasMoon = false;
            }
        }

        private void ResolveSkybox()
        {
            if (_skyboxMaterialOverride != null)
            {
                _resolvedSkybox = _skyboxMaterialOverride;
                _hasSkybox = true;
            }
            else if (RenderSettings.skybox != null)
            {
                _resolvedSkybox = RenderSettings.skybox;
                _hasSkybox = true;
            }
            else
            {
                Debug.LogWarning("[DayNightCycle] Skybox Material을 찾을 수 없습니다. Skybox Lerp가 비활성화됩니다.");
                _hasSkybox = false;
            }
        }

        private void ResolveWeatherManager()
        {
            _weatherManager = WeatherManager.Instance;
            if (_weatherManager != null)
            {
                _weatherManager.OnWeatherChanged += OnWeatherChanged;
                Debug.Log("[DayNightCycle] WeatherManager와 연동되었습니다.");
            }
            else
            {
                Debug.Log("[DayNightCycle] WeatherManager가 없습니다. 날씨 연동 없이 작동합니다.");
            }
        }

        // ================================================================
        // Cleanup
        // ================================================================

        private void OnDestroy()
        {
            // WeatherManager 이벤트 구독 해제
            if (_weatherManager != null)
            {
                _weatherManager.OnWeatherChanged -= OnWeatherChanged;
            }
        }
    }
}