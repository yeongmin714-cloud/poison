using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 타격 반응 컴포넌트 — IDamageable을 구현한 MonoBehaviour에 붙어서 사용.
    /// 넉백(AddForce, Rigidbody 필요)과 경직(0.2초 대기)을 처리.
    /// Animation Riging은 생략하고 단순 Transform/Rigidbody 기반 반응으로 대체.
    /// Rigidbody가 없으면 AddForce는 건너뛰고 경직 + VFX만 처리.
    /// </summary>
    public class HitReaction : MonoBehaviour
    {
        [Header("Hit Reaction Settings")]
        [SerializeField] private float _knockbackForce = 5f;
        [SerializeField] private float _stunDuration = 0.2f;

        [Header("Optional References (auto-found if null)")]
        [SerializeField] private Renderer _targetRenderer;
        [SerializeField] private Rigidbody _rigidbody;

        // 경직 상태
        private bool _isStunned = false;

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
        /// </summary>
        /// <param name="hitDirection">타격 방향 (플레이어 → 몬스터)</param>
        /// <param name="force">넉백 힘 배율</param>
        public void PlayHitReaction(Vector3 hitDirection, float force = 1f)
        {
            if (_isStunned) return;

            // 1. HitFlash
            if (_targetRenderer != null)
                HitVFX.PlayHitFlash(_targetRenderer);

            // 2. 넉백: hitDirection 반대 방향으로 AddForce
            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                Vector3 knockbackDir = -hitDirection.normalized;
                knockbackDir.y = 0.5f; // 약간 위로
                _rigidbody.AddForce(knockbackDir * _knockbackForce * force, ForceMode.Impulse);
            }

            // 3. 경직 (0.2초)
            if (!_isStunned)
                StartCoroutine(StunCoroutine(_stunDuration));
        }

        /// <summary>
        /// 경직 코루틴 — 지정된 시간 동안 움직임/행동 차단
        /// </summary>
        private System.Collections.IEnumerator StunCoroutine(float duration)
        {
            _isStunned = true;
            yield return new WaitForSeconds(duration);
            _isStunned = false;
        }
    }
}