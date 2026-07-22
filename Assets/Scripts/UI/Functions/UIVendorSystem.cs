using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIVendorSystem : MonoBehaviour
    {
        [Header("UI References")]
        public Text vendorNameText;
        public Text vendorDescriptionText;
        public RectTransform vendorItemsPanel;
        public GameObject itemPrefab;
        public Button buyButton;
        public Button sellButton;
        
        [Header("Vendor Data")]
        public string vendorName = "Merchant";
        public string vendorDescription = "Welcome to my shop!";
        public int[] itemPrices = {10, 20, 30};
        public string[] itemNames = {"Sword", "Shield", "Potion"};

        private void Start()
        {
            InitializeVendor();
        }

        public void InitializeVendor()
        {
            vendorNameText.text = vendorName;
            vendorDescriptionText.text = vendorDescription;
            
            // Display vendor items
            for (int i = 0; i < itemNames.Length; i++)
            {
                GameObject itemGO = Instantiate(itemPrefab, vendorItemsPanel);
                // Update item display
            }
        }

        public void BuyItem(int itemIndex)
        {
            // Handle buying an item
            // Debug.Log($"Buying item {itemIndex}");
        }

        public void SellItem(int itemIndex)
        {
            // Handle selling an item
            // Debug.Log($"Selling item {itemIndex}");
        }
    }
}