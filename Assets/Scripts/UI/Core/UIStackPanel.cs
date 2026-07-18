using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

public class UIStackPanel : MonoBehaviour
{
    public RectTransform panelRect;
    public VerticalLayoutGroup layoutGroup;
    
    public void AddChild(GameObject child)
    {
        // Add child to the panel
        child.transform.SetParent(panelRect, false);
    }
}