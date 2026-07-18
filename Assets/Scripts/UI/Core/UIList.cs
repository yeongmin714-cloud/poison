using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

public class UIList : MonoBehaviour
{
    public RectTransform contentPanel;
    public GameObject listItemPrefab;
    
    public void AddItem(string itemText)
    {
        if(listItemPrefab != null && contentPanel != null)
        {
            GameObject newItem = Instantiate(listItemPrefab, contentPanel);
            // Configure the new item...
        }
    }
}