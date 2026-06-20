using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 중앙 사운드 관리 싱글톤.
    /// 절차적(procedural) 사운드 생성 + 실제 AudioClip 재생 지원.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("설정")]
        public AudioConfig config;

        [Header("오디오 소스 (자동 생성)")]
        public AudioSource bgmSource;
        public AudioSource sfxSource;
        public AudioSource uiSource;

        private Dictionary<string, AudioClip> _proceduralClips = new Dictionary<string, AudioClip>();
        private string _currentBGMId = null;
        private int _sampleRate = 44100;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // AudioSource 자동 생성
            if (bgmSource == null)
            {
                var go = new GameObject("BGM_Source");
                go.transform.SetParent(transform);
                bgmSource = go.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.spatialBlend = 0f;
            }
            if (sfxSource == null)
            {
                var go = new GameObject("SFX_Source");
                go.transform.SetParent(transform);
                sfxSource = go.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0f;
            }
            if (uiSource == null)
            {
                var go = new GameObject("UI_Source");
                go.transform.SetParent(transform);
                uiSource = go.AddComponent<AudioSource>();
                uiSource.loop = false;
                uiSource.spatialBlend = 0f;
            }

            if (config != null) config.BuildLookup();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================
        //  BGM
        // ================================================================

        public void PlayBGM(string soundId)
        {
            var data = GetSoundData(soundId);
            if (data == null) return;

            if (_currentBGMId == soundId && bgmSource.isPlaying) return;
            _currentBGMId = soundId;

            AudioClip clip = GetOrCreateClip(data);
            if (clip == null) return;

            float vol = GetEffectiveVolume(data);
            bgmSource.volume = vol;
            bgmSource.pitch = Random.Range(data.pitchMin, data.pitchMax);
            bgmSource.clip = clip;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            bgmSource.Stop();
            _currentBGMId = null;
        }

        // ================================================================
        //  SFX (2D)
        // ================================================================

        public void PlaySFX(string soundId)
        {
            var data = GetSoundData(soundId);
            if (data == null) return;

            AudioClip clip = GetOrCreateClip(data);
            if (clip == null) return;

            float vol = GetEffectiveVolume(data);
            sfxSource.pitch = Random.Range(data.pitchMin, data.pitchMax);
            sfxSource.PlayOneShot(clip, vol);
        }

        // ================================================================
        //  UI
        // ================================================================

        public void PlayUI(string soundId)
        {
            var data = GetSoundData(soundId);
            if (data == null) return;

            AudioClip clip = GetOrCreateClip(data);
            if (clip == null) return;

            float vol = GetEffectiveVolume(data);
            uiSource.pitch = Random.Range(data.pitchMin, data.pitchMax);
            uiSource.PlayOneShot(clip, vol);
        }

        // ================================================================
        //  볼륨 제어
        // ================================================================

        public void SetVolume(SoundType type, float volume)
        {
            if (config == null) return;
            volume = Mathf.Clamp01(volume);
            switch (type)
            {
                case SoundType.BGM: config.bgmVolume = volume; break;
                case SoundType.SFX: config.sfxVolume = volume; break;
                case SoundType.UI: config.uiVolume = volume; break;
            }
            ApplyVolume(type);
        }

        public float GetVolume(SoundType type)
        {
            if (config == null) return 1f;
            switch (type)
            {
                case SoundType.BGM: return config.bgmVolume;
                case SoundType.SFX: return config.sfxVolume;
                case SoundType.UI: return config.uiVolume;
                default: return 1f;
            }
        }

        private void ApplyVolume(SoundType type)
        {
            if (config == null) return;
            switch (type)
            {
                case SoundType.BGM:
                    bgmSource.volume = config.bgmVolume * config.masterVolume;
                    break;
                case SoundType.SFX:
                    sfxSource.volume = config.sfxVolume * config.masterVolume;
                    break;
                case SoundType.UI:
                    uiSource.volume = config.uiVolume * config.masterVolume;
                    break;
            }
        }

        public void MuteAll()
        {
            bgmSource.mute = true;
            sfxSource.mute = true;
            uiSource.mute = true;
        }

        public void UnmuteAll()
        {
            bgmSource.mute = false;
            sfxSource.mute = false;
            uiSource.mute = false;
        }

        public void StopAll()
        {
            bgmSource.Stop();
            sfxSource.Stop();
            uiSource.Stop();
            _currentBGMId = null;
        }

        // ================================================================
        //  내부 헬퍼
        // ================================================================

        private SoundClipData GetSoundData(string soundId)
        {
            if (config == null) return null;
            return config.GetClip(soundId);
        }

        private float GetEffectiveVolume(SoundClipData data)
        {
            if (config == null) return data.volume;
            float catVol = 1f;
            switch (data.soundType)
            {
                case SoundType.BGM: catVol = config.bgmVolume; break;
                case SoundType.SFX: catVol = config.sfxVolume; break;
                case SoundType.UI: catVol = config.uiVolume; break;
            }
            return data.volume * catVol * config.masterVolume;
        }

        private AudioClip GetOrCreateClip(SoundClipData data)
        {
            if (data.clip != null) return data.clip;

            // 절차적 사운드 생성 (캐싱)
            if (!_proceduralClips.TryGetValue(data.soundId, out var clip) || clip == null)
            {
                clip = GenerateProceduralClip(data);
                if (clip != null)
                    _proceduralClips[data.soundId] = clip;
            }
            return clip;
        }

        private AudioClip GenerateProceduralClip(SoundClipData data)
        {
            int lengthSamples = Mathf.Max(1, Mathf.RoundToInt(_sampleRate * data.proceduralDuration));
            AudioClip clip = AudioClip.Create(data.soundId, lengthSamples, 1, _sampleRate, false);

            float[] samples = new float[lengthSamples];
            float freq = data.proceduralPitch;
            float amplitude = 0.3f;

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / _sampleRate;
                float phase = t * freq;

                switch (data.waveformType)
                {
                    case 0: // Sine
                        samples[i] = Mathf.Sin(2f * Mathf.PI * phase) * amplitude;
                        break;
                    case 1: // Square
                        samples[i] = (Mathf.Sin(2f * Mathf.PI * phase) >= 0 ? 1f : -1f) * amplitude;
                        break;
                    case 2: // Sawtooth
                        samples[i] = (2f * (phase - Mathf.Floor(phase)) - 1f) * amplitude;
                        break;
                    case 3: // Triangle
                        float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                        samples[i] = (2f * Mathf.Abs(saw) - 1f) * amplitude;
                        break;
                }

                // 페이드 아웃 (마지막 10%)
                float fadeStart = lengthSamples * 0.9f;
                if (i > fadeStart)
                {
                    float fadeFactor = 1f - (i - fadeStart) / (lengthSamples - fadeStart);
                    samples[i] *= fadeFactor;
                }
            }

            clip.SetData(samples, 0);
            return clip;
        }
    }
}