using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Interop;
using SwitchBlade.Contracts;

namespace SwitchBlade.Plugins.NotepadPlusPlus
{
    /// <summary>
    /// Plugin that discovers individual tabs within Notepad++ instances.
    /// Uses UI Automation to enumerate tabs and allows switching to specific documents.
    /// </summary>
    public class NotepadPlusPlusPlugin : CachingWindowProviderBase
    {
        private ILogger? _logger;
        private IPluginSettingsService? _settingsService;
        private HashSet<string> _nppProcesses = new(StringComparer.OrdinalIgnoreCase);

        // Default process names if no settings exist
        private static readonly List<string> DefaultNppProcesses = new()
        {
            "notepad++"
        };

        // Optimization: Server-side filter to prevent creation of RCWs for heavy Document nodes
        private static readonly Condition NotDocumentCondition = new NotCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));

        public override string PluginName => "NotepadPlusPlusPlugin";
        public override bool HasSettings => true;
        public override bool IsUiaProvider => true;

        public override ISettingsControl? SettingsControl =>
            _settingsService != null
                ? new NotepadPlusPlusSettingsControlProvider(_settingsService, _nppProcesses.ToList())
                : null;

        public NotepadPlusPlusPlugin()
        {
        }

        /// <summary>
        /// Constructor for unit testing with mocked settings.
        /// </summary>
        public NotepadPlusPlusPlugin(IPluginSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public override void Initialize(IPluginContext context)
        {
            base.Initialize(context);
            _logger = context.Logger;

            // Use injected settings if available (v1.9.3+), fallback to self-instantiation
            _settingsService = context.Settings ?? _settingsService ?? new PluginSettingsService(PluginName);

            ReloadSettings();
        }

        public override void ReloadSettings()
        {
            if (_settingsService == null) return;

            // Check if NppProcesses key exists in plugin Registry
            if (_settingsService.KeyExists("NppProcesses"))
            {
                var loadedList = _settingsService.GetStringList("NppProcesses", DefaultNppProcesses);
                _nppProcesses = new HashSet<string>(loadedList, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // First run or missing key - use defaults and save them
                _nppProcesses = new HashSet<string>(DefaultNppProcesses, StringComparer.OrdinalIgnoreCase);
                _settingsService.SetStringList("NppProcesses", _nppProcesses.ToList());
            }

            _logger?.Log($"{PluginName}: Loaded {_nppProcesses.Count} Notepad++ processes");
        }

        public override IEnumerable<string> GetHandledProcesses()
        {
            _logger?.Log($"{PluginName} Handled Processes: {string.Join(", ", _nppProcesses)}");
            return _nppProcesses;
        }

        protected override IEnumerable<WindowItem> ScanWindowsCore()
        {
            var results = new List<WindowItem>();
            if (_nppProcesses.Count == 0) return results;

            _logger?.Log($"{PluginName}: --- Scan started at {DateTime.Now} ---");

            // Use native EnumWindows + cached GetProcessInfo for efficiency
            NativeInterop.EnumWindows((hwnd, lParam) =>
            {
                // Check visibility first for speed
                if (!NativeInterop.IsWindowVisible(hwnd)) return true;

                NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
                var (procName, execPath) = NativeInterop.GetProcessInfo(pid);

                // O(1) HashSet lookup instead of O(n) list search
                if (_nppProcesses.Contains(procName))
                {
                    ScanWindow(hwnd, (int)pid, procName, execPath, results);
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return results;
        }

        private void ScanWindow(IntPtr hwnd, int pid, string processName, string? executablePath, List<WindowItem> results)
        {
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
                // Get window title via native API for consistency
                Span<char> buffer = stackalloc char[512];
                int length = NativeInterop.GetWindowText(hwnd, buffer, buffer.Length);
                string windowTitle = length > 0 ? new string(buffer[..length]) : "";

                if (!string.IsNullOrEmpty(windowTitle))
                {
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
        }

        /// <summary>
        /// Surgical BFS: Uses CacheRequest + FindAll to minimize COM RCW creation.
        /// Prunes Document branches to avoid deep text content traversal.
        /// </summary>
        private List<string> ScanForTabs(IntPtr hwnd, int pid)
        {
            var tabs = new List<string>();

            try
            {
                // Safe UIA access to handle E_FAIL
                var root = TryGetAutomationElement(hwnd, pid);
                if (root == null) return tabs; // Caller handles empty list by adding main window fallback

                var cacheRequest = new CacheRequest();
                cacheRequest.Add(AutomationElement.NameProperty);
                cacheRequest.Add(AutomationElement.ControlTypeProperty);
                cacheRequest.Add(AutomationElement.LocalizedControlTypeProperty);
                cacheRequest.TreeScope = TreeScope.Element | TreeScope.Children;

                using (cacheRequest.Activate())
                {
                    // PRIMARY: Manual BFS traversal
                    try
                    {
                        var queue = new Queue<AutomationElement>();
                        queue.Enqueue(root);

                        int containersChecked = 0;
                        const int MaxContainersToCheck = 50; // Safety limit

                        while (queue.Count > 0 && containersChecked < MaxContainersToCheck)
                        {
                            var current = queue.Dequeue();
                            containersChecked++;

                            AutomationElementCollection? children = null;
                            try { children = current.FindAll(TreeScope.Children, NotDocumentCondition); }
                            catch { continue; }

                            if (children == null || children.Count == 0) continue;

                            foreach (AutomationElement child in children)
                            {
                                try
                                {
                                    var controlType = child.Cached.ControlType;

                                    // PRUNE: Skip Document branches (text areas, edit controls)
                                    if (controlType == ControlType.Document) continue;

                                    // Check for TabItem
                                    bool isTab = controlType == ControlType.TabItem;
                                    if (!isTab)
                                    {
                                        var localizedType = child.Cached.LocalizedControlType;
                                        if (!string.IsNullOrEmpty(localizedType) &&
                                            localizedType.Equals("tab item", StringComparison.OrdinalIgnoreCase))
                                        {
                                            isTab = true;
                                        }
                                    }

                                    if (isTab)
                                    {
                                        var name = child.Cached.Name;
                                        if (!string.IsNullOrWhiteSpace(name))
                                        {
                                            tabs.Add(name);
                                        }
                                    }
                                    else
                                    {
                                        // Enqueue non-Document containers for further traversal
                                        queue.Enqueue(child);
                                    }
                                }
                                catch { /* Element invalidated */ }
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
                    const int MaxContainersToCheck = 50;

                    while (queue.Count > 0 && containersChecked < MaxContainersToCheck)
                    {
                        var current = queue.Dequeue();
                        containersChecked++;

                        AutomationElementCollection? children = null;
                        try { children = current.FindAll(TreeScope.Children, NotDocumentCondition); }
                        catch { continue; }

                        if (children == null || children.Count == 0) continue;

                        foreach (AutomationElement child in children)
                        {
                            try
                            {
                                var controlType = child.Cached.ControlType;

                                // PRUNE: Skip Document branches
                                if (controlType == ControlType.Document) continue;

                                bool isTab = controlType == ControlType.TabItem;
                                if (!isTab)
                                {
                                    var localizedType = child.Cached.LocalizedControlType;
                                    if (!string.IsNullOrEmpty(localizedType) &&
                                        localizedType.Equals("tab item", StringComparison.OrdinalIgnoreCase))
                                    {
                                        isTab = true;
                                    }
                                }

                                if (isTab && child.Cached.Name == targetName)
                                {
                                    return child;
                                }

                                // Enqueue non-Document containers
                                if (!isTab)
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
            return UiaElementResolver.TryResolve(hwnd, pid, PluginName, _logger);
        }
    }
}
