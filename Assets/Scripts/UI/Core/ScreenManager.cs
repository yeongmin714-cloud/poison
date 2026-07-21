using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        private Dictionary<string, Rect> _screens = new Dictionary<string, Rect>();

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

        public Rect GetScreen(string screenName)
        {
            if (_screens.TryGetValue(screenName, out Rect screen))
            {
                return screen;
            }
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        public void RegisterScreen(string name, Rect screen)
        {
            if (!_screens.ContainsKey(name))
            {
                _screens.Add(name, screen);
            }
        }
    }
}