using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// AudioConfig ScriptableObject — 모든 사운드 클립 정의를 관리.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Sound/AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        [Header("볼륨 설정")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Range(0f, 1f)] public float uiVolume = 1f;

        [Header("사운드 클립 목록")]
        public List<SoundClipData> clips = new List<SoundClipData>();

        private Dictionary<string, SoundClipData> _lookup;

        public SoundClipData GetClip(string soundId)
        {
            if (_lookup == null) BuildLookup();
            _lookup.TryGetValue(soundId, out var data);
            return data;
        }

        public void BuildLookup()
        {
            _lookup = new Dictionary<string, SoundClipData>();
            foreach (var clip in clips)
            {
                if (clip != null && !string.IsNullOrEmpty(clip.soundId) && !_lookup.ContainsKey(clip.soundId))
                {
                    _lookup[clip.soundId] = clip;
                }
            }
        }

        private void OnValidate()
        {
            BuildLookup();
        }
    }
}