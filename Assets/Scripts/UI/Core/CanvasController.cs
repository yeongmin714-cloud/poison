using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

public class CanvasController : MonoBehaviour
{
    private static CanvasController instance;
    public static CanvasController Instance => instance;
    
    private List<RectTransform> canvases = new List<RectTransform>();
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void RegisterCanvas(RectTransform canvas)
    {
        canvases.Add(canvas);
        Debug.Log("Canvas registered");
    }
    
    public void UnregisterCanvas(RectTransform canvas)
    {
        canvases.Remove(canvas);
        Debug.Log("Canvas unregistered");
    }
    
    public void SetCanvasActive(RectTransform canvas, bool active)
    {
        // Implementation for setting canvas active
        // Add actual canvas activation logic here
        Debug.Log("Canvas set to active: " + active);
    }
}