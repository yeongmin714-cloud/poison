using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Themes
{
    public class UIDesignTheme : MonoBehaviour
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
        
        public static UIDesignTheme Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}