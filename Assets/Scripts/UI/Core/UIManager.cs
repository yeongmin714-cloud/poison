using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public UIScreen[] screens;
    
    private void Start()
    {
        Debug.Log("UI Manager initialized");
    }
    
    public void ShowScreen(UIScreen screen)
    {
        // TODO: Implement screen showing logic
        foreach(var s in screens)
        {
            s.gameObject.SetActive(s == screen);
        }
    }
}