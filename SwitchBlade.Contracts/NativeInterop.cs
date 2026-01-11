using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Collections.Concurrent;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Shared native interop methods for window management.
    /// Consolidates P/Invoke declarations used by both Core and Plugins.
    /// </summary>
    public static class NativeInterop
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #region user32.dll

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
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

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region dwmapi.dll

        [DllImport("dwmapi.dll")]
        public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [DllImport("dwmapi.dll")]
        public static extern int DwmUnregisterThumbnail(IntPtr thumb);

        [DllImport("dwmapi.dll")]
        public static extern int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DWM_THUMBNAIL_PROPERTIES props);

        #endregion

        #region kernel32.dll

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

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

        private static readonly ConcurrentDictionary<uint, string> _processNameCache = new();

        /// <summary>
        /// Clears the process name cache. Should be called before a fresh scan cycle
        /// to ensure that reused PIDs are re-resolved.
        /// </summary>
        public static void ClearProcessCache()
        {
            _processNameCache.Clear();
        }

        /// <summary>
        /// Retrieves the process name for a given PID using lightweight native APIs.
        /// Caches the result to avoid repeated lookups.
        /// </summary>
        public static string GetProcessName(uint pid)
        {
            if (pid == 0) return "System";

            // Return cached name if available
            if (_processNameCache.TryGetValue(pid, out var cachedName))
            {
                return cachedName;
            }

            string processName = "Unknown";
            IntPtr hProcess = IntPtr.Zero;

            try
            {
                // Open process with limited information access (much faster/lighter than Process class)
                hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);

                if (hProcess != IntPtr.Zero)
                {
                    StringBuilder buffer = new StringBuilder(1024);
                    int size = buffer.Capacity;

                    if (QueryFullProcessImageName(hProcess, 0, buffer, ref size))
                    {
                        var path = buffer.ToString();
                        processName = Path.GetFileNameWithoutExtension(path);
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

            // Cache the result (even if "Unknown", to avoid retrying failed PIDs constantly)
            // Note: PIDs are reused, so this cache might be stale if a process restarts with same PID.
            // For a long-running app, we might want to clear this occasionally or check if process start time matches?
            // For now, this is a significant optimization. PIDs recycle slowly enough for this to be fine.
            _processNameCache.TryAdd(pid, processName);

            return processName;
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

        #endregion
    }
}
