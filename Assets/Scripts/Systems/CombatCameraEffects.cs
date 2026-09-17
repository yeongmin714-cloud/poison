using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// G2-04: 전투 카메라 이펙트 — 흔들림, HitStop, 슬로우모션.
    /// 싱글톤, 코루틴 기반 (Time.unscaledDeltaTime 사용).
    /// [2026-09-15 Phase A] 무기별 카메라 임펄스 프로파일 추가:
    ///   - 검: 근접 강함(전진+흔들림)
    ///   - 창: 찌르기 관통감(좁고 깊은 흔들림)
    ///   - 활: 원거리 약함(가벼운 진동)
    ///   - 맨손: 경쾌한 연타(짧고 빠른)
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

        [Header("Weapon-Specific Impulse Profiles (Phase A)")]
        [SerializeField] private CameraImpulseProfile _swordProfile;
        [SerializeField] private CameraImpulseProfile _spearProfile;
        [SerializeField] private CameraImpulseProfile _bowProfile;
        [SerializeField] private CameraImpulseProfile _fistProfile;

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

        /// <summary>
        /// 무기별 임펄스 프로파일 구조체
        /// </summary>
        [System.Serializable]
        public struct CameraImpulseProfile
        {
            public float intensity;      // 흔들림 강도
            public float duration;       // 지속 시간
            public float frequency;      // 진동 주파수
            public Vector3 directionBias; // 방향 바이어스 (전진/상향 등)
            public bool useImpulseSource; // Cinemachine Impulse Source 사용 여부
        }

        /// <summary>일반 타격 효과: Shake + HitStop</summary>
        public static void PlayHit()
        {
            if (Instance == null) return;
            Instance.PlayHitShake(Instance._hitShakeIntensity);
            Instance.PlayHitStop();
        }

        /// <summary>무기별 타격 효과: 무기 타입에 따른 임펄스 프로파일 적용</summary>
        public static void PlayHit(ProjectName.Core.WeaponType weaponType)
        {
            if (Instance == null) return;

            var profile = Instance.GetProfile(weaponType);
            Instance.PlayWeaponShake(profile);
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

        /// <summary>[70차 후속19/C1] 활 발사 킥 — 가벼운 흔들림만(HitStop 없음, 사격 리듬 유지).</summary>
        public static void PlayFireKick()
        {
            if (Instance == null) return;
            Instance.PlayHitShake(Instance._hitShakeIntensity * 0.4f);
        }

        // ===== Instance Methods =====

        private CameraImpulseProfile GetProfile(ProjectName.Core.WeaponType weaponType)
        {
            switch (weaponType)
            {
                case ProjectName.Core.WeaponType.Sword: return _swordProfile;
                case ProjectName.Core.WeaponType.Spear: return _spearProfile;
                case ProjectName.Core.WeaponType.Bow: return _bowProfile;
                case ProjectName.Core.WeaponType.Fist: return _fistProfile;
                default: return _fistProfile;
            }
        }

        /// <summary>무기별 전용 흔들림 재생 (Cinemachine Impulse + 기존 셰이크 병행)</summary>
        public void PlayWeaponShake(CameraImpulseProfile profile)
        {
            // 1. Cinemachine Impulse Source로 임펄스 발사 (가장 자연스러운 카메라 반응)
            if (profile.useImpulseSource && _mainCamera != null)
            {
                var impulseSource = _mainCamera.GetComponent<CinemachineImpulseSource>();
                if (impulseSource == null)
                    impulseSource = _mainCamera.gameObject.AddComponent<CinemachineImpulseSource>();

                // 임펄스 방향: 전진 바이어스 + 랜덤
                Vector3 impulseDir = _mainCamera.transform.forward * profile.directionBias.z
                                   + _mainCamera.transform.up * profile.directionBias.y
                                   + _mainCamera.transform.right * profile.directionBias.x;

                impulseSource.GenerateImpulse(impulseDir * profile.intensity);
            }

            // 2. 기존 셰이크 루틴도 병행 (호환성)
            if (_activeShake != null)
                StopCoroutine(_activeShake);

            float intensity = profile.intensity;
            if (ActionFeel.HighSpec)
                intensity *= HighSpecShakeMultiplier;

            _activeShake = StartCoroutine(ShakeRoutine(intensity, profile.duration, profile.frequency));
        }

        // ===== Instance Methods =====

        /// <summary>카메라 위치 랜덤 오프셋 + 원복 (주파수 파라미터 추가)</summary>
        public void PlayHitShake(float intensity)
        {
            if (_activeShake != null)
                StopCoroutine(_activeShake);

            if (ActionFeel.HighSpec)
                intensity *= HighSpecShakeMultiplier;

            _activeShake = StartCoroutine(ShakeRoutine(intensity, _hitShakeDuration, 25f));
        }

        /// <summary>Time.timeScale=0.5 (0.1s) → Lerp 복구</summary>
        public void PlayHitStop()
        {
            if (_activeTimeScale != null)
                StopCoroutine(_activeTimeScale);

            float timeScale = _hitStopTimeScale;
            float holdDuration = _hitStopDuration;
            float recoveryDuration = _hitStopRecoveryDuration;

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

        /// <summary>주파수 파라미터 추가된 셰이크 루틴</summary>
        private IEnumerator ShakeRoutine(float intensity, float duration, float frequency = 25f)
        {
            if (_mainCamera == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 주파수 기반 진동 (더 자연스러운 카메라 흔들림)
                float noise = Mathf.PerlinNoise(Time.unscaledTime * frequency, 0f) * 2f - 1f;
                float noise2 = Mathf.PerlinNoise(0f, Time.unscaledTime * frequency) * 2f - 1f;
                float noise3 = Mathf.PerlinNoise(Time.unscaledTime * frequency * 0.7f, Time.unscaledTime * frequency * 0.7f) * 2f - 1f;

                Vector3 randomOffset = new Vector3(noise, noise2, noise3 * 0.3f) * intensity;
                randomOffset.z = randomOffset.z * 0.3f; // Z축은 덜 흔들림 (클리핑 방지)

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
            if (!_isTimeScaleEffectRunning)
            {
                _baseTimeScale = Time.timeScale;
                _isTimeScaleEffectRunning = true;
            }

            Time.timeScale = timeScale;

            float elapsed = 0f;
            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

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
            if (!_isTimeScaleEffectRunning)
            {
                _baseTimeScale = Time.timeScale;
                _isTimeScaleEffectRunning = true;
            }

            Time.timeScale = timeScale;

            float elapsed = 0f;
            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

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