using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using SwitchBlade.Contracts;
using SwitchBlade.Services;

namespace SwitchBlade.Core
{
    public class WindowFinder : IWindowProvider
    {
        private SettingsService? _settingsService;

        public string PluginName => "WindowFinder";
        public bool HasSettings => false;

        public WindowFinder() { }

        public WindowFinder(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public void Initialize(object settingsService, ILogger logger)
        {
           if (settingsService is SettingsService service)
           {
               _settingsService = service;
           }
        }

        public void ReloadSettings()
        {
            // No plugin-specific settings to reload
        }

        public void ShowSettingsDialog(IntPtr ownerHwnd)
        {
            // No settings dialog for core WindowFinder
        }


        public IEnumerable<WindowItem> GetWindows()
        {
            var results = new List<WindowItem>();
            if (_settingsService == null) return results; // Add safety

            var excluded = new HashSet<string>(_settingsService.Settings.ExcludedProcesses, StringComparer.OrdinalIgnoreCase);
            
            // Note: Browser processes are now managed by the ChromeTabFinder plugin.
            // To prevent duplicate windows, add browser process names to ExcludedProcesses in Settings.

            Interop.EnumWindows((hwnd, lParam) =>
            {
                if (!Interop.IsWindowVisible(hwnd))
                    return true;

                StringBuilder sb = new StringBuilder(256);
                Interop.GetWindowText(hwnd, sb, sb.Capacity);
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
                    Interop.GetWindowThreadProcessId(hwnd, out pid);
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
                if (excluded.Contains(processName))
                {
                    // Do not log "excluded" for browsers to reduce noise, or log as debug if needed
                    // Logger.Log($"Excluded Window '{title}' from process '{processName}'"); 
                    return true;
                }
                
                // Debug log
                Logger.Log($"Included Window: '{title}', Process: '{processName}'");

                results.Add(new WindowItem
                {
                    Hwnd = hwnd,
                    Title = title,
                    ProcessName = processName,
                    IsChromeTab = false,
                    Source = this
                });

                return true;
            }, IntPtr.Zero);

            return results;
        }

        public void ActivateWindow(WindowItem windowItem)
        {
             // Robust window activation using the improved helper
            Interop.ForceForegroundWindow(windowItem.Hwnd);
        }
    }
}
