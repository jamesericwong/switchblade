using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Automation;
using System.Windows.Interop;
using SwitchBlade.Contracts;

[assembly: InternalsVisibleTo("SwitchBlade.Tests")]

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

            // Use injected settings if available (v1.9.3+), fallback to self-instantiation
            _settingsService = context.Settings;

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
            return DeduplicateResults(this, pidToResults);
        }

        /// <summary>
        /// Per-PID deduplication: if any handle yielded real tabs, only those are kept;
        /// otherwise a single unique main-window fallback is retained.
        /// </summary>
        internal static List<WindowItem> DeduplicateResults(IWindowProvider source, Dictionary<int, List<WindowItem>> pidToResults)
        {
            var allResults = new List<WindowItem>();

            foreach (var kvp in pidToResults)
            {
                var items = kvp.Value;

                // Real tabs only: fallback (main-window) entries are excluded so a bare
                // "Main Window" never appears alongside the tabs of the same process.
                var windowsWithTabs = items.Where(i => i.Source == source && !string.IsNullOrEmpty(i.Title) && !i.IsFallback).ToList();

                if (windowsWithTabs.Count != 0)
                {
                    allResults.AddRange(windowsWithTabs);
                }
                else if (items.Count != 0)
                {
                    // No tabs found on any handle: keep one fallback per unique window handle
                    // to avoid "Found 2 windows" (both being the main window).
                    var ownFallbacks = items.Where(i => i.Source == source && i.IsFallback).ToList();
                    var uniqueHandleFallback = ownFallbacks.GroupBy(i => i.Hwnd).Select(g => g.First()).FirstOrDefault();
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
                    Source = this,
                    IsFallback = true
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
        private static AutomationElement? FindTabByName(AutomationElement root, string targetName)
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

        private static readonly UiaResolverOptions _resolverOptions = new()
        {
            MaxRetries = 3,
            RetryDelayMs = 50,
            UseFromPointFallback = true
        };

        private AutomationElement? TryGetAutomationElement(IntPtr hwnd, int pid)
        {
            return UiaElementResolver.TryResolve(hwnd, pid, PluginName, _logger, _resolverOptions);
        }
    }
}
