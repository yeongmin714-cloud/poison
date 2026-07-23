using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class SignalManager : MonoBehaviour
{
    private static SignalManager instance;
    public static SignalManager Instance => instance;
    
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
    
    public void SendSignal(string signalName)
    {
        // Implementation for sending signals
    }
    
    public void RegisterSignal(string signalName, System.Action handler)
    {
        // Implementation for registering signals
    }
}