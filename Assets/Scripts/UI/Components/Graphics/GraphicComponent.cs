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
}