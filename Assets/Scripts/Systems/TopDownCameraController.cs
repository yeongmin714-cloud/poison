using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    public class TopDownCameraController : MonoBehaviour
    {
        [Header("Orbit")]
        [SerializeField, Range(5f, 30f)] private float _orbitRadius = 15f;
        [SerializeField, Range(3f, 20f)] private float _minRadius = 5f;
        [SerializeField, Range(10f, 50f)] private float _maxRadius = 25f;
        [SerializeField, Range(20f, 70f)] private float _defaultPitch = 45f;
        [SerializeField, Range(10f, 80f)] private float _minPitch = 20f;
        [SerializeField, Range(20f, 80f)] private float _maxPitch = 70f;

        [Header("Cursor Look")]
        [SerializeField] private float _cursorSensitivity = 0.5f;
        [SerializeField, Range(1f, 50f)] private float _deadZonePixels = 15f;

        [Header("Right-Drag Rotation (Auxiliary)")]
        [SerializeField] private float _dragSensitivityYaw = 0.2f;
        [SerializeField] private float _dragSensitivityPitch = 0.15f;

        [Header("Zoom")]
        [SerializeField] private float _zoomSensitivity = 0.5f;

        [Header("Smoothing")]
        [SerializeField] private float _orbitSmoothTime = 0.15f;

        [Header("Obstacle Avoidance")]
        [Tooltip("레이어마스크: 장애물이 있는 레이어를 지정하세요. 플레이어 레이어는 제외하세요.")]
        [SerializeField] private LayerMask _obstacleLayers = ~0;
        [SerializeField, Range(0.1f, 2f)] private float _obstacleOffset = 0.5f;

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

        private const float MaxYawClamp = 180f;
        private const float CursorPitchRange = 20f;
        private const float ScrollDeadZone = 0.01f;

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

            // Cache player reference and auto-exclude player layer from obstacle mask
            CachePlayerAndExcludeLayer();
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
            // 우클릭 드래그가 활성화된 경우 커서 기반 회전을 건너뜁니다 (충돌 방지)
            bool isRightDragging = Mouse.current != null && Mouse.current.rightButton.isPressed;
            if (!isRightDragging)
                HandleCursor();
            HandleRightDrag();
            HandleZoom();

            // ===== 부드러운 보간 =====
            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _velocityYaw, _orbitSmoothTime);
            _currentPitch = Mathf.SmoothDampAngle(_currentPitch, _targetPitch, ref _velocityPitch, _orbitSmoothTime);
            _currentRadius = Mathf.SmoothDamp(_currentRadius, _targetRadius, ref _velocityRadius, _orbitSmoothTime);

            // ===== 카메라 위치 = 플레이어 기준 궤도 =====
            float height = _currentRadius * Mathf.Sin(_currentPitch * Mathf.Deg2Rad);
            float distance = _currentRadius * Mathf.Cos(_currentPitch * Mathf.Deg2Rad);

            Vector3 orbitOffset = Quaternion.Euler(0, _currentYaw, 0) * new Vector3(0, 0, -distance);
            orbitOffset.y = height;

            Vector3 desiredPos = _player.position + orbitOffset;
            HandleObstacleAvoidance(ref desiredPos);
            transform.position = desiredPos;
            transform.LookAt(_player.position, Vector3.up);
        }

        void OnValidate()
        {
            // 인스펙터에서 잘못된 값이 설정되지 않도록 보장
            _minRadius = Mathf.Min(_minRadius, _maxRadius);
            _minPitch = Mathf.Min(_minPitch, _maxPitch);
        }

        void CachePlayerAndExcludeLayer()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _player = playerGo.transform;
                // Remove player's layer from obstacle mask to prevent self-hitting
                _obstacleLayers &= ~(1 << playerGo.layer);
            }
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
            _targetYaw = Mathf.Clamp(nx * MaxYawClamp * _cursorSensitivity, -MaxYawClamp, MaxYawClamp);
            _targetPitch = Mathf.Clamp(
                _defaultPitch - ny * CursorPitchRange * _cursorSensitivity,
                _minPitch,
                _maxPitch
            );
        }

        void HandleRightDrag()
        {
            if (Mouse.current == null || !Mouse.current.rightButton.isPressed) return;

            Vector2 delta = Mouse.current.delta.ReadValue();
            _targetYaw += delta.x * _dragSensitivityYaw;
            _targetPitch = Mathf.Clamp(
                _targetPitch - delta.y * _dragSensitivityPitch,
                _minPitch,
                _maxPitch
            );
        }

        void HandleZoom()
        {
            if (Mouse.current?.scroll == null) return;
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > ScrollDeadZone)
            {
                _targetRadius = Mathf.Clamp(
                    _targetRadius - scroll * _zoomSensitivity,
                    _minRadius,
                    _maxRadius
                );
            }
        }
    }
}