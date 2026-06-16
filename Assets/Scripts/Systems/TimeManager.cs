using System;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C13-01: 게임 시간 관리 싱글톤.
    /// 현실 시간을 게임 시간으로 변환하고, 시/분/주야 상태를 제공합니다.
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        [Header("Time Settings")]
        [SerializeField] private float _timeScale = 60f; // 현실 1초 = 게임 60초 = 1분

        [Header("Debug")]
        [SerializeField] private bool _verbose;

        // ===== 상태 =====
        private float _gameTime;
        private int _lastHour = -1;
        private int _lastMinute = -1;
        private bool _lastIsDay = true;

        // ===== 공개 프로퍼티 =====

        public float GameTime
        {
            get => _gameTime;
            set => _gameTime = Mathf.Clamp(value, 0f, 86400f);
        }

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = value;
        }

        public int Hour => (int)(_gameTime / 3600f) % 24;
        public int Minute => (int)(_gameTime / 60f) % 60;
        public bool IsDay => Hour >= 6 && Hour < 18;
        public bool IsNight => !IsDay;

        /// <summary>
        /// 하루 진행률 (0.0 ~ 1.0). 0 = 자정, 0.5 = 정오
        /// </summary>
        public float DayProgress => (_gameTime % 86400f) / 86400f;

        // ===== 이벤트 =====
        public event Action<int, int> OnTimeChanged;
        public event Action<bool> OnDayNightChanged;

        // ===== 싱글톤 =====

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (_verbose) Debug.LogWarning($"[TimeManager] 중복 인스턴스 파괴: {gameObject.name}");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _lastHour = Hour;
            _lastMinute = Minute;
            _lastIsDay = IsDay;
        }

        private void Update()
        {
            _gameTime += Time.deltaTime * _timeScale;

            // 0~86400 순환
            if (_gameTime >= 86400f)
                _gameTime -= 86400f;
            if (_gameTime < 0f)
                _gameTime += 86400f;

            // 시간/분 변경 감지
            int currentHour = Hour;
            int currentMinute = Minute;
            bool currentIsDay = IsDay;

            if (currentHour != _lastHour || currentMinute != _lastMinute)
            {
                OnTimeChanged?.Invoke(currentHour, currentMinute);
                _lastHour = currentHour;
                _lastMinute = currentMinute;
            }

            if (currentIsDay != _lastIsDay)
            {
                OnDayNightChanged?.Invoke(currentIsDay);
                _lastIsDay = currentIsDay;
            }
        }

        // ===== 유틸리티 =====

        /// <summary>
        /// 현재 시간을 "HH:MM" 형식으로 반환
        /// </summary>
        public string GetFormattedTime()
        {
            return $"{Hour:D2}:{Minute:D2}";
        }
    }
}
