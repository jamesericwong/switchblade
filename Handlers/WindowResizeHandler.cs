using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Handlers
{
    /// <summary>
    /// Handles window resize grip interactions.
    /// Extracted from MainWindow.xaml.cs for Single Responsibility Principle.
    /// </summary>
    public class WindowResizeHandler
    {
        private readonly Window _window;
        private readonly ILogger _logger;

        public WindowResizeHandler(Window window, ILogger logger)
        {
            _window = window;
            _logger = logger;
        }

        public void HandleBottomRightGripMouseDown(object sender, MouseButtonEventArgs e)
        {
            _logger.Log($"Resize Grip (Bottom-Right) Clicked. ButtonState: {e.ButtonState}");
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    // Manual Resize via System Command
                    NativeInterop.SendMessage(new WindowInteropHelper(_window).Handle,
                                        NativeInterop.WM_SYSCOMMAND,
                                        (IntPtr)(NativeInterop.SC_SIZE + NativeInterop.SC_SIZE_HTBOTTOMRIGHT),
                                        IntPtr.Zero);
                    _logger.Log("Sent SC_SIZE + HTBOTTOMRIGHT command.");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Resize Grip Error", ex);
                }
            }
        }

        public void HandleBottomLeftGripMouseDown(object sender, MouseButtonEventArgs e)
        {
            _logger.Log($"Resize Grip (Bottom-Left) Clicked. ButtonState: {e.ButtonState}");
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    // Manual Resize via System Command - Bottom-Left corner
                    NativeInterop.SendMessage(new WindowInteropHelper(_window).Handle,
                                        NativeInterop.WM_SYSCOMMAND,
                                        (IntPtr)(NativeInterop.SC_SIZE + NativeInterop.SC_SIZE_HTBOTTOMLEFT),
                                        IntPtr.Zero);
                    _logger.Log("Sent SC_SIZE + HTBOTTOMLEFT command.");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Resize Grip Error", ex);
                }
            }
        }
    }
}
