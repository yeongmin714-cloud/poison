using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// G1-05: Weather System — Singleton that manages 5 weather types with
    /// random transitions, smooth fog/lighting interpolation, and wind zone control.
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static WeatherManager _instance;
        private static bool _instanceQuitting;

        public static WeatherManager Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("WeatherManager");
                    _instance = go.AddComponent<WeatherManager>();
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

        // ================================================================
        // Weather Type Enum
        // ================================================================

        public enum WeatherType
        {
            Clear,
            Rain,
            Snow,
            Fog,
            StrongWind
        }

        // ================================================================
        // Events
        // ================================================================

        /// <summary>Fired when weather changes. Parameter = new weather type.</summary>
        public event Action<WeatherType> OnWeatherChanged;

        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("Timing")]
        [SerializeField] private float _minDuration = 15f;
        [SerializeField] private float _maxDuration = 180f;

        [Header("References (optional)")]
        [SerializeField] private Light _directionalLight;

        [Header("Fog")]
        [SerializeField] private float _clearFogDensity = 0.008f;
        [SerializeField] private float _foggyFogDensity = 0.02f;

        [Header("Lighting")]
        [SerializeField] private float _clearLightIntensity = 1.5f;
        [SerializeField] private float _rainLightIntensity = 0.8f;

        [Header("Wind")]
        [SerializeField] private float _windZoneStrength = 3f;

        [Header("Smooth Transition")]
        [SerializeField] private float _transitionDuration = 3f;

        [Header("Weather Weights (must sum to 100)")]
        [SerializeField] private int _clearWeight = 40;
        [SerializeField] private int _rainWeight = 25;
        [SerializeField] private int _fogWeight = 20;
        [SerializeField] private int _windWeight = 10;
        [SerializeField] private int _snowWeight = 5;

        // ================================================================
        // State
        // ================================================================

        private WeatherType _currentWeather = WeatherType.Clear;
        private float _weatherTimer;
        private float _transitionProgress = 1f; // 1 = fully transitioned
        private float _previousFogDensity;
        private float _previousLightIntensity;
        private float _targetFogDensity;
        private float _targetLightIntensity;
        private WeatherType _previousWeather;
        private WindZone _windZone;
        private bool _hasDirectionalLight;

        // ================================================================
        // Public Properties
        // ================================================================

        public WeatherType CurrentWeather
        {
            get => _currentWeather;
            private set
            {
                if (_currentWeather == value) return;
                _previousWeather = _currentWeather;
                _currentWeather = value;
                StartTransition();
                OnWeatherChanged?.Invoke(value);
            }
        }

        public float WeatherTimer => _weatherTimer;
        public float TransitionProgress => _transitionProgress;
        public WeatherType PreviousWeather => _previousWeather;
        public bool IsTransitioning => _transitionProgress < 1f;

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
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _instanceQuitting = true;
        }

        private void Start()
        {
            // Resolve directional light if not set
            if (_directionalLight == null)
            {
                _directionalLight = FindObjectOfType<Light>();
                // Try to find a light tagged as directional
                var lights = FindObjectsOfType<Light>();
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        _directionalLight = l;
                        break;
                    }
                }
            }

            _hasDirectionalLight = _directionalLight != null;

            // Resolve wind zone
            _windZone = FindObjectOfType<WindZone>();

            // Store initial fog/light values for transition targets
            _targetFogDensity = _clearFogDensity;
            _previousFogDensity = RenderSettings.fogDensity;
            if (_hasDirectionalLight)
            {
                _targetLightIntensity = _clearLightIntensity;
                _previousLightIntensity = _directionalLight.intensity;
            }

            // Start with Clear weather
            _currentWeather = WeatherType.Clear;
            _weatherTimer = GetRandomDuration(WeatherType.Clear);
            ApplyWeatherImmediate(WeatherType.Clear);
        }

        private void Update()
        {
            if (_transitionProgress < 1f)
            {
                _transitionProgress += Time.deltaTime / _transitionDuration;
                if (_transitionProgress > 1f)
                    _transitionProgress = 1f;

                // Lerp fog density
                RenderSettings.fogDensity = Mathf.Lerp(
                    _previousFogDensity, _targetFogDensity, _transitionProgress);

                // Lerp light intensity
                if (_hasDirectionalLight)
                {
                    _directionalLight.intensity = Mathf.Lerp(
                        _previousLightIntensity, _targetLightIntensity, _transitionProgress);
                }

                // Lerp wind zone strength
                if (_windZone != null)
                {
                    float targetWind = _currentWeather == WeatherType.StrongWind
                        ? _windZoneStrength
                        : 0f;
                    _windZone.windMain = Mathf.Lerp(
                        _previousWeather == WeatherType.StrongWind ? _windZoneStrength : 0f,
                        targetWind,
                        _transitionProgress);
                }
            }

            // Count down weather timer
            _weatherTimer -= Time.deltaTime;

            if (_weatherTimer <= 0f && _transitionProgress >= 1f)
            {
                TransitionToNextWeather();
            }
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>Force a specific weather type, skipping the timer.</summary>
        public void SetWeather(WeatherType weather)
        {
            _previousFogDensity = RenderSettings.fogDensity;
            _previousLightIntensity = _hasDirectionalLight
                ? _directionalLight.intensity
                : 0f;

            if (_windZone != null)
            {
                _previousWeather = _currentWeather;
            }

            CurrentWeather = weather;
            _weatherTimer = GetRandomDuration(weather);
        }

        /// <summary>Get the duration range for a weather type (min, max).</summary>
        public Vector2 GetDurationRange(WeatherType weather)
        {
            return weather switch
            {
                WeatherType.Clear => new Vector2(60f, 180f),
                WeatherType.Rain => new Vector2(30f, 90f),
                WeatherType.Snow => new Vector2(40f, 120f),
                WeatherType.Fog => new Vector2(20f, 60f),
                WeatherType.StrongWind => new Vector2(15f, 40f),
                _ => new Vector2(60f, 180f),
            };
        }

        // ================================================================
        // Internal
        // ================================================================

        private void StartTransition()
        {
            _transitionProgress = 0f;

            // Set target values
            _previousFogDensity = RenderSettings.fogDensity;
            _previousLightIntensity = _hasDirectionalLight
                ? _directionalLight.intensity
                : 0f;

            _targetFogDensity = GetTargetFogDensity(_currentWeather);
            _targetLightIntensity = GetTargetLightIntensity(_currentWeather);
        }

        private void ApplyWeatherImmediate(WeatherType weather)
        {
            RenderSettings.fogDensity = GetTargetFogDensity(weather);
            if (_hasDirectionalLight)
                _directionalLight.intensity = GetTargetLightIntensity(weather);
            if (_windZone != null)
                _windZone.windMain = weather == WeatherType.StrongWind ? _windZoneStrength : 0f;
        }

        private float GetTargetFogDensity(WeatherType weather)
        {
            return weather switch
            {
                WeatherType.Fog => _foggyFogDensity,
                WeatherType.Rain => _clearFogDensity * 1.5f,
                _ => _clearFogDensity,
            };
        }

        private float GetTargetLightIntensity(WeatherType weather)
        {
            return weather switch
            {
                WeatherType.Rain => _rainLightIntensity,
                WeatherType.Fog => _rainLightIntensity,
                _ => _clearLightIntensity,
            };
        }

        private void TransitionToNextWeather()
        {
            WeatherType next = PickRandomWeather();

            // Store previous values for smooth transition
            _previousFogDensity = RenderSettings.fogDensity;
            _previousLightIntensity = _hasDirectionalLight
                ? _directionalLight.intensity
                : 0f;
            if (_windZone != null)
            {
                _previousWeather = _currentWeather;
            }

            _currentWeather = next;
            _weatherTimer = GetRandomDuration(next);
            _transitionProgress = 0f;

            _targetFogDensity = GetTargetFogDensity(next);
            _targetLightIntensity = GetTargetLightIntensity(next);

            OnWeatherChanged?.Invoke(next);
        }

        private WeatherType PickRandomWeather()
        {
            int totalWeight = _clearWeight + _rainWeight + _fogWeight + _windWeight + _snowWeight;
            int roll = UnityEngine.Random.Range(0, totalWeight);

            if (roll < _clearWeight) return WeatherType.Clear;
            roll -= _clearWeight;
            if (roll < _rainWeight) return WeatherType.Rain;
            roll -= _rainWeight;
            if (roll < _fogWeight) return WeatherType.Fog;
            roll -= _fogWeight;
            if (roll < _windWeight) return WeatherType.StrongWind;

            return WeatherType.Snow;
        }

        private float GetRandomDuration(WeatherType weather)
        {
            Vector2 range = GetDurationRange(weather);
            return UnityEngine.Random.Range(range.x, range.y);
        }

        // ================================================================
        // Editor helpers (accessed via reflection in tests)
        // ================================================================

        internal void SetLightReference(Light light)
        {
            _directionalLight = light;
            _hasDirectionalLight = light != null;
        }

        internal void SetWindZone(WindZone zone)
        {
            _windZone = zone;
        }

        internal void SetTimer(float value)
        {
            _weatherTimer = value;
        }

        internal void SetTransitionProgress(float value)
        {
            _transitionProgress = value;
        }
    }
}