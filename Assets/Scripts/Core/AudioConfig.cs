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

        /// <summary>
        /// soundId로 SoundClipData를 조회합니다. 없으면 null 반환.
        /// </summary>
        public SoundClipData GetClip(string soundId)
        {
            if (string.IsNullOrEmpty(soundId))
            {
                Debug.LogWarning($"[AudioConfig] GetClip called with null or empty soundId");
                return null;
            }

            if (_lookup == null) BuildLookup();

            if (!_lookup.TryGetValue(soundId, out var data))
            {
                Debug.LogWarning($"[AudioConfig] No clip found for soundId '{soundId}'");
                return null;
            }

            return data;
        }

        /// <summary>
        /// clips 리스트를 기반으로 사전 Lookup 테이블을 (재)구축합니다.
        /// 중복 soundId가 발견되면 경고를 출력합니다.
        /// </summary>
        public void BuildLookup()
        {
            _lookup = new Dictionary<string, SoundClipData>(clips.Count);

            foreach (var clip in clips)
            {
                if (clip == null)
                {
                    Debug.LogWarning("[AudioConfig] Null clip entry found in clips list — skipped");
                    continue;
                }

                if (string.IsNullOrEmpty(clip.soundId))
                {
                    Debug.LogWarning($"[AudioConfig] Clip '{clip.name}' has null or empty soundId — skipped");
                    continue;
                }

                if (_lookup.ContainsKey(clip.soundId))
                {
                    Debug.LogWarning($"[AudioConfig] Duplicate soundId '{clip.soundId}' detected — keeping first occurrence, skipping '{clip.name}'");
                    continue;
                }

                _lookup[clip.soundId] = clip;
            }
        }

        private void OnValidate()
        {
            BuildLookup();
        }

        private void OnEnable()
        {
            BuildLookup();
        }
    }
}