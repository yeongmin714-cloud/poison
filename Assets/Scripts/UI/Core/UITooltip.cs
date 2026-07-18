using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;

public class UITooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject tooltip;
    
    private void Start()
    {
        // Initialize tooltip
        if(tooltip != null)
        {
            tooltip.SetActive(false);
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if(tooltip != null)
        {
            tooltip.SetActive(true);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if(tooltip != null)
        {
            tooltip.SetActive(false);
        }
    }
}