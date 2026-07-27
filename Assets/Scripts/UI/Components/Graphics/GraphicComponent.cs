using UnityEngine;
using UnityEngine.UI;

public class GraphicComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private Graphic graphic;
    
    public void Initialize()
    {
        if (graphic == null)
            graphic = GetComponent<Graphic>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    /// <summary>
    /// Sets graphic color
    /// </summary>
    public void SetColor(Color color)
    {
        // Implementation would go here
    }
}