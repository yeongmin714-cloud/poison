using UnityEngine;
using UnityEngine.UI;

public class CanvasComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private Canvas canvas;
    
    public void Initialize()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
}