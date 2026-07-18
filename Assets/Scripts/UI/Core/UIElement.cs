using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

public class UIElement : MonoBehaviour
{
    public Graphic graphic;
    
    public virtual void Init()
    {
        // Common initialization logic
        Debug.Log("UI Element initialized");
    }
}