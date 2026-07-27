using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Utils
{
    public static class InventoryUtils
    {
        public static int GetItemQuantity(Inventory inventory, string itemName)
        {
            return inventory.GetItem(itemName).quantity;
        }
    }
}