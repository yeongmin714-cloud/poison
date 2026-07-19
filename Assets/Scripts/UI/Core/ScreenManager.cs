using UnityEngine;
using UnityEngine.UI;

namespace ProjectName.UI.Core
{
    public class ScreenManager : MonoBehaviour
    {
        private Dictionary<string, RectTransform> screens = new Dictionary<string, RectTransform>();

        public static ScreenManager Instance { get; private set; }

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

        public void RegisterScreen(string name, RectTransform screen)
        {
            screens[name] = screen;
        }

        public void ShowScreen(string name)
        {
            if (screens.TryGetValue(name, out RectTransform screen))
            {
                screen.gameObject.SetActive(true);
            }
        }

        public void HideScreen(string name)
        {
            if (screens.TryGetValue(name, out RectTransform screen))
            {
                screen.gameObject.SetActive(false);
            }
        }
    }
}