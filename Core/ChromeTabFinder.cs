using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;
using SwitchBlade.Services;

namespace SwitchBlade.Core
{
    public class ChromeTabFinder : IWindowProvider
    {
        private readonly SettingsService _settingsService;

        public ChromeTabFinder(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public IEnumerable<WindowItem> GetWindows()
        {
            var results = new List<WindowItem>();
            // Use the processes defined in user settings
            var processesToScan = _settingsService.Settings.BrowserProcesses;

            foreach (var processName in processesToScan)
            {
                Process[] processes = Process.GetProcessesByName(processName);

                foreach (var proc in processes)
                {
                    if (proc.MainWindowHandle == IntPtr.Zero) continue;
                    if (!Interop.IsWindowVisible(proc.MainWindowHandle)) continue;

                    try
                    {
                        AutomationElement root = AutomationElement.FromHandle(proc.MainWindowHandle);
                        if (root == null) continue;

                        var tabCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
                        var tabs = root.FindAll(TreeScope.Descendants, tabCondition);

                        foreach (AutomationElement tab in tabs)
                        {
                            try
                            {
                                string name = tab.Current.Name;
                                if (string.IsNullOrWhiteSpace(name)) continue;

                                results.Add(new WindowItem
                                {
                                    Hwnd = proc.MainWindowHandle,
                                    Title = name,
                                    ProcessName = proc.ProcessName,
                                    IsChromeTab = true
                                });
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            return results;
        }
    }
}
