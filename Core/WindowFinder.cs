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
            if (_settingsService == null) return results;

            var excluded = new HashSet<string>(_settingsService.Settings.ExcludedProcesses, StringComparer.OrdinalIgnoreCase);

            // Explicit delegate to prevent GC issues
            NativeInterop.EnumWindowsProc proc = (hwnd, lParam) =>
            {
                if (!NativeInterop.IsWindowVisible(hwnd)) return true;

                StringBuilder sb = new StringBuilder(256);
                NativeInterop.GetWindowText(hwnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (string.IsNullOrWhiteSpace(title)) return true;
                if (title == "Program Manager") return true;

                string processName = "Unknown_Debug";

                // Filter Excluded Processes (simplified)
                if (excluded.Contains(processName)) return true;

                // TEST: Instantiate but DO NOT Add
                var item = new WindowItem
                {
                    Hwnd = hwnd,
                    Title = title,
                    ProcessName = processName,
                    Source = null
                };

                // results.Add(item);

                return true;
            };

            NativeInterop.EnumWindows(proc, IntPtr.Zero);

            return results;
        }

        public override void ActivateWindow(WindowItem windowItem)
        {
            // Robust window activation using the improved helper
            NativeInterop.ForceForegroundWindow(windowItem.Hwnd);
        }
    }
}
