using System;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;

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
    }
}
