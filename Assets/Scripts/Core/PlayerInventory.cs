using UnityEngine;
using ProjectName.Core.Data;
#pragma warning disable 0414

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
            // === 단순화: 첫 번째가 주인 ===
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // NOTE: Player 오브젝트는 씬에 있어야 함. DontDestroyOnLoad 제거
            // DontDestroyOnLoad(gameObject);
            _slots = new ItemSlot[_maxSlots];
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

        // ===== 씨앗 아이템 (약초 채집 시 확률 드랍, FarmPlot 파종 시 1개 소모) =====
        // ItemCategory에 Seed 전용 항목이 없어 Material 사용 — id("herb_seed_*")와 displayName으로 식별
        public static readonly ItemData Seed_Red    = new ItemData { id = "herb_seed_red",    displayName = "치유초 씨앗",   description = "치유초를 심을 수 있는 씨앗. 밭에 파종해 재배한다.",  category = ItemCategory.Material, maxStack = 20 };
        public static readonly ItemData Seed_Purple = new ItemData { id = "herb_seed_purple", displayName = "독나물 씨앗",   description = "독나물을 심을 수 있는 씨앗. 밭에 파종해 재배한다.",  category = ItemCategory.Material, maxStack = 20 };
        public static readonly ItemData Seed_Yellow = new ItemData { id = "herb_seed_yellow", displayName = "황혼초 씨앗",   description = "황혼초를 심을 수 있는 씨앗. 밭에 파종해 재배한다.",  category = ItemCategory.Material, maxStack = 20 };
        public static readonly ItemData Seed_Silver = new ItemData { id = "herb_seed_silver", displayName = "은빛 이끼 씨앗", description = "은빛 이끼의 희귀 씨앗. 밭에 파종해 재배한다.",      category = ItemCategory.Material, maxStack = 20, rarity = ItemRarity.Rare };
        public static readonly ItemData Seed_Green  = new ItemData { id = "herb_seed_green",  displayName = "피어리 씨앗",   description = "피어리의 희귀 씨앗. 밭에 파종해 재배한다.",        category = ItemCategory.Material, maxStack = 20, rarity = ItemRarity.Rare };

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

        // ===== 물고기 아이템 =====
        public static readonly ItemData Fish_Common = new ItemData
        {
            id = "fish_common",
            displayName = "🐟 붕어",
            description = "평범한 붕어.",
            category = ItemCategory.Material,
            maxStack = 99
        };

        public static readonly ItemData Fish_Rare = new ItemData
        {
            id = "fish_rare",
            displayName = "🐠 황금송어",
            description = "희귀한 황금송어.",
            category = ItemCategory.Material,
            maxStack = 99
        };

        public static readonly ItemData Fish_Legendary = new ItemData
        {
            id = "fish_legendary",
            displayName = "🐉 전설의 물고기",
            description = "전설의 물고기!",
            category = ItemCategory.Material,
            maxStack = 10
        };

        public static readonly ItemData FishingRodItem = new ItemData
        {
            id = "fishing_rod",
            displayName = "🎣 낚시대",
            description = "낚시용 도구.",
            category = ItemCategory.Tool,
            maxStack = 1,
            maxDurability = 20
        };

        // ===== Phase 34: 은신 장비 아이템 =====
        public static readonly ItemData StealthBoots = new ItemData { id = "stealth_boots",  displayName = "은신 부츠", description = "발소음 50% 감소. 은신 효율 증가.", category = ItemCategory.Armor, maxStack = 1, maxDurability = 40 };
        public static readonly ItemData DarkCloak    = new ItemData { id = "dark_cloak",     displayName = "어두운 망토", description = "야간 감지 거리 50% 감소.",      category = ItemCategory.Armor, maxStack = 1, maxDurability = 40 };
        public static readonly ItemData StealthPotion = new ItemData { id = "potion_stealth", displayName = "은신 물약", description = "10초간 반투명 + 발소음 제로.",  category = ItemCategory.Potion, maxStack = 10 };
        public static readonly ItemData Sedative      = new ItemData { id = "potion_sedative", displayName = "진정제",   description = "NPC 5초 행동불능.",            category = ItemCategory.Potion, maxStack = 10 };

        // ===== 말 소환 아이템 (Phase 4) =====
        public static readonly ItemData MountToken = new ItemData
        {
            id = "mount_token",
            displayName = "🐴 말 소환석",
            description = "사용 시 가장 가까운 길바닥에 말을 소환합니다.",
            category = ItemCategory.Tool,
            maxStack = 1
        };

        // ================================================================
        // 2026-09-11: 4티어 GLB 장비 전종 (wood/steel/stone/crystal)
        //  - GLB: Assets/Resources/Models/UserProvided/{glbKey}.glb
        //  - 무기 id = weapon_{type}_{tier} — GblItemIconRenderer._itemToModel 명시 맵과 일치
        //  - 방어구/부속 id = GLB 파일명 그대로({tier}_{slot}) — 관례 후보 1(id 그대로)로 아이콘 자동 베이크
        //    (GblItemIconRenderer.StripPrefix에 armor_/helmet_ 등 접두사가 없어 armor_{tier} 형태는 GLB 해석 불가)
        //  - rarity/내구도: wood=Common/20, steel=Uncommon/40, stone=Rare/60, crystal=Epic/80
        //  - 방어구 def: EquipmentStatBonusApplier._table (id 매핑) — 좌우 boot/glove는 좌우 동일 def
        //  - 무기 dmg 보정: WeaponData.GetTierMultiplier (wood 1.0 / steel 1.8 / stone 2.5 / crystal 3.75)
        //  - 부속 GLB 부재(id 미정의): crystal_shield, steel/stone_shield, crystal_gas_mask, crystal_chemical_pack
        // ================================================================
        private static ItemData Tiered(string id, string name, string desc, ItemCategory cat, ItemRarity rarity, int durability)
            => new ItemData { id = id, displayName = name, description = desc, category = cat, rarity = rarity, maxStack = 1, maxDurability = durability };

        // ── 무기 신규 13종 (wood 3종은 위 기존 정의 사용) ──
        public static readonly ItemData DaggerWood    = Tiered("weapon_dagger_wood",    "나무 단도", "나무로 깎은 단도. 가볍고 빠른 근접 무기.",      ItemCategory.Weapon, ItemRarity.Common,    20);
        public static readonly ItemData SwordSteel    = Tiered("weapon_sword_steel",    "강철검",   "강철로 벼린 검. 단단하고 강하다.",              ItemCategory.Weapon, ItemRarity.Uncommon,  40);
        public static readonly ItemData SpearSteel    = Tiered("weapon_spear_steel",    "강철창",   "강철촉 창. 뚫고 나가는 긴 사거리.",             ItemCategory.Weapon, ItemRarity.Uncommon,  40);
        public static readonly ItemData BowSteel      = Tiered("weapon_bow_steel",      "강철활",   "강철 활대의 활. 강한 시위 장력.",               ItemCategory.Weapon, ItemRarity.Uncommon,  40);
        public static readonly ItemData DaggerSteel   = Tiered("weapon_dagger_steel",   "강철단도", "강철로 벼린 단도. 빠른 연격.",                  ItemCategory.Weapon, ItemRarity.Uncommon,  40);
        public static readonly ItemData SwordStone    = Tiered("weapon_sword_stone",    "돌검",     "단단한 암석을 깎은 검. 묵직한 일격.",           ItemCategory.Weapon, ItemRarity.Rare,      60);
        public static readonly ItemData SpearStone    = Tiered("weapon_spear_stone",    "돌창",     "돌촉 창. 무겁지만 강한 찌르기.",                ItemCategory.Weapon, ItemRarity.Rare,      60);
        public static readonly ItemData BowStone      = Tiered("weapon_bow_stone",      "돌활",     "돌 화살촉을 쓰는 활. 관통력이 높다.",           ItemCategory.Weapon, ItemRarity.Rare,      60);
        public static readonly ItemData DaggerStone   = Tiered("weapon_dagger_stone",   "돌단도",   "돌로 갈아 만든 단도. 잔인한 날.",               ItemCategory.Weapon, ItemRarity.Rare,      60);
        public static readonly ItemData SwordCrystal  = Tiered("weapon_sword_crystal",  "수정검",   "수정으로 빚은 검. 신비한 힘이 깃들었다.",       ItemCategory.Weapon, ItemRarity.Epic,      80);
        public static readonly ItemData SpearCrystal  = Tiered("weapon_spear_crystal",  "수정창",   "수정촉 창. 빛을 반사하는 창신.",                ItemCategory.Weapon, ItemRarity.Epic,      80);
        public static readonly ItemData BowCrystal    = Tiered("weapon_bow_crystal",    "수정활",   "수정 활대의 활. 마력을 담은 화살.",             ItemCategory.Weapon, ItemRarity.Epic,      80);
        public static readonly ItemData DaggerCrystal = Tiered("weapon_dagger_crystal", "수정단도", "수정으로 빚은 단도. 가장 예리한 칼날.",         ItemCategory.Weapon, ItemRarity.Epic,      80);

        // ── 방어구 25종 (id = GLB 파일명: 상의4/투구4/신발좌우8/장갑좌우8/방패 wood만) ──
        public static readonly ItemData ArmorWood     = Tiered("wood_armor",     "나무 갑옷",   "나무 판을 엮은 기본 갑옷.",                 ItemCategory.Armor, ItemRarity.Common,    20);
        public static readonly ItemData ArmorSteel    = Tiered("steel_armor",    "강철 갑옷",   "강철 판갑옷. 튼튼한 방어력.",               ItemCategory.Armor, ItemRarity.Uncommon,  40);
        public static readonly ItemData ArmorStone    = Tiered("stone_armor",    "돌 갑옷",     "석판으로 짠 무거운 갑옷.",                  ItemCategory.Armor, ItemRarity.Rare,      60);
        public static readonly ItemData ArmorCrystal  = Tiered("crystal_armor",  "수정 갑옷",   "수정 결정으로 빚은 최고급 갑옷.",           ItemCategory.Armor, ItemRarity.Epic,      80);
        public static readonly ItemData HelmetWood    = Tiered("wood_helmet",    "나무 투구",   "나무를 깎은 기본 투구.",                    ItemCategory.Armor, ItemRarity.Common,    20);
        public static readonly ItemData HelmetSteel   = Tiered("steel_helmet",   "강철 투구",   "강철 두부 보호구.",                         ItemCategory.Armor, ItemRarity.Uncommon,  40);
        public static readonly ItemData HelmetStone   = Tiered("stone_helmet",   "돌 투구",     "석재 헬멧. 머리를 단단히 지킨다.",          ItemCategory.Armor, ItemRarity.Rare,      60);
        public static readonly ItemData HelmetCrystal = Tiered("crystal_helmet", "수정 투구",   "수정으로 빚은 투구. 신비한 보호막.",        ItemCategory.Armor, ItemRarity.Epic,      80);
        public static readonly ItemData BootWoodLeft    = Tiered("wood_boot_left",    "나무 신발 (왼쪽)",  "나무 각반 — 왼발용.",       ItemCategory.Armor, ItemRarity.Common,    20);
        public static readonly ItemData BootWoodRight   = Tiered("wood_boot_right",   "나무 신발 (오른쪽)", "나무 각반 — 오른발용.",     ItemCategory.Armor, ItemRarity.Common,    20);
        public static readonly ItemData BootSteelLeft   = Tiered("steel_boot_left",   "강철 신발 (왼쪽)",  "강철 각반 — 왼발용.",       ItemCategory.Armor, ItemRarity.Uncommon,  40);
        public static readonly ItemData BootSteelRight  = Tiered("steel_boot_right",  "강철 신발 (오른쪽)", "강철 각반 — 오른발용.",     ItemCategory.Armor, ItemRarity.Uncommon,  40);
        public static readonly ItemData BootStoneLeft   = Tiered("stone_boot_left",   "돌 신발 (왼쪽)",    "석재 각반 — 왼발용.",       ItemCategory.Armor, ItemRarity.Rare,      60);
        public static readonly ItemData BootStoneRight  = Tiered("stone_boot_right",  "돌 신발 (오른쪽)",  "석재 각반 — 오른발용.",     ItemCategory.Armor, ItemRarity.Rare,      60);
        public static readonly ItemData BootCrystalLeft  = Tiered("crystal_boot_left",  "수정 신발 (왼쪽)",  "수정 각반 — 왼발용.",      ItemCategory.Armor, ItemRarity.Epic,      80);
        public static readonly ItemData BootCrystalRight = Tiered("crystal_boot_right", "수정 신발 (오른쪽)", "수정 각반 — 오른발용.",    ItemCategory.Armor, ItemRarity.Epic,      80);
        public static readonly ItemData GloveWoodLeft     = Tiered("wood_glove_left",     "나무 장갑 (왼쪽)",   "나무 손 보호구 — 왼손용.",    ItemCategory.Armor, ItemRarity.Common,    20);
        public static readonly ItemData GloveWoodRight    = Tiered("wood_glove_right",    "나무 장갑 (오른쪽)", "나무 손 보호구 — 오른손용.",  ItemCategory.Armor, ItemRarity.Common,    20);
        public static readonly ItemData GloveSteelLeft    = Tiered("steel_glove_left",    "강철 장갑 (왼쪽)",   "강철 장갑 — 왼손용.",         ItemCategory.Armor, ItemRarity.Uncommon,  40);
        public static readonly ItemData GloveSteelRight   = Tiered("steel_glove_right",   "강철 장갑 (오른쪽)", "강철 장갑 — 오른손용.",       ItemCategory.Armor, ItemRarity.Uncommon,  40);
        public static readonly ItemData GloveStoneLeft    = Tiered("stone_glove_left",    "돌 장갑 (왼쪽)",     "석재 장갑 — 왼손용.",         ItemCategory.Armor, ItemRarity.Rare,      60);
        public static readonly ItemData GloveStoneRight   = Tiered("stone_glove_right",   "돌 장갑 (오른쪽)",   "석재 장갑 — 오른손용.",       ItemCategory.Armor, ItemRarity.Rare,      60);
        public static readonly ItemData GloveCrystalLeft  = Tiered("crystal_glove_left",  "수정 장갑 (왼쪽)",   "수정 장갑 — 왼손용.",         ItemCategory.Armor, ItemRarity.Epic,      80);
        public static readonly ItemData GloveCrystalRight = Tiered("crystal_glove_right", "수정 장갑 (오른쪽)", "수정 장갑 — 오른손용.",       ItemCategory.Armor, ItemRarity.Epic,      80);
        // shield는 wood만 GLB 존재(wood_shield.glb) — steel/stone/crystal_shield.glb 부재로 미정의
        public static readonly ItemData ShieldWood    = Tiered("wood_shield",    "나무 방패",   "두꺼운 나무 방패. 전방 막기용.",            ItemCategory.Armor, ItemRarity.Common,    20);

        // ── 부속 6종 (gas_mask/chemical_pack — crystal GLB 부재로 3티어만) ──
        public static readonly ItemData GasMaskWood         = Tiered("wood_gas_mask",         "나무 방독면",     "나무로 만든 기본 방독면.",           ItemCategory.Armor, ItemRarity.Common,    20);
        public static readonly ItemData GasMaskSteel        = Tiered("steel_gas_mask",        "강철 방독면",     "강철 필터 방독면. 유해 가스 차단.",  ItemCategory.Armor, ItemRarity.Uncommon,  40);
        public static readonly ItemData GasMaskStone        = Tiered("stone_gas_mask",        "돌 방독면",       "석재 마스크. 무겁지만 단단하다.",    ItemCategory.Armor, ItemRarity.Rare,      60);
        public static readonly ItemData ChemicalPackWood    = Tiered("wood_chemical_pack",    "나무 화학장비",   "나무 상자의 기본 화학 장비.",        ItemCategory.Armor, ItemRarity.Common,    20);
        public static readonly ItemData ChemicalPackSteel   = Tiered("steel_chemical_pack",   "강철 화학장비",   "강철 밀폐 화학장비. 유독 물질 취급.", ItemCategory.Armor, ItemRarity.Uncommon,  40);
        public static readonly ItemData ChemicalPackStone   = Tiered("stone_chemical_pack",   "돌 화학장비",     "석재 용기 화학장비. 내식성이 높다.", ItemCategory.Armor, ItemRarity.Rare,      60);

        /// <summary>4티어 GLB 장비 전종 (기존 wood 무기 3종 포함 47종) — 창고 시딩/테스트 순회용.</summary>
        public static readonly ItemData[] AllTieredGear = new ItemData[]
        {
            // 무기 16종
            SwordWood, SpearWood, BowWood, DaggerWood,
            SwordSteel, SpearSteel, BowSteel, DaggerSteel,
            SwordStone, SpearStone, BowStone, DaggerStone,
            SwordCrystal, SpearCrystal, BowCrystal, DaggerCrystal,
            // 방어구 25종
            ArmorWood, ArmorSteel, ArmorStone, ArmorCrystal,
            HelmetWood, HelmetSteel, HelmetStone, HelmetCrystal,
            BootWoodLeft, BootWoodRight, BootSteelLeft, BootSteelRight,
            BootStoneLeft, BootStoneRight, BootCrystalLeft, BootCrystalRight,
            GloveWoodLeft, GloveWoodRight, GloveSteelLeft, GloveSteelRight,
            GloveStoneLeft, GloveStoneRight, GloveCrystalLeft, GloveCrystalRight,
            ShieldWood,
            // 부속 6종
            GasMaskWood, GasMaskSteel, GasMaskStone,
            ChemicalPackWood, ChemicalPackSteel, ChemicalPackStone,
        };
    }
}