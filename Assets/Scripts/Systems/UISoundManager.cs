using UnityEngine;
using UnityEngine.UI;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-20: UI 사운드 관리자 싱글톤.
    /// UI 상호작용에 대한 사운드 효과를 처리합니다.
    /// UIManager와 통합되어 버튼 클릭 시 자동으로 UIClick을 재생합니다.
    /// 현재는 플레이스홀더 단계 — 실제 오디오 파일 없이 Debug.Log로 대체됩니다.
    /// </summary>
    public class UISoundManager : MonoBehaviour
    {
        /// <summary>
        /// UI 효과음 종류.
        /// </summary>
        public enum UISFXType
        {
            /// <summary>버튼 클릭</summary>
            UIClick,

            /// <summary>UI 패널 열림</summary>
            UIOpen,

            /// <summary>UI 패널 닫힘</summary>
            UIClose,

            /// <summary>UI 오류</summary>
            UIError,

            /// <summary>알림</summary>
            UINotification,

            /// <summary>퀘스트 완료</summary>
            UIQuestComplete
        }

        // ===== 싱글톤 =====

        private static UISoundManager _instance;
        private static bool _instanceQuitting = false;

        /// <summary>UISoundManager 싱글톤 인스턴스</summary>
        public static UISoundManager Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("UISoundManager");
                    _instance = go.AddComponent<UISoundManager>();
                    // Awake()에서 DontDestroyOnLoad를 처리하므로 여기서는 불필요
                }
                return _instance;
            }
        }

        // ===== 설정 =====

        [Header("UI 사운드 설정")]

        [SerializeField, Range(0f, 1f)]
        [Tooltip("UI 사운드 볼륨 (0=음소거, 1=최대)")]
        private float _volume = 1.0f;

        [SerializeField]
        [Tooltip("UI 사운드 전용 AudioSource")]
        private AudioSource _uiAudioSource;

        /// <summary>현재 볼륨 값 (0-1)</summary>
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Mathf.Clamp01(value);
                if (_uiAudioSource != null)
                {
                    _uiAudioSource.volume = _volume;
                }
            }
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

            InitializeAudioSource();
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
        /// 지정된 UI 사운드 효과를 재생합니다.
        /// </summary>
        /// <param name="type">재생할 UI 효과음 종류</param>
        public void PlayUISound(UISFXType type)
        {
            if (!Application.isPlaying)
            {
                Debug.Log($"[UISoundManager] (Editor) PlayUISound: {type}");
                return;
            }

            string sfxName = GetUISFXName(type);
            Debug.Log($"[UISoundManager] 🔊 UI 사운드 재생: {sfxName} (플레이스홀더 — 실제 오디오 없음)");

            if (_uiAudioSource != null)
            {
                // 플레이스홀더: clip이 null이므로 재생되지 않음
                _uiAudioSource.clip = null;
                _uiAudioSource.volume = _volume;
                _uiAudioSource.Play();
            }
        }

        /// <summary>
        /// Button 컴포넌트에 UIClick 사운드를 자동 연결합니다.
        /// UIManager에서 버튼 생성 시 호출하여 사용합니다.
        /// </summary>
        /// <param name="button">대상 버튼</param>
        public void RegisterButtonSound(Button button)
        {
            if (button == null) return;

            // 중복 등록 방지를 위해 기존 리스너 제거
            button.onClick.RemoveListener(OnButtonClick);

            // 새 리스너 등록
            button.onClick.AddListener(OnButtonClick);

            Debug.Log($"[UISoundManager] 버튼 사운드 등록: {button.name}");
        }

        /// <summary>
        /// Button 컴포넌트에서 UI 사운드 리스너를 제거합니다.
        /// </summary>
        /// <param name="button">대상 버튼</param>
        public void UnregisterButtonSound(Button button)
        {
            if (button == null) return;

            button.onClick.RemoveListener(OnButtonClick);
        }

        /// <summary>
        /// Toggle 컴포넌트에 UIClick 사운드를 자동 연결합니다.
        /// </summary>
        /// <param name="toggle">대상 토글</param>
        public void RegisterToggleSound(Toggle toggle)
        {
            if (toggle == null) return;

            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
            toggle.onValueChanged.AddListener(OnToggleValueChanged);

            Debug.Log($"[UISoundManager] 토글 사운드 등록: {toggle.name}");
        }

        /// <summary>
        /// 모든 UI 사운드를 즉시 정지합니다.
        /// </summary>
        public void StopAllSounds()
        {
            if (_uiAudioSource != null && _uiAudioSource.isPlaying)
            {
                _uiAudioSource.Stop();
            }

            Debug.Log("[UISoundManager] ⏹️ 모든 UI 사운드 정지");
        }

        // ===== 내부 =====

        /// <summary>
        /// UI 사운드 전용 AudioSource를 초기화합니다.
        /// </summary>
        private void InitializeAudioSource()
        {
            // [SerializeField]로 인스펙터에서 이미 할당된 경우 새로 생성하지 않음
            if (_uiAudioSource != null)
            {
                _uiAudioSource.playOnAwake = false;
                _uiAudioSource.volume = _volume;
                _uiAudioSource.spatialBlend = 0f; // 2D 사운드
                return;
            }

            _uiAudioSource = gameObject.AddComponent<AudioSource>();
            _uiAudioSource.playOnAwake = false;
            _uiAudioSource.volume = _volume;
            _uiAudioSource.spatialBlend = 0f; // 2D 사운드
        }

        /// <summary>
        /// 버튼 클릭 시 호출되는 핸들러.
        /// </summary>
        private void OnButtonClick()
        {
            PlayUISound(UISFXType.UIClick);
        }

        /// <summary>
        /// 토글 값 변경 시 호출되는 핸들러.
        /// </summary>
        private void OnToggleValueChanged(bool isOn)
        {
            PlayUISound(UISFXType.UIClick);
        }

        /// <summary>
        /// UISFXType에 해당하는 내부 이름을 반환합니다.
        /// </summary>
        private static string GetUISFXName(UISFXType type)
        {
            return type switch
            {
                UISFXType.UIClick => "UI_Click",
                UISFXType.UIOpen => "UI_Open",
                UISFXType.UIClose => "UI_Close",
                UISFXType.UIError => "UI_Error",
                UISFXType.UINotification => "UI_Notification",
                UISFXType.UIQuestComplete => "UI_QuestComplete",
                _ => "UI_Unknown"
            };
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