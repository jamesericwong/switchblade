using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Interop;
using SwitchBlade.Contracts;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SwitchBlade.Tests")]

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

        private static readonly HashSet<string> ExcludedTabNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "All",
            "Tab search",
            "Search Tabs",
            "Google Chrome",
            "Chrome",
            "Side panel",
            "Active GitLab Duo Chat"
        };



        public override string PluginName => "ChromeTabFinder";
        public override bool HasSettings => true;
        public override bool IsUiaProvider => true;

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

            // Use injected settings if available (v1.9.3+), fallback to self-instantiation
            _settingsService = context.Settings;

            // Initialize settings from Registry or use defaults
            ReloadSettings();
        }

        public override void ReloadSettings()
        {
            if (_settingsService == null)
            {
                return;
            }

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
            if (_settingsService == null || _browserProcesses.Count == 0)
            {
                return results;
            }

            // Optimization: Don't pre-fetch PIDs using Process.GetProcessesByName (expensive).
            // Instead, we will check process names dynamically inside the EnumWindows loop using our cached native helper.

            var diagnostics = new ScanDiagnostics();
            _logger?.Log($"--- Scan started at {DateTime.Now} in Process {Environment.ProcessId} ---");

            NativeInterop.EnumWindows((hwnd, lParam) =>
            {
                // Check visibility first for speed
                if (!NativeInterop.IsWindowVisible(hwnd))
                {
                    return true;
                }

                NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
                var (procName, execPath) = NativeInterop.GetProcessInfo(pid);

                // O(1) HashSet lookup (comparer set at construction)
                if (_browserProcesses.Contains(procName))
                {
                    // Found a visible window belonging to one of our target browsers
                    ScanWindow(hwnd, (int)pid, procName, execPath, diagnostics, results);
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            diagnostics.Report(_logger, PluginName, results.Count(r => !r.IsFallback));

            return results;
        }

        private void ScanWindow(IntPtr hwnd, int pid, string processName, string? executablePath, ScanDiagnostics diagnostics, List<WindowItem> results)
        {
            // Get window title via Win32 (reliable even when UIA fails)
            Span<char> buffer = stackalloc char[512];
            int length = NativeInterop.GetWindowText(hwnd, buffer, buffer.Length);
            string windowTitle = length > 0 ? new string(buffer[..length]) : "";
            if (string.IsNullOrEmpty(windowTitle))
            {
                return;
            }

            RunTabScan(hwnd, pid, processName, executablePath, windowTitle, diagnostics, results,
                () => UiaElementResolver.TryResolve(hwnd, pid, PluginName, _logger));
        }

        /// <summary>
        /// Core per-window tab scan. Transient UIA failures are handled inside the shared primitives
        /// (UiaSafe + ScanDiagnostics); any other failure is isolated to this window (TeamsPlugin / v1.9.16
        /// parity): logged, then the main-window fallback below still runs — one faulting window must never
        /// abort the whole run and discard sibling windows' results.
        /// </summary>
        internal void RunTabScan(IntPtr hwnd, int pid, string processName, string? executablePath, string windowTitle, ScanDiagnostics diagnostics, List<WindowItem> results, Func<AutomationElement?> resolveRoot)
        {
            var initialCount = results.Count;

            try
            {
                // Use UiaElementResolver for robust multi-strategy element acquisition
                var root = resolveRoot();
                if (root == null)
                {
                    _logger?.Log($"{PluginName}: All UIA strategies failed for PID {pid}, marking scan as failed.");
                }
                else
                {
                    _logger?.Log($"Scanning Window HWND: {hwnd} (PID: {pid}, Name: {processName})");

                    foreach (var tab in UiaTabScanner.FindTabs(root, diagnostics))
                    {
                        var name = UiaTabScanner.GetTabName(tab, diagnostics);
                        if (!string.IsNullOrWhiteSpace(name) && !ExcludedTabNames.Contains(name) && name != "New Tab" && name != "+")
                        {
                            results.Add(new WindowItem
                            {
                                Hwnd = hwnd,
                                Title = name,
                                ProcessName = processName,
                                ExecutablePath = executablePath,
                                Source = this
                            });

                            _logger?.Log($"    FOUND TAB: '{name}'");
                        }
                    }

                    if (results.Count > initialCount)
                    {
                        _logger?.Log($"  Found {results.Count - initialCount} tabs.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"{PluginName}: Error scanning window {hwnd} (PID {pid})", ex);
            }

            // FALLBACK: If no tabs were added for this window (UIA failed OR found 0 tabs),
            // always return the main window so LKG cache sees the PID as alive.
            if (results.Count == initialCount)
            {
                _logger?.Log($"{PluginName}: No tabs found for PID {pid}, returning main window as fallback");
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
            // Use shared NativeInterop for robust window activation
            NativeInterop.ForceForegroundWindow(item.Hwnd);

            // Wait a brief moment for window to actually activate before searching for tabs
            // This is crucial because AutomationElement tree might not update instantly
            System.Threading.Thread.Sleep(50);

            if (string.IsNullOrEmpty(item.Title))
            {
                return;
            }

            // The resolver never throws (its strategy fallbacks are internal) and the shared scanner/activator
            // handle transient UIA failures via UiaSafe, so no blanket catch is needed here.
            NativeInterop.GetWindowThreadProcessId(item.Hwnd, out uint pid);
            var root = UiaElementResolver.TryResolve(item.Hwnd, (int)pid, PluginName, _logger);
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

    // NativeMethods class removed - now using SwitchBlade.Contracts.NativeInterop
}
