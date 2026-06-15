using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-17: 장비 내구도 시스템
    /// 
    /// 장비 아이템(무기/방어구/도구)의 내구도를 관리합니다.
    /// 내구도 0이 되면 아이템이 파괴됩니다.
    /// 크래프트 테이블에서 재료를 소모하여 수리할 수 있습니다.
    /// </summary>
    public static class EquipmentDurabilitySystem
    {
        // 내구도 색상 임계값
        public const float GREEN_THRESHOLD = 0.6f;   // 60% 이상 = 녹색
        public const float YELLOW_THRESHOLD = 0.3f;  // 30~60% = 노랑
        // 30% 미만 = 빨강

        /// <summary>
        /// 내구도 감소 (사용 시 호출)
        /// </summary>
        public static bool ReduceDurability(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null) return false;
            if (slot.item.maxDurability <= 0) return false; // 내구도 없음

            slot.currentDurability--;
            Debug.Log($"[Durability] {slot.item.displayName} 내구도 -1 ({slot.currentDurability}/{slot.item.maxDurability})");

            // 내구도 0 → 파괴
            if (slot.currentDurability <= 0)
            {
                Debug.Log($"[Durability] {slot.item.displayName} 내구도 0 → 파괴됨!");
                return true; // 파괴 신호
            }
            return false;
        }

        /// <summary>
        /// 내구도 수리
        /// </summary>
        public static void Repair(PlayerInventory.ItemSlot slot, int amount = -1)
        {
            if (slot == null || slot.item == null || slot.item.maxDurability <= 0) return;

            if (amount < 0)
                slot.currentDurability = slot.item.maxDurability; // 완전 수리
            else
                slot.currentDurability = Mathf.Min(slot.item.maxDurability, slot.currentDurability + amount);

            Debug.Log($"[Durability] {slot.item.displayName} 수리 완료! ({slot.currentDurability}/{slot.item.maxDurability})");
        }

        /// <summary>
        /// 내구도 비율 반환 (0~1)
        /// </summary>
        public static float GetDurabilityRatio(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null || slot.item.maxDurability <= 0) return 1f;
            return (float)slot.currentDurability / slot.item.maxDurability;
        }

        /// <summary>
        /// 내구도 색상 태그 반환 (녹색/노랑/빨강)
        /// </summary>
        public static string GetDurabilityColorTag(PlayerInventory.ItemSlot slot)
        {
            float ratio = GetDurabilityRatio(slot);
            if (ratio >= GREEN_THRESHOLD) return "🟢";
            if (ratio >= YELLOW_THRESHOLD) return "🟡";
            return "🔴";
        }

        /// <summary>
        /// 내구도 상태 문자열
        /// </summary>
        public static string GetDurabilityString(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null) return "";
            if (slot.item.maxDurability <= 0) return "∞"; // 무한 내구도
            return $"{GetDurabilityColorTag(slot)} {slot.currentDurability}/{slot.item.maxDurability}";
        }

        /// <summary>
        /// 수리 비용 계산 (재료 타입 + 수량)
        /// </summary>
        public static int GetRepairCost(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null) return 0;
            int missingDurability = slot.item.maxDurability - slot.currentDurability;
            if (missingDurability <= 0) return 0;

            // 카테고리별 수리 재료 계수
            float costMultiplier = 1f;
            switch (slot.item.category)
            {
                case PlayerInventory.ItemCategory.Weapon: costMultiplier = 2f; break;
                case PlayerInventory.ItemCategory.Armor: costMultiplier = 3f; break;
                case PlayerInventory.ItemCategory.Tool: costMultiplier = 1f; break;
                default: costMultiplier = 1f; break;
            }

            return Mathf.Max(1, Mathf.CeilToInt(missingDurability * costMultiplier / 10f));
        }

        /// <summary>
        /// 장비가 완전히 파괴되었는가?
        /// </summary>
        public static bool IsBroken(PlayerInventory.ItemSlot slot)
        {
            return slot != null && slot.item != null && slot.item.maxDurability > 0 && slot.currentDurability <= 0;
        }
    }
}