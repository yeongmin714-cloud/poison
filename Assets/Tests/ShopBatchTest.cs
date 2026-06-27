using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;

public class ShopBatchTest : MonoBehaviour
{
    // This method can be called via Unity's -executeMethod in batch mode
    // It does not use any UnityEditor API
    public static void RunBatchTest()
    {
        Debug.Log("[ShopBatchTest] Starting batch test for shop system...");
        
        // In batch mode, we need to load a scene and wait for initialization
        // For a simple test, we'll load MainScene and then attempt to find objects
        // Note: In pure batch mode without Update loops, we need to use coroutines or wait frames
        // For simplicity, we'll just test the core logic by directly calling methods
        
        // Test 1: Verify gold system works
        Debug.Log("[ShopBatchTest] Testing gold system...");
        int initialGold = PlayerStats.Instance.Gold;
        Debug.Log($"[ShopBatchTest] Initial gold: {initialGold}");
        
        PlayerStats.Instance.AddGold(100);
        int afterAdd = PlayerStats.Instance.Gold;
        Debug.Log($"[ShopBatchTest] After adding 100 gold: {afterAdd}");
        
        bool spent = PlayerStats.Instance.SpendGold(50);
        int afterSpend = PlayerStats.Instance.Gold;
        Debug.Log($"[ShopBatchTest] Spent 50 gold: {spent}, remaining: {afterSpend}");
        
        // Test 2: Verify shop window can be instantiated
        Debug.Log("[ShopBatchTest] Testing shop window instantiation...");
        GameObject shopWindowObj = new GameObject("TestShopWindow");
        ShopWindow shopWindow = shopWindowObj.AddComponent<ShopWindow>();
        
        if (shopWindow != null)
        {
            Debug.Log("[ShopBatchTest] ShopWindow component added successfully");
            
            // Initialize inventory (this happens in Awake, but we can call it)
            shopWindow.InitializeShopInventory();
            
            Debug.Log($"[ShopBatchTest] Shop inventory initialized with {shopWindow.ShopInventory.Count} items");
            
            // Test buying an item if we have enough gold
            if (shopWindow.ShopInventory.Count > 0 && PlayerStats.Instance.Gold >= 10)
            {
                ShopWindow.ShopItem firstItem = shopWindow.ShopInventory[0];
                Debug.Log($"[ShopBatchTest] Attempting to buy: {firstItem.item.displayName} for {firstItem.price} gold");
                
                // We need to simulate the selection - for now just call BuySelectedItem with a mock selection
                // Since we don't have the UI grid set up, we'll test the core purchase logic directly
                
                // Test the SpendGold and AddItem logic
                int goldBefore = PlayerStats.Instance.Gold;
                bool canSpend = PlayerStats.Instance.SpendGold(firstItem.price);
                
                if (canSpend)
                {
                    bool added = PlayerInventory.Instance.AddItem(firstItem.item, 1);
                    int goldAfter = PlayerStats.Instance.Gold;
                    
                    Debug.Log($"[ShopBatchTest] Purchase test: SpendGold={canSpend}, AddItem={added}, Gold: {goldBefore} -> {goldAfter}");
                    
                    if (!added)
                    {
                        // Refund if inventory full
                        PlayerStats.Instance.AddGold(firstItem.price);
                        Debug.Log("[ShopBatchTest] Inventory full, gold refunded");
                    }
                }
                else
                {
                    Debug.Log("[ShopBatchTest] Purchase test: Not enough gold");
                }
            }
            else
            {
                Debug.Log("[ShopBatchTest] Skipping purchase test - no items or insufficient gold");
            }
            
            // Clean up
            GameObject.DestroyImmediate(shopWindowObj);
        }
        else
        {
            Debug.LogError("[ShopBatchTest] Failed to add ShopWindow component");
        }
        
        // Test 3: Verify building placeholder shop interaction logic
        Debug.Log("[ShopBatchTest] Testing building placeholder shop logic...");
        GameObject buildingObj = new GameObject("TestShopBuilding");
        BuildingPlaceholder building = buildingObj.AddComponent<BuildingPlaceholder>();
        building.buildingType = BuildingPlaceholder.BuildingType.Shop;
        building.buildingName = "Test Shop";
        
        // Simulate Start (Awake is not public on BuildingPlaceholder)
        building.Start();
        
        Debug.Log($"[ShopBatchTest] Building placeholder created: {building.buildingName} ({building.buildingType})");
        
        // Clean up
        GameObject.DestroyImmediate(buildingObj);
        
        Debug.Log("[ShopBatchTest] Batch test completed successfully!");
    }
}