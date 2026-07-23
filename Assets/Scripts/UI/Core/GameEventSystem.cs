using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

public class GameEventSystem : MonoBehaviour
{
    private static GameEventSystem instance;
    public static GameEventSystem Instance => instance;
    
    private Dictionary<string, List<System.Action>> eventHandlers = new Dictionary<string, List<System.Action>>();
    
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
    
    public void RegisterEvent(string eventName, System.Action handler)
    {
        if (!eventHandlers.ContainsKey(eventName))
        {
            eventHandlers[eventName] = new List<System.Action>();
        }
        eventHandlers[eventName].Add(handler);
        // Add actual event registration logic here
        Debug.Log("Event registered: " + eventName);
    }
    
    public void UnregisterEvent(string eventName, System.Action handler)
    {
        if (eventHandlers.TryGetValue(eventName, out List<System.Action> handlers))
        {
            handlers.Remove(handler);
        }
        // Add actual event unregistration logic here
        Debug.Log("Event unregistered: " + eventName);
    }
    
    public void TriggerEvent(string eventName)
    {
        if (eventHandlers.TryGetValue(eventName, out List<System.Action> handlers))
        {
            foreach (var handler in handlers)
            {
                handler?.Invoke();
            }
        }
        // Add actual event triggering logic here
        Debug.Log("Event triggered: " + eventName);
    }
}