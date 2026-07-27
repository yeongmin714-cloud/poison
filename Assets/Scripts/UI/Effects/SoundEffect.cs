using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Effects
{
    public class SoundEffect : Effect
    {
        [SerializeField] private AudioClip audioClip;
        [SerializeField] private AudioSource audioSource;
        
        public override void ApplyEffect()
        {
            audioSource.PlayOneShot(audioClip);
        }
    }
}