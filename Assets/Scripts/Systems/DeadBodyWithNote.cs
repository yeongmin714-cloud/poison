using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 37-02: 시체 + 메모 조합 — 범죄 현장 연출용 오브젝트.
    /// E키 상호작용으로 메모 내용을 읽고, 선택적으로 ReadDocumentWindow를 엽니다.
    /// 시체 주변에 혈흔(BloodStain)이 함께 배치됩니다.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class DeadBodyWithNote : MonoBehaviour
    {
        [Header("메모 데이터")]
        [SerializeField] private ReadableDocument _noteData;

        [Header("상호작용 설정")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;
        [SerializeField] private float _interactRadius = 2.5f;
        [SerializeField] private LayerMask _playerLayer = 1; // Default Layer

        [Header("시체 설정")]
        [SerializeField] private GameObject _bodyModel;
        [SerializeField] private bool _destroyOnRead;
        [SerializeField, Tooltip("시체 Fade Out 시간 (0 = 즉시 파괴)")] 
        private float _fadeOutDuration = 0f;

        [Header("사운드")]
        [SerializeField] private string _discoverSoundId = "env_body_discover";
        [SerializeField] private AudioSource _audioSource;

        [Header("연결된 혈흔")]
        [SerializeField] private BloodStain[] _linkedBloodStains;

        // ===== 상태 =====
        private SphereCollider _sphereCollider;
        private bool _playerInRange;
        private bool _alreadyRead;
        private Renderer[] _bodyRenderers;
        private MaterialPropertyBlock _propBlock;
        private float _fadeTimer;

        // ================================================================
        // Unity 생명주기
        // ================================================================

        private void Awake()
        {
            _sphereCollider = GetComponent<SphereCollider>();
            _sphereCollider.isTrigger = true;
            _sphereCollider.radius = _interactRadius;

            if (_bodyModel != null)
            {
                _bodyRenderers = _bodyModel.GetComponentsInChildren<Renderer>();
            }

            _propBlock = new MaterialPropertyBlock();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_playerInRange && ((1 << other.gameObject.layer) & _playerLayer) != 0)
            {
                _playerInRange = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & _playerLayer) != 0)
            {
                _playerInRange = false;
            }
        }

        private void Update()
        {
            if (_playerInRange && Input.GetKeyDown(_interactKey) && !_alreadyRead)
            {
                TryReadNote();
            }

            // Fade Out 처리
            if (_fadeTimer > 0f)
            {
                _fadeTimer -= Time.deltaTime;
                float alpha = Mathf.Clamp01(_fadeTimer / _fadeOutDuration);
                SetBodyAlpha(alpha);

                if (_fadeTimer <= 0f)
                {
                    Destroy(gameObject);
                }
            }
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 메모 읽기 시도.
        /// </summary>
        public void TryReadNote()
        {
            if (_alreadyRead) return;
            _alreadyRead = true;

            // 사운드 재생
            PlayDiscoverSound();

            // 메모가 있으면 UI 열기 (정적 델리게이트 브리지 사용)
            if (_noteData != null)
            {
                InteractableDocument.OnDocumentReadRequested?.Invoke(_noteData);

                AmbientDialogueManager.Instance?.RegisterDiscovery(_noteData.DocumentId);
            }
            else
            {
                Debug.Log($"[DeadBodyWithNote] 시체에서 추가 단서는 발견되지 않았습니다. ({gameObject.name})");
            }

            Debug.Log($"[DeadBodyWithNote] 시체 발견: {gameObject.name}");

            if (_destroyOnRead)
            {
                StartFadeOut();
            }
        }

        /// <summary>
        /// 외부에서 메모 데이터 설정.
        /// </summary>
        public void SetNoteData(ReadableDocument note)
        {
            _noteData = note;
        }

        // ================================================================
        // 내부 메서드
        // ================================================================

        private void PlayDiscoverSound()
        {
            if (_audioSource != null && !string.IsNullOrEmpty(_discoverSoundId))
            {
                // SoundEffectManager를 통한 재생 (추후 구현)
                _audioSource.Play();
            }
        }

        private void StartFadeOut()
        {
            if (_fadeOutDuration > 0f)
            {
                _fadeTimer = _fadeOutDuration;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetBodyAlpha(float alpha)
        {
            if (_bodyRenderers == null) return;

            foreach (var renderer in _bodyRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_propBlock);
                Color color = renderer.sharedMaterial.color;
                color.a = alpha;
                _propBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(_propBlock);
            }
        }

        // ================================================================
        // Gizmos
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.2f);
            Gizmos.DrawSphere(transform.position, _interactRadius);
        }
    }
}