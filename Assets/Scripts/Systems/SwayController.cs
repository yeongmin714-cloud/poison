using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 바람에 의한 풀/나무 흔들림 애니메이션 (Rotation Oscillation + Position Bobbing).
    /// WindZone의 방향과 강도에 영향받으며, 카메라 거리 50m 이상이면 비활성화.
    /// </summary>
    public class SwayController : MonoBehaviour
    {
        [Header("Sway Settings")]
        [SerializeField, Range(1f, 3f)] private float _swaySpeed = 1.5f;
        [SerializeField, Range(0f, 5f)] private float _swayAmount = 2f;
        [SerializeField, Range(0.5f, 2f)] private float _bobSpeed = 1f;
        [SerializeField, Range(0f, 0.05f)] private float _bobAmount = 0.02f;

        [Header("Wind")]
        [SerializeField] private float _windInfluence = 0.3f;

        [Header("Performance")]
        [SerializeField] private float _cullDistance = 50f;

        // 내부 상태
        private float _swayOffset;
        private float _bobOffset;
        private Quaternion _initialRotation;
        private Vector3 _initialPosition;
        private Camera _mainCamera;
        private WindZone _windZone;
        private bool _isCulled;

        // ================================================================
        // Properties (테스트/인스펙터 접근용)
        // ================================================================

        public float SwaySpeed => _swaySpeed;
        public float SwayAmount => _swayAmount;
        public float BobSpeed => _bobSpeed;
        public float BobAmount => _bobAmount;
        public float WindInfluence => _windInfluence;
        public float CullDistance => _cullDistance;
        public bool IsCulled => _isCulled;
        public float SwayOffset => _swayOffset;
        public float BobOffset => _bobOffset;
        public Quaternion InitialRotation => _initialRotation;
        public Vector3 InitialPosition => _initialPosition;

        // ================================================================
        // Public Setters (Installer / Tests 용)
        // ================================================================

        public void SetSwaySpeed(float value) { _swaySpeed = Mathf.Clamp(value, 1f, 3f); }
        public void SetSwayAmount(float value) { _swayAmount = Mathf.Clamp(value, 0f, 5f); }
        public void SetBobSpeed(float value) { _bobSpeed = Mathf.Clamp(value, 0.5f, 2f); }
        public void SetBobAmount(float value) { _bobAmount = Mathf.Clamp(value, 0f, 0.05f); }
        public void SetWindInfluence(float value) { _windInfluence = Mathf.Clamp01(value); }
        public void SetCullDistance(float value) { _cullDistance = Mathf.Max(0f, value); }

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            _initialRotation = transform.localRotation;
            _initialPosition = transform.localPosition;
            _mainCamera = Camera.main;

            // 각 오브젝트마다 고유한 랜덤 오프셋 (Seed 기반)
            int seed = gameObject.GetInstanceID();
            var rng = new System.Random(seed);
            _swayOffset = (float)(rng.NextDouble() * Mathf.PI * 2f);
            _bobOffset = (float)(rng.NextDouble() * Mathf.PI * 2f);

            // 씬의 WindZone 캐시
            RefreshWindZone();
        }

        private void OnEnable()
        {
            // Awake 이후 WindZone이 동적으로 추가/변경된 경우 재탐색
            if (_windZone == null)
                RefreshWindZone();
        }

        private void RefreshWindZone()
        {
            _windZone = FindObjectOfType<WindZone>();
        }

        private void Update()
        {
            // 메인 카메라 캐싱 (씬 전환 등으로 null 시 재탐색)
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                    return; // 카메라 없으면 Update 스킵
            }

            // 거리 기반 컬링 (sqrMagnitude로 sqrt 회피)
            Vector3 posDelta = transform.position - _mainCamera.transform.position;
            float sqrDist = posDelta.sqrMagnitude;
            float sqrCull = _cullDistance * _cullDistance;
            if (sqrDist > sqrCull)
            {
                if (!_isCulled)
                {
                    _isCulled = true;
                    ResetState(); // 초기 위치로 복귀 (움직임 중단)
                }
                return;
            }

            // 카메라가 다시 가까워지면 _isCulled 해제 (아래에서 처리)
            _isCulled = false;

            // --- WindZone 영향 계산 ---
            float windStrength = 1f;
            Vector3 windDirection = Vector3.forward;

            if (_windZone != null)
            {
                windStrength = Mathf.Clamp01(_windZone.windMain);
                // WindZone의 방향을 로컬 Z축 기준으로 사용
                windDirection = _windZone.transform.forward;
            }

            // --- Sway (회전 진동) ---
            float swayTime = Time.time * _swaySpeed + _swayOffset;
            float swayAngle = Mathf.Sin(swayTime) * _swayAmount * windStrength;

            // WindZone 방향으로 회전 축 결정
            Vector3 swayAxis;
            if (windDirection.sqrMagnitude > 0.01f)
            {
                // 바람 방향에 수직인 축으로 흔들림 (바람 방향 × Up)
                Vector3 windDir = windDirection.normalized;
                swayAxis = Vector3.Cross(windDir, Vector3.up).normalized;
                if (swayAxis.sqrMagnitude < 0.01f)
                    swayAxis = Vector3.forward;
            }
            else
            {
                // 기본: Z축 기준 회전
                swayAxis = Vector3.forward;
            }

            Quaternion swayRotation = Quaternion.AngleAxis(swayAngle * _windInfluence, swayAxis);
            transform.localRotation = _initialRotation * swayRotation;

            // --- Bobbing (위치 상하 진동) ---
            float bobTime = Time.time * _bobSpeed + _bobOffset;
            float bobOffset = Mathf.Sin(bobTime) * _bobAmount * windStrength;
            transform.localPosition = _initialPosition + new Vector3(0f, bobOffset, 0f);
        }

        /// <summary>
        /// 초기 상태로 리셋 (Transform만 초기화).
        /// <c>_isCulled</c>는 변경하지 않음 —<see cref="Update"/>의 컬링 로직이 관리.
        /// </summary>
        public void ResetState()
        {
            transform.localRotation = _initialRotation;
            transform.localPosition = _initialPosition;
        }
    }
}