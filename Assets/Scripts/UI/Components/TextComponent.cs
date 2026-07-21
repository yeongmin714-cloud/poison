using UnityEngine;
using UnityEngine.UI;

public class TextComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private Text text;
    
    public void Initialize()
    {
        if (text == null)
            text = GetComponent<Text>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void SetText(string newText)
    {
        // Set text implementation
    }
}