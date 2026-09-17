using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 병사 장비 생성기.
    /// 병사 생성 시 자동으로 장비를 생성하여 장착합니다.
    /// 
    /// 흐름:
    ///   1. RarityProbabilityTable.Roll(level) → 등급 결정
    ///   2. LuckyRollSystem.TryLuck(rarity) → 등급 승격 기회
    ///   3. EquipmentPartConfig.RollSlots(level) → 장비 부위 결정
    ///   4. GenerateEquipmentItem(rarity, part) → 아이템 생성
    ///   5. GuardEquipmentSystem.Instance.EquipGuard(guard, slot, item) → 장착
    /// </summary>
    public static class GuardEquipmentSpawner
    {
        /// <summary>
        /// 병사에게 장비를 생성하여 장착합니다.
        /// GuardPlaceholder.Start() 또는 초기화 시점에 호출하세요.
        /// </summary>
        /// <param name="guard">장비를 장착할 병사 GameObject (GuardPlaceholder 컴포넌트 필요)</param>
        /// <param name="guardLevel">병사 레벨</param>
        public static void SpawnEquipment(GameObject guard, int guardLevel)
        {
            // GuardEquipmentSystem 인스턴스가 없으면 동작하지 않음
            if (GuardEquipmentSystem.Instance == null)
            {
                Debug.Log("[GuardEquipmentSpawner] GuardEquipmentSystem.Instance가 없습니다. 장비 생성을 건너뜁니다.");
                return;
            }

            // GuardPlaceholder 컴포넌트 확인
            GuardPlaceholder gp = guard.GetComponent<GuardPlaceholder>();
            if (gp == null)
            {
                Debug.LogWarning("[GuardEquipmentSpawner] GuardPlaceholder 컴포넌트를 찾을 수 없습니다.");
                return;
            }

            // 1. 등급 결정
            ItemRarity rarity = RarityProbabilityTable.Roll(guardLevel);

            // 2. 행운 롤
            rarity = LuckyRollSystem.TryLuck(rarity);

            // 3. 장비 부위 롤
            List<EquipmentPartConfig.EquipmentPart> parts = EquipmentPartConfig.RollSlots(guardLevel);

                        // 4. 각 부위별 장비 생성 및 장착
            foreach (EquipmentPartConfig.EquipmentPart part in parts)
            {
                // 이미 해당 슬롯에 장비가 있으면 건너뛰기
                GuardEquipmentSystem.EquipSlot slot = MapPartToSlot(part);
                GuardEquipmentSystem.EquippedItem existing = GuardEquipmentSystem.Instance.GetGuardEquipped(gp, slot);
                if (existing != null)
                {
                    Debug.Log($"[GuardEquipmentSpawner] {gp.GuardName}의 {slot} 슬롯에 이미 장비가 있어 건너뜁니다.");
                    continue;
                }

                // 아이템 생성
                PlayerInventory.ItemData item = GenerateEquipmentItem(rarity, part, guardLevel);

                // 장착
                bool equipped = GuardEquipmentSystem.Instance.EquipGuard(gp, slot, item);
                if (equipped)
                {
                    Debug.Log($"[GuardEquipmentSpawner] {gp.GuardName}에게 {item.displayName} 장착 완료 (슬롯: {slot})");
                }
            }

            // ===== [후속22] 실착 플레이어 방어구(GLB 비주얼) 로드아웃 =====
            // GuardPlaceholder 장비 필드에 저장 → GuardVisualAttachSystem(시각)과 DropEquippedItems(전리품)가 동일 소스로 사용.
            SpawnPlayerLoadout(gp, guardLevel);
        }

        /// <summary>
        /// 장비 아이템을 생성합니다. 등급과 부위에 따라 스탯이 변동됩니다.
        /// </summary>
        /// <param name="rarity">아이템 등급</param>
        /// <param name="part">장비 부위</param>
        /// <param name="level">병사 레벨 (스탯 스케일링에 사용)</param>
        /// <returns>생성된 아이템 데이터</returns>
        public static PlayerInventory.ItemData GenerateEquipmentItem(ItemRarity rarity, EquipmentPartConfig.EquipmentPart part, int level = 1)
        {
            string rarityStr = rarity.ToString().ToLower();
            string partStr = part.ToString().ToLower();
            string rarityName = EquipmentRarityData.GetRarityDisplayName(rarity);
            string partName = EquipmentPartConfig.GetPartDisplayName(part);

            // 아이템 카테고리 및 기본 정보 결정
            PlayerInventory.ItemCategory category;
            string itemId;
            string description;

            switch (part)
            {
                case EquipmentPartConfig.EquipmentPart.Weapon:
                    category = PlayerInventory.ItemCategory.Weapon;
                    itemId = $"equip_weapon_{rarityStr}_{level}";
                    description = "기본 무기";
                    break;
                case EquipmentPartConfig.EquipmentPart.Head:
                case EquipmentPartConfig.EquipmentPart.Body:
                    // Armor 슬롯 → Armor 카테고리
                    category = PlayerInventory.ItemCategory.Armor;
                    itemId = $"equip_armor_{partStr}_{rarityStr}_{level}";
                    description = "기본 방어구";
                    break;
                case EquipmentPartConfig.EquipmentPart.Hands:
                case EquipmentPartConfig.EquipmentPart.Feet:
                    // Accessory 슬롯 → Material 카테고리 (IsItemValidForSlot 통과용)
                    category = PlayerInventory.ItemCategory.Material;
                    itemId = $"equip_armor_{partStr}_{rarityStr}_{level}";
                    description = "기본 장신구";
                    break;
                default:
                    category = PlayerInventory.ItemCategory.Material;
                    itemId = $"equip_misc_{partStr}_{rarityStr}_{level}";
                    description = "기타 장비";
                    break;
            }

            // 내구도 계산 (등급별 기본 내구도 + 레벨 보정)
            int baseDurability = 30;
            float durabilityMultiplier = EquipmentRarityData.GetStatMultiplier(rarity);
            int maxDurability = Mathf.RoundToInt(baseDurability * durabilityMultiplier * (1f + level * 0.02f));

            // effects 문자열 생성 (stat_bonus 정보 포함)
            float statBonus = 5f * EquipmentRarityData.GetStatMultiplier(rarity) * (1f + level * 0.05f);
            float variance = EquipmentRarityData.GetStatVariance(rarity);
            float actualBonus = statBonus * (1f + (Random.value * 2f - 1f) * variance);

            string effects = "";
            if (part == EquipmentPartConfig.EquipmentPart.Weapon)
            {
                effects = $"stat_bonus:{{\"attack\":{actualBonus:F1}}}";
            }
            else
            {
                effects = $"stat_bonus:{{\"defense\":{actualBonus:F1}}}";
            }

            return new PlayerInventory.ItemData
            {
                id = itemId,
                displayName = $"{rarityName} {partName}",
                description = $"{rarityName} 등급 {partName}. {description}.",
                category = category,
                rarity = rarity,
                maxStack = 1,
                maxDurability = maxDurability,
                effects = effects
            };
        }

        /// <summary>EquipmentPart → GuardEquipmentSystem.EquipSlot 매핑</summary>
        public static GuardEquipmentSystem.EquipSlot MapPartToSlot(EquipmentPartConfig.EquipmentPart part)
        {
            switch (part)
            {
                case EquipmentPartConfig.EquipmentPart.Head:
                case EquipmentPartConfig.EquipmentPart.Body:
                    return GuardEquipmentSystem.EquipSlot.Armor;
                case EquipmentPartConfig.EquipmentPart.Hands:
                case EquipmentPartConfig.EquipmentPart.Feet:
                    return GuardEquipmentSystem.EquipSlot.Accessory;
                case EquipmentPartConfig.EquipmentPart.Weapon:
                    return GuardEquipmentSystem.EquipSlot.Weapon;
                default:
                    return GuardEquipmentSystem.EquipSlot.Accessory;
            }
        }

        /// <summary>
        /// [후속22] 병사가 플레이어 실착 방어구(GLB 비주얼 아이템)를 일부 착용하고 스폰.
        /// 레벨 스케일 등급 + 저레벨 희귀 소확률 (RarityProbabilityTable.Roll + LuckyRollSystem.TryLuck).
        /// 결과를 GuardPlaceholder 장비 필드에 저장 → GuardVisualAttachSystem(시각)와 DropEquippedItems(전리품)가
        /// 동일 소스를 사용해 "보이는 그대로 드랍"된다.
        /// </summary>
        /// <param name="gp">사용할 GuardPlaceholder (필드가 채워짐)</param>
        /// <param name="level">병사 레벨 (등급 테이블 인덱스)</param>
        public static void SpawnPlayerLoadout(GuardPlaceholder gp, int level)
        {
            if (gp == null) return;

            // 부위별 착용 확률 (1보다 크면 항상 착용)
            float helmetChance = 0.65f;
            float armorChance  = 0.80f;
            float bootsChance  = 0.50f;
            float glovesChance = 0.45f;
            float shieldChance = 0.25f;

            if (Random.value < helmetChance) gp.HelmetItem = RollLoadoutItem("helmet", level);
            if (Random.value < armorChance)  gp.ArmorItem  = RollLoadoutItem("armor",  level);
            if (Random.value < bootsChance)  gp.BootsItem  = RollLoadoutItem("boot",   level);
            if (Random.value < glovesChance) gp.GlovesItem = RollLoadoutItem("glove",  level);
            if (Random.value < shieldChance) gp.ShieldItem = RollLoadoutItem("shield", level);
        }

        /// <summary>
        /// [후속22] 부위별 실착 플레이어 방어구 아이템 1개를 롤.
        /// 등급 = RarityProbabilityTable.Roll(level) → LuckyRollSystem.TryLuck 승격 → 티어 매핑
        /// (Common=wood / Uncommon=steel / Rare=stone / Epic+=crystal) → PlayerInventory.GetItemById로 조회.
        /// Shield는 GLB가 wood_shield뿐이므로 항상 wood_shield 반환.
        /// </summary>
        /// <param name="part">"helmet" | "armor" | "boot" | "glove" | "shield"</param>
        /// <param name="level">병사 레벨</param>
        /// <returns>해당 부위 ItemData (없으면 null)</returns>
        public static PlayerInventory.ItemData RollLoadoutItem(string part, int level)
        {
            ItemRarity rarity = RarityProbabilityTable.Roll(level);
            rarity = LuckyRollSystem.TryLuck(rarity);

            // 등급 → 티어 접두 (Epic+는 crystal GLB만 존재)
            string tier;
            switch (rarity)
            {
                case ItemRarity.Common:    tier = "wood";    break;
                case ItemRarity.Uncommon:  tier = "steel";   break;
                case ItemRarity.Rare:      tier = "stone";   break;
                default:                   tier = "crystal"; break; // Epic / Legendary / Unique → crystal
            }

            string itemId;
            if (string.Equals(part, "shield", System.StringComparison.OrdinalIgnoreCase))
                itemId = "wood_shield"; // shield GLB는 wood_shield만 존재
            else
                itemId = tier + "_" + part; // helmet/armor/boot/glove

            return PlayerInventory.GetItemById(itemId);
        }
    }
}