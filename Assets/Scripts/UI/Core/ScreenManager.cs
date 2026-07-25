using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }

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

    public void SetScreen(string screenName)
    {
        // Implementation would go here
    }
    
    public void SwitchScreen(string fromScreen, string toScreen)
    {
        // Implementation would go here
    }
}