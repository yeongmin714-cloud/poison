using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

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

    public string GetLocalizedString(string key)
    {
        // Implementation would go here
        return "";
    }
    
    public void LoadLocalization(string language)
    {
        // Implementation would go here
    }
}