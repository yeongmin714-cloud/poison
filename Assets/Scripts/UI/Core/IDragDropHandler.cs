using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDragDropHandler
{
    void OnDragStart();
    void OnDragEnd();
    
    /// <summary>
    /// Handles drag drop event
    /// </summary>
    void OnDrop();
}