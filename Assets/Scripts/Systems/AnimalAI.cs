using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

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
            if (_level <= 0) return;

            MonsterLevelManager mgr = MonsterLevelManager.Instance;
            if (mgr != null)
            {
                _maxHP = mgr.GetMonsterHP(_level, _tier);
                float dmg = mgr.GetMonsterDamage(_level);
                _attackDamage = Mathf.Max(1, Mathf.RoundToInt(dmg));

                // MonsterLevelLabel 업데이트
                UI.MonsterLevelLabel label = GetComponent<UI.MonsterLevelLabel>();
                if (label != null)
                    label.SetLevel(_level);
            }
        }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();
        }

        private void Start()
        {
            // MonsterDatabase에서 데이터 로드
            ApplyMonsterDefinition();

            _currentHP = _maxHP;
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
            float hpMult = DifficultyManager.GetHpMultiplier((ProjectName.Core.DifficultyMode)GameManager.CurrentDifficulty);
            float dmgMult = DifficultyManager.GetDamageMultiplier((ProjectName.Core.DifficultyMode)GameManager.CurrentDifficulty);
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
            _rareDropChance = Mathf.Clamp01(_rareDropChance * DifficultyManager.GetDropRateMultiplier((ProjectName.Core.DifficultyMode)GameManager.CurrentDifficulty));
        }

        private void Update()
        {
            if (_isDead || _player == null) return;

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
            if (dist > _detectRange) return;

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
            if (dist > _detectRange) return;

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

                // 늪지악어: 접근 시 은신 효과
                if (_monsterId == "swamp_croc" && dist < _detectRange * 0.5f)
                {
                    // 더 빠르게 돌진
                    desiredPos = transform.position + dir * _speed * 1.5f * Time.deltaTime;
                    HandleObstacleAvoidance(ref desiredPos);
                    transform.position = desiredPos;
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
            if (dist > _detectRange) return;

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
            }
            else
            {
                TryAttack();
            }
        }

        /// <summary>
        /// 근처 같은 종류 몬스터 호출
        /// </summary>
        private void CallNearbyMonsters()
        {
            var monsters = FindObjectsByType<AnimalAI>();
            foreach (var m in monsters)
            {
                if (m == this || m._monsterId != _monsterId || m._isDead) continue;
                float d = Vector3.Distance(transform.position, m.transform.position);
                if (d < _packCallRange)
                {
                    // 이미 감지 범위 안이면 자동 추격
                }
            }
        }

        private void TryAttack()
        {
            if (Time.time - _lastAttackTime < _attackCooldown) return;
            _lastAttackTime = Time.time;

            // 플레이어에게 실제 데미지
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.TakeDamage(_attackDamage);
                Debug.Log($"{MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId}가(이) 플레이어에게 {_attackDamage} 데미지!");
                // 적용 디버프: 슬로우넝 (이동 속도 감소)
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.AddBuff("Slowness", 0.5f, 5f); // 이동 속도 0.5 감소, 5초 지속
                    Debug.Log("[AnimalAI] 🐌 플레이어에게 슬로우넝 디버프 적용 (속도 -0.5, 5초)");
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
            CombatCameraEffects.PlayHit();

            // === MonsterAggroSystem: 공격 통보 → 주변 합세 ===
            if (MonsterAggroSystem.Instance != null)
            {
                // 공격자 찾기: hitDirection 반대 방향으로 추정 (또는 player)
                GameObject attacker = null;
                if (_player != null) attacker = _player.gameObject;
                MonsterAggroSystem.Instance.NotifyAttack(gameObject, attacker);
            }

            // === G2-05: CombatVFXController ===
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
                HitVFX.PlayHitFlash(_renderer);
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

                /// <summary>
        /// Auto-hunt by Hunter guard — returns drops directly and kills animal.
        /// Used by HuntingMission.
        /// </summary>
        public bool TryAutoHunt(out System.Collections.Generic.List<(PlayerInventory.ItemData item, int count)> drops)
        {
            drops = new System.Collections.Generic.List<(PlayerInventory.ItemData, int)>();
            if (_isDead) return false;

            _isDead = true;

            // Generate drops (same logic as Die() but without LootBasket)
            int meatCount = _minMeat == _maxMeat ? _minMeat : Random.Range(_minMeat, _maxMeat + 1);
            if (_meatDrop != null && meatCount > 0)
                drops.Add((_meatDrop, meatCount));
            if (_materialDrop != null && _materialCount > 0)
                drops.Add((_materialDrop, _materialCount));
            if (_rareDrop != null && Random.value < _rareDropChance)
                drops.Add((_rareDrop, 1));

            // Hide visual
            if (_collider != null) _collider.enabled = false;
            if (_renderer != null) _renderer.enabled = false;

            // Schedule respawn
            CancelInvoke(nameof(Respawn));
            Invoke(nameof(Respawn), 10f + (int)_tier * 5f);

            return drops.Count > 0;
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // 어그로 해제
            ClearAggro();

            // 플레이어 경험치/킬 카운트 (선택적)
            Debug.Log($"{MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId} 사망!");
            // 경험치 획득
            int exp = 0;
            switch (_tier)
            {
                case MonsterTier.Beginner: exp = Random.Range(10, 31); break;
                case MonsterTier.Intermediate: exp = Random.Range(50, 101); break;
                case MonsterTier.Advanced: exp = Random.Range(150, 301); break;
            }
            PlayerStats.Instance.AddEXP(exp);

            // === G2-05: 사망 VFX ===
            CombatVFXController.SpawnBloodSplatter(transform.position, Vector3.up);

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
            DropTable dropTable = DropTableManager.Instance.GetMonsterTable(_tier);

            // [5.3.5] 레벨 기반 희귀 드랍률 보정
            float levelDropBonus = 0f;
            if (MonsterLevelManager.Instance != null)
                levelDropBonus = MonsterLevelManager.Instance.GetDropRateBonus(_level);

            if (dropTable != null)
            {
                dropTable.ApplyToBasket(basket, levelDropBonus);
            }
            else
            {
                // Fallback to default drops (original logic)
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
            }

            // 시체 처리
            if (_collider != null) _collider.enabled = false;
            if (_renderer != null) _renderer.enabled = false;

            // 리스폰
            float respawnDelay = 10f + (int)_tier * 5f;
            // C20-02: 난이도별 리스폰 속도 배율 (Easy: 빠름, Hard: 느림)
            respawnDelay *= DifficultyManager.GetRespawnRateMultiplier((ProjectName.Core.DifficultyMode)GameManager.CurrentDifficulty);
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
            if (_collider != null) _collider.enabled = true;
            if (_renderer != null) _renderer.enabled = true;
            ApplyColor();
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
                return;
            }

            // 어그로 대상이 사망했는지 확인
            if (IsTargetDead())
            {
                ClearAggro();
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
                        }
                        else { TryAttackAggroTarget(); }
                        break;
                    }
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

            // IDamageable이면 데미지
            if (_aggroTarget != null)
            {
                var dmg = _aggroTarget.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                {
                    Vector3 dir = (_aggroTarget.transform.position - transform.position).normalized;
                    dmg.TakeDamage(_attackDamage, dir);
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