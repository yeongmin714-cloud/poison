using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-down 카메라 컨트롤러 v4
/// 
/// - 플레이어가 항상 화면 중앙에 고정
/// - 카메라가 플레이어 주위를 궤도 회전 (Orbit)
/// - 커서 위치로 궤도 각도 제어 (좌우=요, 상하=피치)
/// - 우클릭 드래그: 수동 회전 (보조)
/// - 휠: 궤도 반경(줌) 조절
/// </summary>
namespace ProjectName.Systems
{
    public class TopDownCameraController : MonoBehaviour
    {
        [Header("Orbit")]
        [SerializeField, Range(10f, 60f)] private float _orbitRadius = 30f;
        [SerializeField, Range(5f, 30f)] private float _minRadius = 10f;
        [SerializeField, Range(20f, 80f)] private float _maxRadius = 50f;
        [SerializeField, Range(10f, 80f)] private float _defaultPitch = 50f;
        [SerializeField, Range(10f, 80f)] private float _minPitch = 20f;
        [SerializeField, Range(20f, 80f)] private float _maxPitch = 70f;

        [Header("Cursor Look")]
        [SerializeField] private float _cursorSensitivity = 0.5f;     // 커서→회전 계수
        [SerializeField, Range(1f, 50f)] private float _deadZonePixels = 15f;

        [Header("Smoothing")]
        [SerializeField] private float _orbitSmoothTime = 0.15f;

        [Header("Obstacle Avoidance")]
        [Tooltip("레이어마스크: 장애물이 있는 레이어를 지정하세요. 플레이어 레이어는 제외하세요.")]
        [SerializeField] private LayerMask _obstacleLayers = ~0; // 레이어마스크 (장애물 레이어 권장, 플레이어 레이어 제외)
        [SerializeField, Range(0.1f, 2f)] private float _obstacleOffset = 0.5f; // 충돌 시 물러날 거리

        private Transform _player;
        private Camera _cam;

        // 궤도 파라미터
        private float _targetYaw = 0f;
        private float _currentYaw = 0f;
        private float _targetPitch;
        private float _currentPitch;
        private float _targetRadius;
        private float _currentRadius;

        private float _velocityYaw = 0f;
        private float _velocityPitch = 0f;
        private float _velocityRadius = 0f;
        private Vector2 _screenSize;

        void Start()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) { enabled = false; return; }
            _cam.orthographic = false;

            _targetPitch = _defaultPitch;
            _currentPitch = _targetPitch;
            _targetRadius = _orbitRadius;
            _currentRadius = _targetRadius;
            _screenSize = new Vector2(Screen.width, Screen.height);

            // Auto-exclude player layer from obstacle mask to prevent self-hitting
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                // Remove player's layer from obstacle mask
                _obstacleLayers &= ~(1 << playerGo.layer);
            }
        }

        void LateUpdate()
        {
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _player = go.transform;
                else return;
            }

            _screenSize = new Vector2(Screen.width, Screen.height);

            // ===== 입력 처리 =====
            HandleCursor();
            HandleRightDrag();
            HandleZoom();

            // ===== 부드러운 보간 =====
            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _velocityYaw, _orbitSmoothTime);
            _currentPitch = Mathf.SmoothDampAngle(_currentPitch, _targetPitch, ref _velocityPitch, _orbitSmoothTime);
            _currentRadius = Mathf.SmoothDamp(_currentRadius, _targetRadius, ref _velocityRadius, _orbitSmoothTime);

            // ===== 카메라 위치 = 플레이어 기준 궤도 (항상 위에 위치) =====
            float height = _currentRadius * Mathf.Sin(_currentPitch * Mathf.Deg2Rad);
            float distance = _currentRadius * Mathf.Cos(_currentPitch * Mathf.Deg2Rad);

            Vector3 orbitOffset = Quaternion.Euler(0, _currentYaw, 0) * new Vector3(0, 0, -distance);
            orbitOffset.y = height;

            Vector3 desiredPos = _player.position + orbitOffset;
            HandleObstacleAvoidance(ref desiredPos);
            transform.position = desiredPos;
            transform.LookAt(_player.position, Vector3.up);
        }

        void HandleObstacleAvoidance(ref Vector3 desiredPos)
        {
            if (_player == null) return;
            Vector3 direction = desiredPos - _player.position;
            float distance = direction.magnitude;
            if (Physics.Raycast(_player.position, direction.normalized, out RaycastHit hit, distance, _obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                desiredPos = hit.point + hit.normal * _obstacleOffset;
            }
        }

        void HandleCursor()
        {
            if (Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector2 center = _screenSize * 0.5f;
            Vector2 offset = mousePos - center;

            if (offset.magnitude < _deadZonePixels) return;

            // 정규화 (-1 ~ 1)
            float nx = offset.x / (_screenSize.x * 0.5f);
            float ny = offset.y / (_screenSize.y * 0.5f);

            // Yaw: ±180°, Pitch: 위로 올리면 먼 곳, 아래로 내리면 가까운 곳
            _targetYaw = Mathf.Clamp(nx * 180f * _cursorSensitivity, -180f, 180f);
            _targetPitch = Mathf.Clamp(
                _defaultPitch - ny * 20f * _cursorSensitivity,
                _minPitch,
                _maxPitch
            );
        }

        void HandleRightDrag()
        {
            if (Mouse.current == null || !Mouse.current.rightButton.isPressed) return;

            Vector2 delta = Mouse.current.delta.ReadValue();
            _targetYaw += delta.x * 0.2f;
            _targetPitch = Mathf.Clamp(
                _targetPitch - delta.y * 0.15f,
                _minPitch,
                _maxPitch
            );
        }

        void HandleZoom()
        {
            if (Mouse.current?.scroll == null) return;
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _targetRadius = Mathf.Clamp(
                    _targetRadius - scroll * 0.5f,
                    _minRadius,
                    _maxRadius
                );
            }
        }
    }
}