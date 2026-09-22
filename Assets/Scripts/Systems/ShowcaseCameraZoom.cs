// Test_11_AnimationShowcase (2026-09-22): 쇼케이스 카메라 휠 줌.
// 요구: "테스트 씬에서도 뷰를 확대할 수 있게" — 기존 고정 탑다운 카메라(0,45,-30 / 60°)의 자세를
// 유지한 채 마우스 휠로 전진/후퇴(돌리 줌)한다.
//  - 줌 중심(pivot) = 카메라 초기 시선이 닿는 지면 지점(Raycast, 실패 시 52m 전방) — 자세/프레이밍 보존.
//  - 거리 클램프 [min, max], 지수 평홴으로 부드럽게. 휠 위(+노치)=확대(거리 감소).
//  - Input System 전용 프로젝트(activeInputHandler=1) — Mouse.current.scroll 사용(노치 정규화 /120).
// TopDownCameraController는 Player 태그 필수라 Player 없는 쇼케이스 씬에서 미작동 → 전용 경량 컴포넌트.
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    [RequireComponent(typeof(Camera))]
    public class ShowcaseCameraZoom : MonoBehaviour
    {
        [Header("Distance Clamp")]
        [SerializeField] private float _minDistance = 6f;
        [SerializeField] private float _maxDistance = 65f;

        [Header("Zoom Feel")]
        [SerializeField] private float _zoomStepPerNotch = 4f; // 휠 노치당 거리 변화
        [SerializeField] private float _smoothTime = 0.12f;    // 지수 평활 시정수

        private Vector3 _pivot;
        private Vector3 _forward;
        private float _distance;
        private float _targetDistance;

        private void Start()
        {
            _forward = transform.forward.normalized;

            // 줌 중심 = 초기 시선이 닿는 지면 지점(지형/평면 콜라이더). 실패 시 52m 전방 폴백.
            _pivot = transform.position + _forward * 52f;
            if (Physics.Raycast(transform.position, _forward, out RaycastHit hit, 200f))
                _pivot = hit.point;

            _distance = Vector3.Distance(transform.position, _pivot);
            _targetDistance = _distance;
            Debug.Log($"[ShowcaseCameraZoom] 휠 줌 활성 — pivot={_pivot:F1}, dist={_distance:F1}m (범위 {_minDistance}~{_maxDistance}m, 휠=확대/축소)");
        }

        private void Update()
        {
            // 휠 입력 — Input System(노치 정규화: Windows 기준 한 노치 = 120)
            if (Mouse.current != null)
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    float notches = scrollY / 120f;
                    _targetDistance = Mathf.Clamp(
                        _targetDistance - notches * _zoomStepPerNotch,
                        _minDistance, _maxDistance);
                }
            }

            // 지수 평활 + 고정 자세 돌리 이동 — 카메라 자세(60° 톱다운)는 유지
            float k = 1f - Mathf.Exp(-(_smoothTime <= 0.0001f ? 0.0001f : _smoothTime) * 20f * Time.deltaTime);
            _distance = Mathf.Lerp(_distance, _targetDistance, k);

            transform.position = _pivot - _forward * _distance;
            transform.rotation = Quaternion.LookRotation(_forward, Vector3.up);
        }
    }
}
