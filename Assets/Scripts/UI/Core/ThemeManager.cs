using UnityEngine;

namespace ProjectName.UI.Core
{
    public class ThemeManager : MonoBehaviour
    {
        public static ThemeManager Instance { get; private set; }

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

        public void ApplyTheme(string themeName)
        {
            // Implementation for applying a theme
        }
    }
}