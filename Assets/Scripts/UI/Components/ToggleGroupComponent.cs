using UnityEngine;
using UnityEngine.UI;

public class ToggleGroupComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private ToggleGroup toggleGroup;
    
    public void Initialize()
    {
        if (toggleGroup == null)
            toggleGroup = GetComponent<ToggleGroup>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnToggleGroupChanged(Toggle toggle)
    {
        // Toggle group changed implementation
    }
}