using System.Collections.Generic;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit RTS feedback overlay. It is attached to the persistent UIRoot at runtime,
    /// listens to GuardSelectionManager's UI bridge events, and never captures pointer picking.
    /// </summary>
    public sealed class RTSSelectionOverlayUTK : VisualElement
    {
        private static readonly Color GithubPanel = new Color32(22, 27, 34, 245);
        private static readonly Color GithubBorder = new Color32(48, 54, 61, 255);
        private static readonly Color GithubBlue = new Color32(88, 166, 255, 255);
        private static readonly Color GithubBlueFill = new Color32(31, 111, 235, 42);
        private static readonly Color GithubText = new Color32(230, 237, 243, 255);

        private static RTSSelectionOverlayUTK _overlay;
        private static bool _eventsHooked;
        private static Rect _selectionScreenRect;
        private static bool _selectionBoxVisible;
        private static int _selectedCount;
        private static VisualElement _box;
        private static Label _countLabel;
        private static VisualElement _countChip;

        private RTSSelectionOverlayUTK()
        {
            name = "RTSSelectionOverlayUTK";
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.right = 0f;
            style.bottom = 0f;
            pickingMode = PickingMode.Ignore;

            BuildSelectionBox();
            BuildCountChip();
            RefreshCount();
            RefreshSelectionBox();
        }

        /// <summary>Runs after scene load; the persistent updater handles late UIRoot readiness/recreation.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            HookEventsOnce();
            UIToolkitBootstrap.Ensure();

            if (Object.FindAnyObjectByType<RTSSelectionOverlayUpdater>() == null)
            {
                var go = new GameObject("RTSSelectionOverlayUpdater");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<RTSSelectionOverlayUpdater>();
            }

            // Events are not replayed to late subscribers, so establish the initial count explicitly.
            var manager = GuardSelectionManager.Instance;
            _selectedCount = manager != null ? manager.SelectedCount : 0;
            RefreshCount();
            EnsureAttached();
        }

        private static void HookEventsOnce()
        {
            if (_eventsHooked) return;
            // Remove first as a defensive guard for no-domain-reload/editor re-entry.
            GuardSelectionManager.SelectionChanged -= OnSelectionChanged;
            GuardSelectionManager.SelectionBoxChanged -= OnSelectionBoxChanged;
            GuardSelectionManager.SelectionChanged += OnSelectionChanged;
            GuardSelectionManager.SelectionBoxChanged += OnSelectionBoxChanged;
            _eventsHooked = true;
        }

        private static void OnSelectionChanged(IReadOnlyList<GuardPlaceholder> guards, int count)
        {
            _selectedCount = Mathf.Max(0, count);
            RefreshCount();
        }

        private static void OnSelectionBoxChanged(Rect screenRect, bool visible, Color fill, Color border)
        {
            _selectionScreenRect = screenRect;
            _selectionBoxVisible = visible;
            RefreshSelectionBox();
        }

        private static void EnsureAttached()
        {
            HookEventsOnce();
            if (_overlay == null)
                _overlay = new RTSSelectionOverlayUTK();

            var root = UIToolkitBootstrap.UIRoot;
            if (root == null || _overlay.parent == root) return;

            if (_overlay.parent != null)
                _overlay.RemoveFromHierarchy();
            root.Add(_overlay);
            RefreshCount();
            RefreshSelectionBox();
        }

        private void BuildSelectionBox()
        {
            _box = new VisualElement { name = "RTSSelectionBox", pickingMode = PickingMode.Ignore };
            _box.style.position = Position.Absolute;
            _box.style.display = DisplayStyle.None;
            _box.style.backgroundColor = GithubBlueFill;
            _box.style.borderLeftWidth = 1f;
            _box.style.borderRightWidth = 1f;
            _box.style.borderTopWidth = 1f;
            _box.style.borderBottomWidth = 1f;
            _box.style.borderLeftColor = GithubBlue;
            _box.style.borderRightColor = GithubBlue;
            _box.style.borderTopColor = GithubBlue;
            _box.style.borderBottomColor = GithubBlue;
            Add(_box);
        }

        private void BuildCountChip()
        {
            _countChip = new VisualElement { name = "RTSSelectionCountChip", pickingMode = PickingMode.Ignore };
            _countChip.style.position = Position.Absolute;
            _countChip.style.top = 18f;
            _countChip.style.right = 18f;
            _countChip.style.flexDirection = FlexDirection.Row;
            _countChip.style.alignItems = Align.Center;
            _countChip.style.paddingLeft = 12f;
            _countChip.style.paddingRight = 12f;
            _countChip.style.paddingTop = 7f;
            _countChip.style.paddingBottom = 7f;
            _countChip.style.backgroundColor = GithubPanel;
            _countChip.style.borderLeftWidth = 1f;
            _countChip.style.borderRightWidth = 1f;
            _countChip.style.borderTopWidth = 1f;
            _countChip.style.borderBottomWidth = 1f;
            _countChip.style.borderLeftColor = GithubBorder;
            _countChip.style.borderRightColor = GithubBorder;
            _countChip.style.borderTopColor = GithubBorder;
            _countChip.style.borderBottomColor = GithubBorder;
            _countChip.style.borderTopLeftRadius = 5f;
            _countChip.style.borderTopRightRadius = 5f;
            _countChip.style.borderBottomLeftRadius = 5f;
            _countChip.style.borderBottomRightRadius = 5f;

            var accent = new VisualElement { name = "RTSSelectionAccent", pickingMode = PickingMode.Ignore };
            accent.style.width = 3f;
            accent.style.height = 16f;
            accent.style.marginRight = 8f;
            accent.style.backgroundColor = GithubBlue;
            _countChip.Add(accent);

            _countLabel = new Label { name = "RTSSelectedCount" };
            _countLabel.style.color = GithubText;
            _countLabel.style.fontSize = 13f;
            _countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _countLabel.pickingMode = PickingMode.Ignore;
            _countChip.Add(_countLabel);
            Add(_countChip);
        }

        private static void RefreshCount()
        {
            if (_countLabel == null) return;
            _countLabel.text = _selectedCount == 1 ? "1 unit selected" : _selectedCount + " units selected";
            if (_countChip != null)
                _countChip.style.display = _selectedCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void RefreshSelectionBox()
        {
            if (_box == null) return;
            if (!_selectionBoxVisible)
            {
                _box.style.display = DisplayStyle.None;
                return;
            }

            var panel = _overlay != null ? _overlay.panel : null;
            float panelWidth = _overlay != null ? _overlay.layout.width : 0f;
            float panelHeight = _overlay != null ? _overlay.layout.height : 0f;
            if (panel == null || panelWidth <= 0f || panelHeight <= 0f || Screen.width <= 0 || Screen.height <= 0)
            {
                _box.style.display = DisplayStyle.None;
                return;
            }

            float scaleX = panelWidth / Screen.width;
            float scaleY = panelHeight / Screen.height;
            _box.style.left = _selectionScreenRect.x * scaleX;
            _box.style.top = _selectionScreenRect.y * scaleY;
            _box.style.width = Mathf.Max(0f, _selectionScreenRect.width * scaleX);
            _box.style.height = Mathf.Max(0f, _selectionScreenRect.height * scaleY);
            _box.style.display = DisplayStyle.Flex;
        }

        /// <summary>Persistent heartbeat: re-hook defensively and attach to whichever scene UIRoot is current.</summary>
        private sealed class RTSSelectionOverlayUpdater : MonoBehaviour
        {
            private void Update()
            {
                EnsureAttached();

                // The manager may be created after this overlay during scene bootstrap. Keep its
                // current state represented even when its initial event was published pre-hook.
                var manager = GuardSelectionManager.Instance;
                if (manager != null && manager.SelectedCount != _selectedCount)
                {
                    _selectedCount = manager.SelectedCount;
                    RefreshCount();
                }

                RefreshSelectionBox();
            }
        }
    }
}
