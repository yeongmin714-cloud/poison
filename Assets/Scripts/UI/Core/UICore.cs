using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core manager for UI systems
/// </summary>
public class UICore : MonoBehaviour
{
    public static UICore Instance { get; private set; }
    
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
    
    /// <summary>
    /// Initializes the UI core system
    /// </summary>
    public void Initialize()
    {
        // Initialization logic here
    }
    
    /// <summary>
    /// Cleans up the UI core system
    /// </summary>
    public void Cleanup()
    {
        // Cleanup logic here
    }
}