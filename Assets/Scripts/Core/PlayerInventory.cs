using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Core
{
    /// <summary>
    /// 인벤토리 데이터 모델 — Phase 2 튜토리얼용.
    /// 싱글톤으로 게임 전체에서 접근 가능.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        public static PlayerInventory Instance { get; private set; }

        [System.Serializable]
        public class ItemSlot
        {
            public ItemData item;
            public int count;
            public int currentDurability; // 현재 내구도 (0이면 파괴)
        }

        [System.Serializable]
        public class ItemData
        {
            public string id;
            public string displayName;
            public string description;
            public ItemCategory category;
            public Sprite icon;
            public int maxStack = 99;
            public int maxDurability = 0; // 0 = 내구도 없음 (소모품)
            public ItemRarity rarity = ItemRarity.Common;
            public string effects = "";
        }

        public enum ItemCategory
        {
            Herb,       // 약초
            Meat,       // 고기
            Food,       // 요리
            Potion,     // 약
            Material,   // 재료
            Drug,       // 마약
            Quest,      // 퀘스트 아이템
            Weapon,     // 무기
            Armor,      // 방어구
            Tool,       // 도구
            Arrow       // 화살 (AB-01)
        }

        [SerializeField] private int _maxSlots = 40;
        private ItemSlot[] _slots;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _slots = new ItemSlot[_maxSlots];
        }

        /// <summary>
        /// 아이템 추가. 성공하면 true, 가득 찼으면 false.
        /// </summary>
        public bool AddItem(ItemData item, int count = 1)
        {
            if (item == null)
            {
                Debug.LogError("[PlayerInventory] AddItem: item is null!");
                return false;
            }
            if (count <= 0) return true;

            // 같은 아이템이 있는 슬롯 먼저 찾기 (stack)
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slots[i].item != null && _slots[i].item.id == item.id && _slots[i].count < item.maxStack)
                {
                    int space = item.maxStack - _slots[i].count;
                    int add = Mathf.Min(space, count);
                    _slots[i].count += add;
                    count -= add;
                    if (count <= 0) return true;
                }
            }

            // 빈 슬롯 찾기
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = new ItemSlot { item = item, count = Mathf.Min(count, item.maxStack), currentDurability = item.maxDurability };
                    count -= _slots[i].count;
                    if (count <= 0) return true;
                }
            }

            Debug.LogWarning($"[PlayerInventory] 인벤토리 가득 참! {item.displayName} x{count} 못 넣음");
            return false;
        }

        /// <summary>
        /// 아이템 제거. count만큼 제거 후 bool 반환.
        /// </summary>
        public bool RemoveItem(string itemId, int count = 1)
        {
            int remaining = count;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slots[i].item != null && _slots[i].item.id == itemId)
                {
                    int remove = Mathf.Min(remaining, _slots[i].count);
                    _slots[i].count -= remove;
                    remaining -= remove;
                    if (_slots[i].count <= 0)
                        _slots[i] = null;
                    if (remaining <= 0) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 특정 아이템 개수 확인
        /// </summary>
        public int GetItemCount(string itemId)
        {
            int count = 0;
            foreach (var slot in _slots)
            {
                if (slot != null && slot.item != null && slot.item.id == itemId)
                    count += slot.count;
            }
            return count;
        }

        /// <summary>
        /// 전체 인벤토리 슬롯 배열 반환 (UI용)
        /// </summary>
        public ItemSlot[] GetAllSlots() => _slots;

        /// <summary>
        /// 특정 카테고리 아이템만 가져오기
        /// </summary>
        public ItemSlot[] GetSlotsByCategory(ItemCategory category)
        {
            var list = new System.Collections.Generic.List<ItemSlot>();
            foreach (var slot in _slots)
            {
                if (slot != null && slot.item != null && slot.item.category == category)
                    list.Add(slot);
            }
            return list.ToArray();
        }

        /// <summary>
        /// 아이템이 있는지 확인
        /// </summary>
        public bool HasItem(string itemId) => GetItemCount(itemId) > 0;
        /// <summary>
        /// Use (consume) the item in the specified slot.
        /// Removes one count and applies its effect if consumable.
        /// </summary>
        /// <param name="slotIndex">Index of the slot to use</param>
        public void UseItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length)
            {
                Debug.LogWarning($"[PlayerInventory] UseItem: slot index {slotIndex} out of range.");
                return;
            }
            var slot = _slots[slotIndex];
            if (slot == null || slot.item == null)
            {
                Debug.LogWarning($"[PlayerInventory] UseItem: slot {slotIndex} is empty.");
                return;
            }
            // Consume via ConsumableSystem
            ConsumableSystem.UseItem(slot.item);
            // Remove one count
            slot.count--;
            if (slot.count <= 0)
            {
                _slots[slotIndex] = null;
            }
        }
        /// <summary>
        /// 카테고리 내 특정 인덱스의 아이템을 사용 (필터링된 목록 기준)
        /// </summary>
        /// <param name="category">아이템 카테고리</param>
        /// <param name="indexInCategory">해당 카테고리 내에서의 인덱스 (0-based)</param>
        public void UseItemFromCategory(PlayerInventory.ItemCategory category, int indexInCategory)
        {
            if (indexInCategory < 0) return;
            int count = -1;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot != null && slot.item != null && slot.item.category == category)
                {
                    count++;
                    if (count == indexInCategory)
                    {
                        UseItem(i);
                        break;
                    }
                }
            }
        }


        // ===== 편의 정적 아이템 데이터 =====
        public static readonly ItemData Herb_Red    = new ItemData { id = "herb_red",    displayName = "치유초",   description = "빨간 약초. 기본 치료 성분.",   category = ItemCategory.Herb,    maxStack = 20 };
        public static readonly ItemData Herb_Purple = new ItemData { id = "herb_purple", displayName = "독나물",   description = "보라색 약초. 독성 성분.",     category = ItemCategory.Herb,    maxStack = 20 };
        public static readonly ItemData Herb_Yellow = new ItemData { id = "herb_yellow", displayName = "황혼초",   description = "노란 약초. 환각/마취 성분.",   category = ItemCategory.Herb,    maxStack = 20 };
        public static readonly ItemData Herb_Silver = new ItemData { id = "herb_silver", displayName = "은빛 이끼", description = "은색 이끼. 해독 성분.",        category = ItemCategory.Herb,    maxStack = 20 };
        public static readonly ItemData Herb_Green  = new ItemData { id = "herb_green",  displayName = "피어리",   description = "초록 약초. 재생/회복 성분.",   category = ItemCategory.Herb,    maxStack = 20 };

        public static readonly ItemData RabbitMeat  = new ItemData { id = "meat_rabbit", displayName = "토끼고기",  description = "작고 부드러운 고기.",          category = ItemCategory.Meat,    maxStack = 20 };
        public static readonly ItemData BoarMeat    = new ItemData { id = "meat_boar",   displayName = "멧돼지고기", description = "걸쭉한 맛이 나는 고기.",        category = ItemCategory.Meat,    maxStack = 20 };
        public static readonly ItemData WolfMeat    = new ItemData { id = "meat_wolf",   displayName = "늑대고기",  description = "담백한 늑대 고기.",            category = ItemCategory.Meat,    maxStack = 20 };

        // 몬스터 추가 드롭 재료
        public static readonly ItemData RabbitFur   = new ItemData { id = "mat_rabbit_fur",  displayName = "토끼털",     description = "부드러운 토끼 털. 기본 방어구 재료.", category = ItemCategory.Material, maxStack = 20 };
        public static readonly ItemData BoarLeather = new ItemData { id = "mat_boar_leather", displayName = "멧돼지 가죽", description = "질긴 멧돼지 가죽. 중급 방어구 재료.",   category = ItemCategory.Material, maxStack = 20 };
        public static readonly ItemData BoarTusk    = new ItemData { id = "mat_boar_tusk",    displayName = "멧돼지 엄니", description = "날카로운 멧돼지 엄니. 기본 무기 재료.",   category = ItemCategory.Material, maxStack = 10 };
        public static readonly ItemData WolfTooth   = new ItemData { id = "mat_wolf_tooth",   displayName = "늑대 이빨",   description = "날카로운 늑대 이빨. 날카로운 무기 재료.", category = ItemCategory.Material, maxStack = 10 };
        public static readonly ItemData WolfFur     = new ItemData { id = "mat_wolf_fur",     displayName = "늑대 모피",   description = "고급 늑대 모피. 고급 방어구 재료.",     category = ItemCategory.Material, maxStack = 10 };

        public static readonly ItemData EstateDeed  = new ItemData { id = "quest_deed",  displayName = "영지 증서", description = "튜토리얼 영주의 영지 증서.",     category = ItemCategory.Quest,   maxStack = 1 };
        public static readonly ItemData Gold    = new ItemData { id = "gold",    displayName = "금",    description = "통화",    category = ItemCategory.Material,    maxStack = 99 };

        // ===== C9-06: 무기/방어구/도구 =====
        // 기본 무기 (재료로 구매)
        public static readonly ItemData SwordWood  = new ItemData { id = "weapon_sword_wood",  displayName = "목검",   description = "나무로 만든 검. 기본 무기.",   category = ItemCategory.Weapon, maxStack = 1, maxDurability = 20 };
        public static readonly ItemData SpearWood  = new ItemData { id = "weapon_spear_wood",  displayName = "나무 창", description = "나무로 만든 창. 약간 긴 사거리.", category = ItemCategory.Weapon, maxStack = 1, maxDurability = 20 };
        public static readonly ItemData BowWood    = new ItemData { id = "weapon_bow_wood",    displayName = "나무 활", description = "나무로 만든 활. 원거리 공격.",   category = ItemCategory.Weapon, maxStack = 1, maxDurability = 20 };

        // 기본 방어구
        public static readonly ItemData LeatherArmor = new ItemData { id = "armor_leather",    displayName = "가죽 갑옷", description = "동물 가죽으로 만든 방어구.", category = ItemCategory.Armor,  maxStack = 1, maxDurability = 30 };
        public static readonly ItemData ClothArmor   = new ItemData { id = "armor_cloth",      displayName = "천 옷",    description = "천으로 만든 가벼운 옷.",     category = ItemCategory.Armor,  maxStack = 1, maxDurability = 15 };

        // 기본 도구
        public static readonly ItemData Pickaxe      = new ItemData { id = "tool_pickaxe",     displayName = "곡괭이",  description = "광석 채굴용 도구.",          category = ItemCategory.Tool,   maxStack = 1, maxDurability = 30 };
        public static readonly ItemData Axe          = new ItemData { id = "tool_axe",         displayName = "도끼",     description = "벌목용 도구.",               category = ItemCategory.Tool,   maxStack = 1, maxDurability = 30 };
        public static readonly ItemData FishingRod   = new ItemData { id = "tool_fishing_rod", displayName = "낚싯대",   description = "낚시용 도구.",               category = ItemCategory.Tool,   maxStack = 1, maxDurability = 20 };
    }
}