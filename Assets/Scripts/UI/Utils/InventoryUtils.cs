using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace Game.UI.Utils
{
    public static class InventoryUtils
    {
        public static int GetItemQuantity(PlayerInventory inventory, string itemName)
        {
            return inventory.GetItemCount(itemName);
        }
    }
}