using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-28: 자원 노드 — 광부가 채광할 수 있는 Wood/Stone/IronOre 노드
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        public enum ResourceType { Wood, Stone, IronOre, Herb, Silver, Gold, Mythril }  // [Milestone C] Silver/Gold/Mythril = 희귀 광물 노드

        [SerializeField, Tooltip("자원 종류")]
        private ResourceType _resourceType = ResourceType.Wood;

        [SerializeField, Tooltip("최소 수확량"), Range(1, 999)]
        private int _minYield = 1;

        [SerializeField, Tooltip("최대 수확량"), Range(1, 999)]
        private int _maxYield = 3;

        [SerializeField, Tooltip("리스폰 시간 (초)"), Min(0.1f)]
        private float _respawnTime = 15f;

        // [Milestone C] 채광 시 한 단계 위 희귀 광물을 추가로 굴릴 확률(0=없음). 가치↑→드랍↓ 밸런스.
        [SerializeField, Tooltip("채광 보너스 희귀 광물 드랍 확률(0~1). 가치가 높은 광물일수록 낮게."), Range(0f, 1f)]
        private float _rareBonusChance = 0f;

        private bool _isDepleted;

        // --- 캐시된 컴포넌트 참조 ---
        private Renderer _renderer;
        private Collider _collider;

        /// <summary>고갈되지 않아 채광 가능한 상태인지 여부</summary>
        public bool IsAvailable => !_isDepleted;
        /// <summary>이 노드의 자원 종류</summary>
        public ResourceType NodeType => _resourceType;
        /// <summary>보너스 희귀 광물 드랍 확률(Inspector).</summary>
        public float RareBonusChance => _rareBonusChance;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();

            // Inspector 설정 검증
            if (_minYield > _maxYield)
            {
                Debug.LogWarning($"[ResourceNode] _minYield({_minYield}) > _maxYield({_maxYield}), 자동 교정합니다.");
                _maxYield = _minYield;
            }
        }

        private void OnDisable()
        {
            // GameObject 비활성화 시에도 Invoke 타이머는 Unity가 일시중지/재개하므로
            // CancelInvoke를 호출하지 않음 (리스폰 타이머 유지).
            // OnDestroy에서만 정리.
        }

        private void OnDestroy()
        {
            CancelInvoke();
        }

        /// <summary>
        /// Miner auto-mine — 자원 채광 및 시각적 고갈 처리
        /// </summary>
        /// <param name="item">채광된 아이템 데이터 (고갈 시 null)</param>
        /// <param name="yield">수확량 (고갈 시 0)</param>
        /// <returns>채광 성공 여부</returns>
        public bool TryAutoMine(out PlayerInventory.ItemData item, out int yield)
        {
            item = null;
            yield = 0;
            if (_isDepleted) return false;

            _isDepleted = true;
            yield = Random.Range(_minYield, _maxYield + 1);
            item = GetItemData();

            // Hide visual
            if (_renderer) _renderer.enabled = false;
            if (_collider) _collider.enabled = false;

            Invoke(nameof(Respawn), _respawnTime);
            return true;
        }

        /// <summary>
        /// [Milestone C] 채광 보너스 — 이 노드에서 한 단계 위 희귀 광물을 추가 드랍.
        /// 가치(티어)↑→확률↓ 원칙: IronOre→Gold, Silver→Gold, Gold→Mythril, Mythril→수정석 등.
        /// _rareBonusChance=0이면 절대 안 뜬다(보통 노드 기본값). 성공 시 item/count 반환.
        /// </summary>
        public bool TryRollRareBonus(out PlayerInventory.ItemData item, out int yield)
        {
            item = null;
            yield = 0;
            if (_rareBonusChance <= 0f || Random.value > _rareBonusChance) return false;

            PlayerInventory.ItemData bonus = _resourceType switch
            {
                ResourceType.Stone  => PlayerInventory.SilverOre,
                ResourceType.IronOre => PlayerInventory.GoldOre,
                ResourceType.Silver => PlayerInventory.GoldOre,
                ResourceType.Gold   => PlayerInventory.MythrilOre,
                ResourceType.Mythril => PlayerInventory.CrystalShard,
                _ => null,
            };
            if (bonus == null) return false;
            item = bonus;
            yield = 1;
            Debug.Log($"[ResourceNode] 💎 보너스 희귀 광물 드랍: {bonus.displayName} ({_resourceType})");
            return true;
        }

        private void Respawn()
        {
            if (this == null) return; // 객체 파괴됐을 경우 방어

            _isDepleted = false;
            if (_renderer) _renderer.enabled = true;
            if (_collider) _collider.enabled = true;
        }

        private PlayerInventory.ItemData GetItemData()
        {
            switch (_resourceType)
            {
                case ResourceType.Wood:
                    return new PlayerInventory.ItemData
                    {
                        id = "mat_wood",   // [Milestone B] 크래프트 재료로 정렬(기존 "wood")
                        displayName = "통나무",
                        description = "채광/벌목으로 얻는 목재. 무기·방어구 제작 재료.",
                        category = PlayerInventory.ItemCategory.Material,
                        maxStack = 99
                    };
                case ResourceType.Stone:
                    return new PlayerInventory.ItemData
                    {
                        id = "mat_stone",  // [Milestone B] 크래프트 재료로 정렬(기존 "stone")
                        displayName = "석재",
                        description = "채광으로 얻는 돌. Stone 티어 장비 제작 재료.",
                        category = PlayerInventory.ItemCategory.Material,
                        maxStack = 99
                    };
                case ResourceType.IronOre:
                    return new PlayerInventory.ItemData
                    {
                        id = "iron_ore",
                        displayName = "철광석",
                        description = "제련하여 철괴를 만들 수 있는 광석.",
                        category = PlayerInventory.ItemCategory.Material,
                        maxStack = 99
                    };
                case ResourceType.Silver:   // [Milestone C] 희귀 광물 노드
                    return new PlayerInventory.ItemData
                    {
                        id = "mat_silver_ore", displayName = "은광석",
                        description = "희귀한 은 광석. 고급 장비·장신구 재료.",
                        category = PlayerInventory.ItemCategory.Material, maxStack = 99
                    };
                case ResourceType.Gold:     // [Milestone C]
                    return new PlayerInventory.ItemData
                    {
                        id = "mat_gold_ore", displayName = "금광석",
                        description = "귀한 금 광석. 희귀 장비·장신구 재료.",
                        category = PlayerInventory.ItemCategory.Material, maxStack = 99
                    };
                case ResourceType.Mythril:  // [Milestone C]
                    return new PlayerInventory.ItemData
                    {
                        id = "mat_mythril_ore", displayName = "미스릴 광석",
                        description = "전설급 미스릴. 최상위 장비 재료.",
                        category = PlayerInventory.ItemCategory.Material, maxStack = 99
                    };
                default:
                    Debug.LogError($"[ResourceNode] 알 수 없는 ResourceType: {_resourceType}");
                    return null;
            }
        }
    }
}