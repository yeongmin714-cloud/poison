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
        private readonly VisualElement _titleBar;   // 표준 크롬 타이틀바
        private VisualElement _dragTarget;
        private readonly VisualElement _shadow;     // 9슬라이스 소프트 그림자 (부모에 형제로 부착)
        private bool _isOpen;
        private bool _dragging;

        /// <summary>섀도우 음각(글로우) 확장 — 창보다 커지는 폭 (px).</summary>
        private const float ShadowPad = 12f;
        /// <summary>떠 보이도록 아래로 살짝 처지는 오프셋 (px).</summary>
        private const float ShadowDropY = 4f;

        public bool IsOpen => _isOpen;
        public string Title => _titleLabel.text;
        /// <summary>Current presentation mode; Standard is the backward-compatible default.</summary>
        public UTKWindowChrome Chrome { get; private set; }
        /// <summary>True when the standard title bar and window border are suppressed.</summary>
        public bool IsFrameless => Chrome == UTKWindowChrome.Frameless;

        /// <summary>윈도우 생성. size = 목표 유닛 크기 (예: 400x300).</summary>
        public UTKWindowBase(string title, Vector2Int size) : this(title, new Vector2(size.x, size.y))
        {
        }

        /// <summary>Opt-in chrome overload; existing constructors retain standard chrome.</summary>
        public UTKWindowBase(string title, Vector2Int size, UTKWindowChrome chrome)
            : this(title, new Vector2(size.x, size.y), chrome)
        {
        }

        /// <summary>윈도우 생성. size = 목표 유닛 크기 (예: (400,300)).</summary>
        public UTKWindowBase(string title, Vector2 size) : this(title, size, UTKWindowChrome.Standard)
        {
        }

        /// <summary>윈도우 생성 with optional standard or frameless chrome.</summary>
        public UTKWindowBase(string title, Vector2 size, UTKWindowChrome chrome)
        {
            Chrome = chrome;
            name = "UTKWindow_" + title;
            AddToClassList("utk-window");
            style.width = size.x;
            style.height = size.y;

            // Keep the standard title controls in the tree for API/USS compatibility;
            // frameless windows hide the entire bar and can supply a custom drag handle.
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

            _content = new VisualElement();
            _content.AddToClassList("utk-content");
            _content.name = "Content";
            Add(_content);

            _titleBar = titleBar;
            _dragTarget = chrome == UTKWindowChrome.Frameless ? _content : titleBar;
            if (chrome == UTKWindowChrome.Frameless)
            {
                titleBar.style.display = DisplayStyle.None;
                AddToClassList("utk-window-frameless");
                style.backgroundColor = new StyleColor(UTKTheme.Panel);
                style.backgroundImage = StyleKeyword.Null;
                style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
                style.borderTopLeftRadius = style.borderTopRightRadius = 0f;
                style.borderBottomLeftRadius = style.borderBottomRightRadius = 0f;
                _content.style.paddingLeft = _content.style.paddingRight = 0f;
                _content.style.paddingTop = _content.style.paddingBottom = 0f;
            }

            RegisterDragCallbacks(_dragTarget);
            ApplyChrome();
            // Separate windows are absolute-positioned by the caller/layout API.
            style.position = Position.Absolute;
            style.left = StyleKeyword.Auto;
            style.top = StyleKeyword.Auto;
            style.right = StyleKeyword.Auto;
            style.bottom = StyleKeyword.Auto;

            // ── 소프트 그림자 (9슬라이스 글로우) ──
            // 윈도우 자체의 자식이 아닌 "형제"로 부모에 부착해야 창의 overflow:hidden에 안 잘린다.
            _shadow = new VisualElement { name = "WindowShadow" };
            _shadow.AddToClassList("utk-window-shadow");
            _shadow.pickingMode = PickingMode.Ignore;   // 드래그/클릭/드롭타겟 간섭 방지
            _shadow.style.display = DisplayStyle.None;  // 부착 전엔 숨김
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            // 창 크기/위치 변동 시 섀도우 미러링 (크기 조절·레이아웃 재계산 포함)
            RegisterCallback<GeometryChangedEvent>(_ => SyncShadow());

            ApplyUIToolkitFont(this);

            // 기본 숨김
            _isOpen = false;
            style.display = DisplayStyle.None;
        }

        // ─────────────────────────── 섀도우 형제 관리 ───────────────────────────

        /// <summary>패널에 부착되면 섀도우를 같은 부모의 맨 뒤(인덱스 0)로 얹는다.</summary>
        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            var p = parent;
            if (p == null || _shadow.parent == p)
                return;
            // 창은 Show()에서 BringToFront() 하므로 섀도우가 항상 뒤에 남는다.
            p.Insert(0, _shadow);
            SyncShadow();
        }

        /// <summary>패널에서 분리되면 섀도우도 함께 제거해 화면에 남지 않게 한다.</summary>
        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            if (_shadow.parent != null)
                _shadow.RemoveFromHierarchy();
        }

        /// <summary>섀도우 표시/위치/크기를 창 resolvedStyle 기준으로 맞춘다.</summary>
        private void SyncShadow()
        {
            if (_shadow.parent == null)
                return;
            if (style.display == DisplayStyle.None)
            {
                // 닫힘/숨김 시 화면에 그림자가 남지 않도록 반드시 숨김
                _shadow.style.display = DisplayStyle.None;
                return;
            }
            _shadow.style.display = DisplayStyle.Flex;
            _shadow.style.left   = resolvedStyle.left   - ShadowPad;
            _shadow.style.top    = resolvedStyle.top    - ShadowPad + ShadowDropY;
            _shadow.style.width  = resolvedStyle.width  + ShadowPad * 2;
            _shadow.style.height = resolvedStyle.height + ShadowPad * 2 - ShadowDropY;
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
            SyncShadow();
            OnWindowOpen();
            // [Fluent 스크롤러] 전 창 공용 — 모든 자식 ScrollView를 얇은 오버레이형으로 통일(멱등).
            UTKTheme.ApplyFluentScrollers(this);
        }

        /// <summary>닫기 (표시 제거 + 해제 + OnWindowClosed).</summary>
        public virtual void Hide()
        {
            if (!_isOpen) return;
            _isOpen = false;
            style.display = DisplayStyle.None;
            if (_shadow != null)
                _shadow.style.display = DisplayStyle.None;   // 닫힘 시 섀도우도 숨김
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

        /// <summary>
        /// Changes chrome without affecting visibility, placement, manager registration, or ESC stack.
        /// Existing windows need not opt in; call this from a subclass for selective presentation.
        /// </summary>
        public void SetChrome(UTKWindowChrome chrome)
        {
            if (Chrome == chrome)
                return;

            UnregisterDragCallbacks(_dragTarget);
            Chrome = chrome;
            if (chrome == UTKWindowChrome.Frameless)
            {
                _titleBar.style.display = DisplayStyle.None;
                AddToClassList("utk-window-frameless");
                _dragTarget = _content;
                style.backgroundImage = StyleKeyword.Null;
                _content.style.paddingLeft = _content.style.paddingRight = 0f;
                _content.style.paddingTop = _content.style.paddingBottom = 0f;
            }
            else
            {
                _titleBar.style.display = DisplayStyle.Flex;
                RemoveFromClassList("utk-window-frameless");
                _dragTarget = _titleBar;
                _content.style.paddingLeft = _content.style.paddingRight = StyleKeyword.Null;
                _content.style.paddingTop = _content.style.paddingBottom = StyleKeyword.Null;
            }

            RegisterDragCallbacks(_dragTarget);
            ApplyChrome();
        }

        /// <summary>
        /// Uses a custom element as a drag handle (including in frameless windows).
        /// Set to null to restore the standard title bar or the frameless window surface.
        /// </summary>
        public void SetDragHandle(VisualElement handle)
        {
            UnregisterDragCallbacks(_dragTarget);
            _dragTarget = handle ?? (IsFrameless ? _content : _titleBar);
            RegisterDragCallbacks(_dragTarget);
        }

        /// <summary>Creates a theme panel inside the content area and returns it for population.</summary>
        public VisualElement AddContentPanel(string panelName = null, bool secondary = false)
        {
            var panel = UTKTheme.CreatePanel(panelName, secondary);
            _content.Add(panel);
            return panel;
        }

        private void ApplyChrome()
        {
            if (IsFrameless)
            {
                style.backgroundColor = new StyleColor(UTKTheme.Panel);
                style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
                style.borderTopLeftRadius = style.borderTopRightRadius = 0f;
                style.borderBottomLeftRadius = style.borderBottomRightRadius = 0f;
                _content.style.paddingLeft = _content.style.paddingRight = 0f;
                _content.style.paddingTop = _content.style.paddingBottom = 0f;
            }
            else
            {
                RemoveFromClassList("utk-window-frameless");
                style.backgroundColor = StyleKeyword.Null;
                style.backgroundImage = StyleKeyword.Null;
                style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = StyleKeyword.Null;
                style.borderTopLeftRadius = style.borderTopRightRadius = StyleKeyword.Null;
                style.borderBottomLeftRadius = style.borderBottomRightRadius = StyleKeyword.Null;
            }
        }

        private void RegisterDragCallbacks(VisualElement target)
        {
            if (target == null) return;
            target.RegisterCallback<PointerDownEvent>(OnTitleBarPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnTitleBarPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnTitleBarPointerUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnDragCaptureOut);
        }

        private void UnregisterDragCallbacks(VisualElement target)
        {
            if (target == null) return;
            target.UnregisterCallback<PointerDownEvent>(OnTitleBarPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnTitleBarPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnTitleBarPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnDragCaptureOut);
        }

        private void OnDragCaptureOut(PointerCaptureOutEvent evt) => _dragging = false;

        // ─────────────────────────── 훅 (오버라이드) ───────────────────────────

        /// <summary>Subclass에서 필요 시 오버라이드.</summary>
        protected virtual void OnWindowOpen() { }

        /// <summary>Subclass에서 필요 시 오버라이드.</summary>
        protected virtual void OnWindowClosed() { }

        // ─────────────────────────── 드래그 (PointerManipulator 구현) ───────────────────────────

        private void OnTitleBarPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            // Frameless drag starts only when the exposed target itself was hit.
            // Bubbling from controls (buttons, slots, ScrollViews, etc.) must not drag.
            if (IsFrameless && (evt.target as VisualElement) != _dragTarget) return;
            if (!IsFrameless && evt.target == _closeButton) return;
            _dragging = true;
            _dragTarget.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnTitleBarPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging) return;
            // 이벤트마다 현재 위치 기반 델타 이동 (캡처 상태에서도 이벤트 수신).
            // PointerMoveEvent.deltaPosition 사용 — 픽셀 단위.
            style.left = style.left.value.value + evt.deltaPosition.x;
            style.top  = style.top.value.value + evt.deltaPosition.y;
            SyncShadow();   // 드래그 중 그림자가 창을 따라다니게
            evt.StopPropagation();
        }

        private void OnTitleBarPointerUp(PointerUpEvent evt)
        {
            if (!_dragging) return;
            _dragging = false;
            _dragTarget.ReleasePointer(evt.pointerId);
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