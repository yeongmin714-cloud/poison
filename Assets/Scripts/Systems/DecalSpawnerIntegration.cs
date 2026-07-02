using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// DecalSpawner와 전투/이동 시스템 통합
    /// 플레이어(PlayerCombat) 및 몬스터(AnimalAI)에 부착하여 데미지/이벤트 시 데칼 생성
    /// </summary>
    public class DecalSpawnerIntegration : MonoBehaviour
    {
        [Header("데칼 설정")]
        [SerializeField] private bool _spawnBloodOnDamage = true;
        [SerializeField] private bool _spawnFootprints = true;
        [SerializeField] private float _footprintInterval = 1.5f;  // 발자국 간격 (초)

        [Header("Poison 데칼")]
        [SerializeField] private bool _spawnPoisonOnGas = true;

        // 내부 상태
        private float _lastFootprintTime;
        private CharacterController _charController;
        private IDamageable _damageable;
        private bool _hasDamageable;

        // ================================================================
        // Unity 생명주기
        // ================================================================

        private void Awake()
        {
            _charController = GetComponent<CharacterController>();
            _damageable = GetComponent<IDamageable>();
            _hasDamageable = _damageable != null;
        }

        private void Start()
        {
            _lastFootprintTime = -_footprintInterval;
        }

        private void Update()
        {
            // === 발자국 데칼 (플레이어 이동 시) ===
            if (_spawnFootprints && _charController != null && _charController.isGrounded)
            {
                Vector3 velocity = _charController.velocity;
                float speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;

                // 일정 속도 이상이고 쿨다운이 지났을 때
                if (speed > 2.0f && Time.time - _lastFootprintTime >= _footprintInterval)
                {
                    _lastFootprintTime = Time.time;

                    // 발 아래 위치 계산
                    Vector3 footPos = transform.position + Vector3.down * (_charController.height * 0.5f);
                    Quaternion footRot = Quaternion.LookRotation(new Vector3(velocity.x, 0f, velocity.z).normalized);

                    DecalSpawner.Instance?.SpawnDecal("Footprint", footPos, footRot);
                }
            }
        }

        // ================================================================
        // 이벤트 리스너
        // ================================================================

        private void OnEnable()
        {
            // AnimalAI의 TakeDamage 호출 시 후크
            // IDamageable를 가진 컴포넌트가 데미지를 받으면 호출됨
        }

        // ================================================================
        // 공개 메서드 (외부 호출용)
        // ================================================================

        /// <summary>
        /// 피격 지점에 혈흔 데칼 생성 (PlayerCombat/AnimalAI에서 호출)
        /// </summary>
        /// <param name="hitPoint">피격 월드 위치</param>
        /// <param name="hitNormal">피격 표면 법선</param>
        public void OnDamageDealt(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!_spawnBloodOnDamage) return;

            // 표면 방향으로 회전 계산 (DecalProjector는 Z축이 투영 방향)
            Quaternion rotation = Quaternion.LookRotation(-hitNormal);

            DecalSpawner.Instance?.SpawnDecal("BloodSplat", hitPoint, rotation);
        }

        /// <summary>
        /// 가스 분사 지점에 독 웅덩이 데칼 생성
        /// </summary>
        /// <param name="position">가스 분사 위치</param>
        public void OnGasSpray(Vector3 position)
        {
            if (!_spawnPoisonOnGas) return;

            DecalSpawner.Instance?.SpawnDecal("PoisonPuddle", position, Quaternion.identity);
        }

        /// <summary>
        /// 특정 위치에 수동으로 발자국 생성
        /// </summary>
        /// <param name="position">위치</param>
        /// <param name="rotation">회전</param>
        public void PlaceFootprint(Vector3 position, Quaternion rotation)
        {
            if (!_spawnFootprints) return;

            DecalSpawner.Instance?.SpawnDecal("Footprint", position, rotation);
        }
    }
}
