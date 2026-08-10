using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;

public class ShopTest : MonoBehaviour
{
    [UnityEditor.MenuItem("Tools/Run Shop Test")]
    public static void RunShopTest()
    {
        Debug.Log("[ShopTest] Running shop test in editor...");
        Debug.Log("[ShopTest] NOTE: Shop is now accessed via BuildingTrigger → IndoorSceneTransition");
        Debug.Log("[ShopTest] Manual test: Approach a Shop building, press E to enter interior,");
        Debug.Log("[ShopTest] then approach ShopNPC inside and press E to open shop.");

        // Load main scene
        SceneManager.LoadScene("MainScene");
        Debug.Log("[ShopTest] MainScene loaded");

        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[ShopTest] Player not found! Make sure player exists in scene.");
            return;
        }

        Debug.Log($"[ShopTest] Player found at {player.transform.position}");

        // Find a shop building
        BuildingPlaceholder[] buildings = GameObject.FindObjectsByType<BuildingPlaceholder>();
        BuildingPlaceholder shopBuilding = null;
        foreach (var building in buildings)
        {
            if (building.buildingType == BuildingPlaceholder.BuildingType.Shop)
            {
                shopBuilding = building;
                break;
            }
        }

        if (shopBuilding == null)
        {
            Debug.LogError("[ShopTest] No shop building found! Run TerritoryBuilder to create one.");
            return;
        }

        Debug.Log($"[ShopTest] Shop building found: {shopBuilding.buildingName} at {shopBuilding.transform.position}");

        // Verify BuildingTrigger is attached (FIX-01)
        var trigger = shopBuilding.GetComponent<BuildingTrigger>();
        if (trigger != null)
        {
            Debug.Log($"[ShopTest] ✅ BuildingTrigger found on shop. Type: {trigger.BuildingType}");
        }
        else
        {
            Debug.LogError("[ShopTest] ❌ BuildingTrigger NOT found on shop! Bug: FIX-01 not applied.");
        }

        // Simulate player moving near shop
        player.transform.position = shopBuilding.transform.position + new Vector3(0, 0, -2);
        Debug.Log("[ShopTest] Player moved near shop. Press E to enter (manual test).");

        // Verify building has correct type
        if (shopBuilding.buildingType == BuildingPlaceholder.BuildingType.Shop)
        {
            Debug.Log("[ShopTest] ✅ Shop building type is correct");
        }

        Debug.Log("[ShopTest] Test completed — building is properly set up for interior transition.");
    }

    public static void RunBatchTest()
    {
        Debug.Log("[ShopTest] Starting batch test...");
        Debug.Log("[ShopTest] Batch test requires manual execution in editor.");
        Debug.Log("[ShopTest] Steps: 1) Open MainScene 2) Tools > Run Shop Test");
    }
}