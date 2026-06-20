using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C16-04: 저장 슬롯 선택 IMGUI UI.
    /// SleepUI에서 수면 옵션 선택 후 저장할 슬롯을 선택하는 화면을 표시합니다.
    /// 3개 슬롯 버튼 + 확인/취소 버튼을 제공합니다.
    /// </summary>
    public class SaveSlotUI : MonoBehaviour
    {
        public static SaveSlotUI Instance { get; private set; }

        [Header("Layout")]
        [SerializeField] private int _windowWidth = 400;
        [SerializeField] private int _windowHeight = 480;
        [SerializeField] private int _slotButtonHeight = 60;
        [SerializeField] private int _buttonSpacing = 8;
        [SerializeField] private int _confirmButtonHeight = 40;

        [Header("Colors")]
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _slotColor = new Color(0.2f, 0.35f, 0.5f, 0.9f);
        [SerializeField] private Color _slotEmptyColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        [SerializeField] private Color _selectedSlotColor = new Color(0.3f, 0.6f, 0.3f, 0.9f);
        [SerializeField] private Color _confirmColor = new Color(0.2f, 0.5f, 0.2f, 0.9f);
        [SerializeField] private Color _cancelColor = new Color(0.6f, 0.2f, 0.2f, 0.9f);

        // ===== 상태 =====
        private bool _isVisible;
        private int _selectedSlot = -1;
        private float _sleepHours; // 저장 후 진행할 수면 시간
        private GUIStyle _titleStyle;
        private GUIStyle _slotLabelStyle;
        private GUIStyle _slotEmptyStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _confirmButtonStyle;
        private GUIStyle _cancelButtonStyle;
        private bool _stylesInitialized;
        private SaveData[] _slotInfos;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SaveSlotUI] 중복 인스턴스 파괴");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 저장 슬롯 선택 UI를 표시합니다.
        /// </summary>
        /// <param name="sleepHours">저장 후 진행할 수면 시간 (TimeManager.SleepFor에 전달)</param>
        public void Show(float sleepHours)
        {
            _sleepHours = sleepHours;
            _selectedSlot = -1;
            _isVisible = true;

            // 슬롯 정보 미리 로드
            if (SaveManager.Instance != null)
            {
                _slotInfos = SaveManager.Instance.GetAllSlotInfos();
            }
        }

        /// <summary>
        /// UI를 닫습니다.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _selectedSlot = -1;
            _slotInfos = null;
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };

            _slotLabelStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _textColor },
                padding = new RectOffset(10, 10, 4, 4)
            };

            _slotEmptyStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) }
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

            _confirmButtonStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };
            _confirmButtonStyle.hover.background = MakeTexture(1, 1, new Color(0.3f, 0.7f, 0.3f, 1f));
            _confirmButtonStyle.active.background = MakeTexture(1, 1, new Color(0.1f, 0.4f, 0.1f, 1f));

            _cancelButtonStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };
            _cancelButtonStyle.hover.background = MakeTexture(1, 1, new Color(0.8f, 0.3f, 0.3f, 1f));
            _cancelButtonStyle.active.background = MakeTexture(1, 1, new Color(0.5f, 0.1f, 0.1f, 1f));

            _stylesInitialized = true;
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        private string FormatSlotLabel(int index, SaveData info)
        {
            if (info == null)
                return $"슬롯 {index + 1} — 비어있음";
            return $"슬롯 {index + 1} — {info.timestamp} (Day {info.time?.day ?? 0}, Lv.{info.player?.level ?? 0})";
        }

        private void OnGUI()
        {
            InitializeStyles();

            if (!_isVisible) return;

            // 배경 딤 (화면 전체 반투명)
            var dimTex = MakeTexture(1, 1, new Color(0f, 0f, 0f, 0.5f));
            var dimStyle = new GUIStyle { normal = { background = dimTex } };
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", dimStyle);

            int centerX = (Screen.width - _windowWidth) / 2;
            int centerY = (Screen.height - _windowHeight) / 2;

            // 메인 박스 배경
            var bgTex = MakeTexture(1, 1, _bgColor);
            var bgStyle = new GUIStyle { normal = { background = bgTex } };
            GUI.Box(new Rect(centerX, centerY, _windowWidth, _windowHeight), "", bgStyle);

            // 제목
            GUI.Label(new Rect(centerX, centerY + 15, _windowWidth, 35), "저장 슬롯 선택", _titleStyle);

            // 슬롯 버튼 (5개)
            int slotStartY = centerY + 60;
            int slotX = centerX + 20;
            int slotWidth = _windowWidth - 40;

            for (int i = 0; i < 5; i++)
            {
                int slotY = slotStartY + (_slotButtonHeight + _buttonSpacing) * i;
                SaveData info = (_slotInfos != null && i < _slotInfos.Length) ? _slotInfos[i] : null;

                bool isSelected = (_selectedSlot == i);
                GUI.backgroundColor = isSelected ? _selectedSlotColor : (info != null ? _slotColor : _slotEmptyColor);

                string label = FormatSlotLabel(i, info);

                if (GUI.Button(new Rect(slotX, slotY, slotWidth, _slotButtonHeight), label, _slotLabelStyle))
                {
                    _selectedSlot = i;
                }
            }

            // 확인/취소 버튼 줄
            int buttonRowY = slotStartY + (_slotButtonHeight + _buttonSpacing) * 3 + 15;
            int buttonWidth = (_windowWidth - 60) / 2;

            // 확인 버튼
            GUI.backgroundColor = _confirmColor;
            bool canConfirm = (_selectedSlot >= 0);
            if (!canConfirm)
            {
                GUI.enabled = false;
            }
            if (GUI.Button(new Rect(slotX, buttonRowY, buttonWidth, _confirmButtonHeight), "확인", _confirmButtonStyle))
            {
                OnConfirm();
            }
            GUI.enabled = true;

            // 취소 버튼
            GUI.backgroundColor = _cancelColor;
            if (GUI.Button(new Rect(slotX + buttonWidth + 20, buttonRowY, buttonWidth, _confirmButtonHeight), "취소", _cancelButtonStyle))
            {
                Hide();
            }
        }

        private void OnConfirm()
        {
            if (_selectedSlot < 0) return;

            // 저장 후 수면 실행
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save(_selectedSlot);
            }

            Hide();

            // 수면 시작 (SleepUI에서 받은 sleepHours 전달)
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.SleepFor(_sleepHours, OnWakeUpComplete);
            }
        }

        private void OnWakeUpComplete()
        {
            Debug.Log("[SaveSlotUI] 기상 완료!");
        }
    }
}