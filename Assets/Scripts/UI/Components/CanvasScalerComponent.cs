using UnityEngine;
using UnityEngine.UI;

public class CanvasScalerComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private CanvasScaler canvasScaler;
    
    public void Initialize()
    {
        if (canvasScaler == null)
            canvasScaler = GetComponent<CanvasScaler>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
}