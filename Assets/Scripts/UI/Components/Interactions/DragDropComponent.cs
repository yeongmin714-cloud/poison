using UnityEngine;
using UnityEngine.UI;

public class DragDropComponent : MonoBehaviour, IUIComponent, IDragDropHandler
{
    [SerializeField] private RectTransform rectTransform;
    
    public void Initialize()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnDragStart()
    {
        // Drag start implementation
    }
    
    public void OnDrag()
    {
        // Drag implementation
    }
    
    public void OnDragEnd()
    {
        // Drag end implementation
    }
}