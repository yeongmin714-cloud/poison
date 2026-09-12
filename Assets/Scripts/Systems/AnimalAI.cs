using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems.Animation.Neural;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 몬스터 AI — GAME_DATA.md v2.0 기반 24종 몬스터 지원.
    /// 토끼(도망), 멧돼지(돌진), 늑대(추격) 등 기본 행동 패턴 + 티어별 난이도.
    /// </summary>
    public class AnimalAI : MonoBehaviour, IDamageable, IAggroable
    {
        [Header("Monster Identity")]
        [SerializeField] private string _monsterId = "rabbit";  // MonsterDatabase 키

        [Header("Stats (auto-set by tier)")]
        [SerializeField] private float _maxHP = 10f;
        [SerializeField] private float _speed = 5f;
        [SerializeField] private float _detectRange = 10f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private int _attackDamage = 5;
        [SerializeField] private float _attackCooldown = 1.5f;
        [SerializeField] private MonsterTier _tier = MonsterTier.Beginner;

        [Header("Level System (5.3.5)")]
        [SerializeField] private int _level = 1;

        // [2026-09-11] 스폰 진단 로그 1회 출력 가드 — 중복 로그 방지
        private bool _spawnLogged;

        [Header("Drop Items (auto-set by MonsterDatabase)")]
        [SerializeField] private PlayerInventory.ItemData _meatDrop;
        [SerializeField] private int _minMeat = 1;
        [SerializeField] private int _maxMeat = 2;
        [SerializeField] private PlayerInventory.ItemData _materialDrop;
        [SerializeField] private int _materialCount = 1;
        [SerializeField] private PlayerInventory.ItemData _rareDrop;
        [SerializeField] [Range(0f, 1f)] private float _rareDropChance = 0.2f;

        [Header("Visual Settings")]
        [SerializeField] private Color _bodyColor = Color.white;

        [Header("Obstacle Avoidance")]
        [SerializeField] private LayerMask _obstacleLayers = ~0; // 레이어마스크 (장애물 레이어 권장, 플레이어 레이어 제외)
        [SerializeField, Range(0.1f, 2f)] private float _obstacleOffset = 0.5f; // 충돌 시 물러날 거리

        [Header("Effects")]
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private GameObject deathEffectPrefab;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip deathSound;

        // 상태
        private float _currentHP;
        private Transform _player;
        private Vector3 _spawnPos;
        private bool _isDead = false;
        private float _lastAttackTime;
        private Vector3 _fleeTarget;
        private Renderer _renderer;
        private Collider _collider;

        // 캐싱
        private float _bodyScale;
        private static readonly float _packCallRange = 8f;
        private float _packCallTimer; // CallNearbyMonsters 주기적 실행 타이머
        private const float PACK_CALL_INTERVAL = 1.5f;

        // Rig animation
        private RigAnimationController _rigAnim;
        private AnimationRiggingSetup _rigSetup;
        private NeuralAnimationController _neuralAnim;

        // === IAggroable (어그로 합세 시스템) ===
        private AggroState _aggroState = AggroState.Idle;
        private GameObject _aggroTarget;
        private float _aggroTimer;
        private GameObject _aggroAttacker; // 공격자 (SetAggroTarget 전달용)

        /// <summary>어그로 상태에 따른 감지/추격 거리 배율</summary>
        private const float AGGRO_SPEED_MULT = 1.2f;
        private const float ALERT_DURATION = 3f;
        private const float COOLDOWN_DURATION = 5f;

        /// <summary>몬스터 ID (MonsterDatabase 키). 읽기 전용.</summary>
        public string MonsterId => _monsterId;
        /// <summary>현재 HP 비율 (0~1)</summary>
        public float HPRatio => _maxHP > 0 ? _currentHP / _maxHP : 0;
        /// <summary>현재 HP</summary>
        public float CurrentHP => _currentHP;
        /// <summary>최대 HP</summary>
        public float MaxHP => _maxHP;
        /// <summary>사망 여부</summary>
        public bool IsDead => _isDead;
        /// <summary>IDamageable: 생존 여부</summary>
        public bool IsAlive => !_isDead;
        /// <summary>몬스터 티어</summary>
        public MonsterTier Tier => _tier;
        /// <summary>[5.3.5] 몬스터 레벨</summary>
        public int Level => _level;

        /// <summary>몬스터 ID 설정. MonsterSpawner에서 호출.</summary>
        public void SetMonsterId(string id)
        {
            _monsterId = id;
        }

        /// <summary>[5.3.5] 몬스터 레벨 설정. MonsterSpawner에서 호출.</summary>
        public void SetLevel(int level)
        {
            _level = level;
            ApplyLevelStats();
        }

        /// <summary>
        /// [5.3.5] 레벨 기반 스탯 적용
        /// MonsterLevelManager의 HP/데미지 계산값으로 오버라이드
        /// </summary>
        private void ApplyLevelStats()
        {
            // [2026-09-11] 레벨 스케일 게이트 — 비활성 시 MonsterDatabase 기본 HP 유지.
            // 테스트 씬은 몬스터가 몇 타에 죽어야 공격 검증 가능(영상 실측: HP바 0 근처인데도 미사망의
            // 원인이 hpPerLevel×level 오버라이드로 커진 MaxHP). SetLevel→ApplyLevelStats 경로만 차단하며
            // Respawn()은 스케일 재적용 없이 _maxHP 재사용하므로 그대로 안전.
            if (!MonsterLevelManager.LevelScalingEnabled) return;

            if (_level <= 0) return;

            MonsterLevelManager mgr = MonsterLevelManager.Instance;
            if (mgr != null)
            {
                _maxHP = mgr.GetMonsterHP(_level, _tier);
                float dmg = mgr.GetMonsterDamage(_level);
                _attackDamage = Mathf.Max(1, Mathf.RoundToInt(dmg));

                // MonsterLevelLabel 업데이트
                ILevelLabel label = GetComponent<ILevelLabel>();
                if (label != null)
                    label.SetLevel(_level);
            }
        }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();

            // Rig animation setup — only for rigged GLB models with actual Animator controller
            _rigAnim = GetComponent<RigAnimationController>();
            _rigSetup = GetComponent<AnimationRiggingSetup>();

            // Animator가 있고 실제 controller가 있는 경우에만 RigAnimationController 추가
            if (_rigAnim == null)
            {
                Animator anim = GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    _rigAnim = gameObject.AddComponent<RigAnimationController>();
                }
            }

            // NeuralAnimationController 설정
            _neuralAnim = GetComponent<NeuralAnimationController>();
            if (_neuralAnim == null)
                _neuralAnim = gameObject.AddComponent<NeuralAnimationController>();
        }

        private void Start()
        {
            // MonsterDatabase에서 데이터 로드
            ApplyMonsterDefinition();

            _currentHP = _maxHP;

            // [2026-09-11] 스폰 진단: HP 최종 결정 지점 1회 로그(사망 판정 디버깅).
            // SetMonsterId/SetLevel→ApplyLevelStats는 Start 이전에 호출되고, Start의
            // ApplyMonsterDefinition이 HP를 재확정하므로 이 지점이 모든 스폰 경로의 공용 최종값.
            if (!_spawnLogged)
            {
                _spawnLogged = true;
                Debug.Log($"[AnimalAI] 스폰 {_monsterId} MaxHP={_maxHP} (스케일게이트={MonsterLevelManager.LevelScalingEnabled}, 레벨={_level})");
            }
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            _spawnPos = transform.position;

            // Auto-exclude player layer from obstacle mask to prevent self-hitting
            if (_player != null)
            {
                // Remove player's layer from obstacle mask
                _obstacleLayers &= ~(1 << _player.gameObject.layer);
            }

            // MonsterAggroSystem 등록
            if (MonsterAggroSystem.Instance != null)
            {
                MonsterAggroSystem.Instance.RegisterMonster(this);
            }
        }

        /// <summary>
        /// MonsterDatabase에서 몬스터 정의를 찾아 스탯 자동 설정
        /// </summary>
        private void ApplyMonsterDefinition()
        {
            MonsterDef def = MonsterDatabase.Get(_monsterId);
            if (def == null)
            {
                Debug.LogWarning($"[AnimalAI] 몬스터 ID '{_monsterId}'를 찾을 수 없습니다. 기본값 사용.");
                return;
            }

            _tier = def.tier;
            _maxHP = def.baseHP;
            _attackDamage = def.baseDamage;
            _speed = def.baseSpeed;
            _bodyColor = def.gizmoColor;

            // C20-02: 난이도별 HP/데미지 배율 적용
            float hpMult = DifficultyManager.GetHpMultiplier((DifficultyMode)GameManager.CurrentDifficulty);
            float dmgMult = DifficultyManager.GetDamageMultiplier((DifficultyMode)GameManager.CurrentDifficulty);
            _maxHP = Mathf.RoundToInt(_maxHP * hpMult);
            _attackDamage = Mathf.Max(1, Mathf.RoundToInt(_attackDamage * dmgMult));

            // 티어별 추가 스탯
            switch (_tier)
            {
                case MonsterTier.Beginner:
                    _detectRange = 10f;
                    _attackRange = 2f;
                    _attackCooldown = 1.5f;
                    _bodyScale = Random.Range(0.6f, 0.9f);
                    break;
                case MonsterTier.Intermediate:
                    _detectRange = 14f;
                    _attackRange = 2.5f;
                    _attackCooldown = 1.2f;
                    _bodyScale = Random.Range(0.9f, 1.2f);
                    break;
                case MonsterTier.Advanced:
                    _detectRange = 18f;
                    _attackRange = 3f;
                    _attackCooldown = 1.0f;
                    _bodyScale = Random.Range(1.2f, 1.8f);
                    break;
            }

            // 스케일 적용
            transform.localScale = Vector3.one * _bodyScale;

            // 색상 적용
            ApplyColor();

            // 고기 드랍 자동 설정
            SetAutoDrops(def);

            // 이펙트 및 사운드 설정 (몬스터별)
            hitEffectPrefab = def.hitEffectPrefab;
            deathEffectPrefab = def.deathEffectPrefab;
            hitSound = def.hitSound;
            deathSound = def.deathSound;
        }

        /// <summary>
        /// 몬스터 색상을 Material 또는 MeshRenderer에 적용
        /// </summary>
        private void ApplyColor()
        {
            if (_renderer != null && _renderer.material != null)
            {
                _renderer.material.color = _bodyColor;
            }
        }

        /// <summary>
        /// 몬스터 이동 시 장애물 회피
        /// </summary>
        private void HandleObstacleAvoidance(ref Vector3 desiredPos)
        {
            if (_player == null) return;
            Vector3 direction = desiredPos - transform.position;
            float distance = direction.magnitude;
            if (Physics.Raycast(transform.position, direction.normalized, out RaycastHit hit, distance, _obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                desiredPos = hit.point + hit.normal * _obstacleOffset;
            }
        }

        /// <summary>
        /// 몬스터 ID 기반 드랍 아이템 자동 설정
        /// </summary>
        private void SetAutoDrops(MonsterDef def)
        {
            // 초반 몬스터 드랍 (튜토리얼 호환)
            switch (_monsterId)
            {
                case "rabbit":
                    _meatDrop = PlayerInventory.RabbitMeat; _minMeat = 1; _maxMeat = 2;
                    _materialDrop = PlayerInventory.RabbitFur; _materialCount = 1;
                    _rareDrop = null; _rareDropChance = 0f;
                    break;
                case "boar":
                    _meatDrop = PlayerInventory.BoarMeat; _minMeat = 1; _maxMeat = 2;
                    _materialDrop = PlayerInventory.BoarLeather; _materialCount = 1;
                    _rareDrop = PlayerInventory.BoarTusk; _rareDropChance = 0.2f;
                    break;
                case "wolf":
                    _meatDrop = PlayerInventory.WolfMeat; _minMeat = 1; _maxMeat = 2;
                    _materialDrop = PlayerInventory.WolfTooth; _materialCount = 1;
                    _rareDrop = PlayerInventory.WolfFur; _rareDropChance = 0.3f;
                    break;
                default:
                    // 나머지 몬스터는 이름 기반 일반 고기 드랍
                    string meatId = $"meat_{_monsterId}";
                    _meatDrop = new PlayerInventory.ItemData
                    {
                        id = meatId,
                        displayName = $"{def.displayName} 고기",
                        description = $"{def.displayName}에게서 얻은 고기.",
                        category = PlayerInventory.ItemCategory.Meat,
                        maxStack = 20
                    };
                    _minMeat = 1 + (int)_tier;
                    _maxMeat = 2 + (int)_tier;
                    // 일반 재료
                    _materialDrop = new PlayerInventory.ItemData
                    {
                        id = $"mat_{_monsterId}",
                        displayName = $"{def.displayName} 재료",
                        description = $"{def.displayName}에게서 얻은 재료.",
                        category = PlayerInventory.ItemCategory.Material,
                        maxStack = 20
                    };
                    _materialCount = 1;
                    _rareDrop = null;
                    _rareDropChance = _tier == MonsterTier.Advanced ? 0.3f : 0.1f;
                    break;
            }

            // C20-02: 난이도별 드랍률 배율 적용
            _rareDropChance = Mathf.Clamp01(_rareDropChance * DifficultyManager.GetDropRateMultiplier((DifficultyMode)GameManager.CurrentDifficulty));
        }

        private void Update()
        {
            if (_isDead || _player == null)
            {
                // 사망 또는 플레이어 없음 → Idle
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetStateImmediate(AnimationState.Idle);
                return;
            }

            // === 어그로 상태 처리 ===
            if (_aggroState != AggroState.Idle)
            {
                UpdateAggroBehavior();
                return; // 어그로 상태에서는 기존 행동 대신 어그로 행동 수행
            }

            float dist = Vector3.Distance(transform.position, _player.position);

            switch (_tier)
            {
                case MonsterTier.Beginner:
                    UpdateBeginner(dist);
                    break;
                case MonsterTier.Intermediate:
                    UpdateIntermediate(dist);
                    break;
                case MonsterTier.Advanced:
                    UpdateAdvanced(dist);
                    break;
            }
        }

        /// <summary>
        /// 초반 티어 행동: 대부분 도망 + 일부 추격
        /// </summary>
        private void UpdateBeginner(float dist)
        {
            if (dist > _detectRange)
            {
                // 감지 범위 밖 → Idle
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetState(AnimationState.Idle);
                return;
            }

            switch (_monsterId)
            {
                case "rabbit":
                case "deer":
                case "bat":
                case "crow":
                {
                    // 도망
                    Vector3 awayDir = (transform.position - _player.position).normalized;
                    awayDir.y = 0;
                    transform.rotation = Quaternion.LookRotation(awayDir);
                    Vector3 desiredPos = transform.position + awayDir * _speed * Time.deltaTime;
                    HandleObstacleAvoidance(ref desiredPos);
                    transform.position = desiredPos;

                    // 애니메이션: Walk (도망)
                    if (_rigAnim != null) { _rigAnim.CurrentSpeed = _speed; _rigAnim.SetState(AnimationState.Walk); }
                    break;
                }
            case "boar":
                {
                    // 돌진
                    if (dist > _attackRange)
                    {
                        Vector3 dir = (_player.position - transform.position).normalized;
                        dir.y = 0;
                        transform.rotation = Quaternion.LookRotation(dir);
                        Vector3 desiredPos = transform.position + dir * (_speed * 1.5f) * Time.deltaTime;
                        HandleObstacleAvoidance(ref desiredPos);
                        transform.position = desiredPos;

                        // 애니메이션: Walk (돌진)
                        if (_rigAnim != null) { _rigAnim.CurrentSpeed = _speed * 1.5f; _rigAnim.SetState(AnimationState.Walk); }
                    }
                    else { TryAttack(); }
                    break;
                }
            case "wolf":
            case "giant_rat":
            case "poison_snake":
            default:
                {
                    // 추격
                    if (dist > _attackRange)
                    {
                        Vector3 dir = (_player.position - transform.position).normalized;
                        dir.y = 0;
                        transform.rotation = Quaternion.LookRotation(dir);
                        Vector3 desiredPos = transform.position + dir * _speed * Time.deltaTime;
                        HandleObstacleAvoidance(ref desiredPos);
                        transform.position = desiredPos;
                        if (_monsterId == "wolf") CallNearbyMonsters();

                        // 애니메이션: Walk (추격)
                        if (_rigAnim != null) { _rigAnim.CurrentSpeed = _speed; _rigAnim.SetState(AnimationState.Walk); }
                    }
                    else { TryAttack(); }
                    break;
                }
            }
        }

        /// <summary>
        /// 중반 티어 행동: 적극적 추격 + 다양한 공격
        /// </summary>
        private void UpdateIntermediate(float dist)
        {
            if (dist > _detectRange)
            {
                // 감지 범위 밖 → Idle
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetState(AnimationState.Idle);
                return;
            }

            if (dist > _attackRange)
            {
                Vector3 dir = (_player.position - transform.position).normalized;
                dir.y = 0;
                transform.rotation = Quaternion.LookRotation(dir);

                // 느린 몬스터는 천천히, 빠른 몬스터는 빠르게
                float speedMult = _monsterId switch
                {
                    "stone_golem" => 0.7f,
                    "swamp_croc" => 0.8f,
                    "forest_spirit" => 1.2f,
                    _ => 1.0f
                };
                Vector3 desiredPos = transform.position + dir * _speed * speedMult * Time.deltaTime;
                HandleObstacleAvoidance(ref desiredPos);
                transform.position = desiredPos;

                // 애니메이션: Walk
                if (_rigAnim != null) { _rigAnim.CurrentSpeed = _speed * speedMult; _rigAnim.SetState(AnimationState.Walk); }

                // 늪지악어: 접근 시 은신 효과
                if (_monsterId == "swamp_croc" && dist < _detectRange * 0.5f)
                {
                    // 더 빠르게 돌진
                    desiredPos = transform.position + dir * _speed * 1.5f * Time.deltaTime;
                    HandleObstacleAvoidance(ref desiredPos);
                    transform.position = desiredPos;

                    // 애니메이션: Run (돌진)
                    if (_rigAnim != null) { _rigAnim.CurrentSpeed = _speed * 1.5f; _rigAnim.SetState(AnimationState.Run); }
                }
            }
            else
            {
                TryAttack();
            }
        }

        /// <summary>
        /// 후반 티어 행동: 매우 공격적, 빠른 속도
        /// </summary>
        private void UpdateAdvanced(float dist)
        {
            if (dist > _detectRange)
            {
                // 감지 범위 밖 → Idle
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetState(AnimationState.Idle);
                return;
            }

            if (dist > _attackRange)
            {
                Vector3 dir = (_player.position - transform.position).normalized;
                dir.y = 0;
                transform.rotation = Quaternion.LookRotation(dir);

                // 그림자 암살자: 은신 후 기습 (빠르게)
                float speedMult = _monsterId == "shadow_assassin" ? 1.5f : 1.0f;
                Vector3 desiredPos = transform.position + dir * _speed * speedMult * Time.deltaTime;
                HandleObstacleAvoidance(ref desiredPos);
                transform.position = desiredPos;

                // 애니메이션: Walk (또는 Run)
                if (_rigAnim != null) { _rigAnim.CurrentSpeed = _speed * speedMult; _rigAnim.SetState(speedMult > 1.0f ? AnimationState.Run : AnimationState.Walk); }
            }
            else
            {
                TryAttack();
            }
        }

        /// <summary>
        /// 근처 같은 종류 몬스터 호출 (주기적 실행, 매 프레임 FindObjectsByType 방지)
        /// </summary>
        private void CallNearbyMonsters()
        {
            _packCallTimer += Time.deltaTime;
            if (_packCallTimer < PACK_CALL_INTERVAL) return;
            _packCallTimer = 0f;

            var monsters = FindObjectsByType<AnimalAI>();
            foreach (var m in monsters)
            {
                if (m == this || m._monsterId != _monsterId || m._isDead) continue;
                float d = Vector3.Distance(transform.position, m.transform.position);
                if (d < _packCallRange)
                {
                    // C27-02: 합세 — MonsterAggroSystem을 통해 다른 몬스터 어그로 설정
                    if (MonsterAggroSystem.Instance != null)
                    {
                        MonsterAggroSystem.Instance.NotifyAttack(m.gameObject, _player?.gameObject);
                    }
                }
            }
        }

        private void TryAttack()
        {
            if (Time.time - _lastAttackTime < _attackCooldown) return;
            _lastAttackTime = Time.time;

            // 애니메이션: Attack
            if (_rigAnim != null)
            {
                _rigAnim.SetState(AnimationState.Attack);
            }
            else
            {
                // 4족 몬스터: QuadrupedProceduralAnimation.RequestAttack() 시도
                var quadAnim = GetComponent<QuadrupedProceduralAnimation>();
                if (quadAnim != null)
                {
                    quadAnim.RequestAttack();
                }
                else
                {
                    // 애니메이션 컨트롤러 없는 몬스터 — 공격 모션 누락 가능
                    Debug.LogWarning($"[AnimalAI] ⚠️ {_monsterId}: RigAnimationController/QuadrupedProceduralAnimation 없음 → 공격 애니메이션 미출력");
                }
            }

            // Neural Animation: Combat 정책으로 전환
            _neuralAnim?.SwitchPolicy(NeuralAnimationController.PolicyType.Combat);

            // 🐉 MonsterSkillSystem: 스킬이 있는 몬스터는 스킬 우선 사용
            if (MonsterSkillSystem.Instance != null)
            {
                var skills = MonsterSkillSystem.Instance.GetSkillsForAI(this);
                if (skills.Length > 0)
                {
                    // 랜덤 스킬 선택 (50% 확률로 스킬 사용, 나머지는 기본 공격)
                    if (Random.value < 0.5f)
                    {
                        GameObject player = _player != null ? _player.gameObject : GameObject.FindGameObjectWithTag("Player");
                        if (player != null)
                        {
                            MonsterSkillSystem.MonsterSkillData skillData = skills[Random.Range(0, skills.Length)];
                            if (MonsterSkillSystem.Instance.ExecuteSkill(this, skillData, player))
                                return; // 스킬 실행 성공 → 기본 공격 스킵
                        }
                    }
                }
            }

            // === 공격 대상 결정: 어그로 대상(아군 병사 등) 우선, 없으면 플레이어 ===
            // [확장] 어그로 대상이 활성 + 생존 중인 IDamageable이면 그 대상에게 근접 데미지.
            //        대상이 없거나 죽었으면 기존처럼 플레이어 공격 (백워드 호환 경로 유지).
            IDamageable aggroDamageable = GetAliveAggroDamageable();
            if (aggroDamageable != null && _aggroTarget != null)
            {
                // 어그로 대상(병사 등)에게 데미지 — IDamageable.TakeDamage(float, Vector3, string) 오버로드 사용
                Vector3 hitDirection = (_aggroTarget.transform.position - transform.position).normalized;
                aggroDamageable.TakeDamage(_attackDamage, hitDirection, "melee");
                Debug.Log($"{MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId}가(이) {_aggroTarget.name}에게 {_attackDamage} 데미지!");
            }
            else if (PlayerHealth.Instance != null)
            {
                // 기존 경로: 플레이어에게 실제 데미지 (기본 공격)
                PlayerHealth.Instance.TakeDamage(_attackDamage);
                Debug.Log($"{MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId}가(이) 플레이어에게 {_attackDamage} 데미지!");
                // 적용 디버프: 슬로우 (이동 속도 감소)
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.AddBuff("Slowness", 0.5f, 5f); // 이동 속도 0.5 감소, 5초 지속
                    Debug.Log("[AnimalAI] 🐌 플레이어에게 슬로우 디버프 적용 (속도 -0.5, 5초)");
                }
                else
                {
                    Debug.LogWarning("[AnimalAI] BuffManager 인스턴스를 찾을 수 없습니다.");
                }
            }
            else
            {
                Debug.Log($"{_monsterId} attacks for {_attackDamage} damage!");
            }
        }

        /// <summary>
        /// 어그로 대상이 유효한 근접 공격 대상인지 확인하고 IDamageable 반환.
        /// 조건: 오브젝트 활성(activeInHierarchy) + 생존 중인 IDamageable 구현.
        /// 유효하지 않으면 null 반환 → 호출부는 기존 플레이어 공격 경로로 폴백.
        /// </summary>
        private IDamageable GetAliveAggroDamageable()
        {
            if (_aggroTarget == null || !_aggroTarget.activeInHierarchy) return null;
            var damageable = _aggroTarget.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return null;
            return damageable;
        }

        /// <summary>
        /// IDamageable.TakeDamage 구현 — 데미지 + 타격 반응 + 사망 처리
        /// 플레이어 공격에 의해 데미지 받음
        /// </summary>
        /// <param name="amount">데미지 양</param>
        /// <param name="hitDirection">타격 방향 (공격자 → 대상)</param>
        /// <param name="weaponType">무기 타입 (확장용)</param>
        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            if (_isDead) return;

            _currentHP -= amount;
            Debug.Log($"{MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId}가(이) {amount} 데미지! HP={_currentHP}/{_maxHP}");

            // === G2-04: 카메라 타격 이펙트 ===
            // FX 체인 격리: 이펙트 예외가 아래 사망 판정(Die()) 도달을 차단하지 않도록 감쌈.
            try
            {
                CombatCameraEffects.PlayHit();
            }
            catch (System.Exception fxEx)
            {
                LogFxWarningOnce("카메라 타격 이펙트", fxEx);
            }

            // === MonsterAggroSystem: 공격 통보 → 주변 합세 ===
            if (MonsterAggroSystem.Instance != null)
            {
                // 공격자 찾기: hitDirection 반대 방향으로 추정 (또는 player)
                GameObject attacker = null;
                if (_player != null) attacker = _player.gameObject;
                MonsterAggroSystem.Instance.NotifyAttack(gameObject, attacker);
            }

            // === G2-05: CombatVFXController + 레거시 히트 이펙트 (FX 체인 격리) ===
            // 데미지 숫자(IMGUI) 등 어떤 FX 예외가 발생해도 Die() 도달이 막히지 않도록
            // FX 전체를 try-catch로 격리한다. 예외 시 스팸 가드(1초 쿨다운) 경고 후 계속.
            try
            {
                CombatVFXController.PlayHitFlash(gameObject);
                CombatVFXController.SpawnHitSparks(transform.position);
                CombatVFXController.SpawnBloodSplatter(transform.position, hitDirection.normalized);

                // === VFX ===
                // 1. Sparks
                HitVFX.PlayHitEffect(transform.position, hitDirection.normalized);

                // 2. Damage Number
                HitVFX.SpawnDamageNumber(transform.position, amount);

                // 3. Damage Number (CombatVFXController — IMGUI)
                Color dmgColor = Color.green;
                if (amount / _maxHP >= 0.3f)
                    dmgColor = Color.red;
                else if (amount / _maxHP >= 0.15f)
                    dmgColor = Color.yellow;
                CombatVFXController.ShowDamageNumber(transform.position, Mathf.RoundToInt(amount), dmgColor);

                // 4. Hit Reaction (넉백 + 경직)
                var hitReaction = GetComponent<HitReaction>();
                if (hitReaction != null)
                {
                    hitReaction.PlayHitReaction(hitDirection, 1f);
                }
                else
                {
                    // HitReaction 없으면 직접 HitFlash만
                    // 2026-09-11: GLB 프리팹은 루트 _renderer가 null → 피격 플래시가 통째로 스킵됨.
                    // 자식 렌더러 폴백 탐색으로 피격 피드백 보장(못 찾으면 PlayHitFlash가 null-safe 스킵).
                    var flashRenderer = _renderer != null ? _renderer : GetComponentInChildren<Renderer>();
                    HitVFX.PlayHitFlash(flashRenderer);
                }

                // 기존 hit effect (레거시 호환)
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
                }
                if (hitSound != null)
                {
                    AudioSource.PlayClipAtPoint(hitSound, transform.position);
                }
            }
            catch (System.Exception fxEx)
            {
                LogFxWarningOnce("타격 FX 체인", fxEx);
            }

            // 🐉 슬라임 분열 체크 (HP 30% 이하)
            if (_monsterId == "slime" && _currentHP > 0 && _currentHP <= _maxHP * 0.3f)
            {
                // TrySplitSlime이 true를 반환하면 원본은 사망 처리됨
                if (MonsterSkillSystem.TrySplitSlime(this))
                    return;
            }

            if (_currentHP <= 0)
            {
                Die();
            }
            else if (_tier >= MonsterTier.Intermediate)
            {
                // 맞으면 더 먼 거리에서도 반응
                _detectRange = Mathf.Max(_detectRange, 20f);
                if (_monsterId == "wolf") CallNearbyMonsters();
            }
        }

        // FX 체인 예외 경고 스팸 가드(1초 쿨다운) — 매 히트마다 로그가 도배되지 않도록 함
        private static float _lastFxWarningTime = -999f;

        private static void LogFxWarningOnce(string context, System.Exception ex)
        {
            if (Time.realtimeSinceStartup - _lastFxWarningTime < 1f) return;
            _lastFxWarningTime = Time.realtimeSinceStartup;
            Debug.LogWarning($"[AnimalAI] {context} FX 예외 무시(사망/전리품 처리는 계속): {ex.GetType().Name}: {ex.Message}");
        }

        /// <summary>
        /// Auto-hunt by Hunter guard — returns drops directly and kills animal.
        /// Used by HuntingMission.
        /// </summary>
        public bool TryAutoHunt(out System.Collections.Generic.List<(PlayerInventory.ItemData item, int count)> drops)
        {
            drops = new System.Collections.Generic.List<(PlayerInventory.ItemData, int)>();
            if (_isDead) return false;

            _isDead = true;

            // 애니메이션: Idle (즉시)
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            // Generate drops (same logic as Die() but without LootBasket)
            int meatCount = _minMeat == _maxMeat ? _minMeat : Random.Range(_minMeat, _maxMeat + 1);
            if (_meatDrop != null && meatCount > 0)
                drops.Add((_meatDrop, meatCount));
            if (_materialDrop != null && _materialCount > 0)
                drops.Add((_materialDrop, _materialCount));
            if (_rareDrop != null && Random.value < _rareDropChance)
                drops.Add((_rareDrop, 1));

            // Hide visual — 2026-09-11: Die()와 동일 계약(자식 렌더러/콜라이더/헤드UI 일괄, GLB 대응)
            SetCorpseVisuals(false);

            // Schedule respawn
            CancelInvoke(nameof(Respawn));
            Invoke(nameof(Respawn), 10f + (int)_tier * 5f);

            return drops.Count > 0;
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            TakeDamage(damageInfo.amount, damageInfo.knockback.normalized, "melee");
        }

        /// <summary>
        /// 2026-09-11: 시체/리스폰 시각 토글 통합 헬퍼. GLB 몬스터는 렌더러가 자식 메시에만 있어
        /// 루트 _renderer 단일 참조로는 시체가 남거나 리스폰 시 복구되지 않는다.
        /// 자식 포함 Renderer/Collider 전체 + 부착된 MonsterHeadUI를 한 번에 토글한다.
        /// </summary>
        private void SetCorpseVisuals(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = visible;
            foreach (var c in GetComponentsInChildren<Collider>(true))
                c.enabled = visible;
            var head = GetComponent<MonsterHeadUI>();
            if (head == null) head = GetComponentInChildren<MonsterHeadUI>(true);
            if (head != null) head.enabled = visible;
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // 애니메이션: Idle (즉시)
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            // 어그로 해제
            ClearAggro();

            // 플레이어 경험치/킬 카운트 (선택적)
            Debug.Log($"{MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId} 사망!");
            // ⏱️ 전투 로그: 처치 기록
            string monsterName = MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId;
            CombatLog.AddEntry($"{monsterName} 처치!", LogType.Kill);
            // 경험치 획득 — 레벨 기반: 티어 기본 EXP × (1 + 레벨×0.1) × 난수(0.8~1.2)
            MonsterLevelManager mgr = MonsterLevelManager.Instance;
            float baseExp = (mgr != null && mgr.Data != null) ? mgr.Data.GetExpBase(_tier) : 15f;
            int exp = Mathf.Max(1, Mathf.RoundToInt(baseExp * (1f + _level * 0.1f) * Random.Range(0.8f, 1.2f)));
            Debug.Log($"[AnimalAI] {monsterName} 경험치 계산: base={baseExp:F0}(티어={_tier}) × (1+Lv{_level}×0.1) × 난수(0.8~1.2) → +{exp} EXP");
            PlayerStats stats = PlayerStats.Instance;
            if (stats != null)
            {
                stats.AddEXP(exp);
            }
            else
            {
                Debug.LogWarning("[AnimalAI] PlayerStats.Instance를 찾을 수 없습니다.");
            }

            // === G2-05: 사망 VFX ===
            CombatVFXController.SpawnBloodSplatter(transform.position, Vector3.up);

            // 🔊 컨트롤러 진동: 몬스터 사망 (Light)
            HapticFeedback.PlayPreset(HapticFeedback.RumblePreset.Light);

            // Death effect
            if (deathEffectPrefab != null)
            {
                Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            }
            if (deathSound != null)
            {
                AudioSource.PlayClipAtPoint(deathSound, transform.position);
            }

            // 드랍 아이템 바구니 생성
            LootBasket basket = LootBasket.Create(transform.position);
            Debug.Log($"[AnimalAI] 🧺 전리품 바구니 스폰 ({transform.position.x:F1}, {transform.position.y:F1}, {transform.position.z:F1})");

            // [전리품 종별 분리 2026-09-11] 티어 공용 DropTable(GetMonsterTable) 경로 제거.
            // 기존에는 같은 티어면 토끼/멧돼지/늑대가 전부 동일한 티어 테이블 전리품을 뽑는 문제가 있었음.
            // 이제 항상 개체별 드랍(_meatDrop/_materialDrop/_rareDrop — SetAutoDrops가 몬스터 ID별로 설정:
            // 토끼=토끼고기+토끼털, 멧돼지=멧돼지고기+멧돼지가죽+멧돼지엄니, 늑대=늑대고기+늑대이빨+늑대털)이
            // 유일한 드랍 경로이며 1차로 적용된다. (공용 DropTable과 결합하지 않음 → 중복 드랍 방지)

            // [5.3.5] 레벨 기반 희귀 드랍률 보정 (유지)
            float levelDropBonus = 0f;
            if (MonsterLevelManager.Instance != null)
                levelDropBonus = MonsterLevelManager.Instance.GetDropRateBonus(_level);

            // 개체별 드랍 적용: 고기(_minMeat~_maxMeat) + 재료(_materialCount) + 희귀(확률+레벨보정)
            int meatCount = _minMeat == _maxMeat ? _minMeat : Random.Range(_minMeat, _maxMeat + 1);
            if (_meatDrop != null && meatCount > 0)
            {
                basket.AddItem(_meatDrop, meatCount);
            }
            if (_materialDrop != null && _materialCount > 0)
            {
                basket.AddItem(_materialDrop, _materialCount);
            }
            // [5.3.5] 레벨 보정된 희귀 드랍 확률
            float finalRareChance = Mathf.Clamp01(_rareDropChance + levelDropBonus);
            if (_rareDrop != null && Random.value < finalRareChance)
            {
                basket.AddItem(_rareDrop, 1);
                Debug.Log($"[AnimalAI] ★ 희귀 드롭! {_rareDrop.displayName} (레벨보정: +{levelDropBonus * 100:F0}%)");
            }

            // === 최소 전리품 보장: 바구니가 절대 빈 채로 소멸하지 않도록 ===
            // 개체별 드랍 적용 결과가 빈 경우 기본 아이템(고기 → 없으면 금화) 1개 이상 보장
            if (basket.IsEmpty)
            {
                PlayerInventory.ItemData guaranteedItem = _meatDrop != null ? _meatDrop : PlayerInventory.Gold;
                basket.AddItem(guaranteedItem, 1);
                Debug.Log($"[AnimalAI] 🧺 최소 전리품 보장: 빈 바구니에 {guaranteedItem.displayName} x1 추가 ({monsterName})");
            }

            // 시체 처리 — 2026-09-11: GLB 프리팹은 렌더러가 자식 메시에만 존재해 루트 _renderer가 null.
            // 단일 _renderer 토글로는 시체가 화면에 남아 Respawn(10초+)까지 표시됨 →
            // 자식 렌더러 전체 + 콜라이더(자식 포함) + MonsterHeadUI 일괄 비활성.
            // (페이드아웃 없이 즉시 비활성 — 리스폰 시 SetCorpseVisuals(true)로 시각 원복)
            SetCorpseVisuals(false);

            // 리스폰
            float respawnDelay = 10f + (int)_tier * 5f;
            // C20-02: 난이도별 리스폰 속도 배율 (Easy: 빠름, Hard: 느림)
            respawnDelay *= DifficultyManager.GetRespawnRateMultiplier((DifficultyMode)GameManager.CurrentDifficulty);
            Invoke(nameof(Respawn), respawnDelay); // 고급 몬스터는 더 천천히 리스폰

            // === G2-04: 처치 카메라 이펙트 ===
            CombatCameraEffects.PlayKill();
        }

        private void Respawn()
        {
            _isDead = false;
            _currentHP = _maxHP;
            _aggroState = AggroState.Idle;
            _aggroTimer = 0f;
            _aggroTarget = null;
            _aggroAttacker = null;
            transform.position = _spawnPos;
            // 2026-09-11: 시체 비활성 원복 — GLB 대응으로 자식 렌더러/콜라이더/헤드UI 일괄 재활성
            // (기존 _renderer 단일 재활성은 GLB에서 null이라 무효)
            SetCorpseVisuals(true);
            ApplyColor();

            // 애니메이션: Idle
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _tier switch
            {
                MonsterTier.Beginner     => Color.green,
                MonsterTier.Intermediate => Color.yellow,
                MonsterTier.Advanced     => Color.red,
                _ => Color.white
            };
            Gizmos.DrawWireSphere(transform.position, _detectRange);

            // 어그로 범위 표시 (주황)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 10f);
        }

        // ===================== IAggroable 구현 =====================

        public AggroState CurrentAggroState => _aggroState;
        public GameObject AggroTarget => _aggroTarget;

        public bool IsInCombat => _aggroState == AggroState.Combat;

        /// <summary>MonsterType = _monsterId (예: "rabbit", "boar", "wolf")</summary>
        public string MonsterType => _monsterId;

        /// <summary>
        /// 어그로 대상 설정. MonsterAggroSystem.NotifyAttack에 의해 호출됨.
        /// 상태: Idle → Alert (3초 후 → Combat)
        /// </summary>
        public void SetAggroTarget(GameObject target)
        {
            if (target == null || _isDead) return;

            _aggroTarget = target;
            _aggroAttacker = target;

            // 상태 전이: Idle → Alert (이미 Alert/Combat이면 유지)
            if (_aggroState == AggroState.Idle || _aggroState == AggroState.Cooldown)
            {
                _aggroState = AggroState.Alert;
                _aggroTimer = 0f;
            }
        }

        /// <summary>어그로 해제. 대상 사망/이탈 시.</summary>
        public void ClearAggro()
        {
            if (_aggroState == AggroState.Idle) return;

            _aggroState = AggroState.Cooldown;
            _aggroTimer = 0f;
            _aggroTarget = null;
            _aggroAttacker = null;
        }

        /// <summary>어그로 타이머 업데이트 (상태 전이)</summary>
        public void UpdateAggroTimer(float deltaTime)
        {
            if (_aggroState == AggroState.Idle) return;

            _aggroTimer += deltaTime;

            switch (_aggroState)
            {
                case AggroState.Alert:
                    // Alert(3초) → Combat
                    if (_aggroTimer >= ALERT_DURATION)
                    {
                        _aggroState = AggroState.Combat;
                        _aggroTimer = 0f;
                    }
                    break;

                case AggroState.Combat:
                    // Combat 중 타겟 확인
                    if (_aggroTarget == null || IsTargetDead())
                    {
                        ClearAggro();
                    }
                    break;

                case AggroState.Cooldown:
                    // Cooldown(5초) → Idle
                    if (_aggroTimer >= COOLDOWN_DURATION)
                    {
                        _aggroState = AggroState.Idle;
                        _aggroTimer = 0f;
                        _aggroAttacker = null;
                    }
                    break;
            }
        }

        /// <summary>어그로 대상이 사망했는지 확인</summary>
        private bool IsTargetDead()
        {
            if (_aggroTarget == null) return true;
            var damageable = _aggroTarget.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsAlive) return true;
            return false;
        }

        /// <summary>
        /// 어그로 상태일 때의 행동 처리.
        /// 각 몬스터 타입별 특성 반영 (도망/돌진/추격)
        /// </summary>
        private void UpdateAggroBehavior()
        {
            if (_aggroTarget == null || _isDead)
            {
                ClearAggro();
                // Idle 애니메이션
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetStateImmediate(AnimationState.Idle);
                return;
            }

            // 어그로 대상이 사망했는지 확인
            if (IsTargetDead())
            {
                ClearAggro();
                // Idle 애니메이션
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetStateImmediate(AnimationState.Idle);
                return;
            }

            float dist = Vector3.Distance(transform.position, _aggroTarget.transform.position);

            // Alert 상태: 대상 방향 응시 (경계)
            if (_aggroState == AggroState.Alert)
            {
                Vector3 dir = (_aggroTarget.transform.position - transform.position).normalized;
                dir.y = 0;
                if (dir != Vector3.zero)
                    transform.rotation = Quaternion.LookRotation(dir);

                // 애니메이션: Idle (경계)
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetState(AnimationState.Idle);
                return;
            }

            // Combat 상태: 몬스터 타입별 행동
            if (_aggroState == AggroState.Combat)
            {
                switch (_monsterId)
                {
                    case "rabbit":
                    case "deer":
                    case "bat":
                    case "crow":
                    {
                        // 도망: 어그로 대상에게서 도망
                        Vector3 awayDir = (transform.position - _aggroTarget.transform.position).normalized;
                        awayDir.y = 0;
                        if (awayDir != Vector3.zero)
                            transform.rotation = Quaternion.LookRotation(awayDir);
                        Vector3 desiredPos = transform.position + awayDir * _speed * AGGRO_SPEED_MULT * Time.deltaTime;
                        HandleObstacleAvoidance(ref desiredPos);
                        transform.position = desiredPos;

                        // 애니메이션: Walk (도망)
                        if (_rigAnim != null) { _rigAnim.CurrentSpeed = _speed * AGGRO_SPEED_MULT; _rigAnim.SetState(AnimationState.Walk); }
                        break;
                    }
                    case "boar":
                    {
                        // 돌진: 어그로 대상에게 돌진
                        float range = _attackRange * 1.2f;
                        if (dist > range)
                        {
                            Vector3 dir = (_aggroTarget.transform.position - transform.position).normalized;
                            dir.y = 0;
                            if (dir != Vector3.zero)
                                transform.rotation = Quaternion.LookRotation(dir);
                            Vector3 desiredPos = transform.position + dir * (_speed * 1.5f * AGGRO_SPEED_MULT) * Time.deltaTime;
                            HandleObstacleAvoidance(ref desiredPos);
                            transform.position = desiredPos;

                            // 애니메이션: Walk (돌진)
                            if (_rigAnim != null) { _rigAnim.CurrentSpeed = _speed * 1.5f * AGGRO_SPEED_MULT; _rigAnim.SetState(AnimationState.Walk); }
                        }
                        else { TryAttackAggroTarget(); }
                        break;
                    }
                    case "wolf":
                    case "giant_rat":
                    case "poison_snake":
                    default:
                    {
                        // 추격 (늑대, 거대쥐, 독뱀 등 기본)
                        float range = _attackRange * 1.2f;
                        if (dist > range)
                        {
                            Vector3 dir = (_aggroTarget.transform.position - transform.position).normalized;
                            dir.y = 0;
                            if (dir != Vector3.zero)
                                transform.rotation = Quaternion.LookRotation(dir);
                            Vector3 desiredPos = transform.position + dir * _speed * AGGRO_SPEED_MULT * Time.deltaTime;
                            HandleObstacleAvoidance(ref desiredPos);
                            transform.position = desiredPos;

                            // 애니메이션: Walk (추격)
                            if (_rigAnim != null) { _rigAnim.CurrentSpeed = _speed * AGGRO_SPEED_MULT; _rigAnim.SetState(AnimationState.Walk); }
                        }
                        else { TryAttackAggroTarget(); }
                        break;
                    }
                }
            }
        }

        /// <summary>어그로 대상에게 공격 시도</summary>
        private void TryAttackAggroTarget()
        {
            if (Time.time - _lastAttackTime < _attackCooldown) return;
            _lastAttackTime = Time.time;

            // 애니메이션: Attack
            if (_rigAnim != null)
            {
                _rigAnim.SetState(AnimationState.Attack);
            }
            else
            {
                // 4족 몬스터: QuadrupedProceduralAnimation.RequestAttack() 시도
                var quadAnim = GetComponent<QuadrupedProceduralAnimation>();
                if (quadAnim != null)
                {
                    quadAnim.RequestAttack();
                }
                else
                {
                    Debug.LogWarning($"[AnimalAI] ⚠️ {_monsterId}: RigAnimationController/QuadrupedProceduralAnimation 없음 → 공격 애니메이션 미출력 (어그로)");
                }
            }

            // Neural Animation: Combat 정책으로 전환
            _neuralAnim?.SwitchPolicy(NeuralAnimationController.PolicyType.Combat);

            // 🐉 MonsterSkillSystem: 스킬이 있는 몬스터는 스킬 우선 사용
            if (MonsterSkillSystem.Instance != null && _aggroTarget != null)
            {
                var skills = MonsterSkillSystem.Instance.GetSkillsForAI(this);
                if (skills.Length > 0)
                {
                    // 랜덤 스킬 선택 (50% 확률)
                    if (Random.value < 0.5f)
                    {
                        MonsterSkillSystem.MonsterSkillData skillData = skills[Random.Range(0, skills.Length)];
                        if (MonsterSkillSystem.Instance.ExecuteSkill(this, skillData, _aggroTarget))
                            return;
                    }
                }
            }

            // IDamageable이면 데미지 (기본 공격)
            if (_aggroTarget != null)
            {
                var dmg = _aggroTarget.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                {
                    Vector3 dir = (_aggroTarget.transform.position - transform.position).normalized;
                    dmg.TakeDamage(_attackDamage, dir, "melee");
                    Debug.Log($"{_monsterId}가(이) {_aggroTarget.name}에게 {_attackDamage} 데미지 (어그로)!");
                }
            }
        }

        private void OnDestroy()
        {
            // MonsterAggroSystem 등록 해제
            if (MonsterAggroSystem.Instance != null)
            {
                MonsterAggroSystem.Instance.UnregisterMonster(this);
            }
        }
    }
}