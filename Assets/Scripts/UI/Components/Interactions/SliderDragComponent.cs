using UnityEngine;
using UnityEngine.UI;

public class SliderDragComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private Slider slider;
    
    public void Initialize()
    {
        if (slider == null)
            slider = GetComponent<Slider>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnDragStarted()
    {
        // Drag started implementation
    }
    
    public void OnDragChanged(float value)
    {
        // Drag changed implementation
    }
    
    public void OnDragEnded()
    {
        // Drag ended implementation
    }
}