using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// 프리셋 저장 시 이름 입력 팝업 (IMGUI singleton)
    /// 텍스트 필드 + [저장] [취소] 버튼
    /// ESC 닫기 지원, 12자 제한
    /// </summary>
    public class PresetNamePopup : MonoBehaviour
    {
        public static PresetNamePopup Instance { get; private set; }

        [Header("Popup Settings")]
        [SerializeField] private int _windowWidth = 600;
        [SerializeField] private int _windowHeight = 300;

        // ── 상태 ──
        private bool _isVisible;
        private string _inputText = "";
        private System.Action<string> _onSaveCallback;
        private System.Action _onCancelCallback;
        private string _title = "프리셋 이름 입력";
        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesInitialized;

        // ── 외부 프로퍼티 ──
        public bool IsVisible => _isVisible;
        public string InputText => _inputText;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 36,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.15f, 0.15f, 0.2f, 0.95f)) }
            };

            _textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 44,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.25f, 0.25f, 0.3f, 0.95f)) },
                focused = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.3f, 0.3f, 0.35f, 0.95f)) }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.2f, 0.5f, 0.2f, 0.9f)) },
                hover = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.3f, 0.6f, 0.3f, 1f)) },
                active = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.15f, 0.4f, 0.15f, 1f)) }
            };
        }

        private Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 프리셋 이름 입력 팝업을 표시합니다.
        /// </summary>
        /// <param name="title">팝업 제목</param>
        /// <param name="defaultText">기본 입력 텍스트</param>
        /// <param name="onSave">저장 버튼 클릭 시 콜백 (입력된 이름 전달)</param>
        /// <param name="onCancel">취소 버튼 클릭 시 콜백</param>
        public void Show(string title = "프리셋 이름 입력", string defaultText = "", System.Action<string> onSave = null, System.Action onCancel = null)
        {
            if (Instance == null)
            {
                Debug.LogError("[PresetNamePopup] Instance is null!");
                return;
            }

            Instance._title = title ?? "프리셋 이름 입력";
            Instance._inputText = defaultText ?? "";
            Instance._onSaveCallback = onSave;
            Instance._onCancelCallback = onCancel;
            Instance._isVisible = true;
            Instance._stylesInitialized = false;
        }

        /// <summary>
        /// 팝업을 닫습니다.
        /// </summary>
        public void Hide()
        {
            if (Instance == null) return;
            Instance._isVisible = false;
            Instance._inputText = "";
            Instance._onSaveCallback = null;
            Instance._onCancelCallback = null;
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            InitializeStyles();

            // 딤드 오버레이
            UIStyleManager.DrawDimOverlay();

            // 팝업 창
            float winX = (Screen.width - _windowWidth) / 2f;
            float winY = (Screen.height - _windowHeight) / 2f;
            Rect winRect = new Rect(winX, winY, _windowWidth, _windowHeight);

            // 배경
            GUI.Box(winRect, "", _boxStyle);

            // 제목
            float titleY = winY + 20;
            GUI.Label(new Rect(winX + 20, titleY, _windowWidth - 40, 60), _title, _titleStyle);

            // 텍스트 입력 필드 (12자 제한)
            float fieldY = titleY + 70;
            GUI.SetNextControlName("PresetNameField");
            string newText = GUI.TextField(new Rect(winX + 40, fieldY, _windowWidth - 80, 60), _inputText, 12, _textFieldStyle);
            if (newText != _inputText)
            {
                _inputText = newText;
            }

            // 포커스 자동 설정
            if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "PresetNameField")
            {
                GUI.FocusControl("PresetNameField");
            }

            // ── 버튼들 ──
            float buttonY = fieldY + 80;
            float buttonWidth = 180;
            float buttonHeight = 60;
            float totalWidth = buttonWidth * 2 + 20;
            float startX = winX + (_windowWidth - totalWidth) / 2f;

            // 저장 버튼
            if (GUI.Button(new Rect(startX, buttonY, buttonWidth, buttonHeight), "저장", _buttonStyle))
            {
                string trimmedName = _inputText.Trim();
                if (!string.IsNullOrEmpty(trimmedName))
                {
                    _onSaveCallback?.Invoke(trimmedName);
                    Hide();
                }
                else
                {
                    Debug.LogWarning("[PresetNamePopup] 프리셋 이름이 비어있습니다.");
                }
            }

            // 취소 버튼
            var cancelStyle = new GUIStyle(_buttonStyle)
            {
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.5f, 0.2f, 0.2f, 0.9f)) },
                hover = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.6f, 0.3f, 0.3f, 1f)) },
                active = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.4f, 0.15f, 0.15f, 1f)) }
            };

            if (GUI.Button(new Rect(startX + buttonWidth + 20, buttonY, buttonWidth, buttonHeight), "취소", cancelStyle))
            {
                _onCancelCallback?.Invoke();
                Hide();
            }

            // ESC 키 감지 → 닫기
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _onCancelCallback?.Invoke();
                Hide();
                Event.current.Use();
            }

            // Enter 키 감지 → 저장
            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                string trimmedName = _inputText.Trim();
                if (!string.IsNullOrEmpty(trimmedName))
                {
                    _onSaveCallback?.Invoke(trimmedName);
                    Hide();
                }
                Event.current.Use();
            }
        }
    }
}