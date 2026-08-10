using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Effects
{
    public class ParticleEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem particleSystem;
        [SerializeField] private bool autoDestroy = true;
        
        public float Duration { get; set; } = 1f;
        
        public virtual void ApplyEffect()
        {
            if(particleSystem != null)
            {
                particleSystem.Play();
            }
            
            if(autoDestroy && particleSystem != null)
            {
                // Delayed destruction after duration
                Destroy(gameObject, Duration);
            }
        }
        
        public void StopEffect()
        {
            if(particleSystem != null)
            {
                particleSystem.Stop();
            }
        }
    }
}