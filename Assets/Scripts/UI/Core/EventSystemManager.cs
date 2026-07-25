using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventSystemManager : MonoBehaviour
{
    public static EventSystemManager Instance { get; private set; }

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

    public void RegisterEvent(string eventName, System.Action callback)
    {
        // Implementation would go here
    }
    
    public void UnregisterEvent(string eventName, System.Action callback)
    {
        // Implementation would go here
    }
}