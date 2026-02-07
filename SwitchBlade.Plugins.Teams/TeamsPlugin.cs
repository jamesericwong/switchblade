using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using SwitchBlade.Contracts;

[assembly: InternalsVisibleTo("SwitchBlade.Tests")]
[assembly: InternalsVisibleTo("SwitchBlade.Plugins.Teams.Tests")]

namespace SwitchBlade.Plugins.Teams
{
    /// <summary>
    /// Plugin that discovers and activates Microsoft Teams chat conversations.
    /// Uses UI Automation to find TreeItem elements in the Teams chat list.
    /// </summary>
    /// <remarks>
    /// Teams (v2) is a Chromium-based application. This plugin runs out-of-process
    /// via SwitchBlade.UiaWorker.exe to prevent memory leaks from COM RCW handling.
    /// </remarks>
    public class TeamsPlugin : CachingWindowProviderBase
    {
        private ILogger? _logger;
        private IPluginSettingsService? _settingsService;
        private List<string> _teamsProcesses = new();

        private static readonly List<string> DefaultTeamsProcesses = new()
        {
            "ms-teams"
        };

        public override string PluginName => "TeamsPlugin";
        public override bool HasSettings => true;
        public override bool IsUiaProvider => true;

        public override ISettingsControl? SettingsControl =>
            _settingsService != null
                ? new TeamsSettingsControlProvider(_settingsService, _teamsProcesses)
                : null;

        private static readonly HashSet<string> TeamsProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "ms-teams"
        };

        #region Regex Patterns

        // Extracted from user's PowerShell UIA scripts
        private static readonly Regex UnreadPrefixRegex = new(@"^Unread message ", RegexOptions.Compiled);

        // "Chat [Name] [Status]" - Individual chats
        private static readonly Regex IndividualChatRegex = new(
            @"^Chat (.+?) (Available|Away|Busy|Do not disturb|Offline|Be right back|Appear offline|Has pinned)",
            RegexOptions.Compiled);

        // "Group chat [Name] Last message..." - Group chats
        private static readonly Regex GroupChatRegex = new(
            @"^Group chat (.+?) Last message",
            RegexOptions.Compiled);

        // "Meeting chat [Name] Last message..." - Meeting chats
        private static readonly Regex MeetingChatRegex = new(
            @"^Meeting chat (.+?) Last message",
            RegexOptions.Compiled);

        #endregion

        public TeamsPlugin() { }

        // Constructor for unit testing
        public TeamsPlugin(IPluginSettingsService settingsService)
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

