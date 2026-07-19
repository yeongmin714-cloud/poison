using UnityEngine;
using UnityEngine.UI;

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