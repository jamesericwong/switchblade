using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;
using SwitchBlade.Services;

namespace SwitchBlade.Core
{
    public class ChromeTabFinder : IWindowProvider
    {
        private readonly SettingsService _settingsService;

        public ChromeTabFinder(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public IEnumerable<WindowItem> GetWindows()
        {
            var results = new List<WindowItem>();
            var processesToScan = _settingsService.Settings.BrowserProcesses;
            var walker = TreeWalker.RawViewWalker;
            
            // Debug logging to help identify why tabs are missed
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_tabs.log");
            try { System.IO.File.AppendAllText(logPath, $"--- Scan started at {DateTime.Now} ---{Environment.NewLine}"); } catch {}

            foreach (var processName in processesToScan)
            {
                Process[] processes = Process.GetProcessesByName(processName);

                foreach (var proc in processes)
                {
                    if (proc.MainWindowHandle == IntPtr.Zero) continue;
                    if (!Interop.IsWindowVisible(proc.MainWindowHandle)) continue;

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
                                IsChromeTab = true 
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
                                 IsChromeTab = true
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
                    // Logging for inspection (only first 100 items to avoid giant logs, or specific types)
                    // System.IO.File.AppendAllText(logPath, $"    [{depth}] {current.Current.ControlType.ProgrammaticName} - '{current.Current.Name}' ({current.Current.LocalizedControlType}){Environment.NewLine}");
                    
                    bool isTab = false;
                    string name = current.Current.Name;
                    
                    // Check 1: Standard ControlType
                    if (current.Current.ControlType == ControlType.TabItem) isTab = true;

                    // Check 2: Localized Control Type (e.g. "tab" string)
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

                    // Heuristic: If we hit a "Pane" that is the document content, we might want to stop recursing into it for performance?
                    // But "Document" control type is tricky.
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
            // First bring the window to front
            Interop.SetForegroundWindow(item.Hwnd);
            
            if (string.IsNullOrEmpty(item.Title)) return;

            try 
            {
                AutomationElement? root = AutomationElement.FromHandle(item.Hwnd);
                if (root == null) return;

                // Re-find the specific tab
                var walker = TreeWalker.RawViewWalker;
                var tabElement = FindTabByNameBFS(root, walker, 12, item.Title);

                if (tabElement != null)
                {
                    // Try to Select (SelectionItemPattern)
                    if (tabElement.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectPatternObj))
                    {
                        var selectPattern = (SelectionItemPattern)selectPatternObj;
                        selectPattern.Select();
                    }
                    // Or Invoke (InvokePattern) - sometimes tabs are buttons
                    else if (tabElement.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePatternObj))
                    {
                        var invokePattern = (InvokePattern)invokePatternObj;
                        invokePattern.Invoke();
                    }
                    else
                    {
                        // Fallback: Click it? or Focus?
                        tabElement.SetFocus();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error if needed
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_tabs.log");
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
                    // Check logic matches FindTabsBFS
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
}
