using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C17-02: 저장 파일 선택 (불러오기) UI.
    /// C17-03: 슬롯 선택 시 SaveManager.Load() + LoadingManager.LoadScene() 호출.
    /// C17-04: 빈 슬롯은 "비어있음" 표시, 클릭 불가.
    /// IMGUI 기반으로 SaveManager의 5개 슬롯(0~4) 정보를 표시합니다.
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
        private GUIStyle _dimBgStyle;
        private GUIStyle _windowBgStyle;
        private GUIStyle _slotBgStyle;
        private GUIStyle _emptyBgStyle;
        private bool _stylesInitialized;

        // 캐시된 텍스처 (메모리 누수 방지용)
        private Texture2D _cachedDimTex;
        private Texture2D _cachedBgTex;
        private Texture2D _cachedSlotTex;
        private Texture2D _cachedEmptyTex;

        private int _slotCount;

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
                _slotCount = SaveManager.Instance.SlotCount;
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

            // OnGUI에서 매 프레임 생성하지 않도록 스타일/텍스처를 미리 캐싱
            _cachedDimTex = MakeTexture(1, 1, new Color(0f, 0f, 0f, 0.5f));
            _dimBgStyle = new GUIStyle { normal = { background = _cachedDimTex } };

            _cachedBgTex = MakeTexture(1, 1, _bgColor);
            _windowBgStyle = new GUIStyle { normal = { background = _cachedBgTex } };

            _cachedSlotTex = MakeTexture(1, 1, _slotColor);
            _slotBgStyle = new GUIStyle { normal = { background = _cachedSlotTex } };

            _cachedEmptyTex = MakeTexture(1, 1, _slotEmptyColor);
            _emptyBgStyle = new GUIStyle { normal = { background = _cachedEmptyTex } };

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

        /// <summary>
        /// 생성된 캐시 텍스처를 정리합니다.
        /// </summary>
        private void CleanupCachedTextures()
        {
            if (_cachedDimTex != null) Destroy(_cachedDimTex);
            if (_cachedBgTex != null) Destroy(_cachedBgTex);
            if (_cachedSlotTex != null) Destroy(_cachedSlotTex);
            if (_cachedEmptyTex != null) Destroy(_cachedEmptyTex);
            _cachedDimTex = null;
            _cachedBgTex = null;
            _cachedSlotTex = null;
            _cachedEmptyTex = null;
        }

        private void OnDestroy()
        {
            CleanupCachedTextures();
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

            // 배경 딤 (캐시된 텍스처 사용)
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _dimBgStyle);

            int centerX = (Screen.width - _windowWidth) / 2;
            int centerY = (Screen.height - _windowHeight) / 2;

            // 메인 박스 배경 (캐시된 텍스처 사용)
            GUI.Box(new Rect(centerX, centerY, _windowWidth, _windowHeight), "", _windowBgStyle);

            // 제목
            GUI.Label(new Rect(centerX, centerY + 15, _windowWidth, 35), "저장 파일 불러오기", _titleStyle);

            // 슬롯 버튼
            int slotStartY = centerY + 60;
            int slotX = centerX + 20;
            int slotWidth = _windowWidth - 40;
            int displaySlotCount = (_slotCount > 0) ? _slotCount : SLOT_COUNT_DEFAULT;

            for (int i = 0; i < displaySlotCount; i++)
            {
                int slotY = slotStartY + (_slotButtonHeight + _buttonSpacing) * i;
                SaveData info = (_slotInfos != null && i < _slotInfos.Length) ? _slotInfos[i] : null;
                bool hasSave = info != null;

                Rect slotRect = new Rect(slotX, slotY, slotWidth, _slotButtonHeight);

                if (hasSave)
                {
                    // 채워진 슬롯: 타임스탬프, Day, Level 정보 표시 (캐시된 텍스처 사용)
                    GUI.Box(slotRect, "", _slotBgStyle);

                    // 슬롯 번호 + 타임스탬프 (상단)
                    string header = $"슬롯 {i + 1}  —  {info.timestamp ?? "날짜 없음"}";
                    GUI.Label(new Rect(slotX + 10, slotY + 8, slotWidth - 20, 24), header, _slotLabelStyle);

                    // Day + Level 정보 (하단)
                    string dayStr = info.time != null ? $"Day {info.time.day}" : "Day ?";
                    string levelStr = info.player != null ? $"Lv.{info.player.level}" : "Lv.?";
                    string details = $"{dayStr}  |  {levelStr}";
                    GUI.Label(new Rect(slotX + 10, slotY + 34, slotWidth - 20, 22), details, _slotDetailStyle);

                    // 클릭 가능한 투명 버튼 오버레이
                    if (!_isLoadingInProgress)
                    {
                        if (GUI.Button(slotRect, "", _slotButtonStyle))
                        {
                            OnSlotClicked(i);
                        }
                    }
                }
                else
                {
                    // 빈 슬롯: "비어있음" 표시, 회색 (캐시된 텍스처 사용)
                    GUI.Box(slotRect, "", _emptyBgStyle);

                    // "비어있음" 텍스트
                    string slotLabel = $"슬롯 {i + 1}";
                    GUI.Label(new Rect(slotX, slotY + 8, slotWidth, 24), slotLabel, _slotLabelStyle);
                    GUI.Label(new Rect(slotX, slotY + 34, slotWidth, 22), "비어있음", _slotEmptyStyle);
                }
            }

            // 빈 슬롯 클릭 메시지
            if (!string.IsNullOrEmpty(_emptySlotMessage))
            {
                int msgY = slotStartY + (_slotButtonHeight + _buttonSpacing) * displaySlotCount + 5;
                GUI.Label(new Rect(centerX, msgY, _windowWidth, 30), _emptySlotMessage, _messageStyle);
            }

            // 뒤로 가기 버튼
            int backButtonY = slotStartY + (_slotButtonHeight + _buttonSpacing) * displaySlotCount + 35;
            int backButtonWidth = 120;
            int backButtonX = centerX + (_windowWidth - backButtonWidth) / 2;

            if (!_isLoadingInProgress)
            {
                GUI.backgroundColor = _backColor;
                if (GUI.Button(new Rect(backButtonX, backButtonY, backButtonWidth, _backButtonHeight), "← 뒤로", _backButtonStyle))
                {
                    OnBackClicked();
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            if (_isLoadingInProgress) return;

            SaveData info = (_slotInfos != null && slotIndex < _slotInfos.Length) ? _slotInfos[slotIndex] : null;

            if (info == null)
            {
                // 빈 슬롯 클릭 → 메시지 표시 (데이터 동기화 지연 등으로 info가 null인 경우)
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

        // 상수: 기본 슬롯 개수 (SaveManager.SlotCount를 우선 사용)
        private const int SLOT_COUNT_DEFAULT = 5;
    }
}