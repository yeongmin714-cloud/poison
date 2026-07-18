using UnityEngine;
using UnityEngine.Localization;

public class UIListItem : MonoBehaviour
{
    public string itemText = "";
    
    public void SetItemText(string text)
    {
        itemText = text;
    }
}