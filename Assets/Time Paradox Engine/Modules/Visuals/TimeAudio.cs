using UnityEngine;
using TimeWarp.Core;

namespace TimeWarp.Modules.Audio
{
    [RequireComponent(typeof(TimeWarpIdentity))]
    [RequireComponent(typeof(AudioSource))]
    [DefaultExecutionOrder(-50)]
    public class TimeAudio : MonoBehaviour, ITimeWarpModule
    {
        private TimeWarpIdentity _identity;
        private AudioSource _source;
        private AudioLowPassFilter _filter;

        private float _basePitch;
        private float _baseVolume;
        private bool _wasPausedByWarp = false;
        private bool _isInitialized = false;

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            _source = GetComponent<AudioSource>();
        }

        private void Start() => InitializeModule();

        private void InitializeModule()
        {
            if (_isInitialized) return;
            
            // Cache original audio settings so we can restore them later
            _basePitch = _source.pitch;
            _baseVolume = _source.volume;

            // Dynamically add a Low Pass Filter if the user wants the "underwater" cinematic effect
            if (_identity.AudioSettings != null && _identity.AudioSettings.ApplyLowPassFilter)
            {
                _filter = gameObject.GetComponent<AudioLowPassFilter>();
                if (_filter == null) _filter = gameObject.AddComponent<AudioLowPassFilter>();
                _filter.enabled = false; 
            }
            
            _isInitialized = true;
        }

        // We don't need fixed update for audio since it's processed on a separate DSP thread
        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate()
        {
            // Exit early if audio channel is disabled or identity is not registered
            if (!_identity.IsRegistered || (_identity.Channels & TimeChannels.Audio) == 0) return;
            if (!_isInitialized) InitializeModule();

            float timeScale = _identity.EffectiveTimeScale;
            bool isRewinding = _identity.IsRewinding;
            var settings = _identity.AudioSettings;

            // 1. HARD PAUSE (TIME STOP OR MUTED REWIND)
            // If time is completely stopped OR we are rewinding but the user chose to mute this sound
            if ((!isRewinding && timeScale <= 0.001f) || (isRewinding && settings.MuteOnRewind))
            {
                if (_source.isPlaying) 
                { 
                    _source.Pause(); 
                    _wasPausedByWarp = true; 
                }
                return; // Exit here to prevent further pitch calculations
            }
            // 2. RESUME AFTER TIME STOP
            else if (_wasPausedByWarp)
            {
                _source.UnPause();
                _wasPausedByWarp = false;
            }

            // 3. DYNAMIC PITCH SCALING (SLOW-MO & REWIND)
            if (settings.AffectPitch)
            {
                if (isRewinding)
                {
                    _source.volume = _baseVolume;
                    
                    // Default rewind speed multiplier (1.0 means it plays backward at normal speed)
                    float rewindSpeed = 1.0f; 

                    // If timescale is actually negative (e.g. -2.0 for 2x rewind), use it.
                    // Otherwise, we keep 1.0f so it doesn't get stuck at 0.1 if timeScale is 0 during physical rewind.
                    if (timeScale < -0.01f) 
                    {
                        rewindSpeed = Mathf.Abs(timeScale);
                    }

                    float targetPitch = -(_basePitch * rewindSpeed);
                    
                    // Clamp to valid Unity bounds. Negative pitch forces Unity to play the buffer backward.
                    // IMPORTANT: AudioClip Load Type MUST be "Decompress On Load" or it will ignore negative pitch!
                    _source.pitch = Mathf.Clamp(targetPitch, -3f, -0.01f); 
                    
                    if (!_source.isPlaying)
                    {
                        // If the sound has already finished playing (time is 0), 
                        // we must artificially move the "playback head" to the very end of the clip to suck it backward.
                        if (_source.time == 0f && _source.clip != null)
                        {
                            _source.time = _source.clip.length - 0.001f;
                        }
                        _source.Play();
                    }
                }
                else
                {
                    _source.volume = _baseVolume;
                    
                    // FLOOR LIMIT: Prevent the pitch from dropping so low that the sound turns into demonic crackling.
                    // 0.35f ensures the sound is deep and slow, but remains coherent.
                    float safeTimeScale = Mathf.Max(0.35f, timeScale); 
                    
                    float targetPitch = _basePitch * safeTimeScale;
                    _source.pitch = Mathf.Min(targetPitch, settings.MaxPitchLimit);
                }
            }

            // 4. CINEMATIC LOW PASS FILTER
            if (_filter != null)
            {
                // Activate the filter only if time is altered
                bool shouldBeActive = Mathf.Abs(timeScale - 1f) > 0.01f || isRewinding;
                
                if (_filter.enabled != shouldBeActive) _filter.enabled = shouldBeActive;
                
                if (shouldBeActive)
                {
                    // If rewinding, muffle the sound heavily (800Hz).
                    // If slow-motion, lerp the cutoff frequency smoothly based on timeScale.
                    float targetCutoff = isRewinding ? 800f : Mathf.Lerp(500f, 5000f, Mathf.Clamp01(timeScale));
                    _filter.cutoffFrequency = targetCutoff;
                }
            }
        }

        private void OnDisable() => RestoreOriginalAudioState();
        private void OnDestroy() => RestoreOriginalAudioState();

        // Safety cleanup to ensure audio returns to normal when the script is disabled or destroyed
        private void RestoreOriginalAudioState()
        {
            if (_source != null && _isInitialized)
            {
                _source.pitch = _basePitch;
                _source.volume = _baseVolume;
                if (_wasPausedByWarp) 
                { 
                    _source.UnPause(); 
                    _wasPausedByWarp = false; 
                }
            }
            if (_filter != null) _filter.enabled = false;
        }
    }
}