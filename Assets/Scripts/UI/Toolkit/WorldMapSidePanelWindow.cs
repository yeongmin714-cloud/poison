using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [144차 P0-WM #1/#2] 월드맵 좌/우 패널 독립 창 호스트 — QuestWindowUTK 선례의
    /// 프레임리스 풀캔버스 투명 호스트(Figma 95:188/95:255 = 캔버스 루트의 독립 프레임).
    /// 기존 BuildQuestListPanel/BuildQuestDetailPanel이 만든 패널 VisualElement 트리를
    /// 통째로 재부착(re-parent)하므로 WorldMapWindowUTK의 필드/갱신/폴링 경로는 무재배선.
    /// 호스트와 디자인 공간은 pickingMode Ignore — 지도 핀 클릭/호버가 통과하고, 패널 자식은
    /// 기본 picking으로 그대로 수신한다. 스케일은 WorldMapWindowUTK.ApplyCanvasBounds와
    /// 동일 축별 모델(1920×1080 ×(rw/1920, rh/1080), transformOrigin 0,0)로 시각 결과 동일 유지.
    /// 개별 CloseButton으로 단독 닫힘 가능(재개방은 월드맵 토글 경로 — 기록).
    /// </summary>
    public sealed class WorldMapSidePanelWindow : UTKWindowBase
    {
        private readonly VisualElement _panel;
        private readonly VisualElement _designSpace;
        private readonly float _panelLeft;
        private readonly float _panelTop;

        private WorldMapSidePanelWindow(string title, VisualElement panel, float panelLeft, float panelTop)
            : base(title, new Vector2(1920f, 1080f), UTKWindowChrome.Frameless)
        {
            _panel = panel;
            _panelLeft = panelLeft;
            _panelTop = panelTop;

            // 투명 풀캔버스 호스트 — RuntimePanelRoots 계약 선례(host alpha 0)와 동일 취급.
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            pickingMode = PickingMode.Ignore;   // 호스트 클릭 통과 — 패널 자식은 기본 picking

            _designSpace = new VisualElement { name = "SidePanelDesignSpace" };
            _designSpace.style.position = Position.Absolute;
            _designSpace.style.left = 0f;
            _designSpace.style.top = 0f;
            _designSpace.style.width = 1920f;
            _designSpace.style.height = 1080f;
            _designSpace.style.flexShrink = 0f;
            _designSpace.style.flexGrow = 0f;
            _designSpace.pickingMode = PickingMode.Ignore;
            _content.style.flexShrink = 0f;
            _content.style.flexGrow = 0f;
            _content.pickingMode = PickingMode.Ignore;
            _content.Add(_designSpace);

            panel.style.position = Position.Absolute;
            panel.style.left = panelLeft;
            panel.style.top = panelTop;
            _designSpace.Add(panel);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChangedApplyCanvas);
        }

        /// <summary>기존 패널 요소를 독립 창으로 이식(1회) — 이후 Show/Hide로 표시 제어.</summary>
        public static WorldMapSidePanelWindow Attach(string title, VisualElement panel, float panelLeft, float panelTop)
        {
            var win = new WorldMapSidePanelWindow(title, panel, panelLeft, panelTop);
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && win.parent == null)
                root.Add(win);
            return win;
        }

        /// <summary>월드맵 ApplyCanvasBounds와 동일 축별 스케일 — 시각 결과 무차 유지.</summary>
        private void OnGeometryChangedApplyCanvas(GeometryChangedEvent _)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float rw = root.resolvedStyle.width, rh = root.resolvedStyle.height;
            if (rw <= 1f || rh <= 1f || float.IsNaN(rw) || float.IsNaN(rh)) return;
            style.left = 0f;
            style.top = 0f;
            style.width = rw;
            style.height = rh;
            _designSpace.style.scale = new Scale(new Vector2(rw / 1920f, rh / 1080f));
            _designSpace.style.transformOrigin = new TransformOrigin(Length.Percent(0f), Length.Percent(0f));
        }

        public void ShowPanel()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null)
            {
                // z-order 보장 — 월드맵이 재부착된 경우에도 패널이 항상 맵 위에 렌더되도록 최상단 이동.
                if (parent != null) RemoveFromHierarchy();
                root.Add(this);
            }
            Show();
        }

        public void HidePanel() => Hide();
    }
}
