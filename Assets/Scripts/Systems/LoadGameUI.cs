using System.Collections;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C17-02: 저장 파일 선택 (불러오기) UI.
    /// C17-03: 슬롯 선택 시 SaveManager.Load() + LoadingManager.LoadScene() 호출.
    /// C17-04: 빈 슬롯은 "비어있음" 표시, 클릭 불가.
    /// IMGUI 기반으로 SaveManager의 3개 슬롯(0, 1, 2) 정보를 표시합니다.
    /// </summary>
    public class LoadGameUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private int _windowWidth = 450;
        [SerializeField] private int _windowHeight = 500;
        [SerializeField] private int _slotButtonHeight = 70;
        [SerializeField] private int _buttonSpacing = 10;
        [SerializeField] private int _backButtonHeight = 40;

        [Header("Colors")]
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.88f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _slotColor = new Color(0.2f, 0.35f, 0.5f, 0.9f);
        [SerializeField] private Color _slotEmptyColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);
        [SerializeField] private Color _slotHoverColor = new Color(0.3f, 0.5f, 0.7f, 1f);
        [SerializeField] private Color _backColor = new Color(0.6f, 0.2f, 0.2f, 0.9f);

        // ===== 상태 =====
        private MainMenuUI _mainMenu;
        private SaveData[] _slotInfos;
        private string _emptySlotMessage = "";
        private float _emptySlotMessageTimer;
        private bool _isLoadingInProgress;

        private GUIStyle _titleStyle;
        private GUIStyle _slotLabelStyle;
        private GUIStyle _slotDetailStyle;
        private GUIStyle _slotEmptyStyle;
        private GUIStyle _slotButtonStyle;
        private GUIStyle _backButtonStyle;
        private GUIStyle _messageStyle;
        private bool _stylesInitialized;

        private const int SLOT_COUNT = 5;

        /// <summary>
        /// 메인 메뉴 UI 참조를 설정합니다.
        /// </summary>
        public void SetMainMenuUI(MainMenuUI mainMenu)
        {
            _mainMenu = mainMenu;
        }

        /// <summary>
        /// 슬롯 정보를 새로고침합니다.
        /// </summary>
        public void RefreshSlots()
        {
            if (SaveManager.Instance != null)
            {
                _slotInfos = SaveManager.Instance.GetAllSlotInfos();
            }
            _isLoadingInProgress = false;
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

            _slotLabelStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _textColor },
                padding = new RectOffset(12, 10, 2, 2)
            };

            _slotDetailStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f, 1f) },
                padding = new RectOffset(12, 10, 0, 2)
            };

            _slotEmptyStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f, 1f) }
            };

            _slotButtonStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };
            _slotButtonStyle.hover.background = MakeTexture(1, 1, _slotHoverColor);
            _slotButtonStyle.active.background = MakeTexture(1, 1, new Color(0.1f, 0.2f, 0.5f, 1f));

            _backButtonStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };
            _backButtonStyle.hover.background = MakeTexture(1, 1, new Color(0.8f, 0.3f, 0.3f, 1f));
            _backButtonStyle.active.background = MakeTexture(1, 1, new Color(0.5f, 0.1f, 0.1f, 1f));

            _messageStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.7f, 0.3f, 1f) }
            };

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

        private void Update()
        {
            if (_emptySlotMessageTimer > 0f)
            {
                _emptySlotMessageTimer -= Time.deltaTime;
                if (_emptySlotMessageTimer <= 0f)
                    _emptySlotMessage = "";
            }
        }

        private void OnGUI()
        {
            InitializeStyles();

            if (_mainMenu == null) return;

            // 배경 딤
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
            GUI.Label(new Rect(centerX, centerY + 15, _windowWidth, 35), "저장 파일 불러오기", _titleStyle);

            // 슬롯 버튼 (3개)
            int slotStartY = centerY + 60;
            int slotX = centerX + 20;
            int slotWidth = _windowWidth - 40;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                int slotY = slotStartY + (_slotButtonHeight + _buttonSpacing) * i;
                SaveData info = (_slotInfos != null && i < _slotInfos.Length) ? _slotInfos[i] : null;
                bool hasSave = info != null;

                if (hasSave)
                {
                    // 채워진 슬롯: 타임스탬프, Day, Level 정보 표시
                    GUI.backgroundColor = _slotColor;

                    Rect slotRect = new Rect(slotX, slotY, slotWidth, _slotButtonHeight);

                    var slotBgTex = MakeTexture(1, 1, _slotColor);
                    var slotBgStyle = new GUIStyle { normal = { background = slotBgTex } };
                    GUI.Box(slotRect, "", slotBgStyle);

                    // 슬롯 번호 + 타임스탬프 (상단)
                    string header = $"슬롯 {i + 1}  —  {info.timestamp ?? "날짜 없음"}";
                    GUI.Label(new Rect(slotX + 10, slotY + 8, slotWidth - 20, 24), header, _slotLabelStyle);

                    // Day + Level 정보 (하단)
                    string dayStr = info.time != null ? $"Day {info.time.day}" : "Day ?";
                    string levelStr = info.player != null ? $"Lv.{info.player.level}" : "Lv.?";
                    string details = $"{dayStr}  |  {levelStr}";
                    GUI.Label(new Rect(slotX + 10, slotY + 34, slotWidth - 20, 22), details, _slotDetailStyle);

                    // 클릭 가능한 버튼 오버레이
                    GUI.backgroundColor = Color.clear;
                    if (!_isLoadingInProgress && GUI.Button(slotRect, "", _slotButtonStyle))
                    {
                        OnSlotClicked(i);
                    }

                    // 로딩 중이면 클릭 방지
                    if (_isLoadingInProgress)
                    {
                        GUI.enabled = false;
                        GUI.Button(slotRect, "");
                        GUI.enabled = true;
                    }
                }
                else
                {
                    // 빈 슬롯: "비어있음" 표시, 회색
                    GUI.backgroundColor = _slotEmptyColor;
                    Rect slotRect = new Rect(slotX, slotY, slotWidth, _slotButtonHeight);

                    var emptyBgTex = MakeTexture(1, 1, _slotEmptyColor);
                    var emptyBgStyle = new GUIStyle { normal = { background = emptyBgTex } };
                    GUI.Box(slotRect, "", emptyBgStyle);

                    // "비어있음" 텍스트
                    string slotLabel = $"슬롯 {i + 1}";
                    GUI.Label(new Rect(slotX, slotY + 8, slotWidth, 24), slotLabel, _slotLabelStyle);
                    GUI.Label(new Rect(slotX, slotY + 34, slotWidth, 22), "비어있음", _slotEmptyStyle);
                }
            }

            // 빈 슬롯 클릭 메시지
            if (!string.IsNullOrEmpty(_emptySlotMessage))
            {
                int msgY = slotStartY + (_slotButtonHeight + _buttonSpacing) * 3 + 5;
                GUI.Label(new Rect(centerX, msgY, _windowWidth, 30), _emptySlotMessage, _messageStyle);
            }

            // 뒤로 가기 버튼
            int backButtonY = slotStartY + (_slotButtonHeight + _buttonSpacing) * 3 + 35;
            int backButtonWidth = 120;
            int backButtonX = centerX + (_windowWidth - backButtonWidth) / 2;

            GUI.backgroundColor = _backColor;
            if (!_isLoadingInProgress && GUI.Button(new Rect(backButtonX, backButtonY, backButtonWidth, _backButtonHeight), "← 뒤로", _backButtonStyle))
            {
                OnBackClicked();
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            if (_isLoadingInProgress) return;

            SaveData info = (_slotInfos != null && slotIndex < _slotInfos.Length) ? _slotInfos[slotIndex] : null;

            if (info == null)
            {
                // 빈 슬롯 클릭 → 메시지 표시
                _emptySlotMessage = "저장된 게임이 없습니다";
                _emptySlotMessageTimer = 2.0f;
                return;
            }

            // 저장된 게임 로드 (C17-03)
            Debug.Log($"[LoadGameUI] 슬롯 {slotIndex} 불러오기 시작...");
            _isLoadingInProgress = true;

            // SaveManager.Load()로 게임 상태 복원
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Load(slotIndex);
            }

            // LoadingManager를 통해 MainScene 로드
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.LoadSceneAsync("MainScene");
            }
            else
            {
                Debug.LogError("[LoadGameUI] LoadingManager.Instance가 null입니다.");
                _isLoadingInProgress = false;
            }
        }

        private void OnBackClicked()
        {
            Debug.Log("[LoadGameUI] 메인 메뉴로 돌아가기");
            if (_mainMenu != null)
            {
                _mainMenu.SetLoadUIActive(false);
                _mainMenu.Show();
            }
            gameObject.SetActive(false);
        }
    }
}