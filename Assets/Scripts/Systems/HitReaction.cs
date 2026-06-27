using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 타격 반응 컴포넌트 — IDamageable을 구현한 MonoBehaviour에 붙어서 사용.
    /// 넉백(AddForce, Rigidbody 필요)과 경직(0.2초 대기)을 처리.
    /// Animation Riging은 생략하고 단순 Transform/Rigidbody 기반 반응으로 대체.
    /// Rigidbody가 없으면 AddForce는 건너뛰고 경직 + VFX만 처리.
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

        [Header("Optional References (auto-found if null)")]
        [SerializeField] private Renderer _targetRenderer;
        [SerializeField] private Rigidbody _rigidbody;

        // 경직 상태 및 실행 중인 코루틴 참조
        private bool _isStunned;
        private Coroutine _stunCoroutine;

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
    }
}