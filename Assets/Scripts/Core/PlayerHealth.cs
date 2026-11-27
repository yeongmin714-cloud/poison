using UnityEngine;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// 플레이어 체력 시스템
    /// - 싱글톤, 최대 HP 100
    /// - 몬스터 공격 시 데미지 적용
    /// - 사망 시 리스폰
    /// - HUD에 HP 표시를 위한 이벤트
    /// - IDamageable 구현
    /// - C21-02: 구르기 중 무적 (PlayerMovement.IsRolling 체크)
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public static PlayerHealth Instance { get; private set; }

        /// <summary>[156차 P5] 이계 카드 피해 감쇠 훅 — Systems(AfterlifeCardSystem) 정적 생성자가 주입(Core→Systems 역참조 회피). null=게이트 없음.</summary>
        public static System.Func<float, float> IncomingDamageModifier;
        /// <summary>[156차 P5] 뼈 방패 1회 완전 차단 훅 — true 반환 시 이번 타격 소멸(패링 성공 시 미소비).</summary>
        public static System.Func<bool> TryBlockOnce;

        [Header("Health Settings")]
        [SerializeField] private float _maxHP = 100f;
        [SerializeField] private float _currentHP;

        [Header("Respawn")]
        [SerializeField] private float _respawnHPPercent = 0.1f; // 최대체력의 10%로 부활
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private Vector3 _defaultRespawnPosition = new Vector3(728f, 1.5f, -529f);
        [Tooltip("true: 가장 가까운 플레이어 소유 영지에서 부활, false: _defaultRespawnPosition")]
        [SerializeField] private bool _respawnAtNearestTerritory = true;

        [Header("Invincibility")]
        [SerializeField] private float _invincibleTime = 0.5f; // 피격 후 무적 시간

        // 상태
        private bool _isDead = false;
        private float _lastDamageTime = float.NegativeInfinity;
        private Transform _playerTransform;
        private Component _movement; // C21-02: 구르기 무적 체크용 (reflection-safe)

        /// <summary>최대 HP</summary>
        public float MaxHP => _maxHP;
        /// <summary>현재 HP</summary>
        public float CurrentHP => _currentHP;
        /// <summary>HP 비율 (0~1)</summary>
        public float HPRatio => _maxHP > 0 ? Mathf.Clamp01(_currentHP / _maxHP) : 0f;
        /// <summary>사망 여부</summary>
        public bool IsDead => _isDead;
        /// <summary>IDamageable: 생존 여부</summary>
        public bool IsAlive => !_isDead;
        /// <summary>마지막 피격 이후 경과 시간(초). C-O4-01 자연 재생 그레이스 판정용 (캡슐화)</summary>
        public float SecondsSinceLastDamage => Time.time - _lastDamageTime;
        /// <summary>최대 체력 변경 (확장용)</summary>
        public void SetMaxHP(float value) { _maxHP = Mathf.Max(1f, value); }

        // HP 변경 이벤트 (HUD에서 구독)
        public event System.Action<float, float> OnHPChanged; // (current, max)

        /// <summary>사망 시 발생 (Systems.DeathEffectController 등에서 구독)</summary>
        public static event System.Action OnPlayerDied;
        /// <summary>부활 시 발생 (Systems.DeathEffectController 등에서 구독)</summary>
        public static event System.Action OnPlayerRespawned;
        /// <summary>
        /// 플레이어 피격 확정 발화 (position: 피격 지점(가슴 높이 +1.2), amount: 실제 적용 데미지).
        /// TakeDamage의 HP 감소 확정 시점에만 발화 — 폴링 엣지(회복 동시 발생 엣지 흡수 등) 결함 제거용.
        /// </summary>
        public static event System.Action<Vector3, float> OnPlayerDamaged;

        /// <summary>
        /// [80차] 플레이어 넉백 요청 — (피격 방향, 무기 타입별 강도). Systems 이동 스크립트
        /// (PlayerMovement)가 구독해 감속 커브 임펄스로 적용한다(구 실내 전용 컨트롤러는 [이동통일 B1]로 PM 통합 폐기).
        /// 구독자가 없으면 기존 CharacterController 즉시 Move 폴백(기존 폭탄/가스 동작 유지).
        /// </summary>
        public static event System.Action<Vector3, float> KnockbackRequested;

        private void Awake()
        {
            // === 단순화: 첫 번째가 주인 ===
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // NOTE: Player 오브젝트는 씬에 있어야 함. DontDestroyOnLoad 제거 (Player가 DontDestroyOnLoad 씬으로 이동하면 GameSetup Purge에서 파괴됨)
            // DontDestroyOnLoad(gameObject);
            _movement = GetComponent("PlayerMovement"); // C21-02: 구르기 무적 체크용 (reflection-safe)
        }

        /// <summary>
        /// 강제 인스턴스 리셋 (씬 전환 시 안전하게 재초기화용)
        /// </summary>
        public static void ResetInstance()
        {
            if (Instance != null)
            {
                DestroyImmediate(Instance.gameObject);
                Instance = null;
            }
        }

        private void Start()
        {
            _currentHP = _maxHP;

            // 자신의 Transform을 가져오고, 이 GameObject가 실제 Player가 아니라면 Player 태그로 찾는다
            _playerTransform = transform;
            if (gameObject.tag != "Player")
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _playerTransform = player.transform;
            }

            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        /// <summary>
        /// 데미지 받기
        /// </summary>
        public void TakeDamage(float damage)
        {
            // AnimalAI/MonsterSkillSystem의 플레이어 대상 공격도 이 untyped 경로를 사용한다.
            // 환경/시스템 전용 데미지는 typed 경로에서 명시적으로 패링 제외한다.
            if (TryParryReflection())
            {
                Debug.Log($"[PlayerHealth] 🛡️ 패링으로 공격 흡수 ({damage} 데미지 차단)");
                return;
            }

            // [156차 P5] 뼈 방패 — 1회 완전 차단 (패링 성공 시 미소비)
            if (TryBlockOnce != null && TryBlockOnce())
            {
                Debug.Log("[PlayerHealth] 🦴 뼈 방패가 타격을 완전히 막았다");
                return;
            }
            if (IncomingDamageModifier != null)
                damage = IncomingDamageModifier(damage); // [156차 P5] 강철 방패 감쇠 — 런 외부 1f

            ApplyDamage(damage);
        }

        /// <summary>공통 피해 처리. 패링 여부는 각 공개 TakeDamage 진입점에서 결정한다.</summary>
        private void ApplyDamage(float damage)
        {
            if (_isDead) return;

            // C21-02: 구르기 중 무적 (reflection-safe access)
            if (_movement != null)
            {
                var rollingProp = _movement.GetType().GetProperty("IsRolling",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (rollingProp != null)
                {
                    bool isRolling = (bool)(rollingProp.GetValue(_movement) ?? false);
                    if (isRolling) return;
                }
            }

            // 무적 시간 체크
            if (Time.time - _lastDamageTime < _invincibleTime) return;
            _lastDamageTime = Time.time;

            // 방어력 적용 — 비율식 데미지 감소 (기존 평탄 감소 방식 대체)
            // standard 공식: actualDamage = damage * (100 / (100 + defense))
            // 방어력이 높을수록 감소율이 커지지만 절대 100%를 넘지 않음 (수렴형)
            float defense = PlayerStats.Instance != null ? PlayerStats.Instance.FinalDefense : 0f;
            float reductionRate = 1f - 100f / (100f + defense); // 감소율 = 1 - 100/(100+defense)
            float actualDamage = damage * (100f / (100f + defense));
            actualDamage = Mathf.Max(0f, actualDamage); // 최소 데미지 0 보장 (음수 방지)
            if (defense > 0f)
                Debug.Log($"[PlayerHealth] 방어력 {defense:F1}({reductionRate:P0}) 데미지 {damage:F1} → {actualDamage:F1}");

            _currentHP -= actualDamage;
            _currentHP = Mathf.Max(0f, _currentHP);

            Debug.Log($"[PlayerHealth] 💥 {actualDamage} 데미지! HP: {_currentHP}/{_maxHP}");

            // 🔊 컨트롤러 진동: 큰 데미지(>20)는 Medium 럼블
            if (actualDamage > 20f)
            {
                Debug.Log("[PlayerHealth] 큰 데미지! 햅틱 피드백 (Medium Rumble)");
            }
            // ⏱️ 전투 로그: 피격 기록
            Debug.Log("[CombatLog] " + actualDamage + " 데미지를 받음");
            OnHPChanged?.Invoke(_currentHP, _maxHP);

            // 이벤트 기반 피격 확정 발화 — HP 감소 확정 시점 1회. 구독자(HumanoidClipDriver 임팩트 FX 등)
            // 예외가 데미지/사망 흐름을 절대 막지 않도록 try-catch로 흡수.
            try
            {
                Vector3 hitPos = (_playerTransform != null ? _playerTransform.position : transform.position)
                                 + Vector3.up * 1.2f;
                OnPlayerDamaged?.Invoke(hitPos, actualDamage);
            }
            catch (System.Exception dmgEvtEx)
            {
                Debug.LogWarning($"[PlayerHealth] OnPlayerDamaged 구독자 예외 흡수(전투 계속): {dmgEvtEx.Message}");
            }

            // G2-04: 피격 카메라 이펙트 (주석처리 - 임시)
            // CombatCameraEffects.PlayHit();

            if (_currentHP <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// IDamageable.TakeDamage 구현: 데미지 + 피격 방향 + 무기 타입
        /// </summary>
        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            // 무기 카테고리와 무관하게 패링하되, Arena 등 전투가 아닌 시스템 조정은 제외한다.
            // Core→Systems 어셈블리 참조 제약을 피하기 위해 PlayerCombat은 리플렉션 경유.
            if (IsParryEligibleDamageType(weaponType) && TryParryReflection())
            {
                Debug.Log($"[PlayerHealth] 🛡️ 패링으로 {weaponType ?? "알 수 없는 타입"} 공격 흡수 ({amount} 데미지 차단)");
                return;
            }

            // [156차 P5] 뼈 방패 — 1회 완전 차단 (패링 성공 시 미소비)
            if (TryBlockOnce != null && TryBlockOnce())
            {
                Debug.Log("[PlayerHealth] 🦴 뼈 방패가 타격을 완전히 막았다");
                return;
            }
            if (IncomingDamageModifier != null)
                amount = IncomingDamageModifier(amount); // [156차 P5] 강철 방패 감쇠 — 런 외부 1f

            // 공통 피해 처리 (방어력/무적 처리 포함), 패링은 위에서만 판정한다.
            ApplyDamage(amount);

            // [63-B] 피격 상태이상 발동 — DOT("status") 재귀 가드 포함(계획서 §4#1)
            TriggerHitStatusEffects(weaponType, amount);

            // hitDirection 기반 넉백 효과 (무기 타입별 강도)
            if (_playerTransform != null && hitDirection != Vector3.zero)
            {
                // [2026-10-08] 폭탄(Explosion)은 강한 넉백, 가스류(Poison/Gas*)는 중간 넉백, 기타는 기존.
                float force = GetKnockbackForce(weaponType);
                var kbHandler = KnockbackRequested;
                if (kbHandler != null)
                {
                    // [80차] 임펄스 경로 — 이동 스크립트가 0.25s 감속 커브로 밀어낸다(즉시 텔레포트 튐 제거).
                    kbHandler.Invoke(hitDirection.normalized, force);
                }
                else
                {
                    // 폴백 — 구독자 없음: 기존 즉시 이동 유지
                    var controller = _playerTransform.GetComponent<CharacterController>();
                    if (controller != null)
                    {
                        Vector3 knockback = hitDirection.normalized * force;
                        controller.Move(knockback);
                    }
                }
                Debug.Log($"[PlayerHealth] 넉백 방향: {hitDirection}, 무기 타입: {weaponType}");
            }
        }

        // 폭발/가스 넉백 강도 — "폭탄은 넉백" 요구(2026-10-08). 몬스터/병사 모두 대상.
        private static float GetKnockbackForce(string weaponType)
        {
            if (string.IsNullOrEmpty(weaponType)) return 2f;
            string t = weaponType.Trim().ToLowerInvariant();
            if (t.StartsWith("explosion")) return 6f;      // 폭발 폭탄 — 강한 넉백
            if (t.StartsWith("gas") || t.StartsWith("poison")) return 3f; // 독가스 — 중간 넉백
            return 2f;                                      // 근접 등 기존
        }

        /// <summary>
        /// 전투 피해는 무기 이름을 제한하지 않고 패링한다. 시스템/상태 피해로 확인된 타입만
        /// 제외하며, null/빈 문자열과 새 전투 타입은 기존 무기 중립 동작을 유지한다.
        /// </summary>
        private static bool IsParryEligibleDamageType(string weaponType)
        {
            if (string.IsNullOrWhiteSpace(weaponType)) return true;

            string tag = weaponType.Trim();
            return !tag.Equals("arena", System.StringComparison.OrdinalIgnoreCase)
                && !tag.Equals("poison", System.StringComparison.OrdinalIgnoreCase)
                && !tag.Equals("overdose", System.StringComparison.OrdinalIgnoreCase)
                && !tag.Equals("executed", System.StringComparison.OrdinalIgnoreCase)
                && !tag.Equals("spy caught", System.StringComparison.OrdinalIgnoreCase)
                && !tag.Equals("poison detected", System.StringComparison.OrdinalIgnoreCase)
                && !tag.Equals("assassination", System.StringComparison.OrdinalIgnoreCase)
                && !tag.StartsWith("gas", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>[171차 E2] TryParry MethodInfo 캐시 — 리플렉션 조회 1회 한정(핫패스 비용 제거). 도메인 리로드 시 static 초기화로 자동 갱신.</summary>
        private static System.Reflection.MethodInfo _tryParryMethodCache;

        /// <summary>[Phase 1-2] 리플렉션 안전 패링 판정 — Systems.PlayerCombat.TryParry() 호출.</summary>
        private bool TryParryReflection()
        {
            try
            {
                // Core→Systems 순환 참조를 피하면서 어셈블리 내 타입 조회까지 지원한다.
                var cls = FindSystemType("PlayerCombat");
                if (cls == null) return false;
                // 패링은 해당 플레이어의 컴포넌트에서 판정해야 하며, static Instance는 EditMode나 중복 씬에서
                // 다른 플레이어를 가리킬 수 있으므로 같은 오브젝트를 우선 사용한다.
                object inst = GetComponent(cls);
                if (inst == null)
                    inst = cls.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (inst == null) return false;
                // [171차 E2] MethodInfo 캐시 — 최초 1회만 GetMethod 조회, 이후 캐시 재사용(null 반환 계약 유지).
                if (_tryParryMethodCache == null)
                    _tryParryMethodCache = cls.GetMethod("TryParry", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var m = _tryParryMethodCache;
                if (m == null) return false;
                object result = m.Invoke(inst, null);
                return result is bool && (bool)result;
            }
            catch (System.Exception parryEx)
            {
                Debug.LogWarning($"[PlayerHealth] 패링 판정 실패(미흡수로 진행): {parryEx.Message}");
                return false;
            }
        }

        /// <summary>
        /// 체력 회복
        /// </summary>
        public void Heal(float amount)
        {
            if (_isDead) return;
            _currentHP += amount;
            _currentHP = Mathf.Min(_currentHP, _maxHP);
            Debug.Log($"[PlayerHealth] 💚 {amount} 회복! HP: {_currentHP}/{_maxHP}");
            // ⏱️ 전투 로그: 회복 기록
            Debug.Log("[CombatLog] HP " + amount + " 회복");
            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        /// <summary>
        /// 전체 회복
        /// </summary>
        public void HealFull()
        {
            Heal(_maxHP);
        }

        /// <summary>[156차 P5] 두 번째 목숨 — 사망 상태 해제 + 비율 부활 (이계 카드 전용 경로).</summary>
        public void ReviveAtRatio(float ratio)
        {
            _isDead = false;
            _currentHP = Mathf.Clamp(_maxHP * ratio, 1f, _maxHP);
            OnHPChanged?.Invoke(_currentHP, _maxHP);
            Debug.Log($"[PlayerHealth] 🕯️ 부활 — HP {_currentHP}/{_maxHP}");
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            Debug.Log("[PlayerHealth] 💀 플레이어 사망! 리스폰 중...");
            OnHPChanged?.Invoke(0, _maxHP);

            // 플레이어 비활성화
            if (_playerTransform != null)
            {
                // 모든 MonoBehaviour 비활성화 (Systems.PlayerMovement 등)
                var behaviours = _playerTransform.GetComponents<MonoBehaviour>();
                foreach (var b in behaviours)
                {
                    if (b != this) // 자기 자신(PlayerHealth)은 제외
                        b.enabled = false;
                }

                var renderer = _playerTransform.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;

                var collider = _playerTransform.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }

            // 사망 이벤트 발생 (DeathEffectController/GuardManager 등에서 처리) — 이계 진입 여부와 무관 항상 발화
            OnPlayerDied?.Invoke();

            // [A2 이계] 사망 라우팅 — 이계 시스템이 가로채면 즉시 리스폰을 시작하지 않음
            if (TryRouteDeathToAfterlife())
                return;

            StartCoroutine(RespawnCoroutine());
        }

        /// <summary>
        /// [A2 이계] 사망을 이계 생존으로 라우팅 — Systems.AfterlifeSurvivalSystem.TryInterceptDeath() 결과.
        /// true면 기존 RespawnCoroutine을 시작하지 않고 이계 시스템이 귀환/퍼머데스를 처리한다.
        /// Core→Systems 리플렉션 (순환 참조 회피 — TryGetBedSpawnPoint 동일 패턴).
        /// </summary>
        private bool TryRouteDeathToAfterlife()
        {
            try
            {
                var t = System.Type.GetType("ProjectName.Systems.AfterlifeSurvivalSystem, ProjectName.Systems");
                if (t == null) return false;
                var m = t.GetMethod("TryInterceptDeath", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (m == null) return false;
                var result = m.Invoke(null, null);
                return result is bool && (bool)result;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerHealth] 이계 라우팅 판정 실패(기존 부활로 진행): {e.Message}");
                return false;
            }
        }

        private System.Collections.IEnumerator RespawnCoroutine()
        {
            // WaitForSecondsRealtime 사용: DeathEffects의 unscaled time(Time.unscaledTime)과 동기화
            // SlowMo(Time.timeScale=0.3) 중에도 실제 3초 후에 리스폰되도록 함
            yield return new WaitForSecondsRealtime(_respawnDelay);
            Respawn();
        }

        private void Respawn() => Respawn(-1f);

        /// <summary>
        /// 부활 — hpPercentOverride가 0~1이면 그 비율로 부활(기존 _respawnHPPercent 대체).
        /// [A2 이계] 귀환 성공 시 시스템이 0.5(50%)로 호출 — 이계 귀환 보상 체감.
        /// </summary>
        private void Respawn(float hpPercentOverride)
        {
            _isDead = false;
            float respawnPercent = (hpPercentOverride >= 0f && hpPercentOverride <= 1f)
                ? hpPercentOverride : _respawnHPPercent;
            _currentHP = _maxHP * respawnPercent;
            _currentHP = Mathf.Max(1f, _currentHP);   // 최소 1은 유지

            // Time.timeScale 복원 (DeathEffectController에서 슬로우 모션 사용 후)
            Time.timeScale = 1f;

            // 부활 위치 우선순위: 세이브한 침대(스폰핀) > 가장 가까운 자기 영지 > 기본 위치
            Vector3 respawnPos = _defaultRespawnPosition;
            bool spawnPosResolved = false;

            // (a) 침대에서 세이브(💾)한 스폰핀 최우선 — Systems.Bed는 Core에서 직접 참조 불가(순환참조)라 리플렉션
            if (TryGetBedSpawnPoint(out Vector3 bedSpawnPos))
            {
                // 매트리스(상단 ~0.5m) 위 소환 방지: 살짝 위에서 부활해 중력으로 착지
                respawnPos = bedSpawnPos + Vector3.up * 0.8f;
                spawnPosResolved = true;
                Debug.Log($"[PlayerHealth] 세이브한 침대(스폰핀)에서 부활: {respawnPos}");
            }

            if (!spawnPosResolved && _respawnAtNearestTerritory)
            {
                // Phase 27: GuardManager를 통해 가장 가까운 플레이어 소유 영지 찾기 (reflection-safe)
                Vector3 currentPos = _playerTransform != null ? _playerTransform.position : _defaultRespawnPosition;

                // GuardManager.Instance?.FindNearestPlayerTerritory(currentPos)
                var guardMgrType = FindSystemType("GuardManager");
                var guardInstance = guardMgrType?.GetField("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                System.Nullable<TerritoryId> nearestTerritory = null;
                if (guardInstance != null)
                {
                    var findMethod = guardMgrType.GetMethod("FindNearestPlayerTerritory",
                        new System.Type[] { typeof(Vector3) });
                    if (findMethod != null)
                    {
                        var result = findMethod.Invoke(guardInstance, new object[] { currentPos });
                        if (result != null)
                            nearestTerritory = (System.Nullable<TerritoryId>)result;
                    }
                }

                if (nearestTerritory.HasValue)
                {
                    // TerritoryManager.Instance?.GetTerritoryCenter()
                    var tmType = FindSystemType("TerritoryManager");
                    var tmInstance = tmType?.GetField("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                    if (tmInstance != null)
                    {
                        var getCenterMethod = tmType.GetMethod("GetTerritoryCenter",
                            new System.Type[] { typeof(TerritoryId) });
                        if (getCenterMethod != null)
                            respawnPos = (Vector3)getCenterMethod.Invoke(tmInstance,
                                new object[] { nearestTerritory.Value });
                    }
                    Debug.Log($"[PlayerHealth] 가장 가까운 영지에서 부활: {nearestTerritory.Value}");
                }
                else
                {
                    // 기본 위치 사용
                    respawnPos = _defaultRespawnPosition;
                    Debug.Log("[PlayerHealth] 플레이어 소유 영지 없음, 기본 위치에서 부활");
                }
            }

            // 플레이어 위치 리셋
            if (_playerTransform != null)
            {
                _playerTransform.position = respawnPos;

                // 모든 MonoBehaviour 재활성화
                var behaviours = _playerTransform.GetComponents<MonoBehaviour>();
                foreach (var b in behaviours)
                {
                    if (b != this)
                        b.enabled = true;
                }

                var renderer = _playerTransform.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = true;

                var collider = _playerTransform.GetComponent<Collider>();
                if (collider != null) collider.enabled = true;
            }

            Debug.Log("[PlayerHealth] 🔄 리스폰 완료!");
            OnHPChanged?.Invoke(_currentHP, _maxHP);

            // 부활 이벤트 발생 (DeathEffectController에서 페이드 인 처리)
            OnPlayerRespawned?.Invoke();
        }

        /// <summary>
        /// [A2 이계] 이계 귀환 정산용 공개 부활 — private Respawn(hpPercentOverride)의 시스템 경로.
        /// 위치 우선순위(스폰핀>자기 영지)+페이드 인 이벤트 포함 정식 부활.
        /// </summary>
        public void RespawnPublic(float hpPercentOverride) => Respawn(hpPercentOverride);

        /// <summary>
        /// [A2 이계] 이계 진입용 현장 부활 — 위치 이동/Respawned 이벤트 없이 살아있는 상태로 복귀
        /// (이계 시스템이 EnterBuilding 흐름으로 플레이어를 이계 씬에 이동시키고, HP/조작이 즉시 가능해야 함).
        /// 귀환 시의 정식 Respawn(0.5)과 달리 OnPlayerRespawned(페이드 인)를 발화하지 않는다.
        /// </summary>
        public void ReviveInPlace(float hpPercent)
        {
            float pct = Mathf.Clamp(hpPercent, 0.05f, 1f);
            _isDead = false;
            _currentHP = Mathf.Max(1f, _maxHP * pct);

            if (_playerTransform != null)
            {
                var behaviours = _playerTransform.GetComponents<MonoBehaviour>();
                foreach (var b in behaviours)
                {
                    if (b != this)
                        b.enabled = true;
                }
                var renderer = _playerTransform.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = true;
                var collider = _playerTransform.GetComponent<Collider>();
                if (collider != null) collider.enabled = true;
            }

            OnHPChanged?.Invoke(_currentHP, _maxHP);
            Debug.Log($"[PlayerHealth] 🔮 이계 현장 부활 — HP {_currentHP:F0}/{_maxHP:F0} ({pct:P0})");
        }

        /// <summary>
        /// 최근 지정된 시간(초) 이내에 피격되었는지 확인합니다.
        /// </summary>
        /// <param name="seconds">확인할 시간 범위 (초)</param>
        /// <returns>해당 시간 이내에 피격되었다면 true</returns>
        public bool WasRecentlyHit(float seconds)
        {
            return Time.time - _lastDamageTime <= seconds;
        }

        /// <summary>
        /// 무적 시간 설정 (외부에서 조정)
        /// </summary>
        public void SetInvincibleTime(float seconds)
        {
            _invincibleTime = Mathf.Max(0f, seconds);
        }

        /// <summary>
        /// 침대에서 세이브한 스폰핀(부활 지점)을 조회합니다 (reflection-safe).
        /// Systems.Bed.s_customSpawnPoint (Vector3?) — Core→Systems 직접 참조 불가(순환참조)라
        /// 기존 GuardManager/TerritoryManager와 동일한 FindSystemType 리플렉션 패턴 사용.
        /// </summary>
        private static bool TryGetBedSpawnPoint(out Vector3 spawnPos)
        {
            spawnPos = Vector3.zero;

            var bedType = FindSystemType("Bed");
            if (bedType == null) return false;

            var spawnField = bedType.GetField("s_customSpawnPoint",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (spawnField == null) return false;

            // Vector3? 박싱: HasValue=true → boxed Vector3 (패턴 일치), false/null → 미설정
            object value = spawnField.GetValue(null);
            if (value is Vector3 pos)
            {
                spawnPos = pos;
                return true;
            }
            return false;
        }

        // ── [63-B] 피격 상태이상 발동 (Core→Systems 참조 제약 — 리플렉션, TryParryReflection 선례) ──
        private static System.Type _statusSysType;
        private static System.Reflection.MethodInfo _statusAdd;
        private static System.Type _statusIdType;
        private static object _bleedingId, _fractureId, _brittleId;

        /// <summary>
        /// [63-B] 피격 상태이상 발동 — ①근접/화살 피격 25% → 출혈(10s) ②단발 피해 ≥HP30% → 골절(90s)
        /// ③≥HP20% → 방어 취약(10s). weaponType=="status"(DOT 자기발동)는 스킵 — 재귀 가드.
        /// </summary>
        private void TriggerHitStatusEffects(string weaponType, float amount)
        {
            try
            {
                if (string.Equals(weaponType, "status", System.StringComparison.OrdinalIgnoreCase)) return;
                if (_statusAdd == null)
                {
                    _statusSysType = FindSystemType("PlayerStatusEffectSystem");
                    if (_statusSysType == null) return;
                    _statusAdd = _statusSysType.GetMethod("AddEffect", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    _statusIdType = FindSystemType("StatusEffectId");
                    if (_statusAdd == null || _statusIdType == null) return;
                    _bleedingId = System.Enum.Parse(_statusIdType, "Bleeding");
                    _fractureId = System.Enum.Parse(_statusIdType, "Fracture");
                    _brittleId = System.Enum.Parse(_statusIdType, "BrittleDefense");
                }

                string wt = (weaponType ?? "").ToLowerInvariant();
                bool meleeish = wt.Contains("melee") || wt.Contains("arrow");
                if (meleeish && UnityEngine.Random.value < 0.25f)
                    _statusAdd.Invoke(null, new object[] { _bleedingId, 10f });
                if (amount >= MaxHP * 0.3f)
                    _statusAdd.Invoke(null, new object[] { _fractureId, 90f });
                else if (amount >= MaxHP * 0.2f)
                    _statusAdd.Invoke(null, new object[] { _brittleId, 10f });
            }
            catch (System.Exception)
            {
                // 상태이상 시스템 미부트/리플렉션 실패 — 전투 흐름 보호(무음 흡수, 선례 TryParryReflection)
            }
        }

        /// <summary>
        /// Systems 어셈블리 타입을 reflection으로 찾습니다 (순환참조 방지)
        /// </summary>
        private static System.Type FindSystemType(string typeName)
        {
            string fullName = "ProjectName.Systems." + typeName;
            var type = System.Type.GetType(fullName);
            if (type != null) return type;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(fullName);
                if (type != null) return type;
                type = asm.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }
    }
}