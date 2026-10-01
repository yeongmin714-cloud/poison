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
        private const float PanelWidth = 240f;
        private const float PanelHeight = 82f;
        private const float GridWidth = 224f;
        private const float GridHeight = 66f;

        private static InteractionPanelUTK _instance;

        private VisualElement _actionGrid;
        private HoverTargetClassifier.TargetKind _targetKind;
        private object _target;

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

        private InteractionPanelUTK() : base("상호작용", new Vector2(PanelWidth, PanelHeight))
        {
            SetChrome(UTKWindowChrome.Frameless);
            style.width = PanelWidth;
            style.height = PanelHeight;
            style.overflow = Overflow.Visible;

            _content.style.width = PanelWidth;
            _content.style.height = PanelHeight;
            _content.style.paddingLeft = 8f;
            _content.style.paddingRight = 8f;
            _content.style.paddingTop = 8f;
            _content.style.paddingBottom = 8f;
            _content.style.flexGrow = 0f;
            _content.style.flexShrink = 0f;

            _actionGrid = new VisualElement { name = "ActionGrid" };
            _actionGrid.style.width = GridWidth;
            _actionGrid.style.height = GridHeight;
            _actionGrid.style.flexGrow = 0f;
            _actionGrid.style.flexShrink = 0f;
            _actionGrid.style.flexDirection = FlexDirection.Row;
            _actionGrid.style.flexWrap = Wrap.Wrap;
            _actionGrid.style.alignItems = Align.Stretch;
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
            RebuildActions();

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);

            Show();
        }

        private void RebuildActions()
        {
            if (_actionGrid == null)
                return;

            _actionGrid.Clear();
            switch (_targetKind)
            {
                case HoverTargetClassifier.TargetKind.EnemyGuard:
                    AddAction("상태보기");
                    AddAction("대화하기");
                    AddAction("뇌물주기");
                    AddAction("포섭하기");
                    break;
                case HoverTargetClassifier.TargetKind.Ally:
                    AddAction("상태보기");
                    AddAction("대화하기");
                    AddAction("물약주기");
                    AddAction("음식주기");
                    break;
                case HoverTargetClassifier.TargetKind.NPC:
                    AddAction("상태보기");
                    AddAction("대화하기");
                    AddAction("선물주기");
                    AddAction("퀘스트");
                    break;
                case HoverTargetClassifier.TargetKind.ShopNPC:
                    AddAction("상태보기");
                    AddAction("대화하기");
                    AddAction("상점");
                    AddAction("밀매제안");
                    break;
                case HoverTargetClassifier.TargetKind.Lord:
                    AddAction("대화하기");
                    AddAction("음식주기");
                    AddAction("선물주기");
                    AddAction("동맹제안");
                    break;
            }
        }

        private void AddAction(string label)
        {
            var button = UTKButton.Create(label, () => ActionRequested?.Invoke(_targetKind, label, _target), UTKButton.Variant.Secondary);
            button.name = "Action_" + label;
            button.style.width = new Length(50f, LengthUnit.Percent);
            button.style.height = 48f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 6f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.flexShrink = 0f;
            button.style.fontSize = 13f;
            _actionGrid.Add(button);
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