            if (_settingsService.KeyExists("TeamsProcesses"))
            {
                _teamsProcesses = _settingsService.GetStringList("TeamsProcesses", DefaultTeamsProcesses);
            }
            else
            {
                _teamsProcesses = new List<string>(DefaultTeamsProcesses);
                _settingsService.SetStringList("TeamsProcesses", _teamsProcesses);
            }
        }

        public override IEnumerable<string> GetHandledProcesses()
        {
            return _teamsProcesses;
        }

        protected override IEnumerable<WindowItem> ScanWindowsCore()
        {
            var results = new List<WindowItem>();
            var targetProcessNames = new HashSet<string>(_teamsProcesses, StringComparer.OrdinalIgnoreCase);

            if (targetProcessNames.Count == 0) return results;

            NativeInterop.EnumWindows((hwnd, lParam) =>
            {
                if (!NativeInterop.IsWindowVisible(hwnd)) return true;

                NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
                var (procName, execPath) = NativeInterop.GetProcessInfo(pid);

                if (targetProcessNames.Contains(procName))
                {
                    ScanTeamsWindow(hwnd, (int)pid, procName, execPath, results);
                }

                return true;
            }, IntPtr.Zero);

            return results;
        }

        private void ScanTeamsWindow(IntPtr hwnd, int pid, string processName, string? executablePath, List<WindowItem> results)
        {
            try
            {
                // Get window title for fallback
                Span<char> buffer = stackalloc char[512];
                int length = NativeInterop.GetWindowText(hwnd, buffer, buffer.Length);
                string windowTitle = length > 0 ? new string(buffer[..length]) : "";
                if (string.IsNullOrEmpty(windowTitle)) return;

                var initialCount = results.Count;

                var root = AutomationElement.FromHandle(hwnd);
                if (root == null) return;

                // Use surgical BFS to find TreeItem elements (chat entries)
                var cacheRequest = new CacheRequest();
                cacheRequest.Add(AutomationElement.NameProperty);
                cacheRequest.Add(AutomationElement.ControlTypeProperty);
                cacheRequest.TreeScope = TreeScope.Element | TreeScope.Children;

                using (cacheRequest.Activate())
                {
                    var queue = new Queue<AutomationElement>();
                    queue.Enqueue(root);

                    int checkedCount = 0;
                    const int MaxElements = 500;

                    while (queue.Count > 0 && checkedCount < MaxElements)
                    {
                        var current = queue.Dequeue();
                        checkedCount++;

                        AutomationElementCollection? children = null;
                        try
                        {
                            children = current.FindAll(TreeScope.Children, Condition.TrueCondition);
                        }
                        catch { continue; }

                        foreach (AutomationElement child in children)
                        {
                            var controlType = child.Cached.ControlType;
                            var rawName = child.Cached.Name;

                            if (controlType == ControlType.TreeItem && !string.IsNullOrWhiteSpace(rawName))
                            {
                                var chatInfo = ParseChatName(rawName);
                                if (chatInfo != null)
                                {
                                    results.Add(new WindowItem
                                    {
                                        Hwnd = hwnd,
                                        Title = chatInfo.Value.Name,
                                        ProcessName = $"Teams ({chatInfo.Value.Type})",
                                        ExecutablePath = executablePath,
                                        Source = this
                                    });
                                }
                            }

                            // Prune known dead-end control types
                            if (controlType != ControlType.Document && controlType != ControlType.Header)
                            {
                                queue.Enqueue(child);
                            }
                        }
                    }
                }

                // Fallback: If no chats were added for this window, add the window itself
                if (results.Count == initialCount)
                {
                    _logger?.Log($"TeamsPlugin: No chats found for PID {pid}, returning main window");
                    results.Add(new WindowItem
                    {
                        Hwnd = hwnd,
                        Title = windowTitle,
                        ProcessName = processName,
                        ExecutablePath = executablePath,
                        Source = this
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"TeamsPlugin: Error scanning window {hwnd}", ex);
            }
        }

        public (string Name, string Type, bool IsUnread)? ParseChatName(string rawName)
        {
            bool isUnread = false;
            var cleanName = rawName;

            // Strip "Unread message " prefix if present
            var unreadMatch = UnreadPrefixRegex.Match(cleanName);
            if (unreadMatch.Success)
            {
                isUnread = true;
                cleanName = cleanName.Substring(unreadMatch.Length);
            }

            // Try Individual chat pattern
            var indMatch = IndividualChatRegex.Match(cleanName);
            if (indMatch.Success)
            {
                return (indMatch.Groups[1].Value, "Individual", isUnread);
            }

            // Try Group chat pattern
            var groupMatch = GroupChatRegex.Match(cleanName);
            if (groupMatch.Success)
            {
                return (groupMatch.Groups[1].Value, "Group", isUnread);
            }

            // Try Meeting chat pattern
            var meetMatch = MeetingChatRegex.Match(cleanName);
            if (meetMatch.Success)
            {
                return (meetMatch.Groups[1].Value, "Meeting", isUnread);
            }

            return null;
        }

        public override void ActivateWindow(WindowItem item)
        {
            // 1. Bring Teams window to foreground
            NativeInterop.ForceForegroundWindow(item.Hwnd);

            // Special case for fallback window (Source is this, but not a parsed chat)
            // We identify parsed chats by the specific "Teams (...)" ProcessName format
            // If it's just the raw process name, we stop here (main window already focused)
            if (item.ProcessName == "ms-teams" || !_teamsProcesses.Contains(item.ProcessName.Split(' ')[0], StringComparer.OrdinalIgnoreCase))
            {
                // This check is a bit simplistic, but fallback items just have the raw process name usually or whatever we passed.
                // Actually in ScanWindow fallback we use 'processName' which is e.g. "ms-teams"
                // And in chat items we use $"Teams ({type})"
                if (!item.ProcessName.StartsWith("Teams ("))
                {
                    return;
                }
            }

            // 2. Re-discover the specific chat element and activate it
            try
            {
                var root = AutomationElement.FromHandle(item.Hwnd);
                if (root == null) return;

                var targetElement = FindChatElement(root, item.Title);

                if (targetElement != null)
                {
                    bool success = false;

                    // Method 1: InvokePattern (for clickable items)
                    if (!success && targetElement.TryGetCurrentPattern(InvokePattern.Pattern, out var invokeObj))
                    {
                        try
                        {
                            ((InvokePattern)invokeObj).Invoke();
                            success = true;
                            _logger?.Log($"TeamsPlugin: Invoked chat '{item.Title}'");
                        }
                        catch { }
                    }

                    // Method 2: SelectionItemPattern (verified working on Teams)
                    if (!success && targetElement.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectObj))
                    {
                        try
                        {
                            ((SelectionItemPattern)selectObj).Select();
                            success = true;
                            _logger?.Log($"TeamsPlugin: Selected chat '{item.Title}'");
                        }
                        catch { }
                    }

                    // Method 3: ExpandCollapsePattern (for expandable tree items)
                    if (!success && targetElement.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj))
                    {
                        try
                        {
                            ((ExpandCollapsePattern)expandObj).Expand();
                            success = true;
                            _logger?.Log($"TeamsPlugin: Expanded chat '{item.Title}'");
                        }
                        catch { }
                    }

                    // Method 4: SetFocus fallback
                    if (!success)
                    {
                        try
                        {
                            targetElement.SetFocus();
                            _logger?.Log($"TeamsPlugin: Focused chat '{item.Title}' (Fallback)");
                        }
                        catch { }
                    }
                }
                else
                {
                    _logger?.Log($"TeamsPlugin Error: Could not find chat element '{item.Title}' during activation.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"TeamsPlugin: Error activating window {item.Hwnd}", ex);
            }
        }

        private AutomationElement? FindChatElement(AutomationElement root, string targetTitle)
        {
            var cacheRequest = new CacheRequest();
            cacheRequest.Add(AutomationElement.NameProperty);
            cacheRequest.Add(AutomationElement.ControlTypeProperty);
            cacheRequest.TreeScope = TreeScope.Element | TreeScope.Children;

            using (cacheRequest.Activate())
            {
                var queue = new Queue<AutomationElement>();
                queue.Enqueue(root);

                int checkedCount = 0;
                const int MaxElements = 500;

                while (queue.Count > 0 && checkedCount < MaxElements)
                {
                    var current = queue.Dequeue();
                    checkedCount++;

                    AutomationElementCollection? children = null;
                    try { children = current.FindAll(TreeScope.Children, Condition.TrueCondition); }
                    catch { continue; }

                    foreach (AutomationElement child in children)
                    {
                        var controlType = child.Cached.ControlType;
                        var rawName = child.Cached.Name;

                        if (controlType == ControlType.TreeItem && !string.IsNullOrWhiteSpace(rawName))
                        {
                            var chatInfo = ParseChatName(rawName);
                            if (chatInfo != null && chatInfo.Value.Name == targetTitle)
                            {
                                return child;
                            }
                        }

                        if (controlType != ControlType.Document && controlType != ControlType.Header)
                        {
                            queue.Enqueue(child);
                        }
                    }
                }
            }
            return null;
        }
    }
}
