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
    public class GuardEquipmentSpawner : MonoBehaviour
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
                    itemId = $"equip_weapon_{rarity.ToString().ToLower()}_{level}";
                    description = "기본 무기";
                    break;
                case EquipmentPartConfig.EquipmentPart.Head:
                case EquipmentPartConfig.EquipmentPart.Body:
                case EquipmentPartConfig.EquipmentPart.Hands:
                case EquipmentPartConfig.EquipmentPart.Feet:
                    category = PlayerInventory.ItemCategory.Armor;
                    itemId = $"equip_armor_{part.ToString().ToLower()}_{rarity.ToString().ToLower()}_{level}";
                    description = "기본 방어구";
                    break;
                default:
                    category = PlayerInventory.ItemCategory.Material;
                    itemId = $"equip_misc_{part.ToString().ToLower()}_{rarity.ToString().ToLower()}";
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
    }
}