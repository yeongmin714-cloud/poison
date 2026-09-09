using System.Collections;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// G2-04: 전투 카메라 이펙트 — 흔들림, HitStop, 슬로우모션.
    /// 싱글톤, 코루틴 기반 (Time.unscaledDeltaTime 사용).
    /// </summary>
    public class CombatCameraEffects : MonoBehaviour
    {
        public static CombatCameraEffects Instance { get; private set; }

        [Header("Shake Settings")]
        [SerializeField] private float _hitShakeIntensity = 0.05f;
        [SerializeField] private float _hitShakeDuration = 0.1f;
        [SerializeField] private float _critShakeMultiplier = 2f;

        [Header("Hit Stop Settings")]
        [SerializeField] private float _hitStopTimeScale = 0.5f;
        [SerializeField] private float _hitStopDuration = 0.1f;
        [SerializeField] private float _hitStopRecoveryDuration = 0.15f;

        [Header("Kill Slow Motion Settings")]
        [SerializeField] private float _killSlowTimeScale = 0.5f;
        [SerializeField] private float _killSlowDuration = 0.3f;
        [SerializeField] private float _killSlowRecoveryDuration = 0.4f;

        // ================================================================
        // HighSpec 전용 feel 튜닝 상수 (Balanced 동작에는 영향 없음)
        // ================================================================
        private const float HighSpecShakeMultiplier = 1.3f;     // 흔들림 강도 1.3배
        private const float HighSpecDurationMultiplier = 1.15f; // HitStop/슬로우모션 지속시간 1.15배
        private const float HighSpecHitStopTimeScale = 0.35f;   // HitStop: 0.5 → 0.35 (더 깊은 정지)
        private const float HighSpecKillSlowTimeScale = 0.4f;   // 킬 슬로우모션: 0.5 → 0.4 (살짝 더 깊게)

        private Camera _mainCamera;
        private Vector3 _originalCamLocalPos;
        private Coroutine _activeShake;
        private Coroutine _activeTimeScale;
        /// <summary>
        /// 효과가 시작되기 전의 원본 Time.timeScale.
        /// 중첩 호출 시 덮어쓰지 않도록 보존한다.
        /// </summary>
        private float _baseTimeScale = 1f;
        /// <summary>
        /// 현재 시간 스케일 효과가 실행 중인지 여부.
        /// _activeTimeScale 대신 사용하여 중첩 시 _baseTimeScale 보호.
        /// </summary>
        private bool _isTimeScaleEffectRunning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Camera.main을 Awake에서 미리 캐싱 (Start보다 빠름)
            _mainCamera = Camera.main;
            _baseTimeScale = Time.timeScale;
        }

        private void Start()
        {
            if (_mainCamera != null)
                _originalCamLocalPos = _mainCamera.transform.localPosition;
        }

        /// <summary>일반 타격 효과: Shake + HitStop</summary>
        public static void PlayHit()
        {
            if (Instance == null) return;
            Instance.PlayHitShake(Instance._hitShakeIntensity);
            Instance.PlayHitStop();
        }

        /// <summary>적 처치 효과: 슬로우모션</summary>
        public static void PlayKill()
        {
            if (Instance == null) return;
            Instance.PlayKillSlowMotion();
        }

        /// <summary>치명타/백어택 효과: Shake 2배 + HitStop</summary>
        public static void PlayCrit()
        {
            if (Instance == null) return;
            Instance.PlayCritShake();
        }

        // ===== Instance Methods =====

        /// <summary>카메라 위치 랜덤 오프셋 + 원복 (0.1s)</summary>
        public void PlayHitShake(float intensity)
        {
            if (_activeShake != null)
                StopCoroutine(_activeShake);

            // HighSpec: 흔들림 강도 1.3배 (Balanced는 기본값 유지).
            // PlayCritShake도 이 메서드를 경유하므로 치명타 흔들림도 함께 강해진다.
            if (ActionFeel.HighSpec)
                intensity *= HighSpecShakeMultiplier;

            _activeShake = StartCoroutine(ShakeRoutine(intensity, _hitShakeDuration));
        }

        /// <summary>Time.timeScale=0.5 (0.1s) → Lerp 복구</summary>
        public void PlayHitStop()
        {
            if (_activeTimeScale != null)
                StopCoroutine(_activeTimeScale);

            float timeScale = _hitStopTimeScale;
            float holdDuration = _hitStopDuration;
            float recoveryDuration = _hitStopRecoveryDuration;

            // HighSpec: 더 깊게(0.35), 더 길게(1.15배) — 더 강한 타격감 (Balanced는 기본값 유지)
            if (ActionFeel.HighSpec)
            {
                timeScale = HighSpecHitStopTimeScale;
                holdDuration *= HighSpecDurationMultiplier;
                recoveryDuration *= HighSpecDurationMultiplier;
            }

            _activeTimeScale = StartCoroutine(HitStopRoutine(timeScale, holdDuration, recoveryDuration));
        }

        /// <summary>Time.timeScale=0.5 (0.3s) → Lerp 복구</summary>
        public void PlayKillSlowMotion()
        {
            if (_activeTimeScale != null)
                StopCoroutine(_activeTimeScale);

            float timeScale = _killSlowTimeScale;
            float holdDuration = _killSlowDuration;
            float recoveryDuration = _killSlowRecoveryDuration;

            // HighSpec: 킬 슬로우모션도 살짝 더 깊게(0.4), 더 길게(1.15배) (Balanced는 기본값 유지)
            if (ActionFeel.HighSpec)
            {
                timeScale = HighSpecKillSlowTimeScale;
                holdDuration *= HighSpecDurationMultiplier;
                recoveryDuration *= HighSpecDurationMultiplier;
            }

            _activeTimeScale = StartCoroutine(KillSlowMotionRoutine(timeScale, holdDuration, recoveryDuration));
        }

        /// <summary>Shake 2배 + HitStop</summary>
        public void PlayCritShake()
        {
            PlayHitShake(_hitShakeIntensity * _critShakeMultiplier);
            PlayHitStop();
        }

        // ===== Coroutines =====

        private IEnumerator ShakeRoutine(float intensity, float duration)
        {
            if (_mainCamera == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                Vector3 randomOffset = Random.insideUnitSphere * intensity;
                // Keep the z offset minimal to avoid clipping
                randomOffset.z = randomOffset.z * 0.3f;
                _mainCamera.transform.localPosition = _originalCamLocalPos + randomOffset;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Restore original position with smooth lerp
            float restoreDuration = duration * 0.5f;
            float restoreElapsed = 0f;
            Vector3 startPos = _mainCamera.transform.localPosition;
            while (restoreElapsed < restoreDuration)
            {
                float t = restoreElapsed / restoreDuration;
                _mainCamera.transform.localPosition = Vector3.Lerp(startPos, _originalCamLocalPos, t);
                restoreElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _mainCamera.transform.localPosition = _originalCamLocalPos;
            _activeShake = null;
        }

        private IEnumerator HitStopRoutine(float timeScale, float holdDuration, float recoveryDuration)
        {
            // 최초 효과 시작 시에만 _baseTimeScale 저장 (중첩 호출 시 덮어쓰지 않음)
            if (!_isTimeScaleEffectRunning)
            {
                _baseTimeScale = Time.timeScale;
                _isTimeScaleEffectRunning = true;
            }

            // Immediate time scale drop
            Time.timeScale = timeScale;

            // Hold at slow speed
            float elapsed = 0f;
            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Lerp recovery back to _baseTimeScale (원본 값으로 복구)
            float recoveryElapsed = 0f;
            float startScale = Time.timeScale;
            while (recoveryElapsed < recoveryDuration)
            {
                float t = recoveryElapsed / recoveryDuration;
                Time.timeScale = Mathf.Lerp(startScale, _baseTimeScale, t);
                recoveryElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = _baseTimeScale;
            _activeTimeScale = null;
            _isTimeScaleEffectRunning = false;
        }

        private IEnumerator KillSlowMotionRoutine(float timeScale, float holdDuration, float recoveryDuration)
        {
            // 최초 효과 시작 시에만 _baseTimeScale 저장 (중첩 호출 시 덮어쓰지 않음)
            if (!_isTimeScaleEffectRunning)
            {
                _baseTimeScale = Time.timeScale;
                _isTimeScaleEffectRunning = true;
            }

            // Immediate time scale drop
            Time.timeScale = timeScale;

            // Hold at slow speed
            float elapsed = 0f;
            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Lerp recovery back to _baseTimeScale (원본 값으로 복구)
            float recoveryElapsed = 0f;
            float startScale = Time.timeScale;
            while (recoveryElapsed < recoveryDuration)
            {
                float t = recoveryElapsed / recoveryDuration;
                Time.timeScale = Mathf.Lerp(startScale, _baseTimeScale, t);
                recoveryElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = _baseTimeScale;
            _activeTimeScale = null;
            _isTimeScaleEffectRunning = false;
        }

        /// <summary>Scene이 언로드될 때 timeScale 복구</summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Time.timeScale = _baseTimeScale;
                Instance = null;
            }
        }
    }
}
