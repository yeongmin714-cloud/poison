using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICanvasComponent
{
    void OnCanvasChanged();
    
    /// <summary>
    /// Gets canvas reference
    /// </summary>
    Canvas GetCanvas();
}