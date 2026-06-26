using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Test consumable system: press 'U' to use the first food item in inventory.
    /// Attach to any GameObject (e.g., via GameManager).
    /// </summary>
    public class ConsumableTester : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
            {
                UseFirstFoodItem();
            }
        }

        private void UseFirstFoodItem()
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogError("[ConsumableTester] PlayerInventory instance not found.");
                return;
            }

            var slots = inventory.GetAllSlots();
            int foodSlotIndex = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.item != null && slot.item.category == PlayerInventory.ItemCategory.Food)
                {
                    foodSlotIndex = i;
                    break;
                }
            }

            if (foodSlotIndex < 0)
            {
                Debug.Log("[ConsumableTester] No food item found in inventory.");
                return;
            }

            Debug.Log($"[ConsumableTester] Using food item at slot {foodSlotIndex}: {slots[foodSlotIndex].item.displayName}");
            inventory.UseItem(foodSlotIndex);
        }
    }
}