using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Copilot.Engine;

namespace Copilot.HdtPlugin
{
    /// <summary>
    /// Big headline (LETHAL / LETHAL? / DANGER) and an optional details block. No animations, never takes focus.
    /// Hidden whenever there is nothing worth saying.
    /// </summary>
    internal sealed class CopilotPanel : Window
    {
        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        private static readonly Brush Red = Freeze(new SolidColorBrush(Color.FromRgb(0xD6, 0x28, 0x28)));
        private static readonly Brush DarkRed = Freeze(new SolidColorBrush(Color.FromRgb(0x7A, 0x12, 0x12)));
        private static readonly Brush Blue = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x5F, 0xBF)));
        private static readonly Brush Panel = Freeze(new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x1A)));

        private readonly Border _headBox;
        private readonly TextBlock _headline;
        private readonly TextBlock _hint;
        private readonly TextBlock _details;
        private readonly bool _clickThrough;
        private readonly System.Windows.Shapes.Ellipse _dot;
        private readonly TextBlock _dotText;
        private readonly StackPanel _dotRow;
        private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71)));

        public event Action ToggleRequested;

        public CopilotPanel(PanelConfig cfg)
        {
            _clickThrough = cfg.ClickThrough;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            Left = cfg.Left;
            Top = cfg.Top;
            Opacity = cfg.Opacity;

            _headline = new TextBlock
            {
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 72,
                FontWeight = FontWeights.ExtraBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _hint = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _headBox = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(3),
                Padding = new Thickness(36, 8, 36, 10),
                Child = new StackPanel { Children = { _headline, _hint } }
            };
            _details = new TextBlock
            {
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Margin = new Thickness(10),
                Visibility = Visibility.Collapsed
            };
            var detailsBox = new Border { Background = Panel, CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 4, 0, 0), Child = _details };

            _dot = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Fill = Green, VerticalAlignment = VerticalAlignment.Center };
            _dotText = new TextBlock
            {
                Text = "Copilot", Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                FontFamily = new FontFamily("Segoe UI"), FontSize = 12, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
            };
            _dotRow = new StackPanel
            {
                Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 4),
                Children = { _dot, _dotText }
            };

            var root = new StackPanel { Children = { _dotRow, _headBox, detailsBox } };
            root.LayoutTransform = new ScaleTransform(cfg.Scale, cfg.Scale);
            Content = root;

            SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                int style = GetWindowLong(h, GwlExStyle) | WsExToolWindow | WsExNoActivate;
                if (_clickThrough) style |= WsExTransparent;
                SetWindowLong(h, GwlExStyle, style);
            };
            MouseLeftButtonUp += (s, e) => ToggleRequested?.Invoke();
            Cursor = Cursors.Hand;
        }

        public void Render(PanelView v, bool expanded, string hotkeyName)
        {
            // Status dot is always visible during a match so a hidden banner never looks like a dead plugin.
            if (v == null || v.Level == PanelLevel.Hidden)
            {
                _headBox.Visibility = Visibility.Collapsed;
                _details.Visibility = Visibility.Collapsed;
                if (!IsVisible) Show();
                return;
            }

            _headBox.Visibility = Visibility.Visible;
            _headBox.Background = v.Level == PanelLevel.Danger ? DarkRed : v.Level == PanelLevel.Mulligan ? Blue : Red;
            expanded = expanded || v.AutoExpand;
            _headline.Text = v.Headline;
            _headline.FontSize = v.Level == PanelLevel.Mulligan ? 34 : 72;
            _hint.Text = v.AutoExpand ? "mulligan advice" : (expanded ? "hide details: " : "details: ") + hotkeyName;
            _details.Text = v.Details ?? "";
            _details.Visibility = expanded && !string.IsNullOrEmpty(v.Details) ? Visibility.Visible : Visibility.Collapsed;
            if (!IsVisible) Show();
        }

        /// <summary>Show only the green status dot (match running, nothing to report).</summary>
        public void ShowIdle()
        {
            if (IsVisible) return;
            _headBox.Visibility = Visibility.Collapsed;
            _details.Visibility = Visibility.Collapsed;
            Show();
        }

        public void HidePanel()
        {
            if (IsVisible) Hide();
        }

        private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
    }
}
