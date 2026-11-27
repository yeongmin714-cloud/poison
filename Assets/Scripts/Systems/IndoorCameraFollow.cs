using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>실내 카메라 추적 — 메인 씬 카메라와 동일 시점(피치 57°/거리 11m, 플레이어 3/4뷰)
    /// + [51차 사용자 요구] 휠 줌 — 메인 씬 CameraZoomControllerRuntime와 동일 체감(휠 → 거리 조절,
    ///   클램프 + Lerp 스무딩, speed 5/smoothing 10 동일 수치). 실내 규모에 맞춘 클램프 6~20m.</summary>
    public class IndoorCameraFollow : MonoBehaviour
    {
        private Transform _player;
        // [실내 각도 v2] 65→57 — 9/20 실내 예시 사진(피치 50~60° 쿼터뷰, 벽 노출 15~20%)과 정렬.
        //   기존 65°는 체감상 더 수직(벽 노출 5~8%) — 살짝만 눕혀 오브젝트 높이/벽면을 읽히게 한다.
        private const float Pitch = 57f;
        private const float DefaultDist = 11f;

        [Header("Zoom Settings — 메인 CameraZoomControllerRuntime 동일 체감")]
        [SerializeField] private float minDistance = 6f;     // 실내 규모(방 내부) — 최대 확대
        [SerializeField] private float maxDistance = 20f;    // 실내 전체 조망 — 최대 축소
        [SerializeField] private float zoomSpeed = 5f;       // 메인과 동일
        [SerializeField] private float zoomSmoothing = 10f;  // 메인과 동일

        private float _targetDistance = DefaultDist;
        private float _currentDistance = DefaultDist;

        [Header("Orthographic Zoom (Test_16 전용 opt-in)")]
        [Tooltip("true면 휠 줌이 거리 대신 Camera.orthographicSize를 조절한다(직교 카메라 프레이밍 줌). 기본 false — 기존 호출부 동작 불변.")]
        [SerializeField] private bool zoomOrthographicSize = false;
        [SerializeField] private float minOrthographicSize = 6f;   // 방 내부 최대 확대
        [SerializeField] private float maxOrthographicSize = 16f;  // [125차 후속17] 줌아웃 한계 24→16 — 사용자 지시("캐릭터가 너무 작게 보이지 않도록") — 공통 계약 orthoSize 14의 약간 상급까지만 허용

        private Camera _camera;
        private float _targetOrthoSize;
        private float _currentOrthoSize;
        private bool _orthoInitialized;

        /// <summary>직교 줌 모드 on/off. Test_16처럼 카메라가 직교일 때 휠이 실제 프레이밍을 바꾸도록 설정한다.</summary>
        public bool ZoomOrthographicSize
        {
            get => zoomOrthographicSize;
            set => zoomOrthographicSize = value;
        }

        [Header("Pitch Override (Test_23 실내 관람 전용 opt-in)")]
        [Tooltip("0 이하 = 기본 57° 경로(기본 -1 — 기존 호출부 동작 불변). 0 초과 = 그 각도로 고정(90 = 완전 탑다운).")]
        [SerializeField] private float pitchOverrideDeg = -1f;

        /// <summary>피치 오버라이드(도). 기본 -1 = 기본 57° 경로 완전 보존. 90 = 완전 탑다운(수직 특이점 회피 경로).</summary>
        public float PitchOverrideDeg
        {
            get => pitchOverrideDeg;
            set => pitchOverrideDeg = value;
        }

        private void LateUpdate()
        {
            // [125차 후속17] 외부 카메라 소유 세션 게이트 — 대화(DialogueCameraDirector)/투기장 관전 등이
            //   CameraFollowSuppressed=true로 카메라를 소유한 동안은 추적 덮어쓰기 금지.
            //   근거: 프로젝트 계약 "조작시 CameraFollowSuppressed 게이트 필수"(카메라 구동자 2종)의 미이행 해소 —
            //   게이트 없이는 투기장 관전 카메라(후속13/16 세팅 pos=(0,26,0))가 다음 프레임에 플레이어 뷰로
            //   되돌아갔다(아레나 테스트 5 실측 — 확정 로그 직후 뷰가 관전석으로 복귀 = "중앙이 아님"의 뿌리).
            if (PlayerMovement.CameraFollowSuppressed)
            {
                // [125차 후속20] 관전 세션 중 휠 줌아웃 허용(사용자 지시 — "탑다운으로 전체 아레나 지형+두 병사 보기").
                //   후속17 게이트 본래 목적(위치/회전 덮어쓰기 금지 = 세션 소유 존중)은 유지하고,
                //   직교 프레이밍(orthoSize)만 사용자 조절 허용: 휠 위 = 확대, 아래 = 축소.
                //   한계는 세션별 — 관전(듀엔) 중엔 외곽 타원 전체까지(전환부 초기값 8에서 줌아웃 → 타원 수용),
                //   일반 실내 추적캠(비세션)의 계약 한계 16은 기존 분기에서 그대로 유지.
                if (zoomOrthographicSize)
                {
                    SyncOrthoFromCamera();   // 전환부(IndoorSceneTransition)가 세팅한 orthoSize 재동기화 — 스테일 타깃 방지
                    UpdateOrthographicZoom(Input.mouseScrollDelta.y, DuelMaxOrthoSize());
                }
                return;
            }

            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return;
                _player = p.transform;
            }

            // [51차] 휠 줌 — 스크롤 위 = 확대(거리 감소). 메인과 동일 입력 부호/속도/스무딩.
            // 단일 휠 리스너: 직교 줌 모드에서는 orthographicSize만 조절하고 거리 줌은 건너뛴다.
            float scroll = Input.mouseScrollDelta.y;
            if (zoomOrthographicSize)
            {
                UpdateOrthographicZoom(scroll);
            }
            else if (Mathf.Abs(scroll) > 0.01f)
            {
                _targetDistance -= scroll * zoomSpeed;
                _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
            }
            _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Time.deltaTime * zoomSmoothing);

            // [실내 관람] 피치 결정 — 오버라이드(0 초과) 우선, 기본 57° 경로 보존(IndoorTourMode.ResolvePitch와
            //   동일 논리 인라인 — Systems→UI 역참조 금지 계약으로 직접 호출 불가). 기본(-1)은 산식 불변.
            float pitch = pitchOverrideDeg > 0f ? pitchOverrideDeg : Pitch;
            Vector3 pos;
            if (pitch >= 89.5f)
            {
                // 완전 탑다운 — 플레이어 바로 위 + 수직 하강. LookRotation의 수직 특이점 회피:
                //   오일러 90° 직접 대입(+Z가 화면 위 — 바닥 평면이 화면 상하가 뒤집히지 않는 관람 방향).
                pos = _player.position + new Vector3(0f, 1.4f + _currentDistance, 0f);
                transform.position = pos;
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                // 기존 3/4뷰 공식 — pitch는 오버라이드가 없으면 Pitch(57)와 동일해 기존 산식 그대로(무수정).
                pos = _player.position + new Vector3(0f, 1.4f, 0f)
                      + Quaternion.Euler(pitch, 0f, 0f) * new Vector3(0f, 0f, -_currentDistance);
                transform.position = pos;
                transform.rotation = Quaternion.LookRotation(_player.position + new Vector3(0f, 1.2f, 0f) - pos);
            }
        }

        // 직교 카메라 프레이밍 줌: 휠 위 = 확대(orthographicSize 감소). 거리 줌과 동일 속도/스무딩 수치 사용.
        //   [125차 후속20] maxOverride > 0면 세션별 한계로 교체(관전 세션 = 전체 타원 수용값).
        private void UpdateOrthographicZoom(float scroll, float maxOverride = -1f)
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return;

            if (!_orthoInitialized)
            {
                _currentOrthoSize = _camera.orthographicSize;
                _targetOrthoSize = _currentOrthoSize;
                _orthoInitialized = true;
            }

            if (Mathf.Abs(scroll) > 0.01f)
            {
                _targetOrthoSize -= scroll * zoomSpeed;
                float maxLimit = maxOverride > 0f ? maxOverride : maxOrthographicSize;
                _targetOrthoSize = Mathf.Clamp(_targetOrthoSize, minOrthographicSize, maxLimit);
            }
            _currentOrthoSize = Mathf.Lerp(_currentOrthoSize, _targetOrthoSize, Time.deltaTime * zoomSmoothing);
            if (_camera.orthographic)
                _camera.orthographicSize = _currentOrthoSize;
        }

        /// <summary>[125차 후속20] 전환부가 세팅한 orthoSize와 내부 타깃이 어긋나면 재동기화 —
        ///   IndoorSceneTransition이 매 진입에 orthoSize를 강제 세팅(로비 14/투기장 8)하므로,
        ///   스테일한 _currentOrthoSize로 줌을 시작하면 진입값 무시하고 옛값으로 튀는 버그 원인.</summary>
        private void SyncOrthoFromCamera()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null || !_camera.orthographic) return;
            if (Mathf.Abs(_currentOrthoSize - _camera.orthographicSize) > 0.05f)
            {
                _currentOrthoSize = _camera.orthographicSize;
                _targetOrthoSize = _currentOrthoSize;
            }
        }

        /// <summary>[125차 후속20] 관전 세션(듀엔) 직교 줌 한계 — 외곽 타원 전체 조망.
        ///   ArenaPitInteriorBuilder.PIT_OUTER_B(23.9) + 여유 2.1 = 26: 세로 52m 뷰로 외곽 타원 47.8m 전체 수용.
        ///   일반 실내 추적캠의 16 한계 계약([125차 후속17] 사용자 지시)은 이 값의 영향을 받지 않는다.</summary>
        private static float DuelMaxOrthoSize() => ArenaPitInteriorBuilder.PIT_OUTER_B + 4.1f;   // [125차 후속25 R4] +2.1→+4.1(=28) — 전체 타원(47.8m)+벽 외곽 여유까지 줌아웃(사용자: 전체 아레나 형태 확인)
    }
}
