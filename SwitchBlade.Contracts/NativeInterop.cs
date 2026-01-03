using System;
using System.Runtime.InteropServices;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Shared native interop methods for window management.
    /// Consolidates P/Invoke declarations used by both Core and Plugins.
    /// </summary>
    public static class NativeInterop
    {
        /// <summary>Delegate for EnumWindows callback.</summary>
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #region user32.dll

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        #endregion

        #region kernel32.dll

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        #endregion

        #region Constants

        /// <summary>Restore a minimized window.</summary>
        public const int SW_RESTORE = 9;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Forcibly brings a window to the foreground, even if another application
        /// currently has focus. Handles minimized windows and thread input attachment.
        /// </summary>
        /// <param name="hwnd">Handle to the window to activate.</param>
        public static void ForceForegroundWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            // 1. Restore if minimized
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }

            // 2. Attach thread input if necessary to steal focus
            // This is required because Windows prevents applications from stealing focus
            // unless they are already in the foreground or have special permission.
            uint foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            uint myThreadId = GetCurrentThreadId();
            bool threadsAttached = false;

            if (foregroundThreadId != myThreadId)
            {
                threadsAttached = AttachThreadInput(myThreadId, foregroundThreadId, true);
            }

            // 3. Bring to top and set foreground
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);

            // 4. Detach
            if (threadsAttached)
            {
                AttachThreadInput(myThreadId, foregroundThreadId, false);
            }
        }

        #endregion
    }
}
