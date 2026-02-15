using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

[assembly: DisableRuntimeMarshalling]

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Shared native interop methods for window management.
    /// Consolidates P/Invoke declarations used by both Core and Plugins.
    /// Modernized for .NET 9+ high performance scenarios.
    /// </summary>
    public static partial class NativeInterop
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #region user32.dll

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        // Optimized for zero-allocation when used with stackalloc or Span
        [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", SetLastError = true)]
        public static partial int GetWindowText(IntPtr hWnd, Span<char> lpString, int nMaxCount);

        // Unsafe overload for maximum speed with stack pointers
        [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", SetLastError = true)]
        public static unsafe partial int GetWindowTextUnsafe(IntPtr hWnd, char* lpString, int nMaxCount);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindowVisible(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        public static partial IntPtr GetForegroundWindow();

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetForegroundWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool BringWindowToTop(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsIconic(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DestroyIcon(IntPtr hIcon);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial void SwitchToThisWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAltTab);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetClientRect(IntPtr hWnd, out Rect lpRect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
        public static partial IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll", SetLastError = true)]
        private static partial IntPtr OpenInputDesktop(uint dwFlags, [MarshalAs(UnmanagedType.Bool)] bool fInherit, uint dwDesiredAccess);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseDesktop(IntPtr hDesktop);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        public static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        public static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const int WS_CAPTION = 0x00C00000;
        public const int WS_SYSMENU = 0x00080000;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;
        public const uint GW_OWNER = 4;

        #endregion

        #region shell32.dll

        [LibraryImport("shell32.dll", EntryPoint = "ExtractIconExW", StringMarshalling = StringMarshalling.Utf16)]
        public static partial uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        #endregion

        #region dwmapi.dll

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmUnregisterThumbnail(IntPtr thumb);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DWM_THUMBNAIL_PROPERTIES props);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        #endregion

        #region kernel32.dll

        [LibraryImport("kernel32.dll")]
        public static partial uint GetCurrentThreadId();

        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial IntPtr OpenProcess(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CloseHandle(IntPtr hObject);

        [LibraryImport("kernel32.dll", SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, Span<char> lpExeName, ref int lpdwSize);

        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public Rect rcDestination;
            public Rect rcSource;
            public byte opacity;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fVisible;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fSourceClientAreaOnly;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public Point ptMinPosition;
            public Point ptMaxPosition;
            public Rect rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        #endregion

        #region Constants

        // Window Management
        public const int SW_RESTORE = 9;
        public const int WA_ACTIVE = 1;
        public const int WA_CLICKACTIVE = 2;

        // Modifiers
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;

        // DWM
        public const int DWM_TNP_RECTDESTINATION = 0x00000001;
        public const int DWM_TNP_VISIBLE = 0x00000008;
        public const int DWM_TNP_OPACITY = 0x00000004;
        public const int DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        // DWM Backdrop Types
        public const int DWM_BACKDROP_NONE = 0;
        public const int DWM_BACKDROP_MICA = 2;
        public const int DWM_BACKDROP_ACRYLIC = 3;
        public const int DWM_BACKDROP_TABBED = 4;

        // DWM Window Corner Preferences
        public const int DWMWCP_DEFAULT = 0;
        public const int DWMWCP_DONOTROUND = 1;
        public const int DWMWCP_ROUND = 2;
        public const int DWMWCP_ROUNDSMALL = 3;

        // SysCommands
        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_SIZE = 0xF000;
        public const int SC_SIZE_HTBOTTOMRIGHT = 8;
        public const int SC_SIZE_HTBOTTOMLEFT = 7;
        public const int HTBOTTOMRIGHT = 17;

        // Process Access Flags
        public const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const int PROCESS_QUERY_INFORMATION = 0x0400;
        public const int PROCESS_VM_READ = 0x0010;

        #endregion

        #region Helper Methods

        private static readonly ConcurrentDictionary<uint, (string ProcessName, string? ExecutablePath)> _processInfoCache = new();

        /// <summary>
        /// Clears the process info cache. Should be called before a fresh scan cycle
        /// to ensure that reused PIDs are re-resolved.
        /// </summary>
        public static void ClearProcessCache()
        {
            _processInfoCache.Clear();
        }

        /// <summary>
        /// Retrieves both the process name and full executable path for a given PID.
        /// Caches the result to avoid repeated lookups within a scan cycle.
        /// </summary>
        public static (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid)
        {
            if (pid == 0) return ("System", null);

            // Check cache first
            if (_processInfoCache.TryGetValue(pid, out var cached))
            {
                return cached;
            }

            string processName = "Unknown";
            string? executablePath = null;
            IntPtr hProcess = IntPtr.Zero;

            try
            {
                hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);

                if (hProcess != IntPtr.Zero)
                {
                    Span<char> buffer = stackalloc char[1024];
                    int size = buffer.Length;

                    if (QueryFullProcessImageName(hProcess, 0, buffer, ref size))
                    {
                        executablePath = new string(buffer[..size]);
                        processName = Path.GetFileNameWithoutExtension(buffer[..size]).ToString();
                    }
                }
            }
            catch
            {
                // Ignore errors (access denied, etc.)
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }

            var result = (processName, executablePath);
            _processInfoCache.TryAdd(pid, result);

            return result;
        }

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

        /// <summary>
        /// Determines whether the workstation is currently locked by attempting
        /// to open the interactive input desktop. When the desktop is locked,
        /// the input desktop switches to the secure Winlogon desktop, making
        /// the user's interactive desktop inaccessible.
        /// </summary>
        /// <returns>True if the workstation appears to be locked.</returns>
        public static bool IsWorkstationLocked()
        {
            // DESKTOP_SWITCHDESKTOP (0x0100) is the minimum access right needed.
            // If the user's input desktop is the active one, this succeeds.
            // If locked, the input desktop is Winlogon and OpenInputDesktop fails.
            const uint DESKTOP_SWITCHDESKTOP = 0x0100;

            IntPtr hDesktop = IntPtr.Zero;
            try
            {
                hDesktop = OpenInputDesktop(0, false, DESKTOP_SWITCHDESKTOP);
                return hDesktop == IntPtr.Zero;
            }
            finally
            {
                if (hDesktop != IntPtr.Zero)
                {
                    CloseDesktop(hDesktop);
                }
            }
        }

        #endregion
    }
}
