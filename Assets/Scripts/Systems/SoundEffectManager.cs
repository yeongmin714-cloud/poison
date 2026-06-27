using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-19: 효과음(SFX) 관리자 싱글톤.
    /// AudioSource 풀을 관리하여 동시에 여러 효과음을 재생할 수 있습니다.
    /// DontDestroyOnLoad로 씬 전환 시 유지됩니다.
    /// 현재는 플레이스홀더 단계 — 실제 오디오 파일 없이 Debug.Log로 대체됩니다.
    /// </summary>
    public class SoundEffectManager : MonoBehaviour
    {
        /// <summary>
        /// 사용 가능한 효과음 종류.
        /// </summary>
        public enum SFXType
        {
            /// <summary>발소리</summary>
            Footstep,

            /// <summary>자원 수집</summary>
            Gather,

            /// <summary>제작</summary>
            Craft,

            /// <summary>전투 — 피격</summary>
            Combat_Hit,

            /// <summary>전투 — 휘두름</summary>
            Combat_Swing,

            /// <summary>암살</summary>
            Assassination,

            /// <summary>문 열림</summary>
            DoorOpen,

            /// <summary>문 닫힘</summary>
            DoorClose,

            /// <summary>아이템 획득</summary>
            ItemPickup,

            /// <summary>아이템 버림</summary>
            ItemDrop,

            /// <summary>경보</summary>
            Alarm,

            /// <summary>승리</summary>
            Victory,

            /// <summary>패배</summary>
            Defeat
        }

        // ===== 싱글톤 =====

        private static SoundEffectManager _instance;
        private static bool _instanceQuitting = false;

        /// <summary>SoundEffectManager 싱글톤 인스턴스</summary>
        public static SoundEffectManager Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("SoundEffectManager");
                    _instance = go.AddComponent<SoundEffectManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ===== 설정 =====

        [Header("SFX 설정")]

        [SerializeField, Range(0f, 1f)]
        [Tooltip("전체 SFX 볼륨 (0=음소거, 1=최대)")]
        private float _volume = 1.0f;

        [SerializeField]
        [Tooltip("AudioSource 풀 크기 (동시 재생 가능한 효과음 수)")]
        private int _poolSize = 12;

        // ===== 상태 =====

        private List<AudioSource> _sourcePool;
        private int _nextSourceIndex;

        /// <summary>현재 볼륨 값 (0-1)</summary>
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Mathf.Clamp01(value);
                ApplyVolumeToAll();
            }
        }

        /// <summary>AudioSource 풀 크기</summary>
        public int PoolSize => _sourcePool?.Count ?? 0;

        // ===== Unity 생명주기 =====

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _instanceQuitting = false;
            DontDestroyOnLoad(gameObject);

            InitializeAudioPool();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _instanceQuitting = true;
        }

        // ===== 퍼블릭 메서드 =====

        /// <summary>
        /// 지정된 효과음을 재생합니다.
        /// AudioSource 풀에서 사용 가능한 소스를 순환하여 사용합니다.
        /// </summary>
        /// <param name="type">재생할 효과음 종류</param>
        public void PlaySFX(SFXType type)
        {
            if (!Application.isPlaying)
            {
                Debug.Log($"[SoundEffectManager] (Editor) PlaySFX: {type}");
                return;
            }

            string sfxName = GetSFXName(type);
            Debug.Log($"[SoundEffectManager] 🔊 SFX 재생: {sfxName} (플레이스홀더 — 실제 오디오 없음)");

            // AudioSource 풀에서 다음 소스 선택 (라운드 로빈)
            AudioSource source = GetNextAvailableSource();
            if (source == null)
            {
                Debug.LogWarning("[SoundEffectManager] 사용 가능한 AudioSource 없음 — 풀 확장 필요");
                return;
            }

            // 소스 상태 초기화 (PlaySFXAtPoint에서 오염 방지)
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;      // 2D 사운드
            source.pitch = 1f;
            source.panStereo = 0f;

            // 플레이스홀더: clip이 null이므로 재생되지 않음
            // 실제 구현에서는 Resources.Load 또는 Addressables로 SFX 클립 로드
            source.clip = null;
            source.volume = _volume;
            source.Play();
        }

        /// <summary>
        /// 지정된 효과음을 특정 위치에서 3D 공간 음향으로 재생합니다.
        /// </summary>
        /// <param name="type">재생할 효과음 종류</param>
        /// <param name="position">월드 공간 재생 위치</param>
        public void PlaySFXAtPoint(SFXType type, Vector3 position)
        {
            if (!Application.isPlaying)
            {
                Debug.Log($"[SoundEffectManager] (Editor) PlaySFXAtPoint: {type} at {position}");
                return;
            }

            string sfxName = GetSFXName(type);
            Debug.Log($"[SoundEffectManager] 🔊 SFX 3D 재생: {sfxName} at {position} (플레이스홀더)");

            // 플레이스홀더: AudioSource.PlayClipAtPoint 사용 (3D 공간 음향)
            // clip이 null이므로 실제로 재생되지는 않음
            AudioSource source = GetNextAvailableSource();
            if (source == null)
            {
                Debug.LogWarning("[SoundEffectManager] 사용 가능한 AudioSource 없음 — 3D SFX 재생 불가");
                return;
            }

            source.transform.position = position;
            // 소스 상태 초기화
            source.spatialBlend = 1f;      // 3D 사운드
            source.pitch = 1f;
            source.panStereo = 0f;
            source.clip = null;
            source.volume = _volume;
            source.Play();
        }

        /// <summary>
        /// 모든 효과음을 즉시 정지합니다.
        /// </summary>
        public void StopAllSFX()
        {
            if (_sourcePool == null) return;

            foreach (var source in _sourcePool)
            {
                if (source != null && source.isPlaying)
                {
                    source.Stop();
                }
            }

            Debug.Log("[SoundEffectManager] ⏹️ 모든 SFX 정지");
        }

        /// <summary>
        /// 특정 종류의 효과음이 현재 재생 중인지 확인합니다.
        /// 플레이스홀더 단계에서는 항상 false를 반환합니다.
        /// </summary>
        /// <param name="type">확인할 효과음 종류</param>
        public bool IsPlaying(SFXType type)
        {
            // 플레이스홀더: clip이 없으므로 항상 재생 중 아님
            return false;
        }

        // ===== 내부 =====

        /// <summary>
        /// AudioSource 풀을 초기화합니다.
        /// </summary>
        private void InitializeAudioPool()
        {
            _sourcePool = new List<AudioSource>(_poolSize);
            _nextSourceIndex = 0;

            for (int i = 0; i < _poolSize; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.volume = _volume;
                source.spatialBlend = 0f; // 기본 2D 사운드
                _sourcePool.Add(source);
            }

            Debug.Log($"[SoundEffectManager] AudioSource 풀 초기화: {_poolSize}개");
        }

        /// <summary>
        /// 풀에서 다음 사용 가능한 AudioSource를 반환합니다 (라운드 로빈).
        /// </summary>
        private AudioSource GetNextAvailableSource()
        {
            if (_sourcePool == null || _sourcePool.Count == 0)
                return null;

            // 라운드 로빈으로 소스 선택
            AudioSource source = _sourcePool[_nextSourceIndex];
            _nextSourceIndex = (_nextSourceIndex + 1) % _sourcePool.Count;

            return source;
        }

        /// <summary>
        /// 모든 AudioSource에 현재 볼륨을 적용합니다.
        /// </summary>
        private void ApplyVolumeToAll()
        {
            if (_sourcePool == null) return;

            foreach (var source in _sourcePool)
            {
                if (source != null)
                {
                    source.volume = _volume;
                }
            }
        }

        /// <summary>
        /// 지정된 효과음을 표면 변형 정보와 함께 재생합니다.
        /// Footstep 등 표면에 따라 다른 사운드가 필요한 경우 사용합니다.
        /// </summary>
        /// <param name="type">재생할 효과음 종류</param>
        /// <param name="surfaceVariant">표면 변형 태그 (step_grass, step_stone 등, 빈 문자열이면 기본값)</param>
        public void PlaySurfacedSFX(SFXType type, string surfaceVariant)
        {
            if (!Application.isPlaying)
            {
                Debug.Log($"[SoundEffectManager] (Editor) PlaySurfacedSFX: {type} variant={surfaceVariant}");
                return;
            }

            string sfxName = GetSFXNameWithVariant(type, surfaceVariant);
            Debug.Log($"[SoundEffectManager] 🔊 SFX 재생: {sfxName} (플레이스홀더 — 실제 오디오 없음)");

            AudioSource source = GetNextAvailableSource();
            if (source == null)
            {
                Debug.LogWarning("[SoundEffectManager] 사용 가능한 AudioSource 없음 — 풀 확장 필요");
                return;
            }

            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            source.panStereo = 0f;

            source.clip = null;
            source.volume = _volume;
            source.Play();
        }

        /// <summary>
        /// SFXType에 해당하는 내부 이름을 반환합니다.
        /// </summary>
        private static string GetSFXName(SFXType type)
        {
            return type switch
            {
                SFXType.Footstep => "SFX_Footstep",
                SFXType.Gather => "SFX_Gather",
                SFXType.Craft => "SFX_Craft",
                SFXType.Combat_Hit => "SFX_Combat_Hit",
                SFXType.Combat_Swing => "SFX_Combat_Swing",
                SFXType.Assassination => "SFX_Assassination",
                SFXType.DoorOpen => "SFX_DoorOpen",
                SFXType.DoorClose => "SFX_DoorClose",
                SFXType.ItemPickup => "SFX_ItemPickup",
                SFXType.ItemDrop => "SFX_ItemDrop",
                SFXType.Alarm => "SFX_Alarm",
                SFXType.Victory => "SFX_Victory",
                SFXType.Defeat => "SFX_Defeat",
                _ => "SFX_Unknown"
            };
        }

        /// <summary>
        /// SFXType과 표면 변형 정보를 결합한 내부 이름을 반환합니다.
        /// variant가 비어있으면 기본 GetSFXName 결과를 반환합니다.
        /// </summary>
        private static string GetSFXNameWithVariant(SFXType type, string variant)
        {
            string baseName = GetSFXName(type);
            if (string.IsNullOrEmpty(variant)) return baseName;
            return $"{baseName}_{variant}";
        }

        /// <summary>
        /// 모든 상태 초기화 (테스트용).
        /// </summary>
        public static void ResetAll()
        {
            if (_instance != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_instance.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(_instance.gameObject);
                }
                _instance = null;
            }
            _instanceQuitting = false;
        }
    }
}