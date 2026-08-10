using UnityEngine;
using Game.UI.Core;
using UnityEngine.EventSystems;
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
    
    /// <summary>
    /// Sets canvas render mode
    /// </summary>
    public void SetRenderMode(RenderMode renderMode)
    {
        // Implementation would go here
    }
}