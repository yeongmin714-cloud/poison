using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 37-02: 묘비 — E키 상호작용으로 묘비에 새겨진 글을 읽습니다.
    /// 묘비 데이터(GravestoneData)를 ScriptableObject로 참조하여 표시합니다.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Gravestone : MonoBehaviour
    {
        [Header("묘비 데이터")]
        [SerializeField] private GravestoneData _gravestoneData;

        [Header("상호작용")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;
        [SerializeField] private float _interactRadius = 2.5f;
        [SerializeField] private LayerMask _playerLayer = 1; // Default Layer

        [Header("묘비 표시 설정")]
        [SerializeField] private bool _showEpitaphOnScreen = true;
        [SerializeField] private float _displayDuration = 5f;

        [Header("GUI 표시 (In-World)")]
        [SerializeField] private Vector2 _guiOffset = new Vector2(0, -50);
        [SerializeField] private Color _guiTextColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private int _guiFontSize = 14;

        [Header("사운드")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _readSound;

        // ===== 상태 =====
        private SphereCollider _sphereCollider;
        private bool _playerInRange;
        private bool _hasBeenRead;
        private float _displayTimer;

        // 화면 표시용 텍스트
        private string _displayName = "";
        private string _displayEpitaph = "";

        // IMGUI 스타일 (OnGUI에서 매번 할당 방지)
        private GUIStyle _guiStyle;

        // ================================================================
        // 내부 데이터 클래스
        // ================================================================

        /// <summary>
        /// 묘비 데이터 (Inspector에서 직접 입력, 또는 ScriptableObject 연결).
        /// </summary>
        [System.Serializable]
        public class GravestoneData
        {
            [Header("묘비 정보")]
            public string personName = "이름 없음";
            public string birthYear = "???";
            public string deathYear = "???";
            [TextArea(3, 6)] public string epitaph = "...";
            public string locationDescription = "";
            public bool isNotable; // 주요 인물인 경우 강조 표시
        }

        // ================================================================
        // Unity 생명주기
        // ================================================================

        private void Awake()
        {
            _sphereCollider = GetComponent<SphereCollider>();
            _sphereCollider.isTrigger = true;
            _sphereCollider.radius = _interactRadius;
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
                TryReadGravestone();
            }

            // 타이머 감소
            if (_displayTimer > 0f)
            {
                _displayTimer -= Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            // 화면 중앙에 묘비 정보 표시
            if (_displayTimer > 0f && _showEpitaphOnScreen)
            {
                if (_guiStyle == null)
                {
                    _guiStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = _guiFontSize,
                        fontStyle = FontStyle.Italic,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = _guiTextColor },
                        wordWrap = true
                    };
                }

                float alpha = Mathf.Clamp01(_displayTimer / 2f); // 2초간 Fade Out
                Color original = _guiStyle.normal.textColor;
                _guiStyle.normal.textColor = new Color(original.r, original.g, original.b, alpha);

                float centerX = Screen.width * 0.5f;
                float centerY = Screen.height * 0.6f;
                float width = 400f;
                float height = 200f;

                Rect displayRect = new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height);
                string displayText = $"<b>{_displayName}</b>\n{_displayEpitaph}";

                GUI.Box(displayRect, "");
                GUI.Label(displayRect, displayText, _guiStyle);

                _guiStyle.normal.textColor = original;
            }
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 묘비 읽기.
        /// </summary>
        public void TryReadGravestone()
        {
            if (_gravestoneData == null)
            {
                Debug.LogWarning($"[Gravestone] No data assigned on {gameObject.name}");
                return;
            }

            _hasBeenRead = true;

            // 표시 텍스트 설정
            if (string.IsNullOrEmpty(_gravestoneData.epitaph))
            {
                _displayEpitaph = "...";
            }
            else
            {
                _displayEpitaph = _gravestoneData.epitaph;
            }

            string years = $"({_gravestoneData.birthYear} - {_gravestoneData.deathYear})";
            _displayName = $"{_gravestoneData.personName} {years}";

            _displayTimer = _displayDuration;

            // 사운드 재생
            if (_audioSource != null && _readSound != null)
            {
                _audioSource.PlayOneShot(_readSound);
            }

            // AmbientDialogueManager에 등록
            AmbientDialogueManager.Instance?.RegisterGravestoneRead(_gravestoneData.personName);

            Debug.Log($"[Gravestone] 읽음: {_gravestoneData.personName} - \"{_gravestoneData.epitaph}\" ");
        }

        /// <summary>
        /// 외부에서 묘비 데이터 설정.
        /// </summary>
        public void SetGravestoneData(GravestoneData data)
        {
            _gravestoneData = data;
        }

        // ================================================================
        // Gizmos
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.15f);
            Gizmos.DrawSphere(transform.position, _interactRadius);

            if (_gravestoneData != null)
            {
                // 묘비 이름 표시
                UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, _gravestoneData.personName);
            }
        }
    }
}