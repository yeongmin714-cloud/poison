using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

public class ScreenManager : MonoBehaviour
{
    private static ScreenManager instance;
    public static ScreenManager Instance => instance;
    
    private Dictionary<string, GameObject> screens = new Dictionary<string, GameObject>();
    
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
    
    public void RegisterScreen(string screenName, GameObject screen)
    {
        screens[screenName] = screen;
        Debug.Log("Screen registered: " + screenName);
    }
    
    public void ShowScreen(string screenName)
    {
        if (screens.TryGetValue(screenName, out GameObject screen))
        {
            screen.SetActive(true);
            Debug.Log("Screen shown: " + screenName);
        }
    }
    
    public void HideScreen(string screenName)
    {
        if (screens.TryGetValue(screenName, out GameObject screen))
        {
            screen.SetActive(false);
            Debug.Log("Screen hidden: " + screenName);
        }
    }
}