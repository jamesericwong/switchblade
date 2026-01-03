using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Interop;
using SwitchBlade.Contracts;

namespace SwitchBlade.Plugins.Chrome
{
    public class ChromeTabFinder : IWindowProvider
    {
        private ILogger? _logger;
        private PluginSettingsService? _settingsService;
        private List<string> _browserProcesses = new();

        // Default browser processes if no settings exist
        private static readonly List<string> DefaultBrowserProcesses = new()
        {
            "chrome","msedge","brave","vivaldi","opera","opera_gx","chromium","thorium","iron","epic","yandex","arc","comet"
        };

        public string PluginName => "ChromeTabFinder";
        public bool HasSettings => true;

        public ChromeTabFinder()
        {
        }

        public void Initialize(object settingsService, ILogger logger)
        {
            _logger = logger;
            _settingsService = new PluginSettingsService(PluginName);
            
            // Initialize settings from Registry or use defaults
            ReloadSettings();
        }

        public void ReloadSettings()
        {
            if (_settingsService == null) return;

            // Check if BrowserProcesses key exists in plugin Registry
            if (_settingsService.KeyExists("BrowserProcesses"))
            {
                _browserProcesses = _settingsService.GetStringList("BrowserProcesses", DefaultBrowserProcesses);
            }
            else
            {
                // First run or missing key - use defaults and save them
                _browserProcesses = new List<string>(DefaultBrowserProcesses);
                _settingsService.SetStringList("BrowserProcesses", _browserProcesses);
            }
            
            _logger?.Log($"ChromeTabFinder: Loaded {_browserProcesses.Count} browser processes");
        }

        public void ShowSettingsDialog(IntPtr ownerHwnd)
        {
            var dialog = new ChromeSettingsWindow(_settingsService!, _browserProcesses);
            if (ownerHwnd != IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(dialog);
                helper.Owner = ownerHwnd;
            }
            dialog.ShowDialog();
            
            // Reload settings after dialog closes
            ReloadSettings();
        }

        public IEnumerable<string> GetHandledProcesses()
        {
            _logger?.Log($"ChromeTabFinder Handled Processes: {string.Join(", ", _browserProcesses)}");
            return _browserProcesses;
        }

        public IEnumerable<WindowItem> GetWindows()
        {
            var results = new List<WindowItem>();
            if (_settingsService == null || _browserProcesses.Count == 0) return results;

            var targetProcessNames = new HashSet<string>(_browserProcesses, StringComparer.OrdinalIgnoreCase);
            var targetPids = new HashSet<int>();


            foreach (var name in targetProcessNames)
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    targetPids.Add(p.Id);
                }
            }

            var walker = TreeWalker.RawViewWalker;
            _logger?.Log($"--- Scan started at {DateTime.Now} ---");

            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                // Check visibility first for speed
                if (!NativeMethods.IsWindowVisible(hwnd)) return true;

                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (targetPids.Contains((int)pid))
                {
                    // Found a visible window belonging to one of our target browsers
                    ScanWindow(hwnd, (int)pid, walker, results);
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return results;
        }

        private void ScanWindow(IntPtr hwnd, int pid, TreeWalker walker, List<WindowItem> results)
        {
            AutomationElement? root = null;
            try
            {
                root = AutomationElement.FromHandle(hwnd);
            }
            catch { return; }

            if (root == null) return;

            // Get Process Name for the result item (expensive? maybe just cache it or look it up)
            string processName = "Unknown";
            try { processName = Process.GetProcessById(pid).ProcessName; } catch {}

            _logger?.Log($"Scanning Window HWND: {hwnd} (PID: {pid}, Name: {processName})");

            var foundTabs = FindTabsBFS(root, walker, maxDepth: 20);

            if (foundTabs.Count == 0)
            {
                 // Fallback: Add the main window if no tabs found
                 string title = "";
                 try { title = root.Current.Name; } catch {}

                if (!string.IsNullOrEmpty(title))
                {
                    results.Add(new WindowItem  
                    {  
                        Hwnd = hwnd,  
                        Title = title,  
                        ProcessName = processName,  
                        IsChromeTab = true, 
                        Source = this
                    });
                }
            }
            else
            {
                 _logger?.Log($"  Found {foundTabs.Count} tabs via BFS.");
                 foreach (var tab in foundTabs)
                 {
                     results.Add(new WindowItem
                     {
                         Hwnd = hwnd,
                         Title = tab,
                         ProcessName = processName,
                         IsChromeTab = true,
                         Source = this
                     });
                 }
            }
        }

