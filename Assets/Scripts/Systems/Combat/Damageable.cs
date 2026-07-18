using UnityEngine;
using ProjectName.Systems.Animation.Procedural;

namespace ProjectName.Systems
{
    /// <summary>
    /// 데미지 가능 인터페이스. 모든 피격 대상(플레이어, 몬스터, NPC 등)이 구현.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo damageInfo);
        void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee");
        float CurrentHP { get; }
        float MaxHP { get; }
        bool IsDead { get; }
        bool IsAlive { get; }
    }

    /// <summary>
    /// 데미지 정보 구조체.
    /// </summary>
    public struct DamageInfo
    {
        public float amount;           // 데미지량
        public Vector3 knockback;      // 넉백 벡터
        public float hitStun;          // 경직 시간
        public Transform source;       // 공격자
        public int attackID;           // 공격 ID (피해 타입 구분용)
        public DamageType type;        // 데미지 타입

        public DamageInfo(float amount = 0f, Vector3 knockback = default, float hitStun = 0f, Transform source = null, int attackID = 0, DamageType type = DamageType.Physical)
        {
            this.amount = amount;
            this.knockback = knockback;
            this.hitStun = hitStun;
            this.source = source;
            this.attackID = attackID;
            this.type = type;
        }
    }

    public enum DamageType
    {
        Physical,
        Magical,
        Poison,
        Fire,
        Ice,
        Lightning,
        True
    }

    /// <summary>
    /// 기본 데미지 처리 컴포넌트.
    /// </summary>
    public class Damageable : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float _maxHP = 100f;
        [SerializeField] private float _currentHP = 100f;

        [Header("Knockback")]
        [SerializeField] private float _knockbackResistance = 0f; // 0~1

        [Header("Invincibility")]
        [SerializeField] private float _invincibilityDuration = 0.5f;

        private float _invincibilityTimer;
        private Rigidbody _rigidbody;

        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsDead => _currentHP <= 0f;
        public bool IsAlive => !IsDead;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _currentHP = _maxHP;
        }

        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            var info = new DamageInfo
            {
                amount = amount,
                knockback = hitDirection,
                hitStun = 0f,
                source = null,
                attackID = 0,
                type = DamageType.Physical
            };
            TakeDamage(info);
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (IsDead) return;
            if (_invincibilityTimer > 0) return;

            // 데미지 계산
            float finalDamage = damageInfo.amount;
            _currentHP = Mathf.Max(0, _currentHP - finalDamage);

            // 넉백
            if (_rigidbody != null && damageInfo.knockback.sqrMagnitude > 0.01f)
            {
                Vector3 kb = damageInfo.knockback * (1f - _knockbackResistance);
                _rigidbody.AddForce(kb, ForceMode.VelocityChange);
            }

            // 무적 시간
            _invincibilityTimer = damageInfo.hitStun > 0 ? damageInfo.hitStun : _invincibilityDuration;

            // 사망 처리
            if (IsDead)
            {
                OnDeath();
            }
            else
            {
                OnHit(damageInfo);
            }
        }

        protected virtual void OnHit(DamageInfo info)
        {
            // 히트 리액션 트리거 (상태 머신 연동)
            var stateMachine = GetComponent<ProceduralAnimStateMachine>();
            if (stateMachine != null)
                stateMachine.TakeDamage(info.amount);
        }

        protected virtual void OnDeath()
        {
            // 적 처치 슬로우모션
            if (gameObject.CompareTag("Enemy") || gameObject.CompareTag("Monster"))
                CombatCameraEffects.PlayKill();

            var stateMachine = GetComponent<ProceduralAnimStateMachine>();
            if (stateMachine != null)
                stateMachine.Die();

            // Rigidbody 제어 해제 등
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.useGravity = true;
            }

            // 콜라이더 비활성화
            var cols = GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = false;

            // 파괴/비활성화
            Destroy(gameObject, 5f);
        }

        public void Heal(float amount)
        {
            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);
        }

        public void HealFull()
        {
            _currentHP = _maxHP;
        }
    }
}