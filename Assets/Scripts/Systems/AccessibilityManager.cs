using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 접근성 설정을 관리하는 정적 클래스.
    /// 색맹 모드, 툴팁 지연 시간, 자막 크기 등을 PlayerPrefs에 저장/로드합니다.
    /// GameManager 또는 SettingsMenuUI.Awake()에서 Initialize()를 호출해야 합니다.
    /// </summary>
    public static class AccessibilityManager
    {
        // ===== PlayerPrefs 키 =====
        private const string KEY_TOOLTIP_DELAY   = "Access_TooltipDelay";
        private const string KEY_COLORBLIND_MODE = "Access_ColorBlindMode";
        private const string KEY_SUBTITLE_SCALE  = "Access_SubtitleScale";

        // ===== 기본값 =====
        private const float DEFAULT_TOOLTIP_DELAY   = 0.3f;
        private const float DEFAULT_SUBTITLE_SCALE  = 1.0f;

        // ===== 속성 (메모리 캐시) =====
        /// <summary>색맹 모드 활성화 여부</summary>
        public static bool ColorBlindMode { get; set; }

        /// <summary>툴팁 지연 시간 (초)</summary>
        public static float TooltipDelay { get; set; }

        /// <summary>자막 크기 배율 (0.8x ~ 2.0x)</summary>
        public static float SubtitleScale { get; set; }

        // ===== 초기화 =====
        /// <summary>
        /// PlayerPrefs에서 접근성 설정을 읽어와 메모리에 캐싱합니다.
        /// 게임 시작 시 GameManager.InitializeSystems() 또는 SettingsMenuUI.Awake()에서 호출하세요.
        /// </summary>
        public static void Initialize()
        {
            LoadSettings();
            Debug.Log($"[AccessibilityManager] 초기화 완료 — ColorBlind={ColorBlindMode}, TooltipDelay={TooltipDelay:F2}, SubtitleScale={SubtitleScale:F2}");
        }

        // ===== 저장 / 로드 =====

        /// <summary>PlayerPrefs에서 모든 접근성 설정을 읽습니다.</summary>
        public static void LoadSettings()
        {
            ColorBlindMode = PlayerPrefs.GetInt(KEY_COLORBLIND_MODE, 0) == 1;
            TooltipDelay   = PlayerPrefs.GetFloat(KEY_TOOLTIP_DELAY,   DEFAULT_TOOLTIP_DELAY);
            SubtitleScale  = PlayerPrefs.GetFloat(KEY_SUBTITLE_SCALE,  DEFAULT_SUBTITLE_SCALE);

            // 값 범위 클램프
            TooltipDelay  = Mathf.Clamp(TooltipDelay,  0f, 1.5f);
            SubtitleScale = Mathf.Clamp(SubtitleScale, 0.8f, 2.0f);
        }

        /// <summary>모든 접근성 설정을 PlayerPrefs에 즉시 저장합니다.</summary>
        public static void SaveSettings()
        {
            PlayerPrefs.SetInt(KEY_COLORBLIND_MODE,   ColorBlindMode ? 1 : 0);
            PlayerPrefs.SetFloat(KEY_TOOLTIP_DELAY,   TooltipDelay);
            PlayerPrefs.SetFloat(KEY_SUBTITLE_SCALE,  SubtitleScale);
            PlayerPrefs.Save();
        }

        // ===== 편의 메서드 =====

        /// <summary>툴팁 지연 시간을 설정하고 즉시 저장합니다.</summary>
        public static void SetTooltipDelay(float value)
        {
            TooltipDelay = Mathf.Clamp(value, 0f, 1.5f);
            PlayerPrefs.SetFloat(KEY_TOOLTIP_DELAY, TooltipDelay);
            PlayerPrefs.Save();
        }

        /// <summary>색맹 모드를 설정하고 즉시 저장합니다.</summary>
        public static void SetColorBlindMode(bool enabled)
        {
            ColorBlindMode = enabled;
            PlayerPrefs.SetInt(KEY_COLORBLIND_MODE, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>자막 크기 배율을 설정하고 즉시 저장합니다.</summary>
        public static void SetSubtitleScale(float value)
        {
            SubtitleScale = Mathf.Clamp(value, 0.8f, 2.0f);
            PlayerPrefs.SetFloat(KEY_SUBTITLE_SCALE, SubtitleScale);
            PlayerPrefs.Save();
        }

        /// <summary>색맹 모드일 때 등급 표시용 접두사를 반환합니다. (예: "[전설]")</summary>
        public static string GetRarityPrefix(string rarityDisplayName)
        {
            if (!ColorBlindMode) return "";
            return $"[{rarityDisplayName}] ";
        }
    }
}
