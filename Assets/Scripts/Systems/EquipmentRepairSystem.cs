using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-19: 장비 수리 시스템 — 크래프트 테이블/인벤토리에서 재료 소모하여 수리
    /// 
    /// 수리 재료: 나무, 돌, 철 (아이템 카테고리 Material)
    /// 수리 비용: EquipmentDurabilitySystem.GetRepairCost() 기반
    /// </summary>
    public static class EquipmentRepairSystem
    {
        /// <summary>
        /// 장비 수리 시도. 성공 여부와 메시지를 반환합니다.
        /// </summary>
        public static RepairResult TryRepair(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null)
                return Fail("수리할 장비가 없습니다.");

            if (slot.item.maxDurability <= 0)
                return Fail("이 아이템은 수리할 수 없습니다.");

            if (slot.currentDurability >= slot.item.maxDurability)
                return Fail("내구도가 이미 가득 찼습니다.");

            if (PlayerInventory.Instance == null)
                return Fail("인벤토리를 찾을 수 없습니다.");

            // 수리 비용 계산
            int cost = EquipmentDurabilitySystem.GetRepairCost(slot);
            if (cost <= 0)
                return Fail("수리할 필요가 없습니다.");

            // 필요한 재료 확인 및 차감
            string matId = GetRepairMaterialId(slot.item.category);
            if (string.IsNullOrEmpty(matId))
                return Fail("수리 재료를 알 수 없습니다.");

            int currentCount = PlayerInventory.Instance.GetItemCount(matId);
            if (currentCount < cost)
                return Fail($"재료 부족! 필요: {GetRepairMaterialName(slot.item.category)} x{cost} (보유: {currentCount})");

            // 재료 차감
            PlayerInventory.Instance.RemoveItem(matId, cost);

            // 수리 실행
            EquipmentDurabilitySystem.Repair(slot);

            return Success($"수리 완료! {slot.item.displayName} ({slot.currentDurability}/{slot.item.maxDurability}) 재료 소모: {GetRepairMaterialName(slot.item.category)} x{cost}");
        }

        /// <summary>
        /// 수리 결과
        /// </summary>
        public struct RepairResult
        {
            public bool success;
            public string message;
        }

        private static RepairResult Success(string msg) => new RepairResult { success = true, message = msg };
        private static RepairResult Fail(string msg) => new RepairResult { success = false, message = msg };

        /// <summary>
        /// 장비 카테고리별 수리 재료 ID 반환
        /// </summary>
        private static string GetRepairMaterialId(PlayerInventory.ItemCategory category)
        {
            switch (category)
            {
                case PlayerInventory.ItemCategory.Weapon: return "mat_repair_metal";  // 금속
                case PlayerInventory.ItemCategory.Armor: return "mat_repair_leather"; // 가죽
                case PlayerInventory.ItemCategory.Tool: return "mat_repair_wood";    // 나무
                default: return "";
            }
        }

        /// <summary>
        /// 수리 재료 이름 반환
        /// </summary>
        private static string GetRepairMaterialName(PlayerInventory.ItemCategory category)
        {
            switch (category)
            {
                case PlayerInventory.ItemCategory.Weapon: return "수리용 금속";
                case PlayerInventory.ItemCategory.Armor: return "수리용 가죽";
                case PlayerInventory.ItemCategory.Tool: return "수리용 나무";
                default: return "";
            }
        }

        /// <summary>
        /// 수리 재료 ItemData 생성 (정적 아이템 정의가 없으므로 동적 생성)
        /// </summary>
        public static PlayerInventory.ItemData CreateRepairMaterial(PlayerInventory.ItemCategory category)
        {
            string id = GetRepairMaterialId(category);
            string name = GetRepairMaterialName(category);
            if (string.IsNullOrEmpty(id)) return null;

            return new PlayerInventory.ItemData
            {
                id = id,
                displayName = name,
                description = $"장비 수리용 재료 ({name})",
                category = PlayerInventory.ItemCategory.Material,
                maxStack = 99,
                maxDurability = 0
            };
        }
    }
}