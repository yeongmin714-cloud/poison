using UnityEngine;
using UnityEngine.Localization;

public class UIPanel : MonoBehaviour
{
    public RectTransform panelRect;
    
    public void SetActive(bool active)
    {
        if(panelRect != null)
        {
            panelRect.gameObject.SetActive(active);
        }
    }
}