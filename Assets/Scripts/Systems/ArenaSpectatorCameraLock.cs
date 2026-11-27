// 2026-10-25 (125차 후속25 R3-3): 투기장 관전 카메라 소유 강제 락.
// 근거: pit_late 실측 — 빌드 2.5s 후 ArenaTestCamera가 (0,26,0)→(0,0,0)으로 떨어짐
// (y=0 = 모래 슬래브 내부 → 수직하강 시점은 역면 컬링으로 지형 전무 = void+실루엣만 = "회색/검정 화면").
// 계약: 투기장 세션 동안 매 프레임 말에 관전 프레이밍을 재어셜한다 —
// 이동 주체와 무관하게 시야를 보장. Update/LateUpdate 2시점 스냅숏으로 이동 주체의 실행 페이즈를 진단한다.
// [125차 후속31 M1] 프레이밍 수직(90°)→사선(62°) — 사용자 지시 "전투가 생동감 있게". 시선축은 격투 중심(0,1,0) 통과.
using UnityEngine;

namespace ProjectName.Systems
{
    public class ArenaSpectatorCameraLock : MonoBehaviour
    {
        /// <summary>[125차 후속31 M1] 관전 프레이밍 단일 소스 — 3 소비처(락 복원/피트 진입 IndoorSceneTransition/캡처 F5 RenderAuditProbe)가 공유.
        ///   수직 탑다운 폐기 → 사선 관전: 고도 62°, 격투 중심(0,1,0) 정준. 카메라 z=-8.9는 외곽 벽(z=±23.9) 안쪽이라 벽 가림 0(기하 검증 완료).
        ///   [125차 후속31 M1+] 사용자 재조정 "조금만 더 아래로" — 고도 62°→56°, 시선축 t=19 유지(격투 중심 통과):
        ///   pos=(0, 1+19·sin56°, -19·cos56°)=(0, 16.8, -10.6). z=-10.6도 벽 안쪽 — 가림 0 유지.</summary>
        public static readonly Vector3 SpectatorPos = new Vector3(0f, 16.8f, -10.6f);
        public const float SpectatorPitchDeg = 56f;
        public const float SpectatorOrthoSize = 10f;

        /// <summary>[125차 후속27 R-A] 관전 세션 활성 플래그 — 모든 카메라/이동 기록자가 이 게이트로 차단된다
        ///   (락의 프레임 말 복구는 2차 방어, 이 플래그 게이트가 1차 차단 = 근본 수리).</summary>
        public static bool Active;

        private Transform _cam;
        private Camera _camComp;
        private Vector3 _updateSnapshot;      // Update 시점(이동자들 실행 전) 위치
        private bool _hasUpdateSnapshot;
        private Vector3 _lastFrameEnd;        // 직전 프레임 말(강제 적용 후) 위치
        private bool _lastSuppressed;
        private int _lateMoverCount;
        private int _otherMoverCount;

        /// <summary>투기장 세션 시작 — 관전 카메라에 락 부착(DontDestroyOnLoad로 씬 전환 중 생존).</summary>
        public static ArenaSpectatorCameraLock Attach(Camera cam)
        {
            if (cam == null) return null;
            var go = new GameObject("ArenaSpectatorCameraLock");
            Object.DontDestroyOnLoad(go);
            var l = go.AddComponent<ArenaSpectatorCameraLock>();
            l._cam = cam.transform;
            l._camComp = cam;
            l._lastFrameEnd = cam.transform.position;
            l._lastSuppressed = PlayerMovement.CameraFollowSuppressed;
            Active = true;   // [125차 후속27 R-A] 세션 플래그 — 기록자 게이트 1차 차단 개시
            Debug.Log($"[ArenaSpectatorLock] 부착 — cam='{cam.name}' pos={cam.transform.position} / CameraFollowSuppressed={l._lastSuppressed} / Active=True");
            return l;
        }

        private void OnDestroy()
        {
            // [125차 후속27 R-A] 세션 종료 — 플래그 해제(로비/월드 복귀 시 기록자 재개)
            Active = false;
            Debug.Log("[ArenaSpectatorLock] 해제 — Active=False");
        }

        private void Update()
        {
            if (!EnsureCam()) return;
            _updateSnapshot = _cam.position;
            _hasUpdateSnapshot = true;
            bool sup = PlayerMovement.CameraFollowSuppressed;
            if (sup != _lastSuppressed)
            {
                Debug.LogWarning($"[ArenaSpectatorLock] CameraFollowSuppressed {_lastSuppressed}→{sup} — 해제자 추적 필요");
                _lastSuppressed = sup;
            }
        }

        private void LateUpdate()
        {
            if (!EnsureCam()) return;
            var before = _cam.position;

            // (A) Update 이후 LateUpdate 사이의 외부 이동 — LateUpdate 이동자 존재
            if (_hasUpdateSnapshot && (before - _updateSnapshot).sqrMagnitude > 0.0025f && _lateMoverCount < 5)
            {
                _lateMoverCount++;
                Debug.LogWarning($"[ArenaSpectatorLock] LateUpdate 이동자 감지 #{_lateMoverCount} — Update시 {_updateSnapshot:F1} → LateUpdate시 {before:F1} (프레임 말 강제 복구)");
            }
            // (B) Update 이전 이동(프레임 간) — Update/코루틴/외부 이동자
            else if ((before - _lastFrameEnd).sqrMagnitude > 0.0025f && _otherMoverCount < 5)
            {
                _otherMoverCount++;
                Debug.LogWarning($"[ArenaSpectatorLock] Update 페이즈 이동자 감지 #{_otherMoverCount} — 직전 프레임 말 {_lastFrameEnd:F1} → {before:F1} (프레임 말 강제 복구)");
            }

            // 소유 강제 — 프레임 말에 관전 프레이밍 복원(이동 주체와 무관한 시야 보장 계약).
            // [125차 후속25 R4] ortho 강제 제거 — 관전 휠 줌(후속20, min 6/듀엔 최대 28)이 사용자 조작으로
            //   동작해야 한다(락이 ortho 8을 강제하면 줌이 죽음 — 영상11 사용자 요구 "줌인 줌아웃").
            // [125차 후속31 M1] 수직 90°→사선 62° — 사용자 지시 "전투 생동감".
            _cam.position = SpectatorPos;
            _cam.rotation = Quaternion.Euler(SpectatorPitchDeg, 0f, 0f);
            _lastFrameEnd = _cam.position;
            _hasUpdateSnapshot = false;
        }

        private bool EnsureCam()
        {
            if (_cam != null && _camComp != null && _camComp.isActiveAndEnabled) return true;
            var main = Camera.main;
            if (main == null) return false;
            _cam = main.transform;
            _camComp = main;
            return true;
        }
    }
}
