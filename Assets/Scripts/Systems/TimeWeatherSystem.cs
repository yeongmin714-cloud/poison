using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.Systems
{
    /// <summary>
    /// 시간/날씨 시스템 - BotW 스타일 미니맵 위 시간/날씨 표시용
    /// </summary>
    public class TimeWeatherSystem : MonoBehaviour
    {
        public static TimeWeatherSystem Instance { get; private set; }

        public enum WeatherType
        {
            Clear,      // 맑음
            Cloudy,     // 흐림
            Rain,       // 비
            Storm,      // 폭풍
            Snow,       // 눈
            Fog,        // 안개
            Night       // 밤 (시간 기반)
        }

        [Header("Time Settings")]
        [SerializeField] private float _dayLengthMinutes = 20f; // 하루 = 20분
        [SerializeField] private float _startTimeOfDay = 0.25f; // 0=자정, 0.25=아침 6시
        [SerializeField] private float _timeScale = 1f;

        [Header("Weather Settings")]
        [SerializeField] private WeatherType _currentWeather = WeatherType.Clear;
        [SerializeField] private float _weatherChangeInterval = 300f; // 5분마다 날씨 변경 체크
        [SerializeField] private List<WeatherType> _possibleWeathers = new List<WeatherType>
        {
            WeatherType.Clear, WeatherType.Clear, WeatherType.Clear,
            WeatherType.Cloudy, WeatherType.Cloudy,
            WeatherType.Rain,
            WeatherType.Storm,
            WeatherType.Snow,
            WeatherType.Fog
        };

        public float TimeOfDay { get; private set; }
        public WeatherType CurrentWeather => _currentWeather;
        public float DayLengthSeconds => _dayLengthMinutes * 60f;

        private float _weatherTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            TimeOfDay = _startTimeOfDay;
        }

        private void Update()
        {
            // 시간 진행
            float daySeconds = _dayLengthMinutes * 60f;
            TimeOfDay += (Time.deltaTime * _timeScale) / daySeconds;
            TimeOfDay %= 1f;

            // 밤 시간대 자동 날씨 Night 설정
            if (TimeOfDay > 0.75f || TimeOfDay < 0.23f) // 밤 10시 ~ 새벽 5시
            {
                if (_currentWeather != WeatherType.Night)
                    _currentWeather = WeatherType.Night;
            }
            else if (_currentWeather == WeatherType.Night)
            {
                // 낮이 되면 맑음으로 복귀
                _currentWeather = WeatherType.Clear;
            }

            // 날씨 변경 타이머
            if (_currentWeather != WeatherType.Night)
            {
                _weatherTimer += Time.deltaTime;
                if (_weatherTimer >= _weatherChangeInterval)
                {
                    _weatherTimer = 0f;
                    TryChangeWeather();
                }
            }
        }

        private void TryChangeWeather()
        {
            // 현재 날씨 제외하고 랜덤 선택
            var candidates = new List<WeatherType>(_possibleWeathers);
            candidates.Remove(_currentWeather);
            
            if (candidates.Count > 0)
            {
                _currentWeather = candidates[Random.Range(0, candidates.Count)];
            }
        }

        /// <summary>
        /// 강제 날씨 설정 (퀘스트, 지역 등)
        /// </summary>
        public void SetWeather(WeatherType weather, float duration = 0f)
        {
            _currentWeather = weather;
            if (duration > 0f)
            {
                StartCoroutine(ResetWeatherAfterDelay(duration));
            }
        }

        private System.Collections.IEnumerator ResetWeatherAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_currentWeather != WeatherType.Night)
                _currentWeather = WeatherType.Clear;
        }

        /// <summary>
        /// 시간 배율 설정
        /// </summary>
        public void SetTimeScale(float scale)
        {
            _timeScale = Mathf.Max(0f, scale);
        }

        /// <summary>
        /// 현재 시간이 밤인지 확인
        /// </summary>
        public bool IsNight => TimeOfDay > 0.75f || TimeOfDay < 0.23f;

        /// <summary>
        /// 현재 시간이 낮인지 확인
        /// </summary>
        public bool IsDay => !IsNight;
    }
}