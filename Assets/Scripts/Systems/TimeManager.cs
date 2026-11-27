using System;
using System.Collections;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C13-01: 게임 시간 관리 싱글톤.
    /// 현실 시간을 게임 시간으로 변환하고, 시/분/주야 상태를 제공합니다.
    /// 씬 기반 싱글톤 (FixMainScene에서 생성)
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }
        static bool _isQuitting = false;

        // NOTE: RuntimeInitializeOnLoadMethod 제거 - FixMainScene에서 씬에 생성하므로 불필요
        // 4중 방어 패턴 불필요 (씬 기반으로 단순화)

        public static TimeManager GetOrCreate()
        {
            if (Instance == null && !_isQuitting)
            {
                var go = new GameObject("[TimeManager]");
                Instance = go.AddComponent<TimeManager>();
                // [QA 수리] EditMode 테스트 GetOrCreate 경로 DDOL 예외 방지 — Play 동작 불변
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }
            return Instance;
        }

        [Header("Time Settings")]
        [SerializeField] private float _timeScale = 60f; // 현실 1초 = 게임 60초 = 1분

        [Header("Sleep Settings")]
        [SerializeField, Tooltip("수면 중 TimeScale 배율 (기본 TimeScale * 이 값)")]
        private float _sleepTimeScaleMultiplier = 10f;

        [Header("Debug")]
        [SerializeField] private bool _verbose;

        // ===== 상태 =====
        // [134차 B-2] 낮 시작(Hour 8) — 기본 0=자정은 밤이라 게임 시작 직후 [133차-P3] 밤 귀가
        // 홀드가 즉시 발동(프로브 전원 "speed=0.0 state=Idle moved=0.00m" 실측 = 정상 동작의 밤 홀드).
        // 걷기 코드 정상 — 문제는 "시작 시각이 밤"이라는 체감이므로 초기 시각만 낮으로 수리.
        private float _gameTime = 8f * 3600f;
        private int _currentDay;
        private int _lastHour = -1;
        private int _lastMinute = -1;
        private bool _lastIsDay = true;

        // ===== 수면 코루틴 =====
        private Coroutine _sleepCoroutine;
        private Action _onSleepComplete;
        private float _originalTimeScale;

        // ===== 공개 프로퍼티 =====

        /// <summary>
        /// 게임 시간(초). [0, 86400) 범위로 정규화되며, 넘치는 일 수는 _currentDay에 누적됩니다.
        /// </summary>
        public float GameTime
        {
            get => _gameTime;
            set
            {
                // FloorToInt를 사용해 양수/음수 모두 올바르게 처리
                // 예: value = -86450 → dayDelta = -2, _currentDay -= 2, _gameTime = 86350
                int dayDelta = Mathf.FloorToInt(value / 86400f);
                _currentDay += dayDelta;
                _gameTime = value - dayDelta * 86400f;
            }
        }

        /// <summary>
        /// 시간 척도. 현실 1초 = 게임 _timeScale초.
        /// 0 이상의 값만 허용됩니다.
        /// </summary>
        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Mathf.Max(0f, value);
        }

        public int Hour => (int)(_gameTime / 3600f) % 24;
        public int Minute => (int)(_gameTime / 60f) % 60;
        public bool IsDay => Hour >= 6 && Hour < 18;
        public bool IsNight => !IsDay;

        /// <summary>
        /// 현재 게임 일차. 전체 일 주기가 완료될 때마다 증가합니다.
        /// </summary>
        public int CurrentDay => _currentDay;

        /// <summary>
        /// 하루 진행률 (0.0 ~ 1.0). 0 = 자정, 0.5 = 정오
        /// </summary>
        public float DayProgress => _gameTime / 86400f;

        // ===== 수면 상태 =====
        public bool IsSleeping { get; private set; }

        /// <summary>
        /// 지정된 시간(게임 시간)만큼 수면을 진행합니다.
        /// 게임 시간을 즉시 점프시키고, 수면 오버레이 지속 시간 동안
        /// TimeScale을 증가시켜 시간을 가속한 후 콜백을 호출합니다.
        /// </summary>
        /// <param name="hours">수면할 게임 시간(시간). 0 이상이어야 합니다.</param>
        /// <param name="onComplete">기상 완료 시 호출될 콜백 (선택)</param>
        public void SleepFor(float hours, Action onComplete = null)
        {
            if (hours < 0f)
            {
                Debug.LogError($"[TimeManager] SleepFor: hours({hours})는 음수일 수 없습니다.");
                return;
            }

            if (IsSleeping)
            {
                if (_verbose) Debug.LogWarning("[TimeManager] 이미 수면 중입니다.");
                return;
            }

            _onSleepComplete = onComplete;
            _originalTimeScale = _timeScale;

            // 수면 중 시간 가속 (게임 시간 점프 후 남은 오버레이 시간 가속)
            _timeScale = Mathf.Max(_timeScale, _timeScale * _sleepTimeScaleMultiplier);

            _sleepCoroutine = StartCoroutine(SleepCoroutine(hours));
        }

        /// <summary>수면 취소/기상. TimeScale을 복원하고 콜백을 호출합니다.</summary>
        public void WakeUp()
        {
            if (!IsSleeping) return;

            if (_sleepCoroutine != null)
            {
                StopCoroutine(_sleepCoroutine);
                _sleepCoroutine = null;
            }

            IsSleeping = false;
            _timeScale = _originalTimeScale;

            var callback = _onSleepComplete;
            _onSleepComplete = null;
            callback?.Invoke();
        }

        // ===== 이벤트 =====
        public event Action<int, int> OnTimeChanged;
        public event Action<bool> OnDayNightChanged;
        public event Action OnNightStart;
        public event Action OnDayStart;

        /// <summary>[A2 이계] 월드 시간 동결 — true 동안 Update 조기 반환(게임시간/일차/주야 이벤트 전부 정지). 귀환 시 false 복원.</summary>
        public static bool IsTimeFrozen { get; set; }

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
            // [134차 B-2] 시작 시각 실측 로그(1회 — 생존 인스턴스 Awake에서만) — 밤 시작 여부 즉시 판별용.
            Debug.Log($"[134차 B-2] 게임 시작 시각 — {Hour}시");

            // [QA 수리] EditMode 테스트에서 GetOrCreate가 Awake를 실행 — DontDestroyOnLoad는 Play 전용이라
            // 편집기에서 InvalidOperationException을 던져 테스트 전체가 OneTimeSetUp 실패. Play 동작 불변.
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            _lastHour = Hour;
            _lastMinute = Minute;
            _lastIsDay = IsDay;
        }

        private void OnApplicationQuit() => _isQuitting = true;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // [A2 이계] 시간 동결 게이트 — 이계 생존 중 월드 게임시간 완전 정지
            // (OnDayStart 미발화 → 급료/공물/배신/자정 판정 전부 안전. 사용자 지시: 귀환 후 변동 없음)
            if (IsTimeFrozen) return;

            GameTime = _gameTime + Time.deltaTime * _timeScale;

            // [63-B] 환경 상태이상 — 밤+비+젖음 → 저체온(갱신형 15s).
            // 허기는 PlayerStatusEffectSystem.TickHunger 내부 적산이 담당 — 이중 발동 금지(계획서 §4#6).
            if (IsNight
                && WeatherManager.Instance != null
                && WeatherManager.Instance.CurrentWeather == WeatherManager.WeatherType.Rain   // [63-B] 내첩 enum 한정
                && PlayerStatusEffectSystem.IsWet)
            {
                PlayerStatusEffectSystem.AddEffect(StatusEffectId.Hypothermia, 15f);
            }

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
                if (currentIsDay)
                    OnDayStart?.Invoke();
                else
                    OnNightStart?.Invoke();
                _lastIsDay = currentIsDay;
            }
        }

        // ===== 수면 코루틴 =====

        private IEnumerator SleepCoroutine(float hours)
        {
            IsSleeping = true;

            // 게임 시간 점프
            float sleepSeconds = hours * 3600f;
            GameTime = _gameTime + sleepSeconds;

            // 수면 오버레이 지속 시간 (게임 시간 → 현실 시간, 0.3~2초 클램프)
            float overlayDuration = Mathf.Clamp(sleepSeconds / _timeScale, 0.3f, 2f);

            if (_verbose)
                Debug.Log($"[TimeManager] 수면 시작: {hours}h ({sleepSeconds}s 게임), 오버레이 {overlayDuration:F2}s");

            yield return new WaitForSeconds(overlayDuration);

            // TimeScale 복원 및 수면 종료
            _timeScale = _originalTimeScale;
            IsSleeping = false;
            _sleepCoroutine = null;

            var callback = _onSleepComplete;
            _onSleepComplete = null;
            callback?.Invoke();

            if (_verbose)
                Debug.Log("[TimeManager] 기상 완료!");
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