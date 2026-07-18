using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UISpriteUtils : MonoBehaviour
    {
        [Header("Sprite Settings")]
        public Image image;
        public Sprite[] sprites;

        public void SetSprite(int index)
        {
            // Set sprite by index
            if (image != null && index >= 0 && index < sprites.Length)
            {
                image.sprite = sprites[index];
            }
        }

        public void SetSprite(Sprite sprite)
        {
            // Set sprite directly
            if (image != null)
            {
                image.sprite = sprite;
            }
        }

        public Sprite GetSprite(int index)
        {
            // Get sprite by index
            if (index >= 0 && index < sprites.Length)
            {
                return sprites[index];
            }
            return null;
        }
    }
}