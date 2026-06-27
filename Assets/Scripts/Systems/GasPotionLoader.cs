using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C8-33: 가스 분사기 물약 장전 시스템
    /// 인벤토리에서 Potion/Herb/Drug 카테고리 아이템을 분사기에 삽입/해제합니다.
    /// </summary>
    public static class GasPotionLoader
    {
        /// <summary>
        /// 인벤토리에서 특정 Potion/Herb/Drug 아이템을 분사기에 장전합니다.
        /// </summary>
        /// <param name="controller">GasSprayerController 인스턴스</param>
        /// <param name="potionItemId">장전할 아이템 ID</param>
        /// <returns>실제로 장전된 개수. 실패 시 0.</returns>
        public static int LoadPotion(GasSprayerController controller, string potionItemId)
        {
            if (controller == null)
            {
                Debug.LogWarning("[GasPotionLoader] GasSprayerController가 null입니다.");
                return 0;
            }

            if (!controller.IsEquipped)
            {
                Debug.LogWarning("[GasPotionLoader] 분사기가 장착되지 않았습니다.");
                return 0;
            }

            if (PlayerInventory.Instance == null)
            {
                Debug.LogWarning("[GasPotionLoader] PlayerInventory.Instance가 없습니다.");
                return 0;
            }

            if (string.IsNullOrEmpty(potionItemId))
            {
                Debug.LogWarning("[GasPotionLoader] 유효하지 않은 아이템 ID입니다.");
                return 0;
            }

            // 이미 다른 물약이 장전되어 있으면 해제 불가 (먼저 언로드 필요)
            if (!string.IsNullOrEmpty(controller.LoadedPotionId) && controller.LoadedPotionId != potionItemId)
            {
                Debug.LogWarning($"[GasPotionLoader] 이미 {controller.LoadedPotionId}이(가) 장전되어 있습니다. 먼저 해제하세요.");
                return 0;
            }

            // 인벤토리에 아이템이 있는지 확인
            int inventoryCount = PlayerInventory.Instance.GetItemCount(potionItemId);
            if (inventoryCount <= 0)
            {
                Debug.LogWarning($"[GasPotionLoader] 인벤토리에 {potionItemId}이(가) 없습니다.");
                return 0;
            }

            // 아이템 카테고리 확인 (Potion/Herb/Drug만 허용)
            var slots = PlayerInventory.Instance.GetAllSlots();
            PlayerInventory.ItemData itemData = null;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].item.id == potionItemId)
                {
                    itemData = slots[i].item;
                    break;
                }
            }

            if (itemData == null)
            {
                Debug.LogWarning($"[GasPotionLoader] {potionItemId} 데이터를 찾을 수 없습니다.");
                return 0;
            }

            if (itemData.category != PlayerInventory.ItemCategory.Potion &&
                itemData.category != PlayerInventory.ItemCategory.Herb &&
                itemData.category != PlayerInventory.ItemCategory.Drug)
            {
                Debug.LogWarning($"[GasPotionLoader] {potionItemId}은(는) Potion/Herb/Drug 카테고리가 아닙니다. (현재: {itemData.category})");
                return 0;
            }

            // 인벤토리에서 모든 해당 아이템 제거
            bool removed = PlayerInventory.Instance.RemoveItem(potionItemId, inventoryCount);
            if (!removed)
            {
                Debug.LogWarning($"[GasPotionLoader] {potionItemId} 제거 실패");
                return 0;
            }

            // 분사기에 설정
            controller.LoadedPotionId = potionItemId;
            controller.LoadedPotionCount += inventoryCount;

            Debug.Log($"[GasPotionLoader] {itemData.displayName} x{inventoryCount} 장전 완료! (총 {controller.LoadedPotionCount}개)");

            // 장전 완료 이벤트 발생
            controller.NotifyPotionChanged();

            return inventoryCount;
        }

        /// <summary>
        /// 분사기에 장전된 물약을 인벤토리로 반환합니다.
        /// </summary>
        /// <param name="controller">GasSprayerController 인스턴스</param>
        /// <returns>반환된 물약 개수. 실패 시 0.</returns>
        public static int UnloadPotion(GasSprayerController controller)
        {
            if (controller == null)
            {
                Debug.LogWarning("[GasPotionLoader] GasSprayerController가 null입니다.");
                return 0;
            }

            if (string.IsNullOrEmpty(controller.LoadedPotionId) || controller.LoadedPotionCount <= 0)
            {
                Debug.LogWarning("[GasPotionLoader] 장전된 물약이 없습니다.");
                return 0;
            }

            if (PlayerInventory.Instance == null)
            {
                Debug.LogWarning("[GasPotionLoader] PlayerInventory.Instance가 없습니다.");
                return 0;
            }

            // 분사 중이면 중단
            if (controller.IsSpraying)
            {
                controller.StopSpray();
            }

            string potionId = controller.LoadedPotionId;
            int count = controller.LoadedPotionCount;

            // 아이템 데이터 생성
            var itemData = CreatePotionItemData(potionId);
            if (itemData == null)
            {
                Debug.LogWarning($"[GasPotionLoader] {potionId}에 해당하는 ItemData를 생성할 수 없습니다.");
                return 0;
            }

            // 인벤토리에 추가
            bool added = PlayerInventory.Instance.AddItem(itemData, count);
            if (!added)
            {
                Debug.LogWarning("[GasPotionLoader] 인벤토리 가득 참 - 물약 반환 실패");
                return 0;
            }

            // 분사기 상태 초기화
            controller.LoadedPotionId = "";
            controller.LoadedPotionCount = 0;

            Debug.Log($"[GasPotionLoader] 물약 x{count} 인벤토리로 반환 완료!");

            controller.NotifyPotionChanged();

            return count;
        }

        /// <summary>
        /// 분사기에 물약을 장전할 수 있는지 확인합니다.
        /// </summary>
        public static bool CanLoadPotion(GasSprayerController controller, string potionItemId)
        {
            if (controller == null || !controller.IsEquipped)
                return false;

            if (PlayerInventory.Instance == null)
                return false;

            if (string.IsNullOrEmpty(potionItemId))
                return false;

            // 이미 다른 물약 장전됨
            if (!string.IsNullOrEmpty(controller.LoadedPotionId) && controller.LoadedPotionId != potionItemId)
                return false;

            // 인벤토리에 있는지 확인
            if (!PlayerInventory.Instance.HasItem(potionItemId))
                return false;

            // 카테고리 확인
            var slots = PlayerInventory.Instance.GetAllSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].item.id == potionItemId)
                {
                    var cat = slots[i].item.category;
                    return cat == PlayerInventory.ItemCategory.Potion ||
                           cat == PlayerInventory.ItemCategory.Herb ||
                           cat == PlayerInventory.ItemCategory.Drug;
                }
            }

            return false;
        }

        /// <summary>
        /// 장전된 물약을 해제할 수 있는지 확인합니다.
        /// </summary>
        public static bool CanUnloadPotion(GasSprayerController controller)
        {
            if (controller == null)
                return false;

            if (string.IsNullOrEmpty(controller.LoadedPotionId) || controller.LoadedPotionCount <= 0)
                return false;

            if (PlayerInventory.Instance == null)
                return false;

            return true;
        }

        /// <summary>
        /// 물약 ID로부터 ItemData 생성 (정적 데이터 매핑)
        /// </summary>
        private static PlayerInventory.ItemData CreatePotionItemData(string potionId)
        {
            // 정적 아이템 데이터 매핑
            if (potionId == "herb_red") return CloneItemData(PlayerInventory.Herb_Red);
            if (potionId == "herb_purple") return CloneItemData(PlayerInventory.Herb_Purple);
            if (potionId == "herb_yellow") return CloneItemData(PlayerInventory.Herb_Yellow);
            if (potionId == "herb_silver") return CloneItemData(PlayerInventory.Herb_Silver);
            if (potionId == "herb_green") return CloneItemData(PlayerInventory.Herb_Green);

            // 알 수 없는 ID — 동적으로 생성
            return new PlayerInventory.ItemData
            {
                id = potionId,
                displayName = potionId,
                description = "알 수 없는 물약",
                category = PlayerInventory.ItemCategory.Potion,
                maxStack = 99,
                rarity = PlayerInventory.ItemRarity.Common,
                effects = ""
            };
        }

        /// <summary>
        /// ItemData 복사 (참조 아닌 새 인스턴스)
        /// </summary>
        private static PlayerInventory.ItemData CloneItemData(PlayerInventory.ItemData source)
        {
            if (source == null) return null;
            return new PlayerInventory.ItemData
            {
                id = source.id,
                displayName = source.displayName,
                description = source.description,
                category = source.category,
                icon = source.icon,
                maxStack = source.maxStack,
                maxDurability = source.maxDurability,
                rarity = source.rarity,
                effects = source.effects
            };
        }
    }
}
