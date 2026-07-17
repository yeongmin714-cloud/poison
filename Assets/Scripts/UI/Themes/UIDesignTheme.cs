using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// UI 디자인 테마 데이터 컨테이너 (ScriptableObject).
    /// 색상, 폰트, 크기, 간격, 그림자, 테두리, 트랜지션 등 UI 전반의 스타일을 정의합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "UIDesignTheme", menuName = "ProjectName/UI/Design Theme")]
    public class UIDesignTheme : ScriptableObject
    {
        // ================================================================
        // 중첩 열거형 (다른 UI 시스템에서 참조)
        // ================================================================

        /// <summary>
        /// 장식 테두리 종류 (DecorativeBorderRenderer에서 사용)
        /// </summary>
        public enum BorderType
        {
            None = 0,
            Filigree = 1,
            Rune = 2,
            Thorn = 3,
            Star = 4,
            Shield = 5,
            Chain = 6,
            Barbed = 7
        }

        /// <summary>
        /// 절차적 텍스처 패턴 종류 (ProceduralTextureGenerator에서 사용)
        /// </summary>
        public enum PatternType
        {
            Parchment = 0,
            Stone = 1,
            Metal = 2,
            Fabric = 3,
            Leather = 4,
            Wood = 5,
            Marble = 6,
            Glass = 7
        }

        /// <summary>
        /// 창 애니메이션 종류 (WindowAnimationProfile에서 사용)
        /// </summary>
        public enum AnimationType
        {
            None = 0,
            Fade = 1,
            Slide = 2,
            Scale = 3,
            FadeSlide = 4,
            Pulse = 5,
            Flip = 6,
            Shatter = 7,
            Spin = 8,
            Pop = 9,
            Bounce = 10,
            Expand = 11,
            Reveal = 12,
            Zoom = 13
        }

        [Header("Colors")]
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.white;
        public Color accentColor = Color.white;
        public Color warningColor = Color.white;
        public Color errorColor = Color.white;
        public Color successColor = Color.white;
        public Color disabledColor = Color.white;
        public Color textColor = Color.white;
        public Color backgroundColor = Color.white;
        public Color panelColor = Color.white;
        public Color borderColor = Color.white;

        [Header("Fonts")]
        public Font primaryFont;
        public Color secondaryFontColor = Color.white;
        public Font secondaryFont;

        [Header("Sizes")]
        public float fontSizeSmall = 12f;
        public float fontSizeMedium = 16f;
        public float fontSizeLarge = 20f;
        public float fontSizeXLarge = 24f;

        [Header("Spacing")]
        public float spacingTiny = 2f;
        public float spacingSmall = 4f;
        public float spacingMedium = 8f;
        public float spacingLarge = 16f;
        public float spacingXLarge = 32f;

        [Header("Shadows")]
        public bool useShadows = true;
        public Color shadowColor = Color.black;
        public Vector2 shadowOffset = new Vector2(2f, -2f);
        public float shadowBlur = 2f;

        [Header("Borders")]
        public bool useBorders = true;
        public float borderWidth = 2f;
        public Color borderColorNormal = Color.white;
        public Color borderColorHover = Color.white;
        public Color borderColorActive = Color.white;

        [Header("Transitions")]
        public float transitionDuration = 0.2f;
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Animation & Pattern")]
        [SerializeField] private AnimationType _currentAnimation = AnimationType.FadeSlide;
        [SerializeField] private PatternType _currentPattern = PatternType.Parchment;
        [SerializeField] private BorderType _currentBorder = BorderType.Filigree;
        [SerializeField] private bool _useMedievalBackground = false;
        [SerializeField] private string _medievalPanelTexture = "Parchment";

        // ================================================================
        // 호환성 속성 (기존 코드에서 사용하는 이름)
        // ================================================================

        /// <summary>배경 색상 (BgColor → backgroundColor)</summary>
        public Color BgColor => backgroundColor;

        /// <summary>강조 색상 (AccentColor → accentColor)</summary>
        public Color AccentColor => accentColor;

        /// <summary>테두리 색상 (BorderColor → borderColor)</summary>
        public Color BorderColor => borderColor;

        /// <summary>텍스트 색상 (TextColor → textColor)</summary>
        public Color TextColor => textColor;

        /// <summary>제목 색상 (TitleColor → primaryColor)</summary>
        public Color TitleColor => primaryColor;

        /// <summary>부제목 색상 (SubTextColor → secondaryFontColor)</summary>
        public Color SubTextColor => secondaryFontColor;

        /// <summary>패널 색상 (PanelColor → panelColor)</summary>
        public Color PanelColor => panelColor;

        /// <summary>현재 애니메이션 타입</summary>
        public AnimationType CurrentAnimation => _currentAnimation;

        /// <summary>현재 패턴 타입</summary>
        public PatternType CurrentPattern => _currentPattern;

        /// <summary>현재 테두리 타입</summary>
        public BorderType CurrentBorder => _currentBorder;

        /// <summary>중세 배경 사용 여부</summary>
        public bool UseMedievalBackground => _useMedievalBackground;

        /// <summary>중세 패널 텍스처 이름</summary>
        public string MedievalPanelTexture => _medievalPanelTexture;

        // FontSize 속성들
        public float FontSizeSmall => fontSizeSmall;
        public float FontSizeMedium => fontSizeMedium;
        public float FontSizeLarge => fontSizeLarge;
        public float FontSizeXLarge => fontSizeXLarge;

        // Spacing 속성들
        public float SpacingTiny => spacingTiny;
        public float SpacingSmall => spacingSmall;
        public float SpacingMedium => spacingMedium;
        public float SpacingLarge => spacingLarge;
        public float SpacingXLarge => spacingXLarge;

        // Shadow 속성들
        public Color ShadowColor => shadowColor;
        public Vector2 ShadowOffset => shadowOffset;
        public float ShadowBlur => shadowBlur;

        // Transition 속성
        public float TransitionDuration => transitionDuration;
        public AnimationCurve TransitionCurve => transitionCurve;

        /// <summary>
        /// 테마의 색상 세트를 일괄 설정합니다.
        /// </summary>
        public void SetColorSet(Color background, Color border, Color title, Color text, Color subText, Color accent)
        {
            backgroundColor = background;
            borderColor = border;
            primaryColor = title;    // title용
            textColor = text;
            secondaryFontColor = subText;
            accentColor = accent;
        }

        /// <summary>
        /// 중세 배경 설정
        /// </summary>
        public void SetMedievalBackground(bool use, string panelTexture = "Parchment")
        {
            _useMedievalBackground = use;
            _medievalPanelTexture = panelTexture;
        }

        /// <summary>
        /// 현재 애니메이션 타입 설정
        /// </summary>
        public void SetAnimationType(AnimationType type)
        {
            _currentAnimation = type;
        }

        /// <summary>
        /// 현재 패턴 타입 설정
        /// </summary>
        public void SetPatternType(PatternType type)
        {
            _currentPattern = type;
        }

        /// <summary>
        /// 현재 테두리 타입 설정
        /// </summary>
        public void SetBorderType(BorderType type)
        {
            _currentBorder = type;
        }
    }
}