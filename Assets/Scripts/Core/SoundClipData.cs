using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 각 사운드 클립의 데이터 정의.
    /// 실제 .wav/.mp3가 없으면 proceduralPitch로 절차적 사운드 생성.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSoundClip", menuName = "Sound/SoundClipData")]
    public class SoundClipData : ScriptableObject
    {
        [Header("식별")]
        public string soundId = "unnamed";
        public SoundType soundType = SoundType.SFX;
        public string category = "general";

        [Header("오디오 클립 (비우면 절차적 사운드 사용)")]
        public AudioClip clip;

        [Header("절차적 사운드 설정")]
        [Tooltip("clip이 null일 때 사용할 주파수 (Hz)")]
        public float proceduralPitch = 440f;
        [Tooltip("파형 타입: 0=sine, 1=square, 2=sawtooth, 3=triangle")]
        public int waveformType = 0;
        [Tooltip("절차적 사운드 길이 (초)")]
        public float proceduralDuration = 0.3f;

        [Header("재생 설정")]
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 3f)] public float pitchMin = 1f;
        [Range(0f, 3f)] public float pitchMax = 1f;
        public bool isLoop = false;
        [Range(0f, 1f)] public float spatialBlend = 0f; // 0=2D, 1=3D
    }
}