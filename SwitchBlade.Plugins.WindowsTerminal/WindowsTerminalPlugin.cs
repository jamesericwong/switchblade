using System;
using System.Collections.Generic;
using System.Windows.Automation;
using System.Windows.Interop;
using SwitchBlade.Contracts;

namespace SwitchBlade.Plugins.WindowsTerminal
{
    /// <summary>
    /// Plugin that discovers individual tabs within Windows Terminal instances.
    /// Falls back to returning the main window if tabs cannot be enumerated
    /// (e.g., due to elevation/UIPI restrictions).
    /// </summary>
    public class WindowsTerminalPlugin : CachingWindowProviderBase
    {
        private ILogger? _logger;
        private IPluginSettingsService? _settingsService;
        private List<string> _terminalProcesses = new();

        // Default terminal processes if no settings exist
        private static readonly List<string> DefaultTerminalProcesses = new()
        {
            "WindowsTerminal"
        };

        // Optimization: Server-side filter to prevent creation of RCWs for heavy Document nodes
        private static readonly Condition NotDocumentCondition = new NotCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));

        public override string PluginName => "WindowsTerminalPlugin";
        public override bool HasSettings => true;
        public override bool IsUiaProvider => true;

        public override ISettingsControl? SettingsControl =>
            _settingsService != null
                ? new TerminalSettingsControlProvider(_settingsService, _terminalProcesses)
                : null;

        public WindowsTerminalPlugin()
        {
        }

        /// <summary>
        /// Constructor for unit testing with mocked settings.
        /// </summary>
        public WindowsTerminalPlugin(IPluginSettingsService settingsService)
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

            // Check if TerminalProcesses key exists in plugin Registry
            if (_settingsService.KeyExists("TerminalProcesses"))
            {
                _terminalProcesses = _settingsService.GetStringList("TerminalProcesses", DefaultTerminalProcesses);
            }
            else
            {
                // First run or missing key - use defaults and save them
                _terminalProcesses = new List<string>(DefaultTerminalProcesses);
                _settingsService.SetStringList("TerminalProcesses", _terminalProcesses);
            }

            _logger?.Log($"{PluginName}: Loaded {_terminalProcesses.Count} terminal processes");
        }

        public override IEnumerable<string> GetHandledProcesses()
        {
            _logger?.Log($"{PluginName} Handled Processes: {string.Join(", ", _terminalProcesses)}");
            return _terminalProcesses;
        }

        protected override IEnumerable<WindowItem> ScanWindowsCore()
        {
            var allResults = new List<WindowItem>();

            var targetProcessNames = new HashSet<string>(_terminalProcesses, StringComparer.OrdinalIgnoreCase);
            if (targetProcessNames.Count == 0) return allResults;

            // Map PID to list of window items found for that process
            var pidToResults = new Dictionary<int, List<WindowItem>>();

            NativeInterop.EnumWindows((hwnd, lParam) =>
            {
                if (!NativeInterop.IsWindowVisible(hwnd)) return true;

                NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
                var (procName, execPath) = NativeInterop.GetProcessInfo(pid);

                if (targetProcessNames.Contains(procName))
                {
                    var resultsForThisHandle = new List<WindowItem>();
                    ScanWindow(hwnd, (int)pid, procName, execPath, resultsForThisHandle);
                    
                    if (!pidToResults.TryGetValue((int)pid, out var list))
                    {
                        list = new List<WindowItem>();
                        pidToResults[(int)pid] = list;
                    }
                    list.AddRange(resultsForThisHandle);
                }

                return true;
            }, IntPtr.Zero);

            // POST-PROCESS: Deduplication and Prioritization
            foreach (var kvp in pidToResults)
            {
                var pid = kvp.Key;
                var items = kvp.Value;

                // Identify if any item is a "legitimate" tab (detected via ScanForTabs)
                // We'll use a heuristic: if we have multiple windows for one PID,
                // and some found 1+ tabs while others found 0 (fallback), discard the fallbacks.
                
                var windowsWithTabs = items.Where(i => i.Source == this && !string.IsNullOrEmpty(i.Title)).ToList();
                
                // If we found specific tabs at all for this PID, use only them.
                // This prevents "Main Window" from appearing if we successfully peered into even one handle.
                if (windowsWithTabs.Any())
                {
                    // Filter: Only include items that are not just the 'main window' fallback.
                    // How to detect? In ScanWindow, fallback uses 'windowTitle'. 
                    // To be safe, we'll track if ScanForTabs returned nodes.
                    // Actually, a simpler way: if we have items, and any found tabs, 
                    // we remove the ones that are just the Fallback markers.
                    // Let's add a Tag or check some unique property.
                    
                    // For now, if we found any tabs, use only the windows that found tabs.
                    allResults.AddRange(windowsWithTabs);
                }
                else if (items.Any())
                {
                    // If no tabs found at all for any handle, just take the first unique handle's fallback
                    // to avoid "Found 2 windows" (both being main window).
                    var uniqueHandleFallback = items.GroupBy(i => i.Hwnd).Select(g => g.First()).FirstOrDefault();
                    if (uniqueHandleFallback != null)
                    {
                        allResults.Add(uniqueHandleFallback);
                    }
                }
            }

            return allResults;
        }

        private void ScanWindow(IntPtr hwnd, int pid, string processName, string? executablePath, List<WindowItem> results)
        {
            // Get window title via native API
            Span<char> buffer = stackalloc char[512];
            int length = NativeInterop.GetWindowText(hwnd, buffer, buffer.Length);
            string windowTitle = length > 0 ? new string(buffer[..length]) : "";
            if (string.IsNullOrEmpty(windowTitle)) return;

            var tabs = ScanForTabs(hwnd, pid);

            if (tabs.Count > 0)
            {
                _logger?.Log($"{PluginName}: Found {tabs.Count} tabs in PID {pid}");
                foreach (var tabName in tabs)
                {
                    results.Add(new WindowItem
                    {
                        Hwnd = hwnd,
                        Title = tabName,
                        ProcessName = processName,
                        ExecutablePath = executablePath,
                        Source = this
                    });
                }
            }
            else
            {
                // Fallback: return main window if no tabs found
                _logger?.Log($"{PluginName}: No tabs found for PID {pid}, returning main window");
                results.Add(new WindowItem
                {
                    Hwnd = hwnd,
                    Title = windowTitle,
                    ProcessName = processName,
                    ExecutablePath = executablePath,
                    Source = this
                });
            }
        }

        /// <summary>
        /// Surgical BFS: Uses CacheRequest + FindAll to minimize COM RCW creation.
        /// Prunes Document branches to avoid deep web/text content traversal.
        /// </summary>
        private List<string> ScanForTabs(IntPtr hwnd, int pid)
        {
            var tabs = new List<string>();

            try
            {
                var root = TryGetAutomationElement(hwnd, pid);
                if (root == null) return tabs;

                var cacheRequest = new CacheRequest();
                cacheRequest.Add(AutomationElement.NameProperty);
                cacheRequest.Add(AutomationElement.ControlTypeProperty);
                cacheRequest.Add(AutomationElement.LocalizedControlTypeProperty);
                cacheRequest.TreeScope = TreeScope.Element | TreeScope.Children;

                using (cacheRequest.Activate())
                {
                    // PRIMARY: Manual BFS traversal (user preferred)
                    try
                    {
                        var queue = new Queue<AutomationElement>();
                        queue.Enqueue(root);

                        int containersChecked = 0;
                        const int MaxContainersToCheck = 200;

                        while (queue.Count > 0 && containersChecked < MaxContainersToCheck)
                        {
                            var current = queue.Dequeue();
                            containersChecked++;

                            AutomationElementCollection? children = null;
                            try { children = current.FindAll(TreeScope.Children, NotDocumentCondition); }
                            catch { continue; }

                            if (children == null) continue;

                            foreach (AutomationElement child in children)
                            {
                                try
                                {
                                    var controlType = child.Cached.ControlType;

                                    if (controlType == ControlType.TabItem || 
                                        child.Cached.LocalizedControlType?.Equals("tab item", StringComparison.OrdinalIgnoreCase) == true)
                                    {
                                        var name = child.Cached.Name;
                                        if (!string.IsNullOrWhiteSpace(name)) tabs.Add(name);
                                    }
                                    else if (controlType != ControlType.Document)
                                    {
                                        queue.Enqueue(child);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"{PluginName}: BFS scan failed, falling back to Descendants search. Error: {ex.Message}");
                    }

                    // FALLBACK: Native Descendants search if BFS found nothing or failed
                    if (tabs.Count == 0)
                    {
                        var condition = new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
                            new PropertyCondition(AutomationElement.LocalizedControlTypeProperty, "tab item")
                        );

                        var elements = root.FindAll(TreeScope.Descendants, condition);
                        foreach (AutomationElement element in elements)
                        {
                            var name = element.Cached.Name;
                            if (!string.IsNullOrWhiteSpace(name)) tabs.Add(name);
                        }
                        
                        if (tabs.Count > 0)
                        {
                            _logger?.Log($"{PluginName}: BFS found 0 tabs, but Descendants fallback found {tabs.Count}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"{PluginName}: Error scanning UIA tree", ex);
            }

            return tabs;
        }

        public override void ActivateWindow(WindowItem item)
        {
            // First, bring the main window to foreground
            NativeInterop.ForceForegroundWindow(item.Hwnd);

            // If this item was created by this plugin and has a title, try to select the specific tab
            if (item.Source == this && !string.IsNullOrEmpty(item.Title))
            {
                System.Threading.Thread.Sleep(50); // Brief wait for window activation

                try
                {
                    NativeInterop.GetWindowThreadProcessId(item.Hwnd, out uint pid);
                    var root = TryGetAutomationElement(item.Hwnd, (int)pid);
                    if (root == null) return;

                    var tabElement = FindTabByName(root, item.Title);
                    if (tabElement != null)
                    {
                        if (tabElement.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern))
                        {
                            ((SelectionItemPattern)pattern).Select();
                        }
                        else if (tabElement.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePattern))
                        {
                            ((InvokePattern)invokePattern).Invoke();
                        }
                        else
                        {
                            tabElement.SetFocus();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"{PluginName}: Error activating tab '{item.Title}'", ex);
                }
            }
        }

        /// <summary>
        /// Surgical BFS for tab activation: Uses CacheRequest + FindAll with Document pruning.
        /// </summary>
        private AutomationElement? FindTabByName(AutomationElement root, string targetName)
        {
            var cacheRequest = new CacheRequest();
            cacheRequest.Add(AutomationElement.NameProperty);
            cacheRequest.Add(AutomationElement.ControlTypeProperty);
            cacheRequest.Add(AutomationElement.LocalizedControlTypeProperty);
            cacheRequest.TreeScope = TreeScope.Element | TreeScope.Children;

            using (cacheRequest.Activate())
            {
                // PRIMARY: Manual BFS
                try
                {
                    var queue = new Queue<AutomationElement>();
                    queue.Enqueue(root);

                    int containersChecked = 0;
                    const int MaxContainersToCheck = 200;

                    while (queue.Count > 0 && containersChecked < MaxContainersToCheck)
                    {
                        var current = queue.Dequeue();
                        containersChecked++;

                        AutomationElementCollection? children = null;
                        try { children = current.FindAll(TreeScope.Children, NotDocumentCondition); }
                        catch { continue; }

                        if (children == null) continue;

                        foreach (AutomationElement child in children)
                        {
                            try
                            {
                                var controlType = child.Cached.ControlType;
                                bool isTab = controlType == ControlType.TabItem || 
                                             child.Cached.LocalizedControlType?.Equals("tab item", StringComparison.OrdinalIgnoreCase) == true;

                                if (isTab && child.Cached.Name == targetName)
                                {
                                    return child;
                                }

                                if (!isTab && controlType != ControlType.Document)
                                {
                                    queue.Enqueue(child);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // FALLBACK: Native Descendants search
                var condition = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
                    new PropertyCondition(AutomationElement.LocalizedControlTypeProperty, "tab item")
                );

                var elements = root.FindAll(TreeScope.Descendants, condition);
                foreach (AutomationElement element in elements)
                {
                    if (element.Cached.Name == targetName)
                    {
                        return element;
                    }
                }
            }

            return null;
        }

        private AutomationElement? TryGetAutomationElement(IntPtr hwnd, int pid)
        {
            // Strategy 1: Direct HWND binding (Fastest)
            try
            {
                return AutomationElement.FromHandle(hwnd);
            }
            catch (Exception ex)
            {
                if (ex is System.Runtime.InteropServices.COMException comEx && (uint)comEx.HResult == 0x80004005)
                {
                    _logger?.Log($"{PluginName}: Direct HWND access failed (E_FAIL). Attempting Desktop Root fallback...");
                }
                else
                {
                    _logger?.Log($"{PluginName}: Direct HWND access failed: {ex.Message}. Attempting fallback...");
                }
            }

            // Strategy 2: Desktop Root Search (Slower but more robust)
            try
            {
                var root = AutomationElement.RootElement;
                var condition = new PropertyCondition(AutomationElement.ProcessIdProperty, pid);

                // Only search direct children of Desktop (Top-Level Windows)
                var match = root.FindFirst(TreeScope.Children, condition);

                if (match != null)
                {
                    _logger?.Log($"{PluginName}: Successfully acquired root via Desktop FindFirst for PID {pid}.");
                    return match;
                }
            }
            catch (Exception fallbackEx)
            {
                _logger?.Log($"{PluginName}: Desktop FindFirst fallback failed: {fallbackEx.Message}. Attempting TreeWalker...");
            }

            // Strategy 3: Desktop Walker (Most Robust, Slowest)
            try
            {
                var walker = TreeWalker.ControlViewWalker;
                var child = walker.GetFirstChild(AutomationElement.RootElement);

                while (child != null)
                {
                    try
                    {
                        if (child.Current.ProcessId == pid)
                        {
                            _logger?.Log($"{PluginName}: Successfully acquired root via Desktop Walker for PID {pid}.");
                            return child;
                        }
                    }
                    catch { /* Skip restricted windows */ }

                    try
                    {
                        child = walker.GetNextSibling(child);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            catch (Exception walkerEx)
            {
                _logger?.Log($"{PluginName}: Desktop Walker fallback failed: {walkerEx.Message}");
            }

            return null;
        }
    }
}
