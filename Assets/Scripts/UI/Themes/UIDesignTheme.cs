#pragma warning disable 0414
#nullable disable
using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Phase 33 UI-01: UI 디자인 테마 ScriptableObject.
    /// 배경 패턴, 테두리 스타일, 애니메이션 타입, 색상 팔레트를 정의합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "New UI Theme", menuName = "UI/Design Theme", order = 100)]
    public class UIDesignTheme : ScriptableObject
    {
        // ================================================================
        // 열거형
        // ================================================================

        /// <summary>배경 패턴 종류</summary>
        public enum PatternType
        {
            Parchment,
            Leather,
            Marble,
            Wood,
            Stone,
            Metal,
            Glass
        }

        /// <summary>테두리 장식 종류</summary>
        public enum BorderType
        {
            Filigree,
            Rune,
            Thorn,
            Star,
            Shield
        }

        /// <summary>모서리 장식 종류</summary>
        public enum DecorationType
        {
            None,
            CornerScroll,
            Rivet,
            Seal,
            Crown,
            Skull
        }

        /// <summary>창 애니메이션 종류</summary>
        public enum AnimationType
        {
            FadeSlide,
            Scale,
            Flip,
            Shatter,
            Spin,
            Bounce,
            Reveal,
            Zoom
        }

        // ================================================================
        // 테마 필드
        // ================================================================

        [Header("Theme Info")]
        [SerializeField] private string _themeName = "Default";
        [SerializeField] private string _iconPrefix = "⚔️";

        [Header("Color Palette")]
        [SerializeField] private Color[] _colorSet = new Color[6]
        {
            new Color(0f, 0f, 0f, 0.88f),       // 0: Bg
            new Color(0.85f, 0.65f, 0.15f, 0.8f), // 1: Border
            new Color(0.9f, 0.7f, 0.3f, 1f),     // 2: Title
            Color.white,                           // 3: Text
            new Color(0.75f, 0.75f, 0.75f, 1f),   // 4: SubText
            new Color(0.3f, 0.5f, 0.7f, 1f)       // 5: Accent
        };

        [Header("Pattern & Border")]
        [SerializeField] private PatternType _patternType = PatternType.Parchment;
        [SerializeField] private BorderType _borderType = BorderType.Filigree;
        [SerializeField] private DecorationType _decorationType = DecorationType.None;
        [SerializeField] private AnimationType _animationType = AnimationType.FadeSlide;

        [Header("Medieval Theme (Optional — PNG Textures)")]
        [SerializeField] private string _medievalPanelTexture = "";
        [SerializeField] private string _medievalBackgroundTexture = "";

        [Header("Window Sizing")]
        [SerializeField] private float _windowWidth = 600f;
        [SerializeField] private float _windowHeight = 400f;

        // ================================================================
        // 공개 프로퍼티
        // ================================================================

        public string ThemeName => _themeName;
        public string IconPrefix => _iconPrefix;

        public Color BgColor => _colorSet[0];
        public Color BorderColor => _colorSet[1];
        public Color TitleColor => _colorSet[2];
        public Color TextColor => _colorSet[3];
        public Color SubTextColor => _colorSet[4];
        public Color AccentColor => _colorSet[5];
        public Color[] ColorSet => _colorSet;

        public PatternType CurrentPattern => _patternType;
        public BorderType CurrentBorder => _borderType;
        public DecorationType CurrentDecoration => _decorationType;
        public AnimationType CurrentAnimation => _animationType;

        public float WindowWidth => _windowWidth;
        public float WindowHeight => _windowHeight;

        // ================================================================
        // Medieval Theme Properties
        // ================================================================

        /// <summary>Panel texture name for medieval backgrounds (e.g. "ornate", "dark")</summary>
        public string MedievalPanelTexture => _medievalPanelTexture;

        /// <summary>Background texture name for medieval backgrounds (e.g. "paper", "wood")</summary>
        public string MedievalBackgroundTexture => _medievalBackgroundTexture;

        /// <summary>Returns true if a medieval panel texture is configured and should be used</summary>
        public bool UseMedievalBackground => !string.IsNullOrEmpty(_medievalPanelTexture);

        /// <summary>Sets the medieval panel texture type (ornate, dark, gold, wide)</summary>
        public void SetMedievalPanelTexture(string panelType)
        {
            _medievalPanelTexture = panelType ?? "";
        }

        /// <summary>Sets the medieval background texture type (paper, wood)</summary>
        public void SetMedievalBackgroundTexture(string bgType)
        {
            _medievalBackgroundTexture = bgType ?? "";
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>색상 세트를 한 번에 설정</summary>
        public void SetColorSet(Color bg, Color border, Color title, Color text, Color subText, Color accent)
        {
            _colorSet[0] = bg;
            _colorSet[1] = border;
            _colorSet[2] = title;
            _colorSet[3] = text;
            _colorSet[4] = subText;
            _colorSet[5] = accent;
        }

        /// <summary>인덱스로 색상 반환 (범위 외시 기본 흰색)</summary>
        public Color GetColor(int index)
        {
            if (index >= 0 && index < _colorSet.Length)
                return _colorSet[index];
            return Color.white;
        }

        /// <summary>색상 개수</summary>
        public int ColorCount => _colorSet.Length;
    }
}
