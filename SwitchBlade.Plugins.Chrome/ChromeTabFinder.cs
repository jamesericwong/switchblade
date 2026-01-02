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

        public IEnumerable<WindowItem> GetWindows()
        {
            var results = new List<WindowItem>();
            if (_settingsService == null) return results;

            var processesToScan = _settingsService.BrowserProcesses;
            var walker = TreeWalker.RawViewWalker;
            
            // Debug logging to help identify why tabs are missed
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "switchblade_debug_tabs.log");
            try { System.IO.File.AppendAllText(logPath, $"--- Scan started at {DateTime.Now} ---{Environment.NewLine}"); } catch {}

            foreach (var processName in processesToScan)
            {
                Process[] processes = Process.GetProcessesByName(processName);

                foreach (var proc in processes)
                {
                    if (proc.MainWindowHandle == IntPtr.Zero) continue;
                    // Note: Interop class is internal to core. We need minimal Interop here or use P/Invoke directly.
                    // For now, let's assume P/Invoke is needed or we duplicate specific Interop helper.
                    // Or we just reference user32 directly if simple. 
                    // Let's rely on standard .NET where possible, but IsWindowVisible needs P/Invoke.
                    // I'll add a minimal P/Invoke class here to keep it self-contained.
                    if (!NativeMethods.IsWindowVisible(proc.MainWindowHandle)) continue;

                    AutomationElement? root = null;
                    try
                    {
                        root = AutomationElement.FromHandle(proc.MainWindowHandle);
                    }
                    catch { continue; }

                    if (root == null) continue;

                    try { System.IO.File.AppendAllText(logPath, $"Scanning process: {processName} (PID: {proc.Id}, HWND: {proc.MainWindowHandle}){Environment.NewLine}"); } catch {}

                    // Manual BFS to find TabItems. 
                    var foundTabs = FindTabsBFS(root, walker, maxDepth: 12, logPath); // Increased depth

                    if (foundTabs.Count == 0)
                    {
                        try { System.IO.File.AppendAllText(logPath, $"  No tabs found via BFS. Using Fallback: {proc.MainWindowTitle}{Environment.NewLine}"); } catch {}
                        
                         // Fallback: Add the main window if no tabs found
                        if (!string.IsNullOrEmpty(proc.MainWindowTitle))
                        {
                            results.Add(new WindowItem  
                            {  
                                Hwnd = proc.MainWindowHandle,  
                                Title = proc.MainWindowTitle,  
                                ProcessName = proc.ProcessName,  
                                IsChromeTab = true, // We can keep this flag
                                Source = this
                            });
                        }
                    }
                    else
                    {
                         try { System.IO.File.AppendAllText(logPath, $"  Found {foundTabs.Count} tabs via BFS.{Environment.NewLine}"); } catch {}
                         foreach (var tab in foundTabs)
                         {
                             results.Add(new WindowItem
                             {
                                 Hwnd = proc.MainWindowHandle,
                                 Title = tab,
                                 ProcessName = proc.ProcessName,
                                 IsChromeTab = true,
                                 Source = this
                             });
                         }
                    }
                }
            }
            return results;
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
                            System.IO.File.AppendAllText(logPath, $"    FOUND TAB: '{name}'{Environment.NewLine}");
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
                        queue.Enqueue((child, depth + 1));
                        child = walker.GetNextSibling(child);
                    }
                }
                catch { }
            }
            
            try { System.IO.File.AppendAllText(logPath, $"  Items Scanned: {itemsScanned}{Environment.NewLine}"); } catch {}

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

                var walker = TreeWalker.RawViewWalker;
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
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
