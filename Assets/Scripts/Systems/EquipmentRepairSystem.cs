using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-19: 장비 수리 시스템 — 크래프트 테이블에서 골드 소모 수리.
    /// 
    /// 수리 가능: 무기(Weapon), 방어구(Armor), 강화/특수 방독면(gasmask_3, gasmask_4)
    /// 수리 비용 = (maxDurability - currentDurability) × grade multiplier
    /// 등급 계수: Common=2g, Uncommon=5g, Rare=15g, Epic=50g, Legendary=200g
    /// </summary>
    public static class EquipmentRepairSystem
    {
        /// <summary>등급별 수리비 계수 (Gold per durability point)</summary>
        private static readonly Dictionary<ItemRarity, int> GradeMultipliers = new Dictionary<ItemRarity, int>
        {
            { ItemRarity.Common, 2 },
            { ItemRarity.Uncommon, 5 },
            { ItemRarity.Rare, 15 },
            { ItemRarity.Epic, 50 },
            { ItemRarity.Legendary, 200 }
        };

        /// <summary>
        /// 기본 계수 (등급을 찾을 수 없을 때)
        /// </summary>
        private const int DefaultMultiplier = 5;

        /// <summary>
        /// 등급별 수리비 계수 반환
        /// </summary>
        public static int GetGradeMultiplier(ItemRarity rarity)
        {
            if (GradeMultipliers.TryGetValue(rarity, out int multiplier))
                return multiplier;
            return DefaultMultiplier;
        }

        /// <summary>
        /// 수리 비용 계산: (maxDurability - currentDurability) × grade multiplier
        /// </summary>
        /// <param name="currentDurability">현재 내구도</param>
        /// <param name="maxDurability">최대 내구도</param>
        /// <param name="itemGrade">아이템 등급 (ItemRarity 이름 문자열)</param>
        /// <returns>수리 골드 비용</returns>
        public static int GetRepairCost(int currentDurability, int maxDurability, string itemGrade)
        {
            if (maxDurability <= 0) return 0;

            int durabilityLoss = maxDurability - Mathf.Max(0, currentDurability);
            if (durabilityLoss <= 0) return 0;

            // 문자열 등급을 ItemRarity로 파싱
            ItemRarity rarity = ParseGrade(itemGrade);
            int multiplier = GetGradeMultiplier(rarity);

            return durabilityLoss * multiplier;
        }

        /// <summary>
        /// 수리 비용 계산 (ItemSlot 기반)
        /// </summary>
        public static int GetRepairCost(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null) return 0;
            return GetRepairCost(
                slot.currentDurability,
                slot.item.maxDurability,
                slot.item.rarity.ToString()
            );
        }

        /// <summary>
        /// 수리 가능한 장비인지 확인.
        /// 수리 가능: 무기(Weapon), 방어구(Armor), 강화 방독면(Reinforced), 특수 방독면(Special)
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>수리 가능 여부</returns>
        public static bool CanRepair(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            // 강화/특수 방독면 (gasmask_3 = Reinforced, gasmask_4 = Special)
            if (itemId == "gasmask_3" || itemId == "gasmask_4")
                return true;

            // 인벤토리에서 아이템 데이터 확인
            if (PlayerInventory.Instance != null)
            {
                var slots = PlayerInventory.Instance.GetAllSlots();
                foreach (var slot in slots)
                {
                    if (slot != null && slot.item != null && slot.item.id == itemId)
                    {
                        return CanRepairByCategory(slot.item.category);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 카테고리 기반 수리 가능 체크
        /// </summary>
        private static bool CanRepairByCategory(PlayerInventory.ItemCategory category)
        {
            return category == PlayerInventory.ItemCategory.Weapon
                || category == PlayerInventory.ItemCategory.Armor;
        }

        /// <summary>
        /// 장비 수리 실행.
        /// 성공 시 내구도를 max까지 회복하고 골드를 차감합니다.
        /// </summary>
        /// <param name="itemId">수리할 아이템 ID</param>
        /// <param name="currentDurability">현재 내구도</param>
        /// <param name="maxDurability">최대 내구도</param>
        /// <param name="playerGold">보유 골드 (in/out ref)</param>
        /// <param name="rarity">아이템 등급</param>
        /// <returns>수리 결과</returns>
        public static RepairResult RepairItem(
            string itemId,
            int currentDurability,
            int maxDurability,
            ref int playerGold,
            ItemRarity rarity = ItemRarity.Common)
        {
            // 1. 수리 가능 여부 확인
            if (!CanRepair(itemId))
            {
                return new RepairResult
                {
                    success = false,
                    message = "이 장비는 수리할 수 없습니다."
                };
            }

            // 2. 내구도 확인
            if (maxDurability <= 0)
            {
                return new RepairResult
                {
                    success = false,
                    message = "내구도가 없는 아이템입니다."
                };
            }

            if (currentDurability >= maxDurability)
            {
                return new RepairResult
                {
                    success = false,
                    message = "내구도가 이미 가득 찼습니다."
                };
            }

            // 3. 수리 비용 계산
            int cost = GetRepairCost(currentDurability, maxDurability, rarity.ToString());
            if (cost <= 0)
            {
                return new RepairResult
                {
                    success = false,
                    message = "수리할 필요가 없습니다."
                };
            }

            // 4. 골드 확인
            if (playerGold < cost)
            {
                return new RepairResult
                {
                    success = false,
                    message = $"골드 부족! 필요: {cost}G, 보유: {playerGold}G",
                    goldCost = cost
                };
            }

            // 5. 골드 차감
            playerGold -= cost;

            // 6. 내구도 복구 완료
            return new RepairResult
            {
                success = true,
                message = $"수리 완료! 내구도가 최대치({maxDurability})로 회복되었습니다.",
                newDurability = maxDurability,
                goldCost = cost
            };
        }

        /// <summary>
        /// 인벤토리 기반 장비 수리 실행.
        /// 인벤토리에서 해당 아이템을 찾아 내구도를 회복하고 골드를 차감합니다.
        /// </summary>
        /// <param name="slotIndex">인벤토리 슬롯 인덱스</param>
        /// <returns>수리 결과</returns>
        public static RepairResult RepairInventorySlot(int slotIndex)
        {
            if (PlayerInventory.Instance == null)
            {
                return new RepairResult
                {
                    success = false,
                    message = "인벤토리를 찾을 수 없습니다."
                };
            }

            var slots = PlayerInventory.Instance.GetAllSlots();
            if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
            {
                return new RepairResult
                {
                    success = false,
                    message = "유효하지 않은 슬롯입니다."
                };
            }

            var slot = slots[slotIndex];
            if (slot.item == null)
            {
                return new RepairResult
                {
                    success = false,
                    message = "빈 슬롯입니다."
                };
            }

            // 골드 보유량 확인
            int playerGold = PlayerInventory.Instance.GetItemCount("gold");

            // 수리 실행
            int goldRef = playerGold;
            var result = RepairItem(
                slot.item.id,
                slot.currentDurability,
                slot.item.maxDurability,
                ref goldRef,
                slot.item.rarity
            );

            // 성공 시 인벤토리 업데이트
            if (result.success)
            {
                // 내구도 복구
                slot.currentDurability = result.newDurability;

                // 골드 차감
                int goldSpent = playerGold - goldRef;
                if (goldSpent > 0)
                {
                    PlayerInventory.Instance.RemoveItem("gold", goldSpent);
                }
            }

            return result;
        }

        /// <summary>
        /// 문자열 등급을 ItemRarity로 파싱
        /// </summary>
        private static ItemRarity ParseGrade(string grade)
        {
            if (string.IsNullOrEmpty(grade)) return ItemRarity.Common;

            // GasMaskGrade → ItemRarity 매핑
            switch (grade.ToLower())
            {
                case "wood": return ItemRarity.Common;
                case "stone": return ItemRarity.Uncommon;
                case "iron": return ItemRarity.Rare;
                case "reinforced": return ItemRarity.Epic;
                case "special": return ItemRarity.Legendary;
                default:
                    if (System.Enum.TryParse<ItemRarity>(grade, true, out var rarity))
                        return rarity;
                    return ItemRarity.Common;
            }
        }

        /// <summary>
        /// 수리 결과 구조체
        /// </summary>
        public struct RepairResult
        {
            public bool success;
            public string message;
            public int newDurability;
            public int goldCost;
        }
    }
}