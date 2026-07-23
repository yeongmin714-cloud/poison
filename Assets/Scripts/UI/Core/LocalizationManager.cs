using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class LocalizationManager : MonoBehaviour
{
    private static LocalizationManager instance;
    public static LocalizationManager Instance => instance;
    
    private Dictionary<string, string> localizedStrings = new Dictionary<string, string>();
    
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
    
    public void LoadLocalizationData(string language)
    {
        // Implementation for loading localization data
    }
    
    public string GetLocalizedString(string key)
    {
        if (localizedStrings.TryGetValue(key, out string value))
        {
            return value;
        }
        return key;
    }
    
    public void SetLocalizedString(string key, string value)
    {
        localizedStrings[key] = value;
    }
}