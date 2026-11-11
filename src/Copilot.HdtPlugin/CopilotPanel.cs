using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Copilot.HdtPlugin
{
    /// <summary>Small always-on-top, click-through text panel. No animations, never takes focus.</summary>
    internal sealed class CopilotPanel : Window
    {
        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        private readonly TextBlock _text;

        public CopilotPanel()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(200, 14, 16, 24));
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            Left = 24;
            Top = 140;
            _text = new TextBlock
            {
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Margin = new Thickness(10)
            };
            Content = _text;
            SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                SetWindowLong(h, GwlExStyle, GetWindowLong(h, GwlExStyle) | WsExTransparent | WsExToolWindow | WsExNoActivate);
            };
        }

        public void SetText(string text)
        {
            _text.Text = text;
            if (!IsVisible) Show();
        }

        public void HidePanel()
        {
            if (IsVisible) Hide();
        }
    }
}
