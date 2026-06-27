using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// ND-04: 드라큘라 영주 보스.
    /// 강화 HP/데미지, 특수 패턴: 순간이동(5초 쿨다운), 흡혈(데미지의 20% HP회복).
    /// IDamageable 구현, 사망 시 특수 드랍 트리거.
    /// </summary>
    public class DraculaLord : MonoBehaviour, IDamageable
    {
        [Header("스탯")]
        [SerializeField] private float _maxHP = 5000f;
        [SerializeField] private float _attackDamage = 80f;
        [SerializeField] private float _defense = 25f;
        [SerializeField] private float _attackCooldown = 2f;
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _detectRange = 15f;
        [SerializeField] private float _attackRange = 3f;

        [Header("특수 패턴")]
        [SerializeField] private float _teleportCooldown = 5f;
        [SerializeField] [Range(0f, 1f)] private float _lifeStealRatio = 0.2f;

        private float _currentHP;
        private bool _isDead;
        private float _attackTimer;
        private float _teleportTimer;

        // ===== Placeholder 도형 =====
        private GameObject _visualRoot;
        private Transform _target;
        private readonly string _targetTag = "Player";

        // ===== 영지/식별 =====
        private TerritoryId _territoryId;
        private string _fullName = "드라큘라 영주";

        // ===== 타겟 캐싱 (Find 대신) =====
        private Transform _cachedPlayerTarget;
        private float _lastTargetSearchTime;
        private const float TARGET_SEARCH_INTERVAL = 0.5f;

        // ===== 이벤트 =====
        public static event System.Action<DraculaLord> OnLordDefeated;
        public event System.Action<DraculaLord> OnLordDied;
        public event System.Action<DraculaLord> OnLordDamaged;
        public event System.Action<DraculaLord> OnBatsSummoned;

        private void Awake()
        {
            _currentHP = _maxHP;
            _attackTimer = 0f;
            _teleportTimer = 0f;
            CreateVisualPlaceholder();
        }

        private void Start()
        {
            gameObject.tag = "DraculaLord";
        }

        private void OnDestroy()
        {
            // 정적 이벤트 구독 해제 방어
            // (필요시 여기서 OnLordDefeated -= ... 수행)
            CleanupVisuals();
        }

        private void Update()
        {
            if (_isDead) return;

            _attackTimer += Time.deltaTime;
            _teleportTimer += Time.deltaTime;

            // 타겟 탐색 (주기적으로만 Find 수행)
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                if (Time.time - _lastTargetSearchTime >= TARGET_SEARCH_INTERVAL)
                {
                    FindTarget();
                    _lastTargetSearchTime = Time.time;
                }
            }

            if (_target == null) return;

            float dist = Vector3.Distance(transform.position, _target.position);

            // 순간이동 (쿨다운 체크 + 거리 조건)
            if (_teleportTimer >= _teleportCooldown && dist > _attackRange && dist <= _detectRange)
            {
                TeleportBehindTarget();
                _teleportTimer = 0f;
                return;
            }

            // 공격
            if (dist <= _attackRange && _attackTimer >= _attackCooldown)
            {
                AttackTarget();
                _attackTimer = 0f;
            }
            else if (dist <= _detectRange && dist > _attackRange)
            {
                // 추적
                Vector3 dir = (_target.position - transform.position).normalized;
                dir.y = 0;
                transform.position += dir * _moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        private void FindTarget()
        {
            // 캐싱된 타겟이 유효하면 재사용
            if (_cachedPlayerTarget != null && _cachedPlayerTarget.gameObject.activeInHierarchy)
            {
                float dist = Vector3.Distance(transform.position, _cachedPlayerTarget.position);
                if (dist <= _detectRange * 1.5f)
                {
                    _target = _cachedPlayerTarget;
                    return;
                }
            }

            GameObject player = GameObject.FindGameObjectWithTag(_targetTag);
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist <= _detectRange * 1.5f)
                {
                    _target = player.transform;
                    _cachedPlayerTarget = _target;
                }
            }
        }

        /// <summary>
        /// 순간이동: 타겟 뒤쪽으로 3~5m 떨어진 위치로 이동
        /// </summary>
        private void TeleportBehindTarget()
        {
            if (_target == null) return;

            Vector3 behindDir = -_target.forward;
            Vector3 teleportPos = _target.position + behindDir * Random.Range(3f, 5f);
            teleportPos.y = transform.position.y;

            transform.position = teleportPos;

            // 타겟을 바라보도록 회전
            transform.LookAt(new Vector3(_target.position.x, transform.position.y, _target.position.z));

            Debug.Log($"[DraculaLord] ⚡ 순간이동! → {teleportPos}");

            // 순간이동 시 시각 효과 — Sphere 대신 단순 Debug 표시로 변경 (불필요한 콜라이더 방지)
            Debug.DrawLine(teleportPos, teleportPos + Vector3.up * 2f, Color.red, 0.3f);
        }

        private void AttackTarget()
        {
            if (_target == null) return;

            var damageable = _target.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                Vector3 hitDir = (_target.position - transform.position).normalized;

                // 데미지 적용 (방어력 적용)
                float finalDamage = Mathf.Max(1f, _attackDamage - _defense);
                damageable.TakeDamage(finalDamage, hitDir, "melee");

                // 흡혈: 실제 가한 데미지의 20% HP 회복
                float healAmount = finalDamage * _lifeStealRatio;
                _currentHP = Mathf.Min(_maxHP, _currentHP + healAmount);

                Debug.Log($"[DraculaLord] 🧛 공격! {finalDamage} 데미지 (방어력 적용), {healAmount:F1} HP 회복 (HP: {_currentHP}/{_maxHP})");
            }
        }

        // ===== Placeholder 시각적 생성 =====
        private void CreateVisualPlaceholder()
        {
            if (_visualRoot != null)
                Destroy(_visualRoot);

            _visualRoot = new GameObject("DraculaLord_Visual");
            _visualRoot.transform.SetParent(transform, false);
            _visualRoot.transform.localPosition = Vector3.zero;

            // 몸통 (검은색 큐브 — 더 크게)
            var body = CreatePrimitiveNoCollider(PrimitiveType.Cube);
            body.transform.SetParent(_visualRoot.transform, false);
            body.transform.localScale = new Vector3(1.0f, 1.2f, 0.6f);
            body.transform.localPosition = new Vector3(0, 1.5f, 0);
            var bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.material.color = Color.black;

            // 망토 (빨간색 원기둥 — 뒤쪽)
            var cape = CreatePrimitiveNoCollider(PrimitiveType.Cylinder);
            cape.transform.SetParent(_visualRoot.transform, false);
            cape.transform.localScale = new Vector3(0.8f, 0.3f, 0.4f);
            cape.transform.localPosition = new Vector3(0, 1.2f, -0.4f);
            cape.transform.localRotation = Quaternion.Euler(20, 0, 0);
            var capeRenderer = cape.GetComponent<Renderer>();
            capeRenderer.material.color = Color.red;

            // 머리 (빨간색 캡슐)
            var head = CreatePrimitiveNoCollider(PrimitiveType.Capsule);
            head.transform.SetParent(_visualRoot.transform, false);
            head.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            head.transform.localPosition = new Vector3(0, 2.3f, 0);
            var headRenderer = head.GetComponent<Renderer>();
            headRenderer.material.color = new Color(0.8f, 0.1f, 0.1f); // 진한 빨강

            // 눈 (노란색 구)
            CreateEye(new Vector3(-0.18f, 2.4f, 0.25f), Color.yellow);
            CreateEye(new Vector3(0.18f, 2.4f, 0.25f), Color.yellow);

            // 팔 (검은색 캡슐)
            CreateLimb(new Vector3(-0.75f, 1.8f, 0), new Vector3(0.2f, 0.6f, 0.2f), Color.black);
            CreateLimb(new Vector3(0.75f, 1.8f, 0), new Vector3(0.2f, 0.6f, 0.2f), Color.black);

            // 다리 (검은색 캡슐)
            CreateLimb(new Vector3(-0.3f, 0.5f, 0), new Vector3(0.2f, 0.6f, 0.2f), Color.black);
            CreateLimb(new Vector3(0.3f, 0.5f, 0), new Vector3(0.2f, 0.6f, 0.2f), Color.black);

            // HP 바 표시 위함 (더 큰 오브젝트)
            transform.localScale = Vector3.one * 1.2f;
        }

        /// <summary>
        /// 콜라이더 없는 Primitive 생성 (시각적 Placeholder 전용)
        /// </summary>
        private static GameObject CreatePrimitiveNoCollider(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            return go;
        }

        private void CreateEye(Vector3 position, Color color)
        {
            var eye = CreatePrimitiveNoCollider(PrimitiveType.Sphere);
            eye.transform.SetParent(_visualRoot.transform, false);
            eye.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
            eye.transform.localPosition = position;
            var eyeRenderer = eye.GetComponent<Renderer>();
            eyeRenderer.material.color = color;
        }

        private void CreateLimb(Vector3 position, Vector3 scale, Color color)
        {
            var limb = CreatePrimitiveNoCollider(PrimitiveType.Capsule);
            limb.transform.SetParent(_visualRoot.transform, false);
            limb.transform.localScale = scale;
            limb.transform.localPosition = position;
            var limbRenderer = limb.GetComponent<Renderer>();
            limbRenderer.material.color = color;
        }

        private void CleanupVisuals()
        {
            if (_visualRoot != null)
            {
                Destroy(_visualRoot);
                _visualRoot = null;
            }
        }

        // ===== IDamageable =====

        public bool IsAlive => !_isDead;

        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            if (_isDead) return;

            // 방어력 적용 (최소 1 데미지)
            float actualDamage = Mathf.Max(1f, amount - _defense);
            _currentHP -= actualDamage;

            Debug.Log($"[DraculaLord] 💥 {amount} raw 데미지 → {actualDamage} 실제 데미지 (방어력 {_defense}) (HP: {Mathf.Max(0, _currentHP)}/{_maxHP})");

            OnLordDamaged?.Invoke(this);

            if (_currentHP <= 0)
                Die();
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log("[DraculaLord] 🧛 드라큘라 영주 처치!");

            // 사망 이벤트 발생 (static + instance)
            OnLordDefeated?.Invoke(this);
            OnLordDied?.Invoke(this);

            // DraculaTerritoryController에 통지
            if (DraculaTerritoryController.Instance != null)
                DraculaTerritoryController.Instance.OnDraculaLordDefeated();

            // 특수 드랍 처리
            if (DropTableManager.Instance != null)
            {
                LootBasket basket = LootBasket.Create(transform.position + Vector3.up * 0.5f);
                DropTableManager.Instance.ApplyDraculaLordDrops(basket);
            }

            // 비활성화
            gameObject.SetActive(false);
        }

        // ===== 퍼블릭 API =====

        public float HP => _currentHP;
        public float MaxHP => _maxHP;
        public float AttackDamage => _attackDamage;
        public bool IsDead => _isDead;

        // ===== 추가 프로퍼티 (ND-02/ND-06 호환) =====
        public string FullName => _fullName;
        public float Attack => _attackDamage;
        public float Defense => _defense;
        public float CurrentHP => _currentHP;
        public float HPPercentage => _maxHP > 0f ? _currentHP / _maxHP : 0f;
        public TerritoryId TerritoryId => _territoryId;

        /// <summary>
        /// 테스트용 HP 설정
        /// </summary>
        public void SetHP(float hp)
        {
            _currentHP = Mathf.Clamp(hp, 0, _maxHP);
        }

        /// <summary>
        /// 테스트용 부활 (ResetHP와 동일, API 호환 유지)
        /// </summary>
        public void Resurrect()
        {
            ResetHP();
        }

        // ===== 추가 메서드 (ND-02/ND-06 호환) =====

        /// <summary>
        /// 단일 float 파라미터 TakeDamage 오버로드 (테스트 호환)
        /// </summary>
        public void TakeDamage(float rawDamage)
        {
            if (_isDead) return;

            // 방어력 적용 (최소 1 데미지)
            float actualDamage = Mathf.Max(1f, rawDamage - _defense);
            _currentHP -= actualDamage;

            Debug.Log($"[DraculaLord] 💥 {rawDamage} raw 데미지 → {actualDamage} 실제 데미지 (방어력 {_defense}) (HP: {Mathf.Max(0, _currentHP)}/{_maxHP})");

            OnLordDamaged?.Invoke(this);

            if (_currentHP <= 0)
                Die();
        }

        /// <summary>
        /// 체력 재생
        /// </summary>
        public void RegenerateHP(float amount)
        {
            if (_isDead) return;
            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);
        }

        /// <summary>
        /// 체력/사망 상태 완전 초기화
        /// </summary>
        public void ResetHP()
        {
            _isDead = false;
            _currentHP = _maxHP;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 스탯 변경
        /// </summary>
        public void SetStats(float maxHP, float attack, float defense)
        {
            _maxHP = maxHP;
            _attackDamage = attack;
            _defense = defense;
            _currentHP = _maxHP;
        }

        /// <summary>
        /// 박쥐 소환 강제 트리거 (테스트/이벤트용)
        /// </summary>
        public void ForceSummonBats()
        {
            OnBatsSummoned?.Invoke(this);
        }

        /// <summary>
        /// 영지 ID 설정
        /// </summary>
        public void SetTerritoryId(TerritoryId id)
        {
            _territoryId = id;
        }
    }
}