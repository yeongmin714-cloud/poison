#nullable disable
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Phase 33 UI-01: UI 디자인 테마 매니저 싱글톤.
    /// 모든 UIDesignTheme ScriptableObject를 관리하고 창 타입별로 테마를 매핑합니다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class UIDesignManager : MonoBehaviour
    {
        // ================================================================
        // 싱글톤
        // ================================================================

        private static UIDesignManager _instance;
        public static UIDesignManager Instance => _instance;

        // ================================================================
        // 인스펙터 필드
        // ================================================================

        [Header("Theme Registry")]
        [SerializeField] private UIDesignTheme _defaultTheme;

        [Header("Window → Theme Mapping")]
        [SerializeField] private List<WindowThemeEntry> _windowThemeEntries = new List<WindowThemeEntry>();

        // ================================================================
        // 내부 캐싱
        // ================================================================

        private Dictionary<string, UIDesignTheme> _themeMap = new Dictionary<string, UIDesignTheme>();

        // ================================================================
        // Serialized helper
        // ================================================================

        [System.Serializable]
        public struct WindowThemeEntry
        {
            public string windowType;       // "QuestWindow", "InventoryWindow", etc.
            public UIDesignTheme theme;
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
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            BuildThemeMap();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// WindowThemeEntry 목록을 Dictionary로 변환합니다.
        /// </summary>
        private void BuildThemeMap()
        {
            _themeMap.Clear();
            foreach (var entry in _windowThemeEntries)
            {
                if (!string.IsNullOrEmpty(entry.windowType) && entry.theme != null)
                {
                    if (!_themeMap.ContainsKey(entry.windowType))
                        _themeMap[entry.windowType] = entry.theme;
                }
            }
        }

        // ================================================================
        // 공개 API
        // ================================================================

        /// <summary>
        /// 창 타입 이름(string)에 해당하는 테마를 반환합니다.
        /// 매핑이 없으면 기본 테마(defaultTheme)를 반환하고,
        /// 기본 테마마저 없으면 새 기본 인스턴스를 생성합니다.
        /// </summary>
        public UIDesignTheme GetThemeForWindow(string windowType)
        {
            if (!string.IsNullOrEmpty(windowType) && _themeMap.TryGetValue(windowType, out UIDesignTheme theme))
                return theme;

            if (_defaultTheme != null)
                return _defaultTheme;

            // 최후 폴백: 기본 테마 동적 생성
            return CreateFallbackTheme();
        }

        /// <summary>
        /// 창 타입에 테마를 등록합니다 (런타임).
        /// </summary>
        public void RegisterTheme(string windowType, UIDesignTheme theme)
        {
            if (string.IsNullOrEmpty(windowType) || theme == null)
                return;

            _themeMap[windowType] = theme;
        }

        /// <summary>
        /// 기본 테마를 설정합니다.
        /// </summary>
        public void SetDefaultTheme(UIDesignTheme theme)
        {
            _defaultTheme = theme;
        }

        /// <summary>
        /// 모든 캐시된 텍스처를 정리합니다.
        /// </summary>
        public static void ClearTextureCache()
        {
            ProceduralTextureGenerator.ClearCache();
            MedievalUIResources.ClearCache();
        }

        // ================================================================
        // 내부
        // ================================================================

        private static UIDesignTheme CreateFallbackTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Fallback Theme";
            theme.SetColorSet(
                new Color(0f, 0f, 0f, 0.88f),       // bg
                new Color(0.85f, 0.65f, 0.15f, 0.8f), // border
                new Color(0.9f, 0.7f, 0.3f, 1f),     // title
                Color.white,                           // text
                new Color(0.75f, 0.75f, 0.75f, 1f),   // subText
                new Color(0.3f, 0.5f, 0.7f, 1f)       // accent
            );
            // 리플렉션 없이 public 메서드만 사용
            return theme;
        }
    }
}