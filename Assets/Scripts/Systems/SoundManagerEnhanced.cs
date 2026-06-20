using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.Systems
{
    /// <summary>
    /// G2-08: SoundManager Enhanced — 통합 사운드 시스템 개선.
    ///
    /// 기존 SoundManager(Core)를 확장하여 BGM, SFX, UI, Ambient 4개 채널을
    /// 각각 독립적인 AudioSource로 관리합니다.
    /// - BGM: 루프 재생, Scene/지역 전환 시 자동 전환
    /// - SFX: 일회성 효과음, SoundEffectManager.SFXType 참조
    /// - UI: UI 상호작용 사운드
    /// - Ambient: 앰비언트 사운드 (루프), G1.8 AmbientEffectManager와 연동
    ///
    /// DontDestroyOnLoad 자동 설정, SceneManager.sceneLoaded 이벤트를 통해
    /// 씬 전환 시 BGM 자동 전환을 지원합니다.
    /// </summary>
    public class SoundManagerEnhanced : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static SoundManagerEnhanced _instance;
        private static bool _instanceQuitting;

        public static SoundManagerEnhanced Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("SoundManagerEnhanced");
                    _instance = go.AddComponent<SoundManagerEnhanced>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("볼륨 설정")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("BGM 볼륨")]
        private float _volumeBGM = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("SFX 볼륨")]
        private float _volumeSFX = 1.0f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("UI 사운드 볼륨")]
        private float _volumeUI = 1.0f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Ambient 사운드 볼륨")]
        private float _volumeAmbient = 0.4f;

        [Header("오디오 소스 참조 (자동 생성)")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _uiSource;
        [SerializeField] private AudioSource _ambientSource;

        [Header("BGM Scene 매핑")]
        [Tooltip("Scene 이름 → BGM 클립 이름 매핑")]
        public List<SceneBGMMapping> sceneBgmMappings = new List<SceneBGMMapping>();

        // ================================================================
        // Private State
        // ================================================================

        private bool _initialized;
        private string _currentBGMId;
        private bool _isMuted;
        private float _savedBGMVol;
        private float _savedSFXVol;
        private float _savedUIVol;
        private float _savedAmbientVol;

        // ================================================================
        // Public Properties
        // ================================================================

        /// <summary>BGM AudioSource (읽기 전용)</summary>
        public AudioSource BGMSource => _bgmSource;

        /// <summary>SFX AudioSource (읽기 전용)</summary>
        public AudioSource SFXSource => _sfxSource;

        /// <summary>UI AudioSource (읽기 전용)</summary>
        public AudioSource UISource => _uiSource;

        /// <summary>Ambient AudioSource (읽기 전용)</summary>
        public AudioSource AmbientSource => _ambientSource;

        /// <summary>현재 재생 중인 BGM ID</summary>
        public string CurrentBGMId => _currentBGMId;

        /// <summary>초기화 완료 여부</summary>
        public bool Initialized => _initialized;

        /// <summary>음소거 상태</summary>
        public bool IsMuted => _isMuted;

        /// <summary>BGM 볼륨 (0-1)</summary>
        public float VolumeBGM
        {
            get => _volumeBGM;
            set
            {
                _volumeBGM = Mathf.Clamp01(value);
                if (_bgmSource != null) _bgmSource.volume = _volumeBGM;
            }
        }

        /// <summary>SFX 볼륨 (0-1)</summary>
        public float VolumeSFX
        {
            get => _volumeSFX;
            set
            {
                _volumeSFX = Mathf.Clamp01(value);
                if (_sfxSource != null) _sfxSource.volume = _volumeSFX;
            }
        }

        /// <summary>UI 사운드 볼륨 (0-1)</summary>
        public float VolumeUI
        {
            get => _volumeUI;
            set
            {
                _volumeUI = Mathf.Clamp01(value);
                if (_uiSource != null) _uiSource.volume = _volumeUI;
            }
        }

        /// <summary>Ambient 사운드 볼륨 (0-1)</summary>
        public float VolumeAmbient
        {
            get => _volumeAmbient;
            set
            {
                _volumeAmbient = Mathf.Clamp01(value);
                if (_ambientSource != null) _ambientSource.volume = _volumeAmbient;
            }
        }

        // ================================================================
        // Unity Lifecycle
        // ================================================================

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

            Initialize();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _instanceQuitting = true;
        }

        // ================================================================
        // Initialization
        // ================================================================

        private void Initialize()
        {
            if (_initialized) return;

            // 1. Create AudioSources
            CreateAudioSources();

            // 2. Register scene-loaded callback
            SceneManager.sceneLoaded += OnSceneLoaded;

            _initialized = true;
            Debug.Log("[SoundManagerEnhanced] 초기화 완료 — BGM/SFX/UI/Ambient 4채널");
        }

        private void CreateAudioSources()
        {
            // BGM Source (loop)
            if (_bgmSource == null)
            {
                var go = new GameObject("BGM_Source");
                go.transform.SetParent(transform, false);
                _bgmSource = go.AddComponent<AudioSource>();
                _bgmSource.loop = true;
                _bgmSource.playOnAwake = false;
                _bgmSource.spatialBlend = 0f;
                _bgmSource.volume = _volumeBGM;
            }

            // SFX Source (one-shot)
            if (_sfxSource == null)
            {
                var go = new GameObject("SFX_Source");
                go.transform.SetParent(transform, false);
                _sfxSource = go.AddComponent<AudioSource>();
                _sfxSource.loop = false;
                _sfxSource.playOnAwake = false;
                _sfxSource.spatialBlend = 0f;
                _sfxSource.volume = _volumeSFX;
            }

            // UI Source (one-shot)
            if (_uiSource == null)
            {
                var go = new GameObject("UI_Source");
                go.transform.SetParent(transform, false);
                _uiSource = go.AddComponent<AudioSource>();
                _uiSource.loop = false;
                _uiSource.playOnAwake = false;
                _uiSource.spatialBlend = 0f;
                _uiSource.volume = _volumeUI;
            }

            // Ambient Source (loop) — G1.8 AmbientEffectManager 연동
            if (_ambientSource == null)
            {
                var go = new GameObject("Ambient_Source");
                go.transform.SetParent(transform, false);
                _ambientSource = go.AddComponent<AudioSource>();
                _ambientSource.loop = true;
                _ambientSource.playOnAwake = false;
                _ambientSource.spatialBlend = 0f;
                _ambientSource.volume = _volumeAmbient;
            }
        }

        // ================================================================
        // BGM
        // ================================================================

        /// <summary>
        /// 지정된 BGM 클립을 재생합니다. 이미 같은 클립이 재생 중이면 무시합니다.
        /// </summary>
        /// <param name="clipName">BGM 클립 이름 (Resources.Load에 사용)</param>
        /// <param name="volume">재생 볼륨 (0-1, 기본 0.5)</param>
        public void PlayBGM(string clipName, float volume = 0.5f)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                Debug.LogWarning("[SoundManagerEnhanced] PlayBGM: clipName이 비어 있음");
                return;
            }

            // 이미 같은 클립 재생 중이면 무시
            if (_currentBGMId == clipName && _bgmSource != null && _bgmSource.isPlaying)
            {
                return;
            }

            _currentBGMId = clipName;
            _volumeBGM = Mathf.Clamp01(volume);

            if (_bgmSource == null)
            {
                Debug.LogWarning("[SoundManagerEnhanced] BGM AudioSource가 없음");
                return;
            }

            // Resources에서 클립 로드 시도 (없으면 플레이스홀더)
            AudioClip clip = Resources.Load<AudioClip>($"Sounds/BGM/{clipName}");
            if (clip == null)
            {
                Debug.Log($"[SoundManagerEnhanced] 🎵 BGM 요청: {clipName} (플레이스홀더 — clip 없음)");
                _bgmSource.clip = null;
            }
            else
            {
                _bgmSource.clip = clip;
            }

            _bgmSource.volume = _isMuted ? 0f : _volumeBGM;
            _bgmSource.Play();
        }

        /// <summary>
        /// 현재 BGM을 정지합니다.
        /// </summary>
        public void StopBGM()
        {
            if (_bgmSource != null)
            {
                _bgmSource.Stop();
                _bgmSource.clip = null;
            }
            _currentBGMId = null;
        }

        // ================================================================
        // SFX (SoundEffectManager.SFXType 기반)
        // ================================================================

        /// <summary>
        /// 지정된 효과음을 재생합니다. SoundEffectManager.SFXType enum을 참조하여
        /// Resources/Sounds/SFX/에서 클립을 로드합니다.
        /// </summary>
        /// <param name="clipName">SFX 클립 이름 (예: "SFX_Footstep")</param>
        /// <param name="volume">재생 볼륨 (0-1, 기본 1.0)</param>
        public void PlaySFX(string clipName, float volume = 1.0f)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                Debug.LogWarning("[SoundManagerEnhanced] PlaySFX: clipName이 비어 있음");
                return;
            }

            if (_sfxSource == null)
            {
                Debug.LogWarning("[SoundManagerEnhanced] SFX AudioSource가 없음");
                return;
            }

            AudioClip clip = Resources.Load<AudioClip>($"Sounds/SFX/{clipName}");
            if (clip == null)
            {
                Debug.Log($"[SoundManagerEnhanced] 🔊 SFX 요청: {clipName} (플레이스홀더 — clip 없음)");
                return;
            }

            float vol = Mathf.Clamp01(volume) * (_isMuted ? 0f : 1f);
            _sfxSource.PlayOneShot(clip, vol);
        }

        /// <summary>
        /// SoundEffectManager.SFXType을 기반으로 효과음을 재생합니다.
        /// 내부적으로 SFX 이름으로 변환하여 PlaySFX를 호출합니다.
        /// </summary>
        /// <param name="type">효과음 종류</param>
        /// <param name="volume">재생 볼륨 (0-1, 기본 1.0)</param>
        public void PlaySFXByType(SoundEffectManager.SFXType type, float volume = 1.0f)
        {
            string clipName = GetSFXNameForType(type);
            PlaySFX(clipName, volume);
        }

        /// <summary>
        /// SoundEffectManager.SFXType에 대응하는 리소스 이름을 반환합니다.
        /// </summary>
        private static string GetSFXNameForType(SoundEffectManager.SFXType type)
        {
            return type switch
            {
                SoundEffectManager.SFXType.Footstep => "SFX_Footstep",
                SoundEffectManager.SFXType.Gather => "SFX_Gather",
                SoundEffectManager.SFXType.Craft => "SFX_Craft",
                SoundEffectManager.SFXType.Combat_Hit => "SFX_Combat_Hit",
                SoundEffectManager.SFXType.Combat_Swing => "SFX_Combat_Swing",
                SoundEffectManager.SFXType.Assassination => "SFX_Assassination",
                SoundEffectManager.SFXType.DoorOpen => "SFX_DoorOpen",
                SoundEffectManager.SFXType.DoorClose => "SFX_DoorClose",
                SoundEffectManager.SFXType.ItemPickup => "SFX_ItemPickup",
                SoundEffectManager.SFXType.ItemDrop => "SFX_ItemDrop",
                SoundEffectManager.SFXType.Alarm => "SFX_Alarm",
                SoundEffectManager.SFXType.Victory => "SFX_Victory",
                SoundEffectManager.SFXType.Defeat => "SFX_Defeat",
                _ => "SFX_Unknown"
            };
        }

        // ================================================================
        // UI Sound
        // ================================================================

        /// <summary>
        /// UI 사운드를 재생합니다.
        /// </summary>
        /// <param name="clipName">UI 사운드 클립 이름 (Resources/Sounds/UI/)</param>
        public void PlayUI(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                Debug.LogWarning("[SoundManagerEnhanced] PlayUI: clipName이 비어 있음");
                return;
            }

            if (_uiSource == null)
            {
                Debug.LogWarning("[SoundManagerEnhanced] UI AudioSource가 없음");
                return;
            }

            AudioClip clip = Resources.Load<AudioClip>($"Sounds/UI/{clipName}");
            if (clip == null)
            {
                Debug.Log($"[SoundManagerEnhanced] 🖱️ UI 사운드 요청: {clipName} (플레이스홀더 — clip 없음)");
                return;
            }

            float vol = _isMuted ? 0f : _volumeUI;
            _uiSource.PlayOneShot(clip, vol);
        }

        // ================================================================
        // Ambient Sound (G1.8 AmbientEffectManager 연동)
        // ================================================================

        /// <summary>
        /// 앰비언트 사운드를 재생합니다 (루프).
        /// G1.8 AmbientEffectManager의 지역 탐지 결과와 연동하여
        /// 지역에 맞는 앰비언트 오디오를 재생합니다.
        /// </summary>
        /// <param name="clipName">Ambient 클립 이름 (Resources/Sounds/Ambient/)</param>
        /// <param name="volume">재생 볼륨 (0-1, 기본 0.4)</param>
        public void PlayAmbient(string clipName, float volume = 0.4f)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                Debug.LogWarning("[SoundManagerEnhanced] PlayAmbient: clipName이 비어 있음");
                return;
            }

            if (_ambientSource == null)
            {
                Debug.LogWarning("[SoundManagerEnhanced] Ambient AudioSource가 없음");
                return;
            }

            // 이미 같은 앰비언트 재생 중이면 무시
            if (_ambientSource.isPlaying && _ambientSource.clip != null &&
                _ambientSource.clip.name == clipName)
            {
                return;
            }

            AudioClip clip = Resources.Load<AudioClip>($"Sounds/Ambient/{clipName}");
            if (clip == null)
            {
                Debug.Log($"[SoundManagerEnhanced] 🌿 Ambient 요청: {clipName} (플레이스홀더 — clip 없음)");
                _ambientSource.clip = null;
            }
            else
            {
                _ambientSource.clip = clip;
            }

            _volumeAmbient = Mathf.Clamp01(volume);
            _ambientSource.volume = _isMuted ? 0f : _volumeAmbient;
            _ambientSource.Play();
        }

        /// <summary>
        /// 앰비언트 사운드를 정지합니다.
        /// </summary>
        public void StopAmbient()
        {
            if (_ambientSource != null)
            {
                _ambientSource.Stop();
                _ambientSource.clip = null;
            }
        }

        // ================================================================
        // Volume Control
        // ================================================================

        /// <summary>BGM 볼륨 설정 (0-1)</summary>
        public void SetVolumeBGM(float volume)
        {
            VolumeBGM = volume;
        }

        /// <summary>SFX 볼륨 설정 (0-1)</summary>
        public void SetVolumeSFX(float volume)
        {
            VolumeSFX = volume;
        }

        /// <summary>UI 사운드 볼륨 설정 (0-1)</summary>
        public void SetVolumeUI(float volume)
        {
            VolumeUI = volume;
        }

        /// <summary>Ambient 사운드 볼륨 설정 (0-1)</summary>
        public void SetVolumeAmbient(float volume)
        {
            VolumeAmbient = volume;
        }

        // ================================================================
        // Mute / Unmute
        // ================================================================

        /// <summary>
        /// 모든 오디오 채널을 음소거합니다.
        /// 현재 볼륨을 저장하고 볼륨을 0으로 설정합니다.
        /// </summary>
        public void MuteAll()
        {
            if (_isMuted) return;

            _isMuted = true;

            // 현재 볼륨 저장
            _savedBGMVol = _volumeBGM;
            _savedSFXVol = _volumeSFX;
            _savedUIVol = _volumeUI;
            _savedAmbientVol = _volumeAmbient;

            // 음소거 적용
            if (_bgmSource != null) _bgmSource.volume = 0f;
            if (_sfxSource != null) _sfxSource.volume = 0f;
            if (_uiSource != null) _uiSource.volume = 0f;
            if (_ambientSource != null) _ambientSource.volume = 0f;

            Debug.Log("[SoundManagerEnhanced] 🔇 모든 사운드 음소거");
        }

        /// <summary>
        /// 모든 오디오 채널의 음소거를 해제합니다.
        /// 저장된 볼륨으로 복원합니다.
        /// </summary>
        public void UnmuteAll()
        {
            if (!_isMuted) return;

            _isMuted = false;

            // 저장된 볼륨 복원
            _volumeBGM = _savedBGMVol;
            _volumeSFX = _savedSFXVol;
            _volumeUI = _savedUIVol;
            _volumeAmbient = _savedAmbientVol;

            // 볼륨 적용
            if (_bgmSource != null) _bgmSource.volume = _volumeBGM;
            if (_sfxSource != null) _sfxSource.volume = _volumeSFX;
            if (_uiSource != null) _uiSource.volume = _volumeUI;
            if (_ambientSource != null) _ambientSource.volume = _volumeAmbient;

            Debug.Log("[SoundManagerEnhanced] 🔊 모든 사운드 음소거 해제");
        }

        /// <summary>
        /// 모든 사운드를 즉시 정지합니다.
        /// </summary>
        public void StopAll()
        {
            StopBGM();
            StopAmbient();

            if (_sfxSource != null) _sfxSource.Stop();
            if (_uiSource != null) _uiSource.Stop();

            Debug.Log("[SoundManagerEnhanced] ⏹️ 모든 사운드 정지");
        }

        // ================================================================
        // Scene Transition — BGM Auto-Switch
        // ================================================================

        /// <summary>
        /// SceneManager.sceneLoaded 콜백.
        /// sceneBgmMappings를 참조하여 현재 씬에 맞는 BGM을 자동 전환합니다.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string sceneName = scene.name;
            Debug.Log($"[SoundManagerEnhanced] 씬 전환 감지: {sceneName}");

            // 매핑에서 현재 씬에 맞는 BGM 검색
            string bgmClipName = GetBGMForScene(sceneName);
            if (!string.IsNullOrEmpty(bgmClipName))
            {
                PlayBGM(bgmClipName);
            }
            else
            {
                Debug.Log($"[SoundManagerEnhanced] 씬 \"{sceneName}\"에 매핑된 BGM 없음");
            }

            // AmbientEffectManager와 연동: G1.8 지역 기반 앰비언트
            TrySyncAmbientWithRegion();
        }

        /// <summary>
        /// sceneBgmMappings 리스트에서 주어진 씬 이름에 매핑된 BGM 클립 이름을 찾습니다.
        /// </summary>
        private string GetBGMForScene(string sceneName)
        {
            if (sceneBgmMappings == null) return null;

            foreach (var mapping in sceneBgmMappings)
            {
                if (mapping != null && mapping.sceneName == sceneName)
                {
                    return mapping.bgmClipName;
                }
            }
            return null;
        }

        /// <summary>
        /// G1.8 AmbientEffectManager가 존재하면 현재 지역 정보를 읽어
        /// 적절한 앰비언트 사운드로 전환합니다.
        /// </summary>
        private void TrySyncAmbientWithRegion()
        {
            var ambientEffectManager = AmbientEffectManager.Instance;
            if (ambientEffectManager == null || !ambientEffectManager.Initialized)
            {
                // AmbientEffectManager가 없으면 기본 앰비언트
                PlayAmbient("Ambient_Default", _volumeAmbient);
                return;
            }

            // AmbientEffectManager.CurrentEffect 기반으로 앰비언트 사운드 선택
            string ambientClip = ambientEffectManager.CurrentEffect switch
            {
                AmbientEffectManager.AmbientEffectType.Fireflies => "Ambient_Forest",
                AmbientEffectManager.AmbientEffectType.Leaves => "Ambient_Forest",
                AmbientEffectManager.AmbientEffectType.Dust => "Ambient_Desert",
                AmbientEffectManager.AmbientEffectType.Embers => "Ambient_Volcanic",
                _ => "Ambient_Default"
            };

            PlayAmbient(ambientClip, _volumeAmbient);
        }
    }

    /// <summary>
    /// Scene 이름과 BGM 클립 이름의 매핑을 정의하는 데이터 클래스.
    /// SoundManagerEnhanced의 Inspector에서 설정합니다.
    /// </summary>
    [System.Serializable]
    public class SceneBGMMapping
    {
        [Tooltip("Unity Scene 이름")]
        public string sceneName;

        [Tooltip("Resources/Sounds/BGM/ 경로의 클립 이름")]
        public string bgmClipName;
    }
}