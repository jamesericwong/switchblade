using System;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Handlers
{
    /// <summary>
    /// Handles window resize grip interactions - WinUI version.
    /// Uses native P/Invoke for resize operations.
    /// </summary>
    public class WindowResizeHandler
    {
        private readonly Window _window;
        private readonly ILogger _logger;
        private readonly IntPtr _hwnd;

        public WindowResizeHandler(Window window, ILogger logger)
        {
            _window = window;
            _logger = logger;
            _hwnd = WindowNative.GetWindowHandle(window);
        }

        public void HandleBottomRightGripMouseDown()
        {
            _logger.Log("Resize Grip (Bottom-Right) Clicked");
            try
            {
                // Manual Resize via System Command
                NativeInterop.SendMessage(_hwnd,
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

        public void HandleBottomLeftGripMouseDown()
        {
            _logger.Log("Resize Grip (Bottom-Left) Clicked");
            try
            {
                // Manual Resize via System Command - Bottom-Left corner
                NativeInterop.SendMessage(_hwnd,
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
