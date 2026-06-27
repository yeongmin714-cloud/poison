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
        public enum ResourceType { Wood, Stone, IronOre }

        [SerializeField, Tooltip("자원 종류")]
        private ResourceType _resourceType = ResourceType.Wood;

        [SerializeField, Tooltip("최소 수확량"), Range(1, 999)]
        private int _minYield = 1;

        [SerializeField, Tooltip("최대 수확량"), Range(1, 999)]
        private int _maxYield = 3;

        [SerializeField, Tooltip("리스폰 시간 (초)"), Min(0.1f)]
        private float _respawnTime = 15f;

        private bool _isDepleted;

        // --- 캐시된 컴포넌트 참조 ---
        private Renderer _renderer;
        private Collider _collider;

        /// <summary>고갈되지 않아 채광 가능한 상태인지 여부</summary>
        public bool IsAvailable => !_isDepleted;
        /// <summary>이 노드의 자원 종류</summary>
        public ResourceType NodeType => _resourceType;

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
                        id = "wood",
                        displayName = "나무",
                        description = "기본 건축 자재.",
                        category = PlayerInventory.ItemCategory.Material,
                        maxStack = 99
                    };
                case ResourceType.Stone:
                    return new PlayerInventory.ItemData
                    {
                        id = "stone",
                        displayName = "돌",
                        description = "기본 건축 자재.",
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
                default:
                    Debug.LogError($"[ResourceNode] 알 수 없는 ResourceType: {_resourceType}");
                    return null;
            }
        }
    }
}