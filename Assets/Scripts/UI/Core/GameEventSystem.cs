using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameEventSystem : MonoBehaviour
{
    public static GameEventSystem Instance { get; private set; }

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

    public void TriggerEvent(string eventName)
    {
        // Implementation would go here
    }
    
    public void SubscribeEvent(string eventName, System.Action callback)
    {
        // Implementation would go here
    }
}