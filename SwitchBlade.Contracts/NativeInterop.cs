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

        // Optimized for zero-allocation when used with stackalloc
        [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

        // Unsafe overload for maximum speed with stack pointers
        [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", SetLastError = true)]
        public static unsafe partial int GetWindowTextUnsafe(IntPtr hWnd, char* lpString, int nMaxCount);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindowVisible(IntPtr hWnd);

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

        [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
        public static partial IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region dwmapi.dll

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmUnregisterThumbnail(IntPtr thumb);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DWM_THUMBNAIL_PROPERTIES props);

        #endregion

        #region kernel32.dll

        [LibraryImport("kernel32.dll")]
        public static partial uint GetCurrentThreadId();

        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial IntPtr OpenProcess(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CloseHandle(IntPtr hObject);

        [LibraryImport("kernel32.dll", SetLastError = true, EntryPoint = "QueryFullProcessImageNameW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, [Out] char[] lpExeName, ref int lpdwSize);

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
                    // Use a small buffer on the stack to avoid allocations
                    // MAX_PATH is usually 260, but NTFS allows 32k. 
                    // 1024 chars (2KB) on stack is safe and covers 99.9% of cases.
                    char[] buffer = new char[1024]; // Used with P/Invoke that expects array
                    int size = buffer.Length;

                    if (QueryFullProcessImageName(hProcess, 0, buffer, ref size))
                    {
                        // Create string only from the valid part
                        // Path.GetFileNameWithoutExtension is handy but does allocation.
                        // We can optimize if we really want to, but standard Path methods are robust.
                        // For extreme optimization: manually find last separator.

                        // We need a string key for the Dictionary anyway, so one allocation is inevitable
                        // unless we use a custom string-interning pool, which is overkill here.
                        var path = new string(buffer, 0, size);
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

            // Cache the result
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
