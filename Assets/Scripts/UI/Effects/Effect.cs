using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Effects
{
    public class Effect : ScriptableObject
    {
        [SerializeField] private string effectName;
        [SerializeField] private float duration;
        [SerializeField] private bool isPersistent = false;
        
        public virtual void ApplyEffect()
        {
            // Base implementation
        }
        
        public string EffectName => effectName;
        public float Duration => duration;
        public bool IsPersistent => isPersistent;
    }
}