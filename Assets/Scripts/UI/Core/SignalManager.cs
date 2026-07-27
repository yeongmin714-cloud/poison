using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SignalManager : MonoBehaviour
{
    public static SignalManager Instance { get; private set; }
    
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
    
    public void SendSignal(string signal)
    {
        // Implementation would go here
    }
    
    public void RegisterSignal(string signal, System.Action callback)
    {
        // Implementation would go here
    }
    
    /// <summary>
    /// Unregisters a signal handler
    /// </summary>
    public void UnregisterSignal(string signal)
    {
        // Implementation would go here
    }
}