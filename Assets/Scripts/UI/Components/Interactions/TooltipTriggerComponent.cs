using UnityEngine;
using UnityEngine.UI;

public class TooltipTriggerComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private ToolTipManager tooltipManager;
    [SerializeField] private string tooltipText;
    
    public void Initialize()
    {
        // Initialization logic
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnPointerEnter()
    {
        // Pointer enter implementation
    }
    
    public void OnPointerExit()
    {
        // Pointer exit implementation
    }
}