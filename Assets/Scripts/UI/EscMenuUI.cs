using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// G3-07: ESC 메뉴 (일시정지).
    /// ESC 키 → Time.timeScale=0, 재개/저장/설정/타이틀로/종료 버튼.
    /// </summary>
    public class EscMenuUI : MonoBehaviour
    {
        public static EscMenuUI Instance { get; private set; }

        [SerializeField] private int _windowWidth = 1012;
        [SerializeField] private int _windowHeight = 1125;
        [SerializeField] private int _buttonHeight = 112;
        [SerializeField] private int _buttonSpacing = 18;

        private bool _isOpen;
        private UIDesignTheme _theme;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesInit;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _theme = Phase33_Themes.EscMenuTheme();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            if (_isOpen) Resume(); else Open();
        }

        public void Open()
        {
            _isOpen = true;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            _isOpen = false;
            Time.timeScale = 1f;
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _titleStyle = new GUIStyle
            {
                fontSize = 96, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = UIStyleManager.TitleColor }
            };
            _buttonStyle = new GUIStyle
            {
                fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _buttonStyle.hover.background = UIStyleManager.MakeTexture(1, 1, UIStyleManager.HoverColor);
            _buttonStyle.active.background = UIStyleManager.MakeTexture(1, 1, new Color(0.1f, 0.2f, 0.5f, 1f));
            _stylesInit = true;
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            InitStyles();
            UIStyleManager.DrawDimOverlay();

            int cx = (Screen.width - _windowWidth) / 2;
            int cy = (Screen.height - _windowHeight) / 2;
            var bgRect = new Rect(cx, cy, _windowWidth, _windowHeight);

            UIStyleManager.DrawWindowBackground(bgRect);
            UIStyleManager.DrawTitle(bgRect, "일시정지");

            int btnY = cy + 60;
            int btnW = 330;
            int btnX = cx + (_windowWidth - btnW) / 2;

            string[] labels = { "▶ 재개", "💾 저장", "⚙ 설정", "🏠 타이틀로", "❌ 종료" };
            System.Action[] actions = {
                Resume,
                () => { if (SaveManager.Instance != null) SaveManager.Instance.AutoSave(); Resume(); },
                () => { Resume(); SettingsMenuUI.Instance?.Show(); },
                () => { Time.timeScale = 1f; UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu"); },
                () => { Time.timeScale = 1f; Application.Quit(); }
            };

            for (int i = 0; i < labels.Length; i++)
            {
                int y = btnY + i * (_buttonHeight + _buttonSpacing);
                GUI.backgroundColor = i == 4 ? new Color(0.6f, 0.2f, 0.2f, 0.9f) : new Color(0.2f, 0.35f, 0.5f, 0.9f);
                if (GUI.Button(new Rect(btnX, y, btnW, _buttonHeight), labels[i], _buttonStyle))
                    actions[i]();
            }
        }
    }
}