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
        private static readonly Brush Panel = Freeze(new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x1A)));

        private readonly Border _headBox;
        private readonly TextBlock _headline;
        private readonly TextBlock _hint;
        private readonly TextBlock _details;
        private readonly bool _clickThrough;

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
                FontSize = 38,
                FontWeight = FontWeights.ExtraBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _hint = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _headBox = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(22, 6, 22, 6),
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

            var root = new StackPanel { Children = { _headBox, detailsBox } };
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
            if (v == null || v.Level == PanelLevel.Hidden) { HidePanel(); return; }

            _headBox.Background = v.Level == PanelLevel.Danger ? DarkRed : Red;
            _headline.Text = v.Headline;
            _hint.Text = (expanded ? "hide details: " : "details: ") + hotkeyName;
            _details.Text = v.Details ?? "";
            _details.Visibility = expanded && !string.IsNullOrEmpty(v.Details) ? Visibility.Visible : Visibility.Collapsed;
            if (!IsVisible) Show();
        }

        public void HidePanel()
        {
            if (IsVisible) Hide();
        }

        private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
    }
}
