using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C19-01: 플레이어 사망 VFX + C19-02: 사망 UI/페이드 IMGUI 컨트롤러.
    /// 모든 UI 효과는 OnGUI()에서 Texture2D + GUI.DrawTexture (IMGUI)로 구현.
    /// </summary>
    public class DeathEffectController : MonoBehaviour
    {
        // ==================================================================
        // 싱글톤
        // ==================================================================
        private static DeathEffectController _instance;
        private static bool _instanceQuitting = false;

        /// <summary>DeathEffectController 싱글톤 인스턴스</summary>
        public static DeathEffectController Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("DeathEffectController");
                    _instance = go.AddComponent<DeathEffectController>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ==================================================================
        // 텍스처 (OnGUI 용)
        // ==================================================================
        private Texture2D _whiteTexture;
        private GUIStyle _deathTextStyle;
        private GUIStyle _countdownStyle;

        // ==================================================================
        // 타이밍 (모든 효과는 unscaled time 사용 — SlowMo 영향 안 받음)
        // ==================================================================
        private bool _isPlaying = false;

        // --- 플래시 ---
        private bool _isFlashing = false;
        private float _flashStartTime;
        private const float FlashDuration = 0.5f;

        // --- 파티클 ---
        private List<GameObject> _particles = new List<GameObject>();
        private const float ParticleLifetime = 1f;

        // --- 카메라 셰이크 ---
        private Camera _mainCamera;
        private bool _isShaking = false;
        private float _shakeStartTime;
        private Vector3 _shakeOriginalPosition;
        private const float ShakeDuration = 0.3f;
        private const float ShakeIntensity = 0.3f;

        // --- 슬로우 모션 ---
        private bool _isSlomo = false;
        private float _slomoStartTime;
        private const float SlomoHoldDuration = 0.5f;
        private const float SlomoLerpDuration = 0.5f;

        // --- 페이드 투 블랙 ---
        private bool _isFadingToBlack = false;
        private float _fadeToBlackStartTime;
        private float _fadeToBlackAlpha = 0f;
        private const float FadeToBlackDuration = 1.5f;

        // --- 사망 텍스트 ---
        private bool _isShowingDeathText = false;
        private float _deathTextStartTime;
        private const float DeathTextDuration = 3f;

        // --- 리스폰 카운트다운 ---
        private bool _isCountingDown = false;
        private float _countdownStartTime;
        private int _currentCount = 3;

        // --- 페이드 인 (부활 시) ---
        private bool _isFadingIn = false;
        private float _fadeInStartTime;
        private float _fadeInAlpha = 0f;
        private const float FadeInDuration = 1f;

        // ==================================================================
        // 생애주기
        // ==================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _instanceQuitting = false;
            DontDestroyOnLoad(gameObject);

            // 1×1 흰색 텍스처 (GUI.DrawTexture + GUI.color 로 모든 색 표현)
            _whiteTexture = new Texture2D(1, 1);
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();

#if UNITY_EDITOR
            // 테스트 환경에서 PlayerHealth 없을 수 있으므로 안전하게 구독
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.OnPlayerDied += PlayDeathEffects;
                PlayerHealth.OnPlayerRespawned += StartFadeIn;
            }
#else
            PlayerHealth.OnPlayerDied += PlayDeathEffects;
            PlayerHealth.OnPlayerRespawned += StartFadeIn;
