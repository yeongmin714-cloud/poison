using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Utils
{
    public static class InventoryUtils
    {
        public static void SortInventoryItems(List<InventoryItem> items)
        {
            items.Sort((x, y) => x.name.CompareTo(y.name));
        }
        
        public static bool IsItemEquipped(InventoryItem item, List<InventoryItem> equippedItems)
        {
            return equippedItems.Contains(item);
        }
        
        public static void AddItemToInventory(InventoryItem item, List<InventoryItem> inventory)
        {
            if (item != null && !inventory.Contains(item))
            {
                inventory.Add(item);
            }
        }
        
        public static void RemoveItemFromInventory(InventoryItem item, List<InventoryItem> inventory)
        {
            if (item != null)
            {
                inventory.Remove(item);
            }
        }
    }
    
    [System.Serializable]
    public class InventoryItem
    {
        public string name;
        public int quantity;
        public Sprite icon;
        public ItemType type;
        
        public enum ItemType
        {
            Weapon,
            Armor,
            Consumable,
            QuestItem,
            Material
        }
    }
}