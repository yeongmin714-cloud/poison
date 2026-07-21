using UnityEngine;
using UnityEngine.UI;

public class ButtonClickComponent : MonoBehaviour, IUIComponent
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
        // Click implementation
    }
}