#endif
        }

        private void Start()
        {
            _mainCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;

                // 구독 해제
#if UNITY_EDITOR
                if (PlayerHealth.Instance != null)
#endif
                {
                    PlayerHealth.OnPlayerDied -= PlayDeathEffects;
                    PlayerHealth.OnPlayerRespawned -= StartFadeIn;
                }
            }

            CleanupParticles();

            if (_whiteTexture != null)
            {
                Destroy(_whiteTexture);
                _whiteTexture = null;
            }
        }

        private void OnApplicationQuit()
        {
            _instanceQuitting = true;
        }

        // ==================================================================
        // 퍼블릭 API
        // ==================================================================

        /// <summary>
        /// 사망 시 모든 VFX + UI 효과를 시작합니다.
        /// PlayerHealth.Die() 에서 호출 (이벤트 구독으로 자동 연결).
        /// </summary>
        public void PlayDeathEffects()
        {
            if (_isPlaying) return;
            _isPlaying = true;

            StartFlash();
            StartParticles();
            StartCameraShake();
            StartSlowMotion();
            StartFadeToBlack();
            StartDeathText();
            StartCountdown();
        }

        /// <summary>
        /// 부활 시 페이드 인을 시작합니다.
        /// PlayerHealth.Respawn() 에서 호출 (이벤트 구독으로 자동 연결).
        /// </summary>
        public void StartFadeIn()
        {
            _isFadingIn = true;
            _fadeInStartTime = Time.unscaledTime;
            _fadeInAlpha = 1f;
            _isFadingToBlack = false;
        }

        // ==================================================================
        // 내부 — 개별 효과 시작
        // ==================================================================

        private void StartFlash()
        {
            _isFlashing = true;
            _flashStartTime = Time.unscaledTime;
        }

        private void StartParticles()
        {
            CleanupParticles();
            _particles.Clear();

            for (int i = 0; i < 20; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"DeathParticle_{i}";
                sphere.transform.position = transform.position + Random.insideUnitSphere * 0.5f;
                sphere.transform.localScale = Vector3.one * Random.Range(0.1f, 0.3f);

                var rend = sphere.GetComponent<Renderer>();
                if (rend != null)
                {
                    Color color = Random.value > 0.5f ? Color.red : Color.black;
                    rend.material.color = color;
                }

                var rb = sphere.AddComponent<Rigidbody>();
                rb.linearVelocity = Random.onUnitSphere * Random.Range(3f, 8f);

                _particles.Add(sphere);
                Destroy(sphere, ParticleLifetime);
            }
        }

        private void StartCameraShake()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;
            if (_mainCamera == null)
                return;

            _isShaking = true;
            _shakeStartTime = Time.unscaledTime;
            _shakeOriginalPosition = _mainCamera.transform.position;
        }

        private void StartSlowMotion()
        {
            _isSlomo = true;
            _slomoStartTime = Time.unscaledTime;
            Time.timeScale = 0.3f;
        }

        private void StartFadeToBlack()
        {
            _isFadingToBlack = true;
            _fadeToBlackStartTime = Time.unscaledTime;
            _fadeToBlackAlpha = 0f;
        }

        private void StartDeathText()
        {
            _isShowingDeathText = true;
            _deathTextStartTime = Time.unscaledTime;
        }

        private void StartCountdown()
        {
            _isCountingDown = true;
            _countdownStartTime = Time.unscaledTime;
            _currentCount = 3;
        }

        // ==================================================================
        // Update — 실시간 효과 (카메라 셰이크, 슬로우 모션 복원 등)
        // ==================================================================

        private void Update()
        {
            if (!_isPlaying) return;

            float now = Time.unscaledTime;

            // --- 카메라 셰이크 ---
            if (_isShaking)
            {
                float elapsed = now - _shakeStartTime;
                if (elapsed < ShakeDuration)
                {
                    if (_mainCamera != null)
                    {
                        Vector3 offset = Random.insideUnitSphere * ShakeIntensity;
                        offset.z = 0f;
                        _mainCamera.transform.position = _shakeOriginalPosition + offset;
                    }
                }
                else
                {
                    if (_mainCamera != null)
                        _mainCamera.transform.position = _shakeOriginalPosition;
                    _isShaking = false;
                }
            }

            // --- 슬로우 모션 Lerp 복원 ---
            if (_isSlomo)
            {
                float elapsed = now - _slomoStartTime;
                if (elapsed > SlomoHoldDuration)
                {
                    float lerpElapsed = elapsed - SlomoHoldDuration;
                    float t = Mathf.Clamp01(lerpElapsed / SlomoLerpDuration);
                    Time.timeScale = Mathf.Lerp(0.3f, 1.0f, t);

                    if (t >= 1f)
                    {
                        Time.timeScale = 1f;
                        _isSlomo = false;
                    }
                }
            }

            // --- 페이드 투 블랙 알파 갱신 ---
            if (_isFadingToBlack)
            {
                float elapsed = now - _fadeToBlackStartTime;
                _fadeToBlackAlpha = Mathf.Clamp01(elapsed / FadeToBlackDuration);
            }

            // --- 페이드 인 알파 갱신 ---
            if (_isFadingIn)
            {
                float elapsed = now - _fadeInStartTime;
                _fadeInAlpha = 1f - Mathf.Clamp01(elapsed / FadeInDuration);
                if (elapsed >= FadeInDuration)
                {
                    _fadeInAlpha = 0f;
                    _isFadingIn = false;
                    _isPlaying = false;
                }
            }

            // --- 카운트다운 갱신 ---
            if (_isCountingDown)
            {
                float elapsed = now - _countdownStartTime;
                int newCount = 3 - Mathf.FloorToInt(elapsed);
                if (newCount != _currentCount && newCount >= 0)
                {
                    _currentCount = newCount;
                }
                if (newCount < 0)
                {
                    _isCountingDown = false;
                }
            }
        }

        // ==================================================================
        // OnGUI — 모든 IMGUI 렌더링 (플래시, 페이드, 텍스트, 카운트다운)
        // ==================================================================

        private void OnGUI()
        {
            float now = Time.unscaledTime;

            // 1) 화면 플래시 (빨간색, 0.5초, 페이드 아웃)
            if (_isFlashing)
            {
                float elapsed = now - _flashStartTime;
                if (elapsed < FlashDuration)
                {
                    float alpha = 1f - (elapsed / FlashDuration);
                    Color oldColor = GUI.color;
                    GUI.color = new Color(1f, 0f, 0f, alpha * 0.5f);
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whiteTexture);
                    GUI.color = oldColor;
                }
                else
                {
                    _isFlashing = false;
                }
            }

            // 2) 페이드 투 블랙 (검은색, 1.5초, alpha 0→1)
            if (_isFadingToBlack && _fadeToBlackAlpha > 0.001f)
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, _fadeToBlackAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whiteTexture);
                GUI.color = oldColor;
            }

            // 3) 페이드 인 (검은색 → 투명, 1초)
            if (_isFadingIn && _fadeInAlpha > 0.001f)
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, _fadeInAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whiteTexture);
                GUI.color = oldColor;
            }

            // 4) 사망 텍스트 "💀 사망했습니다..." (중앙, 빨간색, 36pt, 3초 페이드)
            if (_isShowingDeathText)
            {
                float elapsed = now - _deathTextStartTime;

                // 마지막 1초 동안 페이드 아웃
                float textAlpha = 1f;
                if (elapsed > DeathTextDuration - 1f)
                {
                    textAlpha = Mathf.Clamp01((DeathTextDuration - elapsed) / 1f);
                }

                if (textAlpha > 0.001f)
                {
                    if (_deathTextStyle == null)
                    {
                        _deathTextStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 36,
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = Color.red }
                        };
                    }

                    Color oldColor = _deathTextStyle.normal.textColor;
                    _deathTextStyle.normal.textColor = new Color(1f, 0f, 0f, textAlpha);
                    GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60), "💀 사망했습니다...", _deathTextStyle);
                    _deathTextStyle.normal.textColor = oldColor;
                }
            }

            // 5) 리스폰 카운트다운 "3... 2... 1..." (중앙, 흰색, 48pt)
            if (_isCountingDown && _currentCount >= 1)
            {
                float elapsed = now - _countdownStartTime;
                float countAlpha = Mathf.Clamp01(1f - (elapsed % 1f) * 2f); // 1초마다 깜빡

                if (_countdownStyle == null)
                {
                    _countdownStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 48,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    };
                }

                string countText = $"{_currentCount}...";

                Color oldColor = _countdownStyle.normal.textColor;
                _countdownStyle.normal.textColor = new Color(1f, 1f, 1f, countAlpha);
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60), countText, _countdownStyle);
                _countdownStyle.normal.textColor = oldColor;
            }
        }

        // ==================================================================
        // 정리
        // ==================================================================

        private void CleanupParticles()
        {
            foreach (var p in _particles)
            {
                if (p != null)
                    Destroy(p);
            }
            _particles.Clear();
        }
    }
}