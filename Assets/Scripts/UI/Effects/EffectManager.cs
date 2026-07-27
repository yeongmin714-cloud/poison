using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Effects
{
    public class EffectManager : MonoBehaviour
    {
        [SerializeField] private List<Effect> effects;
        private Dictionary<string, Effect> effectDictionary;
        
        public void Initialize()
        {
            effectDictionary = new Dictionary<string, Effect>();
            foreach(var effect in effects)
            {
                if(effect != null && !effectDictionary.ContainsKey(effect.EffectName))
                {
                    effectDictionary.Add(effect.EffectName, effect);
                }
            }
        }

        public void ApplyEffect(string effectName)
        {
            if(effectDictionary != null && effectDictionary.ContainsKey(effectName))
            {
                effectDictionary[effectName].ApplyEffect();
            }
            else
            {
                // Fallback to searching through all effects
                foreach (var effect in effects)
                {
                    if (effect != null && effect.name == effectName)
                    {
                        effect.ApplyEffect();
                        break;
                    }
                }
            }
        }
        
        public void RemoveEffect(string effectName)
        {
            if(effectDictionary != null && effectDictionary.ContainsKey(effectName))
            {
                effectDictionary.Remove(effectName);
            }
        }
    }
}