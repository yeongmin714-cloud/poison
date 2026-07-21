using UnityEngine;
using UnityEngine.UI;

public class SliderComponent : MonoBehaviour, IUIComponent
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
    
    public void OnValueChanged(float value)
    {
        // Slider value changed implementation
    }
}