using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Core
{
    /// <summary>
    /// Simple test to verify dishes are loaded correctly and can be added to inventory.
    /// Attach to any GameObject (e.g., via GameManager).
    /// </summary>
    public class DishTester : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[DishTester] Awake");
            TestLoading();
            TestInventoryIntegration();
        }

        private void TestLoading()
        {
            var all = DishDatabase.All;
            Debug.Log($"[DishTester] Total dishes loaded: {all.Count}");
            // Show first few
            for (int i = 0; i < Mathf.Min(5, all.Count); i++)
            {
                var d = all[i];
                Debug.Log($"[DishTester] {d.Id}: {d.DisplayName} - {d.Description} (효과: {d.Effect})");
            }
        }

        private void TestInventoryIntegration()
        {
            // Test adding a few known dishes to player inventory
            var testDishes = new[]
            {
                "토끼 허브 구이",
                "야성적인 육포",
                "꽃향기 스테이크",
                "마비 뱀 꼬치",
                "여우 눈알 요리"
            };

            foreach (var dishName in testDishes)
            {
                var item = DishDatabase.GetItemData(dishName);
                if (item != null)
                {
                    if (PlayerInventory.Instance == null)
                    {
                        Debug.LogWarning($"[DishTester] PlayerInventory.Instance is null, skipping AddItem for {dishName}");
                        continue;
                    }
                    bool added = PlayerInventory.Instance.AddItem(item, 1);
                    Debug.Log($"[DishTester] Added {dishName} to inventory: {added}");
                }
                else
                {
                    Debug.LogWarning($"[DishTester] Could not get item data for {dishName}");
                }
            }

            // Log inventory slots
            Debug.Log("[DishTester] Current inventory slots:");
            if (PlayerInventory.Instance == null)
            {
                Debug.LogWarning("[DishTester] PlayerInventory.Instance is null, skipping slot display.");
                return;
            }
            var slots = PlayerInventory.Instance.GetAllSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.item != null)
                {
                    Debug.Log($"  Slot {i}: {slot.item.displayName} x{slot.count}");
                }
            }
        }
    }
}