        private List<string> FindTabsBFS(AutomationElement root, TreeWalker walker, int maxDepth)
        {
            var results = new List<string>();
            var queue = new Queue<(AutomationElement Element, int Depth)>();
            queue.Enqueue((root, 0));

            int itemsScanned = 0;

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                if (depth > maxDepth) continue;

                itemsScanned++;

                try
                {
                    // Optimization: Do not traverse into web content (Document control type)
                    // This prevents scanning thousands of DOM nodes, focusing on the browser "Chrome" UI.
                    if (current.Current.ControlType == ControlType.Document) continue;

                    bool isTab = false;
                    string name = current.Current.Name;
                    
                    if (current.Current.ControlType == ControlType.TabItem) isTab = true;

                    if (!isTab && !string.IsNullOrEmpty(current.Current.LocalizedControlType))
                    {
                        if (current.Current.LocalizedControlType.Equals("tab", StringComparison.OrdinalIgnoreCase)) isTab = true;
                    }

                    if (isTab)
                    {
                        if (!string.IsNullOrWhiteSpace(name) && name != "New Tab" && name != "+")
                        {
                            results.Add(name);
                            _logger?.Log($"    FOUND TAB: '{name}'");
                        }
                    }
                }
                catch { /* Element might be invalid now */ }

                // Get children
                try
                {
                    var child = walker.GetFirstChild(current);
                    while (child != null)
                    {
                        // Enqueue child only
                        queue.Enqueue((child, depth + 1));
                        child = walker.GetNextSibling(child);
                    }
                }
                catch { }
            }
            
            _logger?.Log($"  Items Scanned: {itemsScanned}");

            return results;
        }

        public void ActivateWindow(WindowItem item)
        {
            // NativeMethods.SetForegroundWindow(item.Hwnd); // Replaced with robust logic
            NativeMethods.ForceForegroundWindow(item.Hwnd);
            
            // Wait a brief moment for window to actually activate before searching for tabs
            // This is crucial because AutomationElement tree might not update instantly
            System.Threading.Thread.Sleep(50);
            
            if (string.IsNullOrEmpty(item.Title)) return;

            try 
            {
                AutomationElement? root = AutomationElement.FromHandle(item.Hwnd);
                if (root == null) return;

                var walker = TreeWalker.ControlViewWalker;
                var tabElement = FindTabByNameBFS(root, walker, 12, item.Title);

                if (tabElement != null)
                {
                    if (tabElement.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectPatternObj))
                    {
                        var selectPattern = (SelectionItemPattern)selectPatternObj;
                        selectPattern.Select();
                    }
                    else if (tabElement.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePatternObj))
                    {
                        var invokePattern = (InvokePattern)invokePatternObj;
                        invokePattern.Invoke();
                    }
                    else
                    {
                        tabElement.SetFocus();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error activating tab '{item.Title}'", ex);
            }
        }

        private AutomationElement? FindTabByNameBFS(AutomationElement root, TreeWalker walker, int maxDepth, string targetName)
        {
            var queue = new Queue<(AutomationElement Element, int Depth)>();
            queue.Enqueue((root, 0));

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                if (depth > maxDepth) continue;

                try
                {
                    bool isTab = false;
                    if (current.Current.ControlType == ControlType.TabItem) isTab = true;
                    if (!isTab && !string.IsNullOrEmpty(current.Current.LocalizedControlType))
                    {
                        if (current.Current.LocalizedControlType.Equals("tab", StringComparison.OrdinalIgnoreCase)) isTab = true;
                    }

                    if (isTab && current.Current.Name == targetName)
                    {
                        return current;
                    }
                }
                catch { }

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

    internal static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        public const int SW_RESTORE = 9;

        public static void ForceForegroundWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }

            uint dummyPid;
            uint foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), out dummyPid);
            uint myThreadId = GetCurrentThreadId();
            bool threadsAttached = false;

            if (foregroundThreadId != myThreadId)
            {
                threadsAttached = AttachThreadInput(myThreadId, foregroundThreadId, true);
            }

            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            
            if (threadsAttached)
            {
                AttachThreadInput(myThreadId, foregroundThreadId, false);
            }
        }
    }
}
