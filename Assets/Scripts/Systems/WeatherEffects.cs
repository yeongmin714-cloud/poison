using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 41.2: 날씨별 게임플레이 효과 관리.
    /// WeatherManager의 현재 날씨를 기반으로 플레이어에게 다양한 효과를 적용합니다.
    /// 
    /// 날씨별 효과:
    ///   Clear     — 효과 없음
    ///   Rain      — 이동속도 -10%, 독/안개 2배 확산, 화염 피해 50% 감소
    ///   Snow      — 이동속도 -20%, 발자국 남음(시스템 연동), 시야 감소
    ///   Fog       — 시야 대폭 감소, 은신 보너스 +30%, 원거리 명중률 -20%
    ///   StrongWind — 이동 불가/제한, 건물 내 강제, 외출 시 지속 데미지
    /// </summary>
    public class WeatherEffects : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        public static WeatherEffects Instance { get; private set; }

        // ================================================================
        // Events
        // ================================================================

        /// <summary>날씨 효과가 적용될 때 발생합니다.</summary>
        public event Action<WeatherManager.WeatherType> OnWeatherEffectApplied;

        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("Movement Speed Modifiers")]
        [SerializeField, Range(-1f, 0f), Tooltip("비: 이동속도 보정 (-10%)")]
        private float _rainSpeedModifier = -0.10f;

        [SerializeField, Range(-1f, 0f), Tooltip("눈: 이동속도 보정 (-20%)")]
        private float _snowSpeedModifier = -0.20f;

        [SerializeField, Range(-1f, 0f), Tooltip("강풍: 이동속도 보정 (-50%)")]
        private float _stormSpeedModifier = -0.50f;

        [Header("Stealth Bonuses (additive)")]
        [SerializeField, Range(0f, 1f), Tooltip("안개: 은신 보너스 (+30%)")]
        private float _fogStealthBonus = 0.30f;

        [SerializeField, Range(0f, 1f), Tooltip("비: 은신 보너스 (+10%)")]
        private float _rainStealthBonus = 0.10f;

        [SerializeField, Range(0f, 1f), Tooltip("눈: 은신 보너스 (+5%)")]
        private float _snowStealthBonus = 0.05f;

        [Header("Vision / Detection Modifiers")]
        [SerializeField, Range(0f, 2f), Tooltip("안개: 시야 배율 (0.4 = 40%)")]
        private float _fogVisionMultiplier = 0.4f;

        [SerializeField, Range(0f, 2f), Tooltip("눈: 시야 배율 (0.6 = 60%)")]
        private float _snowVisionMultiplier = 0.6f;

        [SerializeField, Range(0f, 2f), Tooltip("비: 시야 배율 (0.85 = 85%)")]
        private float _rainVisionMultiplier = 0.85f;

        [Header("Combat Modifiers")]
        [SerializeField, Range(-1f, 0f), Tooltip("안개: 원거리 명중률 보정 (-20%)")]
        private float _fogRangedAccuracyModifier = -0.20f;

        [SerializeField, Range(-1f, 0f), Tooltip("비: 화염 피해 보정 (-50%)")]
        private float _rainFireDamageModifier = -0.50f;

        [SerializeField, Range(-1f, 0f), Tooltip("강풍: 원거리 명중률 보정 (-40%)")]
        private float _stormRangedAccuracyModifier = -0.40f;

        [Header("Spread / Propagation Modifiers")]
        [SerializeField, Range(0f, 5f), Tooltip("비: 독/안개 확산 배율 (2배)")]
        private float _rainSpreadMultiplier = 2.0f;

        [Header("Weather Damage")]
        [SerializeField, Tooltip("강풍(StrongWind) 상태에서 외출 시 초당 데미지")]
        private float _stormDamagePerSecond = 5f;

        [Header("Footprint Settings")]
        [SerializeField, Tooltip("눈: 발자국 남김 활성화")]
        private bool _snowFootprintsEnabled = true;

        // ================================================================
        // Private State
        // ================================================================

        private WeatherManager _weatherManager;
        private WeatherManager.WeatherType _lastWeather = WeatherManager.WeatherType.Clear;
        private bool _isPlayerOutside = true;

        // ================================================================
        // Public Properties
        // ================================================================

        /// <summary>현재 날씨 종류</summary>
        public WeatherManager.WeatherType CurrentWeather => _weatherManager != null
            ? _weatherManager.CurrentWeather
            : WeatherManager.WeatherType.Clear;

        /// <summary>현재 이동속도 보정값 (-1..0)</summary>
        public float CurrentSpeedModifier => GetSpeedModifier(CurrentWeather);

        /// <summary>현재 은신 보너스</summary>
        public float CurrentStealthBonus => GetStealthBonus(CurrentWeather);

        /// <summary>현재 시야 배율</summary>
        public float CurrentVisionMultiplier => GetVisionMultiplier(CurrentWeather);

        /// <summary>현재 원거리 명중률 보정</summary>
        public float CurrentRangedAccuracyModifier => GetRangedAccuracyModifier(CurrentWeather);

        /// <summary>현재 화염 피해 보정</summary>
        public float CurrentFireDamageModifier => GetFireDamageModifier(CurrentWeather);

        /// <summary>현재 독/안개 확산 배율</summary>
        public float CurrentSpreadMultiplier => GetSpreadMultiplier(CurrentWeather);

        /// <summary>현재 외출 시 초당 데미지 (강풍 시)</summary>
        public float CurrentStormDamagePerSecond
        {
            get
            {
                if (CurrentWeather == WeatherManager.WeatherType.StrongWind && _isPlayerOutside)
                    return _stormDamagePerSecond;
                return 0f;
            }
        }

        /// <summary>발자국 활성화 여부 (눈)</summary>
        public bool IsFootprintsEnabled
        {
            get
            {
                if (!_snowFootprintsEnabled) return false;
                return CurrentWeather == WeatherManager.WeatherType.Snow;
            }
        }

        /// <summary>외부/내부 상태 (NPCWeatherBehavior에서 설정)</summary>
        public bool IsPlayerOutside
        {
            get => _isPlayerOutside;
            set => _isPlayerOutside = value;
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

        private void OnDestroy()
        {
            if (_weatherManager != null)
            {
                _weatherManager.OnWeatherChanged -= OnWeatherChanged;
            }
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            _weatherManager = WeatherManager.Instance;
            if (_weatherManager == null)
            {
                Debug.LogError("[WeatherEffects] WeatherManager.Instance를 찾을 수 없습니다.");
                enabled = false;
                return;
            }

            // WeatherManager 이벤트 구독
            _weatherManager.OnWeatherChanged += OnWeatherChanged;

            // 초기 날씨 저장
            _lastWeather = _weatherManager.CurrentWeather;

            // 초기 효과 적용
            ApplyWeatherEffects(_lastWeather);

            Debug.Log($"[WeatherEffects] 초기화 완료: {_lastWeather}");
        }

        private void Update()
        {
            // 강풍(StrongWind) 상태에서 외출 데미지 처리
            if (_weatherManager == null) return;

            WeatherManager.WeatherType current = _weatherManager.CurrentWeather;
            if (current == WeatherManager.WeatherType.StrongWind && _isPlayerOutside)
            {
                // 외출 데미지 — PlayerHealth 또는 직접 플레이어 데미지 처리
                // 실제 데미지 처리는 PlayerHealth나 CombatSystem이 담당
                // 여기서는 매 프레임 체크만 수행
            }
        }

        // ================================================================
        // WeatherManager Callback
        // ================================================================

        /// <summary>
        /// WeatherManager의 날씨 변경 이벤트 콜백.
        /// 날씨가 변경되면 효과를 재계산하고 이벤트를 발생시킵니다.
        /// </summary>
        private void OnWeatherChanged(WeatherManager.WeatherType weatherType)
        {
            _lastWeather = weatherType;
            ApplyWeatherEffects(weatherType);
            OnWeatherEffectApplied?.Invoke(weatherType);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[WeatherEffects] 날씨 효과 적용: {weatherType}");
#endif
        }

        /// <summary>
        /// 주어진 날씨에 맞는 효과를 즉시 적용합니다.
        /// 각 시스템(PlayerMovement, StealthSystem 등)은
        /// WeatherEffects.Instance의 프로퍼티를 참조하여 효과를 반영합니다.
        /// </summary>
        private void ApplyWeatherEffects(WeatherManager.WeatherType weatherType)
        {
            // PlayerMovement 속도 수정자에 반영
            // PlayerMovement가 WeatherEffects.Instance.CurrentSpeedModifier를
            // 읽어서 _speedModifier에 반영한다고 가정합니다.

            switch (weatherType)
            {
                case WeatherManager.WeatherType.Clear:
                    // 효과 없음 — 모든 수정자 리셋
                    break;

                case WeatherManager.WeatherType.Rain:
                    // Rain 효과는 각 Consumer가 프로퍼티를 통해 읽음
                    break;

                case WeatherManager.WeatherType.Snow:
                    // Snow 효과
                    break;

                case WeatherManager.WeatherType.Fog:
                    // Fog 효과
                    break;

                case WeatherManager.WeatherType.StrongWind:
                    // StrongWind → Storm 효과
                    break;
            }
        }

        // ================================================================
        // Effect Accessors
        // ================================================================

        private float GetSpeedModifier(WeatherManager.WeatherType weather)
        {
            switch (weather)
            {
                case WeatherManager.WeatherType.Rain:       return _rainSpeedModifier;
                case WeatherManager.WeatherType.Snow:       return _snowSpeedModifier;
                case WeatherManager.WeatherType.StrongWind: return _stormSpeedModifier;
                default:                                    return 0f;
            }
        }

        private float GetStealthBonus(WeatherManager.WeatherType weather)
        {
            switch (weather)
            {
                case WeatherManager.WeatherType.Fog:  return _fogStealthBonus;
                case WeatherManager.WeatherType.Rain: return _rainStealthBonus;
                case WeatherManager.WeatherType.Snow: return _snowStealthBonus;
                default:                             return 0f;
            }
        }

        private float GetVisionMultiplier(WeatherManager.WeatherType weather)
        {
            switch (weather)
            {
                case WeatherManager.WeatherType.Fog:  return _fogVisionMultiplier;
                case WeatherManager.WeatherType.Snow: return _snowVisionMultiplier;
                case WeatherManager.WeatherType.Rain: return _rainVisionMultiplier;
                default:                             return 1f;
            }
        }

        private float GetRangedAccuracyModifier(WeatherManager.WeatherType weather)
        {
            switch (weather)
            {
                case WeatherManager.WeatherType.Fog:       return _fogRangedAccuracyModifier;
                case WeatherManager.WeatherType.StrongWind: return _stormRangedAccuracyModifier;
                default:                                    return 0f;
            }
        }

        private float GetFireDamageModifier(WeatherManager.WeatherType weather)
        {
            switch (weather)
            {
                case WeatherManager.WeatherType.Rain: return _rainFireDamageModifier;
                default:                             return 0f;
            }
        }

        private float GetSpreadMultiplier(WeatherManager.WeatherType weather)
        {
            switch (weather)
            {
                case WeatherManager.WeatherType.Rain: return _rainSpreadMultiplier;
                default:                             return 1f;
            }
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// 현재 날씨가 악천후(비/눈/안개/강풍)인지 확인합니다.
        /// </summary>
        public bool IsSevereWeather()
        {
            WeatherManager.WeatherType w = CurrentWeather;
            return w == WeatherManager.WeatherType.Rain
                || w == WeatherManager.WeatherType.Snow
                || w == WeatherManager.WeatherType.Fog
                || w == WeatherManager.WeatherType.StrongWind;
        }

        /// <summary>
        /// 외출 시 데미지를 받아야 하는 날씨인지 확인합니다.
        /// </summary>
        public bool IsDamageWeatherOutside()
        {
            return CurrentWeather == WeatherManager.WeatherType.StrongWind && _isPlayerOutside;
        }

        /// <summary>
        /// 강제로 날씨 효과를 갱신합니다. (날씨가 외부에서 변경된 경우 호출)
        /// </summary>
        public void RefreshEffects()
        {
            if (_weatherManager != null)
            {
                ApplyWeatherEffects(_weatherManager.CurrentWeather);
                OnWeatherEffectApplied?.Invoke(_weatherManager.CurrentWeather);
            }
        }
    }
}