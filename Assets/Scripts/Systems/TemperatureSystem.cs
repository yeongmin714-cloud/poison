// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 414
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 온도 시스템 - BotW 스타일 온도 게이지용
    /// -1(극한 추위) ~ 0(보통) ~ 1(극한 더위)
    /// </summary>
    public class TemperatureSystem : MonoBehaviour
    {
        public static TemperatureSystem Instance { get; private set; }

        [Header("Temperature Settings")]
        [SerializeField] private float _baseTemperature = -0.2f; // 기본: 약간 추움
        [SerializeField] private float _minTemperature = -1f;
        [SerializeField] private float _maxTemperature = 1f;
        [SerializeField] private float _changeRate = 0.05f; // 초당 변화율

        public float CurrentTemperature => Mathf.Clamp(_baseTemperature, _minTemperature, _maxTemperature);

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

        /// <summary>
        /// 외부에서 온도 변경 (지역 이동, 날씨 변화 등)
        /// </summary>
        public void SetTemperature(float temperature, bool immediate = false)
        {
            _baseTemperature = Mathf.Clamp(temperature, _minTemperature, _maxTemperature);
        }

        /// <summary>
        /// 온도 오프셋 추가 (예: 불 근처, 물 속)
        /// </summary>
        public void AddTemperatureOffset(float offset, float duration = 0f)
        {
            _baseTemperature = Mathf.Clamp(_baseTemperature + offset, _minTemperature, _maxTemperature);
            
            if (duration > 0f)
            {
                StartCoroutine(RemoveOffsetAfterDelay(offset, duration));
            }
        }

        private System.Collections.IEnumerator RemoveOffsetAfterDelay(float offset, float delay)
        {
            yield return new WaitForSeconds(delay);
            _baseTemperature = Mathf.Clamp(_baseTemperature - offset, _minTemperature, _maxTemperature);
        }
    }
}