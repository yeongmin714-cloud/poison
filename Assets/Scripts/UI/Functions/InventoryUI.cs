using UnityEngine;
using UnityEngine.Localization;

public class InventoryUI : MonoBehaviour
{
    public GameObject inventoryPanel;
    public Transform itemSlots;
    
    private void Start()
    {
        // Initialize inventory
        Debug.Log("Inventory UI initialized");
    }
    
    public void UpdateInventory()
    {
        // Update inventory display
    }
}