using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

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

    public void InitializeUI()
    {
        // Implementation would go here
    }
    
    public void ShowUI(string uiName)
    {
        // Implementation would go here
    }
    
    public void HideUI(string uiName)
    {
        // Implementation would go here
    }
}