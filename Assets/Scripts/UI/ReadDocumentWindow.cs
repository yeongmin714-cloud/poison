using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 37-01: 문서 읽기 IMGUI 팝업 윈도우.
    /// InteractableDocument에서 호출하여 문서 내용을 표시합니다.
    /// UIWindow 베이스 클래스를 상속받아 Show/Hide 기능을 제공합니다.
    /// </summary>
    public class ReadDocumentWindow : UIWindow
    {
        public static ReadDocumentWindow Instance { get; private set; }

        [Header("문서 읽기 설정")]
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;
        [SerializeField] private float _windowWidth = 600f;
        [SerializeField] private float _windowHeight = 500f;
        [SerializeField] private float _contentPadding = 20f;

        [Header("스타일")]
        [SerializeField] private Color _titleColor = new Color(0.9f, 0.7f, 0.2f);
        [SerializeField] private Color _categoryColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] private Color _contentColor = Color.white;
        [SerializeField] private Color _locationColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Texture2D _backgroundTexture;

        // ===== 상태 =====
        private ReadableDocument _currentDocument;
        private Vector2 _scrollPosition = Vector2.zero;
        private bool _isDocumentOpen;

        // 캐싱된 스타일 (OnGUI에서 매번 생성 방지)
        private GUIStyle _titleStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _contentStyle;
        private GUIStyle _locationStyle;
        private GUIStyle _closeButtonStyle;
        private bool _stylesInitialized;

        // ================================================================
        // Unity 생명주기
        // ================================================================

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Systems → UI 이벤트 브리지 구독
            InteractableDocument.OnDocumentReadRequested += ShowDocument;
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                InteractableDocument.OnDocumentReadRequested -= ShowDocument;
                Instance = null;
            }
        }

        protected override void OnGUI()
        {
            if (!_isDocumentOpen || _currentDocument == null) return;

            InitializeStyles();

            // 배경 딤드
            GUI.depth = 0;
            Rect fullScreenRect = new Rect(0, 0, Screen.width, Screen.height);
            GUI.Box(fullScreenRect, ""); // 빈 박스로 딤드 효과 (투명도는 s_guiStyle로 제어)

            // ESC 닫기
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == _closeKey)
            {
                CloseDocument();
                Event.current.Use();
                return;
            }

            // 윈도우 영역
            float centerX = (Screen.width - _windowWidth) * 0.5f;
            float centerY = (Screen.height - _windowHeight) * 0.5f;
            Rect windowRect = new Rect(centerX, centerY, _windowWidth, _windowHeight);

            // 배경
            if (_backgroundTexture != null)
                GUI.DrawTexture(windowRect, _backgroundTexture, ScaleMode.StretchToFill);
            else
                GUI.Box(windowRect, "");

            // 내부 패딩
            Rect contentRect = new Rect(
                windowRect.x + _contentPadding,
                windowRect.y + _contentPadding,
                windowRect.width - _contentPadding * 2,
                windowRect.height - _contentPadding * 2
            );

            // 제목
            Rect titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 40);
            GUI.Label(titleRect, _currentDocument.Title, _titleStyle);

            // 분류 + 중요도
            string categoryText = GetCategoryDisplayName(_currentDocument.Category);
            string importanceText = GetImportanceDisplayName(_currentDocument.Importance);
            Rect categoryRect = new Rect(contentRect.x, titleRect.y + 45, contentRect.width, 20);
            GUI.Label(categoryRect, $"{categoryText}  |  {importanceText}", _categoryStyle);

            // 발견 위치
            if (!string.IsNullOrEmpty(_currentDocument.LocationDescription))
            {
                Rect locationRect = new Rect(contentRect.x, categoryRect.y + 22, contentRect.width, 20);
                GUI.Label(locationRect, $"발견 위치: {_currentDocument.LocationDescription}", _locationStyle);
            }

            // 내용 (스크롤뷰)
            float contentYStart = categoryRect.y + 50;
            float contentAreaHeight = contentRect.height - contentYStart + contentRect.y - 60;
            Rect scrollViewRect = new Rect(contentRect.x, contentYStart, contentRect.width, contentAreaHeight);
            Rect viewRect = new Rect(0, 0, scrollViewRect.width - 20, 1000); // 가변 높이 (추후 자동 계산)

            GUI.BeginScrollView(scrollViewRect, _scrollPosition, viewRect);
            GUI.Label(viewRect, _currentDocument.Content, _contentStyle);
            GUI.EndScrollView();

            // 닫기 버튼 (우측 상단)
            float closeBtnSize = 30f;
            Rect closeBtnRect = new Rect(
                windowRect.x + windowRect.width - closeBtnSize - 10,
                windowRect.y + 10,
                closeBtnSize,
                closeBtnSize
            );

            if (GUI.Button(closeBtnRect, "X", _closeButtonStyle))
            {
                CloseDocument();
            }
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 문서를 표시합니다. 외부(InteractableDocument 등)에서 호출.
        /// </summary>
        public void ShowDocument(ReadableDocument document)
        {
            if (document == null) return;

            _currentDocument = document;
            _scrollPosition = Vector2.zero;
            _isDocumentOpen = true;

            // UIWindow.Open() 호출 (부모 클래스)
            Open();


        }

        /// <summary>
        /// 문서 창을 닫습니다.
        /// </summary>
        public void CloseDocument()
        {
            _isDocumentOpen = false;
            _currentDocument = null;
            Hide();


        }

        /// <summary>
        /// 현재 열려있는지 여부
        /// </summary>
        public new bool IsOpen => _isDocumentOpen;

        // ================================================================
        // 내부 메서드
        // ================================================================

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _titleColor }
            };

            _categoryStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _categoryColor }
            };

            _contentStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = _contentColor },
                richText = true
            };

            _locationStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _locationColor }
            };

            _closeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = Color.red }
            };
        }

        private string GetCategoryDisplayName(ReadableDocument.DocumentCategory category)
        {
            return category switch
            {
                ReadableDocument.DocumentCategory.Letter => "✉ 편지",
                ReadableDocument.DocumentCategory.Diary => "📖 일기",
                ReadableDocument.DocumentCategory.OfficialDoc => "📜 공문",
                ReadableDocument.DocumentCategory.Scroll => "📜 스크롤",
                ReadableDocument.DocumentCategory.Wanted => "⚠ 현상수배",
                _ => "문서"
            };
        }

        private string GetImportanceDisplayName(ReadableDocument.DocumentImportance importance)
        {
            return importance switch
            {
                ReadableDocument.DocumentImportance.Normal => "일반 문서",
                ReadableDocument.DocumentImportance.Important => "★ 중요 문서",
                ReadableDocument.DocumentImportance.QuestRequired => "◆ 퀘스트 문서",
                _ => ""
            };
        }
    }
}