using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        private Dictionary<string, Dictionary<string, string>> _localizedText = new Dictionary<string, Dictionary<string, string>>();
        private string _currentLanguage = "en";

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

        public string GetText(string key)
        {
            if (_localizedText.TryGetValue(_currentLanguage, out Dictionary<string, string> languageText) &&
                languageText.TryGetValue(key, out string text))
            {
                return text;
            }
            return key;
        }

        public void SetLanguage(string language)
        {
            _currentLanguage = language;
        }

        public void RegisterText(string language, string key, string text)
        {
            if (!_localizedText.ContainsKey(language))
            {
                _localizedText.Add(language, new Dictionary<string, string>());
            }

            _localizedText[language].Add(key, text);
        }
    }
}