using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 소음 시스템 - BotW 스타일 소음 게이지용
    /// 0(무음) ~ 1(최대 소음)
    /// </summary>
    public class SoundSystem : MonoBehaviour
    {
        public static SoundSystem Instance { get; private set; }

        [Header("Sound Settings")]
        [SerializeField] private float _baseNoiseLevel = 0.1f;
        [SerializeField] private float _walkNoise = 0.3f;
        [SerializeField] private float _runNoise = 0.6f;
        [SerializeField] private float _crouchNoise = 0.05f;
        [SerializeField] private float _attackNoise = 0.8f;
        [SerializeField] private float _decayRate = 2f; // 소음 감소 속도

        private float _currentNoiseLevel = 0f;
        private float _targetNoiseLevel = 0f;

        public float CurrentNoiseLevel => _currentNoiseLevel;

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
            _currentNoiseLevel = _baseNoiseLevel;
            _targetNoiseLevel = _baseNoiseLevel;
        }

        private void Update()
        {
            // 목표 소음 레벨로 부드럽게 보간
            _currentNoiseLevel = Mathf.Lerp(_currentNoiseLevel, _targetNoiseLevel, Time.deltaTime * _decayRate);
            _currentNoiseLevel = Mathf.Clamp01(_currentNoiseLevel);
        }

        /// <summary>
        /// 이동 시 소음 발생
        /// </summary>
        public void OnMove(bool isRunning, bool isCrouching)
        {
            if (isCrouching)
                _targetNoiseLevel = _crouchNoise;
            else if (isRunning)
                _targetNoiseLevel = _runNoise;
            else
                _targetNoiseLevel = _walkNoise;
        }

        /// <summary>
        /// 정지 시 기본 소음으로 복귀
        /// </summary>
        public void OnStop()
        {
            _targetNoiseLevel = _baseNoiseLevel;
        }

        /// <summary>
        /// 공격/액션 시 소음 발생
        /// </summary>
        public void OnAction(float noiseIntensity = 1f)
        {
            _currentNoiseLevel = Mathf.Min(1f, _currentNoiseLevel + _attackNoise * noiseIntensity);
            _targetNoiseLevel = _currentNoiseLevel;
        }

        /// <summary>
        /// 직접 소음 레벨 설정
        /// </summary>
        public void SetNoiseLevel(float level)
        {
            _currentNoiseLevel = Mathf.Clamp01(level);
            _targetNoiseLevel = _currentNoiseLevel;
        }
    }
}