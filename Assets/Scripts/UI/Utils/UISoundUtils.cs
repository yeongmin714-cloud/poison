using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UISoundUtils : MonoBehaviour
    {
        [Header("Sound Settings")]
        public AudioSource audioSource;
        public AudioClip[] soundEffects;

        public void PlaySound(string soundName)
        {
            // Play a specific sound effect
            Debug.Log($"Playing sound: {soundName}");
        }

        public void PlaySound(AudioClip clip)
        {
            // Play audio clip
            if (audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        public void PlayBackgroundMusic(AudioClip music)
        {
            // Play background music
            if (audioSource != null)
            {
                audioSource.clip = music;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
    }
}