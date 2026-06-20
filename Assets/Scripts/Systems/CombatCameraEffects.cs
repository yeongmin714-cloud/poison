using System.Collections;
using UnityEngine;

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

        private Camera _mainCamera;
        private Vector3 _originalCamLocalPos;
        private Coroutine _activeShake;
        private Coroutine _activeTimeScale;
        private float _preTimeScale;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _preTimeScale = 1f;
        }

        private void Start()
        {
            _mainCamera = Camera.main;
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
            _activeShake = StartCoroutine(ShakeRoutine(intensity, _hitShakeDuration));
        }

        /// <summary>Time.timeScale=0.5 (0.1s) → Lerp 복구</summary>
        public void PlayHitStop()
        {
            if (_activeTimeScale != null)
                StopCoroutine(_activeTimeScale);
            _activeTimeScale = StartCoroutine(HitStopRoutine());
        }

        /// <summary>Time.timeScale=0.5 (0.3s) → Lerp 복구</summary>
        public void PlayKillSlowMotion()
        {
            if (_activeTimeScale != null)
                StopCoroutine(_activeTimeScale);
            _activeTimeScale = StartCoroutine(KillSlowMotionRoutine());
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

        private IEnumerator HitStopRoutine()
        {
            _preTimeScale = Time.timeScale;

            // Immediate time scale drop
            Time.timeScale = _hitStopTimeScale;

            // Hold at slow speed
            float elapsed = 0f;
            while (elapsed < _hitStopDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Lerp recovery back to original scale
            float recoveryElapsed = 0f;
            float startScale = Time.timeScale;
            while (recoveryElapsed < _hitStopRecoveryDuration)
            {
                float t = recoveryElapsed / _hitStopRecoveryDuration;
                Time.timeScale = Mathf.Lerp(startScale, _preTimeScale, t);
                recoveryElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = _preTimeScale;
            _activeTimeScale = null;
        }

        private IEnumerator KillSlowMotionRoutine()
        {
            _preTimeScale = Time.timeScale;

            // Immediate time scale drop
            Time.timeScale = _killSlowTimeScale;

            // Hold at slow speed
            float elapsed = 0f;
            while (elapsed < _killSlowDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Lerp recovery back to original scale
            float recoveryElapsed = 0f;
            float startScale = Time.timeScale;
            while (recoveryElapsed < _killSlowRecoveryDuration)
            {
                float t = recoveryElapsed / _killSlowRecoveryDuration;
                Time.timeScale = Mathf.Lerp(startScale, _preTimeScale, t);
                recoveryElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = _preTimeScale;
            _activeTimeScale = null;
        }

        /// <summary>Scene이 언로드될 때 timeScale 복구</summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Time.timeScale = 1f;
                Instance = null;
            }
        }
    }
}