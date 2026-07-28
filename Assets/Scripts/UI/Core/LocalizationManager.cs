using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class LocalizationManager : MonoBehaviour
    {
        private Dictionary<string, string> localizedStrings;

        public void Initialize()
        {
            localizedStrings = new Dictionary<string, string>();
        }

        public string GetLocalizedString(string key)
        {
            if(localizedStrings != null && localizedStrings.ContainsKey(key))
            {
                return localizedStrings[key];
            }
            return key;
        }

        public void SetLocalizedString(string key, string value)
        {
            if(localizedStrings == null)
                Initialize();

            localizedStrings[key] = value;
        }
    }
}