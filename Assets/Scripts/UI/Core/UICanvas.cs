using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

public class UICanvas : MonoBehaviour
{
    public Canvas canvas;
    public CanvasScaler scaler;
    
    private void Awake()
    {
        if(canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }
    }
}