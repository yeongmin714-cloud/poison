using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Effects
{
    public class EffectManager : MonoBehaviour
    {
        [SerializeField] private List<Effect> effects;
        
        public void ApplyEffect(string effectName)
        {
            foreach (var effect in effects)
            {
                if (effect.name == effectName)
                {
                    effect.ApplyEffect();
                    break;
                }
            }
        }
    }
}