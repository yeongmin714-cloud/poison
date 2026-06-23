using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// G3-08: 사망 화면 — 붉은 Fade + 부활/로드 선택.
    /// PlayerHealth 연동.
    /// </summary>
    public class DeathScreenUI : MonoBehaviour
    {
        public static DeathScreenUI Instance { get; private set; }

        [SerializeField] private float _fadeDuration = 1.5f;
        [SerializeField] private Color _deathOverlayColor = new Color(0.4f, 0.02f, 0.02f, 0f);

        private bool _isVisible;
        private float _fadeTimer;
        private bool _isFading;

        private UIDesignTheme _theme;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _subTextStyle;
        private bool _stylesInit;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _theme = Phase33_Themes.DeathTheme();
        }

        public void Show()
        {
            _isVisible = true;
            _isFading = true;
            _fadeTimer = 0f;
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            _isVisible = false;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (_isFading)
            {
                _fadeTimer += Time.unscaledDeltaTime;
                if (_fadeTimer >= _fadeDuration)
                {
                    _fadeTimer = _fadeDuration;
                    _isFading = false;
                }
            }
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _titleStyle = new GUIStyle
            {
                fontSize = 768, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.2f, 0.2f, 1f) }
            };
            _buttonStyle = new GUIStyle
            {
                fontSize = 320, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _buttonStyle.hover.background = UIStyleManager.MakeTexture(1, 1, new Color(0.5f, 0.1f, 0.1f, 1f));
            _buttonStyle.active.background = UIStyleManager.MakeTexture(1, 1, new Color(0.3f, 0.05f, 0.05f, 1f));
            _subTextStyle = new GUIStyle
            {
                fontSize = 256, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.4f, 0.4f, 1f) }
            };
            _stylesInit = true;
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            InitStyles();

            // 붉은 오버레이 (페이드 인)
            float alpha = Mathf.Clamp01(_fadeTimer / _fadeDuration);
            Color overlayColor = _deathOverlayColor;
            overlayColor.a = alpha * 0.85f;
            var overlayTex = UIStyleManager.MakeTexture(1, 1, overlayColor);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", new GUIStyle { normal = { background = overlayTex } });

            if (!_isFading)
            {
                int cx = Screen.width / 2;
                int cy = Screen.height / 2;

                // YOU DIED
                GUI.Label(new Rect(cx - 150, cy - 120, 675, 135), "YOU DIED", _titleStyle);
                GUI.Label(new Rect(cx - 150, cy - 60, 675, 45), "당신은 쓰러졌습니다...", _subTextStyle);

                // 부활 버튼
                int btnW = 330;
                int btnH = 75;
                int btnX = cx - btnW / 2;

                GUI.backgroundColor = new Color(0.3f, 0.1f, 0.1f, 0.9f);
                if (GUI.Button(new Rect(btnX, cy, btnW, btnH), "🔄 부활", _buttonStyle))
                    OnRespawn();

                GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                if (GUI.Button(new Rect(btnX, cy + btnH + 15, btnW, btnH), "📂 저장 불러오기", _buttonStyle))
                    OnLoadGame();
            }
        }

        private void OnRespawn()
        {
            // PlayerHealth 체력 회복
            if (PlayerHealth.Instance != null)
            {
                // 리플렉션으로 _currentHP 최대치로 설정 (Heal은 상대치)
                var hpField = typeof(PlayerHealth).GetField("_currentHP",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var maxField = typeof(PlayerHealth).GetField("_maxHP",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hpField != null && maxField != null)
                {
                    hpField.SetValue(PlayerHealth.Instance, maxField.GetValue(PlayerHealth.Instance));
                }
                var deadField = typeof(PlayerHealth).GetField("_isDead",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (deadField != null)
                    deadField.SetValue(PlayerHealth.Instance, false);
            }
            Hide();
        }

        private void OnLoadGame()
        {
            Hide();
            // SaveManager의 첫 번째 슬롯 로드
            if (SaveManager.Instance != null)
                SaveManager.Instance.Load(0);
        }
    }
}