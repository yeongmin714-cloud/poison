using System;
using UnityEngine;
using UnityEngine.Audio;

/*
 * Add this to any object that you want to be affected by the Magic Time system. It will handle
 * the timeScale values for any Audio Mixers on the object, adjusting the pitch of the audio. Generally this would
 * be added to your Audio Mixer.
 *
 * If you need to adjust additional parameters, you can extend this class and override the Update method to add
 * new functionality.
 */

namespace MagicPigGames.MagicTime
{
    [System.Serializable]
    public class MagicTimeUserAudioMixer : MagicTimeMonoBehaviour
    {
        public MagicTimeUser magicTimeUser;
        public AudioMixer audioMixer;
        public string pitchParameter = "Pitch"; // Name of the exposed pitch parameter in the mixer
        
        protected float _cachedTimeScale;

        [SerializeField]
        private bool affectPitch = true; // Option to affect pitch

        [Header("TimeScale and Pitch Ranges")]
        public float minTimeScale = 0f; // Minimum allowed time scale
        public float maxTimeScale = 2f; // Maximum allowed time scale
        public float minPitch = 0.5f;   // Minimum pitch when time scale is at minTimeScale
        public float maxPitch = 1.5f;   // Maximum pitch when time scale is at maxTimeScale

        public void Awake()
        {
            OnValidate();
        }

        public virtual void Update()
        {
            // Only update if time scale has changed
            if (Mathf.Approximately(magicTimeUser.TimeScale, _cachedTimeScale))
                return;

            _cachedTimeScale = magicTimeUser.TimeScale;

            if (affectPitch)
                SetPitch(_cachedTimeScale);
        }

        protected void OnEnable()
        {
            // Reset pitch when re-enabled, just in case
            if (affectPitch)
                SetPitch(1.0f); // Default to normal pitch when re-enabled
        }

        // Set the pitch on the AudioMixer using an exposed parameter, based on the TimeScale value
        protected void SetPitch(float timeScale)
        {
            if (audioMixer == null)
            {
                Debug.LogError($"AudioMixer is not assigned in {gameObject.name}. Please assign a valid AudioMixer.");
                return;
            }

            // Clamp the time scale to the defined range
            timeScale = Mathf.Clamp(timeScale, minTimeScale, maxTimeScale);

            // Map the timeScale to the corresponding pitch range (minPitch to maxPitch)
            float normalizedTimeScale = Mathf.InverseLerp(minTimeScale, maxTimeScale, timeScale);
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, normalizedTimeScale);

            // Apply the pitch to the AudioMixer
            audioMixer.SetFloat(pitchParameter, Mathf.Log10(targetPitch) * 20f); // Logarithmic scaling for proper pitch change
        }

        public virtual void OnValidate()
        {
            if (magicTimeUser == null)
                magicTimeUser = GetComponent<MagicTimeUser>();
        }
    }
}