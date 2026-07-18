using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIItemManager : MonoBehaviour
    {
        [Header("UI References")]
        public Text itemInventoryText;
        public RectTransform itemPanel;
        public GameObject itemPrefab;
        
        [Header("Item Data")]
        public int itemCount = 0;
        public string[] itemNames = {"Sword", "Shield", "Potion"};

        private void Start()
        {
            InitializeItemManager();
        }

        public void InitializeItemManager()
        {
            itemInventoryText.text = $"Items: {itemCount}";
            
            // Display items
            foreach(string itemName in itemNames)
            {
                GameObject itemGO = Instantiate(itemPrefab, itemPanel);
                // Update item display
            }
        }

        public void AddItem(string itemName)
        {
            itemCount++;
            itemInventoryText.text = $"Items: {itemCount}";
        }

        public void RemoveItem(string itemName)
        {
            itemCount--;
            itemInventoryText.text = $"Items: {itemCount}";
        }
    }
}