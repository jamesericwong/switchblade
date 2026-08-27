using System;
using System.Collections.Generic;
using System.Linq;
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
        private HashSet<string> _terminalProcesses = new(StringComparer.OrdinalIgnoreCase);

        // Default terminal processes if no settings exist
        private static readonly List<string> DefaultTerminalProcesses = new()
        {
            "WindowsTerminal"
        };

        public override string PluginName => "WindowsTerminalPlugin";
        public override bool HasSettings => true;
        public override bool IsUiaProvider => true;

        public override ISettingsControl? SettingsControl =>
            _settingsService != null
                ? new TerminalSettingsControlProvider(_settingsService, _terminalProcesses.ToList())
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
            if (_settingsService == null)
            {
                return;
            }

            // Check if TerminalProcesses key exists in plugin Registry
            if (_settingsService.KeyExists("TerminalProcesses"))
            {
                var loaded = _settingsService.GetStringList("TerminalProcesses", DefaultTerminalProcesses);
                _terminalProcesses = new HashSet<string>(loaded, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // First run or missing key - use defaults and save them
                _terminalProcesses = new HashSet<string>(DefaultTerminalProcesses, StringComparer.OrdinalIgnoreCase);
                _settingsService.SetStringList("TerminalProcesses", _terminalProcesses.ToList());
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
            if (_terminalProcesses.Count == 0)
            {
                return new List<WindowItem>();
            }

            // Map PID to list of window items found for that process
            var pidToResults = new Dictionary<int, List<WindowItem>>();
            var diagnostics = new ScanDiagnostics();

            NativeInterop.EnumWindows((hwnd, lParam) =>
            {
                if (!NativeInterop.IsWindowVisible(hwnd))
                {
                    return true;
                }

                NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
                var (procName, execPath) = NativeInterop.GetProcessInfo(pid);

                // O(1) HashSet lookup (comparer set at construction)
                if (_terminalProcesses.Contains(procName))
                {
                    var resultsForThisHandle = new List<WindowItem>();
                    ScanWindow(hwnd, (int)pid, procName, execPath, diagnostics, resultsForThisHandle);

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
            var results = DeduplicateResults(this, pidToResults);
            diagnostics.Report(_logger, PluginName, results.Count(r => !r.IsFallback));
            return results;
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

        private void ScanWindow(IntPtr hwnd, int pid, string processName, string? executablePath, ScanDiagnostics diagnostics, List<WindowItem> results)
        {
            // Get window title via native API
            Span<char> buffer = stackalloc char[512];
            int length = NativeInterop.GetWindowText(hwnd, buffer, buffer.Length);
            string windowTitle = length > 0 ? new string(buffer[..length]) : "";
            if (string.IsNullOrEmpty(windowTitle))
            {
                return;
            }

            RunTabScan(hwnd, pid, processName, executablePath, windowTitle, diagnostics, results, () => TryGetAutomationElement(hwnd, pid));
        }

        /// <summary>
        /// Core per-window tab scan. Transient UIA failures are handled inside the shared primitives
        /// (UiaSafe + ScanDiagnostics); any other failure is isolated to this window (TeamsPlugin / v1.9.16
        /// parity): logged, then the main-window fallback below still runs — one faulting window must never
        /// abort the whole run and discard sibling windows' results.
        /// </summary>
        internal void RunTabScan(IntPtr hwnd, int pid, string processName, string? executablePath, string windowTitle, ScanDiagnostics diagnostics, List<WindowItem> results, Func<AutomationElement?> resolveRoot)
        {
            var tabNames = new List<string>();

            try
            {
                // The resolver's strategy fallbacks are internal and never throw; transients are handled inside the scanner.
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
            }
            catch (Exception ex)
            {
                _logger?.LogError($"{PluginName}: Error scanning window {hwnd} (PID {pid})", ex);
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
            // All plugins share the canonical resilient defaults (UiaResolverOptions.Default):
            // bounded retries plus the ownership-verified FromPoint last resort.
            return UiaElementResolver.TryResolve(hwnd, pid, PluginName, _logger);
        }
    }
}
