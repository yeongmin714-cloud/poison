using UnityEngine;
using System.Collections.Generic;
using System.Collections;

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
    }
    
    public void ShowScreen(string screenName)
    {
        if (screens.TryGetValue(screenName, out GameObject screen))
        {
            screen.SetActive(true);
        }
    }
    
    public void HideScreen(string screenName)
    {
        if (screens.TryGetValue(screenName, out GameObject screen))
        {
            screen.SetActive(false);
        }
    }
}