using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Shared GitHub-dark / Fluent design tokens and small UI Toolkit styling helpers.
    /// Existing windows can continue to use Theme.uss; helpers here are opt-in for new
    /// panels and incremental window migrations.
    /// </summary>
    public static class UTKTheme
    {
        // ===== GitHub-dark / Fluent dark tokens =====
        /// <summary>Screen/backdrop base. #0B0E14</summary>
        public static readonly Color BgBase = Hex(0x0B0E14);
        /// <summary>Main panel. #161B22</summary>
        public static readonly Color Panel = Hex(0x161B22);
        /// <summary>Secondary panel/title/card. #21262D</summary>
        public static readonly Color PanelSub = Hex(0x21262D);
        /// <summary>Accent. #58A6FF</summary>
        public static readonly Color Accent = Hex(0x58A6FF);
        /// <summary>Rare/active/gold. #E3B341</summary>
        public static readonly Color Gold = Hex(0xE3B341);
        /// <summary>Primary text. #F0F6FC</summary>
        public static readonly Color TextMain = Hex(0xF0F6FC);
        /// <summary>Secondary text. #8B949E</summary>
        public static readonly Color TextSub = Hex(0x8B949E);
        /// <summary>Border/divider. #2E343D</summary>
        public static readonly Color Stroke = Hex(0x2E343D);
        /// <summary>Danger. #F85149</summary>
        public static readonly Color Danger = Hex(0xF85149);
        /// <summary>Success. #3FB950</summary>
        public static readonly Color Success = Hex(0x3FB950);
        /// <summary>Warning. #D29922</summary>
        public static readonly Color Warn = Hex(0xD29922);
        public static readonly Color DangerHover = Hex(0xDA3633);
        public static readonly Color AccentHover = Hex(0x79C0FF);
        public static readonly Color SuccessHover = Hex(0x57AB5A);

        public const int RadiusMain = 8;
        public const int RadiusSub = 6;
        public const int RadiusBadge = 4;
        public const float PanelBorderWidth = 1f;
        public const float PanelPadding = 10f;

        /// <summary>
        /// Creates a consistently styled panel suitable for cards and composite layouts.
        /// Use ApplyPanelStyle when styling an existing VisualElement instead.
        /// </summary>
        public static VisualElement CreatePanel(string name = null, bool secondary = false)
        {
            var panel = new VisualElement();
            if (!string.IsNullOrEmpty(name))
                panel.name = name;
            panel.AddToClassList("utk-panel");
            if (secondary)
                panel.AddToClassList("utk-panel-sub");
            ApplyPanelStyle(panel, secondary);
            return panel;
        }

        /// <summary>Applies shared panel tokens without requiring a stylesheet class.</summary>
        public static void ApplyPanelStyle(VisualElement panel, bool secondary = false)
        {
            if (panel == null)
                return;

            Color fill = secondary ? PanelSub : Panel;
            panel.style.backgroundColor = new StyleColor(fill);
            panel.style.color = new StyleColor(TextMain);
            panel.style.borderTopWidth = PanelBorderWidth;
            panel.style.borderBottomWidth = PanelBorderWidth;
            panel.style.borderLeftWidth = PanelBorderWidth;
            panel.style.borderRightWidth = PanelBorderWidth;
            panel.style.borderTopColor = new StyleColor(Stroke);
            panel.style.borderBottomColor = new StyleColor(Stroke);
            panel.style.borderLeftColor = new StyleColor(Stroke);
            panel.style.borderRightColor = new StyleColor(Stroke);
            panel.style.borderTopLeftRadius = secondary ? RadiusSub : RadiusMain;
            panel.style.borderTopRightRadius = secondary ? RadiusSub : RadiusMain;
            panel.style.borderBottomLeftRadius = secondary ? RadiusSub : RadiusMain;
            panel.style.borderBottomRightRadius = secondary ? RadiusSub : RadiusMain;
            panel.style.paddingLeft = PanelPadding;
            panel.style.paddingRight = PanelPadding;
            panel.style.paddingTop = PanelPadding;
            panel.style.paddingBottom = PanelPadding;
        }

        /// <summary>Shared theme chrome for windows explicitly opting into code styling.</summary>
        public static void ApplyWindowChrome(VisualElement window, VisualElement titleBar, VisualElement content)
        {
            if (window == null)
                return;

            window.style.backgroundColor = new StyleColor(Panel);
            window.style.color = new StyleColor(TextMain);
            window.style.borderTopWidth = PanelBorderWidth;
            window.style.borderBottomWidth = PanelBorderWidth;
            window.style.borderLeftWidth = PanelBorderWidth;
            window.style.borderRightWidth = PanelBorderWidth;
            window.style.borderTopColor = new StyleColor(Stroke);
            window.style.borderBottomColor = new StyleColor(Stroke);
            window.style.borderLeftColor = new StyleColor(Stroke);
            window.style.borderRightColor = new StyleColor(Stroke);
            window.style.borderTopLeftRadius = RadiusMain;
            window.style.borderTopRightRadius = RadiusMain;
            window.style.borderBottomLeftRadius = RadiusMain;
            window.style.borderBottomRightRadius = RadiusMain;

            if (titleBar != null)
            {
                titleBar.style.backgroundColor = new StyleColor(PanelSub);
                titleBar.style.borderBottomWidth = PanelBorderWidth;
                titleBar.style.borderBottomColor = new StyleColor(Stroke);
            }
            if (content != null)
                content.style.color = new StyleColor(TextMain);
        }

        private static Color Hex(uint rgb)
        {
            return new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }
    }
}

namespace ProjectName.UI.Toolkit
{
    /// <summary>Window presentation mode. Standard preserves the established Theme.uss chrome.</summary>
    public enum UTKWindowChrome
    {
        Standard,
        Frameless
    }
}
