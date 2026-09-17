using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.UI;   // UIFont 공용 폰트

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U0 — UTK 윈도우 베이스 (VisualElement 래퍼).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 구조:
    ///   .utk-window (bg-panel + 브론즈 2px)
    ///     ├─ .utk-title-bar (제목 Label + 닫기 Button) — 드래그 이동
    ///     └─ .utk-content (ScrollView/새 ScrollViewport Optional)
    ///
    /// 기능: Show/Hide/Toggle/Close, IsOpen, ESC 시 UTKWindowManager 경유 자동 등록/해제,
    ///       타이틀바 PointerManipulator 드래그, UIFont 공용 폰트 헬퍼.
    /// </summary>
    public class UTKWindowBase : VisualElement
    {
        /// <summary>닫기 버튼 클릭 시 호출 (기본: Close() 재정의용 훅).</summary>
        public System.Action OnCloseRequested;

        protected readonly VisualElement _content;
        protected readonly Label _titleLabel;
        private readonly Button _closeButton;
        private readonly VisualElement _titleBar;   // 드래그 대상
        private bool _isOpen;
        private bool _dragging;

        public bool IsOpen => _isOpen;
        public string Title => _titleLabel.text;

        /// <summary>윈도우 생성. size = 목표 유닛 크기 (예: 400x300).</summary>
        public UTKWindowBase(string title, Vector2Int size) : this(title, new Vector2(size.x, size.y))
        {
        }

        /// <summary>윈도우 생성. size = 목표 유닛 크기 (예: (400,300)).</summary>
        public UTKWindowBase(string title, Vector2 size)
        {
            name = "UTKWindow_" + title;
            AddToClassList("utk-window");
            style.width = size.x;
            style.height = size.y;

            // ── 타이틀 바 ──
            var titleBar = new VisualElement();
            titleBar.AddToClassList("utk-title-bar");
            titleBar.name = "TitleBar";

            _titleLabel = new Label(title ?? "");
            _titleLabel.AddToClassList("utk-title-label");

            _closeButton = new Button(OnCloseClicked) { text = "✕" };
            _closeButton.AddToClassList("utk-close-btn");
            _closeButton.name = "CloseButton";

            titleBar.Add(_titleLabel);
            titleBar.Add(_closeButton);
            Add(titleBar);

            // ── 컨텐츠 영역 ──
            _content = new VisualElement();
            _content.AddToClassList("utk-content");
            _content.name = "Content";
            Add(_content);

            _titleBar = titleBar;

            // 타이틀바 드래그 이동 (PointerManipulator 계열)
            _titleBar.RegisterCallback<PointerDownEvent>(OnTitleBarPointerDown);
            _titleBar.RegisterCallback<PointerMoveEvent>(OnTitleBarPointerMove);
            _titleBar.RegisterCallback<PointerUpEvent>(OnTitleBarPointerUp);
            _titleBar.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);

            ApplyUIToolkitFont(this);

            // 기본 숨김
            _isOpen = false;
            style.display = DisplayStyle.None;
        }

        // ─────────────────────────── 공개 API ───────────────────────────

        /// <summary>열기 (등록 + 표시 + OnShown).</summary>
        public virtual void Show()
        {
            if (_isOpen) return;
            _isOpen = true;
            style.display = DisplayStyle.Flex;
            BringToFront();
            UTKWindowManager.Register(this);
            OnWindowOpen();
        }

        /// <summary>닫기 (표시 제거 + 해제 + OnWindowClosed).</summary>
        public virtual void Hide()
        {
            if (!_isOpen) return;
            _isOpen = false;
            style.display = DisplayStyle.None;
            UTKWindowManager.Unregister(this);
            OnWindowClosed();
        }

        /// <summary>열려있으면 닫고, 닫혀있으면 염.</summary>
        public virtual void Toggle()
        {
            if (_isOpen) Hide(); else Show();
        }

        /// <summary>Hide()와 동일 — ESC 처리 경유 (스택 최상단 제거).</summary>
        public virtual void Close() => Hide();

        /// <summary>컨텐츠 영역 접근자 — 하위 컨트롤 추가용.</summary>
        public VisualElement Content => _content;

        // ─────────────────────────── 훅 (오버라이드) ───────────────────────────

        /// <summary>Subclass에서 필요 시 오버라이드.</summary>
        protected virtual void OnWindowOpen() { }

        /// <summary>Subclass에서 필요 시 오버라이드.</summary>
        protected virtual void OnWindowClosed() { }

        // ─────────────────────────── 드래그 (PointerManipulator 구현) ───────────────────────────

        private void OnTitleBarPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            _dragging = true;
            _titleBar.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnTitleBarPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging) return;
            // 이벤트마다 현재 위치 기반 델타 이동 (캡처 상태에서도 이벤트 수신).
            // PointerMoveEvent.deltaPosition 사용 — 픽셀 단위.
            style.left = style.left.value.value + evt.deltaPosition.x;
            style.top  = style.top.value.value + evt.deltaPosition.y;
            evt.StopPropagation();
        }

        private void OnTitleBarPointerUp(PointerUpEvent evt)
        {
            if (!_dragging) return;
            _dragging = false;
            _titleBar.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnCloseClicked()
        {
            OnCloseRequested?.Invoke();
            Close();
        }

        // ─────────────────────────── 폰트 헬퍼 ───────────────────────────

        /// <summary>
        /// UIFont 공용 폰트(한글 서포트)를 지정된 VisualElement 및 하위 TextElement에 적용.
        /// Theme.uss의 폰트 크기 CSS 변수와 함께 사용. 실제 폰트 에셋 할당은 코드로 수행.
        /// </summary>
        public static void ApplyUIToolkitFont(VisualElement ve)
        {
            if (ve == null) return;
            var uiFont = UIFont.Load();
            if (uiFont == null) return;

            ve.style.unityFont = uiFont;
            foreach (var child in ve.Children())
                ApplyUIToolkitFont(child);
        }
    }
}