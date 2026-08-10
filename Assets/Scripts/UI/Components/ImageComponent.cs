using UnityEngine;
using Game.UI.Core;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ImageComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private Image image;
    
    public void Initialize()
    {
        if (image == null)
            image = GetComponent<Image>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void SetImage(Sprite sprite)
    {
        // Set image implementation
    }
    
    /// <summary>
    /// Sets image color
    /// </summary>
    public void SetColor(Color color)
    {
        // Implementation would go here
    }
}