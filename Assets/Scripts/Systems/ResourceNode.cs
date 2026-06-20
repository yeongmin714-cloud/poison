using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-28: 자원 노드 — 광부가 채광할 수 있는 Wood/Stone/IronOre 노드
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        public enum ResourceType { Wood, Stone, IronOre }

        [SerializeField] private ResourceType _resourceType = ResourceType.Wood;
        [SerializeField] private int _minYield = 1;
        [SerializeField] private int _maxYield = 3;
        [SerializeField] private float _respawnTime = 15f;
        private bool _isDepleted = false;

        public bool IsAvailable => !_isDepleted;
        public ResourceType NodeType => _resourceType;

        /// <summary>Miner auto-mine — returns resources directly</summary>
        public bool TryAutoMine(out PlayerInventory.ItemData item, out int yield)
        {
            item = null;
            yield = 0;
            if (_isDepleted) return false;

            _isDepleted = true;
            yield = Random.Range(_minYield, _maxYield + 1);
            item = GetItemData();

            // Hide visual
            var r = GetComponent<Renderer>();
            if (r) r.enabled = false;
            var c = GetComponent<Collider>();
            if (c) c.enabled = false;

            Invoke(nameof(Respawn), _respawnTime);
            return true;
        }

        private void Respawn()
        {
            _isDepleted = false;
            var r = GetComponent<Renderer>();
            if (r) r.enabled = true;
            var c = GetComponent<Collider>();
            if (c) c.enabled = true;
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
                    return null;
            }
        }
    }
}