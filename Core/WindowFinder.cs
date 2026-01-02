using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace SwitchBlade.Core
{
    public class WindowFinder : IWindowProvider
    {
        private readonly Services.SettingsService _settingsService;

        public WindowFinder(Services.SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public IEnumerable<WindowItem> GetWindows()
        {
            var results = new List<WindowItem>();
            var excluded = new HashSet<string>(_settingsService.Settings.ExcludedProcesses, StringComparer.OrdinalIgnoreCase);

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
                    Logger.Log($"Excluded Window '{title}' from process '{processName}'");
                    return true;
                }
                
                // Debug log
                Logger.Log($"Included Window: '{title}', Process: '{processName}'");

                results.Add(new WindowItem
                {
                    Hwnd = hwnd,
                    Title = title,
                    ProcessName = processName,
                    IsChromeTab = false
                });

                return true;
            }, IntPtr.Zero);

            return results;
        }
    }
}
