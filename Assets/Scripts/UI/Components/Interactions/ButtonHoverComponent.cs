using UnityEngine;
using UnityEngine.UI;

public class ButtonHoverComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private Button button;
    [SerializeField] private Color normalColor;
    [SerializeField] private Color hoverColor;
    
    public void Initialize()
    {
        if (button == null)
            button = GetComponent<Button>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnHoverEnter()
    {
        // Hover enter implementation
    }
    
    public void OnHoverExit()
    {
        // Hover exit implementation
    }
}