using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Placement helpers for independent top-level windows, plus an opt-in responsive
    /// three-column composite layout for content hosted inside a single window.
    /// These two patterns are deliberately separate: Place positions sibling windows;
    /// CreateResponsiveColumns creates ordinary panels inside one window's Content.
    /// </summary>
    public static class UTKThreeColumnLayout
    {
        public const float TopMargin = 96f;
        public const float DefaultColumnGap = 12f;
        public const float DefaultStackBreakpoint = 900f;
        private const float FallbackWidth = 1920f;
        private const float FallbackHeight = 1080f;
        private static readonly ConditionalWeakTable<VisualElement, RequestedWidth> RequestedWidths =
            new ConditionalWeakTable<VisualElement, RequestedWidth>();

        private sealed class RequestedWidth
        {
            public readonly float Value;
            public bool IsClamped;

            public RequestedWidth(float value)
            {
                Value = value;
            }
        }

        /// <summary>UIRoot resolved width (1920 fallback before layout resolves).</summary>
        public static float RootWidth => ReadRootDimension(true);

        /// <summary>UIRoot resolved height (1080 fallback before layout resolves).</summary>
        public static float RootHeight => ReadRootDimension(false);

        private static float ReadRootDimension(bool width)
        {
            var root = UIToolkitBootstrap.UIRoot;
            float value = root != null
                ? (width ? root.resolvedStyle.width : root.resolvedStyle.height)
                : 0f;
            return value > 0f ? value : (width ? FallbackWidth : FallbackHeight);
        }

        /// <summary>Legacy three-sibling-window column geometry (0=left, 1=center, 2=right).</summary>
        public static void GetColumn(int index, out float x, out float width)
        {
            int safeIndex = Mathf.Clamp(index, 0, 2);
            float columnWidth = RootWidth / 3f;
            x = columnWidth * safeIndex;
            width = Mathf.Max(0f, columnWidth - 16f);
        }

        /// <summary>
        /// Positions an independent top-level window in one of the three root columns.
        /// This does not create a composite layout; each window remains independently draggable.
        /// </summary>
        public static void Place(VisualElement win, int index)
        {
            if (win == null) return;
            GetColumn(index, out float x, out float columnWidth);
            win.style.position = Position.Absolute;
            win.style.left = x + 8f;
            win.style.top = TopMargin;
            win.style.right = StyleKeyword.Auto;
            win.style.bottom = StyleKeyword.Auto;
            if (!RequestedWidths.TryGetValue(win, out RequestedWidth requestedWidth))
            {
                float initialWidth;
                if (win.style.width != null && win.style.width.value.unit == Length.Unit.Pixel)
                {
                    initialWidth = win.style.width.value.value;
                }
                else if (win.style.width == null)
                {
                    initialWidth = win.resolvedStyle.width;
                }
                else
                {
                    float currentWidth = win.resolvedStyle.width;
                    if (currentWidth <= 0f)
                        currentWidth = win.style.width.value.value;
                    if (currentWidth > columnWidth && columnWidth > 0f)
                        win.style.width = columnWidth;
                    return;
                }

                if (initialWidth <= 0f)
                    return;

                requestedWidth = new RequestedWidth(initialWidth);
                RequestedWidths.Add(win, requestedWidth);
            }

            if (columnWidth > 0f && requestedWidth.Value > columnWidth)
            {
                win.style.width = columnWidth;
                requestedWidth.IsClamped = true;
            }
            else if (requestedWidth.IsClamped)
            {
                win.style.width = requestedWidth.Value;
                requestedWidth.IsClamped = false;
            }
        }

        /// <summary>Positions an independent window at an explicit root-local location.</summary>
        public static void PlaceAt(VisualElement win, Vector2 position)
        {
            if (win == null) return;
            win.style.position = Position.Absolute;
            win.style.left = position.x;
            win.style.top = position.y;
            win.style.right = StyleKeyword.Auto;
            win.style.bottom = StyleKeyword.Auto;
        }

        /// <summary>Centers an independent window in the current root (placement can be refreshed).</summary>
        public static void Center(VisualElement win, float topOffset = TopMargin)
        {
            if (win == null) return;
            float width = win.resolvedStyle.width;
            if (width <= 0f && win.style.width != null)
                width = win.style.width.value.value;
            PlaceAt(win, new Vector2(Mathf.Max(0f, (RootWidth - width) * 0.5f), topOffset));
        }

        /// <summary>
        /// Creates three themed columns within a single window/content element. The columns
        /// lay out in a row when there is room and stack vertically below the breakpoint.
        /// </summary>
        public static ResponsiveColumns CreateResponsiveColumns(
            VisualElement host,
            float gap = DefaultColumnGap,
            float stackBreakpoint = DefaultStackBreakpoint)
        {
            return new ResponsiveColumns(host, gap, stackBreakpoint);
        }

        /// <summary>Live columns created by CreateResponsiveColumns.</summary>
        public sealed class ResponsiveColumns
        {
            private readonly VisualElement _host;
            private readonly VisualElement _container;
            private readonly VisualElement[] _columns;
            private readonly float _gap;
            private readonly float _stackBreakpoint;

            public VisualElement Container => _container;
            public VisualElement Left => _columns[0];
            public VisualElement Center => _columns[1];
            public VisualElement Right => _columns[2];
            public VisualElement this[int index] => _columns[Mathf.Clamp(index, 0, 2)];

            public ResponsiveColumns(VisualElement host, float gap, float stackBreakpoint)
            {
                _host = host;
                _gap = Mathf.Max(0f, gap);
                _stackBreakpoint = Mathf.Max(0f, stackBreakpoint);
                _columns = new VisualElement[3];
                _container = new VisualElement { name = "UTKThreeColumnContainer" };
                _container.AddToClassList("utk-three-column-layout");
                _container.style.flexGrow = 1f;
                _container.style.flexShrink = 1f;
                _container.style.flexDirection = FlexDirection.Row;
                _container.style.alignItems = Align.Stretch;
                _container.style.minWidth = 0f;

                for (int i = 0; i < _columns.Length; i++)
                {
                    var column = UTKTheme.CreatePanel("UTKColumn" + (i + 1));
                    column.AddToClassList("utk-column");
                    column.style.flexGrow = 1f;
                    column.style.flexShrink = 1f;
                    column.style.flexBasis = 0f;
                    column.style.minWidth = 0f;
                    if (i > 0)
                        column.style.marginLeft = _gap;
                    _columns[i] = column;
                    _container.Add(column);
                }

                if (_host != null)
                {
                    _host.Add(_container);
                    _host.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                }
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null)
                    root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                UpdateDirection(AvailableWidth());
            }

            /// <summary>Stops responsive callbacks and removes this layout from its host.</summary>
            public void Dispose()
            {
                if (_host != null)
                {
                    _host.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                    if (_container.parent == _host)
                        _host.Remove(_container);
                }
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null)
                    root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }

            private void OnGeometryChanged(GeometryChangedEvent evt)
            {
                UpdateDirection(AvailableWidth());
            }

            private float AvailableWidth()
            {
                if (_host != null && _host.resolvedStyle.width > 0f)
                    return _host.resolvedStyle.width;
                return RootWidth;
            }

            private void UpdateDirection(float width)
            {
                bool stacked = width < _stackBreakpoint;
                _container.style.flexDirection = stacked ? FlexDirection.Column : FlexDirection.Row;
                for (int i = 0; i < _columns.Length; i++)
                {
                    var column = _columns[i];
                    if (stacked)
                    {
                        column.style.flexBasis = StyleKeyword.Auto;
                        column.style.width = StyleKeyword.Auto;
                    }
                    else
                    {
                        column.style.flexBasis = 0f;
                        column.style.width = StyleKeyword.Null;
                    }
                    column.style.marginLeft = stacked || i == 0 ? 0f : _gap;
                    column.style.marginTop = stacked && i > 0 ? _gap : 0f;
                }
            }
        }
    }
}
