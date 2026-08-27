using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Automation;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Shared tab discovery for UIA-based plugins (Chrome, Windows Terminal, Notepad++):
    /// breadth-first traversal with Document pruning plus a native Descendants fallback.
    /// Unifies the tab-detection literals that previously drifted across plugins — Chrome matched
    /// LocalizedControlType "tab" while Terminal/Notepad++ matched "tab item", so at least one variant
    /// silently yielded zero tabs in some environments. Both variants are accepted here, case-insensitively.
    /// </summary>
    public static class UiaTabScanner
    {
        /// <summary>Unified BFS safety cap (the maximum of the historical per-plugin caps: 50 / 100 / 200).</summary>
        public const int DefaultMaxContainers = 200;

        private const string TabLiteral_Tab = "tab";
        private const string TabLiteral_TabItem = "tab item";

        private static readonly HashSet<string> TabLocalizedControlTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            TabLiteral_Tab,     // Chrome-family browsers report LocalizedControlType "tab"
            TabLiteral_TabItem  // Windows Terminal / Notepad++ report "tab item"
        };

        // Server-side filter: prevents creation of RCWs for heavy Document nodes.
        private static readonly Condition NotDocumentCondition = new NotCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));

        /// <summary>
        /// Determines whether an element is a tab by control type or localized control type literal.
        /// Pure function of the two property values so it can be unit-tested without a live UIA tree.
        /// </summary>
        /// <remarks>
        /// A <see cref="ControlType.Tab"/> container (Notepad++'s SysTabControl32, Windows Terminal's XAML TabView)
        /// reports LocalizedControlType "tab" and holds the real TabItem entries as children. It must NOT be
        /// collected by the literal — FindTabs treats matched elements as leaves it never descends into, so
        /// collecting the container would swallow its tab items (v1.9.17 zero-tab regression). ControlType.Tab
        /// stays expandable; its children are still matched via TabItem or the "tab item" literal.
        /// </remarks>
        public static bool IsTabElement(ControlType? controlType, string? localizedControlType) =>
            controlType == ControlType.TabItem ||
            (localizedControlType != null &&
             TabLocalizedControlTypes.Contains(localizedControlType) &&
             controlType != ControlType.Tab);

        /// <summary>
        /// Finds tab elements under <paramref name="root"/> using BFS with Document pruning; falls back to a
        /// native Descendants search when the traversal finds nothing. Returns matched elements in discovery order.
        /// </summary>
        [ExcludeFromCodeCoverage] // Requires a live UIA tree; decision logic is covered via IsTabElement and UiaSafe tests.
        public static List<AutomationElement> FindTabs(AutomationElement root, ScanDiagnostics? diagnostics = null) =>
            FindTabs(root, diagnostics, DefaultMaxContainers);

        /// <summary>
        /// Finds tab elements under <paramref name="root"/> bounded by <paramref name="maxContainers"/> BFS containers.
        /// </summary>
        [ExcludeFromCodeCoverage] // Requires a live UIA tree; decision logic is covered via IsTabElement and UiaSafe tests.
        public static List<AutomationElement> FindTabs(AutomationElement root, ScanDiagnostics? diagnostics, int maxContainers)
        {
            var tabs = new List<AutomationElement>();

            var cacheRequest = new CacheRequest();
            cacheRequest.Add(AutomationElement.NameProperty);
            cacheRequest.Add(AutomationElement.ControlTypeProperty);
            cacheRequest.Add(AutomationElement.LocalizedControlTypeProperty);
            cacheRequest.TreeScope = TreeScope.Element | TreeScope.Children;

            using (cacheRequest.Activate())
            {
                var queue = new Queue<AutomationElement>();
                queue.Enqueue(root);

                int containersChecked = 0;
                while (queue.Count > 0 && containersChecked < maxContainers)
                {
                    var container = queue.Dequeue();
                    containersChecked++;

                    foreach (var child in GetChildren(container, diagnostics))
                    {
                        var controlType = ReadControlType(child, diagnostics);
                        if (controlType == ControlType.Document)
                        {
                            continue; // Prune heavy branches on the walker path (server-side filter does not apply there).
                        }

                        if (IsTabElement(controlType, ReadLocalizedControlType(child, diagnostics)))
                        {
                            tabs.Add(child); // Tabs are leaves of interest: collect, do not descend.
                        }
                        else if (controlType != null)
                        {
                            queue.Enqueue(child); // Unreadable children are skipped (historical per-plugin behavior).
                        }
                    }
                }
            }

            if (tabs.Count == 0)
            {
                // Native Descendants search with the unified literal set (robust when BFS is blocked).
                var condition = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
                    new OrCondition(
                        new PropertyCondition(AutomationElement.LocalizedControlTypeProperty, TabLiteral_Tab),
                        new PropertyCondition(AutomationElement.LocalizedControlTypeProperty, TabLiteral_TabItem)));

                UiaSafe.TryRun(() =>
                {
                    foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, condition))
                    {
                        tabs.Add(element);
                    }
                }, diagnostics);
            }

            return tabs;
        }

        /// <summary>
        /// Reads a tab's display name tolerating transient failures and cache misses (see
        /// <see cref="TryReadCached"/>). Returns null when unavailable.
        /// </summary>
        [ExcludeFromCodeCoverage] // Requires a live UIA element.
        public static string? GetTabName(AutomationElement element, ScanDiagnostics? diagnostics = null)
        {
            if (TryReadCached(() => element.Cached.Name, () => element.Current.Name, diagnostics, out var name))
            {
                return name;
            }

            return null;
        }

        private static List<AutomationElement> GetChildren(AutomationElement container, ScanDiagnostics? diagnostics)
        {
            if (UiaSafe.TryGet(() => container.FindAll(TreeScope.Children, NotDocumentCondition), diagnostics, out var collection))
            {
                var children = new List<AutomationElement>();
                foreach (AutomationElement child in collection)
                {
                    children.Add(child);
                }

                return children;
            }

            // Transient FindAll failure: manual RawView walk (historical fallback for suspended tabs / RPC faults).
            var walker = TreeWalker.RawViewWalker;
            var walked = new List<AutomationElement>();
            AutomationElement? current = UiaSafe.TryGet(() => walker.GetFirstChild(container), diagnostics, out var first) ? first : null;

            while (current != null)
            {
                walked.Add(current);
                if (!UiaSafe.TryGet(() => walker.GetNextSibling(current), diagnostics, out var next))
                {
                    break; // Transient failure mid-walk: the partial result is still usable.
                }

                current = next;
            }

            return walked;
        }

        /// <summary>
        /// Reads a property from the element's cache context, falling back to a live read. Elements obtained via
        /// the RawViewWalker fallback path in <see cref="GetChildren"/> never received a cache context: accessing
        /// .Cached.* on them throws InvalidOperationException ("not cached"), which is an expected condition of that
        /// path rather than a transient COM failure and must not abort the scan. v1.9.17 regression: Comet's
        /// faulting window produced exactly this and killed the whole plugin run; v1.9.16 fell back to live reads.
        /// </summary>
        private static bool TryReadCached<T>(Func<T> cachedAccess, Func<T> liveAccess, ScanDiagnostics? diagnostics, out T value)
        {
            try
            {
                if (UiaSafe.TryGet(cachedAccess, diagnostics, out value))
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // Element carries no cache context — fall through to the live read below.
            }

            return UiaSafe.TryGet(liveAccess, diagnostics, out value);
        }

        private static ControlType? ReadControlType(AutomationElement element, ScanDiagnostics? diagnostics)
        {
            if (TryReadCached(() => element.Cached.ControlType, () => element.Current.ControlType, diagnostics, out var controlType))
            {
                return controlType;
            }

            return null;
        }

        private static string? ReadLocalizedControlType(AutomationElement element, ScanDiagnostics? diagnostics)
        {
            if (TryReadCached(() => element.Cached.LocalizedControlType, () => element.Current.LocalizedControlType, diagnostics, out var localized))
            {
                return localized;
            }

            return null;
        }
    }
}
