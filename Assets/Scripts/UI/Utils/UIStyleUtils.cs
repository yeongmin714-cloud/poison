using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIStyleUtils : MonoBehaviour
    {
        [Header("Style Settings")]
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.black;

        public void ApplyStyle(Graphic graphic, string styleName)
        {
            // Apply style to graphic
            Debug.Log($"Applying style: {styleName}");
        }

        public void SetColor(Graphic graphic, Color color)
        {
            // Set color of graphic
            if (graphic != null)
                graphic.color = color;
        }

        public void SetFontSize(Text text, int fontSize)
        {
            // Set font size of text
            if (text != null)
                text.fontSize = fontSize;
        }
    }
}