using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Effects
{
    public class Effect : MonoBehaviour
    {
        [SerializeField] private string effectName;
        [SerializeField] private float duration;
        
        public virtual void ApplyEffect()
        {
            // Base implementation
        }
    }
}