using UnityEngine;
using UnityEngine.UI;

public class DropdownComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private Dropdown dropdown;
    
    public void Initialize()
    {
        if (dropdown == null)
            dropdown = GetComponent<Dropdown>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnValueChanged(int value)
    {
        // Dropdown value changed implementation
    }
}