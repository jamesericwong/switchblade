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

        /// <summary>
        /// Creates a new window resize handler.
        /// </summary>
        /// <param name="window">The window to handle resizing for.</param>
        public WindowResizeHandler(Window window)
        {
            _window = window;
        }

        /// <summary>
        /// Handles the bottom-right resize grip mouse down event.
        /// </summary>
        public void HandleBottomRightGripMouseDown(object sender, MouseButtonEventArgs e)
        {
            Logger.Log($"Resize Grip (Bottom-Right) Clicked. ButtonState: {e.ButtonState}");
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    // Manual Resize via System Command
                    NativeInterop.SendMessage(new WindowInteropHelper(_window).Handle,
                                        NativeInterop.WM_SYSCOMMAND,
                                        (IntPtr)(NativeInterop.SC_SIZE + NativeInterop.SC_SIZE_HTBOTTOMRIGHT),
                                        IntPtr.Zero);
                    Logger.Log("Sent SC_SIZE + HTBOTTOMRIGHT command.");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Resize Grip Error", ex);
                }
            }
        }

        /// <summary>
        /// Handles the bottom-left resize grip mouse down event.
        /// </summary>
        public void HandleBottomLeftGripMouseDown(object sender, MouseButtonEventArgs e)
        {
            Logger.Log($"Resize Grip (Bottom-Left) Clicked. ButtonState: {e.ButtonState}");
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    // Manual Resize via System Command - Bottom-Left corner
                    NativeInterop.SendMessage(new WindowInteropHelper(_window).Handle,
                                        NativeInterop.WM_SYSCOMMAND,
                                        (IntPtr)(NativeInterop.SC_SIZE + NativeInterop.SC_SIZE_HTBOTTOMLEFT),
                                        IntPtr.Zero);
                    Logger.Log("Sent SC_SIZE + HTBOTTOMLEFT command.");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Resize Grip Error", ex);
                }
            }
        }
    }
}
