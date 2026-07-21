using UnityEngine;
using System.Collections.Generic;

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
    }
    
    public void UnregisterDragDropHandler(IDragDropHandler handler)
    {
        dragDropHandlers.Remove(handler);
    }
    
    public void StartDrag(IDragDropHandler handler)
    {
        // Drag start implementation
    }
    
    public void UpdateDrag(IDragDropHandler handler, Vector2 position)
    {
        // Drag update implementation
    }
    
    public void EndDrag(IDragDropHandler handler)
    {
        // Drag end implementation
    }
}