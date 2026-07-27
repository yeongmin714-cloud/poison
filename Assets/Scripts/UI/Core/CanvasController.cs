using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasController : MonoBehaviour
{
    public static CanvasController Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void SetCanvasActive(Canvas canvas, bool active)
    {
        // Implementation would go here
    }
    
    public void SetCanvasSortingOrder(Canvas canvas, int sortingOrder)
    {
        // Implementation would go here
    }
    
    /// <summary>
    /// Gets canvas world camera
    /// </summary>
    public Camera GetCanvasWorldCamera(Canvas canvas)
    {
        // Implementation would go here
        return null;
    }
}