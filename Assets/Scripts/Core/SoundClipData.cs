using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 각 사운드 클립의 데이터 정의.
    /// 실제 .wav/.mp3가 없으면 procedural 설정으로 절차적 사운드 생성.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSoundClip", menuName = "Sound/SoundClipData")]
    public class SoundClipData : ScriptableObject
    {
        [Header("식별")]
        public string soundId = "unnamed";
        public SoundType soundType = SoundType.SFX;

        [Header("오디오 클립 (비우면 절차적 사운드 사용)")]
        public AudioClip clip;

        [Header("절차적 사운드 설정")]
        [Tooltip("clip이 null일 때 사용할 주파수 (Hz)")]
        public float proceduralFrequency = 440f;
        [Tooltip("파형 타입")]
        public WaveformType waveformType = WaveformType.Sine;
        [Tooltip("절차적 사운드 길이 (초)")]
        public float proceduralDuration = 0.3f;

        [Header("재생 설정")]
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 3f)] public float pitchMin = 1f;
        [Range(0f, 3f)] public float pitchMax = 1f;
        public bool loop = false;
        [Range(0f, 1f)] public float spatialBlend = 0f; // 0=2D, 1=3D

        private void OnValidate()
        {
            // pitchMin은 pitchMax를 초과할 수 없음
            if (pitchMin > pitchMax)
            {
                Debug.LogWarning($"[SoundClipData] '{name}': pitchMin({pitchMin}) > pitchMax({pitchMax}). pitchMin을 pitchMax로 조정합니다.");
                pitchMin = pitchMax;
            }

            // 절차적 사운드 길이는 최소 0.01초
            if (proceduralDuration < 0.01f)
            {
                Debug.LogWarning($"[SoundClipData] '{name}': proceduralDuration({proceduralDuration})이 너무 짧습니다. 0.01s로 조정합니다.");
                proceduralDuration = 0.01f;
            }
        }
    }
}