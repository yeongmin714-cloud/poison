using UnityEngine;
using UnityEngine.UI;

public class RawImageComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private RawImage rawImage;
    
    public void Initialize()
    {
        if (rawImage == null)
            rawImage = GetComponent<RawImage>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void SetTexture(Texture texture)
    {
        // Set texture implementation
    }
}