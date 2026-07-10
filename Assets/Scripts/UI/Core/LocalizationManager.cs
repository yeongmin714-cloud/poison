using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class LocalizationManager : MonoBehaviour
    {
        private Dictionary<string, string> localizedStrings = new Dictionary<string, string>();

        public static LocalizationManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
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
    }
}