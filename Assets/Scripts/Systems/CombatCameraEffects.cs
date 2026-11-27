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
    /// [무번호 80-P5] 셰이크는 코루틴(Update 후) 대신 LateUpdate(order 1000) 지배로 전환:
    ///   TopDownCameraController.LateUpdate가 매 프레임 transform.position을 팔로우 재기록해
    ///   기존 코루틴 경로의 localPosition 쓰기가 매 프레임 클로버링되어 셰이크가 실질 소멸(80-P5 포렌식).
    ///   additive 방식: 팔로우 출력 위에 마지막 오프셋을 strip 후 신규 노이즈를 얹는다 —
    ///   팔로우와 무간섭, 종료 시 오프셋 제거로 원복(포즈 덮어쓰기 없음 — 계획 Task 5.2 계약).
    /// </summary>
    [DefaultExecutionOrder(1000)]
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
        private Coroutine _activeTimeScale;
        // [무번호 80-P5] additive 셰이크 상태 — 코루틴(ShakeRoutine) 대체. LateUpdate 지배.
        private bool _shakeActive;
        private float _shakeElapsed;
        private float _shakeDuration;
        private float _shakeRestoreDuration;
        private float _shakeIntensity;
        private float _shakeFrequency;
        private Vector3 _shakeLastOffset;   // 마지막 프레임 적용 오프셋(strip용 — 팔로우 출력 위 additive)
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
            // [무번호 71-W1] 전투 잔상 비네트 — 처치 진입 단일 관문 훅(AnimalAI:1513 직접 · PlayKillVFX:825 수렴).
            //   연출 보조 — Application.isPlaying 가드 + try/catch 예외 격리(CombatFXGate 선례 — 전투 방해 금지).
            if (Application.isPlaying) { try { FxCombatResidueFX.Pulse(); } catch (System.Exception) { } }
            if (Instance == null) return;
            // [212차] 킬 슬로우모션 호출 폐지 — 사용자 지시(몬스터 처치 급슬로우 불필요; 패막 히트스톱·크리티컬 불릿타임은 유지). 메서드/상수는 재활용 대비 보존.
        }

        /// <summary>치명타/백어택 효과: Shake 2배 + HitStop</summary>
        public static void PlayCrit()
        {
            if (Instance == null) return;
            Instance.PlayCritShake();
            // [테스트 42] 크리티컬 임팩트 국소 슬로우모션 — 0.4배 디핑 후 짧게 홀드 후 복원 (히트스톱 보강)
            BulletTime.Get().Apply(0.4f, 0.08f, 0.10f);
        }

        /// <summary>[70차 후속19/C1] 활 발사 킥 — 가벼운 흔들림만(HitStop 없음, 사격 리듬 유지).</summary>
        public static void PlayFireKick()
        {
            if (Instance == null) return;
            Instance.PlayHitShake(Instance._hitShakeIntensity * 0.4f);
        }

        /// <summary>[무번호 28-M2] 피격 리코일 — 피격 방향 반대로 카메라가 움찔했다 복귀(위치 기반 — FOV 계약 무손상).
        ///   ScreenVignetteFX 23-V1 피격 비네트와 짝을 이루는 카메라 반응. Core 무수정 — OnPlayerDamaged 구독만.
        ///   강도는 피해량 비례(클램프) — 등급/무기 계약 무관.</summary>
        public static void PlayDamageRecoil(Vector3 hitWorldPos, float amount)
        {
            if (Instance == null || Instance._mainCamera == null) return;
            // 피격원→카메라 방향(XZ) — 카메라가 뒤로 움찔(-forward 성분 우세)하게 로컬 바이어스 환산
            Vector3 away = Instance._mainCamera.transform.position - hitWorldPos; away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) away = -Instance._mainCamera.transform.forward;
            Vector3 dir = away.normalized;
            Vector3 bias = new Vector3(
                Vector3.Dot(dir, Instance._mainCamera.transform.right),
                0f,
                Vector3.Dot(dir, Instance._mainCamera.transform.forward));
            var profile = new CameraImpulseProfile
            {
                intensity = Mathf.Clamp(0.02f + amount * 0.004f, 0.02f, 0.08f),
                duration = 0.18f,
                frequency = 18f,
                directionBias = bias,
                useImpulseSource = false,   // Cinemachine 임펄스 미사용 — 기존 ShakeRoutine 경로만(예측 가능성)
            };
            Instance.PlayWeaponShake(profile);
        }

        /// <summary>[무번호 28-M2] 피격 리코일 부트스트랩 — Core 이벤트 구독(Systems→Core 허용 방향,
        ///   Core 무수정 계약). ScreenVignetteFX 23-V1 선례(중복 구독 방지 정적 플래그) 그대로 복제.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SubscribeDamageRecoil()
        {
            // [무번호 28-M2] Core 무수정 계약 — OnPlayerDamaged 구독만(23-V1 선례). 이중 구독 방지 정적 가드.
            if (_damageRecoilHooked) return;
            _damageRecoilHooked = true;
            ProjectName.Core.PlayerHealth.OnPlayerDamaged += (hitPos, amount) => PlayDamageRecoil(hitPos, amount);
        }

        private static bool _damageRecoilHooked;   // [무번호 28-M2] 이중 구독 방지 가드(ScreenVignetteFX _damageHooked 선례)

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

            // 2. 기존 셰이크 루틴도 병행 (호환성) — [무번호 80-P5] additive LateUpdate 경로.
            float intensity = profile.intensity;
            if (ActionFeel.HighSpec)
                intensity *= HighSpecShakeMultiplier;

            StartShake(intensity, profile.duration, profile.frequency);
        }

        // ===== Instance Methods =====

        /// <summary>카메라 위치 랜덤 오프셋 + 원복 (주파수 파라미터 추가)</summary>
        public void PlayHitShake(float intensity)
        {
            if (ActionFeel.HighSpec)
                intensity *= HighSpecShakeMultiplier;

            StartShake(intensity, _hitShakeDuration, 25f);
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

        /// <summary>[무번호 80-P5] additive 셰이크 개시 — 코루틴(ShakeRoutine) 대체.
        ///   LateUpdate(order 1000)에서 팔로우 출력 위에 노이즈를 얹는다(클로버링 방지).</summary>
        private void StartShake(float intensity, float duration, float frequency)
        {
            if (_mainCamera == null) return;
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeRestoreDuration = duration * 0.5f;
            _shakeFrequency = frequency;
            _shakeElapsed = 0f;
            _shakeActive = true;
        }

        /// <summary>[무번호 80-P5] additive 셰이크 — LateUpdate 지배(order 1000).
        ///   팔로우(TopDownCameraController.LateUpdate)가 쓴 출력 위에 마지막 오프셋을 strip 후
        ///   신규 노이즈를 얹는다 → 팔로우와 무간섭, 종료 시 오프셋 제거로 원복(덮어쓰기 없음).
        ///   수식은 기존 ShakeRoutine과 동일(Perlin 3축 + z 감쇠) — 체감 연속성 유지.</summary>
        private void LateUpdate()
        {
            if (_mainCamera == null) return;
            if (!_shakeActive)
            {
                if (_shakeLastOffset != Vector3.zero)
                {
                    _mainCamera.transform.localPosition -= _shakeLastOffset;
                    _shakeLastOffset = Vector3.zero;
                }
                return;
            }

            _shakeElapsed += Time.unscaledDeltaTime;
            float amp;
            if (_shakeElapsed < _shakeDuration)
            {
                amp = _shakeIntensity;
            }
            else
            {
                // 감쇠 램프 — 기존 restore lerp(duration*0.5)를 진폭 감쇠로 대체(체감 연속성).
                float rt = _shakeElapsed - _shakeDuration;
                amp = _shakeIntensity * Mathf.Clamp01(1f - rt / _shakeRestoreDuration);
                if (amp <= 0f)
                {
                    _mainCamera.transform.localPosition -= _shakeLastOffset;
                    _shakeLastOffset = Vector3.zero;
                    _shakeActive = false;
                    return;
                }
            }

            // 주파수 기반 진동 (기존 ShakeRoutine 수식 동일 — 체감 연속성)
            float noise = Mathf.PerlinNoise(Time.unscaledTime * _shakeFrequency, 0f) * 2f - 1f;
            float noise2 = Mathf.PerlinNoise(0f, Time.unscaledTime * _shakeFrequency) * 2f - 1f;
            float noise3 = Mathf.PerlinNoise(Time.unscaledTime * _shakeFrequency * 0.7f, Time.unscaledTime * _shakeFrequency * 0.7f) * 2f - 1f;
            Vector3 randomOffset = new Vector3(noise, noise2, noise3 * 0.3f) * amp;
            randomOffset.z = randomOffset.z * 0.3f; // Z축은 덜 흔들림 (클리핑 방지)

            // additive: 현재 localPosition에서 마지막 적용분을 strip 후 신규 오프셋 얹기
            Vector3 basePos = _mainCamera.transform.localPosition - _shakeLastOffset;
            _mainCamera.transform.localPosition = basePos + randomOffset;
            _shakeLastOffset = randomOffset;
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