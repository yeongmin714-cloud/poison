using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 툴팁에 표시할 아이템 정보 데이터 구조.
    /// ItemSlot/ShopItem 등 다양한 소스에서 추출 가능.
    /// </summary>
    [System.Serializable]
    public struct ItemTooltipData
    {
        public string itemName;
        public string description;
        public string effects;
        public ItemRarity rarity;
        public PlayerInventory.ItemCategory category;
        public int maxDurability;
        public int currentDurability;
        public int count;
        public bool hasDurability => maxDurability > 0;
        public float durabilityRatio => hasDurability
            ? Mathf.Clamp01((float)currentDurability / maxDurability)
            : 1f;

        /// <summary>툴팁이 유효한 데이터를 가지고 있는지</summary>
        public bool IsValid => !string.IsNullOrEmpty(itemName);

        /// <summary>등급별 한글 이름</summary>
        public static string GetRarityDisplayName(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => "일반",
                ItemRarity.Uncommon => "고급",
                ItemRarity.Rare => "희귀",
                ItemRarity.Epic => "영웅",
                ItemRarity.Legendary => "전설",
                _ => "일반"
            };
        }

        /// <summary>등급별 테두리 색상</summary>
        public static Color GetRarityBorderColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.8f, 0.8f, 0.8f, 1f),
                ItemRarity.Uncommon => new Color(0.3f, 0.9f, 0.3f, 1f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1.0f, 1f),
                ItemRarity.Epic => new Color(0.7f, 0.3f, 0.9f, 1f),
                ItemRarity.Legendary => new Color(1.0f, 0.7f, 0.1f, 1f),
                _ => new Color(0.8f, 0.8f, 0.8f, 1f)
            };
        }

        /// <summary>카테고리별 강조 색상</summary>
        public static Color GetCategoryColor(PlayerInventory.ItemCategory category)
        {
            return category switch
            {
                PlayerInventory.ItemCategory.Herb => new Color(0.3f, 0.8f, 0.3f, 1f),
                PlayerInventory.ItemCategory.Meat => new Color(0.8f, 0.4f, 0.2f, 1f),
                PlayerInventory.ItemCategory.Food => new Color(0.9f, 0.8f, 0.2f, 1f),
                PlayerInventory.ItemCategory.Potion => new Color(0.7f, 0.3f, 0.8f, 1f),
                PlayerInventory.ItemCategory.Drug => new Color(0.9f, 0.2f, 0.5f, 1f),
                PlayerInventory.ItemCategory.Material => new Color(0.5f, 0.5f, 0.5f, 1f),
                PlayerInventory.ItemCategory.Quest => new Color(0.2f, 0.7f, 0.8f, 1f),
                PlayerInventory.ItemCategory.Weapon => new Color(0.8f, 0.3f, 0.2f, 1f),
                PlayerInventory.ItemCategory.Armor => new Color(0.4f, 0.5f, 0.8f, 1f),
                PlayerInventory.ItemCategory.Tool => new Color(0.6f, 0.5f, 0.3f, 1f),
                _ => Color.gray
            };
        }

        /// <summary>내구도 색상</summary>
        public static Color GetDurabilityColor(float ratio)
        {
            return ratio >= 0.6f ? Color.green :
                   ratio >= 0.3f ? Color.yellow : Color.red;
        }
    }

    /// <summary>
    /// 슬롯 데이터에서 ItemTooltipData를 추출하는 확장 메서드
    /// </summary>
    public static class ItemTooltipExtensions
    {
        /// <summary>PlayerInventory.ItemSlot → ItemTooltipData</summary>
        public static ItemTooltipData ToTooltipData(this PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null)
                return default;

            return new ItemTooltipData
            {
                itemName = slot.item.displayName,
                description = slot.item.description,
                effects = slot.item.effects,
                rarity = slot.item.rarity,
                category = slot.item.category,
                maxDurability = slot.item.maxDurability,
                currentDurability = slot.currentDurability,
                count = slot.count
            };
        }

        /// <summary>ShopWindow.ShopItem → ItemTooltipData</summary>
        public static ItemTooltipData ToTooltipData(this object shopItem)
        {
            var item = shopItem;
            if (item == null)
                return default;

            // Reflection-safe: ShopItem 클래스에 직접 접근하지 않고
            // ShopItem 구조가 item(ItemData), price, stock, isRare 필드를 가짐
            var type = item.GetType();
            var itemDataField = type.GetField("item");
            if (itemDataField == null)
                return default;

            var itemData = itemDataField.GetValue(item) as PlayerInventory.ItemData;
            if (itemData == null)
                return default;

            int stock = 0;
            var stockField = type.GetField("stock");
            if (stockField != null)
                stock = (int)stockField.GetValue(item);

            int price = 0;
            var priceField = type.GetField("price");
            if (priceField != null)
                price = (int)priceField.GetValue(item);

            bool isRare = false;
            var rareField = type.GetField("isRare");
            if (rareField != null)
                isRare = (bool)rareField.GetValue(item);

            return new ItemTooltipData
            {
                itemName = itemData.displayName,
                description = itemData.description,
                effects = itemData.effects,
                rarity = isRare ? ItemRarity.Rare : itemData.rarity,
                category = itemData.category,
                maxDurability = itemData.maxDurability,
                currentDurability = itemData.maxDurability,
                count = stock > 0 ? stock : -1 // -1 = 무한
            };
        }

        /// <summary>PlayerInventory.ItemData → ItemTooltipData (단순 데이터, 내구도 없음)</summary>
        public static ItemTooltipData ToTooltipData(this PlayerInventory.ItemData itemData)
        {
            if (itemData == null)
                return default;

            return new ItemTooltipData
            {
                itemName = itemData.displayName,
                description = itemData.description,
                effects = itemData.effects,
                rarity = itemData.rarity,
                category = itemData.category,
                maxDurability = itemData.maxDurability,
                currentDurability = itemData.maxDurability,
                count = 1
            };
        }
    }
}