using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 병사 장비 슬롯 종류
    /// </summary>
    public enum EquipSlot
    {
        Weapon,    // 무기 (검/창/활)
        Helmet,    // 투구
        Armor,     // 갑옷
        Shield     // 방패
    }

    /// <summary>C9-29: 병사 무기 파츠 교체 시스템</summary>
    public static class WeaponPartsSystem
    {
        /// <summary>장비 장착/탈착 결과</summary>
        public struct EquipResult
        {
            public bool success;
            public string message;
        }

        /// <summary>슬롯별 허용 카테고리 반환</summary>
        public static PlayerInventory.ItemCategory GetRequiredCategory(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Weapon: return PlayerInventory.ItemCategory.Weapon;
                case EquipSlot.Helmet: return PlayerInventory.ItemCategory.Armor;
                case EquipSlot.Armor:  return PlayerInventory.ItemCategory.Armor;
                case EquipSlot.Shield: return PlayerInventory.ItemCategory.Armor;
                default: return PlayerInventory.ItemCategory.Material;
            }
        }

        /// <summary>슬롯 이름 (한글)</summary>
        public static string GetSlotName(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Weapon: return "⚔️ 무기";
                case EquipSlot.Helmet: return "🪖 투구";
                case EquipSlot.Armor:  return "🛡️ 갑옷";
                case EquipSlot.Shield: return "🔰 방패";
                default: return "알 수 없음";
            }
        }

        /// <summary>장비 장착. 성공/실패 메시지 반환.</summary>
        public static EquipResult EquipItem(GuardPlaceholder guard, EquipSlot slot, PlayerInventory.ItemSlot inventorySlot)
        {
            // 체크: guard 유효
            if (guard == null)
                return new EquipResult { success = false, message = "병사가 없습니다." };

            // 체크: guard 생존 + 포섭됨
            if (!guard.IsAlive)
                return new EquipResult { success = false, message = "죽은 병사에게 장비를 장착할 수 없습니다." };

            if (!guard.IsRecruited)
                return new EquipResult { success = false, message = "포섭되지 않은 병사입니다." };

            // 체크: inventorySlot 유효
            if (inventorySlot == null || inventorySlot.item == null || inventorySlot.count <= 0)
                return new EquipResult { success = false, message = "인벤토리 아이템이 유효하지 않습니다." };

            var item = inventorySlot.item;

            // 체크: 카테고리 일치
            var requiredCategory = GetRequiredCategory(slot);
            if (item.category != requiredCategory)
                return new EquipResult { success = false, message = $"'{GetSlotName(slot)}' 슬롯에는 '{requiredCategory}' 카테고리 아이템만 장착할 수 있습니다." };

            // 체크: 장비 내구도 있음 (maxDurability > 0)
            if (item.maxDurability <= 0)
                return new EquipResult { success = false, message = "내구도가 없는 아이템은 장비할 수 없습니다." };

            // 기존 장비가 있으면 인벤토리로 반환
            var existingItem = GetEquippedItem(guard, slot);
            if (existingItem != null)
            {
                if (PlayerInventory.Instance != null)
                {
                    PlayerInventory.Instance.AddItem(existingItem, 1);
                }
            }

            // 새 장비를 guard 슬롯에 설정
            SetEquippedItem(guard, slot, item);

            // 인벤토리에서 제거
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.RemoveItem(item.id, 1);
            }

            // 외형 업데이트
            guard.UpdateVisual();

            return new EquipResult { success = true, message = $"{GetSlotName(slot)} 장착 완료: {item.displayName}" };
        }

        /// <summary>장비 탈착. 인벤토리로 반환.</summary>
        public static EquipResult UnequipItem(GuardPlaceholder guard, EquipSlot slot)
        {
            // 체크: guard 유효
            if (guard == null)
                return new EquipResult { success = false, message = "병사가 없습니다." };

            // 체크: guard 생존 + 포섭됨
            if (!guard.IsAlive)
                return new EquipResult { success = false, message = "죽은 병사에게서 장비를 해제할 수 없습니다." };

            if (!guard.IsRecruited)
                return new EquipResult { success = false, message = "포섭되지 않은 병사입니다." };

            // 체크: 슬롯에 장비 있음
            var existingItem = GetEquippedItem(guard, slot);
            if (existingItem == null)
                return new EquipResult { success = false, message = $"{GetSlotName(slot)} 슬롯에 장비가 없습니다." };

            // 체크: 인벤토리 공간
            if (PlayerInventory.Instance != null && !PlayerInventory.Instance.AddItem(existingItem, 1))
                return new EquipResult { success = false, message = "인벤토리가 가득 찼습니다." };

            // guard 슬롯 제거
            SetEquippedItem(guard, slot, null);

            // 외형 업데이트
            guard.UpdateVisual();

            return new EquipResult { success = true, message = $"{GetSlotName(slot)} 해제 완료: {existingItem.displayName} → 인벤토리" };
        }

        /// <summary>병사의 특정 슬롯 장비 아이템 반환</summary>
        public static PlayerInventory.ItemData GetEquippedItem(GuardPlaceholder guard, EquipSlot slot)
        {
            if (guard == null) return null;

            switch (slot)
            {
                case EquipSlot.Weapon: return guard.WeaponItem;
                case EquipSlot.Helmet: return guard.HelmetItem;
                case EquipSlot.Armor:  return guard.ArmorItem;
                case EquipSlot.Shield: return guard.ShieldItem;
                default: return null;
            }
        }

        /// <summary>병사의 특정 슬롯 장비 아이템 설정</summary>
        private static void SetEquippedItem(GuardPlaceholder guard, EquipSlot slot, PlayerInventory.ItemData item)
        {
            switch (slot)
            {
                case EquipSlot.Weapon: guard.WeaponItem = item; break;
                case EquipSlot.Helmet: guard.HelmetItem = item; break;
                case EquipSlot.Armor:  guard.ArmorItem = item; break;
                case EquipSlot.Shield: guard.ShieldItem = item; break;
            }
        }

        /// <summary>병사 장비 전체 목록 반환</summary>
        public static List<(EquipSlot slot, PlayerInventory.ItemData item)> GetAllEquipped(GuardPlaceholder guard)
        {
            var result = new List<(EquipSlot slot, PlayerInventory.ItemData item)>();

            if (guard == null) return result;

            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                var item = GetEquippedItem(guard, slot);
                if (item != null)
                    result.Add((slot, item));
            }

            return result;
        }
    }
}