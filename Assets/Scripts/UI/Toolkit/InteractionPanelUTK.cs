using System;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;
using ProjectName.UI;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Compact target interaction panel. Button behavior is exposed as an event so game-specific
    /// actions can be connected without introducing a Systems-to-UI dependency.
    /// </summary>
    public class InteractionPanelUTK : UTKWindowBase
    {
        // The source-supported four-action frames (81:91, 81:134 and 82:2) share
        // 288x98.4 Figma geometry. At the 1.2x canonical scale, the logical panel is
        // 240x82 and ActionGrid is (8,8,224,66), enclosing a 2x2 set of buttons.
        private const float PanelWidth = 240f;
        private const float StandardPanelHeight = 82f;
        private const float GridLeft = 8f;
        private const float GridTop = 8f;
        private const float GridWidth = 224f;
        private const float GridHeight = 66f;
        // The four Figma buttons are 130.8x36 with a 7.2px column gap;
        // at 1.2x canonical scale their logical bounds are 109x30 with a 6px gap.
        // Two columns at x=0/115 and rows at y=0/36 fit the 224x66 grid exactly.
        // Existing UTKButton padding/radius stays inside those fixed outer bounds.
        private const float ActionWidth = 109f;
        private const float ActionHeight = 30f;
        private const float ColumnGap = 6f;
        private const float SecondRowTop = 36f;

        private static InteractionPanelUTK _instance;

        private VisualElement _actionGrid;
        private HoverTargetClassifier.TargetKind _targetKind;
        private object _target;
        private float _uiScaleX = 1f;
        private float _uiScaleY = 1f;

        /// <summary>Raised when a panel action is clicked. TargetKind and target match the current panel.</summary>
        public static event Action<HoverTargetClassifier.TargetKind, string, object> ActionRequested;

        public static InteractionPanelUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance == null)
                _instance = new InteractionPanelUTK();
        }

        /// <summary>Open the compact panel for a classified target. Click-trigger wiring is intentionally external.</summary>
        public static void Open(HoverTargetClassifier.TargetKind kind, object target)
        {
            if (!IsSupportedKind(kind))
                return;

            Ensure();
            _instance.OpenForTarget(kind, target);
        }

        private InteractionPanelUTK() : base("상호작용", new Vector2(PanelWidth, StandardPanelHeight))
        {
            SetChrome(UTKWindowChrome.Frameless);
            style.width = PanelWidth;
            style.height = StandardPanelHeight;
            style.overflow = Overflow.Visible;

            _content.style.width = PanelWidth;
            _content.style.height = StandardPanelHeight;
            _content.style.paddingLeft = 0f;
            _content.style.paddingRight = 0f;
            _content.style.paddingTop = 0f;
            _content.style.paddingBottom = 0f;
            _content.style.flexGrow = 0f;
            _content.style.flexShrink = 0f;

            _actionGrid = new VisualElement { name = "ActionGrid" };
            _actionGrid.style.position = Position.Absolute;
            _actionGrid.style.left = GridLeft;
            _actionGrid.style.top = GridTop;
            _actionGrid.style.width = GridWidth;
            _actionGrid.style.height = GridHeight;
            _actionGrid.style.flexGrow = 0f;
            _actionGrid.style.flexShrink = 0f;
            _actionGrid.style.flexDirection = FlexDirection.Row;
            _actionGrid.style.flexWrap = Wrap.NoWrap;
            _actionGrid.style.alignItems = Align.FlexStart;
            _actionGrid.style.justifyContent = Justify.FlexStart;
            _actionGrid.style.overflow = Overflow.Visible;
            _content.Add(_actionGrid);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
        }

        private static bool IsSupportedKind(HoverTargetClassifier.TargetKind kind)
        {
            return kind == HoverTargetClassifier.TargetKind.EnemyGuard ||
                   kind == HoverTargetClassifier.TargetKind.Ally ||
                   kind == HoverTargetClassifier.TargetKind.NPC ||
                   kind == HoverTargetClassifier.TargetKind.ShopNPC ||
                   kind == HoverTargetClassifier.TargetKind.Lord;
        }

        private void OpenForTarget(HoverTargetClassifier.TargetKind kind, object target)
        {
            _targetKind = kind;
            _target = target;

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);

            ApplyRootScale(root);
            RebuildActions();
            Show();
        }

        private void ApplyRootScale(VisualElement root)
        {
            if (root == null)
                return;

            float rootWidth = root.resolvedStyle.width;
            float rootHeight = root.resolvedStyle.height;
            if (rootWidth <= 0f || rootHeight <= 0f)
                return;

            // Panel/root coordinates and screen input share the canonical 1920x1080
            // canvas. Scale the logical 240x82 panel directly against that root.
            float uiScaleX = rootWidth / FigmaCanvasLayout.CanvasWidth;
            float uiScaleY = rootHeight / FigmaCanvasLayout.CanvasHeight;
            _uiScaleX = uiScaleX;
            _uiScaleY = uiScaleY;
            Vector2 size = GetLayoutSize(_targetKind);
            style.width = size.x * uiScaleX;
            style.height = size.y * uiScaleY;
            _content.style.width = size.x * uiScaleX;
            _content.style.height = size.y * uiScaleY;
            _actionGrid.style.left = GridLeft * uiScaleX;
            _actionGrid.style.top = GridTop * uiScaleY;
            _actionGrid.style.width = GridWidth * uiScaleX;
            _actionGrid.style.height = GridHeight * uiScaleY;

            // The click dispatcher supplies screen-space input. Normalize it to the
            // canonical canvas and use the shared canvas scaler before positioning.
            if (Screen.width <= 0 || Screen.height <= 0)
                return;
            Vector3 mouse = Input.mousePosition;
            float canvasX = mouse.x * FigmaCanvasLayout.CanvasWidth / Screen.width;
            float canvasY = (Screen.height - mouse.y) * FigmaCanvasLayout.CanvasHeight / Screen.height;
            Rect scaledInput = FigmaCanvasLayout.ScaleRect(
                new Rect(canvasX, canvasY, 0f, 0f), new Vector2(rootWidth, rootHeight));
            Rect panelBounds = FigmaCanvasLayout.ScaleRect(
                new Rect(0f, 0f, size.x, size.y), new Vector2(rootWidth, rootHeight));
            style.position = Position.Absolute;
            style.left = Mathf.Clamp(scaledInput.x, 0f, Mathf.Max(0f, rootWidth - panelBounds.width));
            style.top = Mathf.Clamp(scaledInput.y, 0f, Mathf.Max(0f, rootHeight - panelBounds.height));
        }

        private static Vector2 GetLayoutSize(HoverTargetClassifier.TargetKind kind)
        {
            // The current Systems dispatcher routes five kinds to these four-action
            // frames; variants without a proven caller are not exposed here.
            switch (kind)
            {
                case HoverTargetClassifier.TargetKind.EnemyGuard: // 81:4
                case HoverTargetClassifier.TargetKind.Ally:       // 81:51
                case HoverTargetClassifier.TargetKind.NPC:        // 81:91
                case HoverTargetClassifier.TargetKind.ShopNPC:    // 81:134
                case HoverTargetClassifier.TargetKind.Lord:       // 82:2
                    return new Vector2(PanelWidth, StandardPanelHeight);
                default:
                    return new Vector2(PanelWidth, StandardPanelHeight);
            }
        }

        private void RebuildActions()
        {
            if (_actionGrid == null)
                return;

            _actionGrid.Clear();
            switch (_targetKind)
            {
                case HoverTargetClassifier.TargetKind.EnemyGuard:
                    AddAction("상태보기", 0);
                    AddAction("대화하기", 1);
                    AddAction("뇌물주기", 2);
                    AddAction("포섭하기", 3);
                    break;
                case HoverTargetClassifier.TargetKind.Ally:
                    AddAction("상태보기", 0);
                    AddAction("대화하기", 1);
                    AddAction("물약주기", 2);
                    AddAction("음식주기", 3);
                    break;
                case HoverTargetClassifier.TargetKind.NPC:
                    AddAction("상태보기", 0);
                    AddAction("대화하기", 1);
                    AddAction("선물주기", 2);
                    AddAction("퀘스트", 3);
                    break;
                case HoverTargetClassifier.TargetKind.ShopNPC:
                    AddAction("상태보기", 0);
                    AddAction("대화하기", 1);
                    AddAction("상점", 2);
                    AddAction("밀매제안", 3);
                    break;
                case HoverTargetClassifier.TargetKind.Lord:
                    AddAction("대화하기", 0);
                    AddAction("음식주기", 1);
                    AddAction("선물주기", 2);
                    AddAction("동맹제안", 3);
                    break;
            }
        }

        private void AddAction(string label, int index)
        {
            int row = index / 2;
            int column = index % 2;
            var button = UTKButton.Create(label, () => ActionRequested?.Invoke(_targetKind, label, _target), UTKButton.Variant.Secondary);
            button.name = "Action_" + label;
            button.style.position = Position.Absolute;
            button.style.left = column * (ActionWidth + ColumnGap) * _uiScaleX;
            button.style.top = (row == 0 ? 0f : SecondRowTop) * _uiScaleY;
            button.style.width = ActionWidth * _uiScaleX;
            button.style.height = ActionHeight * _uiScaleY;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.flexShrink = 0f;
            button.style.fontSize = 13.2f * Mathf.Min(_uiScaleX, _uiScaleY);
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.whiteSpace = WhiteSpace.NoWrap;
            button.SetEnabled(IsActionSupported(_targetKind, label, _target));
            _actionGrid.Add(button);
        }

        private static bool IsActionSupported(HoverTargetClassifier.TargetKind kind, string label, object target)
        {
            switch (label)
            {
                case "상태보기":
                    return (kind == HoverTargetClassifier.TargetKind.EnemyGuard
                            || kind == HoverTargetClassifier.TargetKind.Ally)
                           && target is GuardPlaceholder;
                case "대화하기":
                    return target is TerritoryNPCBehaviour npc
                           && !string.IsNullOrEmpty(npc.NPCData.NpcId);
                case "상점":
                    return kind == HoverTargetClassifier.TargetKind.ShopNPC;
                default:
                    return false;
            }
        }

        public override void Show()
        {
            bool wasOpen = IsOpen;
            base.Show();
            if (wasOpen)
                return;

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
        }

        // ===== [Phase D] 시스템 브리지 구독 + 액션 라우팅 =====

        /// <summary>정적 구독 등록 — 클릭 트리거 → 패널 표시, 패널 액션 → 실제 창 개폐.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            _instance = new InteractionPanelUTK();
            SoldierInteractBridge.OnTargetInteractionRequested += OnSystemTargetRequested;
            ActionRequested += OnActionRequested;
        }

        /// <summary>Systems(ContextCommandRouter)가 분류한 대상으로 패널 열기.</summary>
        private static void OnSystemTargetRequested(HoverTargetClassifier.TargetKind kind, object target)
        {
            Open(kind, target);
        }

        /// <summary>
        /// [Phase D] 패널 버튼 액션 라우팅. 이번 단계에서 동작하는 것은:
        ///   대화하기 → NPCInstance(NPCDialoguePanelUTK 선택지 패널), 상태보기(병사) → GuardInfoUTK,
        ///   상점(ShopNPC) → ShopWindowUTK. 뇌물/포섭/물약/음식/선물/퀘스트/동맹/밀매는 미구현(로그만).
        /// </summary>
        private static void OnActionRequested(HoverTargetClassifier.TargetKind kind, string label, object target)
        {
            switch (label)
            {
                case "대화하기":
                    OnTalkRequested(kind, target);
                    break;
                case "상태보기":
                    if ((kind == HoverTargetClassifier.TargetKind.EnemyGuard
                         || kind == HoverTargetClassifier.TargetKind.Ally) && target is GuardPlaceholder guard)
                        SoldierInteractBridge.Raise(guard);   // GuardInfoUTK 정보창 (기존 경로)
                    else
                        Debug.Log($"[InteractionPanelUTK] 상태보기 미지원 대상: {kind}");
                    break;
                case "상점":
                    if (kind == HoverTargetClassifier.TargetKind.ShopNPC)
                        ShopWindowUTK.Open();
                    else
                        Debug.Log($"[InteractionPanelUTK] 상점 액션 대상 오류: {kind}");
                    break;
                default:
                    Debug.Log($"[InteractionPanelUTK] 미구현 액션: {kind}/{label}");
                    break;
            }
        }

        /// <summary>대화하기 — NPCInstance면 선택지 패널, 영주/병사는 무해 처리.</summary>
        private static void OnTalkRequested(HoverTargetClassifier.TargetKind kind, object target)
        {
            var npc = target as TerritoryNPCBehaviour;
            if (npc != null
                && !string.IsNullOrEmpty(npc.NPCData.NpcId))
            {
                NPCDialoguePanelUTK.Open(npc.NPCData);
                return;
            }
            Debug.Log($"[InteractionPanelUTK] 대화 미지원 대상: {kind}");
        }
    }
}
