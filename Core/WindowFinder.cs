using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using SwitchBlade.Contracts;
using SwitchBlade.Services;

namespace SwitchBlade.Core
{
    public class WindowFinder : CachingWindowProviderBase
    {
        private ISettingsService? _settingsService;
        private readonly IWindowInterop _interop;
        private IEnumerable<string> _dynamicExclusions = new List<string>();

        public override string PluginName => "WindowFinder";
        public override bool HasSettings => false;
        public override bool IsUiaProvider => false; // Uses EnumWindows, not UIA

        public WindowFinder() : this(null, null) { }

        public override void SetExclusions(IEnumerable<string> exclusions)
        {
            _dynamicExclusions = exclusions;
        }

        public WindowFinder(ISettingsService? settingsService, IWindowInterop? interop = null)
        {
            _settingsService = settingsService;
            _interop = interop ?? new WindowInterop();
        }

        public override void Initialize(IPluginContext context)
        {
            base.Initialize(context);
            // Note: SettingsService is now injected via constructor, not through Initialize
        }

        public override void ReloadSettings()
        {
            // No plugin-specific settings to reload
        }


        protected override IEnumerable<WindowItem> ScanWindowsCore()
        {
            var results = new List<WindowItem>();
            if (_settingsService == null) return results; // Add safety

            var excluded = new HashSet<string>(_settingsService.Settings.ExcludedProcesses, StringComparer.OrdinalIgnoreCase);

            // Note: Browser processes are now managed by the ChromeTabFinder plugin.
            // To prevent duplicate windows, add browser process names to ExcludedProcesses in Settings.

            unsafe bool EnumCallback(IntPtr hwnd, IntPtr lParam)
            {
                if (!_interop.IsWindowVisible(hwnd))
                    return true;

                // Bleeding edge optimization: Use stackalloc for zero-allocation title retrieval
                // Max window title length is technically 256, but can be larger. 512 is safe.
                const int simplifyTitleBuffer = 512;
                char* buffer = stackalloc char[simplifyTitleBuffer];

                int length = _interop.GetWindowTextUnsafe(hwnd, (IntPtr)buffer, simplifyTitleBuffer);
                if (length == 0)
                    return true;

                // Perform "Program Manager" check without allocating string
                // Check if starts with "Program Manager" (length 15)
                if (length == 15)
                {
                    // Fast manual check
                    // "Program Manager"
                    bool match = true;
                    string pm = "Program Manager";
                    for (int i = 0; i < 15; i++)
                    {
                        if (buffer[i] != pm[i]) { match = false; break; }
                    }
                    if (match) return true;
                }

                // Get Process Name and Path (Optimized Interop handles caching and minimal allocations internally)
                string processName = "Window";
                string? executablePath = null;
                try
                {
                    uint pid;
                    _interop.GetWindowThreadProcessId(hwnd, out pid);
                    if (pid != 0)
                    {
                        (processName, executablePath) = _interop.GetProcessInfo(pid);
                    }
                }
                catch
                {
                    // Ignore access denied errors etc.
                }

                // fast-path rejection before allocating title string
                if (excluded.Contains(processName) || _dynamicExclusions.Contains(processName, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Only allocate string if we are keeping the window
                string title = new string(buffer, 0, length);

                // Debug log
                base.Logger?.Log($"Included Window: '{title}', Process: '{processName}' (Exclusions: {string.Join(",", _dynamicExclusions)})");

                results.Add(new WindowItem
                {
                    Hwnd = hwnd,
                    Title = title,
                    ProcessName = processName,
                    ExecutablePath = executablePath,
                    Source = this
                });

                return true;
            }

            _interop.EnumWindows(EnumCallback, IntPtr.Zero);

            return results;
        }

        public override void ActivateWindow(WindowItem windowItem)
        {
            // Robust window activation using the improved helper
            _interop.ForceForegroundWindow(windowItem.Hwnd);
        }

        protected override int GetPid(IntPtr hwnd)
        {
            try
            {
                _interop.GetWindowThreadProcessId(hwnd, out uint pid);
                return (int)pid != 0 ? (int)pid : -1;
            }
            catch
            {
                return -1;
            }
        }

        protected override (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid)
        {
             if ((int)pid == -1) return ("Window", null);
             
             try
             {
                return _interop.GetProcessInfo(pid);
             }
             catch
             {
                return ("Window", null);
             }
        }

        protected override bool IsWindowValid(IntPtr hwnd)
        {
            return _interop.IsWindowVisible(hwnd);
        }
    }
}
