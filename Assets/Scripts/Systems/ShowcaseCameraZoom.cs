// Test_11_AnimationShowcase (2026-09-22 수리): 쇼케이스 카메라 줌 — 다중 입력 채널 무장화.
// 요구: "여전히 휠로 확대 안되니 해결하고 카메라를 캐릭터들 근처로".
// [수리] 휠 1경로(Mouse.current.scroll)만 있어 환경에 따라 무시될 수 있던 것을 4채널로 확장:
//  ①Input System 휠 ②레거시 Input.mouseScrollDelta(try/catch — activeInputHandler=2 Both 모드에서
//  유효, CameraZoomControllerRuntime 선례) ③PageUp(확대)/PageDown(축소) 키 ④우클릭 드래그 상하.
// 채널별 최초 수신 1회 로그로 입력 수신 경로를 콘솔에서 증명한다.
// 자세(60° 톱다운) 유지 돌리 줌 — 줌 중심=초기 시선 지면 지점(Raycast, 실패 시 40m 전방).
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    [RequireComponent(typeof(Camera))]
    public class ShowcaseCameraZoom : MonoBehaviour
    {
        [Header("Distance Clamp")]
        [SerializeField] private float _minDistance = 4f;
        [SerializeField] private float _maxDistance = 45f;

        [Header("Zoom Feel")]
        [SerializeField] private float _zoomStepPerNotch = 3f;   // 휠 노치당 거리 변화
        [SerializeField] private float _keyZoomSpeed = 12f;      // PageUp/Down 초당
        [SerializeField] private float _dragZoomSensitivity = 0.06f; // 우클릭 드래그 px당
        [SerializeField] private float _smoothTime = 0.1f;       // 지수 평활 시정수

        [Header("WASD Pan (2026-09-22)")]
        [SerializeField] private float _panSpeed = 8f;           // WASD 이동 속도(m/s)
        [SerializeField] private float _panSprint = 2f;          // Shift 배속

        private Vector3 _pivot;
        private Vector3 _forward;
        private float _distance;
        private float _targetDistance;
        private float _pivotY; // WASD 이동 시 고정되는 시점 높이(지면)

        // 입력 채널 진단 — 최초 수신 1회 로그(콘솔에서 어떤 경로로 들어오는지 증명)
        private bool _loggedIS, _loggedLegacy, _loggedKey, _loggedDrag;
        private bool _legacyBroken; // Input-System-only 모드 legacy 예외 1회 캐시(재시도 없음)

        private void Start()
        {
            _forward = transform.forward.normalized;

            // 줌 중심 = 초기 시선이 닿는 지면 지점(지형/평면 콜라이더). 실패 시 40m 전방 폴백.
            _pivot = transform.position + _forward * 40f;
            if (Physics.Raycast(transform.position, _forward, out RaycastHit hit, 300f))
                _pivot = hit.point;

            _distance = Vector3.Distance(transform.position, _pivot);
            _targetDistance = _distance;
            _pivotY = _pivot.y; // WASD 팬에서 y는 이 값으로 고정(지면 수준)
            Debug.Log($"[ShowcaseCameraZoom] 줌 활성 — pivot={_pivot:F1}, dist={_distance:F1}m (범위 {_minDistance}~{_maxDistance}m) | 입력: 휠 / PageUp·PageDown / 우클릭 드래그 상하 / WASD 이동(Shift 가속)");
        }

        private void Update()
        {
            // ① Input System 휠
            if (Mouse.current != null)
            {
                float sy = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(sy) > 0.01f)
                {
                    if (!_loggedIS) { _loggedIS = true; Debug.Log("[ShowcaseCameraZoom] 휠 입력 수신 (Input System)"); }
                    _targetDistance = Mathf.Clamp(
                        _targetDistance - (sy / 120f) * _zoomStepPerNotch, _minDistance, _maxDistance);
                }
            }

            // ② 레거시 휠 — Both 모드에서 유효(선례: CameraZoomControllerRuntime).
            //    Input-System-only 모드면 예외 1회 후 채널 폐기(무시).
            if (!_legacyBroken)
            {
                try
                {
                    float ly = Input.mouseScrollDelta.y;
                    if (Mathf.Abs(ly) > 0.01f)
                    {
                        if (!_loggedLegacy) { _loggedLegacy = true; Debug.Log("[ShowcaseCameraZoom] 휠 입력 수신 (Legacy Input)"); }
                        _targetDistance = Mathf.Clamp(
                            _targetDistance - ly * _zoomStepPerNotch, _minDistance, _maxDistance);
                    }
                }
                catch (System.Exception)
                {
                    _legacyBroken = true;
                }
            }

            // ③ 키보드 폴백 — PageUp=확대(거리 감소) / PageDown=축소. 휠 환경 무관 항상 동작.
            if (Keyboard.current != null)
            {
                float key = 0f;
                if (Keyboard.current.pageUpKey.isPressed) key = -1f;
                else if (Keyboard.current.pageDownKey.isPressed) key = +1f;
                if (key != 0f)
                {
                    if (!_loggedKey) { _loggedKey = true; Debug.Log("[ShowcaseCameraZoom] 키보드 줌 수신 (PageUp/Down)"); }
                    _targetDistance = Mathf.Clamp(
                        _targetDistance + key * _keyZoomSpeed * Time.deltaTime, _minDistance, _maxDistance);
                }
            }

            // ④ 우클릭 드래그 상하 — 드래그 위=확대(거리 감소). 마우스 휠 불가 환경 폴백.
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                float dy = Mouse.current.delta.ReadValue().y;
                if (Mathf.Abs(dy) > 0.5f)
                {
                    if (!_loggedDrag) { _loggedDrag = true; Debug.Log("[ShowcaseCameraZoom] 우클릭 드래그 줌 수신"); }
                    _targetDistance = Mathf.Clamp(
                        _targetDistance - dy * _dragZoomSensitivity, _minDistance, _maxDistance);
                }
            }

            // WASD 팬 — 카메라 기준 수평 이동(지면 투영), Shift 가속. [2026-09-22]
            if (Keyboard.current != null)
            {
                float x = 0f, z = 0f;
                if (Keyboard.current.wKey.isPressed) z += 1f;
                if (Keyboard.current.sKey.isPressed) z -= 1f;
                if (Keyboard.current.dKey.isPressed) x += 1f;
                if (Keyboard.current.aKey.isPressed) x -= 1f;

                if (x != 0f || z != 0f)
                {
                    float speed = _panSpeed * (Keyboard.current.leftShiftKey.isPressed ? _panSprint : 1f);
                    // 카메라 시선을 지면에 투영한 전/우 방향 — 각도 불변, 시점만 이동
                    Vector3 flatFwd = Vector3.ProjectOnPlane(_forward, Vector3.up).normalized;
                    Vector3 flatRight = Vector3.Cross(Vector3.up, flatFwd).normalized;
                    _pivot += (flatFwd * z + flatRight * x) * speed * Time.deltaTime;
                    _pivot.y = _pivotY; // 시점 높이 고정 — 지면 수준 유지
                }
            }

            // 지수 평활 + 자세 고정 돌리 이동 — 카메라 자세(60° 톱다운)는 유지
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, _smoothTime));
            _distance = Mathf.Lerp(_distance, _targetDistance, k);

            transform.position = _pivot - _forward * _distance;
            transform.rotation = Quaternion.LookRotation(_forward, Vector3.up);
        }
    }
}
