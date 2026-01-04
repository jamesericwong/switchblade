using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;
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
        private const string ProcessName = "WindowsTerminal";

        public override string PluginName => "WindowsTerminalPlugin";
        public override bool HasSettings => false;

        public override void Initialize(IPluginContext context)
        {
            base.Initialize(context);
            _logger = context.Logger;
            _logger?.Log($"{PluginName}: Initialized");
        }

        public override void ReloadSettings()
        {
            // No settings for this plugin
        }

        public override IEnumerable<string> GetHandledProcesses()
        {
            return new[] { ProcessName };
        }

        public override void ShowSettingsDialog(IntPtr ownerHwnd)
        {
            // No settings dialog for this plugin
        }

        protected override IEnumerable<WindowItem> ScanWindowsCore()
        {
            var results = new List<WindowItem>();

            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(ProcessName);
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
                                ProcessName = ProcessName,
                                IsChromeTab = false,
                                IsTerminalTab = true,
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
                            ProcessName = ProcessName,
                            IsChromeTab = false,
                            IsTerminalTab = false,
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

            // If this is a tab, try to select it
            if (item.IsTerminalTab && !string.IsNullOrEmpty(item.Title))
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
