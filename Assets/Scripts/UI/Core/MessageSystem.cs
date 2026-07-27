using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MessageSystem : MonoBehaviour
{
    public static MessageSystem Instance { get; private set; }
    
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
    
    public void SendMessage(string message)
    {
        // Implementation would go here
    }
    
    public void BroadcastMessage(string message)
    {
        // Implementation would go here
    }
    
    /// <summary>
    /// Clears all messages
    /// </summary>
    public void ClearMessages()
    {
        // Implementation would go here
    }
}