using UnityEngine;
using UnityEngine.UI;

public class ImageMeshProComponent : MonoBehaviour, IUIComponent
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
}