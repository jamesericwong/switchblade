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
        private IEnumerable<string> _dynamicExclusions = new List<string>();

        public override string PluginName => "WindowFinder";
        public override bool HasSettings => false;
        public override bool IsUiaProvider => false; // Uses EnumWindows, not UIA

        public WindowFinder() { }

        public override void SetExclusions(IEnumerable<string> exclusions)
        {
            _dynamicExclusions = exclusions;
        }

        public WindowFinder(ISettingsService settingsService)
        {
            _settingsService = settingsService;
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

            // Define callback as a local function to avoid lambda syntax issues with unsafe blocks
            unsafe bool EnumCallback(IntPtr hwnd, IntPtr lParam)
            {
                if (!NativeInterop.IsWindowVisible(hwnd))
                    return true;

                // Bleeding edge optimization: Use stackalloc for zero-allocation title retrieval
                // Max window title length is technically 256, but can be larger. 512 is safe.
                const int simplifyTitleBuffer = 512;
                char* buffer = stackalloc char[simplifyTitleBuffer];

                int length = NativeInterop.GetWindowTextUnsafe(hwnd, buffer, simplifyTitleBuffer);
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
                    NativeInterop.GetWindowThreadProcessId(hwnd, out pid);
                    if (pid != 0)
                    {
                        (processName, executablePath) = NativeInterop.GetProcessInfo(pid);
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

            NativeInterop.EnumWindows(EnumCallback, IntPtr.Zero);

            return results;
        }

        public override void ActivateWindow(WindowItem windowItem)
        {
            // Robust window activation using the improved helper
            NativeInterop.ForceForegroundWindow(windowItem.Hwnd);
        }
    }
}
