using System;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems.Animation.Procedural;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 약초 채집 시스템. E 키로 채집, 인벤토리에 저장.
    /// 지형에 배치된 약초 오브젝트에 붙임.
    /// </summary>
    public class HerbPickup : MonoBehaviour
    {
        [System.Serializable]
        public enum HerbType
        {
            Red,    // 치유초
            Purple, // 독나물
            Yellow, // 황혼초
            Silver, // 은빛 이끼
            Green   // 피어리
        }

        [Header("설정")]
        [SerializeField] private HerbType _herbType = HerbType.Red;
        [SerializeField] private float _interactRange = 2.5f;
        [SerializeField] private float _respawnTime = 30f;   // 채집 후 재생성 시간
        [SerializeField] private int _minYield = 1;
        [SerializeField] private int _maxYield = 3;

        [Header("UI")]
        [SerializeField] private GameObject _promptPrefab;  // "E 키로 채집" 안내 (World Space Canvas)

        // 상태
        private bool _isHarvested = false;
        private bool _playerNearby = false;
        private GameObject _promptInstance;
        private Transform _player;
        private float _harvestTime = 0f;
        private Renderer _renderer;   // C5-17: 반투명 효과용
        private Material _materialCopy; // 렌더러 재질 복사본

        // Rig animation (플레이어의 RigAnimationController)
        private RigAnimationController _playerRigAnim;
        // Procedural animation (플레이어의 ProceduralAnimationController)
        private ProceduralAnimationController _playerProceduralAnim;
        // 자체 Rig animation (약초 자체에 Animator가 있는 경우)
        private RigAnimationController _rigAnim;

        // Public API
        public bool IsAvailable => !_isHarvested;
        public HerbType HerbPickupType => _herbType;
        public bool IsHarvested => _isHarvested;
        public float RespawnDuration => _respawnTime;
        public float RespawnTimeLeft => _isHarvested ? Mathf.Max(0f, _respawnTime - (Time.time - _harvestTime)) : 0f;
        public float RespawnProgress => _isHarvested ? Mathf.Clamp01((Time.time - _harvestTime) / _respawnTime) : 1f;

        // Events for visual state / UI
        public event Action OnHarvestStarted;
        public event Action OnRespawnCompleted;

        private static readonly (HerbType, PlayerInventory.ItemData)[] HerbMap = new[]
        {
            (HerbType.Red,    PlayerInventory.Herb_Red),
            (HerbType.Purple, PlayerInventory.Herb_Purple),
            (HerbType.Yellow, PlayerInventory.Herb_Yellow),
            (HerbType.Silver, PlayerInventory.Herb_Silver),
            (HerbType.Green,  PlayerInventory.Herb_Green),
        };

        private PlayerInventory.ItemData GetItemData()
        {
            foreach (var pair in HerbMap)
            {
                if (pair.Item1 == _herbType) return pair.Item2;
            }
            return PlayerInventory.Herb_Red;
        }

        // ===== 씨앗 드랍 확률 (코드 상수로 명시) =====
        private const float SeedDropChanceCommon = 0.24f;   // 기본 씨앗(Red/Purple/Yellow): 설계 22~25% 범위 내 24%
        private const float SeedDropChanceRare   = 0.09f;   // 희귀 씨앗(Silver/Green): 설계 8~10% 범위 내 9%

        /// <summary>HerbType → 대응 씨앗 ItemData 매핑 (PlayerInventory 정의)</summary>
        private static PlayerInventory.ItemData SeedItemForCrop(HerbType type)
        {
            switch (type)
            {
                case HerbType.Red:    return PlayerInventory.Seed_Red;
                case HerbType.Purple: return PlayerInventory.Seed_Purple;
                case HerbType.Yellow: return PlayerInventory.Seed_Yellow;
                case HerbType.Silver: return PlayerInventory.Seed_Silver;
                case HerbType.Green:  return PlayerInventory.Seed_Green;
                default:              return PlayerInventory.Seed_Red;
            }
        }

        /// <summary>
        /// 씨앗 드랍 확률 판정. 성공 시 대응 씨앗 ItemData를 out으로 반환.
        /// 희귀 종(Silver/Green)은 낮은 확률(9%), 나머지는 기본 확률(24%) 적용.
        /// </summary>
        private static bool TryRollSeedDrop(HerbType herbType, out PlayerInventory.ItemData seed, out float chance)
        {
            bool isRare = herbType == HerbType.Silver || herbType == HerbType.Green;
            chance = isRare ? SeedDropChanceRare : SeedDropChanceCommon;
            seed = SeedItemForCrop(herbType);
            return UnityEngine.Random.value < chance;
        }

        /// <summary>
        /// 채집 시 희귀확률로 씨앗을 바구니에 함께 드랍 (LootBasket.AddItem은 여러 번 호출 가능).
        /// Harvest 경로 전용.
        /// </summary>
        private static void AddSeedDrop(LootBasket basket, HerbType herbType)
        {
            if (basket == null) return;
            if (TryRollSeedDrop(herbType, out var seed, out float chance))
            {
                basket.AddItem(seed, 1);
                Debug.Log($"[HerbPickup] 🌰 씨앗 드랍! {seed.displayName} (확률 {chance:P0})");
            }
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning("[HerbPickup] Player 태그 오브젝트를 찾을 수 없음");

            // 플레이어의 RigAnimationController 찾기
            if (_player != null)
            {
                _playerRigAnim = _player.GetComponent<RigAnimationController>();
                if (_playerRigAnim == null)
                {
                    Animator playerAnim = _player.GetComponent<Animator>();
                    if (playerAnim != null && playerAnim.runtimeAnimatorController != null)
                        _playerRigAnim = _player.gameObject.AddComponent<RigAnimationController>();
                }

                // 자식 모델의 ProceduralAnimationController 찾기
                _playerProceduralAnim = _player.GetComponentInChildren<ProceduralAnimationController>();
                if (_playerProceduralAnim == null)
                {
                    Transform model = _player.Find("PlayerModel");
                    if (model != null)
                        _playerProceduralAnim = model.GetComponent<ProceduralAnimationController>();
                }
            }

            // 자체 RigAnimationController (약초에 Animator가 있는 경우)
            _rigAnim = GetComponent<RigAnimationController>();
            if (_rigAnim == null)
            {
                Animator anim = GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                    _rigAnim = gameObject.AddComponent<RigAnimationController>();
            }

            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
                _materialCopy = new Material(_renderer.material);
        }

        private void Update()
        {
            // C5-17: 채집 불가 상태일 때 반투명 표시
            if (_isHarvested)
            {
                UpdateHarvestedVisual();
                return;
            }

            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _playerNearby = dist <= _interactRange;

            // 프롬프트 표시/숨김
            if (_playerNearby && _promptInstance == null)
                ShowPrompt();
            else if (!_playerNearby && _promptInstance != null)
                HidePrompt();

            // E 키 입력
            if (_playerNearby && Input.GetKeyDown(KeyCode.E))
                Harvest();
        }

        private void Harvest()
        {
            _isHarvested = true;
            _harvestTime = Time.time;
            HidePrompt();

            // 플레이어 채집 애니메이션 (Gather)
            if (_playerRigAnim != null) _playerRigAnim.SetState(AnimationState.Gather);
            _playerProceduralAnim?.TriggerAction("gather");

            // T-D3+: 클립 기반 채집 모션(Harvest=Pull_Radish) — HumanoidClipDriver 트리거
            var clipDriver = _player != null ? (_player.GetComponent<HumanoidClipDriver>() ?? _player.GetComponentInChildren<HumanoidClipDriver>()) : null;
            if (clipDriver != null)
            {
                int variant = UnityEngine.Random.Range(0, 3);
                if (variant == 0) clipDriver.TriggerHarvest();
                else if (variant == 1) clipDriver.TriggerHarvestObject();
                else clipDriver.TriggerHarvestPickUp();
            }

            int yield = UnityEngine.Random.Range(_minYield, _maxYield + 1);
            var item = GetItemData();

            // LootBasket 생성: 약초가 바구니에 담겨서 떨어짐
            LootBasket basket = LootBasket.Create(transform.position);
            basket.AddItem(item, yield);

            // 씨앗 드랍: 채집 시 희귀확률로 씨앗이 바구니에 함께 담김
            AddSeedDrop(basket, _herbType);

            // 경험치 획득
            PlayerStats.Instance.AddEXP(3);
            Debug.Log($"[HerbPickup] 🧺 {item.displayName} x{yield} 바구니 생성!");

            // Phase 8.3: 채집 사운드
            SoundManager.Instance?.PlaySFX("pickup");

            // 잠시 안 보이게 (채집 연출)
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            if (_renderer != null) _renderer.enabled = false;

            // Fire harvest event
            OnHarvestStarted?.Invoke();

            // 리스폰
            Invoke(nameof(Respawn), _respawnTime);
        }

        private void Respawn()
        {
            _isHarvested = false;
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = true;
            if (_renderer != null) _renderer.enabled = true;

            // 약초 자체 Idle 애니메이션
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            // Fire respawn event
            OnRespawnCompleted?.Invoke();
        }

        /// <summary>
        /// Auto-gather by Herbalist guard — transfers directly to inventory.
        /// Returns true if herbs were gathered, with item and yield info.
        /// </summary>
        public bool TryAutoGather(out PlayerInventory.ItemData item, out int yield)
        {
            item = null;
            yield = 0;
            if (_isHarvested) return false;

            _isHarvested = true;
            _harvestTime = Time.time;

            // 플레이어 채집 애니메이션 (Gather)
            if (_playerRigAnim != null) _playerRigAnim.SetState(AnimationState.Gather);
            _playerProceduralAnim?.TriggerAction("gather");

            // T-D3+: 클립 기반 채집 모션(Harvest=Pull_Radish) — HumanoidClipDriver 트리거
            var clipDriver = _player != null ? (_player.GetComponent<HumanoidClipDriver>() ?? _player.GetComponentInChildren<HumanoidClipDriver>()) : null;
            if (clipDriver != null)
            {
                int variant = UnityEngine.Random.Range(0, 3);
                if (variant == 0) clipDriver.TriggerHarvest();
                else if (variant == 1) clipDriver.TriggerHarvestObject();
                else clipDriver.TriggerHarvestPickUp();
            }

            item = GetItemData();
            yield = UnityEngine.Random.Range(_minYield, _maxYield + 1);

            // 씨앗 드랍 판정 — 자동 채집은 바구니 없이 인벤토리 직송 구조이므로
            // 호출부(HerbGatheringMission)와 동일하게 플레이어 인벤토리에 즉시 추가한다.
            if (TryRollSeedDrop(_herbType, out var seedDrop, out float seedChance))
            {
                if (PlayerInventory.Instance != null && PlayerInventory.Instance.AddItem(seedDrop, 1))
                    Debug.Log($"[HerbPickup] 🌰 자동 채집 씨앗 획득! {seedDrop.displayName} (확률 {seedChance:P0})");
            }

            // Hide visual
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            if (_renderer != null) _renderer.enabled = false;

            OnHarvestStarted?.Invoke();

            // Phase 8.3: 채집 사운드
            SoundManager.Instance?.PlaySFX("pickup");

            // Schedule respawn
            Invoke(nameof(Respawn), _respawnTime);

            return true;
        }

        private void ShowPrompt()
        {
            if (_promptPrefab != null)
            {
                _promptInstance = Instantiate(_promptPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity, transform);
            }
        }

        private void HidePrompt()
        {
            if (_promptInstance != null)
            {
                Destroy(_promptInstance);
                _promptInstance = null;
            }
        }

        /// <summary>C5-17: 채집된 상태에서 반투명 시각 피드백</summary>
        private void UpdateHarvestedVisual()
        {
            if (_renderer == null || _materialCopy == null) return;

            float progress = RespawnProgress;

            // 리스폰 진행에 따라 0.2→1.0 알파
            float alpha = Mathf.Lerp(0.2f, 1.0f, progress);
            Color c = _materialCopy.color;
            c.a = alpha;
            _renderer.material.color = c;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}