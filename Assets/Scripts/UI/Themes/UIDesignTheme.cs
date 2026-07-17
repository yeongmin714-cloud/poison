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
    }
}