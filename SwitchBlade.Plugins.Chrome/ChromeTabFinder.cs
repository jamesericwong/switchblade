using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;
using SwitchBlade.Contracts;

namespace SwitchBlade.Plugins.Chrome
{
    public class ChromeTabFinder : IWindowProvider
    {
        private IBrowserSettingsProvider? _settingsService;

        public ChromeTabFinder()
        {
        }

        public void Initialize(object settingsService)
        {
           if (settingsService is IBrowserSettingsProvider service)
           {
               _settingsService = service;
           }
        }

        private static readonly object _logLock = new object();

        public IEnumerable<WindowItem> GetWindows()
        {
            var results = new List<WindowItem>();
            if (_settingsService == null) return results;

            var targetProcessNames = new HashSet<string>(_settingsService.BrowserProcesses, StringComparer.OrdinalIgnoreCase);
            var targetPids = new HashSet<int>();

            foreach (var name in targetProcessNames)
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    targetPids.Add(p.Id);
                }
            }

            var walker = TreeWalker.RawViewWalker;
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "switchblade_debug_tabs.log");
            
            try { lock (_logLock) { System.IO.File.AppendAllText(logPath, $"--- Scan started at {DateTime.Now} ---{Environment.NewLine}"); } } catch {}

            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                // Check visibility first for speed
                if (!NativeMethods.IsWindowVisible(hwnd)) return true;

                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (targetPids.Contains((int)pid))
                {
                    // Found a visible window belonging to one of our target browsers
                    ScanWindow(hwnd, (int)pid, walker, logPath, results);
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return results;
        }

        private void ScanWindow(IntPtr hwnd, int pid, TreeWalker walker, string logPath, List<WindowItem> results)
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

            try { lock (_logLock) { System.IO.File.AppendAllText(logPath, $"Scanning Window HWND: {hwnd} (PID: {pid}, Name: {processName}){Environment.NewLine}"); } } catch {}

            var foundTabs = FindTabsBFS(root, walker, maxDepth: 20, logPath);

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
                 try { lock (_logLock) { System.IO.File.AppendAllText(logPath, $"  Found {foundTabs.Count} tabs via BFS.{Environment.NewLine}"); } } catch {}
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

        private List<string> FindTabsBFS(AutomationElement root, TreeWalker walker, int maxDepth, string logPath)
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
                            try { lock (_logLock) { System.IO.File.AppendAllText(logPath, $"    FOUND TAB: '{name}'{Environment.NewLine}"); } } catch {}
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
            
            try { lock (_logLock) { System.IO.File.AppendAllText(logPath, $"  Items Scanned: {itemsScanned}{Environment.NewLine}"); } } catch {}

            return results;
        }

        public void ActivateWindow(WindowItem item)
        {
            NativeMethods.SetForegroundWindow(item.Hwnd);
            
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
                var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "switchblade_debug_tabs.log");
                try { System.IO.File.AppendAllText(logPath, $"Error activating tab '{item.Title}': {ex.Message}{Environment.NewLine}"); } catch {}
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
    }
}
