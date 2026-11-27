using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 74-G2] 벌목 완료 쓰러짐 시퀀스 — 통나무가 플레이어 반대 방향으로 절차 쓰러짐(0.9s 가속 회전).
    /// 물리엔진 미사용(FxDeathRelax 선례 — 순수 절차 회전). 쓰러짐 완료 시 지면 충격 먼지 1회 →
    /// 완료 콜백(기존 숨김·그루터기·리스폰 경로 그대로) → 자가 파괴.
    /// visual 부재(파괴/비목재) 시 기존 경로 즉시 완료(안전 폴백 — 기존 동작 보존).
    /// </summary>
    public class FellTreeSequence : MonoBehaviour
    {
        private const float FellDurationSec = 0.9f;   // 쓰러짐 소요(계획서 — 0.9s·가속 이징)
        private const float FellAngleDeg = 90f;       // 완전 쓰러짐 각도

        private Transform _visual;
        private Quaternion _baseRotation;   // 기본 회전 — 시작 시 캡처, 완료 시 원복(리스폰 직립 보존)
        private Vector3 _axis;              // 쓰러짐 회전축 = Cross(up, dir)
        private Vector3 _dir;               // 쓰러짐 방향(수평) — 착지 먼지 지점 산출용
        private float _elapsed;
        private bool _done;
        private Action _onComplete;

        /// <summary>쓰러짐 시퀀스 개시 — visual 부재(파괴/비목재) 시 기존 경로 즉시 완료(안전 폴백).</summary>
        public static void Trigger(Transform visual, Vector3 playerPos, Action onComplete)
        {
            if (visual == null)
            {
                onComplete?.Invoke();   // 기존 동작 보존 — 즉시 숨김 경로
                return;
            }

            // 쓰러짐 방향 = (나무 - 플레이어) 수평 정규화 — 플레이어 반대쪽으로 쓰러진다.
            Vector3 dir = visual.position - playerPos;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                dir = visual.forward;   // 퇴화 방어 — 나무 정면 방향(결정론)
                dir.y = 0f;
            }
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            Vector3 axis = Vector3.Cross(Vector3.up, dir);   // 이 축 양회전 시 나무 상단이 dir 쪽으로 넘어간다

            var driver = new GameObject("[무번호 74-G2_FellTreeSequence]").AddComponent<FellTreeSequence>();
            driver._visual = visual;
            driver._baseRotation = visual.rotation;   // 기본 회전 시작 시 캡처
            driver._axis = axis;
            driver._dir = dir;
            driver._onComplete = onComplete;
        }

        private void Update()
        {
            if (_done) return;
            if (_visual == null)   // 쓰러짐 중 시각 파괴 방어 — 먼지 지점 산출 불가, 즉시 완료
            {
                Finish(skipPuff: true);
                return;
            }

            _elapsed += Time.deltaTime;             // FxDeathRelax 선례 — 스케일 시간(히트스톱 연동)
            float t = Mathf.Clamp01(_elapsed / FellDurationSec);
            float angle = FellAngleDeg * (t * t);   // 가속 이징 — 쓰러짐은 점점 빨라진다
            _visual.rotation = Quaternion.AngleAxis(angle, _axis) * _baseRotation;

            if (t >= 1f) Finish(skipPuff: false);
        }

        /// <summary>쓰러짐 완료 — 지면 충격 먼지 1회 → 완료 콜백(기존 숨김 경로) → 자가 파괴.</summary>
        private void Finish(bool skipPuff)
        {
            if (_done) return;
            _done = true;

            if (_visual != null)
            {
                _visual.rotation = _baseRotation;   // 원복 — 숨김 후 리스폰 시 직립 보존(기존 경로 등가)
                if (!skipPuff)
                {
                    // 착지 지점 = 밑동 + 쓰러짐 방향 × 수목 도달 거리(렌더러 바운즈 높이 — 결정론).
                    float reach = 3f;
                    Renderer[] rends = _visual.GetComponentsInChildren<Renderer>();
                    if (rends != null && rends.Length > 0)
                    {
                        Bounds b = rends[0].bounds;
                        foreach (var r in rends) b.Encapsulate(r.bounds);
                        reach = Mathf.Clamp(b.size.y, 1.5f, 12f);
                    }
                    Vector3 basePos = _visual.position;
                    Vector3 groundPos = new Vector3(
                        basePos.x + _dir.x * reach, basePos.y, basePos.z + _dir.z * reach);
                    ShockwaveRingFX.SpawnMoveTrailPuff(groundPos);
                }
            }

            _onComplete?.Invoke();
            Destroy(gameObject);   // 자가 파괴 — 드라이버 누출 금지
        }
    }
}
