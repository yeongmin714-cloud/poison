using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
#pragma warning disable 0414

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

        // --- 캐싱된 스타일 (OnGUI GC 방지) ---
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _subTextStyle;
        private GUIStyle _overlayStyle;
        private Texture2D _overlayTex;
        private bool _stylesInit;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            // Time.timeScale 안전 복원 — 오브젝트 파괴 시 타임스케일 고정 방지
            if (_isVisible)
            {
                Time.timeScale = 1f;
            }

            // 텍스처 정리
            if (_overlayTex != null)
            {
                Destroy(_overlayTex);
                _overlayTex = null;
            }
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
                fontSize = 60,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.2f, 0.2f, 1f) }
            };

            _buttonStyle = new GUIStyle
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _buttonStyle.hover.background = UIStyleManager.MakeTexture(1, 1, new Color(0.5f, 0.1f, 0.1f, 1f));
            _buttonStyle.active.background = UIStyleManager.MakeTexture(1, 1, new Color(0.3f, 0.05f, 0.05f, 1f));

            _subTextStyle = new GUIStyle
            {
                fontSize = 24,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.4f, 0.4f, 1f) }
            };

            // 오버레이용 1x1 흰색 텍스처 캐싱 (OnGUI에서 매 프레임 새 텍스처 생성 방지)
            _overlayTex = new Texture2D(1, 1);
            _overlayTex.hideFlags = HideFlags.HideAndDontSave;
            _overlayTex.SetPixel(0, 0, Color.white);
            _overlayTex.Apply();
            _overlayStyle = new GUIStyle { normal = { background = _overlayTex } };

            _stylesInit = true;
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            InitStyles();

            // 붉은 오버레이 (페이드 인) — GUI.color로 색상만 변경, 텍스처 재사용
            float alpha = Mathf.Clamp01(_fadeTimer / _fadeDuration);
            Color overlayColor = _deathOverlayColor;
            overlayColor.a = alpha * 0.85f;

            var prevColor = GUI.color;
            GUI.color = overlayColor;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _overlayStyle);
            GUI.color = prevColor;

            if (!_isFading)
            {
                int cx = Screen.width / 2;
                int cy = Screen.height / 2;

                // YOU DIED
                GUI.Label(new Rect(cx - 225, cy - 120, 450, 90), "YOU DIED", _titleStyle);
                GUI.Label(new Rect(cx - 200, cy - 60, 400, 40), "당신은 쓰러졌습니다...", _subTextStyle);

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
            // PlayerHealth 체력 회복 (리플렉션 최소화)
            if (PlayerHealth.Instance != null)
            {
                // _isDead = false 로 설정 (필요한 리플렉션만 유지)
                var deadField = typeof(PlayerHealth).GetField("_isDead",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (deadField != null)
                    deadField.SetValue(PlayerHealth.Instance, false);

                // 공개 API인 HealFull() 사용 (리플렉션 불필요)
                PlayerHealth.Instance.HealFull();
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
