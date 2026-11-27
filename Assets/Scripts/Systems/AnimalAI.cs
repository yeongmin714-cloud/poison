// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 618
using UnityEngine;
using System.Collections; // [Phase L] 사망 시 다운 모션 + 지연 파괴 코루틴용
using ProjectName.Core;
using ProjectName.Core.Data;
// [2026-09-14(50차)] 2족 절차 애니 연결 — IVelocityProvider/ProceduralAnimationController 참조용
using ProjectName.Systems.Animation.Procedural;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 몬스터 AI — GAME_DATA.md v2.0 기반 24종 몬스터 지원.
    /// 토끼(도망), 멧돼지(돌진), 늑대(추격) 등 기본 행동 패턴 + 티어별 난이도.
    /// </summary>
    // [2026-09-14(50차)] IVelocityProvider 구현 — 2족 몬스터의 ProceduralAnimationController에
    // AnimalAI 실 이동 속도를 공급한다(UpdateBipedLink에서 SetVelocityProvider(this)로 연결).
    public class AnimalAI : MonoBehaviour, IDamageable, IAggroable, IVelocityProvider
    {
        [Header("Monster Identity")]
        [SerializeField] private string _monsterId = "rabbit";  // MonsterDatabase 키

        [Header("Stats (auto-set by tier)")]
        [SerializeField] private float _maxHP = 10f;
        [SerializeField] private float _speed = 5f;
        [SerializeField] private float _detectRange = 10f;
        [SerializeField] private float _attackRange = 1.6f;   // [무번호 20-H1] 2.0→1.6 축소 — 링/판정/추격(1.2×) 동시 일치
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
        private Quaternion _spawnRot;   // [Phase L] 리스폰 시 회전 원복용
        private bool _isDead = false;
        private bool _deathRoutineStarted; // [Phase L] 다운 시퀀스 코루틴 재진입 가드
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

        // [2026-09-14(49차)] 4족 절차 애니메이션 연결 — AI 이동 속도를 애니에 피드(다리 미동작 수리).
        // ModelAnimatorAssigner.SetupQuadruped가 몬스터에 늦게 부착할 수 있어 지연 탐색 재시도.
        private QuadrupedProceduralAnimation _quadAnim;
        private bool _quadConfigured;          // 연결+프로필 적용 완료 플래그
        private float _quadSearchElapsed;      // 탐색 경과 시간
        private const float QUAD_SEARCH_TIMEOUT = 3f; // 탐색 포기 시간(초) — 2족 몬스터 등

        // [2026-09-14(50차)] 2족 절차 애니메이션 연결 — AI 이동 속도를 ProceduralAnimationController에 피드.
        // ModelAnimatorAssigner.SetupBiped가 AnimalAI.Start 이후에 늦게 부착할 수 있어 지연 탐색 재시도
        // (4족 _quadAnim 지연 탐색 패턴과 동일). 탐색 성공/포기 후 1회만 시도.
        private ProceduralAnimationController _bipedAnim;
        private bool _bipedConfigured;         // 연결+프로필 적용 완료 플래그
        private float _bipedSearchElapsed;     // 탐색 경과 시간

        // [2026-09-14(50차)] IVelocityProvider 출력 값 — 실 이동 방향×속력(FeedQuadrupedSpeed 공용 경로에서 갱신)
        private Vector3 _currentAIVelocity;
        private float _currentAISpeed;

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

        // ===================== IVelocityProvider 구현 [2026-09-14(50차)] =====================
        /// <summary>현재 이동 속도 벡터(실 이동 방향×속력) — 절차 보행 위상/조향 판정에 사용.</summary>
        public Vector3 CurrentVelocity => _currentAIVelocity;
        /// <summary>현재 실제 이동 속력(m/s) — 보행/달리기 블렌드 판정에 사용.</summary>
        public float CurrentSpeed => _currentAISpeed;
        /// <summary>몬스터는 항상 지면 위를 이동하므로 true 고정(공중 상태 없음).</summary>
        public bool IsGrounded => true;

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
            // [2026-09-22 뉴럴 애니 제거] NeuralAnimationController 부착 경로 퇴역 — 절차/클립 경로 단일화.
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
            _spawnRot = transform.rotation; // [Phase L] 리스폰/다운 복구용

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
                    _attackRange = 1.6f;   // [무번호 20-H1] 2.0→1.6 축소
                    _attackCooldown = 1.5f;
                    _bodyScale = Random.Range(0.6f, 0.9f);
                    break;
                case MonsterTier.Intermediate:
                    _detectRange = 14f;
                    _attackRange = 2f;   // [무번호 20-H1] 2.5→2.0 축소
                    _attackCooldown = 1.2f;
                    _bodyScale = Random.Range(0.9f, 1.2f);
                    break;
                case MonsterTier.Advanced:
                    _detectRange = 18f;
                    _attackRange = 2.4f;   // [무번호 20-H1] 3.0→2.4 축소
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

        // [2026-09-14(49차)] 4족 절차 애니 연결 — 지연 탐색 + AI 모드 설정 + 종별 보행 프로필 적용.
        // ModelAnimatorAssigner.SetupQuadruped가 AnimalAI.Start 이후에 QuadrupedProceduralAnimation을
        // 부착할 수 있어, 발견 시까지 최대 QUAD_SEARCH_TIMEOUT초간 재시도한다.
        private void UpdateQuadrupedLink()
        {
            if (_quadConfigured) return;

            if (_quadAnim == null)
            {
                _quadAnim = GetComponent<QuadrupedProceduralAnimation>();
                if (_quadAnim == null)
                {
                    _quadSearchElapsed += Time.deltaTime;
                    if (_quadSearchElapsed < QUAD_SEARCH_TIMEOUT) return;
                    _quadConfigured = true; // 4족 애니 미부착(2족 등) — 탐색 포기
                    return;
                }
            }

            // 4족 발견 → AI 구동 모드 진입 + 종별 보행 프로필 적용 (이동/회전은 AnimalAI 담당)
            _quadAnim.SetAiDriven(true);
            _quadAnim.ApplyMonsterProfile(_monsterId);
            _quadConfigured = true;
            Debug.Log($"[AnimalAI] 4족 절차 애니 연결 완료: {_monsterId} (AI 구동 모드 + 보행 프로필)");
        }

        // [2026-09-14(50차)] 2족 절차 애니 대상 판정 — MonsterDef.isQuadruped == false인 실제 2족형 6종.
        // (독뱀/박쥐/까마귀/슬라임/숲정령 등 나머지 비4족은 특수형·비2족으로 제외)
        private bool IsBipedMonster()
        {
            MonsterDef def = MonsterDatabase.Get(_monsterId);
            if (def != null && def.isQuadruped) return false; // DB상 4족이면 제외

            switch (_monsterId)
            {
                case "stone_golem":     // 돌골렘
                case "wild_troll":      // 야생트롤
                case "ogre":            // 오우거
                case "banshee":         // 밴시
                case "minotaur":        // 미노타우로스
                case "shadow_assassin": // 그림자암살자
                    return true;
                default:
                    return false;
            }
        }

        // [2026-09-14(50차)] 2족 절차 애니 연결 — 지연 탐색 + IVelocityProvider 연결 + 종별 보행 프로필 적용.
        // ModelAnimatorAssigner.SetupBiped가 AnimalAI.Start 이후에 ProceduralAnimationController를
        // 부착할 수 있어, 발견 시까지 최대 QUAD_SEARCH_TIMEOUT초간 재시도한다(UpdateQuadrupedLink와 동일 패턴).
        private void UpdateBipedLink()
        {
            if (_bipedConfigured) return;

            // 2족 대상이 아니면 1회 판정 후 영구 스킵
            if (!IsBipedMonster())
            {
                _bipedConfigured = true;
                return;
            }

            if (_bipedAnim == null)
            {
                _bipedAnim = GetComponent<ProceduralAnimationController>();
                if (_bipedAnim == null)
                {
                    _bipedSearchElapsed += Time.deltaTime;
                    if (_bipedSearchElapsed < QUAD_SEARCH_TIMEOUT) return;
                    _bipedConfigured = true; // 절차 애니 미부착 — 탐색 포기(AI 이동만 유지)
                    Debug.Log($"[AnimalAI] 2족 절차 애니 미부착 — 속도 피드 생략: {_monsterId}");
                    return;
                }
            }

            // 2족 절차 컨트롤러 발견 → AI 속도 공급자 연결 + 종별 보행 프로필 적용.
            // 실 이동/회전은 AnimalAI가 transform으로 담당하며, 컨트롤러는 공급 속도를 읽어
            // 보행 위상(다리 사이클)·lean만 구동한다(provider 연결 시 컨트롤러 자체 이동/중력은 스킵됨).
            _bipedAnim.SetVelocityProvider(this);
            _bipedAnim.ApplyMonsterProfile(_monsterId);
            _bipedConfigured = true;
            Debug.Log($"[AnimalAI] 2족 절차 애니 연결 완료: {_monsterId} (IVelocityProvider 연결 + 보행 프로필)");

            // 이중 구동 판별용 모니터 로그 — RigAnimationController도 부착돼 있으면 두 경로 병존
            if (_rigAnim != null)
                Debug.Log($"[AnimalAI] {_monsterId}: RigAnimationController 병존 — 기존 SetState 경로 유지 + 절차 보행 병행");
        }

        /// <summary>
        /// [2026-09-14(49차)] 4족 절차 애니메이션에 실제 이동 속도 피드.
        /// AnimalAI가 transform을 직접 이동하므로 이동 크기만 전달하고, 방향 회전은 AnimalAI가
        /// 이미 처리하므로 애니 컨트롤러의 회전 중복은 없다(SetMovementSpeed는 속도만 설정).
        /// </summary>
        private void FeedQuadrupedSpeed(float speed)
        {
            // [2026-09-14(50차)] AI 실속도 갱신(공용 경로) — 모든 이동/정지 루틴이 이 메서드를 거치므로
            // 2족 절차 애니용 IVelocityProvider 출력을 여기서 함께 갱신한다. 이동 방향은 이동 루틴이
            // LookRotation으로 정렬한 직후 호출되므로 transform.forward와 일치.
            // 4족 피드 로직(SetMovementSpeed)은 변경 없음 — 별개 경로 유지.
            _currentAISpeed = Mathf.Max(0f, speed);
            _currentAIVelocity = _currentAISpeed > 0.001f ? transform.forward * _currentAISpeed : Vector3.zero;

            if (_quadAnim != null && _quadConfigured)
                _quadAnim.SetMovementSpeed(speed);
        }

        private void Update()
        {
            // [P14] 실내 활성 시 몬스터 AI 완전 정지 — 어그로/추격이 실내 씬으로 유입되는 것 차단
            if (ProjectName.Core.UITransitionState.IndoorActive)
            {
                _currentAISpeed = 0f;
                _currentAIVelocity = Vector3.zero;
                if (_quadAnim != null && _quadConfigured) _quadAnim.SetMovementSpeed(0f);
                return;
            }

            // [무번호5/P3] Time Paradox 스테이시스 게이트 — PERFECT 패링 타임 스톱 중 이동/어그로 스킵(
            //   IndoorActive 게이트 선례 동일 패턴). 상태 소유자=TimeWarpIdentity(에셋) — 읽기 전용 의존.
            if (ParadoxTimeStop.IsActiveFor(this))
            {
                _currentAISpeed = 0f;
                _currentAIVelocity = Vector3.zero;
                if (_quadAnim != null && _quadConfigured) _quadAnim.SetMovementSpeed(0f);
                return;
            }

            // [2026-09-14(49차)] 4족 절차 애니 연결 시도(지연 부착 대응) — 최초 1회 성공 후 스킵
            UpdateQuadrupedLink();

            // [2026-09-14(50차)] 2족 절차 애니 연결 시도(지연 부착 대응) — IVelocityProvider 연결 + 보행 프로필
            UpdateBipedLink();

            if (_isDead || _player == null)
            {
                // 사망 또는 플레이어 없음 → Idle
                // [2026-09-14(49차)] 정지 상태 — 4족 애니 속도 0 피드(다리 정지)
                if (_quadAnim != null && _quadConfigured) _quadAnim.SetMovementSpeed(0f);
                // [2026-09-14(50차)] 2족 절차 애니 정지 — 속도 공급 값 0 클리어(보행 위상 정지)
                _currentAISpeed = 0f;
                _currentAIVelocity = Vector3.zero;
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

            // [TEST28-69차] 추격/공격 대상 — 어그로 대상(병사)이 생존하면 그 대상 우선, 없으면 플레이어.
            //   기존은 dist/방향이 전부 _player 기준이라 병사가 옆에 있어도 몬스터가 병사를 공격하지 않았다.
            Transform chaseTarget = _player;
            if (_aggroTarget != null && _aggroTarget.activeInHierarchy)
                chaseTarget = _aggroTarget.transform;
            if (chaseTarget == null)
            {
                _currentAIVelocity = Vector3.zero;
                return;
            }
            float dist = Vector3.Distance(transform.position, chaseTarget.position);

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
                // [2026-09-14(49차)] 정지 — 4족 애니 속도 0 피드
                FeedQuadrupedSpeed(0f);
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

                    // [2026-09-14(49차)] 도망 이동 속도(_speed)를 4족 애니에 피드 — 다리 위상 구동
                    FeedQuadrupedSpeed(_speed);

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

                        // [2026-09-14(49차)] 돌진 속도(_speed×1.5)를 4족 애니에 피드
                        FeedQuadrupedSpeed(_speed * 1.5f);

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

                        // [2026-09-14(49차)] 추격 속도(_speed)를 4족 애니에 피드
                        FeedQuadrupedSpeed(_speed);

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
                // [2026-09-14(49차)] 정지 — 4족 애니 속도 0 피드
                FeedQuadrupedSpeed(0f);
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

                // [2026-09-14(49차)] 추격 실속도(_speed×speedMult)를 4족 애니에 피드
                FeedQuadrupedSpeed(_speed * speedMult);

                // 애니메이션: Walk
                if (_rigAnim != null) { _rigAnim.CurrentSpeed = _speed * speedMult; _rigAnim.SetState(AnimationState.Walk); }

                // 늪지악어: 접근 시 은신 효과
                if (_monsterId == "swamp_croc" && dist < _detectRange * 0.5f)
                {
                    // 더 빠르게 돌진
                    desiredPos = transform.position + dir * _speed * 1.5f * Time.deltaTime;
                    HandleObstacleAvoidance(ref desiredPos);
                    transform.position = desiredPos;

                    // [2026-09-14(49차)] 악어 돌진 속도(_speed×1.5)로 피드 갱신
                    FeedQuadrupedSpeed(_speed * 1.5f);

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
                // [2026-09-14(49차)] 정지 — 4족 애니 속도 0 피드
                FeedQuadrupedSpeed(0f);
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

                // [2026-09-14(49차)] 추격 실속도(_speed×speedMult)를 4족 애니에 피드
                FeedQuadrupedSpeed(_speed * speedMult);

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
            // [2026-09-14(49차)] 공격 중 정지 상태 — 4족 애니 속도 0 피드(다리 정지 후 공격 모션)
            FeedQuadrupedSpeed(0f);

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

            // === [95차 F5] 예고→타격 2페이즈 — 즉발 TakeDamage 대신 텔레그래프 진입 ===
            // 공격 대상 결정: 어그로 대상(아군 병사 등) 우선, 없으면 플레이어 (기존 결정 규칙 무변경).
            // 데미지 판정/적용은 예고 0.5s 종료 후 거리 재확인을 거쳐 ConcludeMeleeTelegraph에서 수행.
            IDamageable aggroDamageable = GetAliveAggroDamageable();
            if (aggroDamageable != null && _aggroTarget != null)
            {
                // 어그로 대상(병사 등) 근접 공격 — 예고 후 IDamageable.TakeDamage(float, Vector3, string) 오버로드 사용
                BeginMeleeTelegraph(aggroDamageable);
            }
            else if (PlayerHealth.Instance != null)
            {
                // 기존 경로: 플레이어에게 실제 데미지 (기본 공격) — 예고 후 기존 TakeDamage 시그니처 그대로
                BeginMeleeTelegraph(null);
            }
            else
            {
                Debug.Log($"{_monsterId} attacks for {_attackDamage} damage!");
            }
        }

        // ===================== [95차 F5] 적 공격 텔레그래프 (예고→타격 2페이즈) =====================
        // 공격 조건 충족(사정거리/쿨다운) 시 즉발 TakeDamage 대신 0.5s 예고:
        //   ①선동작 = 절차 스케일 펄스 1.0→1.12→1.0 (애니 에셋/클립 불변 — transform.localScale 펄스만)
        //   ②발밑 경고 인디케이터 = 붉은 반투명 링 쿼드 펄스 (월드 스페이스 — BlobShadow 쿼드 스폰 선례 재사용)
        // 예고 종료 시 거리 재확인 — 이탈 시 공격 공백(빈 스윙), 사정거리 유지 시 기존 TakeDamage 시그니처 그대로.
        // 쿨다운/데미지 밸런스 무변경 — 예고 페이즈만 추가. 상태는 전부 인스턴스 필드(정적 상태 금지 — 다중 인스턴스 안전).

        // [156차 P5] 이계의 부적인 — 보유 시 +0.1s(런 외부 0 무영향). const→static 프로퍼티(사용처 L858/867/876 시그니처 무변경).
        private static float TelegraphWindupSeconds => 0.5f + AfterlifeCardSystem.TelegraphBonusSeconds;    // 예고 페이즈 길이 (패링 창 0.45s 커버)
        private const float TelegraphScalePulsePeak = 1.12f;  // 선동작 스케일 펄스 첨두치 (1.0→1.12→1.0)

        private bool _telegraphActive;             // 텔레그래프 상태 머신 플래그 (인스턴스)
        private Coroutine _telegraphRoutine;       // 진행 중 예고 코루틴
        private GameObject _telegraphIndicator;    // 발밑 경고 인디케이터(붉은 링 쿼드)
        private Vector3 _telegraphArcDir = Vector3.forward;   // [169-P6] 호 텔레그래프 방향(예고 시작 시점 타겟 방향 고정 — 회피 가능 설계)
        private Vector3 _telegraphBaseScale = Vector3.one; // 펄스 복원용 기저 스케일 (스폰 스케일 보존)
        private IDamageable _telegraphAggroTarget; // 예고 중 확정된 어그로 대상 (null이면 플레이어 경로)

        // ===================== [무번호 28-M1] 몬스터 충전 글로우 (예고 windup 발광) =====================
        // 예고 페이즈 동안 몸이 위험 적색으로 달아오르는 충전 글로우. 발광 부여 패턴은
        // ShockwaveRingFX.ApplyEmissionBoost(25-BL1) 선례 준용(EnableKeyword+SetColor, GI 비기년재).
        // GLB 몬스터는 루트 _renderer가 null이므로 GetComponentsInChildren<Renderer>(true) 전체 순회로 커버 —
        // Trail/ParticleSystem/Line 이펙트 렌더러는 스킵(53차 측정 필터 선례). base color/SetBodyColor(46/47차
        // 몸색) 계약 무손상 — emission만 조작. 색 단일 소스 FXPalette.MonsterDanger(U-1), 강도는
        // FXMotion.Progress(U-3) — 결정론(진행 입력만 사용).

        /// <summary>[무번호 28-M1] 충전 글로우 피크 강도 — 예고 완료 순간(t=1) 발광 배율.</summary>
        private const float ChargeGlowPeakIntensity = 1.6f;

        /// <summary>[무번호 28-M1] 충전 글로우 경고 1회 가드 — 동일 키 재경고 억제(스팸 방지 관행).</summary>
        private static readonly System.Collections.Generic.HashSet<string> _chargeGlowWarnedKeys = new System.Collections.Generic.HashSet<string>();

        // [무번호 29-M1-2] 충전 림 오버레이(위험 임박 fill≥0.75) — FxRimOverlay 소유 인스턴스.
        private GameObject _chargeRim;

        /// <summary>경고 1회성 가드 — 동일 키 최초 1회만 로그(스팸 방지).</summary>
        private static void WarnOnce(string key, string message)
        {
            if (!_chargeGlowWarnedKeys.Add(key)) return;
            Debug.LogWarning(message);
        }

        /// <summary>
        /// [무번호 28-M1] 충전 글로우 갱신 — 예고 windup 진행도 t(0→1)에 비례해 몸 전체 렌더러에
        /// 위험 적색(FXPalette.MonsterDanger) 발광을 부여한다. 강도 = FXMotion.Progress(t) × 피크 1.6.
        /// _EMISSION 미지원 셰이더는 폴백 스킵 + 씬 키 WarnOnce 1회.
        /// </summary>
        private void UpdateChargeGlow(float t)
        {
            float intensity = FXMotion.Progress(t) * ChargeGlowPeakIntensity;
            Color danger = FXPalette.MonsterDanger;
            Color emission = new Color(danger.r * intensity, danger.g * intensity, danger.b * intensity, 1f);
            string sceneKey = $"charge-glow-missing-emission:{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
            foreach (var rend in GetComponentsInChildren<Renderer>(true))
            {
                if (rend is TrailRenderer || rend is ParticleSystemRenderer || rend is LineRenderer)
                    continue; // 이펙트 렌더러 스킵 — 53차 측정 필터 선례
                if (rend.sharedMaterial == null) continue;
                var mat = rend.material;   // 인스턴스 머티리얼 — L300-302 .material 패턴 기존 존재
                if (mat == null || !mat.HasProperty("_EmissionColor"))
                {
                    WarnOnce(sceneKey, $"[AnimalAI] [무번호 28-M1] 충전 글로우 미지원 셰이더 폴백 스킵 — _EmissionColor 프로퍼티 부재({(mat != null ? mat.shader.name : "null")})");
                    continue;
                }
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }

            // [무번호 29-M1-2] 충전 림 — 위험 임박(fill≥0.75) 프레넬 림 강화(D-3 오버레이).
            //   등 위계: AfterlifeEnemy 동거(정예)=주황(ElitAura), 일반=위험 적색(MonsterDanger).
            //   림 셰이더 미지원 환경 = 기존 emissive만(폴백 계약 — 위 루프 무손상).
            float fill = FXMotion.Progress(t);
            if (fill >= 0.75f)
            {
                bool elite = TryGetComponent<AfterlifeEnemy>(out _);
                Color rimCol = elite ? FXPalette.ElitAura : FXPalette.MonsterDanger;
                if (_chargeRim == null)
                {
                    var rimSmr = GetComponentInChildren<SkinnedMeshRenderer>();
                    if (rimSmr != null) _chargeRim = FxRimOverlay.AttachRim(rimSmr, rimCol, 3f, 1.2f);
                    if (_chargeRim == null)
                        WarnOnce($"charge-rim-fallback:{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}",
                            "[AnimalAI] [무번호 29-M1-2] 충전 림 폴백 — 림 셰이더 부재/SMR 부재, 기존 emissive만 유지");
                }
                if (_chargeRim != null)
                {
                    FxRimOverlay.SetRimColor(_chargeRim, rimCol);
                    FxRimOverlay.SetRimIntensity(_chargeRim, 0.8f + 1.2f * (fill - 0.75f) / 0.25f);   // 임박 강화
                }
            }
            else if (_chargeRim != null)
            {
                FxRimOverlay.Release(_chargeRim); _chargeRim = null;
            }
        }

        /// <summary>
        /// [무번호 28-M1] 충전 글로우 해제 — 각 인스턴스 머티리얼의 _EmissionColor를 블랙으로 복원
        /// (EmissiveIsBlack GI 비기년재 — 25-BL1 주석 선례). base color 무손상. 멱등(다중 경로 호출 안전).
        /// </summary>
        private void ReleaseChargeGlow()
        {
            foreach (var rend in GetComponentsInChildren<Renderer>(true))
            {
                if (rend is TrailRenderer || rend is ParticleSystemRenderer || rend is LineRenderer)
                    continue;
                if (rend.sharedMaterial == null) continue;
                var mat = rend.material;
                if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
                mat.SetColor("_EmissionColor", Color.black);
            }
            if (_chargeRim != null) { FxRimOverlay.Release(_chargeRim); _chargeRim = null; }   // [무번호 29-M1-2] 림 해제(멱등)
        }

        /// <summary>[무번호 29-M2-1] 히트 림 플래시 — 피격 순간 몸 림 0.12s(D-3 오버레이·별도 경로).
        /// EA1 몸색 혼합과 병행, 기존 히트컬러 계약 무손상. 림 불가 환경=무영향 폴백. 결정론(Random 0건).</summary>
        public void PlayHitRimFlash()
        {
            if (!Application.isPlaying) return;
            var rimSmr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (rimSmr == null) return;
            var rim = FxRimOverlay.AttachRim(rimSmr, FXPalette.MonsterDanger, 2.5f, 1.6f);
            if (rim == null) return;   // 폴백 — 기존 히트 경로만
            StartCoroutine(ReleaseHitRimAfter(rim, 0.12f));
        }

        private System.Collections.IEnumerator ReleaseHitRimAfter(GameObject rim, float seconds)
        {
            yield return new UnityEngine.WaitForSeconds(seconds);
            FxRimOverlay.Release(rim);
        }

        /// <summary>
        /// [무번호 28-P3] 공격자 반응 플래시 — 패링 성공 순간 공격자 몸 백색 반전 0.1s(PlayerCombat Perfect 배선).
        /// GetComponentsInChildren&lt;Renderer&gt;(true) 전부 순회(Trail/Particle/Line 스킵 — M1 필터 선례),
        /// EnableKeyword("_EMISSION") + _EmissionColor = Color.white × 1.8f → 0.1s(unscaled) → black 원복.
        /// M1 해제 상태(black)와 동일 — 상충 없음; windup 재시작 시 UpdateChargeGlow가 매 프레임 재개(자가 치유).
        /// </summary>
        public void PlayParryFlash()
        {
            if (!Application.isPlaying) return;   // EditMode 가드 — 코루틴 미개시(무해)
            StartCoroutine(PlayParryFlashRoutine());
        }

        /// <summary>[무번호 28-P3] PlayParryFlash 본체 코루틴 — white 1.8f 펄스 → black 원복(M1 인프라 재사용).</summary>
        private System.Collections.IEnumerator PlayParryFlashRoutine()
        {
            // [무번호 29-P3-2] 공격자 백색 림 — 실루엣 반전 강조(D-3 오버레이).
            //   M1 충전 림 상충 가드 유지: 충전 림 활성 중이면 스킵(가드 계약 계승).
            GameObject whiteRim = null;
            if (_chargeRim == null)
            {
                var rimSmr = GetComponentInChildren<SkinnedMeshRenderer>();
                if (rimSmr != null) whiteRim = FxRimOverlay.AttachRim(rimSmr, Color.white, 2.2f, 1.8f);
            }
            var flashMats = new System.Collections.Generic.List<Material>();
            foreach (var rend in GetComponentsInChildren<Renderer>(true))
            {
                if (rend is TrailRenderer || rend is ParticleSystemRenderer || rend is LineRenderer)
                    continue;   // 이펙트 렌더러 스킵 — M1 필터 선례
                if (rend.sharedMaterial == null) continue;
                var mat = rend.material;
                if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white * 1.8f);
                flashMats.Add(mat);
            }
            yield return new WaitForSecondsRealtime(0.1f);
            if (whiteRim != null) FxRimOverlay.Release(whiteRim);   // [무번호 29-P3-2] 림 해제(누출 금지)
            foreach (var mat in flashMats)
            {
                if (mat == null) continue;
                mat.SetColor("_EmissionColor", Color.black);   // black 원복 — M1 해제 상태와 동일
            }
        }

        /// <summary>예고 페이즈 진입 — 텔레그래프 상태 진환(인디케이터 스폰/스케일 펄스 시작). 기존 쿨다운 게이트는 TryAttack 선두 유지.</summary>
        private void BeginMeleeTelegraph(IDamageable aggroTarget)
        {
            // 이중 진입 방지 — 진행 중 예고가 있으면 안전 정리 후 재시작 (다중 인스턴스 안전: 전부 인스턴스 상태)
            CancelMeleeTelegraph();

            _telegraphActive = true;
            _telegraphAggroTarget = aggroTarget;
            _telegraphBaseScale = transform.localScale;

            // ②발밑 경고 인디케이터 — [190차 제거] 몬스터 적색링(공격 텔레그래프 호) 폐지 — 사용자 지시
            //   "몬스터 적색링은 아직 안 없어진거 같던데". 스폰 삭제 — _telegraphIndicator 영구 null,
            //   발동 플래시(ConcludeMeleeTelegraph null 가드)·인디케이터 정리(null 가드) 모두 무영향.
            // [무번호 25-A3] 적색링 절반 축소 계약(T6)은 190차 제거 계약으로 갱신.
            // [169-P6] 호 판정 자체는 유지 — 방향은 예고 시작 시점 타겟 방향으로 고정 — windup 내 호 밖
            //   이동 시 빈 스윙(회피 가능) — 시각 없이 판정만 존속.
            Vector3 arcDir = transform.forward;
            if (_aggroTarget != null)
            {
                Vector3 d = _aggroTarget.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) arcDir = d.normalized;
            }
            _telegraphArcDir = arcDir;
            _telegraphIndicator = null;   // [190차] 적색링 제거 — 스폰 삭제(판정·윈드업 로직 무영향)

            _telegraphRoutine = StartCoroutine(MeleeTelegraphRoutine());
        }

        /// <summary>예고 코루틴 — 0.5s 스케일 펄스(선동작) 후 거리 재확인 타격. 예고 중 사망 시 안전 정리.</summary>
        private System.Collections.IEnumerator MeleeTelegraphRoutine()
        {
            float elapsed = 0f;
            while (elapsed < TelegraphWindupSeconds)
            {
                if (_isDead)          // 예고 중 사망 — 무타격 보장 후 정리
                {
                    CancelMeleeTelegraph();
                    yield break;
                }

                // ①선동작 — 절차 스케일 펄스 1.0→1.12→1.0 (사인 반파, 기저 스케일 배수 — 스폰 스케일 보존)
                float t = Mathf.Clamp01(elapsed / TelegraphWindupSeconds);
                UpdateChargeGlow(t);   // [무번호 28-M1] 충전 글로우 — 예고 진행에 비례해 위험 적색 발광 상승
                float pulse = 1f + (TelegraphScalePulsePeak - 1f) * Mathf.Sin(t * Mathf.PI);
                transform.localScale = _telegraphBaseScale * pulse;

                elapsed += Time.deltaTime;
                yield return null;
            }

            ConcludeMeleeTelegraph();
        }

        /// <summary>예고 종료 — 거리 재확인 후 타격(기존 TakeDamage 시그니처 그대로) 또는 공격 공백(빈 스윙).</summary>
        private void ConcludeMeleeTelegraph()
        {
            RestoreTelegraphVisuals();
            ReleaseChargeGlow();   // [무번호 28-M1] 해제 경로 ① — 루프 정상 종료 직후(공격 실행 지점)

            // [무번호 20-H3] 발동 순간 공격 방향 부채꼴 플래시 — 실제 스윙 방향(어그로 대상/플레이어)으로
            //   정직한 예고(B2 부채꼴·플래시 셰이더 자산 사용 배선 — 중복 구현 없음).
            if (_telegraphIndicator != null)
            {
                var telegraphComp = _telegraphIndicator.GetComponent<TelegraphArcIndicator>();
                if (telegraphComp != null)
                {
                    Vector3 flashDir = _telegraphAggroTarget != null && _aggroTarget != null
                        ? (Vector3)(_aggroTarget.transform.position - transform.position)
                        : (PlayerHealth.Instance != null ? (Vector3)(PlayerHealth.Instance.transform.position - transform.position) : transform.forward);
                    flashDir.y = 0f;
                    telegraphComp.FlashConclude(flashDir.sqrMagnitude > 0.0001f ? flashDir.normalized : transform.forward);
                }
            }

            IDamageable aggroTarget = _telegraphAggroTarget;
            _telegraphAggroTarget = null;

            if (aggroTarget != null)
            {
                // 어그로 대상 경로 — 재활성/생존/사정거리 재확인 후 기존 오버로드 그대로
                if (_aggroTarget != null && _aggroTarget.activeInHierarchy && aggroTarget.IsAlive)
                {
                    float aggroDist = Vector3.Distance(transform.position, _aggroTarget.transform.position);
                    if (aggroDist <= _attackRange && TelegraphArcIndicator.IsInFrontArc(transform.position, _telegraphArcDir, _aggroTarget.transform.position, _attackRange))   // [169-P6] 부채꼴 판정 — 표시=판정 정합
                    {
                        Vector3 hitDirection = (_aggroTarget.transform.position - transform.position).normalized;
                        aggroTarget.TakeDamage(_attackDamage, hitDirection, "melee");
                        // [무번호 21-L2] 스콜치 데칼 — 명중 지점(대상 발밑) 전투 흔적 1회. 빈 스윙은 미배치(판정 정직성).
                        try
                        {
                            if (DecalSpawner.Instance != null)
                                DecalSpawner.Instance.SpawnDecal("ScorchMark",
                                    new Vector3(_aggroTarget.transform.position.x, 0.02f, _aggroTarget.transform.position.z), Quaternion.identity);
                        }
                        catch (System.Exception) { }
                        // [무번호 22-S2/S3] 스윙 지면 파동(공격 방향 좁은 팬 3본)+불티 파편(5개) — 흔적에 생동감.
                        try
                        {
                            Vector3 hitGround = new Vector3(_aggroTarget.transform.position.x, 0.05f, _aggroTarget.transform.position.z);
                            ShockwaveRingFX.SpawnSwingSweep(hitGround, hitDirection, new Color(1f, 0.35f, 0.25f, 0.9f), 0.25f);
                            ShockwaveRingFX.SpawnEmberBurst(hitGround);
                            // [무번호 23-X3] 몬스터 스윙 아크 — 곡선 잔상(위스프 재사용, 적색 계열)
                            ShockwaveRingFX.SpawnParryArcWisps(new Vector3(hitGround.x, hitGround.y + 0.9f, hitGround.z),
                                hitDirection, new Color(1f, 0.35f, 0.25f, 0.9f), 0.25f);
                        }
                        catch (System.Exception) { }
                        Debug.Log($"{MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId}가(이) {_aggroTarget.name}에게 {_attackDamage} 데미지!");
                    }
                    else
                    {
                        Debug.Log($"[AnimalAI] {_monsterId} 빈 스윙 — 예고 중 대상 이탈 (거리 {aggroDist:F2} > 사정거리 {_attackRange:F2}) [95차 F5]");
                    }
                }
            }
            else if (PlayerHealth.Instance != null)
            {
                // 기존 경로: 플레이어 공격 — [95차 F5] 예고 종료 후 거리 재확인 (이탈 시 빈 스윙)
                // [80차] 플레이어 넉백 — 타격 방향 전달(공격자→플레이어 평면 방향). 기존 규약 유지.
                Vector3 playerHitDir = PlayerHealth.Instance.transform.position - transform.position;
                playerHitDir.y = 0f;
                float playerDist = playerHitDir.magnitude;
                if (playerDist <= _attackRange && TelegraphArcIndicator.IsInFrontArc(transform.position, _telegraphArcDir, PlayerHealth.Instance.transform.position, _attackRange))   // [169-P6] 부채꼴 판정
                {
                    Vector3 kbDir = playerHitDir.sqrMagnitude > 0.0001f ? playerHitDir.normalized : transform.forward;
                    // [159차 P4] 몬스터 타격 임팩트 VFX — 흡수점(플레이어 흉부)에서 발화.
                    //   ①히트스톱 0.04s(scale 0.10) — 타격 무게감(패링 0.07s 대비 약 0.6배)
                    //   ②지면 링 1겹(흡수점 y 0.05m, 붉은 톤 1.4m) — 몬스터 공격 시그니처 색(패링 금/청 대비)
                    //     [무번호 25-A4] SpawnImpactRing — HDR emissive ×2.0 + 깊이 페이드(_FadeLength 0.35) +
                    //     EliteColor 변주 계약(텔레그래프 위험도 2색 패밀리 승계). 46/47차 에셋 계약 무변경.
                    //   ③방향성 스파크(공격자→플레이어 방향) — 기존 ShockwaveRingFX API 재사용
                    if (Application.isPlaying)
                        HitStopManager.RequestHitStop(0.04f, 0.10f);
                    Vector3 impactP = PlayerHealth.Instance.transform.position + Vector3.up * 0.9f;
                    // [195차 P1] 지면 링(SpawnImpactRing 1.4m 적색) 호출 철거 — 사용자 지시 몬스터 링 시각 폐지.
                    //   개성 틴트(accentDef/accentBody)는 해당 링 전용 인자라 함께 걷어냄(틴트 헬퍼 정의는 시그니처 계약 보존).
                    //   ③방향성 스파크 이하 데칼/스윙/불티 따뜻한 색 히트 피드백은 무수정.
                    ShockwaveRingFX.SpawnDirectionalSpark(impactP, -kbDir, new Color(1f, 0.35f, 0.25f, 0.9f), 0.35f);
                    // [무번호 21-L2] 스콜치 데칼 — 명중 지점 지면 그을음 흔적 1회(빈 스윙은 미배치 — 판정 정직성).
                    try
                    {
                        if (DecalSpawner.Instance != null)
                            DecalSpawner.Instance.SpawnDecal("ScorchMark",
                                new Vector3(impactP.x, 0.02f, impactP.z), Quaternion.identity);
                    }
                    catch (System.Exception) { }
                    // [무번호 22-S2/S3] 스윙 지면 파동(공격 방향)+불티 파편 — 흔적에 생동감.
                    try
                    {
                        Vector3 hitGround = new Vector3(impactP.x, 0.05f, impactP.z);
                        ShockwaveRingFX.SpawnSwingSweep(hitGround, kbDir, new Color(1f, 0.35f, 0.25f, 0.9f), 0.25f);
                        ShockwaveRingFX.SpawnEmberBurst(hitGround);
                        // [무번호 23-X3] 몬스터 스윙 아크 — 곡선 잔상(위스프 재사용, 적색 계열)
                        ShockwaveRingFX.SpawnParryArcWisps(new Vector3(hitGround.x, hitGround.y + 0.9f, hitGround.z),
                            kbDir, new Color(1f, 0.35f, 0.25f, 0.9f), 0.25f);
                    }
                    catch (System.Exception) { }
                    PlayerHealth.Instance.TakeDamage(_attackDamage, kbDir, "melee");
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
                    // 공격 공백 — 빈 스윙 (데미지/쿨다운 계약 무손상, 타격만 보류)
                    // [159차 P4] 빈 스윙(회피 성공) 피드백 — 몬스터 발밑 희미한 흰 파동(0.9m).
                    //   막았을 때(패링 흰색)와 명중 시(붉은색)과 분리된 3번째 피드백 채널.
                    Vector3 missP = transform.position + Vector3.up * 0.05f;
                    ShockwaveRingFX.Spawn(missP, 0.9f, new Color(0.9f, 0.9f, 0.9f, 0.45f), 0.4f);
                    Debug.Log($"[AnimalAI] {_monsterId} 빈 스윙 — 예고 중 플레이어 이탈 (거리 {playerDist:F2} > 사정거리 {_attackRange:F2}) [95차 F5]");
                }
            }
            else
            {
                Debug.Log($"{_monsterId} attacks for {_attackDamage} damage!");
            }
        }

        /// <summary>예고 시각 요소 정리 — 스케일 복원 + 인디케이터 제거 + 상태 초기화(코루틴 정지 포함).</summary>
        private void CancelMeleeTelegraph()
        {
            if (_telegraphRoutine != null)
            {
                StopCoroutine(_telegraphRoutine);
                _telegraphRoutine = null;
            }
            RestoreTelegraphVisuals();
            ReleaseChargeGlow();   // [무번호 28-M1] 해제 경로 ② — CancelMeleeTelegraph/예고 중단 경로
            _telegraphAggroTarget = null;
        }

        /// <summary>선동작 스케일 복원 + 인디케이터 제거 + 플래그 해제 (파괴 진행 중 호출 안전).</summary>
        private void RestoreTelegraphVisuals()
        {
            _telegraphActive = false;
            _telegraphRoutine = null;
            if (this != null && transform != null)
                transform.localScale = _telegraphBaseScale;
            if (_telegraphIndicator != null)
            {
                Destroy(_telegraphIndicator);
                _telegraphIndicator = null;
            }
        }

        /// <summary>
        /// 어그로 대상이 유효한 근접 공격 대상인지 확인하고 IDamageable 반환.
        /// 조건: 오브젝트 활성(activeInHierarchy) + 생존 중인 IDamageable 구현.
        /// 유효하지 않으면 null 반환 → 호출부는 기존 플레이어 공격 경로로 폴백.
        /// </summary>
        /// <summary>
        /// [TEST28-69차] 병사 등 비-플레이어 공격자 어그로 등록 — 병사가 몬스터를 때리면
        /// 몬스터의 근접 공격 대상이 병사로 향한다(기존 GetAliveAggroDamageable 경로 재사용).
        /// GuardPlaceholder.PerformAttack에서 호출. 즉시 Combat(Alert 응시 스킵 — 68차에서 3초 응시만 하던 문제).
        /// </summary>
        public void NotifyAttacker(GameObject attacker)
        {
            if (attacker == null || _isDead) return;
            _aggroTarget = attacker;
            _aggroAttacker = attacker;
            if (_aggroState == AggroState.Idle || _aggroState == AggroState.Cooldown || _aggroState == AggroState.Alert)
                _aggroState = AggroState.Combat;   // 병사 어그로는 즉시 Combat — 추격·공격 시작
            MonsterAggroSystem.Instance?.NotifyAttack(gameObject, attacker);
            Debug.Log($"[AnimalAI] {MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId} 어그로 → {attacker.name} (병사 공격자 등록 — Combat)");
        }

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
            // [TEST28-69차] 병사 타격(weaponType "guard")은 어그로를 플레이어로 되돌리지 않는다 —
            //   기존은 무조건 attacker=player라 병사가 때려도 플레이어 타격마다 어그로가 플립됐다
            //   (병사 어그로는 PerformAttack → NotifyAttacker가 직접 등록).
            if (MonsterAggroSystem.Instance != null && weaponType != "guard")
            {
                // 공격자 찾기: hitDirection 반대 방향으로 추정 (또는 player)
                GameObject attacker = null;
                if (_player != null) attacker = _player.gameObject;
                MonsterAggroSystem.Instance.NotifyAttack(gameObject, attacker);
            }

            // === G2-05: CombatVFXController + 레거시 히트 이펙트 (FX 체인 격리) ===
            // 데미지 숫자(IMGUI) 등 어떤 FX 예외가 발생해도 Die() 도달이 막히지 않도록
            // FX 전체를 try-catch로 격리한다. 예외 시 스팸 가드(1초 쿨다운) 경고 후 계속.
            // 2026-09-13(46차): 피격 표현 단일화 — PlayerCombat→CombatFXGate 경로(Guz 에셋+데미지 숫자+카메라)로 통일,
            // AnimalAI 자체 절차 파티클 제거(스파크/블러드/HitVFX/숫자 2종). HitReaction은 행동 로직으로 유지.
            try
            {
                CombatVFXController.PlayHitFlash(gameObject);
                PlayHitRimFlash();   // [무번호 29-M2-1] 히트 림 플래시(별도 경로 — 히트컬러 계약 무손상)

                // 4. Hit Reaction (넉백 + 경직)
                var hitReaction = GetComponent<HitReaction>();
                if (hitReaction != null)
                {
                    hitReaction.PlayHitReaction(hitDirection, 1f);
                }
                // [T1 2026-09-15] 히트플래시 단일화 — HitReaction 유무와 무관하게
                // CombatVFXController.PlayHitFlash(gameObject) 하나만 사용한다(위). GetComponentsInChildren<Renderer>로
                // GLB 자식 렌더러를 모두 덮으므로 레거시 HitVFX.PlayHitFlash(MPB) 경로는 이중 발화·흰색 고정 원인이 되어 제거.

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
            if (_chargeRim != null) { FxRimOverlay.Release(_chargeRim); _chargeRim = null; }   // [무번호 29-M1-2] 사망 림 해제(누출 금지)

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
            if (_chargeRim != null) { FxRimOverlay.Release(_chargeRim); _chargeRim = null; }   // [무번호 29-M1-2] 사망 림 해제(누출 금지)

            // [95차 F5] 예고 중 사망 — 진행 중 텔레그래프 안전 정리(코루틴 정지+인디케이터 제거+스케일 복원), 무타격 보장
            CancelMeleeTelegraph();
            ReleaseChargeGlow();   // [무번호 28-M1] 해제 경로 ③ — Die 경로(사망 시각 복원 지점 — GetComponentsInChildren 토글 근처)

            // 애니메이션: Idle (즉시)
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            // 어그로 해제
            ClearAggro();

            // 플레이어 경험치/킬 카운트 (선택적)
            Debug.Log($"{MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId} 사망!");
            // ⏱️ 전투 로그: 처치 기록
            string monsterName = MonsterDatabase.Get(_monsterId)?.displayName ?? _monsterId;
            CombatLog.AddEntry($"{monsterName} 처치!", LogType.Kill);
            // 경험치 획득 — [O3 C-O3-02] 레벨 2차 곡선: CalculateXP(L) × (티어 기본 EXP/15) × 난수(0.8~1.2)
            // — GetExpBase(티어)/15는 배율로 보존(티어 데이터 무손상), 기존 (1+0.1L) 선형 배율은 CalculateXP가 대체
            MonsterLevelManager mgr = MonsterLevelManager.Instance;
            float baseExp = (mgr != null && mgr.Data != null) ? mgr.Data.GetExpBase(_tier) : 15f;
            int exp = Mathf.Max(1, Mathf.RoundToInt(MonsterLevelSystem.CalculateXP(_level) * (baseExp / 15f) * Random.Range(0.8f, 1.2f)));
            Debug.Log($"[AnimalAI] {monsterName} 경험치 계산: CalculateXP(Lv{_level}) × base={baseExp:F0}/15(티어={_tier}) × 난수(0.8~1.2) → +{exp} EXP");
            PlayerStats stats = PlayerStats.Instance;
            if (stats != null)
            {
                stats.AddEXP(exp);
            }
            else
            {
                Debug.LogWarning("[AnimalAI] PlayerStats.Instance를 찾을 수 없습니다.");
            }

            // [Phase D] 킬 VFX — 슬로우모션 + 화이트 플래시 + XP 팝업 (파티클 버스트 제거)
            CombatVFXController.PlayKillVFX(transform.position + Vector3.up * 1f, exp);

            // === G2-05: 사망 VFX ===
            // 2026-09-13(46차): 사망 혈흔도 절차 파티클 제외 — 사용자 지정

            // 🔊 컨트롤러 진동: 몬스터 사망 (Light)
            HapticFeedback.PlayPreset(HapticFeedback.RumblePreset.Light);

            // [무번호 25-A1] 사망 FX 무한 반복 수리 — Instantiate(deathEffectPrefab) 직접 호출(루프 오프/파괴 예약 부재,
            // DeathSmoke burst 루핑이 죽은 자리에 연기를 영원히 남김) 폐기 → DeathVFX.PlayDeathVFX 유한 수명 경로로 우회
            // (main.loop=false 강제 + 루핑 서브시스템 Stop + Destroy(go, 2~3s) + maxBurstsPerSecond=8 예산).
            // deathEffectPrefab 필드와 MonsterData 폴백 로드 경로는 유지(EA2 MonsterData 계약 무손상, 리소스 판별 용도).
            // [무번호 46-T2] FX 격리 — 45차 실측: DeathVFX NRE가 Die()를 1447행에서 중단 → 그 아래 전리품
            //   바구니·고기/재료/희귀 드롭·디졸브·시체 정리 전부 미실행(사용자 보고 "전리품 안 뜸/사망 확인 불가"의
            //   직접 뿌리). FX 실패는 사망 후처리를 방해하지 않는다 — TakeDamage의 카메라 이펙트 격리 선례와 동일 원칙.
            try
            {
                DeathVFX.PlayDeathVFX(transform.position, 1f);
            }
            catch (System.Exception deathFxEx)
            {
                Debug.LogWarning($"[46-DIE] 사망 FX 격리 — 사망 후처리(전리품·디졸브) 계속: {deathFxEx.Message}");
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

            // [Phase L] 전리품은 위에서 다운 시작 시 1회 드롭됨(재진입은 _isDead로 가드).
            // 즉시 SetCorpseVisuals(false)로 시체를 숨기지 않고, 절차 눕힘 + sink 후 0.6~1.2s 지연으로 파괴.
            float respawnDelay = 10f + (int)_tier * 5f;
            // C20-02: 난이도별 리스폰 속도 배율 (Easy: 빠름, Hard: 느림)
            respawnDelay *= DifficultyManager.GetRespawnRateMultiplier((DifficultyMode)GameManager.CurrentDifficulty);
            StartDeathSequence(respawnDelay);

            // === G2-04: 처치 카메라 이펙트
            CombatCameraEffects.PlayKill();
            // [무번호 71-G2] 처치 콤보 카운터 등록 — 동일 처치가 PlayKillVFX 훅과 이중 계수되지 않도록
            //   카운터 내 디듀프 가드(KillDedupSec)가 정합 담당. 연출 보조 try/catch 격리(전투 방해 금지).
            try { FxKillComboCounter.RegisterKill(transform.position); } catch (System.Exception) { }
        }

        /// <summary>
        /// [Phase L] 다운 모션 + 지연 파괴 시퀀스 시작. 콜라이더/어그로는 즉시 비활성,
        /// 루트를 90° 눕힌 뒤 0.6~1.2s 지연으로 시체(렌더러/콜라이더/헤드UI)를 숨긴다.
        /// 전리품은 호출측(Die)에서 다운 시작 시 1회 드롭하므로 이곳에선 드롭을 하지 않는다.
        /// </summary>
        private void StartDeathSequence(float respawnDelay)
        {
            if (_deathRoutineStarted) return;
            _deathRoutineStarted = true;

            // 어그로 해제 + 콜라이더 즉시 비활성 (렌더러는 다운 동안 유지)
            ClearAggro();
            FeedQuadrupedSpeed(0f);
            SetCollidersEnabled(false);

            // [무번호 30-D4] 사망 디졸브 — 눕힘/sink 창(0.6~1.2s)에 바디 소멸 램프(엣지=MonsterDanger).
            //   폴백: 셰이더 부재/편집 모드 = 기존 사망 연출만(무영향). 머티리얼 인스턴스는 램프 종료 시 파괴(누출 금지).
            bool dissolveBegan = FxDissolveUtility.TryBeginDissolve(gameObject, FXPalette.MonsterDanger, 0.9f, (int)_tier);   // [무번호 39-T4] 티어 위계
            if (dissolveBegan)
            {
                StartCoroutine(FxDissolveUtility.FadeRoutine(gameObject, 0.9f));
                // [무번호 33-A3] 사망 소멸 광 — 디졸브 램프(0.9s)와 동기된 소멸 광(색=MonsterDanger 단일 소스).
                //   디졸브 폴백(셰이더 부재) 시 라이트도 스킵(연출 동기 계약).
                FxImpactLight.Spike(transform.position, FXPalette.MonsterDanger, 1.6f, 5f, 0.9f);
                // [무번호 33-D2] 소멸 불티 — 디졸브 엣지 상승 불티 버스트(FXPalette.Edge 단일 소스, 유한 수명).
                FxEmberRise.Burst(transform.position);
            }

            // [무번호 72-W2] 사망 관절 이완 1단계 — 절차 본 회전(러그돌 아님), 0.35s(눕힘 0.5s 창과 겹침,
            //   시체 숨김 0.6~1.2s 전 완료). FX 격리 원칙: 사망 후처리(디졸브/눕힘/전리) 절대 방해 금지 — 예외 조용 스킵.
            try { FxDeathRelax.Begin(gameObject, 0.35f); } catch (System.Exception) { }

            StartCoroutine(DeathDownRoutine(respawnDelay));
        }

        /// <summary>자식 포함 콜라이더만 토글 (렌더러는 유지 → 다운 모션 중 시체 표시).</summary>
        private void SetCollidersEnabled(bool enabled)
        {
            foreach (var c in GetComponentsInChildren<Collider>(true))
                c.enabled = enabled;
        }

        private IEnumerator DeathDownRoutine(float respawnDelay)
        {
            // 1) 절차 눕힘: 루트 좌우 회전 90° (측면으로 눕는 연출을 클립 없이 절차 재현) — 0.5s
            Quaternion startRot = transform.rotation; // 현재 바라보는 방향 기준 눕힘 (스폰 회전으로 역스냅 방지)
            Quaternion downRot = startRot * Quaternion.Euler(0f, 0f, 90f); // GLB 메시 기준 측면 눕힘
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);
            if (_rigAnim != null) _rigAnim.enabled = false;   // [무번호 72-W2] 리그 프리즈 — 본 구동 충돌 제거(이완 러너가 사지 본 구동 담당, 컴포넌트 인스턴스 토글만)

            float downDuration = 0.5f;
            float t = 0f;
            while (t < downDuration)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, downRot, Mathf.Clamp01(t / downDuration));
                yield return null;
            }
            transform.rotation = downRot;

            // 2) 총 0.6~1.2s 지연: 다운 후 남은 시간 동안 점점 sink (지면으로 가라앉는 연출)
            float totalDelay = Random.Range(0.6f, 1.2f);
            float remaining = Mathf.Max(0.05f, totalDelay - downDuration);
            Vector3 sinkStart = transform.position;
            float sinkDuration = Mathf.Min(0.5f, remaining);
            t = 0f;
            while (t < sinkDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(sinkStart, sinkStart - Vector3.up * 0.4f, Mathf.Clamp01(t / sinkDuration));
                yield return null;
            }

            // 남은 지연 대기
            if (remaining > sinkDuration)
                yield return new WaitForSeconds(remaining - sinkDuration);

            // 3) 시체 파괴(숨김): 렌더러/콜라이더/헤드UI 일괄. 회전 원복 후 리스폰.
            transform.rotation = _spawnRot; // 회전 원복 — 리스폰이 올바른 자세로 재생성
            SetCorpseVisuals(false);
            _deathRoutineStarted = false;

            Invoke(nameof(Respawn), respawnDelay);
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
            transform.rotation = _spawnRot; // [Phase L] 다운 중 눕혔던 회전 원복
            _deathRoutineStarted = false;   // [Phase L] 다운 시퀀스 초기화
            // 2026-09-11: 시체 비활성 원복 — GLB 대응으로 자식 렌더러/콜라이더/헤드UI 일괄 재활성
            // (기존 _renderer 단일 재활성은 GLB에서 null이라 무효)
            SetCorpseVisuals(true);
            ApplyColor();

            // [2026-09-14(49차)] 리스폰 정지 — 4족 애니 속도 0 피드
            FeedQuadrupedSpeed(0f);

            // 애니메이션: Idle
            if (_rigAnim != null) _rigAnim.enabled = true;   // [무번호 72-W2] 리그 원복 — 리스폰 후 정상 구동(프리즈 대응)
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
                // [무번호 19-E1] 발견(!) 시각 시그널 — Alert 진입 시 발밑 적색 ! 링(SpawnAlert, 3s 전이 게이지)
                // [무번호 19-E2] 발견 SFX — Alert 진입 시 저음 웅성+짧은 경계음(시각+청각 이중 예고)
                // [195차 P1] 시그널 본문 비움 — 적색링·청록 회전 체브런 시각+발견 SFX 철거(호출 배선·Alert→Combat 전이 로직 무수정)
                SpawnAlertSignals();
            }
        }

        /// <summary>[무번호 19-E1/E2] 발견 시각+청각 시그널 — (구) 발밑 적색 ! 링(3s 전이 게이지) + 발견 SFX.
        /// [195차 P1] 시각 제거 — 사용자 지시로 적색 링+청록 회전 체브런 스폰과 발견 SFX를 모두 철거, 본문 비움.
        ///   메서드 시그니처는 계약 보존(호출부 SetAggroTarget 무수정) — Alert→Combat 3초 전이(UpdateAggroTimer) 무수정.</summary>
        private void SpawnAlertSignals()
        {
            // [195차 P1] 본문 비움 — 발견 시각/청각 시그널 철거(시그니처 계약 보존).
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
                // [2026-09-14(49차)] 정지 — 4족 애니 속도 0 피드
                FeedQuadrupedSpeed(0f);
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetStateImmediate(AnimationState.Idle);
                return;
            }

            // 어그로 대상이 사망했는지 확인
            if (IsTargetDead())
            {
                ClearAggro();
                // Idle 애니메이션
                // [2026-09-14(49차)] 정지 — 4족 애니 속도 0 피드
                FeedQuadrupedSpeed(0f);
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
                // [2026-09-14(49차)] 경계 정지 — 4족 애니 속도 0 피드
                FeedQuadrupedSpeed(0f);
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetState(AnimationState.Idle);

                // [무번호 19-E3] 발견 응시 애니메이션 — Alert 진입 첫 0.5s 동안 몬스터가 대상 방향으로
                // 응시 + 짧은 정지(4족 애니 속도 0 피드는 이미 위에서 수행). 보강: Alert 진입 직후
                // 시각 피드백(적색 ! 링 E1)이 발밑에 렌더되므로, 응시 동작은 기존 LookRotation 스냅 유지.
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

                        // [2026-09-14(49차)] 어그로 도망 속도(_speed×어그로배율)를 4족 애니에 피드
                        FeedQuadrupedSpeed(_speed * AGGRO_SPEED_MULT);

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

                            // [2026-09-14(49차)] 어그로 돌진 속도(_speed×1.5×어그로배율)를 4족 애니에 피드
                            FeedQuadrupedSpeed(_speed * 1.5f * AGGRO_SPEED_MULT);

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

                            // [2026-09-14(49차)] 어그로 추격 속도(_speed×어그로배율)를 4족 애니에 피드
                            FeedQuadrupedSpeed(_speed * AGGRO_SPEED_MULT);

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

            // [95차 F5] IDamageable이면 데미지 (기본 공격) — 즉발 대신 예고→타격 2페이즈(거리 재확인 포함)
            if (_aggroTarget != null)
            {
                var dmg = _aggroTarget.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                {
                    BeginMeleeTelegraph(dmg);
                }
            }
        }

        private void OnDestroy()
        {
            // [95차 F5] 예고 중 파괴 — 코루틴/인디케이터 안전 정리 (파괴 후에도 링 쿼드 잔존 방지)
            CancelMeleeTelegraph();

            // MonsterAggroSystem 등록 해제
            if (MonsterAggroSystem.Instance != null)
            {
                MonsterAggroSystem.Instance.UnregisterMonster(this);
            }
        }

        /// <summary>[무번호 25-EA1] 몬스터 개성 임팩트 틴트 — 제안안(위험도 기본 적색 유지 + 몸색 25% 반영).
        ///   B3 위험도 색 계약(텔레그래프 일반=적/정예=주황)은 AttackTelegraphIndicator 내부 독립이라 무영향. 결정론.</summary>
        private static Color MonsterAccentImpactColor(Color baseImpactColor, Color bodyColor)
        {
            float maxC = Mathf.Max(bodyColor.r, Mathf.Max(bodyColor.g, bodyColor.b));
            if (maxC <= 0.001f) return baseImpactColor;         // 0색 안전 가드
            Color sat = new Color(bodyColor.r / maxC, bodyColor.g / maxC, bodyColor.b / maxC, 1f);   // 정규화
            return Color.Lerp(baseImpactColor, sat, 0.25f);     // 적색 우세 유지 — hue 25% 시프트
        }
    }
}