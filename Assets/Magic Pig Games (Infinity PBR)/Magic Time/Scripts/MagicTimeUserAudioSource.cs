using UnityEngine;
using UnityEngine.Serialization;

/*
 * Add this to any Projectile that you want to be affected by the Magic Time system. It will handle
 * the timeScale values for any Audio Sources on the object, adjusting the pitch of the audio.
 */

namespace MagicPigGames.MagicTime
{
    [System.Serializable]
    public class MagicTimeUserAudioSource : MagicTimeMonoBehaviour
    {
        public MagicTimeUser magicTimeUser;
        public AudioSource[] audioSources;
        private float[] _cachedPitches;

        protected float _cachedTimeScale;

        [SerializeField] private bool affectPitch = true; // Option to affect pitch

        [Header("TimeScale and Pitch Ranges")]
        public float minTimeScale = 0f; // Minimum allowed time scale
        public float maxTimeScale = 2f; // Maximum allowed time scale
        public float minPitch = 0.5f;   // Minimum pitch when time scale is at minTimeScale
        public float maxPitch = 1.5f;   // Maximum pitch when time scale is at maxTimeScale

        public virtual void Awake()
        {
            OnValidate();
        }
        
        public virtual void Update()
        {
            // Only update if time scale has changed
            if (Mathf.Approximately(magicTimeUser.TimeScale, _cachedTimeScale))
                return;

            _cachedTimeScale = magicTimeUser.TimeScale;

            // Apply time scaling to the pitch of AudioSources
            for (var i = 0; i < audioSources.Length; i++)
            {
                if (affectPitch)
                {
                    float normalizedTimeScale = Mathf.InverseLerp(minTimeScale, maxTimeScale, _cachedTimeScale);
                    float targetPitch = Mathf.Lerp(minPitch, maxPitch, normalizedTimeScale);
                    audioSources[i].pitch = targetPitch;
                }
            }
        }

        protected void OnEnable()
        {
            // Restore cached pitch values on enable
            for (var i = 0; i < audioSources.Length; i++)
            {
                if (affectPitch)
                    audioSources[i].pitch = _cachedPitches[i];
            }
        }

        // Automatically populate audioSources with all AudioSources in children
        public virtual void OnValidate()
        {
            if (audioSources == null || audioSources.Length == 0)
                audioSources = GetComponentsInChildren<AudioSource>();

            if (magicTimeUser == null)
                magicTimeUser = GetComponent<MagicTimeUser>();

            // Cache default pitch values
            _cachedPitches = new float[audioSources.Length];

            for (var i = 0; i < audioSources.Length; i++)
                _cachedPitches[i] = audioSources[i].pitch;
        }
    }
}