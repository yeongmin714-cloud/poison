using UnityEngine;
using UnityEngine.UI;

public class UIElement : MonoBehaviour
{
    public Graphic graphic;
    
    public virtual void Init()
    {
        // Common initialization logic
        Debug.Log("UI Element initialized");
    }
}