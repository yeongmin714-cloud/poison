using UnityEngine;
using UnityEngine.UI;

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