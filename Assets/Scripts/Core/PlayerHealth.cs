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

        /// <summary>
        /// [RuntimeInitializeOnLoadMethod] 폴백: 씬에 PlayerHealth가 없으면 자동 생성.
        /// GameManager.InitializeSystems()보다 먼저 실행되어 Awake() 타이밍 문제를 방지합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreateFallback()
        {
            if (Instance != null) return;

            var existing = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            var go = new GameObject("PlayerHealth");
            go.AddComponent<PlayerHealth>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            Debug.Log("[PlayerHealth] Auto-created via RuntimeInitializeOnLoadMethod fallback.");
        }

        [Header("Health Settings")]
        [SerializeField] private float _maxHP = 100f;
        [SerializeField] private float _currentHP;

        [Header("Respawn")]
        [SerializeField] private float _respawnHPPercent = 0.1f; // 최대체력의 10%로 부활
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private Vector3 _defaultRespawnPosition = Vector3.zero;
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
        /// <summary>최대 체력 변경 (확장용)</summary>
        public void SetMaxHP(float value) { _maxHP = Mathf.Max(1f, value); }

        // HP 변경 이벤트 (HUD에서 구독)
        public event System.Action<float, float> OnHPChanged; // (current, max)

        /// <summary>사망 시 발생 (Systems.DeathEffectController 등에서 구독)</summary>
        public static event System.Action OnPlayerDied;
        /// <summary>부활 시 발생 (Systems.DeathEffectController 등에서 구독)</summary>
        public static event System.Action OnPlayerRespawned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _movement = GetComponent("PlayerMovement"); // C21-02: 구르기 무적 체크용 (reflection-safe)
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

            // 방어력 적용
            float defense = 0f;
            if (PlayerStats.Instance != null)
                defense = PlayerStats.Instance.FinalDefense;
            float actualDamage = Mathf.Max(0f, damage - defense);
            if (defense > 0f)
                Debug.Log($"[PlayerHealth] 방어력 {defense}로 데미지 감소: {damage} → {actualDamage}");

            _currentHP -= actualDamage;
            _currentHP = Mathf.Max(0f, _currentHP);

            Debug.Log($"[PlayerHealth] 💥 {actualDamage} 데미지! HP: {_currentHP}/{_maxHP}");

            // 🔊 컨트롤러 진동: 큰 데미지(>20)는 Medium 럼블
            if (actualDamage > 20f)
            {
                HapticFeedback.PlayPreset(HapticFeedback.RumblePreset.Medium);
            }
            // ⏱️ 전투 로그: 피격 기록
            Debug.Log("[CombatLog] " + actualDamage + " 데미지를 받음");
            OnHPChanged?.Invoke(_currentHP, _maxHP);

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
            // 기존 TakeDamage 호출 (방어력/무적 처리 포함)
            TakeDamage(amount);

            // hitDirection 기반 간단한 넉백 효과
            if (_playerTransform != null && hitDirection != Vector3.zero)
            {
                // CharacterController가 있다면 넉백 적용
                var controller = _playerTransform.GetComponent<CharacterController>();
                if (controller != null)
                {
                    Vector3 knockback = hitDirection.normalized * 2f;
                    controller.Move(knockback);
                }
                Debug.Log($"[PlayerHealth] 넉백 방향: {hitDirection}, 무기 타입: {weaponType}");
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

            StartCoroutine(RespawnCoroutine());

            // 사망 이벤트 발생 (DeathEffectController 등에서 처리)
            OnPlayerDied?.Invoke();
        }

        private System.Collections.IEnumerator RespawnCoroutine()
        {
            // WaitForSecondsRealtime 사용: DeathEffects의 unscaled time(Time.unscaledTime)과 동기화
            // SlowMo(Time.timeScale=0.3) 중에도 실제 3초 후에 리스폰되도록 함
            yield return new WaitForSecondsRealtime(_respawnDelay);
            Respawn();
        }

        private void Respawn()
        {
            _isDead = false;
            _currentHP = _maxHP * _respawnHPPercent; // 최대체력의 10%로 부활
            _currentHP = Mathf.Max(1f, _currentHP);   // 최소 1은 유지

            // Time.timeScale 복원 (DeathEffectController에서 슬로우 모션 사용 후)
            Time.timeScale = 1f;

            // 부활 위치: 가장 가까운 영지 또는 기본 위치
            Vector3 respawnPos = _defaultRespawnPosition;
            if (_respawnAtNearestTerritory)
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