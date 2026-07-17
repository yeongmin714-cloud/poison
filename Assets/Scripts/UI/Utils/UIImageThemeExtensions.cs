using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ProjectName.UI.Themes;

namespace ProjectName.UI.Utils
{
    public static class UIImageThemeExtensions
    {
        public static void ApplyThemeToImage(this Image image, UIDesignTheme theme)
        {
            if (image == null || theme == null) return;
            
            // Apply theme colors to image
            // This is a placeholder implementation
        }
        
        public static void ApplyThemeToSprite(this Sprite sprite, UIDesignTheme theme)
        {
            if (sprite == null || theme == null) return;
            
            // Apply theme colors to sprite
            // This is a placeholder implementation
        }

        /// <summary>
        /// 중세 스타일 패널 텍스처를 이미지에 적용합니다.
        /// </summary>
        public static void SetMedievalPanelTexture(this Image image, UIDesignTheme theme, string textureName = "Parchment")
        {
            if (image == null || theme == null) return;

            // ProceduralTextureGenerator를 사용하여 텍스처 생성/가져오기
            var texture = ProceduralTextureGenerator.GetPatternTexture(
                (UIDesignTheme.PatternType)System.Enum.Parse(typeof(UIDesignTheme.PatternType), textureName)
            );

            if (texture != null)
            {
                image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                image.type = Image.Type.Tiled;
            }
        }

        /// <summary>
        /// 중세 스타일 배경 텍스처를 이미지에 적용합니다.
        /// </summary>
        public static void SetMedievalBackgroundTexture(this Image image, UIDesignTheme theme, string textureName = "Parchment")
        {
            if (image == null || theme == null) return;

            var texture = ProceduralTextureGenerator.GetPatternTexture(
                (UIDesignTheme.PatternType)System.Enum.Parse(typeof(UIDesignTheme.PatternType), textureName)
            );

            if (texture != null)
            {
                image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                image.type = Image.Type.Tiled;
                image.color = theme.backgroundColor;
            }
        }
    }
}