using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class ThemeManager : MonoBehaviour
    {
        public static ThemeManager Instance { get; private set; }

        private Dictionary<string, Color> _colors = new Dictionary<string, Color>();
        private Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private Dictionary<string, AudioClip> _audioClips = new Dictionary<string, AudioClip>();
        private string _currentTheme = "Default";

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

        public void SetTheme(string themeName)
        {
            _currentTheme = themeName;
        }

        public Color GetColor(string colorName)
        {
            if (_colors.TryGetValue(colorName, out Color color))
            {
                return color;
            }
            return Color.white;
        }

        public Sprite GetSprite(string spriteName)
        {
            if (_sprites.TryGetValue(spriteName, out Sprite sprite))
            {
                return sprite;
            }
            return null;
        }

        public AudioClip GetAudioClip(string audioName)
        {
            if (_audioClips.TryGetValue(audioName, out AudioClip audioClip))
            {
                return audioClip;
            }
            return null;
        }

        public void RegisterColor(string name, Color color)
        {
            if (!_colors.ContainsKey(name))
            {
                _colors.Add(name, color);
            }
        }

        public void RegisterSprite(string name, Sprite sprite)
        {
            if (!_sprites.ContainsKey(name))
            {
                _sprites.Add(name, sprite);
            }
        }

        public void RegisterAudioClip(string name, AudioClip audioClip)
        {
            if (!_audioClips.ContainsKey(name))
            {
                _audioClips.Add(name, audioClip);
            }
        }
    }
}