using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interface for UI components that need initialization and cleanup
/// </summary>
public interface IUIComponent
{
    /// <summary>
    /// Initializes the UI component
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Cleans up the UI component
    /// </summary>
    void Cleanup();
}