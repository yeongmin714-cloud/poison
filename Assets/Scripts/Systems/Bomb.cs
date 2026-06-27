using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 폭탄 종류
    /// </summary>
    public enum BombType
    {
        Explosive,   // 폭발 데미지
        PoisonGas,   // 독 가스 영역 디버프
        Smoke,       // 연막 시야 가림
        Molotov      // 화염 영역 지속 데미지
    }

    /// <summary>
    /// 폭탄 기본 동작: 폭발 시 효과 발동 후 자기 파괴
    /// </summary>
    public class Bomb : MonoBehaviour
    {
        [Header("Bomb Settings")]
        public BombType bombType;
        public float explosionRadius = 3f;
        public float explosionDelay = 0.5f; // 퓨즈 시간
        public LayerMask targetLayers;

        [Header("Effects")]
        public GameObject explosionEffectPrefab;
        public AudioClip explosionSound;
        public float explosionForce = 500f; // 폭발력으로 주변 객체 밀치기

        private void Start()
        {
            // 퓨즈 시작
            Invoke(nameof(Explode), explosionDelay);
        }

        private void Explode()
        {
            // 사운드 재생
            if (explosionSound != null)
            {
                AudioSource.PlayClipAtPoint(explosionSound, transform.position);
            }

            // 효과 인스턴스화
            if (explosionEffectPrefab != null)
            {
                Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            }

            // 종류별 효과 적용
            switch (bombType)
            {
                case BombType.Explosive:
                    ApplyExplosiveDamage();
                    break;
                case BombType.PoisonGas:
                    ApplyPoisonGas();
                    break;
                case BombType.Smoke:
                    ApplySmoke();
                    break;
                case BombType.Molotov:
                    ApplyMolotov();
                    break;
            }

            // 폭탄 객체 제거
            Destroy(gameObject);
        }

        private void ApplyExplosiveDamage()
        {
            // 원형 범위 내의 충돌체 탐지
            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, targetLayers);
            foreach (var hit in hits)
            {
                // 데미지 적용 가능한 인터페이스 찾기
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    // 거리 기반 데미지 감쇠 (선택)
                    float distance = Vector3.Distance(transform.position, hit.transform.position);
                    float damage = 10f * (1f - distance / explosionRadius); // 예시 데미지 10 at center
                    Vector3 hitDir = (hit.transform.position - transform.position).normalized;
                    damageable.TakeDamage(damage, hitDir, "Explosion");
                }

                // 폭발력 적용 (Rigidbody가 있으면)
                if (hit.attachedRigidbody != null)
                {
                    hit.attachedRigidbody.AddExplosionForce(explosionForce, transform.position, explosionRadius, 1f, ForceMode.Impulse);
                }
            }
        }

        private void ApplyPoisonGas()
        {
            // 독 가스 영역: 일정 시간 동안 중독 증가 또는 디버프 적용
            // 여기서는 간단히 피해를 주는 영역으로 구현 (실제로는 중독 시스템과 연동 필요)
            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, targetLayers);
            foreach (var hit in hits)
            {
                // 폭발 피해: IDamageable 대상에 직접 피해
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    Vector3 hitDir = (hit.transform.position - transform.position).normalized;
                    damageable.TakeDamage(5f, hitDir, "Poison");
                }
            }
            // 가스 시각 효과는 explosionEffectPrefab에서 처리한다고 가정
        }

        private void ApplySmoke()
        {
            // 연막: 일정 시간 동안 시야 가림 (예: 후처리 효과 또는 구름 프리펩)
            // 여기서는 단순히 폭발 효과와 동일하게 처리하고, 별도의 스모크 영역 스크립트가 있다고 가정
            // SmokeArea.cs 라는 스크립트를 효과 프리펩에 붙여서 구현할 수 있음.
            // 여기서는 별도 처리 없음.
        }

        private void ApplyMolotov()
        {
            // 화염 영역: 일정 시간 동안 화염 피해 영역 생성
            // MolotovArea.cs 스크립트를 효과 프리펩에 붙여서 구현
        }

        // 편집기에서 반경 표시
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}