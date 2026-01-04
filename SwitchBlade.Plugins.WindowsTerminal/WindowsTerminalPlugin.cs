using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public override string PluginName => "WindowsTerminalPlugin";
        public override bool HasSettings => true;

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

        public override void ShowSettingsDialog(IntPtr ownerHwnd)
        {
            var dialog = new TerminalSettingsWindow(_settingsService!, _terminalProcesses);
            if (ownerHwnd != IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(dialog);
                helper.Owner = ownerHwnd;
            }
            dialog.ShowDialog();

            // Reload settings after dialog closes
            ReloadSettings();
        }

        protected override IEnumerable<WindowItem> ScanWindowsCore()
        {
            var results = new List<WindowItem>();

            var targetProcessNames = new HashSet<string>(_terminalProcesses, StringComparer.OrdinalIgnoreCase);

            Process[] processes;
            try
            {
                processes = targetProcessNames.SelectMany(name => Process.GetProcessesByName(name)).ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"{PluginName}: Failed to get processes", ex);
                return results;
            }

            foreach (var process in processes)
            {
                try
                {
                    var hwnd = process.MainWindowHandle;
                    if (hwnd == IntPtr.Zero) continue;

                    string windowTitle = process.MainWindowTitle;
                    if (string.IsNullOrEmpty(windowTitle)) continue;

                    var tabs = ScanForTabs(hwnd);

                    if (tabs.Count > 0)
                    {
                        _logger?.Log($"{PluginName}: Found {tabs.Count} tabs in PID {process.Id}");
                        foreach (var tabName in tabs)
                        {
                            results.Add(new WindowItem
                            {
                                Hwnd = hwnd,
                                Title = tabName,
                                ProcessName = process.ProcessName,
                                Source = this
                            });
                        }
                    }
                    else
                    {
                        // Fallback: return main window if no tabs found
                        _logger?.Log($"{PluginName}: No tabs found for PID {process.Id}, returning main window");
                        results.Add(new WindowItem
                        {
                            Hwnd = hwnd,
                            Title = windowTitle,
                            ProcessName = process.ProcessName,
                            Source = this
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"{PluginName}: Error scanning process {process.Id}", ex);
                }
            }

            return results;
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

                const int maxDepth = 12;

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

            const int maxDepth = 12;

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
