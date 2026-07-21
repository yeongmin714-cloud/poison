using UnityEngine;
using UnityEngine.UI;

public class RectTransformComponent : MonoBehaviour, IUIComponent
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
    
    public void SetPosition(Vector2 position)
    {
        // Set position implementation
    }
}