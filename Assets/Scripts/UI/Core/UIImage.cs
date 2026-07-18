using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

public class UIImage : MonoBehaviour
{
    public Image imageComponent;
    public Sprite defaultSprite;
    
    private void Start()
    {
        if(imageComponent != null && defaultSprite != null)
        {
            imageComponent.sprite = defaultSprite;
        }
    }
    
    public void SetImage(Sprite newSprite)
    {
        if(imageComponent != null)
        {
            imageComponent.sprite = newSprite;
        }
    }
}