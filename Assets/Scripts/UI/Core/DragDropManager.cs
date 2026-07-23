using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

public class DragDropManager : MonoBehaviour
{
    private static DragDropManager instance;
    public static DragDropManager Instance => instance;
    
    private List<IDragDropHandler> dragDropHandlers = new List<IDragDropHandler>();
    
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
    
    public void RegisterDragDropHandler(IDragDropHandler handler)
    {
        dragDropHandlers.Add(handler);
        // Add actual drag drop handler registration logic here
    }
    
    public void UnregisterDragDropHandler(IDragDropHandler handler)
    {
        dragDropHandlers.Remove(handler);
        // Add actual drag drop handler unregistration logic here
    }
    
    public void StartDrag(IDragDropHandler handler)
    {
        // Drag start implementation
        // Add actual drag start logic here
    }
    
    public void UpdateDrag(IDragDropHandler handler, Vector2 position)
    {
        // Drag update implementation
        // Add actual drag update logic here
    }
    
    public void EndDrag(IDragDropHandler handler)
    {
        // Drag end implementation
        // Add actual drag end logic here
    }
}