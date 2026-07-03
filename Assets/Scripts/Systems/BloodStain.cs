using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 37-02: 바닥 혈흔 — 범죄 현장/전투 흔적 연출.
    /// DecalProjector 또는 Quad 머티리얼 기반으로 바닥에 혈흔을 렌더링합니다.
    /// 여러 혈흔 타입(신선함/마름/대량/흔적)을 지원합니다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class BloodStain : MonoBehaviour
    {
        [Header("혈흔 설정")]
        [SerializeField] private BloodStainType _stainType = BloodStainType.Fresh;
        [SerializeField] private float _stainSize = 1f;
        [SerializeField] private Color _freshColor = new Color(0.6f, 0.02f, 0.02f, 0.8f);
        [SerializeField] private Color _driedColor = new Color(0.25f, 0.05f, 0.03f, 0.6f);
        [SerializeField] private Color _massiveColor = new Color(0.5f, 0.02f, 0.02f, 0.9f);
        [SerializeField] private Color _trailColor = new Color(0.4f, 0.02f, 0.02f, 0.4f);

        [Header("랜덤 변형")]
        [SerializeField] private bool _randomizeRotation = true;
        [SerializeField] private bool _randomizeScale = true;
        [SerializeField] private Vector2 _scaleRange = new Vector2(0.7f, 1.3f);

        [Header("Fade Out")]
        [SerializeField] private bool _fadeOverTime;
        [SerializeField] private float _fadeDuration = 60f; // 초

        // ===== 혈흔 타입 =====
        public enum BloodStainType
        {
            Fresh,   // 신선한 혈흔 (선명한 빨강)
            Dried,   // 마른 혈흔 (어두운 갈색)
            Massive, // 대량 혈흔 (넓은 범위)
            Trail    // 흔적 (연하게, 좁게)
        }

        // ===== 상태 =====
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;
        private float _elapsedTime;
        private Color _currentColor;
        private bool _initialized;

        // ================================================================
        // Unity 생명주기
        // ================================================================

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            InitializeStain();
        }

        private void Update()
        {
            if (_fadeOverTime && _initialized)
            {
                _elapsedTime += Time.deltaTime;

                if (_elapsedTime < _fadeDuration)
                {
                    float t = _elapsedTime / _fadeDuration;
                    Color faded = _currentColor;
                    faded.a = Mathf.Lerp(_currentColor.a, 0f, t);
                    ApplyColor(faded);
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 혈흔 타입을 변경하고 시각을 즉시 업데이트합니다.
        /// </summary>
        public void SetStainType(BloodStainType type)
        {
            _stainType = type;
            InitializeStain();
        }

        /// <summary>
        /// 혈흔을 즉시 사라지게 합니다(Fade Out 없이).
        /// </summary>
        public void ClearStain()
        {
            gameObject.SetActive(false);
        }

        // ================================================================
        // 내부 메서드
        // ================================================================

        private void InitializeStain()
        {
            if (_renderer == null) return;

            // 랜덤 회전
            if (_randomizeRotation)
            {
                transform.rotation = Quaternion.Euler(
                    transform.rotation.eulerAngles.x,
                    Random.Range(0f, 360f),
                    transform.rotation.eulerAngles.z
                );
            }

            // 랜덤 크기
            if (_randomizeScale)
            {
                float scaleFactor = Random.Range(_scaleRange.x, _scaleRange.y);
                transform.localScale = new Vector3(
                    transform.localScale.x * scaleFactor,
                    transform.localScale.y,
                    transform.localScale.z * scaleFactor
                );
            }

            // 혈흔 타입별 색상
            _currentColor = _stainType switch
            {
                BloodStainType.Fresh => _freshColor,
                BloodStainType.Dried => _driedColor,
                BloodStainType.Massive => _massiveColor,
                BloodStainType.Trail => _trailColor,
                _ => _freshColor
            };

            ApplyColor(_currentColor);
            _initialized = true;
        }

        private void ApplyColor(Color color)
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", color);
            _propBlock.SetColor("_Color", color);
            _renderer.SetPropertyBlock(_propBlock);
        }

        // ================================================================
        // Editor
        // ================================================================

        private void OnValidate()
        {
            if (Application.isPlaying) return;

            // 에디터에서 타입 변경 시 즉시 미리보기
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                Color previewColor = _stainType switch
                {
                    BloodStainType.Fresh => _freshColor,
                    BloodStainType.Dried => _driedColor,
                    BloodStainType.Massive => _massiveColor,
                    BloodStainType.Trail => _trailColor,
                    _ => _freshColor
                };

                _propBlock ??= new MaterialPropertyBlock();
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_BaseColor", previewColor);
                _renderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}