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
    ///
    /// F-UI 전환 (2026-09-23): 커서 아이콘을 Figma 디자인(GitHub-dark 3dicons) 9종으로 교체.
    ///   primary(화살촉=기본) / mine(곡괭이=채굴) / gather(잎=채집) / interact(말풍선=캐릭터 상호작용)
    ///   / shop(돈=상점) / attack(과녁=공격) / cook(불=요리) / monster(돋보기=Ctrl+몬스터=정보)
    ///   / door(열쇠=문). HoverTargetClassifier가 몬스터 vs 적병사 vs NPC를 구분(UTKCursorOverlay가
    ///   Ctrl 상태와 조합해 최종 아이콘 결정). 아이콘 = Figma export 베이크 PNG(프리미티브 금지 규약).
    /// 종류는 HoverTargetClassifier 판정(250ms 폴링 — 매 프레임 레이캐스트 부하 회피).
    /// OS 커서는 CursorVisibilityController가 계속 숨김. PointerOverUI에서는 아이콘 반투명.
    /// </summary>
    public class UTKCursorOverlay : VisualElement
    {
        private static UTKCursorOverlay _instance;

        private const float Size = 44f;
        private readonly VisualElement _icon;
        private readonly Texture2D _primary, _mine, _gather, _interact, _shop, _attack, _cook, _monster, _door;
        private HoverTargetClassifier.TargetKind _lastKind = HoverTargetClassifier.TargetKind.None;

        public static UTKCursorOverlay Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new UTKCursorOverlay();
            root.Add(_instance);
            Debug.Log("[UTKCursorOverlay][F-UI] 화면 커서 오버레이 부착 — OS 커서 대체 (Figma 3dicons 9종)");
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

            _primary  = Resources.Load<Texture2D>("UI/cursor_primary");
            _mine     = Resources.Load<Texture2D>("UI/cursor_mine");
            _gather   = Resources.Load<Texture2D>("UI/cursor_gather");
            _interact = Resources.Load<Texture2D>("UI/cursor_interact");
            _shop     = Resources.Load<Texture2D>("UI/cursor_shop");
            _attack   = Resources.Load<Texture2D>("UI/cursor_attack");
            _cook     = Resources.Load<Texture2D>("UI/cursor_cook");
            _monster  = Resources.Load<Texture2D>("UI/cursor_monster");
            _door     = Resources.Load<Texture2D>("UI/cursor_door");

            SetIcon(_primary);

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

            // [P25-B] 기본 숨김 게이트 — 16ms(빠른 응답)로 갱신. Ctrl 홀드(컨텍스트 아이콘)
            //   또는 UI 호버(화살표 반투명)에서만 표시. 활 드로 중엔 리티클이 대체하므로 숨김.
            UpdateVisibility();
        }

        /// <summary>[P25-B] 표시 조건: ①Ctrl 홀드 or ②UI 호버 or ③활 드로(리티클 대체) — 그 외엔 숨김.</summary>
        private void UpdateVisibility()
        {
            bool drawing = ProjectName.Systems.BowAimState.Drawing;
            if (drawing)
            {
                if (_icon.style.display != DisplayStyle.None) _icon.style.display = DisplayStyle.None;
                return;
            }
            bool ctrlHeld = IsCtrlHeld();
            bool overUI = ProjectName.Core.UITransitionState.PointerOverUI;
            bool show = ctrlHeld || overUI;
            _icon.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            _icon.style.opacity = overUI ? 0.45f : 0.92f;
        }

        /// <summary>Ctrl 홀드 여부 — [F-UI] UpdateKind/UpdateVisibility 공용.</summary>
        private static bool IsCtrlHeld()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null
                && (kb.ctrlKey.isPressed
                    || kb.leftCtrlKey.isPressed
                    || kb.rightCtrlKey.isPressed);
        }

        private void UpdateKind()
        {
            // [P25-B] 숨김 상태(기본/드로/비 Ctrl)에서는 분류 생략
            if (_icon.style.display == DisplayStyle.None) return;

            bool overUI = ProjectName.Core.UITransitionState.PointerOverUI;
            if (overUI)
            {
                if (_lastKind != HoverTargetClassifier.TargetKind.None)
                {
                    SetIcon(_primary);
                    _lastKind = HoverTargetClassifier.TargetKind.None;
                }
                return;
            }

            var kind = HoverTargetClassifier.ClassifyAt(
                UnityEngine.InputSystem.Mouse.current != null
                    ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                    : Vector2.zero);

            if (kind == _lastKind) return;
            _lastKind = kind;
            bool ctrl = IsCtrlHeld();
            switch (kind)
            {
                case HoverTargetClassifier.TargetKind.Mine:     SetIcon(_mine); break;
                case HoverTargetClassifier.TargetKind.Gather:   SetIcon(_gather); break;
                // 몬스터: 기본 공격(과녁) / Ctrl 홀드 시 정보(돋보기)
                case HoverTargetClassifier.TargetKind.Monster:  SetIcon(ctrl ? _monster : _attack); break;
                // 적 병사: 기본 공격(과녁) / Ctrl 홀드 시 상호작용(말풍선)
                case HoverTargetClassifier.TargetKind.EnemyGuard: SetIcon(ctrl ? _interact : _attack); break;
                case HoverTargetClassifier.TargetKind.Ally:     SetIcon(_interact); break;
                case HoverTargetClassifier.TargetKind.NPC:      SetIcon(_interact); break;
                case HoverTargetClassifier.TargetKind.Door:     SetIcon(_door); break;
                case HoverTargetClassifier.TargetKind.Cook:     SetIcon(_cook); break;
                case HoverTargetClassifier.TargetKind.Shop:     SetIcon(_shop); break;
                // 폴백 적 태그(보스 등) → 공격
                case HoverTargetClassifier.TargetKind.Enemy:    SetIcon(_attack); break;
                case HoverTargetClassifier.TargetKind.Farm:     SetIcon(_primary); break;
                default: SetIcon(_primary); break;
            }
        }

        private void SetIcon(Texture2D tex)
        {
            _icon.style.backgroundImage = UTKTextureSafe.ToBackground(tex);
            _icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }
    }
}