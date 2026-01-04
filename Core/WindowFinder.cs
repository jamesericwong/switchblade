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
        private SettingsService? _settingsService;
        private IEnumerable<string> _dynamicExclusions = new List<string>();

        public override string PluginName => "WindowFinder";
        public override bool HasSettings => false;

        public WindowFinder() { }

        public override void SetExclusions(IEnumerable<string> exclusions)
        {
            _dynamicExclusions = exclusions;
        }

        public WindowFinder(SettingsService settingsService)
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

        public override void ShowSettingsDialog(IntPtr ownerHwnd)
        {
            // No settings dialog for core WindowFinder
        }


        protected override IEnumerable<WindowItem> ScanWindowsCore()
        {
            var results = new List<WindowItem>();
            if (_settingsService == null) return results; // Add safety

            var excluded = new HashSet<string>(_settingsService.Settings.ExcludedProcesses, StringComparer.OrdinalIgnoreCase);

            // Note: Browser processes are now managed by the ChromeTabFinder plugin.
            // To prevent duplicate windows, add browser process names to ExcludedProcesses in Settings.

            NativeInterop.EnumWindows((hwnd, lParam) =>
            {
                if (!NativeInterop.IsWindowVisible(hwnd))
                    return true;

                StringBuilder sb = new StringBuilder(256);
                NativeInterop.GetWindowText(hwnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (string.IsNullOrWhiteSpace(title))
                    return true;

                // Simple filter to remove common system windows usually not interesting to user
                if (title == "Program Manager") return true;

                // Get Process Name
                string processName = "Window";
                try
                {
                    uint pid;
                    NativeInterop.GetWindowThreadProcessId(hwnd, out pid);
                    if (pid != 0)
                    {
                        var proc = Process.GetProcessById((int)pid);
                        processName = proc.ProcessName;
                    }
                }
                catch
                {
                    // Ignore access denied errors etc.
                }

                // Filter Excluded Processes
                if (excluded.Contains(processName) || _dynamicExclusions.Contains(processName, StringComparer.OrdinalIgnoreCase))
                {
                    // Do not log "excluded" for browsers to reduce noise, or log as debug if needed
                    base.Logger?.Log($"Excluded Window '{title}' from process '{processName}' (Matched Exclusion)");
                    return true;
                }

                // Debug log
                base.Logger?.Log($"Included Window: '{title}', Process: '{processName}' (Exclusions: {string.Join(",", _dynamicExclusions)})");

                results.Add(new WindowItem
                {
                    Hwnd = hwnd,
                    Title = title,
                    ProcessName = processName,
                    Source = this
                });

                return true;
            }, IntPtr.Zero);

            return results;
        }

        public override void ActivateWindow(WindowItem windowItem)
        {
            // Robust window activation using the improved helper
            NativeInterop.ForceForegroundWindow(windowItem.Hwnd);
        }
    }
}
