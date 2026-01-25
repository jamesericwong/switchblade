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

        public override string PluginName => "NotepadPlusPlusPlugin";
        public override bool HasSettings => true;

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

            var walker = TreeWalker.RawViewWalker;
            _logger?.Log($"{PluginName}: --- Scan started at {DateTime.Now} ---");

            // Use native EnumWindows + cached GetProcessInfo for efficiency
            // This replaces the expensive Process.GetProcessesByName() approach
            NativeInterop.EnumWindows((hwnd, lParam) =>
            {
                // Check visibility first for speed
                if (!NativeInterop.IsWindowVisible(hwnd)) return true;

                NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
                var (procName, execPath) = NativeInterop.GetProcessInfo(pid);

                // O(1) HashSet lookup instead of O(n) list search
                if (_nppProcesses.Contains(procName))
                {
                    ScanWindow(hwnd, (int)pid, procName, execPath, walker, results);
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return results;
        }

        private void ScanWindow(IntPtr hwnd, int pid, string processName, string? executablePath, TreeWalker walker, List<WindowItem> results)
        {
            var tabs = ScanForTabs(hwnd);

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
                        Source = this
                    });
                }
            }
        }

        private List<string> ScanForTabs(IntPtr hwnd)
        {
            var tabs = new List<string>();

            try
            {
                var root = AutomationElement.FromHandle(hwnd);
                if (root == null) return tabs;

                // Use BFS to find TabItem elements
                var walker = TreeWalker.RawViewWalker;
                var queue = new Queue<(AutomationElement Element, int Depth)>();
                queue.Enqueue((root, 0));

                const int maxDepth = 15;

                while (queue.Count > 0)
                {
                    var (current, depth) = queue.Dequeue();
                    if (depth > maxDepth) continue;

                    try
                    {
                        var controlType = current.Current.ControlType;
                        var name = current.Current.Name;

                        // Check if this is a tab
                        bool isTab = controlType == ControlType.TabItem;
                        if (!isTab)
                        {
                            var localizedType = current.Current.LocalizedControlType;
                            if (!string.IsNullOrEmpty(localizedType) &&
                                localizedType.Equals("tab item", StringComparison.OrdinalIgnoreCase))
                            {
                                isTab = true;
                            }
                        }

                        if (isTab && !string.IsNullOrWhiteSpace(name))
                        {
                            tabs.Add(name);
                        }
                    }
                    catch
                    {
                        // Element may have become invalid
                        continue;
                    }

                    // Enqueue children
                    try
                    {
                        var child = walker.GetFirstChild(current);
                        while (child != null)
                        {
                            queue.Enqueue((child, depth + 1));
                            child = walker.GetNextSibling(child);
                        }
                    }
                    catch
                    {
                        // Access denied or element invalid
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
                    var root = AutomationElement.FromHandle(item.Hwnd);
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

        private AutomationElement? FindTabByName(AutomationElement root, string targetName)
        {
            var walker = TreeWalker.RawViewWalker;
            var queue = new Queue<(AutomationElement Element, int Depth)>();
            queue.Enqueue((root, 0));

            const int maxDepth = 15;

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                if (depth > maxDepth) continue;

                try
                {
                    var controlType = current.Current.ControlType;
                    bool isTab = controlType == ControlType.TabItem;
                    if (!isTab)
                    {
                        var localizedType = current.Current.LocalizedControlType;
                        if (!string.IsNullOrEmpty(localizedType) &&
                            localizedType.Equals("tab item", StringComparison.OrdinalIgnoreCase))
                        {
                            isTab = true;
                        }
                    }

                    if (isTab && current.Current.Name == targetName)
                    {
                        return current;
                    }
                }
                catch
                {
                    continue;
                }

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
}
