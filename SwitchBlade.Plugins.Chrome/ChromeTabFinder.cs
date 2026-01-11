using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Interop;
using SwitchBlade.Contracts;

namespace SwitchBlade.Plugins.Chrome
{
    public class ChromeTabFinder : CachingWindowProviderBase
    {
        private ILogger? _logger;
        private IPluginSettingsService? _settingsService;
        private List<string> _browserProcesses = new();

        // Default browser processes if no settings exist
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

        public ChromeTabFinder()
        {
        }

        /// <summary>
        /// Constructor for unit testing with mocked settings.
        /// </summary>
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

            // Initialize settings from Registry or use defaults
            ReloadSettings();
        }

        public override void ReloadSettings()
        {
            if (_settingsService == null) return;

            // Check if BrowserProcesses key exists in plugin Registry
            if (_settingsService.KeyExists("BrowserProcesses"))
            {
                _browserProcesses = _settingsService.GetStringList("BrowserProcesses", DefaultBrowserProcesses);
            }
            else
            {
                // First run or missing key - use defaults and save them
                _browserProcesses = new List<string>(DefaultBrowserProcesses);
                _settingsService.SetStringList("BrowserProcesses", _browserProcesses);
            }

            _logger?.Log($"ChromeTabFinder: Loaded {_browserProcesses.Count} browser processes");
        }

        public override void ShowSettingsDialog(IntPtr ownerHwnd)
        {
            var dialog = new ChromeSettingsWindow(_settingsService!, _browserProcesses);
            if (ownerHwnd != IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(dialog);
                helper.Owner = ownerHwnd;
            }
            dialog.ShowDialog();

            // Reload settings after dialog closes
            ReloadSettings();
        }

        public override IEnumerable<string> GetHandledProcesses()
        {
            _logger?.Log($"ChromeTabFinder Handled Processes: {string.Join(", ", _browserProcesses)}");
            return _browserProcesses;
        }

