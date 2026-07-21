using UnityEngine;
using UnityEngine.UI;

public class ScrollRectComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private ScrollRect scrollRect;
    
    public void Initialize()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnValueChanged(Vector2 delta)
    {
        // Scroll rect value changed implementation
    }
}