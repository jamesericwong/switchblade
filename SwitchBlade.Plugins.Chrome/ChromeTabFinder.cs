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
        private HashSet<string> _browserProcesses = new(StringComparer.OrdinalIgnoreCase);

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

        public override ISettingsControl? SettingsControl =>
            _settingsService != null
                ? new ChromeSettingsControlProvider(_settingsService, _browserProcesses.ToList())
                : null;

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
                var loadedList = _settingsService.GetStringList("BrowserProcesses", DefaultBrowserProcesses);
                _browserProcesses = new HashSet<string>(loadedList, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // First run or missing key - use defaults and save them
                _browserProcesses = new HashSet<string>(DefaultBrowserProcesses, StringComparer.OrdinalIgnoreCase);
                _settingsService.SetStringList("BrowserProcesses", _browserProcesses.ToList());
            }

            _logger?.Log($"ChromeTabFinder: Loaded {_browserProcesses.Count} browser processes");
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
                var (procName, execPath) = NativeInterop.GetProcessInfo(pid);

                // O(1) HashSet lookup (comparer set at construction)
                if (_browserProcesses.Contains(procName))
                {
                    // Found a visible window belonging to one of our target browsers
                    ScanWindow(hwnd, (int)pid, procName, execPath, walker, results);
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            // Memory cleanup: UI Automation creates many AutomationElement objects
            // holding COM RCW references. Force a Gen2 collection to promptly release
            // these wrappers and prevent memory accumulation between polling cycles.
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);

            return results;
        }

        private void ScanWindow(IntPtr hwnd, int pid, string processName, string? executablePath, TreeWalker walker, List<WindowItem> results)
        {
            AutomationElement? root = null;
            try
            {
                root = AutomationElement.FromHandle(hwnd);
            }
            catch { return; }

            if (root == null) return;

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
                        ExecutablePath = executablePath,
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
                        ExecutablePath = executablePath,
                        Source = this
                    });
                }
            }
        }

        private List<string> FindTabsBFS(AutomationElement root, TreeWalker walker, int maxDepth)
        {
            var results = new List<string>();

            // Memory optimization: Use FindAll with Condition to let UIA do the traversal
            // internally, returning only matching TabItem elements. This creates far fewer
            // AutomationElement wrappers compared to manual BFS traversal.
            // 
            // The condition matches ControlType.TabItem (most browsers) OR elements with
            // LocalizedControlType == "tab" (for browsers that use non-standard tab controls).
            try
            {
                var cacheRequest = new CacheRequest();
                cacheRequest.Add(AutomationElement.NameProperty);
                cacheRequest.Add(AutomationElement.ControlTypeProperty);
                cacheRequest.TreeScope = TreeScope.Element;

                using (cacheRequest.Activate())
                {
                    // Find all TabItem control types in the subtree
                    var tabCondition = new PropertyCondition(
                        AutomationElement.ControlTypeProperty,
                        ControlType.TabItem);

                    var tabElements = root.FindAll(TreeScope.Descendants, tabCondition);

                    foreach (AutomationElement tab in tabElements)
                    {
                        try
                        {
                            string name = tab.Cached.Name;
                            if (!string.IsNullOrWhiteSpace(name) && name != "New Tab" && name != "+")
                            {
                                results.Add(name);
                                _logger?.Log($"    FOUND TAB: '{name}'");
                            }
                        }
                        catch { /* Element might be stale */ }
                    }
                }

                _logger?.Log($"  Found {results.Count} tabs via FindAll");
            }
            catch (Exception ex)
            {
                _logger?.Log($"  FindAll failed: {ex.Message}, falling back to BFS");
                // Fallback to manual BFS if FindAll fails (rare edge case)
                results = FindTabsBFSFallback(root, walker, maxDepth);
            }

            return results;
        }

        /// <summary>
        /// Fallback BFS implementation for edge cases where FindAll fails.
        /// This is kept for robustness but should rarely be called.
        /// </summary>
        private List<string> FindTabsBFSFallback(AutomationElement root, TreeWalker walker, int maxDepth)
        {
            var results = new List<string>();
            int itemsScanned = 0;

            var cacheRequest = new CacheRequest();
            cacheRequest.Add(AutomationElement.NameProperty);
            cacheRequest.Add(AutomationElement.ControlTypeProperty);
            cacheRequest.Add(AutomationElement.LocalizedControlTypeProperty);
            cacheRequest.TreeScope = TreeScope.Element;

            using (cacheRequest.Activate())
            {
                var queue = new Queue<(AutomationElement Element, int Depth)>();

                AutomationElement? cachedRoot = null;
                try { cachedRoot = root.GetUpdatedCache(cacheRequest); } catch { }
                if (cachedRoot == null) return results;

                queue.Enqueue((cachedRoot, 0));

                while (queue.Count > 0)
                {
                    var (current, depth) = queue.Dequeue();
                    if (depth > maxDepth) continue;

                    itemsScanned++;

                    try
                    {
                        var controlType = current.Cached.ControlType;
                        if (controlType == ControlType.Document) continue;

                        bool isTab = controlType == ControlType.TabItem;
                        string name = current.Cached.Name;

                        if (!isTab)
                        {
                            string localizedType = current.Cached.LocalizedControlType;
                            if (!string.IsNullOrEmpty(localizedType) &&
                                localizedType.Equals("tab", StringComparison.OrdinalIgnoreCase))
                            {
                                isTab = true;
                            }
                        }

                        if (isTab && !string.IsNullOrWhiteSpace(name) && name != "New Tab" && name != "+")
                        {
                            results.Add(name);
                        }
                    }
                    catch { }

                    try
                    {
                        var child = walker.GetFirstChild(current);
                        while (child != null)
                        {
                            try
                            {
                                var cachedChild = child.GetUpdatedCache(cacheRequest);
                                queue.Enqueue((cachedChild, depth + 1));
                            }
                            catch { }
                            child = walker.GetNextSibling(child);
                        }
                    }
                    catch { }
                }
            }

            _logger?.Log($"  Fallback BFS scanned: {itemsScanned}");
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
