using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

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
        [Header("Damage")]
        public float bombDamage = 25f;   // [193차] 폭탄 중심 피해량 — ItemData.bombDamage 주입(BombArmController.SpawnThrownBomb), 폴백 25f

        [Header("Effects")]
        public GameObject explosionEffectPrefab;
        public AudioClip explosionSound;
        public float explosionForce = 500f; // 폭발력으로 주변 객체 밀치기

        private void Start()
        {
            // 퓨즈 시작
            Invoke(nameof(Explode), explosionDelay);

            // [무번호 181] 착지 텔레그래프 — 낙하 중 퓨즈가 이미 시작되므로 Start에서 지면 투영 원형 표시(사용자 요구
            // "떨어질 때 피격 범위 표시"). 폭탄은 공중에서도 이동하므로 고정 원형이 아닌 followRoot 추적 모드(Bomb transform) —
            // 인디케이터가 followRoot.position의 x/z를 따라가고 y는 지면 쿼리로 접지.
            // [207차 P4-1] y=0.02f 하드코딩 폐기 → TelegraphArcIndicator.QueryGroundY 지면 높이 쿼리
            // (①Physics.Raycast 하향 ②GetHeightAt(Plains, 42) ③0.02 폴백) — MainScene 지형 y≈42에서
            // 원형이 지하 42m 렌더되어 보이지 않던 근원 수리. duration = explosionDelay + 0.1f 여유.
            // 연출만 — 퓨즈/피해 로직 무영향(어떤 브랜치에서도 예외 재Throw 없이 스킵).
            try
            {
                float groundY = TelegraphArcIndicator.QueryGroundY(transform.position, 0.02f);   // [207차 P4-1]
                TelegraphArcIndicator.SpawnCircle(
                    new Vector3(transform.position.x, groundY, transform.position.z),
                    explosionRadius, explosionDelay + 0.1f, transform, false);
            }
            catch (System.Exception telegraphEx)
            {
                Debug.LogWarning($"[Bomb] 착지 텔레그래프 스킵 — 퓨즈/피해 로직 무영향: {telegraphEx.Message}");
            }
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

            // [무번호 181] 타입별 에셋 VFX 발화(시각만 — 퓨즈/피해 로직 무영향, LDR 계약: 색/머티리얼 조작 0건).
            // PoisonGas는 시각 추가 없음(기존 유지 — 가스 시각은 기존 가스 시스템 소관, 계획 기록).
            switch (bombType)
            {
                case BombType.Explosive:
                    MeteoriteVFXRunner.PlayImpactSequence(transform.position);   // 180차 임팩트 시퀀스(라이트 플래시+2차 파장) 1회 재사용
                    break;
                case BombType.Molotov:
                    BombVfx.PlayPrefab(BombVfx.FireBigPath, transform.position, 6f);   // 화염 영역 연출
                    break;
                case BombType.Smoke:
                    BombVfx.PlayPrefab(BombVfx.DeathSmokePath, transform.position, 4f);
                    break;
                case BombType.PoisonGas:
                    break;   // 시각 추가 없음 — 기존 유지
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
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();   // [193차] 루트 탐색 — 근접(AttackSystem)과 동일 단일소스(자식 히트박스 → 루트 IDamageable 해석)
                if (damageable != null)
                {
                    // 거리 기반 데미지 감쇠 (선택)
                    float distance = Vector3.Distance(transform.position, hit.transform.position);
                    float damage = bombDamage * (1f - distance / explosionRadius);   // [193차] 스탯 기반 피해량 — 하드코딩 10f 폐지(ItemData.bombDamage 주입)
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
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();   // [193차] 루트 탐색 — 근접(AttackSystem)과 동일 단일소스(자식 히트박스 → 루트 IDamageable 해석)
                if (damageable != null)
                {
                    // [2026-10-08] 플레이어는 방독면 착용 중이면 독가스 효과를 받지 않는다.
                    if (hit.CompareTag("Player") && GasMaskSystem.IsActive) continue;

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
            // [63-B] 화염 접촉 → 플레이어 화상(강틱 DOT 5HP/s — 화염 자체 대미지는 Burn이 담당, 신설 대미지 없음).
            // 기존 계획(MolotovArea 프리펩) 미구현 상태 — OverlapSphere 1회 접촉 판정(ApplyPoisonGas 선례).
            Collider[] fireHits = Physics.OverlapSphere(transform.position, explosionRadius, targetLayers);
            foreach (var fireHit in fireHits)
            {
                if (!fireHit.CompareTag("Player")) continue;
                PlayerStatusEffectSystem.AddEffect(StatusEffectId.Burn, 4f);
            }
        }

        // 편집기에서 반경 표시
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }

        // ===== [무번호 181] 폭탄 VFX 공용 헬퍼 =====

        /// <summary>
        /// [무번호 181] 폭탄 VFX 정적 헬퍼 — Resources 프리팹 지연 로드 + static 캐시 + 인스턴스화(빈 GO 자식 배치)
        /// + 수명 자동 소멸(Object.Destroy(go, 수명)). 계약 준수: 절차 파티클 금지(46차 — 에셋 경로 Instantiate만) ·
        /// 색/머티리얼 조작 없음(LDR — 인스턴스화+배치만) · MonsterSkillSystem.MonsterSkillVfx(180차) 동일 패턴 승계.
        /// </summary>
        private static class BombVfx
        {
            // ── Resources 경로 상수(단일 소스 — 확장자 제외, 전부 Resources 실재) ──
            internal const string FireBigPath = "FX/Fire/VFX_Fire_01_Big";
            internal const string DeathSmokePath = "FX/Death/DeathSmoke";

            private static readonly Dictionary<string, GameObject> _prefabCache =
                new Dictionary<string, GameObject>();
            private static bool _loadWarned;   // 로드 실패 1회 경고(MeteoriteVFXRunner 선례 — 스팸 방지)

            /// <summary>
            /// 프리팹 지연 로드+캐시 → 빈 GO 자식 배치 인스턴스화 → 수명 자동 소멸(Object.Destroy(go, 수명)).
            /// 파티클 컴포넌트 파라미터/색/머티리얼 일절 조작 없음(인스턴스화+배치만 — LDR 계약 선례). 즉시 리턴(로직 지연 없음).
            /// </summary>
            internal static GameObject PlayPrefab(string resourcePath, Vector3 position, float lifetimeSec)
            {
                if (!_prefabCache.TryGetValue(resourcePath, out GameObject prefab) || prefab == null)
                {
                    prefab = Resources.Load<GameObject>(resourcePath);
                    _prefabCache[resourcePath] = prefab;   // null도 캐시(반복 로드 시도 방지)
                    if (prefab == null && !_loadWarned)
                    {
                        _loadWarned = true;
                        Debug.LogWarning($"[BombVfx] Resources 프리팹 로드 실패: {resourcePath} — 임포트 상태 확인 필요, 스킵(전투 흐름 무영향)");
                    }
                }
                if (prefab == null) return null;

                // 빈 GO 자식 배치 — 루트 1개 수명 소멸 예약으로 자식 프리팹 인스턴스 동반 정리(정리 경로 단일화)
                var root = new GameObject("BombVfx_" + resourcePath.Replace('/', '_'));
                root.transform.SetPositionAndRotation(position, Quaternion.identity);
                Object.Instantiate(prefab, root.transform, false);
                Object.Destroy(root, Mathf.Max(0.1f, lifetimeSec));   // 수명 자동 소멸 — 잔존 GO 누출 금지
                return root;
            }
        }
    }
}