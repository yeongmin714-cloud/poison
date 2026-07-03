using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 37-02: 저주받은 물건 — 접근 시 속삭임/효과음/시각 효과.
    /// PlayerPrefs 볼륨 설정을 존중하며, 플레이어가 가까이 갈수록 효과가 강해집니다.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class CursedObject : MonoBehaviour
    {
        [Header("저주 설정")]
        [SerializeField] private string _objectName = "저주받은 물건";
        [SerializeField, TextArea(3, 10)] private string _curseDescription = "이 물건 주변에서 불길한 기운이 느껴진다...";
        [SerializeField] private float _curseRadius = 8f;
        [SerializeField] private float _intenseRadius = 2f; // 강력한 효과 반경

        [Header("사운드")]
        [SerializeField] private AudioClip _whisperSound;
        [SerializeField] private AudioClip _intenseSound;
        [SerializeField] private float _whisperVolume = 0.3f;
        [SerializeField] private float _intenseVolume = 0.6f;
        [SerializeField] private float _soundCooldown = 3f;
        [SerializeField] private AudioSource _audioSource;

        [Header("시각 효과")]
        [SerializeField] private ParticleSystem _ambientParticle;
        [SerializeField] private Light _ambientLight;
        [SerializeField] private Color _ambientLightColor = new Color(0.2f, 0.8f, 0.2f); // 불길한 녹색
        [SerializeField] private float _lightIntensityBase = 0.5f;

        [Header("화면 효과 (Post-Processing)")]
        [SerializeField] private bool _enableVignette = true;
        [SerializeField] private float _vignetteIntensity = 0.3f;

        [Header("상호작용")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;
        [SerializeField] private string _promptText = "[E] 조사하기";

        // ===== 상태 =====
        private SphereCollider _sphereCollider;
        private Transform _playerTransform;
        private float _soundTimer;
        private bool _playerInRange;
        private bool _playerInIntenseRange;
        private float _curseProximity; // 0~1, 가까울수록 1

        // PlayerPrefs 키
        private const string SFX_VOLUME_KEY = "SFXVolume";
        private const float DEFAULT_SFX_VOLUME = 0.8f;

        // ================================================================
        // Unity 생명주기
        // ================================================================

        private void Awake()
        {
            _sphereCollider = GetComponent<SphereCollider>();
            _sphereCollider.isTrigger = true;
            _sphereCollider.radius = _curseRadius;

            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.loop = false;
            _audioSource.spatialBlend = 1f; // 3D 사운드
            _audioSource.maxDistance = _curseRadius;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerTransform = other.transform;
                _playerInRange = true;
                Debug.Log($"[CursedObject] 플레이어가 '{_objectName}'의 영역에 들어왔습니다.");
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player") && _playerTransform != null)
            {
                float distance = Vector3.Distance(transform.position, _playerTransform.position);
                _curseProximity = 1f - Mathf.Clamp01(distance / _curseRadius);
                _playerInIntenseRange = distance <= _intenseRadius;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerTransform = null;
                _playerInRange = false;
                _playerInIntenseRange = false;
                _curseProximity = 0f;
            }
        }

        private void Update()
        {
            if (!_playerInRange) return;

            // 근접도 기반 효과 업데이트
            UpdateSoundEffect();
            UpdateVisualEffect();
            UpdateLightEffect();

            // E키 상호작용 (강한 영역 내에서만)
            if (_playerInIntenseRange && Input.GetKeyDown(_interactKey))
            {
                TryInteract();
            }
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 저주받은 물건과 상호작용.
        /// </summary>
        public void TryInteract()
        {
            Debug.Log($"[CursedObject] {_objectName}: {_curseDescription}");

            // AmbientDialogueManager에 메시지 전송
            AmbientDialogueManager.Instance?.RegisterCursedObjectInteraction(_objectName);

            // 강력한 사운드 재생
            PlayIntenseSound();
        }

        // ================================================================
        // 내부 메서드
        // ================================================================

        private void UpdateSoundEffect()
        {
            float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, DEFAULT_SFX_VOLUME);
            _soundTimer -= Time.deltaTime;

            if (_soundTimer <= 0f && sfxVolume > 0.01f)
            {
                _soundTimer = _soundCooldown * (1f - _curseProximity * 0.7f); // 가까울수록 짧은 간격

                if (_playerInIntenseRange && _intenseSound != null)
                {
                    _audioSource.PlayOneShot(_intenseSound, _intenseVolume * sfxVolume);
                }
                else if (_whisperSound != null)
                {
                    _audioSource.PlayOneShot(_whisperSound, _whisperVolume * sfxVolume);
                }
            }
        }

        private void PlayIntenseSound()
        {
            float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, DEFAULT_SFX_VOLUME);

            if (_intenseSound != null)
            {
                _audioSource.PlayOneShot(_intenseSound, _intenseVolume * sfxVolume * 1.5f);
            }
        }

        private void UpdateVisualEffect()
        {
            if (_ambientParticle != null)
            {
                var emission = _ambientParticle.emission;
                emission.rateOverTime = Mathf.Lerp(0f, 20f, _curseProximity);

                var main = _ambientParticle.main;
                main.startSize = Mathf.Lerp(0.1f, 0.5f, _curseProximity);
            }
        }

        private void UpdateLightEffect()
        {
            if (_ambientLight != null)
            {
                _ambientLight.enabled = _curseProximity > 0.1f;
                _ambientLight.intensity = _lightIntensityBase * _curseProximity;
                _ambientLight.color = _ambientLightColor;

                // 깜빡임 효과
                float flicker = 1f + Mathf.Sin(Time.time * 5f) * 0.2f;
                _ambientLight.intensity *= flicker;
            }
        }

        // ================================================================
        // Gizmos
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            // 일반 범위
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.15f);
            Gizmos.DrawSphere(transform.position, _curseRadius);

            // 강한 범위
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, _intenseRadius);
        }
    }
}