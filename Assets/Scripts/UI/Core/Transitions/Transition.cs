using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Transition : MonoBehaviour
{
    public virtual void Play()
    {
        // Base implementation
    }
    
    public virtual void Stop()
    {
        // Base implementation
    }
    
    /// <summary>
    /// Updates transition state
    /// </summary>
    public virtual void UpdateTransition()
    {
        // Base implementation
    }
}