using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 타격 반응 컴포넌트 — IDamageable을 구현한 MonoBehaviour에 붙어서 사용.
    /// 넉백(AddForce, Rigidbody 필요)과 경직(0.2초 대기)을 처리.
    /// Animation Riging은 생략하고 단순 Transform/Rigidbody 기반 반응으로 대체.
    /// Rigidbody가 없거나 isKinematic이어도 Transform 절트(KinematicJolt)로 항상 넉백 반응을 연출.
    ///
    /// 경직 중 추가 타격이 들어오면:
    ///   - HitFlash + 넉백은 항상 실행 (시각적 피드백 유지)
    ///   - 경직 코루틴은 중단 후 재시작 (지속 시간 갱신)
    /// </summary>
    public class HitReaction : MonoBehaviour
    {
        [Header("Hit Reaction Settings")]
        [SerializeField] private float _knockbackForce = 5f;
        [SerializeField] private float _stunDuration = 0.2f;
        [SerializeField] private float _joltDistance = 0.25f; // 키네마틱 절트 밀림 거리 (HighSpec에서 2배)

        [Header("Optional References (auto-found if null)")]
        [SerializeField] private Renderer _targetRenderer;
        [SerializeField] private Rigidbody _rigidbody;

        // 경직 상태 및 실행 중인 코루틴 참조
        private bool _isStunned;
        private Coroutine _stunCoroutine;
        private Coroutine _joltCoroutine;
        private Vector3 _joltOffset; // 현재 적용 중인 절트 변위량 (중첩 타격 시 위치 누적 방지)

        /// <summary>현재 경직 중인가?</summary>
        public bool IsStunned => _isStunned;

        private void Awake()
        {
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();
            if (_targetRenderer == null)
                _targetRenderer = GetComponent<Renderer>();
        }

        /// <summary>
        /// 타격 반응 실행: 넉백(AddForce) + 경직 + VFX
        /// 경직 중에도 HitFlash와 넉백은 항상 실행하며,
        /// 경직 지속 시간은 갱신(refresh)된다.
        /// </summary>
        /// <param name="hitDirection">타격 방향 (플레이어 → 몬스터)</param>
        /// <param name="force">넉백 힘 배율</param>
        public void PlayHitReaction(Vector3 hitDirection, float force = 1f)
        {
            // 1. HitFlash — 경직 중에도 항상 실행 (시각적 피드백 필수)
            if (_targetRenderer != null)
                HitVFX.PlayHitFlash(_targetRenderer);

            // 2. 넉백: hitDirection 반대 방향으로 AddForce
            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                Vector3 knockbackDir = -hitDirection.normalized;
                knockbackDir.y = 0.5f; // 약간 위로
                _rigidbody.AddForce(knockbackDir * _knockbackForce * force, ForceMode.Impulse);
            }

            // 2-b. 키네마틱 절트 — Rigidbody 상태와 무관하게 항상 실행.
            //      몬스터/병사/영주는 isKinematic=true(절차 애니메이션)라 AddForce가 무시되므로,
            //      Transform을 짧게 밀었다 되돌리는 플린치로 타격감을 보장한다.
            Vector3 joltDir = -hitDirection.normalized;
            float joltDistance = _joltDistance * force;
            if (ActionFeel.HighSpec)
            {
                joltDistance *= 2f; // HighSpec: 절트 거리 2배
                joltDir.y += 0.4f;  // HighSpec: 살짝 위로 튀어오르는 홉
            }

            if (_joltCoroutine != null)
                StopCoroutine(_joltCoroutine);
            _joltCoroutine = StartCoroutine(KinematicJolt(joltDir, joltDistance));

            // 3. 경직 — 실행 중이면 재시작 (지속 시간 갱신)
            if (_stunCoroutine != null)
                StopCoroutine(_stunCoroutine);

            _stunCoroutine = StartCoroutine(StunCoroutine(_stunDuration));
        }

        /// <summary>
        /// 경직 코루틴 — 지정된 시간 동안 움직임/행동 차단
        /// </summary>
        private System.Collections.IEnumerator StunCoroutine(float duration)
        {
            _isStunned = true;
            yield return new WaitForSeconds(duration);
            _isStunned = false;
            _stunCoroutine = null;
        }

        /// <summary>
        /// 키네마틱 절트 코루틴 — transform.position을 dir 방향으로 짧게 밀었다가 복귀.
        /// 0.06초 동안 빠르게 밀리고(ease-out), 0.12초 동안 부드럽게 제자리 복귀(smoothstep).
        /// y는 +0.15 바이어스로 살짝 뜨는 느낌. 경직(Stun) 코루틴과 별개로 항상 실행된다.
        /// </summary>
        private System.Collections.IEnumerator KinematicJolt(Vector3 dir, float distance)
        {
            // 이전 절트가 남긴 변위량을 먼저 제거해 위치가 누적되지 않게 한다.
            transform.position -= _joltOffset;

            Vector3 offset = dir.normalized * distance;
            offset.y += 0.15f; // 위로 살짝 뜨는 바이어스
            _joltOffset = offset;

            Vector3 basePos = transform.position;
            Vector3 pushPos = basePos + offset;

            // 1) 빠른 밀림 (0.06초, ease-out)
            const float pushTime = 0.06f;
            float t = 0f;
            while (t < pushTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / pushTime);
                transform.position = Vector3.Lerp(basePos, pushPos, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            transform.position = pushPos;

            // 2) 부드러운 복귀 (0.12초, smoothstep)
            const float settleTime = 0.12f;
            t = 0f;
            while (t < settleTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / settleTime);
                transform.position = Vector3.Lerp(pushPos, basePos, k * k * (3f - 2f * k));
                yield return null;
            }

            transform.position = basePos;
            _joltOffset = Vector3.zero;
            _joltCoroutine = null;
        }
    }
}