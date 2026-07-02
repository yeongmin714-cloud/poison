using System.Collections;
using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 🎵 지역별 BGM 컨트롤러 — 싱글톤.
    /// TerritoryManager에서 현재 국가(NationType)를 감지하여 BGM을 자동 전환합니다.
    /// - 낮: 국가별 BGM (East/West/South/North/Empire)
    /// - 저녁/밤: Night BGM
    /// - 전투 중: Combat BGM (최우선)
    /// BGM 전환 시 1.5초 페이드 아웃/인을 수행합니다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RegionBGMController : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static RegionBGMController _instance;
        private static bool _instanceQuitting;

        public static RegionBGMController Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("RegionBGMController");
                    _instance = go.AddComponent<RegionBGMController>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("BGM 키 (string ID)")]
        [SerializeField, Tooltip("동(East) 지역 BGM 키")]
        private string _bgmKeyEast = "bgm_east";

        [SerializeField, Tooltip("서(West) 지역 BGM 키")]
        private string _bgmKeyWest = "bgm_west";

        [SerializeField, Tooltip("남(South) 지역 BGM 키")]
        private string _bgmKeySouth = "bgm_south";

        [SerializeField, Tooltip("북(North) 지역 BGM 키")]
        private string _bgmKeyNorth = "bgm_north";

        [SerializeField, Tooltip("황제국(Empire) 지역 BGM 키")]
        private string _bgmKeyEmpire = "bgm_empire";

        [SerializeField, Tooltip("야간 BGM 키")]
        private string _bgmKeyNight = "bgm_night";

        [SerializeField, Tooltip("전투 BGM 키")]
        private string _bgmKeyCombat = "bgm_combat";

        [Header("설정")]
        [SerializeField, Tooltip("BGM 체크 간격 (초)")]
        private float _checkInterval = 1.0f;

        [SerializeField, Tooltip("BGM 기본 볼륨 (0~1)")]
        private float _bgmVolume = 0.5f;

        [SerializeField, Tooltip("페이드 지속 시간 (초)")]
        private float _fadeDuration = 1.5f;

        // ================================================================
        // Private State
        // ================================================================

        private AudioSource _bgmSource;
        private string _currentBGMKey;
        private float _checkTimer;
        private Coroutine _fadeCoroutine;
        private bool _initialized;

        // 마지막으로 감지된 상태 (변경 감지용)
        private NationType _lastNation = NationType.None;
        private bool _lastIsNight;
        private bool _lastIsCombat;

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            // 싱글톤 패턴
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
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _instanceQuitting = true;
        }

        private void Start()
        {
            if (!_initialized) return;

            // 초기 BGM 즉시 재생
            RefreshBGM();
        }

        private void Update()
        {
            if (!_initialized) return;

            // 주기적 체크 (GC 최소화를 위해 타이머 사용)
            _checkTimer += Time.deltaTime;
            if (_checkTimer >= _checkInterval)
            {
                _checkTimer = 0f;
                CheckBGMChange();
            }
        }

        // ================================================================
        // Initialization
        // ================================================================

        /// <summary>
        /// 초기화: AudioSource 생성 및 기존 시스템 참조 확인.
        /// </summary>
        private void Initialize()
        {
            if (_initialized) return;

            // AudioSource 설정 (loop)
            _bgmSource = GetComponent<AudioSource>();
            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
            }
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = 0f; // 페이드로 시작

            // SoundManagerEnhanced null 체크 (경고만, 없으면 동작 중단)
            if (SoundManagerEnhanced.Instance == null)
            {
                Debug.LogWarning("[RegionBGMController] SoundManagerEnhanced.Instance가 null입니다. BGM 기능이 동작하지 않습니다.");
                // SoundManagerEnhanced가 없으면 Core SoundManager 사용 시도
                if (SoundManager.Instance == null)
                {
                    Debug.LogWarning("[RegionBGMController] SoundManager.Instance도 null입니다. BGM 기능이 완전히 비활성화됩니다.");
                    enabled = false;
                    return;
                }
            }

            // TimeManager null 체크
            if (TimeManager.Instance == null)
            {
                Debug.LogWarning("[RegionBGMController] TimeManager.Instance가 null입니다. 야간 BGM 전환이 동작하지 않습니다.");
            }

            // TerritoryManager null 체크 (경고만)
            if (TerritoryManager.Instance == null)
            {
                Debug.LogWarning("[RegionBGMController] TerritoryManager.Instance가 null입니다. 지역별 BGM 전환이 동작하지 않습니다.");
            }

            _initialized = true;
            Debug.Log("[RegionBGMController] 초기화 완료 — 지역별 BGM 시스템 활성화");
        }

        // ================================================================
        // BGM Detection & Switching
        // ================================================================

        /// <summary>
        /// 현재 상태(국가, 시간, 전투)를 감지하여 BGM 변경이 필요한지 확인합니다.
        /// </summary>
        private void CheckBGMChange()
        {
            // 현재 상태 수집
            NationType currentNation = GetCurrentNation();
            bool isNight = IsNightTime();
            bool isCombat = IsInCombat();

            // 변경 감지
            bool nationChanged = currentNation != _lastNation;
            bool nightChanged = isNight != _lastIsNight;
            bool combatChanged = isCombat != _lastIsCombat;

            if (!nationChanged && !nightChanged && !combatChanged)
                return;

            // 상태 갱신
            _lastNation = currentNation;
            _lastIsNight = isNight;
            _lastIsCombat = isCombat;

            // BGM 전환
            RefreshBGM();
        }

        /// <summary>
        /// 현재 상황에 맞는 BGM을 결정하고 재생합니다.
        /// 우선순위: 전투 > 야간 > 지역
        /// </summary>
        private void RefreshBGM()
        {
            // BGM 키 결정
            string targetBGMKey = DetermineBGMKey();

            // 같은 BGM이면 중복 재생 방지
            if (_currentBGMKey == targetBGMKey && _bgmSource != null && _bgmSource.isPlaying)
                return;

            // BGM 전환 (페이드 포함)
            PlayBGMWithFade(targetBGMKey);
        }

        /// <summary>
        /// 현재 상황에 따라 적절한 BGM 키를 반환합니다.
        /// 우선순위: Combat > Night > Region
        /// </summary>
        private string DetermineBGMKey()
        {
            // 1순위: 전투 중 → Combat BGM
            if (IsInCombat())
                return _bgmKeyCombat;

            // 2순위: 야간 → Night BGM
            if (IsNightTime())
                return _bgmKeyNight;

            // 3순위: 국가별 BGM
            NationType nation = GetCurrentNation();
            return GetBGMKeyForNation(nation);
        }

        /// <summary>
        /// 국가(NationType)에 대응하는 BGM 키를 반환합니다.
        /// </summary>
        private string GetBGMKeyForNation(NationType nation)
        {
            switch (nation)
            {
                case NationType.East:
                    return _bgmKeyEast;       // 밝은/희망찬 BGM
                case NationType.West:
                    return _bgmKeyWest;       // 서정적인 BGM
                case NationType.South:
                    return _bgmKeySouth;      // 열정적인 BGM
                case NationType.North:
                    return _bgmKeyNorth;      // 웅장한/어두운 BGM
                case NationType.Empire:
                    return _bgmKeyEmpire;     // 장엄한 BGM
                case NationType.Dracula:
                    // Dracula — 어두운 분위기, North BGM으로 폴백
                    return _bgmKeyNorth;
                default:
                    // None 또는 알 수 없는 국가 → East BGM (기본)
                    return _bgmKeyEast;
            }
        }

        /// <summary>
        /// BGM을 페이드 아웃 → 새 BGM 설정 → 페이드 인 순서로 전환합니다.
        /// </summary>
        /// <param name="bgmKey">재생할 BGM 키</param>
        private void PlayBGMWithFade(string bgmKey)
        {
            // Null 체크
            if (_bgmSource == null) return;
            if (string.IsNullOrEmpty(bgmKey)) return;

            // 진행 중인 페이드 코루틴 중단
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            // 페이드 전환 시작
            _fadeCoroutine = StartCoroutine(FadeToBGM(bgmKey));
        }

        /// <summary>
        /// 페이드 아웃(1.5s) → BGM 전환 → 페이드 인(1.5s) 코루틴.
        /// </summary>
        private IEnumerator FadeToBGM(string newBGMKey)
        {
            // 1. 페이드 아웃 (현재 볼륨 → 0)
            if (_bgmSource.volume > 0.01f)
            {
                yield return Transitions.FadeVolume(_bgmSource, 0f, _fadeDuration);
            }

            // 2. BGM 전환 — SoundManagerEnhanced를 통해 재생
            if (SoundManagerEnhanced.Instance != null)
            {
                // SoundManagerEnhanced의 PlayBGM 사용 (Resources/Sounds/BGM/ 경로)
                SoundManagerEnhanced.Instance.PlayBGM(newBGMKey, _bgmVolume);
            }
            else if (SoundManager.Instance != null)
            {
                // 폴백: Core SoundManager 사용
                SoundManager.Instance.PlayBGM(newBGMKey);
            }
            else
            {
                // 사운드 매니저 없음 — 자체 AudioSource로 직접 재생 시도
                Debug.LogWarning($"[RegionBGMController] SoundManager가 없습니다. BGM 키: {newBGMKey}");
                _bgmSource.Stop();
                _bgmSource.clip = null;
                _currentBGMKey = null;
                _fadeCoroutine = null;
                yield break;
            }

            // 현재 BGM 키 갱신
            _currentBGMKey = newBGMKey;

            // SoundManagerEnhanced의 BGMSource 볼륨을 0으로 설정하고,
            // 이 컨트롤러의 AudioSource를 통해 페이드 인
            // (SoundManagerEnhanced.PlayBGM이 내부 _bgmSource.volume을 설정하므로,
            //  이 컨트롤러는 자체 AudioSource로 페이드 처리)
            // 실제로는 SoundManagerEnhanced의 BGMSource를 페이드하는 것이 더 일관됨
            if (SoundManagerEnhanced.Instance != null && SoundManagerEnhanced.Instance.BGMSource != null)
            {
                // SoundManagerEnhanced의 BGMSource 볼륨을 0으로 설정
                SoundManagerEnhanced.Instance.BGMSource.volume = 0f;

                // 3. 페이드 인 (0 → _bgmVolume)
                yield return Transitions.FadeVolume(SoundManagerEnhanced.Instance.BGMSource, _bgmVolume, _fadeDuration);
            }
            else if (SoundManager.Instance != null && SoundManager.Instance.bgmSource != null)
            {
                // Core SoundManager 폴백
                SoundManager.Instance.bgmSource.volume = 0f;
                yield return Transitions.FadeVolume(SoundManager.Instance.bgmSource, _bgmVolume, _fadeDuration);
            }

            _fadeCoroutine = null;
        }

        // ================================================================
        // State Detection
        // ================================================================

        /// <summary>
        /// TerritoryManager에서 현재 국가(NationType)를 가져옵니다.
        /// </summary>
        private NationType GetCurrentNation()
        {
            // TerritoryManager 우선 사용
            if (TerritoryManager.Instance != null)
            {
                return TerritoryManager.Instance.CurrentTerritoryId.nation;
            }

            // 폴백: TerritoryBiomeMapper를 직접 사용할 수 없으므로 None 반환
            return NationType.None;
        }

        /// <summary>
        /// TimeManager를 통해 현재가 야간인지 확인합니다.
        /// </summary>
        private bool IsNightTime()
        {
            if (TimeManager.Instance == null)
                return false;

            return TimeManager.Instance.IsNight;
        }

        /// <summary>
        /// 현재 전투 중인지 감지합니다.
        /// GuardPlaceholder 중 하나라도 IsInCombat == true면 전투 상태로 간주합니다.
        /// </summary>
        private bool IsInCombat()
        {
            // 씬에 있는 모든 GuardPlaceholder 검사
            var guards = FindObjectsByType<GuardPlaceholder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var guard in guards)
            {
                if (guard != null && guard.IsInCombat)
                    return true;
            }

            return false;
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// 현재 재생 중인 BGM 키를 반환합니다.
        /// </summary>
        public string CurrentBGMKey => _currentBGMKey;

        /// <summary>
        /// BGM을 강제로 갱신합니다 (외부 호출용).
        /// </summary>
        public void ForceRefresh()
        {
            if (!_initialized) return;

            _lastNation = NationType.None;
            _lastIsNight = false;
            _lastIsCombat = false;
            _checkTimer = _checkInterval; // 즉시 체크
            CheckBGMChange();
        }

        /// <summary>
        /// BGM 볼륨을 설정합니다 (0~1).
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// 현재 BGM 볼륨을 반환합니다.
        /// </summary>
        public float BGMVolume => _bgmVolume;

        /// <summary>
        /// 페이드 지속 시간을 설정합니다 (초).
        /// </summary>
        public void SetFadeDuration(float duration)
        {
            _fadeDuration = Mathf.Max(0.1f, duration);
        }
    }
}