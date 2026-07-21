using UnityEngine;
using System.Collections.Generic;

public class MessageSystem : MonoBehaviour
{
    private static MessageSystem instance;
    public static MessageSystem Instance => instance;
    
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
    
    public void ShowMessage(string message)
    {
        // Implementation for showing messages
    }
    
    public void ShowMessage(string message, float duration)
    {
        // Implementation for showing messages with duration
    }
}