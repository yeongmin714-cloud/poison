using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIItemTooltip : MonoBehaviour
    {
        [Header("UI References")]
        public Text itemNameText;
        public Text itemDescriptionText;
        public Image itemIcon;
        public GameObject tooltipPanel;
        
        [Header("Item Data")]
        public string itemName = "Item";
        public string itemDescription = "Item Description";
        public Sprite itemSprite;

        public void ShowTooltip(string name, string description, Sprite sprite)
        {
            itemNameText.text = name;
            itemDescriptionText.text = description;
            itemIcon.sprite = sprite;
            
            tooltipPanel.SetActive(true);
        }

        public void HideTooltip()
        {
            tooltipPanel.SetActive(false);
        }
    }
}