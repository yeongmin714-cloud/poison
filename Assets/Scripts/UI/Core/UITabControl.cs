using UnityEngine;
using UnityEngine.UI;

public class UITabControl : MonoBehaviour
{
    public ToggleGroup tabGroup;
    public GameObject[] tabContent;
    
    public void SelectTab(int tabIndex)
    {
        // Logic to select tab
        Debug.Log($"Selecting tab {tabIndex}");
    }
}