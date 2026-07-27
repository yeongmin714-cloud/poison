using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Effects
{
    public class ParticleEffect : Effect
    {
        [SerializeField] private ParticleSystem particleSystem;
        
        public override void ApplyEffect()
        {
            particleSystem.Play();
        }
    }
}