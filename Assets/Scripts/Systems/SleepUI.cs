using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C16-02: IMGUI 기반 수면 옵션 UI 싱글톤.
    /// 침대 상호작용 시 수면 시간을 선택하는 창을 표시합니다.
    /// 
    /// C16-03: 수면 중 검은 화면 오버레이 + ESC 기상 지원.
    /// </summary>
    public class SleepUI : MonoBehaviour
    {
        public static SleepUI Instance { get; private set; }

        [Header("Layout")]
        [SerializeField] private int _windowWidth = 300;
        [SerializeField] private int _windowHeight = 300;
        [SerializeField] private int _buttonHeight = 40;
        [SerializeField] private int _buttonSpacing = 8;

        [Header("Colors")]
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _buttonColor = new Color(0.2f, 0.3f, 0.6f, 0.9f);
        [SerializeField] private Color _cancelButtonColor = new Color(0.6f, 0.2f, 0.2f, 0.9f);

        [Header("Sleep Overlay")]
        [SerializeField] private Color _sleepOverlayColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private int _overlayFontSize = 28;

        private Bed _currentBed;
        private bool _isVisible;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _cancelButtonStyle;
        private GUIStyle _overlayStyle;
        private bool _stylesInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SleepUI] 중복 인스턴스 파괴");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // 수면 중 ESC 누르면 기상
            if (TimeManager.Instance != null && TimeManager.Instance.IsSleeping)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    TimeManager.Instance.WakeUp();
                    _isVisible = false;
                    _currentBed = null;
                }
            }
        }

        /// <summary>
        /// 수면 옵션 UI를 표시합니다.
        /// </summary>
        /// <param name="bed">상호작용한 침대</param>
        public void Show(Bed bed)
        {
            _currentBed = bed;
            _isVisible = true;
        }

        /// <summary>
        /// 수면 옵션 UI를 닫습니다.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _currentBed = null;
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };

            _buttonStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };
            _buttonStyle.hover.background = MakeTexture(1, 1, new Color(0.3f, 0.4f, 0.8f, 1f));
            _buttonStyle.active.background = MakeTexture(1, 1, new Color(0.1f, 0.2f, 0.5f, 1f));

            _cancelButtonStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };
            _cancelButtonStyle.hover.background = MakeTexture(1, 1, new Color(0.8f, 0.3f, 0.3f, 1f));
            _cancelButtonStyle.active.background = MakeTexture(1, 1, new Color(0.5f, 0.1f, 0.1f, 1f));

            _overlayStyle = new GUIStyle
            {
                fontSize = _overlayFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _stylesInitialized = true;
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            InitializeStyles();

            // ===== 수면 중 검은 화면 오버레이 =====
            if (TimeManager.Instance != null && TimeManager.Instance.IsSleeping)
            {
                // 전체 화면 검정 오버레이
                var overlayTex = MakeTexture(1, 1, _sleepOverlayColor);
                var overlayStyle = new GUIStyle { normal = { background = overlayTex } };
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", overlayStyle);

                // "😴 Sleeping... (ESC to wake)" 텍스트
                GUI.Label(new Rect(0, Screen.height / 2 - 50, Screen.width, 60), "😴 Sleeping...\n(ESC to wake)", _overlayStyle);
                return;
            }

            // ===== 수면 옵션 UI (침대 상호작용 시) =====
            if (!_isVisible || _currentBed == null) return;

            int centerX = (Screen.width - _windowWidth) / 2;
            int centerY = (Screen.height - _windowHeight) / 2;

            // 배경 박스
            var bgTex = MakeTexture(1, 1, _bgColor);
            var bgStyle = new GUIStyle { normal = { background = bgTex } };
            GUI.Box(new Rect(centerX, centerY, _windowWidth, _windowHeight), "", bgStyle);

            // 제목
            string title = $"{_currentBed.BedName} — 얼마나 주무시겠습니까?";
            GUI.Label(new Rect(centerX, centerY + 15, _windowWidth, 35), title, _titleStyle);

            // 버튼들
            int buttonStartY = centerY + 65;
            int buttonX = centerX + 20;
            int buttonWidth = _windowWidth - 40;

            DrawSleepButton(buttonX, buttonStartY, buttonWidth, "2시간 자기", () => StartSleep(2f));
            DrawSleepButton(buttonX, buttonStartY + (_buttonHeight + _buttonSpacing) * 1, buttonWidth, "4시간 자기", () => StartSleep(4f));
            DrawSleepButton(buttonX, buttonStartY + (_buttonHeight + _buttonSpacing) * 2, buttonWidth, "6시간 자기", () => StartSleep(6f));
            DrawSleepButton(buttonX, buttonStartY + (_buttonHeight + _buttonSpacing) * 3, buttonWidth, "8시간 자기", () => StartSleep(8f));
            DrawSleepButton(buttonX, buttonStartY + (_buttonHeight + _buttonSpacing) * 4, buttonWidth, "아침까지 자기", () => StartSleep(-1f));

            // 취소 버튼
            int cancelY = buttonStartY + (_buttonHeight + _buttonSpacing) * 5 + 5;
            GUI.backgroundColor = _cancelButtonColor;
            if (GUI.Button(new Rect(buttonX, cancelY, buttonWidth, _buttonHeight), "취소", _cancelButtonStyle))
            {
                Hide();
            }
        }

        private void DrawSleepButton(int x, int y, int width, string label, Action onClick)
        {
            GUI.backgroundColor = _buttonColor;
            if (GUI.Button(new Rect(x, y, width, _buttonHeight), label, _buttonStyle))
            {
                onClick?.Invoke();
            }
        }

        private void StartSleep(float hours)
        {
            // C16-04: 저장 슬롯 선택 UI 표시 (저장 후 수면 진행)
            if (SaveSlotUI.Instance != null)
            {
                SaveSlotUI.Instance.Show(hours);
                _isVisible = false; // 저장 UI로 전환
            }
            else if (TimeManager.Instance != null)
            {
                // SaveSlotUI가 없으면 기존처럼 바로 수면 (fallback)
                TimeManager.Instance.SleepFor(hours, OnWakeUpComplete);
                _isVisible = false;
            }
        }

        private void OnWakeUpComplete()
        {
            _currentBed = null;
            Debug.Log("[SleepUI] 기상 완료!");
        }
    }
}