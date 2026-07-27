using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Effects
{
    public class SoundEffect : Effect
    {
        [SerializeField] private AudioClip audioClip;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool loopSound = false;
        
        public override void ApplyEffect()
        {
            if(audioSource != null && audioClip != null)
            {
                audioSource.loop = loopSound;
                audioSource.PlayOneShot(audioClip);
            }
        }
        
        public void StopEffect()
        {
            if(audioSource != null)
            {
                audioSource.Stop();
            }
        }
    }
}