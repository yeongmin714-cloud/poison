using UnityEngine;
using UnityEngine.UI;

public class TextMeshProComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private TMPro.TextMeshProUGUI textMeshPro;
    
    public void Initialize()
    {
        if (textMeshPro == null)
            textMeshPro = GetComponent<TMPro.TextMeshProUGUI>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    /// <summary>
    /// Sets text content
    /// </summary>
    public void SetText(string text)
    {
        // Implementation would go here
    }
}