using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 37-01: 씬에 배치되는 상호작용 가능한 문서 오브젝트.
    /// E키로 접근 시 ReadDocumentWindow를 열어 문서 내용을 표시합니다.
    /// 중요도에 따라 ParticleSystem 하이라이트 효과가 적용됩니다.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class InteractableDocument : MonoBehaviour
    {
        [Header("문서 데이터")]
        [SerializeField] private ReadableDocument _documentData;
        public ReadableDocument DocumentData => _documentData;

        [Header("상호작용 설정")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;
        [SerializeField] private float _interactRadius = 2.5f;
        [SerializeField] private LayerMask _playerLayer = 1; // Default Layer

        [Header("파티클 효과")]
        [SerializeField] private ParticleSystem _highlightParticle;
        [SerializeField] private Color _normalParticleColor = Color.white;
        [SerializeField] private Color _importantParticleColor = new Color(1f, 0.84f, 0f); // 황금빛
        [SerializeField] private Color _questParticleColor = new Color(0f, 0.8f, 1f); // 청록색 (퀘스트)

        [Header("프롬프트 UI")]
        [SerializeField] private string _promptText = "[E] 읽기";

        // ===== 상태 =====
        private SphereCollider _sphereCollider;
        private bool _playerInRange;
        private bool _alreadyDiscovered;
        private ParticleSystem.MainModule _particleMain;
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;

        // ===== 이벤트 =====
        /// <summary>문서가 처음 발견되었을 때 발생</summary>
        public event System.Action<ReadableDocument> OnDocumentDiscovered;

        // ================================================================
        // Unity 생명주기
        // ================================================================

        private void Awake()
        {
            _sphereCollider = GetComponent<SphereCollider>();
            _sphereCollider.isTrigger = true;
            _sphereCollider.radius = _interactRadius;

            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();

            if (_highlightParticle != null)
            {
                _particleMain = _highlightParticle.main;
                ApplyParticleColor();
            }
        }

        private void Start()
        {
            // 중요 문서 자동 효과
            if (_documentData != null)
                ApplyDocumentVisuals();
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
            if (_playerInRange && Input.GetKeyDown(_interactKey))
            {
                TryReadDocument();
            }
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 문서 읽기 시도. ReadDocumentWindow를 통해 UI를 표시합니다.
        /// </summary>
        public void TryReadDocument()
        {
            if (_documentData == null)
            {
                Debug.LogWarning($"[InteractableDocument] No document data assigned on {gameObject.name}");
                return;
            }

            // 문서 읽기 UI 열기
            ReadDocumentWindow.Instance?.ShowDocument(_documentData);

            // 최초 발견 처리
            if (!_alreadyDiscovered)
            {
                _alreadyDiscovered = true;
                OnDocumentDiscovered?.Invoke(_documentData);
                AmbientDialogueManager.Instance?.RegisterDiscovery(_documentData.DocumentId);

                Debug.Log($"[InteractableDocument] Discovered: {_documentData.Title} ({_documentData.DocumentId})");
            }
        }

        /// <summary>
        /// 외부에서 문서 데이터를 설정 (런타임 동적 할당용).
        /// </summary>
        public void SetDocumentData(ReadableDocument data)
        {
            _documentData = data;
            ApplyDocumentVisuals();
        }

        // ================================================================
        // 내부 메서드
        // ================================================================

        private void ApplyDocumentVisuals()
        {
            if (_documentData == null) return;

            // 문서 중요도에 따라 파티클 색상 변경
            ApplyParticleColor();

            // 이름 설정
            gameObject.name = $"Document_{_documentData.DocumentId}";
        }

        private void ApplyParticleColor()
        {
            if (_highlightParticle == null || _documentData == null) return;

            Color targetColor = _normalParticleColor;

            switch (_documentData.Importance)
            {
                case ReadableDocument.DocumentImportance.Normal:
                    targetColor = _normalParticleColor;
                    break;
                case ReadableDocument.DocumentImportance.Important:
                    targetColor = _importantParticleColor;
                    break;
                case ReadableDocument.DocumentImportance.QuestRequired:
                    targetColor = _questParticleColor;
                    break;
            }

            _particleMain.startColor = targetColor;
        }

        // ================================================================
        // Gizmos
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, _interactRadius);
        }
    }
}