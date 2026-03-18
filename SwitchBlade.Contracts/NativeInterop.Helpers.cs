using System;
using System.Collections.Concurrent;
using System.IO;

namespace SwitchBlade.Contracts
{
    public static partial class NativeInterop
    {
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
    }
}
