using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-18: 배경음악(BGM) 관리자 싱글톤.
    /// MusicTrack 열거형으로 트랙을 선택하고 크로스페이드를 지원합니다.
    /// DontDestroyOnLoad로 씬 전환 시 유지됩니다.
    /// 현재는 플레이스홀더 단계 — 실제 오디오 파일 없이 Debug.Log로 대체됩니다.
    /// </summary>
    public class BackgroundMusicManager : MonoBehaviour
    {
        /// <summary>
        /// 사용 가능한 BGM 트랙 종류.
        /// </summary>
        public enum MusicTrack
        {
            /// <summary>메인 테마 (기본값)</summary>
            MainTheme,

            /// <summary>전투 BGM</summary>
            Battle,

            /// <summary>잠입 BGM</summary>
            Stealth,

            /// <summary>평화 BGM</summary>
            Peace,

            /// <summary>밤 BGM</summary>
            Night
        }

        // ===== 싱글톤 =====

        private static BackgroundMusicManager _instance;
        private static bool _instanceQuitting = false;

        /// <summary>BackgroundMusicManager 싱글톤 인스턴스</summary>
        public static BackgroundMusicManager Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("BackgroundMusicManager");
                    _instance = go.AddComponent<BackgroundMusicManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ===== 설정 =====

        [Header("BGM 설정")]

        [SerializeField, Range(0f, 1f)]
        [Tooltip("전체 BGM 볼륨 (0=음소거, 1=최대)")]
        private float _volume = 1.0f;

        [SerializeField]
        [Tooltip("크로스페이드 지속 시간 (초)")]
        private float _crossfadeDuration = 1.0f;

        [SerializeField]
        [Tooltip("기본 재생 트랙")]
        private MusicTrack _defaultTrack = MusicTrack.MainTheme;

        // ===== 컴포넌트 =====

        private AudioSource _currentSource;
        private AudioSource _nextSource;

        // ===== 상태 =====

        private MusicTrack _currentTrack;
        private Coroutine _crossfadeRoutine;

        /// <summary>현재 재생 중인 트랙</summary>
        public MusicTrack CurrentTrack => _currentTrack;

        /// <summary>현재 볼륨 값 (0-1)</summary>
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Mathf.Clamp01(value);
                ApplyVolume();
            }
        }

        /// <summary>크로스페이드 지속 시간 (초)</summary>
        public float CrossfadeDuration
        {
            get => _crossfadeDuration;
            set => _crossfadeDuration = Mathf.Max(0.01f, value);
        }

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

            InitializeAudioSources();
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

        private void Start()
        {
            // 기본 트랙 재생
            PlayMusic(_defaultTrack, true);
        }

        // ===== 퍼블릭 메서드 =====

        /// <summary>
        /// 지정된 트랙으로 BGM을 전환합니다. (크로스페이드 적용)
        /// </summary>
        /// <param name="track">재생할 트랙</param>
        /// <param name="immediate">true이면 크로스페이드 없이 즉시 전환</param>
        public void PlayMusic(MusicTrack track, bool immediate = false)
        {
            if (!Application.isPlaying)
            {
                Debug.Log($"[BackgroundMusicManager] (Editor) PlayMusic: {track}");
                _currentTrack = track;
                return;
            }

            if (track == _currentTrack && _currentSource.isPlaying)
            {
                Debug.Log($"[BackgroundMusicManager] 이미 재생 중인 트랙: {track}");
                return;
            }

            Debug.Log($"[BackgroundMusicManager] ▶️ 트랙 전환: {_currentTrack} → {track}");

            _currentTrack = track;

            if (immediate)
            {
                // 즉시 전환
                if (_crossfadeRoutine != null)
                {
                    StopCoroutine(_crossfadeRoutine);
                    _crossfadeRoutine = null;
                }

                // 현재 소스 정지
                if (_currentSource != null)
                {
                    _currentSource.Stop();
                    _currentSource.volume = 0f;
                }

                // 다음 소스를 현재로 전환
                var temp = _currentSource;
                _currentSource = _nextSource;
                _nextSource = temp;

                // 새 트랙 재생
                if (_currentSource != null)
                {
                    _currentSource.volume = _volume;
                    _currentSource.Play();
                }
            }
            else
            {
                // 크로스페이드 시작
                if (_crossfadeRoutine != null)
                {
                    StopCoroutine(_crossfadeRoutine);
                }
                _crossfadeRoutine = StartCoroutine(CrossfadeCoroutine());
            }
        }

        /// <summary>
        /// BGM을 일시 중지합니다.
        /// </summary>
        public void PauseMusic()
        {
            if (_currentSource != null && _currentSource.isPlaying)
            {
                _currentSource.Pause();
                Debug.Log("[BackgroundMusicManager] ⏸️ BGM 일시 중지");
            }
        }

        /// <summary>
        /// BGM을 재개합니다.
        /// </summary>
        public void ResumeMusic()
        {
            if (_currentSource != null && !_currentSource.isPlaying)
            {
                _currentSource.UnPause();
                Debug.Log("[BackgroundMusicManager] ▶️ BGM 재개");
            }
        }

        /// <summary>
        /// BGM을 완전히 정지합니다.
        /// </summary>
        public void StopMusic()
        {
            if (_currentSource != null)
            {
                _currentSource.Stop();
                _currentSource.volume = 0f;
            }
            if (_nextSource != null)
            {
                _nextSource.Stop();
                _nextSource.volume = 0f;
            }

            if (_crossfadeRoutine != null)
            {
                StopCoroutine(_crossfadeRoutine);
                _crossfadeRoutine = null;
            }

            Debug.Log("[BackgroundMusicManager] ⏹️ BGM 정지");
        }

        // ===== 내부 =====

        /// <summary>
        /// 두 개의 AudioSource를 초기화합니다 (크로스페이드용).
        /// </summary>
        private void InitializeAudioSources()
        {
            // 현재 소스
            _currentSource = gameObject.AddComponent<AudioSource>();
            _currentSource.loop = true;
            _currentSource.playOnAwake = false;
            _currentSource.volume = 0f;

            // 다음 소스 (크로스페이드 대상)
            _nextSource = gameObject.AddComponent<AudioSource>();
            _nextSource.loop = true;
            _nextSource.playOnAwake = false;
            _nextSource.volume = 0f;
        }

        /// <summary>
        /// 현재 볼륨을 AudioSource에 적용합니다.
        /// </summary>
        private void ApplyVolume()
        {
            if (_currentSource != null)
            {
                _currentSource.volume = _volume;
            }
        }

        /// <summary>
        /// 크로스페이드 코루틴 — 현재 트랙을 페이드 아웃하고 새 트랙을 페이드 인합니다.
        /// </summary>
        private System.Collections.IEnumerator CrossfadeCoroutine()
        {
            if (_currentSource == null || _nextSource == null)
                yield break;

            // 새 트랙을 다음 소스에 할당하고 재생 시작 (볼륨 0)
            if (_nextSource.clip != GetTrackClip())
            {
                _nextSource.clip = GetTrackClip();
            }
            _nextSource.volume = 0f;
            _nextSource.Play();

            float elapsed = 0f;
            while (elapsed < _crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _crossfadeDuration);

                // 현재 소스 페이드 아웃
                if (_currentSource != null)
                {
                    _currentSource.volume = Mathf.Lerp(_volume, 0f, t);
                }

                // 다음 소스 페이드 인
                if (_nextSource != null)
                {
                    _nextSource.volume = Mathf.Lerp(0f, _volume, t);
                }

                yield return null;
            }

            // 현재 소스 정지
            if (_currentSource != null)
            {
                _currentSource.Stop();
                _currentSource.volume = 0f;
            }

            // 다음 소스를 현재로 전환
            var temp = _currentSource;
            _currentSource = _nextSource;
            _nextSource = temp;

            _crossfadeRoutine = null;
        }

        /// <summary>
        /// 현재 트랙에 해당하는 AudioClip을 반환합니다.
        /// 플레이스홀더 단계에서는 null을 반환합니다.
        /// </summary>
        private AudioClip GetTrackClip()
        {
            // 플레이스홀더: 실제 오디오 파일이 없으므로 null
            // 향후 Resources.Load 또는 Addressables로 대체
            string trackName = _currentTrack switch
            {
                MusicTrack.MainTheme => "BGM_MainTheme",
                MusicTrack.Battle => "BGM_Battle",
                MusicTrack.Stealth => "BGM_Stealth",
                MusicTrack.Peace => "BGM_Peace",
                MusicTrack.Night => "BGM_Night",
                _ => "BGM_MainTheme"
            };

            Debug.Log($"[BackgroundMusicManager] 🎵 트랙 로드 요청: {trackName} (플레이스홀더 — 실제 오디오 없음)");
            return null;
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