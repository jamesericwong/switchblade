using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SwitchBlade.Contracts;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace SwitchBlade.Plugins.Chrome
{
    public class ChromeTabFinder : CachingWindowProviderBase
    {
        private ILogger? _logger;
        private IPluginSettingsService? _settingsService;
        private List<string> _browserProcesses = new();

        private static readonly List<string> DefaultBrowserProcesses = new()
        {
            "chrome",
            "msedge",
            "brave",
            "vivaldi",
            "opera",
            "opera_gx",
            "chromium",
            "thorium",
            "iron",
            "epic",
            "yandex",
            "arc",
            "comet"
        };

        public override string PluginName => "ChromeTabFinder";
        public override bool HasSettings => true;

        public ChromeTabFinder() { }

        public ChromeTabFinder(IPluginSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public override void Initialize(IPluginContext context)
        {
            base.Initialize(context);
            _logger = context.Logger;

            if (_settingsService == null)
            {
                _settingsService = new PluginSettingsService(PluginName);
            }
            ReloadSettings();
        }

        public override void ReloadSettings()
        {
            if (_settingsService == null) return;
            if (_settingsService.KeyExists("BrowserProcesses"))
            {
                _browserProcesses = _settingsService.GetStringList("BrowserProcesses", DefaultBrowserProcesses);
            }
            else
            {
                _browserProcesses = new List<string>(DefaultBrowserProcesses);
                _settingsService.SetStringList("BrowserProcesses", _browserProcesses);
            }
            _logger?.Log($"ChromeTabFinder: Loaded {_browserProcesses.Count} browser processes");
        }

        public override void ShowSettingsDialog(IntPtr ownerHwnd)
        {
            try
            {
                var dialog = new ChromeSettingsWindow(_settingsService!, _browserProcesses);
                dialog.Activate();
                dialog.Closed += (s, e) => ReloadSettings();
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to open Chrome Settings Window", ex);
            }
        }

        public override IEnumerable<string> GetHandledProcesses()
        {
            return _browserProcesses;
        }

        protected override IEnumerable<WindowItem> ScanWindowsCore()
        {
            var results = new List<WindowItem>();
            if (_settingsService == null || _browserProcesses.Count == 0) return results;

            var targetProcessNames = new HashSet<string>(_browserProcesses, StringComparer.OrdinalIgnoreCase);
            var targetPids = new HashSet<int>();

            foreach (var name in targetProcessNames)
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    targetPids.Add(p.Id);
                }
            }

            using var automation = new UIA3Automation();

            NativeInterop.EnumWindows((hwnd, lParam) =>
            {
                if (!NativeInterop.IsWindowVisible(hwnd)) return true;

                NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
                if (targetPids.Contains((int)pid))
                {
                    ScanWindow(hwnd, (int)pid, automation, results);
                }
                return true;
            }, IntPtr.Zero);

            return results;
        }

        private void ScanWindow(IntPtr hwnd, int pid, UIA3Automation automation, List<WindowItem> results)
        {
            AutomationElement? windowElement = null;
            try
            {
                windowElement = automation.FromHandle(hwnd);
            }
            catch { return; }

            if (windowElement == null) return;

            string processName = "Unknown";
            try { processName = Process.GetProcessById(pid).ProcessName; } catch { }

            // _logger?.Log($"Scanning Window HWND: {hwnd} (PID: {pid}, Name: {processName})");

            var foundTabs = FindTabs(windowElement, automation);

            if (foundTabs.Count == 0)
            {
                string title = windowElement.Name;
                if (!string.IsNullOrEmpty(title))
                {
                    results.Add(new WindowItem
                    {
                        Hwnd = hwnd,
                        Title = title,
                        ProcessName = processName,
                        Source = this
                    });
                }
            }
            else
            {
                foreach (var tabName in foundTabs)
                {
                    results.Add(new WindowItem
                    {
                        Hwnd = hwnd,
                        Title = tabName,
                        ProcessName = processName,
                        Source = this
                    });
                }
            }
        }

        private List<string> FindTabs(AutomationElement root, UIA3Automation automation)
        {
            var results = new List<string>();
            try
            {
                var tabs = root.FindAll(TreeScope.Descendants, new PropertyCondition(automation.PropertyLibrary.Element.ControlType, ControlType.TabItem));
                foreach (var tab in tabs)
                {
                    if (tab.Name != "New Tab" && tab.Name != "+" && !string.IsNullOrWhiteSpace(tab.Name))
                    {
                        results.Add(tab.Name);
                    }
                }
            }
            catch { }
            return results;
        }

        public override void ActivateWindow(WindowItem item)
        {
            NativeInterop.ForceForegroundWindow(item.Hwnd);
            System.Threading.Thread.Sleep(50);

            if (string.IsNullOrEmpty(item.Title)) return;

            try
            {
                using var automation = new UIA3Automation();
                var root = automation.FromHandle(item.Hwnd);
                if (root == null) return;

                var tab = root.FindFirst(TreeScope.Descendants, new PropertyCondition(automation.PropertyLibrary.Element.Name, item.Title)
                    .And(new PropertyCondition(automation.PropertyLibrary.Element.ControlType, ControlType.TabItem)));

                if (tab != null)
                {
                    if (tab.Patterns.SelectionItem.TryGetPattern(out var selectPattern))
                    {
                        selectPattern.Select();
                    }
                    else if (tab.Patterns.Invoke.TryGetPattern(out var invokePattern))
                    {
                        invokePattern.Invoke();
                    }
                    else
                    {
                        tab.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error activating tab '{item.Title}'", ex);
            }
        }
    }
}
