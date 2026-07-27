using UnityEngine;
using UnityEngine.UI;

public class ButtonComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private Button button;
    
    public void Initialize()
    {
        if (button == null)
            button = GetComponent<Button>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnClick()
    {
        // Button click implementation
    }
    
    /// <summary>
    /// Sets button interactivity state
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        // Implementation would go here
    }
}