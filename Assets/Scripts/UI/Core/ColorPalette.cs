using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class ColorPalette : MonoBehaviour
    {
        public static ColorPalette Instance { get; private set; }

        private Dictionary<string, Color> _colors = new Dictionary<string, Color>();

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

        public Color GetColor(string colorName)
        {
            if (_colors.TryGetValue(colorName, out Color color))
            {
                return color;
            }
            return Color.white;
        }

        public void RegisterColor(string name, Color color)
        {
            _colors.Add(name, color);
        }
    }
}