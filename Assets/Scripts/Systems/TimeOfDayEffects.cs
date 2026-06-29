using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 41.1: 시간대별 게임플레이 효과 관리.
    /// TimeManager의 시간 정보를 기반으로 TimeOfDay를 판별하고,
    /// 은신 보너스, 시야 범위, NPC 활동량 등을 시간대에 따라 조정합니다.
    /// 
    /// 시간대 구분:
    ///   Dawn    (새벽)   4~6시   — 시야 감소, 은신 보너스 +20%
    ///   Day     (낮)     6~18시  — 기본 상태, NPC 활동 최대
    ///   Evening (저녁)  18~20시  — 은신 보너스 +10%, NPC 귀가 시작
    ///   Night   (밤)    20~4시   — 은신 보너스 +40%, 야간 몬스터 출현
    /// </summary>
    public class TimeOfDayEffects : MonoBehaviour
    {
        // ================================================================
        // Enums
        // ================================================================

        public enum TimeOfDay
        {
            Dawn,     // 04:00 ~ 05:59
            Day,      // 06:00 ~ 17:59
            Evening,  // 18:00 ~ 19:59
            Night     // 20:00 ~ 03:59
        }

        // ================================================================
        // Singleton
        // ================================================================

        public static TimeOfDayEffects Instance { get; private set; }

        // ================================================================
        // Events
        // ================================================================

        /// <summary>시간대가 변경될 때 발생합니다.</summary>
        public event Action<TimeOfDay> OnTimeOfDayChanged;

        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("Stealth Bonuses (additive)")]
        [SerializeField, Range(0f, 1f), Tooltip("새벽 은신 보너스 (+20%)")]
        private float _dawnStealthBonus = 0.20f;

        [SerializeField, Range(0f, 1f), Tooltip("낮 은신 보너스 (기본)")]
        private float _dayStealthBonus = 0f;

        [SerializeField, Range(0f, 1f), Tooltip("저녁 은신 보너스 (+10%)")]
        private float _eveningStealthBonus = 0.10f;

        [SerializeField, Range(0f, 1f), Tooltip("밤 은신 보너스 (+40%)")]
        private float _nightStealthBonus = 0.40f;

        [Header("Vision Range Multipliers")]
        [SerializeField, Range(0f, 1.5f), Tooltip("새벽 시야 배율 (기본 0.7 = 70%)")]
        private float _dawnVisionMultiplier = 0.7f;

        [SerializeField, Range(0f, 1.5f), Tooltip("낮 시야 배율 (기본 1.0 = 100%)")]
        private float _dayVisionMultiplier = 1.0f;

        [SerializeField, Range(0f, 1.5f), Tooltip("저녁 시야 배율 (기본 0.9 = 90%)")]
        private float _eveningVisionMultiplier = 0.9f;

        [SerializeField, Range(0f, 1.5f), Tooltip("밤 시야 배율 (기본 0.5 = 50%)")]
        private float _nightVisionMultiplier = 0.5f;

        [Header("NPC Activity Multipliers")]
        [SerializeField, Range(0f, 2f), Tooltip("새벽 NPC 활동 배율")]
        private float _dawnNPCActivity = 0.5f;

        [SerializeField, Range(0f, 2f), Tooltip("낮 NPC 활동 배율 (최대)")]
        private float _dayNPCActivity = 1.0f;

        [SerializeField, Range(0f, 2f), Tooltip("저녁 NPC 활동 배율 (귀가 시작)")]
        private float _eveningNPCActivity = 0.7f;

        [SerializeField, Range(0f, 2f), Tooltip("밤 NPC 활동 배율 (최소)")]
        private float _nightNPCActivity = 0.3f;

        [Header("Monster Spawn Multiplier")]
        [SerializeField, Range(0f, 5f), Tooltip("야간 몬스터 출현 배율")]
        private float _nightMonsterSpawnMultiplier = 2.0f;

        [SerializeField, Range(0f, 5f), Tooltip("새벽 몬스터 출현 배율")]
        private float _dawnMonsterSpawnMultiplier = 1.3f;

        // ================================================================
        // Private State
        // ================================================================

        private TimeManager _timeManager;
        private TimeOfDay _currentTimeOfDay = TimeOfDay.Day;

        // ================================================================
        // Public Properties
        // ================================================================

        /// <summary>현재 시간대</summary>
        public TimeOfDay CurrentTimeOfDay => _currentTimeOfDay;

        /// <summary>현재 시간대의 은신 보너스 (0..1)</summary>
        public float CurrentStealthBonus => GetStealthBonus(_currentTimeOfDay);

        /// <summary>현재 시간대의 시야 배율</summary>
        public float CurrentVisionMultiplier => GetVisionMultiplier(_currentTimeOfDay);

        /// <summary>현재 시간대의 NPC 활동 배율</summary>
        public float CurrentNPCActivityMultiplier => GetNPCActivityMultiplier(_currentTimeOfDay);

        /// <summary>현재 시간대의 몬스터 출현 배율</summary>
        public float CurrentMonsterSpawnMultiplier => GetMonsterSpawnMultiplier(_currentTimeOfDay);

        /// <summary>현재 시간대 이름 (한글)</summary>
        public string CurrentTimeOfDayName
        {
            get
            {
                switch (_currentTimeOfDay)
                {
                    case TimeOfDay.Dawn:    return "새벽";
                    case TimeOfDay.Day:     return "낮";
                    case TimeOfDay.Evening: return "저녁";
                    case TimeOfDay.Night:   return "밤";
                    default:                return "알 수 없음";
                }
            }
        }

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _timeManager = TimeManager.Instance;
            if (_timeManager == null)
            {
                Debug.LogError("[TimeOfDayEffects] TimeManager.Instance를 찾을 수 없습니다.");
                enabled = false;
                return;
            }

            // TimeManager 이벤트 구독
            _timeManager.OnTimeChanged += OnTimeChanged;

            // 초기 시간대 설정
            _currentTimeOfDay = CalculateTimeOfDay(_timeManager.Hour);
            OnTimeOfDayChanged?.Invoke(_currentTimeOfDay);

            Debug.Log($"[TimeOfDayEffects] 초기화 완료: {_currentTimeOfDay}");
        }

        private void OnDestroy()
        {
            if (_timeManager != null)
            {
                _timeManager.OnTimeChanged -= OnTimeChanged;
            }
            if (Instance == this)
                Instance = null;
        }

        // ================================================================
        // TimeManager Callback
        // ================================================================

        /// <summary>
        /// TimeManager의 시간 변경 이벤트 콜백.
        /// 시간대가 변경되었는지 확인하고, 변경되었다면 이벤트를 발생시킵니다.
        /// </summary>
        private void OnTimeChanged(int hour, int minute)
        {
            TimeOfDay newTimeOfDay = CalculateTimeOfDay(hour);
            if (newTimeOfDay != _currentTimeOfDay)
            {
                TimeOfDay previous = _currentTimeOfDay;
                _currentTimeOfDay = newTimeOfDay;
                OnTimeOfDayChanged?.Invoke(newTimeOfDay);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[TimeOfDayEffects] 시간대 변경: {previous} → {newTimeOfDay} (현재 시각 {hour:D2}:{minute:D2})");
#endif
            }
        }

        // ================================================================
        // TimeOfDay Calculation
        // ================================================================

        /// <summary>
        /// 주어진 시각(Hour)으로부터 TimeOfDay를 계산합니다.
        /// </summary>
        private TimeOfDay CalculateTimeOfDay(int hour)
        {
            if (hour >= 20 || hour < 4)
                return TimeOfDay.Night;
            if (hour >= 4 && hour < 6)
                return TimeOfDay.Dawn;
            if (hour >= 6 && hour < 18)
                return TimeOfDay.Day;
            // hour >= 18 && hour < 20
            return TimeOfDay.Evening;
        }

        // ================================================================
        // Effect Accessors
        // ================================================================

        /// <summary>시간대별 은신 보너스 반환</summary>
        private float GetStealthBonus(TimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case TimeOfDay.Dawn:    return _dawnStealthBonus;
                case TimeOfDay.Day:     return _dayStealthBonus;
                case TimeOfDay.Evening: return _eveningStealthBonus;
                case TimeOfDay.Night:   return _nightStealthBonus;
                default:                return 0f;
            }
        }

        /// <summary>시간대별 시야 배율 반환</summary>
        private float GetVisionMultiplier(TimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case TimeOfDay.Dawn:    return _dawnVisionMultiplier;
                case TimeOfDay.Day:     return _dayVisionMultiplier;
                case TimeOfDay.Evening: return _eveningVisionMultiplier;
                case TimeOfDay.Night:   return _nightVisionMultiplier;
                default:                return 1f;
            }
        }

        /// <summary>시간대별 NPC 활동 배율 반환</summary>
        private float GetNPCActivityMultiplier(TimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case TimeOfDay.Dawn:    return _dawnNPCActivity;
                case TimeOfDay.Day:     return _dayNPCActivity;
                case TimeOfDay.Evening: return _eveningNPCActivity;
                case TimeOfDay.Night:   return _nightNPCActivity;
                default:                return 1f;
            }
        }

        /// <summary>시간대별 몬스터 출현 배율 반환</summary>
        private float GetMonsterSpawnMultiplier(TimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case TimeOfDay.Dawn:    return _dawnMonsterSpawnMultiplier;
                case TimeOfDay.Day:     return 1f;
                case TimeOfDay.Evening: return 1f;
                case TimeOfDay.Night:   return _nightMonsterSpawnMultiplier;
                default:                return 1f;
            }
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// 현재 시간대가 밤인지 확인합니다.
        /// </summary>
        public bool IsNightTime()
        {
            return _currentTimeOfDay == TimeOfDay.Night;
        }

        /// <summary>
        /// 현재 시간대가 낮(새벽 포함)인지 확인합니다.
        /// </summary>
        public bool IsDayTime()
        {
            return _currentTimeOfDay == TimeOfDay.Day || _currentTimeOfDay == TimeOfDay.Dawn;
        }

        /// <summary>
        /// 특정 시간대가 현재 시간대와 일치하는지 확인합니다.
        /// </summary>
        public bool IsTimeOfDay(TimeOfDay timeOfDay)
        {
            return _currentTimeOfDay == timeOfDay;
        }

        /// <summary>
        /// 현재 시간대를 강제로 설정합니다 (디버그/이벤트 용).
        /// </summary>
        public void ForceTimeOfDay(TimeOfDay timeOfDay)
        {
            if (_currentTimeOfDay == timeOfDay) return;

            TimeOfDay previous = _currentTimeOfDay;
            _currentTimeOfDay = timeOfDay;
            OnTimeOfDayChanged?.Invoke(timeOfDay);

            Debug.Log($"[TimeOfDayEffects] 강제 시간대 변경: {previous} → {timeOfDay}");
        }
    }
}