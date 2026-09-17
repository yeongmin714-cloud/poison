using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round A — 문서 읽기 윈도우 (UTK).
    /// 원본: Assets/Scripts/UI/ReadDocumentWindow.cs (260줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 문서 표시 — ReadableDocument.Title / Content / LocationDescription / Category / Importance 실측 접근.
    ///  ② 스크롤 — 본문을 ScrollView에 표시.
    ///  ③ 닫기 — UTKWindowBase 닫기 버튼 + "닫기 ✕" 버튼.
    ///  ④ 연동 — Ensure() 시 InteractableDocument.OnDocumentReadRequested 이벤트 브리지 구독.
    ///  각 경로에 [ReadDocUTK] UnityEngine.Debug 로그.
    /// [진입점] static Open(ReadableDocument) / Ensure() / Toggle(). 순수 VisualElement 트리.
    /// </summary>
    public class ReadDocumentUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static ReadDocumentUTK _instance;
        public static ReadDocumentUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new ReadDocumentUTK();
            InteractableDocument.OnDocumentReadRequested += _instance.ShowDocument;
        }

        /// <summary>문서 창 열기 (원본 ReadDocumentWindow.ShowDocument 대응).</summary>
        public static void Open(ReadableDocument document)
        {
            Ensure();
            _instance.ShowDocument(document);
        }

        /// <summary>문서 창 닫기 (원본 CloseDocument 대응).</summary>
        public static void CloseDocument()
        {
            if (_instance != null)
                _instance.HideNow();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
        }

        // ===== 설정 =====
        private const float WinW = 640f;
        private const float WinH = 540f;

        // ===== 상태 =====
        private ReadableDocument _currentDocument;

        // ===== 레퍼런스 =====
        private Label _titleLabel;
        private Label _categoryLabel;
        private Label _locationLabel;
        private ScrollView _scrollView;

        private ReadDocumentUTK() : base("📜 문서", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            // 제목
            _titleLabel = new Label("📜 문서");
            _titleLabel.AddToClassList("utk-title-label");
            _titleLabel.style.fontSize = 22f;
            _titleLabel.style.marginBottom = 6f;
            _content.Add(_titleLabel);

            // 분류 + 중요도
            _categoryLabel = new Label("");
            _categoryLabel.style.fontSize = 14f;
            _categoryLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _categoryLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _content.Add(_categoryLabel);

            // 발견 위치
            _locationLabel = new Label("");
            _locationLabel.style.fontSize = 12f;
            _locationLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _locationLabel.style.marginTop = 4f;
            _locationLabel.style.marginBottom = 8f;
            _content.Add(_locationLabel);

            // 본문 (스크롤)
            _scrollView = new ScrollView();
            _scrollView.name = "DocumentScroll";
            _scrollView.style.flexGrow = 1f;
            _content.Add(_scrollView);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 640f;
            style.top = 120f;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 640f;
            style.top = 120f;
        }

        public override void Hide()
        {
            base.Hide();
            Debug.Log("[ReadDocUTK] 문서 창 닫힘");
        }

        private void HideNow()
        {
            _currentDocument = null;
            Close();
        }

        // ===== 진입점 =====

        private void ShowDocument(ReadableDocument document)
        {
            if (document == null) return;

            _currentDocument = document;

            string category = GetCategoryDisplayName(document.Category);
            string importance = GetImportanceDisplayName(document.Importance);

            _titleLabel.text = document.Title;
            _categoryLabel.text = category + "  |  " + importance;
            _locationLabel.text = string.IsNullOrEmpty(document.LocationDescription)
                ? ""
                : ("발견 위치: " + document.LocationDescription);
            _locationLabel.style.display = string.IsNullOrEmpty(document.LocationDescription)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            _scrollView.Clear();
            var body = new Label(document.Content);
            body.AddToClassList("utk-content-label");
            body.style.fontSize = 16f;
            body.style.color = new StyleColor(UTKColor.TextPrimary);
            body.style.whiteSpace = WhiteSpace.Normal;
            _scrollView.Add(body);

            Debug.Log("[ReadDocUTK] 문서 표시: " + document.Title);
            Show();
        }

        // ===== 헬퍼 =====

        private static string GetCategoryDisplayName(ReadableDocument.DocumentCategory category)
        {
            switch (category)
            {
                case ReadableDocument.DocumentCategory.Letter: return "✉ 편지";
                case ReadableDocument.DocumentCategory.Diary: return "📖 일기";
                case ReadableDocument.DocumentCategory.OfficialDoc: return "📜 공문";
                case ReadableDocument.DocumentCategory.Scroll: return "📜 스크롤";
                case ReadableDocument.DocumentCategory.Wanted: return "⚠ 현상수배";
                default: return "문서";
            }
        }

        private static string GetImportanceDisplayName(ReadableDocument.DocumentImportance importance)
        {
            switch (importance)
            {
                case ReadableDocument.DocumentImportance.Normal: return "일반 문서";
                case ReadableDocument.DocumentImportance.Important: return "★ 중요 문서";
                case ReadableDocument.DocumentImportance.QuestRequired: return "◆ 퀘스트 문서";
                default: return "";
            }
        }
    }
}