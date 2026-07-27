using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class AbilityManager : MonoBehaviour
    {
        [SerializeField] private List<string> abilities;
        
        public void UnlockAbility(string abilityName)
        {
            abilities.Add(abilityName);
        }
    }
}