        protected override IEnumerable<WindowItem> ScanWindowsCore()
        {
            var results = new List<WindowItem>();
            if (_settingsService == null || _browserProcesses.Count == 0) return results;

            // Optimization: Don't pre-fetch PIDs using Process.GetProcessesByName (expensive).
            // Instead, we will check process names dynamically inside the EnumWindows loop using our cached native helper.


            var walker = TreeWalker.RawViewWalker;
            _logger?.Log($"--- Scan started at {DateTime.Now} ---");

            NativeInterop.EnumWindows((hwnd, lParam) =>
            {
                // Check visibility first for speed
                if (!NativeInterop.IsWindowVisible(hwnd)) return true;

                NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
                string procName = NativeInterop.GetProcessName(pid);

                if (_browserProcesses.Contains(procName, StringComparer.OrdinalIgnoreCase)) // Extension methods might be needed or just simple validation
                {
                    // Check if it matches one of our targets
                    bool isMatch = false;
                    foreach (var browser in _browserProcesses)
                    {
                        if (string.Equals(browser, procName, StringComparison.OrdinalIgnoreCase))
                        {
                            isMatch = true;
                            break;
                        }
                    }

                    if (isMatch)
                    {
                        // Found a visible window belonging to one of our target browsers
                        ScanWindow(hwnd, (int)pid, walker, results);
                    }
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return results;
        }

        private void ScanWindow(IntPtr hwnd, int pid, TreeWalker walker, List<WindowItem> results)
        {
            AutomationElement? root = null;
            try
            {
                root = AutomationElement.FromHandle(hwnd);
            }
            catch { return; }

            if (root == null) return;

            // Get Process Name for the result item (expensive? maybe just cache it or look it up)
            string processName = NativeInterop.GetProcessName((uint)pid);

            _logger?.Log($"Scanning Window HWND: {hwnd} (PID: {pid}, Name: {processName})");

            var foundTabs = FindTabsBFS(root, walker, maxDepth: 20);

            if (foundTabs.Count == 0)
            {
                // Fallback: Add the main window if no tabs found
                string title = "";
                try { title = root.Current.Name; } catch { }

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
                _logger?.Log($"  Found {foundTabs.Count} tabs via BFS.");
                foreach (var tab in foundTabs)
                {
                    results.Add(new WindowItem
                    {
                        Hwnd = hwnd,
                        Title = tab,
                        ProcessName = processName,
                        Source = this
                    });
                }
            }
        }

        private List<string> FindTabsBFS(AutomationElement root, TreeWalker walker, int maxDepth)
        {
            var results = new List<string>();
            var queue = new Queue<(AutomationElement Element, int Depth)>();
            queue.Enqueue((root, 0));

            int itemsScanned = 0;

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                if (depth > maxDepth) continue;

                itemsScanned++;

                try
                {
                    // Optimization: Do not traverse into web content (Document control type)
                    // This prevents scanning thousands of DOM nodes, focusing on the browser "Chrome" UI.
                    if (current.Current.ControlType == ControlType.Document) continue;

                    bool isTab = false;
                    string name = current.Current.Name;

                    if (current.Current.ControlType == ControlType.TabItem) isTab = true;

                    if (!isTab && !string.IsNullOrEmpty(current.Current.LocalizedControlType))
                    {
                        if (current.Current.LocalizedControlType.Equals("tab", StringComparison.OrdinalIgnoreCase)) isTab = true;
                    }

                    if (isTab)
                    {
                        if (!string.IsNullOrWhiteSpace(name) && name != "New Tab" && name != "+")
                        {
                            results.Add(name);
                            _logger?.Log($"    FOUND TAB: '{name}'");
                        }
                    }
                }
                catch { /* Element might be invalid now */ }

                // Get children
                try
                {
                    var child = walker.GetFirstChild(current);
                    while (child != null)
                    {
                        // Enqueue child only
                        queue.Enqueue((child, depth + 1));
                        child = walker.GetNextSibling(child);
                    }
                }
                catch { }
            }

            _logger?.Log($"  Items Scanned: {itemsScanned}");

            return results;
        }

        public override void ActivateWindow(WindowItem item)
        {
            // Use shared NativeInterop for robust window activation
            NativeInterop.ForceForegroundWindow(item.Hwnd);

            // Wait a brief moment for window to actually activate before searching for tabs
            // This is crucial because AutomationElement tree might not update instantly
            System.Threading.Thread.Sleep(50);

            if (string.IsNullOrEmpty(item.Title)) return;

            try
            {
                AutomationElement? root = AutomationElement.FromHandle(item.Hwnd);
                if (root == null) return;

                var walker = TreeWalker.ControlViewWalker;
                var tabElement = FindTabByNameBFS(root, walker, 12, item.Title);

                if (tabElement != null)
                {
                    if (tabElement.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectPatternObj))
                    {
                        var selectPattern = (SelectionItemPattern)selectPatternObj;
                        selectPattern.Select();
                    }
                    else if (tabElement.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePatternObj))
                    {
                        var invokePattern = (InvokePattern)invokePatternObj;
                        invokePattern.Invoke();
                    }
                    else
                    {
                        tabElement.SetFocus();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error activating tab '{item.Title}'", ex);
            }
        }

        private AutomationElement? FindTabByNameBFS(AutomationElement root, TreeWalker walker, int maxDepth, string targetName)
        {
            var queue = new Queue<(AutomationElement Element, int Depth)>();
            queue.Enqueue((root, 0));

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                if (depth > maxDepth) continue;

                try
                {
                    bool isTab = false;
                    if (current.Current.ControlType == ControlType.TabItem) isTab = true;
                    if (!isTab && !string.IsNullOrEmpty(current.Current.LocalizedControlType))
                    {
                        if (current.Current.LocalizedControlType.Equals("tab", StringComparison.OrdinalIgnoreCase)) isTab = true;
                    }

                    if (isTab && current.Current.Name == targetName)
                    {
                        return current;
                    }
                }
                catch { }

                try
                {
                    var child = walker.GetFirstChild(current);
                    while (child != null)
                    {
                        queue.Enqueue((child, depth + 1));
                        child = walker.GetNextSibling(child);
                    }
                }
                catch { }
            }
            return null;
        }
    }

    // NativeMethods class removed - now using SwitchBlade.Contracts.NativeInterop
}
