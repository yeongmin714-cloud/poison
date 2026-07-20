using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class AbilityManager : MonoBehaviour
    {
        public static AbilityManager Instance { get; private set; }

        private Dictionary<string, bool> _abilities = new Dictionary<string, bool>();

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

        public bool IsAbilityActive(string abilityName)
        {
            if (_abilities.TryGetValue(abilityName, out bool active))
            {
                return active;
            }
            return false;
        }

        public void SetAbilityActive(string abilityName, bool active)
        {
            _abilities[abilityName] = active;
        }
    }
}