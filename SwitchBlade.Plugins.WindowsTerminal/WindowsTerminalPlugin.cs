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

namespace SwitchBlade.Plugins.WindowsTerminal
{
    public class WindowsTerminalPlugin : CachingWindowProviderBase
    {
        private ILogger? _logger;
        private IPluginSettingsService? _settingsService;
        private List<string> _terminalProcesses = new();

        private static readonly List<string> DefaultTerminalProcesses = new()
        {
            "WindowsTerminal"
        };

        public override string PluginName => "WindowsTerminalPlugin";
        public override bool HasSettings => true;

        public WindowsTerminalPlugin() { }

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

            ReloadSettings();
        }

        public override void ReloadSettings()
        {
            if (_settingsService == null) return;

            if (_settingsService.KeyExists("TerminalProcesses"))
            {
                _terminalProcesses = _settingsService.GetStringList("TerminalProcesses", DefaultTerminalProcesses);
            }
            else
            {
                _terminalProcesses = new List<string>(DefaultTerminalProcesses);
                _settingsService.SetStringList("TerminalProcesses", _terminalProcesses);
            }

            _logger?.Log($"{PluginName}: Loaded {_terminalProcesses.Count} terminal processes");
        }

        public override IEnumerable<string> GetHandledProcesses()
        {
            return _terminalProcesses;
        }

        public override void ShowSettingsDialog(IntPtr ownerHwnd)
        {
            try
            {
                var dialog = new TerminalSettingsWindow(_settingsService!, _terminalProcesses);
                dialog.Activate();
                dialog.Closed += (s, e) => ReloadSettings();
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to open Terminal Settings Window", ex);
            }
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

            using var automation = new UIA3Automation();

            foreach (var process in processes)
            {
                try
                {
                    var hwnd = process.MainWindowHandle;
                    if (hwnd == IntPtr.Zero) continue;
                    if (!NativeInterop.IsWindowVisible(hwnd)) continue;

                    string windowTitle = process.MainWindowTitle;
                    if (string.IsNullOrEmpty(windowTitle)) continue;

                    var tabs = ScanForTabs(hwnd, automation);

                    if (tabs.Count > 0)
                    {
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

        private List<string> ScanForTabs(IntPtr hwnd, UIA3Automation automation)
        {
            var tabs = new List<string>();
            try
            {
                var root = automation.FromHandle(hwnd);
                if (root == null) return tabs;

                var tabElements = root.FindAll(TreeScope.Descendants, new PropertyCondition(automation.PropertyLibrary.Element.ControlType, ControlType.TabItem));

                foreach (var element in tabElements)
                {
                    if (!string.IsNullOrWhiteSpace(element.Name) && element.Name != "New Tab" && element.Name != "+")
                    {
                        tabs.Add(element.Name);
                    }
                }
            }
            catch { }
            return tabs;
        }

        public override void ActivateWindow(WindowItem item)
        {
            NativeInterop.ForceForegroundWindow(item.Hwnd);
            if (item.Source == this && !string.IsNullOrEmpty(item.Title))
            {
                System.Threading.Thread.Sleep(50);
                try
                {
                    using var automation = new UIA3Automation();
                    var root = automation.FromHandle(item.Hwnd);
                    if (root == null) return;

                    var tabElement = root.FindFirst(TreeScope.Descendants, new PropertyCondition(automation.PropertyLibrary.Element.Name, item.Title)
                        .And(new PropertyCondition(automation.PropertyLibrary.Element.ControlType, ControlType.TabItem)));

                    if (tabElement != null)
                    {
                        if (tabElement.Patterns.SelectionItem.TryGetPattern(out var pattern))
                        {
                            pattern.Select();
                        }
                        else if (tabElement.Patterns.Invoke.TryGetPattern(out var invokePattern))
                        {
                            invokePattern.Invoke();
                        }
                        else
                        {
                            tabElement.Focus();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"{PluginName}: Error activating tab '{item.Title}'", ex);
                }
            }
        }
    }
}
