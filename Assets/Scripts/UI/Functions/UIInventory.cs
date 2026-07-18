using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIInventory : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform inventoryPanel;
        public GridLayoutGroup gridLayout;
        public GameObject itemPrefab;

        private System.Collections.Generic.List<Item> items = new System.Collections.Generic.List<Item>();

        private void Start()
        {
            InitializeInventory();
        }

        public void InitializeInventory()
        {
            // Create initial inventory items
            AddItem(new Item("Health Potion", 1));
            AddItem(new Item("Mana Potion", 1));
            AddItem(new Item("Sword", 1));
            
            UpdateUI();
        }

        public void AddItem(Item item)
        {
            items.Add(item);
        }

        public void RemoveItem(Item item)
        {
            items.Remove(item);
        }

        public void UpdateUI()
        {
            foreach (Transform child in inventoryPanel.transform)
            {
                Destroy(child.gameObject);
            }

            foreach (Item item in items)
            {
                GameObject itemGO = Instantiate(itemPrefab, inventoryPanel);
                // Update item display logic here
            }
        }
    }

    [System.Serializable]
    public class Item
    {
        public string name;
        public int quantity;

        public Item(string name, int quantity)
        {
            this.name = name;
            this.quantity = quantity;
        }
    }
}