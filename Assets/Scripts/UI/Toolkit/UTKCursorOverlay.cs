using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P22-2 — UTK 화면 커서 오버레이.
    /// 3D 월드 앵커 방식(카메라 앞 3D 아이콘)은 카메라 각도/거리에 따라 안 보이거나 어긋나는
    /// 근본 결함이 있어 폐기하고, UIRoot 직속 아이콘이 마우스 패널 좌표를 추적하는
    /// **화면 스페이스 커서**로 재설계 — 항상 정확히 마우스 위치에 보인다.
    /// 아이콘 = 베이크 PNG 5종(검/곡괭이/삽/호미/화살표, 금속 그라데이션 — 프리미티브 금지 규약).
    /// 종류는 HoverTargetClassifier 판정(250ms 폴링 — 매 프레임 레이캐스트 부하 회피).
    /// OS 커서는 CursorVisibilityController가 계속 숨김. PointerOverUI에서는 아이콘 반투명.
    /// </summary>
    public class UTKCursorOverlay : VisualElement
    {
        private static UTKCursorOverlay _instance;

        private const float Size = 44f;
        private readonly VisualElement _icon;
        private readonly Texture2D _sword, _pick, _shovel, _hoe, _arrow;
        private HoverTargetClassifier.TargetKind _lastKind = HoverTargetClassifier.TargetKind.None;

        public static UTKCursorOverlay Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new UTKCursorOverlay();
            root.Add(_instance);
            Debug.Log("[UTKCursorOverlay][P22-2] 화면 커서 오버레이 부착 — OS 커서 대체");
            return _instance;
        }

        private UTKCursorOverlay()
        {
            name = "UTKCursorOverlay";
            pickingMode = PickingMode.Ignore;           // 클릭은 실제 마우스로 — 오버레이는 시각 전용
            style.position = Position.Absolute;
            style.width = Size;
            style.height = Size;
            style.left = 0f;
            style.top = 0f;

            _icon = new VisualElement();
            _icon.pickingMode = PickingMode.Ignore;     // [P23] 기본값 Position은 픽커블 — 44px 아이콘이 모든 포인터 이벤트(클릭/드래그)를 흡수하는 버그. 루트와 동일하게 시각 전용 유지
            _icon.style.width = Size;
            _icon.style.height = Size;
            _icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            _icon.style.opacity = 0.92f;
            Add(_icon);

            _sword = Resources.Load<Texture2D>("UI/cursor_sword");
            _pick = Resources.Load<Texture2D>("UI/cursor_pickaxe");
            _shovel = Resources.Load<Texture2D>("UI/cursor_shovel");
            _hoe = Resources.Load<Texture2D>("UI/cursor_hoe");
            _arrow = Resources.Load<Texture2D>("UI/cursor_arrow");

            SetIcon(_arrow);

            // [P22-2] 마우스 추적 — 16ms 스케줄(매 프레임)
            schedule.Execute(UpdatePosition).Every(16);
            // [P22-2] 컨텍스트 아이콘 판정 — 250ms 폴링(매 프레임 레이캐스트 부하 회피)
            schedule.Execute(UpdateKind).Every(250);
        }

        private void UpdatePosition()
        {
            var root = UIToolkitBootstrap.UIRoot;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (root == null || mouse == null) return;

            // UTKDragDrop.GetPanelPointerPos와 동일 수식 — 패널 스케일 무관 정확 좌표
            var screen = mouse.position.ReadValue();
            float scale = root.worldBound.width / (float)Screen.width;
            if (scale <= 0f) scale = 1f;
            float px = screen.x * scale;
            float py = root.worldBound.height - screen.y * scale;

            style.left = px - Size * 0.5f;   // 아이콘 중심 = 마우스 포인터
            style.top = py - Size * 0.5f;
            BringToFront();
        }

        private void UpdateKind()
        {
            // UI 위에서는 화살표 유지(반투명) — 버튼 호버 가독성
            bool overUI = ProjectName.Core.UITransitionState.PointerOverUI;
            _icon.style.opacity = overUI ? 0.45f : 0.92f;
            if (overUI) { if (_lastKind != HoverTargetClassifier.TargetKind.None) { SetIcon(_arrow); _lastKind = HoverTargetClassifier.TargetKind.None; } return; }

            var kind = HoverTargetClassifier.ClassifyAt(
                UnityEngine.InputSystem.Mouse.current != null
                    ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                    : Vector2.zero);

            if (kind == _lastKind) return;
            _lastKind = kind;
            switch (kind)
            {
                case HoverTargetClassifier.TargetKind.Enemy: SetIcon(_sword); break;
                case HoverTargetClassifier.TargetKind.Mine: SetIcon(_pick); break;
                case HoverTargetClassifier.TargetKind.Gather: SetIcon(_shovel); break;
                case HoverTargetClassifier.TargetKind.Farm: SetIcon(_hoe); break;
                default: SetIcon(_arrow); break;
            }
        }

        private void SetIcon(Texture2D tex)
        {
            _icon.style.backgroundImage = UTKTextureSafe.ToBackground(tex);
            _icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }
    }
}
