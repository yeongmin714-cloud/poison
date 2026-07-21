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
}