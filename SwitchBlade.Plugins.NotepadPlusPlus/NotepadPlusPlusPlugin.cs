using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Interop;
using SwitchBlade.Contracts;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SwitchBlade.Tests")]

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
            _settingsService = context.Settings;

            ReloadSettings();
        }

        public override void ReloadSettings()
        {
            if (_settingsService == null)
            {
                return;
            }

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
            if (_nppProcesses.Count == 0)
            {
                return results;
            }

            _logger?.Log($"{PluginName}: --- Scan started at {DateTime.Now} ---");

            var diagnostics = new ScanDiagnostics();

            // Use native EnumWindows + cached GetProcessInfo for efficiency
            NativeInterop.EnumWindows((hwnd, lParam) =>
            {
                // Check visibility first for speed
                if (!NativeInterop.IsWindowVisible(hwnd))
                {
                    return true;
                }

                NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
                var (procName, execPath) = NativeInterop.GetProcessInfo(pid);

                // O(1) HashSet lookup instead of O(n) list search
                if (_nppProcesses.Contains(procName))
                {
                    ScanWindow(hwnd, (int)pid, procName, execPath, diagnostics, results);
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            diagnostics.Report(_logger, PluginName, results.Count(r => !r.IsFallback));

            return results;
        }

        private void ScanWindow(IntPtr hwnd, int pid, string processName, string? executablePath, ScanDiagnostics diagnostics, List<WindowItem> results)
        {
            RunTabScan(hwnd, pid, processName, executablePath, diagnostics, results, () => TryGetAutomationElement(hwnd, pid));
        }

        /// <summary>
        /// Core per-window tab scan. Transient UIA failures are handled inside the shared primitives
        /// (UiaSafe + ScanDiagnostics); non-transient exceptions propagate to the scan coordinator's
        /// error path instead of being swallowed here.
        /// </summary>
        internal void RunTabScan(IntPtr hwnd, int pid, string processName, string? executablePath, ScanDiagnostics diagnostics, List<WindowItem> results, Func<AutomationElement?> resolveRoot)
        {
            var tabNames = new List<string>();

            // Safe UIA access to handle E_FAIL (the resolver's strategy fallbacks are internal and never throw)
            var root = resolveRoot();
            if (root != null)
            {
                foreach (var tab in UiaTabScanner.FindTabs(root, diagnostics))
                {
                    var name = UiaTabScanner.GetTabName(tab, diagnostics);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        tabNames.Add(name);
                    }
                }
            }

            if (tabNames.Count > 0)
            {
                _logger?.Log($"{PluginName}: Found {tabNames.Count} tabs in PID {pid}");
                foreach (var tabName in tabNames)
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

        public override void ActivateWindow(WindowItem item)
        {
            // First, bring the main window to foreground
            NativeInterop.ForceForegroundWindow(item.Hwnd);

            // If this item was created by this plugin and has a title, try to select the specific tab
            if (item.Source == this && !string.IsNullOrEmpty(item.Title))
            {
                System.Threading.Thread.Sleep(50); // Brief wait for window activation

                // The resolver never throws and the shared scanner/activator handle transient UIA failures
                // via UiaSafe, so no blanket catch is needed here.
                NativeInterop.GetWindowThreadProcessId(item.Hwnd, out uint pid);
                var root = TryGetAutomationElement(item.Hwnd, (int)pid);
                if (root == null)
                {
                    return;
                }

                var tabElement = UiaTabScanner.FindTabs(root).FirstOrDefault(tab =>
                    string.Equals(UiaTabScanner.GetTabName(tab), item.Title, StringComparison.Ordinal));

                if (tabElement != null)
                {
                    ElementActivator.TryActivate(tabElement, UiaActivationStrategy.SelectionItem, UiaActivationStrategy.Invoke);
                }
            }
        }

        private AutomationElement? TryGetAutomationElement(IntPtr hwnd, int pid)
        {
            return UiaElementResolver.TryResolve(hwnd, pid, PluginName, _logger);
        }
    }
}
