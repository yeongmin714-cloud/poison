using UnityEngine;
using UnityEngine.UI;

public class ToggleComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private Toggle toggle;
    
    public void Initialize()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnToggleChanged(bool isOn)
    {
        // Toggle state changed implementation
    }
}