using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 4: 말 스포너 (MountSpawner)
    /// Horse NPC 프리팹에 부착되어 말의 생명주기를 관리합니다.
    /// - 고정된 말 NPC 또는 소환된 말 관리
    /// - 말 NPC와 플레이어 거리 50m 초과 시 자동 제거 (despawn) + 재소환 가능
    /// - 말 사망 시 (HP 0) → despawn, 60초 후 재소환 가능
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MountSpawner : MonoBehaviour
    {
        [Header("말 설정")]
        [SerializeField] private string _horseName = "말";
        [SerializeField] private float _maxHP = 100f;

        [Header("거리 설정")]
        [SerializeField] private float _despawnDistance = 50f;
        [SerializeField] private float _respawnCooldown = 60f; // 사망 후 재소환 쿨다운

        [Header("태그/레이어")]
        [SerializeField] private string _mountTag = "Mount";
        [SerializeField] private string _mountLayer = "Mount";

        // 상태
        private float _currentHP;
        private bool _isDead = false;
        private bool _isSpawned = false;
        private float _deathTime = 0f;
        private GameObject _playerCache;

        // Rig animation (말 애니메이션)
        private RigAnimationController _rigAnim;

        // MountSystem 참조
        private MountSystem _mountSystem;

        // ===== Public Properties =====

        /// <summary>말 이름</summary>
        public string HorseName => _horseName;

        /// <summary>현재 체력</summary>
        public float CurrentHP => _currentHP;

        /// <summary>최대 체력</summary>
        public float MaxHP => _maxHP;

        /// <summary>사망 상태인가?</summary>
        public bool IsDead => _isDead;

        /// <summary>소환된 상태인가?</summary>
        public bool IsSpawned => _isSpawned;

        /// <summary>사망 후 경과 시간</summary>
        public float TimeSinceDeath => _isDead ? Time.time - _deathTime : 0f;

        /// <summary>재소환 가능한가? (사망 후 _respawnCooldown 경과)</summary>
        public bool CanRespawn => _isDead && (Time.time - _deathTime >= _respawnCooldown);

        /// <summary>재소환까지 남은 시간</summary>
        public float RespawnTimeLeft => _isDead ? Mathf.Max(0f, _respawnCooldown - (Time.time - _deathTime)) : 0f;

        // ===== Unity Lifecycle =====

        private void Awake()
        {
            // 태그 자동 설정
            if (string.IsNullOrEmpty(gameObject.tag) || gameObject.tag == "Untagged")
            {
                gameObject.tag = _mountTag;
            }

            // 레이어 설정
            int layer = LayerMask.NameToLayer(_mountLayer);
            if (layer >= 0)
            {
                gameObject.layer = layer;
            }

            // Collider가 Trigger가 아닌지 확인
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
            }

            // RigAnimationController 캐싱
            _rigAnim = GetComponent<RigAnimationController>();
            if (_rigAnim == null)
            {
                Animator anim = GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    _rigAnim = gameObject.AddComponent<RigAnimationController>();
                }
            }

            _currentHP = _maxHP;
            _isSpawned = false;
        }

        private void Start()
        {
            // MountSystem 참조 캐싱
            _mountSystem = MountSystem.Instance;
            if (_mountSystem == null)
            {
                _mountSystem = FindAnyObjectByType<MountSystem>();
            }

            // 플레이어 캐싱
            CachePlayerReference();
        }

        private void Update()
        {
            // 사망 상태 업데이트
            if (_isDead)
            {
                // 아무 처리 안 함 (재소환 대기)
                return;
            }

            if (!_isSpawned) return;

            // 플레이어 참조 갱신
            if (_playerCache == null || !_playerCache.activeInHierarchy)
            {
                CachePlayerReference();
            }

            if (_playerCache == null) return;

            // 거리 체크 → 자동 제거
            float dist = Vector3.Distance(transform.position, _playerCache.transform.position);
            if (dist > _despawnDistance)
            {
                AutoDespawn();
            }

            // MountSystem을 통한 HP 동기화 (탑승 중일 때)
            if (_mountSystem == null)
            {
                _mountSystem = MountSystem.Instance;
            }

            if (_mountSystem != null && _mountSystem.IsMounted && _mountSystem.CurrentHorse == gameObject)
            {
                // MountSystem의 HP를 이 스포너에 반영
                _currentHP = _mountSystem.MountHP;

                // HP 0 체크 (MountSystem에서 이미 처리하지만, 혹시 모를 안전장치)
                if (_currentHP <= 0f && !_isDead)
                {
                    OnHorseDeath();
                }
            }

            // Idle 애니메이션 (움직이지 않을 때)
            if (_rigAnim != null && _currentHP > 0f && !_isDead)
            {
                // 애니메이션 상태는 MountSystem에서 관리하므로 여기서는 Idle 유지
                // (탑승 중에는 MountSystem이 Run 등으로 변경)
                if (_mountSystem == null || !_mountSystem.IsMounted || _mountSystem.CurrentHorse != gameObject)
                {
                    if (_rigAnim.CurrentState != AnimationState.Idle)
                    {
                        _rigAnim.SetState(AnimationState.Idle);
                    }
                }
            }
        }

        // ===== 말 생명주기 =====

        /// <summary>
        /// 말이 소환되었을 때 호출됩니다. (MountSystem.SummonHorse()에서 호출)
        /// </summary>
        public void OnHorseSummoned()
        {
            if (_isDead) return;

            _isSpawned = true;
            _currentHP = _maxHP;

            // 태그/레이어 재설정
            gameObject.tag = _mountTag;
            int layer = LayerMask.NameToLayer(_mountLayer);
            if (layer >= 0)
            {
                gameObject.layer = layer;
            }

            // 활성화
            gameObject.SetActive(true);

            // Collider 활성화
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            // Renderer 활성화
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = true;

            // 애니메이션
            if (_rigAnim != null)
            {
                _rigAnim.SetStateImmediate(AnimationState.Idle);
            }

            Debug.Log($"[MountSpawner] 🐴 {_horseName} 소환됨! HP: {_currentHP}/{_maxHP}");
        }

        /// <summary>
        /// 말 사망 처리 (HP 0 도달 시)
        /// </summary>
        public void OnHorseDeath()
        {
            if (_isDead) return;

            _isDead = true;
            _isSpawned = false;
            _deathTime = Time.time;
            _currentHP = 0f;

            // 사망 애니메이션
            if (_rigAnim != null)
            {
                _rigAnim.SetStateImmediate(AnimationState.Idle);
            }

            // 시각적 비활성화
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // LootBasket 생성 (선택사항 — 말 사망 시 약간의 아이템 드롭)
            SpawnDeathLoot();

            Debug.Log($"[MountSpawner] 💀 {_horseName} 사망! {_respawnCooldown}초 후 재소환 가능");
        }

        /// <summary>
        /// 자동 제거 (플레이어와 거리 50m 초과)
        /// </summary>
        private void AutoDespawn()
        {
            if (_isDead) return;

            Debug.Log($"[MountSpawner] 🗑️ {_horseName} 자동 제거 (거리 초과: {_despawnDistance}m)");

            // MountSystem에 알림
            if (_mountSystem == null)
            {
                _mountSystem = MountSystem.Instance;
            }

            if (_mountSystem != null && _mountSystem.CurrentHorse == gameObject)
            {
                _mountSystem.DespawnCurrentHorse();
                // DespawnCurrentHorse에서 이미 처리되므로 여기서 추가 처리 불필요
            }
            else
            {
                // MountSystem이 모르는 말이면 직접 제거
                gameObject.SetActive(false);
                _isSpawned = false;
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 말 사망 시 약간의 전리품 생성 (선택사항)
        /// </summary>
        private void SpawnDeathLoot()
        {
            // 간단한 전리품: 가죽 or 고기 정도
            LootBasket basket = LootBasket.Create(transform.position);
            if (basket != null)
            {
                // 말고기 or 가죽 드롭 (랜덤)
                if (Random.value < 0.5f)
                {
                    basket.AddItem(new PlayerInventory.ItemData
                    {
                        id = "meat_horse",
                        displayName = "말고기",
                        description = "질긴 말고기.",
                        category = PlayerInventory.ItemCategory.Meat,
                        maxStack = 10
                    }, Random.Range(1, 3));
                }

                if (Random.value < 0.3f)
                {
                    basket.AddItem(new PlayerInventory.ItemData
                    {
                        id = "mat_horse_leather",
                        displayName = "말가죽",
                        description = "질긴 말가죽. 가죽세공 재료.",
                        category = PlayerInventory.ItemCategory.Material,
                        maxStack = 10
                    }, Random.Range(1, 2));
                }
            }
        }

        /// <summary>
        /// 말 재소환 (사망 후 쿨다운 완료 시 호출 가능)
        /// MountSystem.SummonHorse()에서 호출됩니다.
        /// </summary>
        public void Respawn()
        {
            if (!_isDead) return;
            if (!CanRespawn)
            {
                Debug.Log($"[MountSpawner] ⏳ 재소환 대기 중... {RespawnTimeLeft:F1}초 남음");
                return;
            }

            _isDead = false;
            _isSpawned = true;
            _currentHP = _maxHP;

            // 위치: 플레이어 근처로
            if (_playerCache != null)
            {
                Vector3 spawnPos = _playerCache.transform.position + _playerCache.transform.forward * 3f;
                RaycastHit hit;
                if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out hit, 10f))
                {
                    spawnPos = hit.point;
                }
                transform.position = spawnPos;
            }

            // 시각적 활성화
            gameObject.SetActive(true);

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = true;

            // 애니메이션
            if (_rigAnim != null)
            {
                _rigAnim.SetStateImmediate(AnimationState.Idle);
            }

            Debug.Log($"[MountSpawner] 🐴 {_horseName} 재소환!");
        }

        // ===== 헬퍼 =====

        /// <summary>
        /// 플레이어 GameObject 참조 캐싱
        /// </summary>
        private void CachePlayerReference()
        {
            _playerCache = GameObject.FindGameObjectWithTag("Player");
            if (_playerCache == null)
            {
                // PlayerMovement로 찾기
                var pm = FindAnyObjectByType<PlayerMovement>();
                if (pm != null)
                    _playerCache = pm.gameObject;
            }
        }

        // ===== Gizmos =====

        private void OnDrawGizmosSelected()
        {
            // 상호작용 범위
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 3f);

            // 제거 거리
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _despawnDistance);
        }
    }
}