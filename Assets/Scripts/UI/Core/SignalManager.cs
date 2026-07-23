using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

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
        // Add actual signal sending logic here
        Debug.Log("Signal sent: " + signalName);
    }
    
    public void RegisterSignal(string signalName, System.Action handler)
    {
        // Implementation for registering signals
        // Add actual signal registration logic here
        Debug.Log("Signal registered: " + signalName);
    }
}