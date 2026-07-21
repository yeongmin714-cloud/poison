using UnityEngine;
using UnityEngine.UI;

public class ScrollbarComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private Scrollbar scrollbar;
    
    public void Initialize()
    {
        if (scrollbar == null)
            scrollbar = GetComponent<Scrollbar>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnValueChanged(float value)
    {
        // Scrollbar value changed implementation
    }
}