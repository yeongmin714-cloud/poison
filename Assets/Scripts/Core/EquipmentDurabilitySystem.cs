using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 장비 아이템의 내구도 시스템.
    /// InventoryWindow에서 내구도 UI 표시용으로 사용됩니다.
    /// </summary>
    public static class EquipmentDurabilitySystem
    {
        /// <summary>
        /// 아이템 슬롯의 내구도 비율을 반환 (0.0 ~ 1.0)
        /// </summary>
        public static float GetDurabilityRatio(PlayerInventory.ItemSlot slot)
        {
            if (slot?.item == null || slot.item.maxDurability <= 0)
                return 1f;

            return Mathf.Clamp01((float)slot.currentDurability / slot.item.maxDurability);
        }

        /// <summary>
        /// 내구도 문자열 반환 (예: "45/100")
        /// </summary>
        public static string GetDurabilityString(PlayerInventory.ItemSlot slot)
        {
            if (slot?.item == null || slot.item.maxDurability <= 0)
                return "-/-";

            return $"{slot.currentDurability:F0}/{slot.item.maxDurability:F0}";
        }

        /// <summary>
        /// 장비 수리. currentDurability를 maxDurability까지 채웁니다.
        /// </summary>
        public static void Repair(PlayerInventory.ItemSlot slot)
        {
            if (slot?.item == null || slot.item.maxDurability <= 0)
                return;

            slot.currentDurability = slot.item.maxDurability;
            Debug.Log($"[EquipmentDurabilitySystem] 🔧 {slot.item.displayName} 수리 완료 ({slot.currentDurability}/{slot.item.maxDurability})");
        }

        /// <summary>
        /// 수리 비용 계산. 부족한 내구도에 비례하여 재료 개수 반환.
        /// </summary>
        public static int GetRepairCost(PlayerInventory.ItemSlot slot)
        {
            if (slot?.item == null || slot.item.maxDurability <= 0)
                return 0;

            float missingRatio = 1f - ((float)slot.currentDurability / slot.item.maxDurability);
            if (missingRatio <= 0f) return 0;

            // 기본 1개 + 부족 비율당 추가 재료 (최대 5개)
            return Mathf.Max(1, Mathf.RoundToInt(missingRatio * 5f));
        }
    }
}