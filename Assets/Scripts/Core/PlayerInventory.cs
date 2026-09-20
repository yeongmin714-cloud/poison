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
            public bool isBomb = false; // 폭탄 — 퀵슬롯 사용 시 무장(소모 금지), 좌클릭 투척
            public int basePrice = 0; // O2 C-O2-02: 기본가 (0 = 무가격 → EconomyPricing 등급표 폴백. 기존 정의 하위호환)
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
            Accessory,  // [Milestone B] 장신구 (반지/목걸이 — %버프)
            Tool,       // 도구
            Arrow,      // 화살 (AB-01)
            Bomb        // 폭탄 (퀵슬롯 무장/투척 전용)
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
            // [폭탄] 폭탄 아이템 — 소모하지 않고 그대로 반환.
            // 무장/해제는 UI 계층(QuickSlotUI)이 BombArmController를 호출해 담당한다.
            if (slot.item.isBomb)
            {
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

        // ===== P4 농사/채집 (Farming/Gathering) 자원 연결 =====
        // 약초(농사 수확 + 채집 산출)와 약초씨(밭 파종 소모).
        // GetItemById의 _idLookupCache는 public static ItemData 필드를 1회 리플렉션 스캔하므로,
        // 아래 static 필드만 추가하면 자동으로 id→ItemData 조회에 포함된다.
        public static readonly ItemData Herb_Yakcho = new ItemData { id = "herb_yakcho", displayName = "약초", description = "들판에서 자라는 기본 약초. 밭 수확/채집으로 얻는다.", category = ItemCategory.Herb,    maxStack = 99 };
        public static readonly ItemData Seed_Herb    = new ItemData { id = "seed_herb",   displayName = "약초씨",  description = "밭(FarmPlot)에 파종해 약초를 재배하는 씨앗.",  category = ItemCategory.Material, maxStack = 20 };

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

        // ===== [Milestone B] 광물 재료 (채광 산출 → 무기/방어구/장신구 재료) =====
        public static readonly ItemData Mat_Wood  = new ItemData { id = "mat_wood",     displayName = "통나무", description = "채광/벌목으로 얻는 기본 목재. Wood 티어 장비 재료.", category = ItemCategory.Material, maxStack = 99 };
        public static readonly ItemData Mat_Stone = new ItemData { id = "mat_stone",    displayName = "석재",   description = "채광으로 얻는 돌. Stone 티어 장비 재료.",           category = ItemCategory.Material, maxStack = 99 };
        public static readonly ItemData IronOre   = new ItemData { id = "iron_ore",     displayName = "철광석", description = "채광으로 얻는 철광. Steel/Chain 티어 재료.",        category = ItemCategory.Material, maxStack = 99, rarity = ItemRarity.Uncommon };
        public static readonly ItemData IronIngot = new ItemData { id = "iron_ingot",   displayName = "철괴",   description = "철광석을 정련한 것. 강철 장비 재료.",             category = ItemCategory.Material, maxStack = 99, rarity = ItemRarity.Uncommon };
        public static readonly ItemData SilverOre = new ItemData { id = "mat_silver_ore", displayName = "은광석", description = "희귀한 은 광석. 은 티어·장신구 재료.",           category = ItemCategory.Material, maxStack = 99, rarity = ItemRarity.Uncommon };
        public static readonly ItemData GoldOre   = new ItemData { id = "mat_gold_ore",   displayName = "금광석", description = "귀한 금 광석. 희귀 장비·장신구 재료.",            category = ItemCategory.Material, maxStack = 99, rarity = ItemRarity.Rare };
        public static readonly ItemData MythrilOre= new ItemData { id = "mat_mythril_ore",displayName = "미스릴 광석", description = "전설급 미스릴. 최상위 장비 재료.",             category = ItemCategory.Material, maxStack = 99, rarity = ItemRarity.Epic };
        public static readonly ItemData CrystalShard = new ItemData { id = "crystal_shard", displayName = "수정석", description = "마력이 깃든 수정. 크리스탈 티어 재료.",          category = ItemCategory.Material, maxStack = 99, rarity = ItemRarity.Rare };

        // ===== [Milestone B] 장신구 (반지/목걸이 — %버프, AccessoryDefinitions에 수치 맵) =====
        public static readonly ItemData Ring_Evasion  = new ItemData { id = "ring_evasion",  displayName = "민첩 반지", description = "회피율 +3%.",        category = ItemCategory.Accessory, maxStack = 1, rarity = ItemRarity.Uncommon };
        public static readonly ItemData Ring_Vitality = new ItemData { id = "ring_vitality", displayName = "생명 반지", description = "최대 체력 +10%.",     category = ItemCategory.Accessory, maxStack = 1, rarity = ItemRarity.Uncommon };
        public static readonly ItemData Ring_Power    = new ItemData { id = "ring_power",    displayName = "힘의 반지", description = "공격력 +6%.",        category = ItemCategory.Accessory, maxStack = 1, rarity = ItemRarity.Rare };
        public static readonly ItemData Necklace_HP   = new ItemData { id = "necklace_hp",   displayName = "체력 목걸이", description = "최대 체력 +15%.",  category = ItemCategory.Accessory, maxStack = 1, rarity = ItemRarity.Rare };
        public static readonly ItemData Necklace_Guard= new ItemData { id = "necklace_guard",displayName = "수호 목걸이", description = "방어력 +8%.",      category = ItemCategory.Accessory, maxStack = 1, rarity = ItemRarity.Rare };

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
        // 폭탄 아이템 (BombArmController 투척 전용)
        //   퀵슬롯 등록 → 번호키 무장 → 좌클릭 투척 → 퓨즈 후 폭발.
        //   ItemData.isBomb 분기 — UseItem에서 무장/해제 처리, 소모하지 않음.
        // ================================================================
        public static readonly ItemData Bomb_Explosive = new ItemData
        {
            id = "bomb_explosive",
            displayName = "폭탄",
            description = "심지를 뽑아 던지면 짧은 시간 뒤 폭발하는 폭탄. 퀵슬롯에 등록 후 번호키로 들고, 좌클릭으로 투척한다.",
            category = ItemCategory.Bomb,
            maxStack = 5,
            rarity = ItemRarity.Rare,
            isBomb = true
        };
        public static readonly ItemData[] AllBombs = new ItemData[]
        {
            Bomb_Explosive,
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
        // [TEST28-69차] 부츠/장갑 통합 아이템 — 좌/우 분리 → 단일 아이템(장착 시 양발/양손 자동 부착).
        //   GLB는 좌우 파일이 별도(wood_boot_left/right.glb) → ArmorVisualAttachSystem이 본 좌우에 맞춰 선택 부착.
        public static readonly ItemData BootWood    = Tiered("wood_boot",    "나무 신발",  "나무 각반 — 양발 착용.",   ItemCategory.Armor, ItemRarity.Common,    20);
        public static readonly ItemData BootSteel   = Tiered("steel_boot",   "강철 신발",  "강철 각반 — 양발 착용.",   ItemCategory.Armor, ItemRarity.Uncommon,  40);
        public static readonly ItemData BootStone   = Tiered("stone_boot",   "돌 신발",    "석재 각반 — 양발 착용.",   ItemCategory.Armor, ItemRarity.Rare,      60);
        public static readonly ItemData BootCrystal = Tiered("crystal_boot", "수정 신발",  "수정 각반 — 양발 착용.",   ItemCategory.Armor, ItemRarity.Epic,      80);
        public static readonly ItemData GloveWood    = Tiered("wood_glove",    "나무 장갑",  "나무 손 보호구 — 양손 착용.",  ItemCategory.Armor, ItemRarity.Common,    20);
        public static readonly ItemData GloveSteel   = Tiered("steel_glove",   "강철 장갑",  "강철 장갑 — 양손 착용.",       ItemCategory.Armor, ItemRarity.Uncommon,  40);
        public static readonly ItemData GloveStone   = Tiered("stone_glove",   "돌 장갑",    "석재 장갑 — 양손 착용.",       ItemCategory.Armor, ItemRarity.Rare,      60);
        public static readonly ItemData GloveCrystal = Tiered("crystal_glove", "수정 장갑",  "수정 장갑 — 양손 착용.",       ItemCategory.Armor, ItemRarity.Epic,      80);
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
            // 방어구 17종 — [TEST28-69차] 부츠/장갑 좌우 분리 → 통합 아이템(장착 시 양발/양손 부착)
            ArmorWood, ArmorSteel, ArmorStone, ArmorCrystal,
            HelmetWood, HelmetSteel, HelmetStone, HelmetCrystal,
            BootWood, BootSteel, BootStone, BootCrystal,
            GloveWood, GloveSteel, GloveStone, GloveCrystal,
            ShieldWood,
            // 부속 6종
            GasMaskWood, GasMaskSteel, GasMaskStone,
            ChemicalPackWood, ChemicalPackSteel, ChemicalPackStone,
        };

        // 2026-09-13(P5): id → ItemData 정적 조회 캐시 — GetItemById 전용.
        // static readonly 필드를 1회 리플렉션 스캔해 구축(EquipmentManager.BuildItemCache와 동일 방식).
        private static System.Collections.Generic.Dictionary<string, ItemData> _idLookupCache;

        /// <summary>
        /// 아이템 ID로 정의된 ItemData를 조회 (인스턴스/인벤 슬롯과 무관한 정적 정의 조회).
        /// 2026-09-13(P5) 추가: EquipmentManager.SetWeaponSlot의 무기 슬롯 itemData 채움 전용.
        /// 찾지 못하면 null 반환.
        /// </summary>
        public static ItemData GetItemById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_idLookupCache == null)
            {
                _idLookupCache = new System.Collections.Generic.Dictionary<string, ItemData>();
                var fields = typeof(PlayerInventory).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                foreach (var field in fields)
                {
                    if (field.FieldType != typeof(ItemData)) continue;
                    var item = field.GetValue(null) as ItemData;
                    if (item != null && !string.IsNullOrEmpty(item.id) && !_idLookupCache.ContainsKey(item.id))
                        _idLookupCache[item.id] = item;
                }
            }
            _idLookupCache.TryGetValue(itemId, out var found);
            return found;
        }
    }
}